# PayFast Sandbox Operations

This slice enables **sandbox only**, not live money. Use the tenant's own sandbox merchant ID, merchant key and passphrase. Never put credentials or callback secrets in Git, chat, screenshots or PR comments. Sandbox completion is stored/displayed as `SandboxComplete`, not a commercial paid state. Faunex subscriptions, live activation, refunds and carrier booking are separate work.

## VPS Setup

No reboot is required. Before enabling checkout, add these values to `/home/ubuntu/faunex/.env` using PuTTY and an editor such as `nano .env`:

```dotenv
FAUNEX_PAYFAST_SANDBOX_ENABLED=true
FAUNEX_PAYMENTS_INGRESS_TOKEN=<a fresh random secret of at least 32 characters>
```

Generate a strong secret on the VPS with `openssl rand -hex 32`. Keep it in `.env` only. Do not duplicate existing settings or use the test token from the test suite. The same value is passed automatically to Caddy and the API by Compose. New containers must be recreated for edited environment variables to take effect; restarting an existing container alone does not replace its environment. The normal master deployment recreates changed services. If configuring after deployment:

```sh
cd /home/ubuntu/faunex
docker compose up -d --force-recreate faunex-api caddy
docker compose ps -a
```

Do not use `down -v`, remove payment keys, or print the rendered Compose environment to share diagnostics. Missing setup leaves checkout disabled and notification ingress unavailable; it does not disable the rest of the site. Check configuration without exposing its secret:

```sh
docker compose exec -T faunex-api sh -c 'test "$Payments__SandboxEnabled" = true && test "${#Payments__IngressToken}" -ge 32 && echo "Sandbox ingress configured"'
```

The fixed public notification URL is `https://faunex.co.za/payments/payfast/notify`. Only that path on the Faunex root proxies to the API; the rest of the API stays private. Caddy overwrites the source-IP and ingress-secret headers using the actual transport peer and its environment, and removes incoming authorization/cookies. The API rejects a missing/wrong ingress secret, unapproved source IP, oversized/duplicate/ambiguous fields or invalid content type. Do not add a blanket API proxy, trust caller-supplied X-Forwarded-For/Referer, or disable source validation to make a test pass.

## First Tenant Acceptance

1. Use `/tenant/payments` to enter Stormbird's own **sandbox** credentials; these are separate from its live account. The user confirmed they can configure this account. Storage alone does not verify credentials.
2. Publish tenant delivery/charge terms before publishing a new test auction. Older auctions without recorded pricing are not backfilled with new fees.
3. Use disposable tenant data to complete listing approval, bidding and authoritative closure. The winning buyer reviews/accepts the final itemised quote.
4. Prepare sandbox checkout. Check the amount/currency and merchant ID; submit the native form to `https://sandbox.payfast.co.za/eng/process` and use PayFast's sandbox payment flow. Do not change the form action to the live site or use live merchant credentials.
5. Return to the order. A return/cancel URL is not proof of payment and changes no payment state. Pending orders poll the verified payment summary; Refresh Payment can retry the read. Only a fully validated COMPLETE notification changes the sandbox status.
6. Confirm a `Sandbox payment confirmed` result, matching merchant, provider reference and total, and a verified notification in both buyer and tenant order views. Confirm the transaction separately in the sandbox merchant dashboard.
7. Test browser cancellation/incomplete payment, re-entry, provider/notification retry, and duplicate notifications. A pending reference is reused rather than creating multiple open checkout intents. Non-COMPLETE statuses are retained as unconfirmed events, never interpreted as payment. They cannot undo a verified completion.

A safe routing/source-denial probe from a non-PayFast machine is:

```sh
curl -sS -o /dev/null -w '%{http_code}\n' -X POST https://faunex.co.za/payments/payfast/notify -H 'Content-Type: application/x-www-form-urlencoded' --data 'signature=invalid'
```

Expected: 400 with no payment mutation when ingress is configured; 503 if ingress is missing. This is not a successful provider acceptance test. Real external sandbox return/ITN routing, TLS, provider credentials and source ranges require the actual hosted sandbox journey after merge. Local/CI use a verifier double and do not prove those external dependencies.

## Payment Invariants

- Checkout is buyer/owner/tenant/host protected. It uses the accepted quote, requires ZAR, and never converts currencies or accepts a browser-selected amount/merchant.
- Each order has one sandbox intent. Amount, quote, merchant, protected credentials, environment and return/callback URLs are immutable. Pending retries reuse it. Merchant setting changes block offering the old intent again, rather than silently retargeting it.
- The custom signature uses the provider's documented ordered form fields and PHP-compatible URL encoding. The passphrase never leaves the API. PayFast's protocol requires the merchant key in the hidden checkout form; it is not returned by settings/history/order APIs. MD5 is used only because the provider mandates it for this protocol, not for passwords or stored secrets.
- Notifications select the stored intent by its reference, not a tenant or merchant supplied in the callback. They require a valid signature using captured credentials, exact merchant/gross amount and ZAR context, a trusted provider source, and a successful HTTPS server validation response from the fixed sandbox endpoint. Amount fee/net fields do not replace the gross order total. Validation times out/fails closed; no redirects or client-controlled validation host are allowed.
- Valid state change and append-only notification metadata commit atomically with optimistic concurrency. Exact verified replays are idempotent. Events retain provider reference/status, amount, sequence, time, decision and a SHA-256 payload hash, not raw personal data, signatures or secrets. The latest 20 events are displayed; all are retained in the database subject to operational retention policy.
- A second COMPLETE with a different provider transaction, or a provider reference already assigned to another order for the merchant, flags `ReviewRequired`; do not retry payment. No automatic duplicate allocation or refund occurs. Inspect both provider transactions and order history before manual resolution. No resolve/erase-history endpoint is implemented here.
- Removing merchant settings, disabling new sandbox checkout or deactivating a tenant does not invalidate already issued forms or discard late authenticated confirmations. Old protected credential snapshots remain necessary for those callbacks. Removing keys can strand pending confirmations; restore the original key ring, never bypass signature validation. A callback secret rotation must update Caddy and API together.
- Signed checkout forms can still be resubmitted outside the app after issue. This integration detects conflicting provider completions; it cannot revoke a previously delivered provider form. Keep payments sandbox-only until retry/refund/reconciliation policy and live acceptance are approved.

## Provider Sources

Checked 30 October 2026: [custom integration documentation](https://developers.payfast.co.za/documentation), [required integration checks](https://payfast.io/integration/submit-your-solution/), official [signature implementation](https://github.com/Payfast/payfast-php-sdk/blob/master/lib/Auth.php), [notification implementation](https://github.com/Payfast/payfast-php-sdk/blob/master/lib/PaymentIntegrations/Notification.php) and [ITN troubleshooting](https://support.payfast.help/portal/en/kb/articles/what-causes-the-itn-security-check-errors-20-9-2022). Custom-checkout and API signatures have different ordering rules. The application checks the actual trusted proxy peer address, not the SDK's Referer-based example.

The published source ranges used are `197.97.145.144/28`, `41.74.179.192/27`, `102.216.36.0/28`, `102.216.36.128/28`, and `144.126.193.139/32`. Treat provider source changes as an operational update requiring current primary-source verification and tests, not a reason to accept all IPs. The complete hosted sandbox journey and real Caddy header forwarding remain acceptance items; configuration validation and verifier-double tests are not substitutes.
