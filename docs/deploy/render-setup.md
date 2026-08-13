# Render release profile — P3-W14 historical record

Date: 2026-08-12
Status: COMPLETE — the P3 deployment and post-cutover validation are recorded in
[`p3-deploy-runbook.md`](p3-deploy-runbook.md).

This document records the service shape used for the released portfolio. It is not a
provider-dashboard recipe and does not disclose production values, service origins or
secret names. The production branch is `main`; any future deployment change requires its
own approved plan.

## Service shape

| Setting category | Released choice |
|---|---|
| Service kind | One Docker web service on the free tier |
| Repository scope | `backend` |
| Dockerfile | `AstronomyExplorer.Api/Dockerfile` relative to that scope |
| Health route | `/health` |
| Runtime work | No startup migration, catalog seed, worker, cron or persistent disk |
| Deployment policy | Production deploys from `main`; automatic versus manual triggering remains an owner-controlled provider setting |

The image is non-root and binds the port assigned by the platform. Database migrations and
catalog ingestion are intentionally separate, local operator actions.

## Configuration boundary

Provider-managed configuration supplies these categories outside Git:

- production environment designation and managed PostgreSQL access;
- public application origin and session configuration;
- APOD upstream credential and approved catalog target;
- transactional email sender and credential; and
- the shared value that authorizes the frontend proxy boundary.

Values are stored only in the relevant provider dashboard or a local secret store. They
are never copied to repository files, Docker build arguments, screenshots, terminal
transcripts or this documentation. Development fixtures, local email logging and local
mock-upstream settings are not valid production configuration.

## Application access boundary

The public frontend serves the browser-facing application routes on one origin and proxies
them to the API. In production, the API accepts application traffic only through that
configured boundary. The health route remains available to the hosting platform for its
probe. Browser traffic does not need a direct API origin or a cross-site refresh cookie.

The implementation has automated coverage for the boundary and a completed production
smoke result. This document deliberately omits signature mechanics, validation claims,
rate-limit budgets and direct-request procedures; those are operational controls, not a
public integration contract.
