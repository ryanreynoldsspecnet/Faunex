# Faunex Domain and SSL Deployment

Faunex is served from the Xneelo Linux VPS at `154.65.98.94`.

## DNS

The public website records should point at the VPS:

- `faunex.co.za` A record -> `154.65.98.94`
- `www.faunex.co.za` A record -> `154.65.98.94`

Mail-related records should remain pointed at the existing mail host unless mail hosting is intentionally migrated.

## Reverse Proxy

Production HTTPS is handled by Caddy in Docker Compose.

Caddy routes:

- `https://faunex.co.za` -> `faunex-web:8080`
- `https://www.faunex.co.za` -> redirects to `https://faunex.co.za`
- `http://154.65.98.94` -> redirects to `https://faunex.co.za`

Caddy automatically requests and renews Let's Encrypt certificates. Certificate state is stored in the `caddy_data` Docker volume.

## VPS Prerequisites

Before deploying the Caddy-enabled compose file:

1. Confirm DNS has propagated for both `faunex.co.za` and `www.faunex.co.za`.
2. Open inbound ports `80/tcp`, `443/tcp`, and optionally `443/udp` on the VPS firewall.
3. Ensure no host-level Nginx, Apache, or other reverse proxy is already binding ports `80` or `443`.
4. Deploy with the existing CI/CD pipeline or run:

```bash
cd /home/ubuntu/faunex
git pull origin master
docker compose down
docker compose up -d --build
```

## Verification

After deployment:

```bash
docker compose ps
docker compose logs caddy --tail=100
curl -I http://faunex.co.za
curl -I https://faunex.co.za
curl -I https://www.faunex.co.za
```

Expected behavior:

- `http://faunex.co.za` redirects to `https://faunex.co.za`
- `https://faunex.co.za` returns the Faunex web app
- `https://www.faunex.co.za` redirects to `https://faunex.co.za`
