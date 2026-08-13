# P3 production deployment record and smoke summary

Date: 2026-08-12
Status: DONE — P3 cutover to `main` and post-release verification completed by P4-W1.

This versioned record preserves the completed P3 release evidence. It is not authority to
change production resources, recreate provider configuration, or run a new catalog seed.
Future operational changes require their own approved release plan and must preserve the
zero-cost and no-background-work constraints.

## Current operating boundary

- The public application and API deployment surfaces build from `main`.
- Browser application traffic uses the public site origin and same-origin application
  routes; the platform health probe remains a separate operational concern.
- The catalog loader is a manual local operator command. It does not run at API startup,
  in a hosted worker, or on a schedule.
- Secrets, provider settings, sender identities, service origins, confirmation links and
  test-account details remain outside Git and this record.

## Historical execution summary

The following is a factual summary of the completed release; it intentionally omits the
dashboard procedure and configuration values that were used at the time.

1. The owner verified the free-tier and zero-overage constraints of the hosting, managed
   PostgreSQL, email and upstream-data providers before making external changes.
2. The PostgreSQL schema was migrated from a local operator environment using a
   short-lived local credential. Data Protection persistence was validated against the
   managed database.
3. The owner previewed and ran a bounded initial APOD catalog seed locally. The command
   resumed safely after an interruption and completed without a scheduler or hosted job.
4. The transactional email sender was verified, then the containerized API and public
   frontend were deployed with provider-managed configuration.
5. The public experience, account flows, password recovery, favorites, catalog readiness,
   cold-start behavior and session persistence were smoke-tested. The access-control
   boundary was separately covered by production and automated verification without
   publishing probing instructions.

## Sanitized production evidence

| Field | Result |
|---|---|
| Release date | 2026-08-12 |
| Public application | PASS — public site served the Angular application through its same-origin routes. |
| API health | PASS — platform health check remained healthy after deployment and a manual restart. |
| Managed database | PASS — free managed PostgreSQL and provider-managed at-rest protections were verified for this portfolio. |
| Transactional email | PASS — verified sender delivered confirmation and password-recovery mail. |
| Initial catalog | PASS — target `2026-07-13..2026-08-11` completed with 30 initial APOD entries; public catalog status reported `ready: true`. |
| Functional smoke | PASS — Home, date, case-insensitive search, account confirmation, session refresh, password reset, favorites and logout completed. |
| Key persistence | PASS — a confirmation link issued before a service restart remained valid after health recovered. |
| Cost controls | PASS — no paid service, keepalive, scheduled worker, persistent disk or automated catalog backfill was configured. |
| Test-data disposition | PASS — the owner retained one portfolio account and deleted the secondary test account and its related data outside this repository. |

## P4-W1 post-cutover reconciliation (2026-08-12)

P4-W1 confirmed that `main` contains the promoted P3 integration and that both production
surfaces deploy from that branch. The post-cutover health and same-origin catalog checks
passed. P3 is therefore `DONE`; P4 continues only with documentation alignment.

## References

- [Netlify rewrites and proxies](https://docs.netlify.com/manage/routing/redirects/rewrites-proxies/)
- [Netlify rate limiting](https://docs.netlify.com/manage/security/secure-access-to-sites/rate-limiting/)
- [Render Free instances](https://render.com/docs/free)
- [Neon security overview](https://neon.com/docs/security/security-overview)
- [Resend pricing](https://resend.com/pricing)
