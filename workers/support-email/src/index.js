const FIREBASE_JWKS_URL =
  "https://www.googleapis.com/service_accounts/v1/jwk/securetoken@system.gserviceaccount.com";
const RESEND_EMAIL_URL = "https://api.resend.com/emails";
const MAX_ATTACHMENT_COUNT = 2;
const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024;
const MAX_SUBJECT_LENGTH = 120;
const MAX_BODY_LENGTH = 4000;

let signingKeys = new Map();
let signingKeysExpireAt = 0;

export default {
  async fetch(request, env, context) {
    if (request.method === "OPTIONS")
      return jsonResponse(204, null);

    const url = new URL(request.url);
    if (request.method === "GET" && (url.pathname === "/" || url.pathname === "/health")) {
      return jsonResponse(200, { ok: true, service: "gravity-support-email" });
    }

    if (request.method !== "POST" || url.pathname !== "/support")
      return jsonResponse(404, { error: "Route not found" });

    try {
      if (!env.RESEND_API_KEY)
        throw new HttpError(503, "Email service is not configured");

      const token = bearerToken(request.headers.get("Authorization"));
      const player = await verifyFirebaseIdToken(token, env.FIREBASE_PROJECT_ID);
      await enforceShortRateLimit(player.uid, context);

      const payload = await readRequestPayload(request);
      const requestId = cleanText(payload.requestId, 64);
      const subject = cleanText(payload.subject, MAX_SUBJECT_LENGTH);
      const body = cleanText(payload.body, MAX_BODY_LENGTH);
      if (!/^[a-f0-9]{32}$/.test(requestId) || subject.length < 3 || body.length < 10)
        throw new HttpError(400, "Invalid support request");

      const attachments = validateAttachments(payload.attachments);
      const email = {
        from: env.SUPPORT_FROM,
        to: [env.SUPPORT_TO],
        subject: `[Gravity Support ${requestId.slice(0, 8).toUpperCase()}] ${subject}`,
        text: [
          `Firebase UID: ${player.uid}`,
          `Account email: ${player.email || "Guest account"}`,
          "",
          body,
        ].join("\n"),
        attachments: attachments.map(({ filename, contentBase64 }) => ({
          filename,
          content: contentBase64,
        })),
      };
      if (player.email)
        email.reply_to = player.email;

      const resendResponse = await fetch(RESEND_EMAIL_URL, {
        method: "POST",
        headers: {
          Authorization: `Bearer ${env.RESEND_API_KEY}`,
          "Content-Type": "application/json",
          "Idempotency-Key": `gravity-support-${requestId}`,
        },
        body: JSON.stringify(email),
      });
      const resendResult = await safeJson(resendResponse);
      if (!resendResponse.ok) {
        console.error("Resend rejected support email", resendResponse.status, resendResult);
        throw new HttpError(502, "Email provider rejected the request");
      }

      return jsonResponse(200, {
        ok: true,
        requestId,
        emailId: resendResult && resendResult.id ? resendResult.id : null,
      });
    } catch (error) {
      const status = error instanceof HttpError ? error.status : 500;
      const message = error instanceof HttpError ? error.message : "Support delivery failed";
      if (status >= 500)
        console.error("Support request failed", error);
      return jsonResponse(status, { error: message });
    }
  },
};

async function readRequestPayload(request) {
  const contentType = request.headers.get("Content-Type") || "";
  if (!contentType.toLowerCase().includes("application/json"))
    throw new HttpError(415, "JSON body required");
  try {
    return await request.json();
  } catch {
    throw new HttpError(400, "Invalid JSON body");
  }
}

function validateAttachments(value) {
  if (value == null)
    return [];
  if (!Array.isArray(value) || value.length > MAX_ATTACHMENT_COUNT)
    throw new HttpError(400, "A maximum of two screenshots is allowed");

  let totalBytes = 0;
  return value.map((attachment, index) => {
    if (!attachment || typeof attachment !== "object")
      throw new HttpError(400, "Invalid screenshot attachment");

    const contentType = cleanText(attachment.contentType, 40).toLowerCase();
    if (!/^image\/(jpeg|png|webp)$/.test(contentType))
      throw new HttpError(400, "Only JPEG, PNG, or WebP screenshots are allowed");

    const contentBase64 = typeof attachment.contentBase64 === "string"
      ? attachment.contentBase64
      : "";
    if (!isValidBase64(contentBase64))
      throw new HttpError(400, "Invalid screenshot encoding");

    totalBytes += decodedBase64Length(contentBase64);
    if (totalBytes <= 0 || totalBytes > MAX_ATTACHMENT_BYTES)
      throw new HttpError(400, "Screenshots must total no more than 10 MB");

    const extension = contentType === "image/png"
      ? ".png"
      : contentType === "image/webp" ? ".webp" : ".jpg";
    const requestedName = cleanText(attachment.filename, 80)
      .replace(/[^a-zA-Z0-9._-]/g, "-");
    const filename = requestedName.length > 0
      ? requestedName
      : `gravity-support-${index + 1}${extension}`;
    return { filename, contentType, contentBase64 };
  });
}

async function verifyFirebaseIdToken(token, projectId) {
  if (!token)
    throw new HttpError(401, "Authentication required");
  const parts = token.split(".");
  if (parts.length !== 3)
    throw new HttpError(401, "Invalid authentication token");

  let header;
  let claims;
  try {
    header = JSON.parse(decodeBase64UrlText(parts[0]));
    claims = JSON.parse(decodeBase64UrlText(parts[1]));
  } catch {
    throw new HttpError(401, "Invalid authentication token");
  }

  if (header.alg !== "RS256" || typeof header.kid !== "string")
    throw new HttpError(401, "Invalid authentication token");
  const jwk = await firebaseSigningKey(header.kid);
  if (!jwk)
    throw new HttpError(401, "Unknown authentication key");

  const key = await crypto.subtle.importKey(
    "jwk",
    jwk,
    { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" },
    false,
    ["verify"],
  );
  const validSignature = await crypto.subtle.verify(
    "RSASSA-PKCS1-v1_5",
    key,
    decodeBase64UrlBytes(parts[2]),
    new TextEncoder().encode(`${parts[0]}.${parts[1]}`),
  );
  if (!validSignature)
    throw new HttpError(401, "Invalid authentication signature");

  const now = Math.floor(Date.now() / 1000);
  const expectedIssuer = `https://securetoken.google.com/${projectId}`;
  const audienceMatches = claims.aud === projectId
    || Array.isArray(claims.aud) && claims.aud.includes(projectId);
  if (!audienceMatches
      || claims.iss !== expectedIssuer
      || typeof claims.sub !== "string"
      || claims.sub.length === 0
      || claims.sub.length > 128
      || typeof claims.exp !== "number"
      || claims.exp <= now - 30
      || typeof claims.iat !== "number"
      || claims.iat > now + 30
      || typeof claims.auth_time !== "number"
      || claims.auth_time > now + 30) {
    throw new HttpError(401, "Expired or invalid authentication token");
  }

  return {
    uid: claims.sub,
    email: typeof claims.email === "string" ? claims.email.slice(0, 320) : "",
  };
}

async function firebaseSigningKey(kid) {
  const now = Date.now();
  if (now >= signingKeysExpireAt || signingKeys.size === 0) {
    const response = await fetch(FIREBASE_JWKS_URL, {
      cf: { cacheEverything: true, cacheTtl: 3600 },
    });
    if (!response.ok)
      throw new HttpError(503, "Authentication service unavailable");
    const payload = await response.json();
    signingKeys = new Map((payload.keys || []).map((key) => [key.kid, key]));
    const cacheControl = response.headers.get("Cache-Control") || "";
    const match = cacheControl.match(/max-age=(\d+)/i);
    const seconds = match ? Math.max(300, Number(match[1])) : 3600;
    signingKeysExpireAt = now + seconds * 1000;
  }
  return signingKeys.get(kid);
}

async function enforceShortRateLimit(uid, context) {
  const key = new Request(`https://support-rate-limit.invalid/${encodeURIComponent(uid)}`);
  const cached = await caches.default.match(key);
  if (cached)
    throw new HttpError(429, "Please wait a few seconds before sending another request");
  context.waitUntil(caches.default.put(key, new Response("1", {
    headers: { "Cache-Control": "max-age=15" },
  })));
}

function bearerToken(value) {
  return typeof value === "string" && value.startsWith("Bearer ")
    ? value.slice(7).trim()
    : "";
}

function cleanText(value, maximumLength) {
  return typeof value === "string"
    ? value.replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]/g, "").trim().slice(0, maximumLength)
    : "";
}

function isValidBase64(value) {
  return value.length > 0
    && value.length % 4 === 0
    && /^[A-Za-z0-9+/]*={0,2}$/.test(value);
}

function decodedBase64Length(value) {
  const padding = value.endsWith("==") ? 2 : value.endsWith("=") ? 1 : 0;
  return value.length * 3 / 4 - padding;
}

function decodeBase64UrlText(value) {
  return new TextDecoder().decode(decodeBase64UrlBytes(value));
}

function decodeBase64UrlBytes(value) {
  const normalized = value.replace(/-/g, "+").replace(/_/g, "/");
  const padded = normalized + "=".repeat((4 - normalized.length % 4) % 4);
  const binary = atob(padded);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index++)
    bytes[index] = binary.charCodeAt(index);
  return bytes;
}

async function safeJson(response) {
  try {
    return await response.json();
  } catch {
    return null;
  }
}

function jsonResponse(status, payload) {
  return new Response(payload == null ? null : JSON.stringify(payload), {
    status,
    headers: {
      "Content-Type": "application/json; charset=utf-8",
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Headers": "Authorization, Content-Type",
      "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
      "Cache-Control": "no-store",
    },
  });
}

class HttpError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}
