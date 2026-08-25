# Gravity support email Worker

This Worker replaces the paid Firebase Function used by the Unity Contact
Support screen. It verifies the Firebase ID token, validates up to two inline
screenshots (10 MB combined), and sends the message through Resend.

The Resend key must only exist as the encrypted Worker secret
`RESEND_API_KEY`. Never add it to Unity, `wrangler.jsonc`, or source control.

Deploy from this directory:

```sh
npx wrangler login
npx wrangler secret put RESEND_API_KEY
npx wrangler deploy
```

For initial Resend testing, `onboarding@resend.dev` can send to the email
address belonging to the Resend account. Before sending to other recipients,
verify a domain in Resend and update `SUPPORT_FROM` in `wrangler.jsonc`.
