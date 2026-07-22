# Render setup — P3-W14

Date: 2026-07-22
Scope: production preparation only. Follow
[`p3-deploy-runbook.md`](p3-deploy-runbook.md) for the ordered provider, seed and smoke
gates. Nothing in this file authorizes a paid plan, a scheduled job or a keepalive.

## Service definition

Create one **Web Service** on Render's Free plan from this repository. In the service
form use the following Docker settings:

| Setting | Value |
|---|---|
| Root directory | `backend` |
| Dockerfile path | `AstronomyExplorer.Api/Dockerfile` |
| Runtime | Docker |
| Health check path | `/health` |
| Plan | Free only |
| Background workers, cron jobs and disks | Do not create |
| Auto-deploy | Enable only after the Netlify/Neon/Resend gates are ready |

The image is non-root and binds the `PORT` that Render assigns. It never migrates or
seeds at startup. The API health endpoint remains direct only for Render's probe; all
`/api/*` and `/auth/*` production requests must carry the signed Netlify proxy header.

## Dashboard-only environment variables

Mark secret values as secrets in Render. Do not place any of them in Git, `.env`, Docker
build arguments or a deploy log.

| Variable | Required value / rule |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Postgres` | Neon pooled PostgreSQL TLS connection string |
| `Frontend__PublicBaseUrl` | Exact public Netlify HTTPS origin, no path |
| `Session__Issuer` | Stable Render HTTPS service origin, no path |
| `Session__Audience` | `astronomy-explorer-spa` |
| `Session__SigningKey` | Unique random value of at least 32 UTF-8 bytes |
| `NasaApod__ApiKey` | Personal NASA API key; never `DEMO_KEY` |
| `NasaApod__BaseUrl` | Leave default `https://api.nasa.gov/` unless explicitly changed |
| `Catalog__RequiredFrom` | Exact approved seed start, `YYYY-MM-DD` |
| `Catalog__RequiredTo` | Exact approved seed end, `YYYY-MM-DD` |
| `Email__Provider` | `Resend` |
| `Resend__ApiKey` | Resend API key for the verified sender |
| `Resend__FromAddress` | Verified Resend sender, e.g. `Astronomy Explorer <hello@…>` |
| `NetlifyProxy__SigningKey` | Same random value as Netlify's `NETLIFY_PROXY_SIGNING_KEY` |
| `NetlifyProxy__UseEdgeRateLimits` | `true` |

Do not set `LocalFixtures__Enabled`, `Email__Provider=LocalLog`, a local NASA URL or
Docker-secret-file variables. Startup fails closed if the database/session/proxy settings
are missing or unsafe.

## Direct URL boundary

Render Free does not provide the network access-control feature needed to hide a web
service URL. The application therefore enforces the boundary cryptographically:

- Netlify's signed redirect emits an HS256 `x-nf-sign` request header.
- The API validates its signature, Netlify issuer, production deploy context, expiry and
  exact public site URL before every `/api/*` or `/auth/*` request.
- A direct Render request, an `X-Forwarded-For` spoof or a Netlify preview signature is
  rejected with `403 invalid_proxy_request`.
- Browser visitor-IP protection runs on the signed Netlify redirect rules. The API keeps
  its email-partition limiter, but intentionally does not treat an unverified forwarded
  header as a client IP.

The `NETLIFY_PROXY_SIGNING_KEY` environment value must be available to the Netlify
**Runtime** scope. `P3_RENDER_API_ORIGIN` is a separate non-secret Netlify **Build**
variable with this Render service's HTTPS origin. See the deploy runbook for the exact
Netlify configuration and smoke proof.
