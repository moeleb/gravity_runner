# Support email deployment

The Contact Support screen no longer uses a Firebase HTTPS Function. It sends
authenticated requests directly to the free Cloudflare Worker at:

```text
https://gravity-support-email.mkanafani40.workers.dev/support
```

Worker source and deployment instructions are in
`workers/support-email/README.md`.

The Unity client sends screenshots inline as Base64, so this path does not use
Firebase Storage and does not require a Firebase Blaze billing plan. Firebase
Authentication is still used to identify the signed-in player.

Do not deploy `submitSupportRequest` from `functions/index.js`; it is retained
only as legacy reference while the unrelated notification function remains in
that file.
