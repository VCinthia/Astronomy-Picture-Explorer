# P3 production deploy and smoke runbook

Date: 2026-07-22
Status: PREPARED — external execution is intentionally pending owner credentials and
provider access.

This is the only approved runbook for mutating production P3 resources. It keeps the
portfolio on free tiers, avoids background work and records evidence without recording
secrets, confirmation codes or personal data.

## 0. Required owner decisions

Before creating or changing a provider resource, record the following in a private
password manager or deployment ticket — never in this repository:

1. Public Netlify origin (the existing `*.netlify.app` origin is sufficient; a custom
   domain is optional).
2. Resend sender/domain ownership and the DNS access needed to verify it.
3. A personal NASA API key.
4. The fixed initial catalog range. For a portfolio, the recommended starting target is
   the latest 30 completed UTC days, not the full APOD archive. The approved `from` and
   `to` values must be reused verbatim in the Neon seed and Render variables.
5. A temporary test mailbox that can receive the confirmation and password-reset emails.

Without all five, stop before provider mutation; local W12/W13 remains the supported
demonstration environment.

## 1. Revalidate zero-cost gates on the deployment day

Open the official pricing/limit pages and record only the date, plan name, quota and the
resulting action in the evidence table below.

| Provider | Required configuration | Fail-closed action |
|---|---|---|
| Netlify | Free plan; keep auto-recharge disabled; no paid add-ons | Usage limit pauses rather than buying credits |
| Render | Free Web Service; do not add a payment method; no disk, worker or cron | Service suspends if a charge would otherwise occur |
| Neon | Free project; do not enable paid compute/scale-to-zero exceptions | Stop the seed before a paid upgrade |
| Resend | Free account; one verified sender/domain; do not enable paid usage | Stop delivery testing at the free limit |

Provider terms evolve. If a dashboard does not make the required fail-closed setting
available, do not deploy until it is resolved without a paid workaround.

## 2. Create Neon and migrate from the local operator machine

1. Create a Neon **Free** PostgreSQL project and retain its TLS connection string only
   in the password manager.
2. Confirm Neon encryption-at-rest and TLS terms on the day. The P3 Data Protection XML
   key ring uses the provider's standard encryption-at-rest; P3 does not add a paid
   customer-managed-key service.
3. In a fresh PowerShell session, set the connection string only for that session and
   run the migration. Do not paste the value into a terminal transcript or screenshot.

   ```powershell
   $env:ConnectionStrings__Postgres = '<Neon TLS connection string>'
   dotnet tool restore
   dotnet ef database update --project backend/AstronomyExplorer.Api
   ```

4. Confirm the migration command succeeded, then close the shell or remove the
   temporary environment variable before using it for anything else.

## 3. Dry-run and seed the bounded catalog locally

The Catalog CLI must run from the developer machine, never from Render, Netlify, a
worker or an automated schedule. Replace the two dates with the approved fixed target.

```powershell
$env:ConnectionStrings__Postgres = '<Neon TLS connection string>'
$env:NasaApod__ApiKey = '<personal NASA API key>'

dotnet run --project backend/AstronomyExplorer.Catalog -- `
  catalog sync --from <YYYY-MM-DD> --to <YYYY-MM-DD> --batch-size 30 `
  --allow-local-production --dry-run

dotnet run --project backend/AstronomyExplorer.Catalog -- `
  catalog sync --from <YYYY-MM-DD> --to <YYYY-MM-DD> --batch-size 30 `
  --allow-local-production
```

If interrupted or rate-limited, inspect the output and use the same range with
`--resume`; do not start a parallel process. Record the final count, coverage and
database size without exposing the connection string. The seed is acceptable only when
the persisted target becomes `completed` and the later API `catalog-status` reports
`ready: true`.

## 4. Configure Resend, Render and Netlify

1. In Resend, verify the owned domain/sender and complete its DNS verification. Send no
   test mail until the sender status is verified.
2. Create the Render service as specified in
   [`render-setup.md`](render-setup.md). Set the dashboard-only variables, including the
   approved catalog range. Deploy once and verify only `https://<render-service>/health`
   is healthy. Do not test API/application routes directly.
3. In Netlify, set these production environment variables:

   | Variable | Scope | Value |
   |---|---|---|
   | `P3_RENDER_API_ORIGIN` | Builds | Render HTTPS origin, no trailing path |
   | `NETLIFY_PROXY_SIGNING_KEY` | Runtime | Unique random value, at least 32 UTF-8 bytes |

   Copy the same proxy value into Render's `NetlifyProxy__SigningKey`. Do not reuse the
   session signing key. The build script replaces only the committed invalid placeholder
   in `netlify.toml`; the proxy key is injected by Netlify at request time and is not
   written into the frontend.
4. Deploy the `codex/p3-integration` release candidate to Netlify. Review the deploy log
   after post-processing: the redirect rate-limit rules must be shown as valid. The
   proxy uses Netlify's per-domain-and-IP limits (Free availability) with account-specific
   limits before generic auth/API rules.

## 5. Production smoke

Use a private browser session and the temporary mailbox. Record PASS/FAIL plus a short
sanitized observation for every item:

1. First load after Render idle: Home shows content or the explicit connecting/retry CTA;
   Retry reaches content.
2. Home APOD today, Explorer exact date and case-insensitive search each work through the
   public Netlify origin; `catalog-status` reports `ready: true`.
3. In browser developer tools, application requests have the Netlify origin — no Render
   URL, CORS request or cross-site cookie.
4. `GET https://<render-service>/api/apod/catalog-status` without the Netlify signature
   returns `403 invalid_proxy_request`; the same browser operation via Netlify succeeds.
5. Register, receive Resend email, open the frontend confirmation link, confirm by POST,
   sign in, reload (refresh), request password recovery, open its frontend reset link,
   set a new password, confirm the old password/refresh session no longer works, sign in
   with the new password, add/list/delete a favorite, then log out.
6. Re-run the direct Render check with a fabricated `X-Forwarded-For`; it remains 403.
   Use two normal browser/network clients to demonstrate the edge limit is per visitor,
   without generating abusive traffic.
7. Trigger a Render restart/redeploy and confirm a previously issued (unexpired)
   confirmation link still works, proving the Neon-backed Data Protection key ring.
8. Remove the temporary account/favorite through the Neon console or a controlled local
   SQL session, then clear browser data. Do not retain its email, password or link in
   evidence.

## 6. Evidence and completion record

| Field | Value |
|---|---|
| Date/time (UTC) | Pending |
| Netlify public URL | Pending |
| Render health URL | Pending |
| Neon Free / encryption evidence | Pending |
| Resend sender status / daily limit | Pending |
| Approved catalog range / count / ready result | Pending |
| Netlify/Render/Neon/Resend zero-cost settings | Pending |
| Cold start, proxy, auth, password recovery and favorites smoke result | Pending |
| Cleanup result | Pending |

Only after every row is filled with PASS evidence may P3-W14, R3.14 and P3 be marked
DONE, `codex/p3-integration` be promoted to `main`, and the public release tag be made.

## Provider references verified for this preparation

- [Netlify signed proxy redirects](https://docs.netlify.com/manage/routing/redirects/rewrites-proxies/)
- [Netlify redirect rate limiting](https://docs.netlify.com/manage/security/secure-access-to-sites/rate-limiting/)
- [Netlify file configuration and build substitution](https://docs.netlify.com/build/configure-builds/file-based-configuration/)
- [Render Free instances](https://render.com/docs/free)
- [Neon security overview](https://neon.com/docs/security/security-overview)
- [Resend pricing](https://resend.com/pricing)
