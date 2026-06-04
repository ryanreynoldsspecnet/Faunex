# Faunex Email Setup

Faunex sends password reset emails through SMTP. The application is provider-neutral, so the production VPS can use Xneelo mail hosting or another SMTP provider.

## Required VPS `.env` Values

Set these values in `/home/ubuntu/faunex/.env`:

```dotenv
FAUNEX_PUBLIC_BASE_URL=https://faunex.co.za
FAUNEX_EMAIL_SMTP_HOST=smtp.example.co.za
FAUNEX_EMAIL_SMTP_PORT=587
FAUNEX_EMAIL_SMTP_USERNAME=no-reply@faunex.co.za
FAUNEX_EMAIL_SMTP_PASSWORD=change-me
FAUNEX_EMAIL_SMTP_ENABLE_SSL=true
FAUNEX_EMAIL_FROM_EMAIL=no-reply@faunex.co.za
FAUNEX_EMAIL_FROM_NAME=Faunex
```

After editing `.env`, redeploy with:

```bash
cd /home/ubuntu/faunex
docker compose up -d --build --remove-orphans
```

## Behaviour

- `/forgot-password` always shows a generic response so account existence is not leaked.
- Production sends an email with a reset link to `/reset-password`.
- Development can still return the token in the API response for local testing.
- If SMTP is not configured or delivery fails, the API logs the failure and still returns the safe generic response.
