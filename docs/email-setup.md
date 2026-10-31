# Faunex Email Setup

Faunex sends password reset emails through SMTP. Production uses Zoho ZeptoMail for transactional email.

ZeptoMail SMTP uses:

- Server: `smtp.zeptomail.com`
- Port `587` with TLS
- Port `465` with SSL
- TLS v1.2 support for SMTP

## Required VPS `.env` Values

Set these values in `/home/ubuntu/faunex/.env`:

```dotenv
FAUNEX_PUBLIC_BASE_URL=https://faunex.co.za
FAUNEX_EMAIL_SMTP_HOST=smtp.zeptomail.com
FAUNEX_EMAIL_SMTP_PORT=587
FAUNEX_EMAIL_SMTP_USERNAME=your-zeptomail-smtp-username
FAUNEX_EMAIL_SMTP_PASSWORD=your-zeptomail-smtp-password
FAUNEX_EMAIL_SMTP_ENABLE_SSL=true
FAUNEX_EMAIL_FROM_EMAIL=no-reply@faunex.co.za
FAUNEX_EMAIL_FROM_NAME=Faunex
```

Use the SMTP username and password generated inside the ZeptoMail Mail Agent. The sender address in `FAUNEX_EMAIL_FROM_EMAIL` must be an approved/verified sender for that Mail Agent.

After the first Section 7A release, apply an SMTP-only `.env` change using the retained successful image (no rebuild):

```bash
cd /home/ubuntu/faunex
docker compose up -d --no-build --pull never --force-recreate --wait --wait-timeout 600 faunex-api
```

Use the normal validated merge-to-master workflow for application releases or initial setup. See [release/recovery operations](release-recovery.md). A simple restart does not replace container environment variables. Keep SMTP credentials out of chat, logs and source control; schedule the brief API interruption appropriately.

## Behaviour

- `/forgot-password` always shows a generic response so account existence is not leaked.
- Production sends an email with a reset link to `/reset-password`.
- Development can still return the token in the API response for local testing.
- If SMTP is not configured or delivery fails, the API logs the failure and still returns the safe generic response.
