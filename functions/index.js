"use strict";

const { onRequest } = require("firebase-functions/v2/https");
const { onDocumentCreated } = require("firebase-functions/v2/firestore");
const { defineSecret } = require("firebase-functions/params");
const admin = require("firebase-admin");
const nodemailer = require("nodemailer");

admin.initializeApp();

const smtpHost = defineSecret("SMTP_HOST");
const smtpPort = defineSecret("SMTP_PORT");
const smtpUser = defineSecret("SMTP_USER");
const smtpPass = defineSecret("SMTP_PASS");
const supportFrom = defineSecret("SUPPORT_FROM");
const supportRecipient = "mkanafani40@gmail.com";
const maximumAttachmentBytes = 10 * 1024 * 1024;

/**
 * Counts the first creation of each idempotent user purchase record and keeps
 * one live winner for G Coins and one for Gravity Cores. The client reads only
 * the winner document, so "MOST POPULAR" is never statically assigned.
 *
 * Before a public production launch, purchase records should be created only
 * after server-side Google Play / App Store receipt validation.
 */
exports.aggregateStorePurchase = onDocumentCreated({
  document: "users/{uid}/purchases/{purchaseId}",
  region: "europe-west1",
}, async (event) => {
  const snapshot = event.data;
  if (!snapshot) return;
  const purchase = snapshot.data() || {};
  const productId = clean(purchase.product_id, 128);
  const rewardType = clean(purchase.reward_type, 32);
  if (!/^[A-Za-z0-9._-]{3,128}$/.test(productId)
      || (rewardType !== "coins" && rewardType !== "gravity_cores")) {
    console.warn("Ignoring malformed store purchase", event.params.purchaseId);
    return;
  }

  const database = admin.firestore();
  const metricReference = database.collection("storeProductMetrics").doc(productId);
  const popularReference = database.collection("storePopularity").doc("current");
  await database.runTransaction(async (transaction) => {
    const [metricSnapshot, popularSnapshot] = await Promise.all([
      transaction.get(metricReference),
      transaction.get(popularReference),
    ]);
    const currentMetric = metricSnapshot.exists ? metricSnapshot.data() : {};
    const nextCount = Math.max(0, Number(currentMetric.purchase_count || 0)) + 1;
    transaction.set(metricReference, {
      product_id: productId,
      reward_type: rewardType,
      purchase_count: nextCount,
      updated_at: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });

    const popular = popularSnapshot.exists ? popularSnapshot.data() : {};
    const idField = rewardType === "coins"
      ? "coin_product_id" : "gravity_core_product_id";
    const countField = rewardType === "coins"
      ? "coin_purchase_count" : "gravity_core_purchase_count";
    const existingWinner = clean(popular[idField], 128);
    const existingCount = Math.max(0, Number(popular[countField] || 0));
    if (nextCount > existingCount || existingWinner === productId) {
      transaction.set(popularReference, {
        [idField]: productId,
        [countField]: nextCount,
        updated_at: admin.firestore.FieldValue.serverTimestamp(),
      }, { merge: true });
    }
  });
});

/**
 * Admin-only Android campaign endpoint. It targets only devices that explicitly
 * opted into the gravity_android topic. A confirmation phrase prevents
 * accidental sends from scripts or console testing.
 */
exports.sendAndroidCampaign = onRequest({ region: "europe-west1" }, async (request, response) => {
  if (request.method !== "POST") {
    response.status(405).json({ error: "POST required" });
    return;
  }
  try {
    const authorization = request.get("authorization") || "";
    if (!authorization.startsWith("Bearer ")) {
      response.status(401).json({ error: "Authentication required" });
      return;
    }
    const decoded = await admin.auth().verifyIdToken(authorization.slice(7));
    if (decoded.admin !== true) {
      response.status(403).json({ error: "Admin claim required" });
      return;
    }

    const title = clean(request.body && request.body.title, 60);
    const body = clean(request.body && request.body.body, 180);
    const deepLink = clean(request.body && request.body.deepLink, 300);
    if (title.length < 2 || body.length < 2) {
      response.status(400).json({ error: "Campaign title and body are required" });
      return;
    }

    const message = {
      topic: "gravity_android",
      notification: { title, body },
      data: { campaign: "gravity_android", deepLink },
      android: {
        priority: "high",
        notification: { channelId: "gravity_game_updates", sound: "default" },
      },
    };
    const dryRun = request.body && request.body.confirm !== "SEND_ANDROID_CAMPAIGN";
    const messageId = await admin.messaging().send(message, dryRun);
    await admin.firestore().collection("notificationCampaigns").add({
      platform: "android",
      topic: "gravity_android",
      title,
      body,
      deepLink,
      dryRun,
      messageId,
      sentBy: decoded.uid,
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
    });
    response.status(200).json({ ok: true, dryRun, messageId });
  } catch (error) {
    console.error("Android campaign failed", error);
    response.status(500).json({ error: "Campaign delivery failed" });
  }
});

exports.submitSupportRequest = onRequest({
  region: "europe-west1",
  timeoutSeconds: 60,
  memory: "512MiB",
  secrets: [smtpHost, smtpPort, smtpUser, smtpPass, supportFrom],
}, async (request, response) => {
  if (request.method !== "POST") {
    response.status(405).json({ error: "POST required" });
    return;
  }

  try {
    const authorization = request.get("authorization") || "";
    if (!authorization.startsWith("Bearer ")) {
      response.status(401).json({ error: "Authentication required" });
      return;
    }

    const decoded = await admin.auth().verifyIdToken(authorization.slice(7));
    const payload = request.body || {};
    const requestId = clean(payload.requestId, 64);
    const subject = clean(payload.subject, 120);
    const body = clean(payload.body, 2000);
    const attachmentPaths = Array.isArray(payload.attachmentPaths) ? payload.attachmentPaths : [];
    if (!/^[a-f0-9]{32}$/.test(requestId) || subject.length < 3 || body.length < 10) {
      response.status(400).json({ error: "Invalid support request" });
      return;
    }

    const expectedPrefix = `support/${decoded.uid}/${requestId}/`;
    if (attachmentPaths.some((path) => typeof path !== "string" || !path.startsWith(expectedPrefix))) {
      response.status(403).json({ error: "Invalid attachment path" });
      return;
    }

    const bucket = admin.storage().bucket();
    const attachments = [];
    let totalBytes = 0;
    for (const path of attachmentPaths) {
      const file = bucket.file(path);
      const [metadata] = await file.getMetadata();
      const size = Number(metadata.size || 0);
      const contentType = String(metadata.contentType || "");
      totalBytes += size;
      if (size <= 0 || totalBytes > maximumAttachmentBytes || !/^image\/(jpeg|png|webp)$/.test(contentType)) {
        response.status(400).json({ error: "Attachments must be images totalling no more than 10 MB" });
        return;
      }
      const [content] = await file.download();
      attachments.push({ filename: file.name.split("/").pop(), content, contentType });
    }

    const playerSnapshot = await admin.firestore().collection("users").doc(decoded.uid).get();
    const player = playerSnapshot.exists ? playerSnapshot.data() : {};
    const playerId = player.player_id || `GHD-${decoded.uid.slice(0, 6).toUpperCase()}`;
    const transporter = nodemailer.createTransport({
      host: smtpHost.value(),
      port: Number(smtpPort.value()),
      secure: Number(smtpPort.value()) === 465,
      auth: { user: smtpUser.value(), pass: smtpPass.value() },
    });

    await transporter.sendMail({
      from: supportFrom.value(),
      to: supportRecipient,
      replyTo: decoded.email || undefined,
      subject: `[Gravity Support ${requestId.slice(0, 8).toUpperCase()}] ${subject}`,
      text: [
        `Player ID: ${playerId}`,
        `Firebase UID: ${decoded.uid}`,
        `Account email: ${decoded.email || "Guest account"}`,
        `Device hash: ${player.device_id_hash || "Not recorded"}`,
        "",
        body,
      ].join("\n"),
      attachments,
    });

    await admin.firestore().collection("supportRequests").doc(requestId).set({
      uid: decoded.uid,
      playerId,
      email: decoded.email || null,
      subject,
      body,
      attachmentPaths,
      attachmentBytes: totalBytes,
      status: "emailed",
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
    });

    response.status(200).json({ ok: true, requestId });
  } catch (error) {
    console.error("Support request failed", error);
    response.status(500).json({ error: "Support delivery failed" });
  }
});

function clean(value, maximumLength) {
  return typeof value === "string" ? value.trim().slice(0, maximumLength) : "";
}
