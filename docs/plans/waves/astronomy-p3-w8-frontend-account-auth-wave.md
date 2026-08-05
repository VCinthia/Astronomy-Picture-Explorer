# Wave P3-W8 - Frontend de cuenta y autenticacion

Date: 2026-07-16
Status: DONE - 2026-07-20
Wave ID: `P3-W8`
Depends On: P3-W2 + P3-W3 merged + Angular 22.0.7 maintenance gate closed
Suggested Branch: `wave/p3-w8-frontend-account-auth`

## Goal

Agregar formularios y estado Angular para registro, confirmacion POST y login, sin
implementar aun guard/refresh automatico.

## File scope

- `src/app/services/auth.service.ts` + tests
- `src/app/auth/login/`
- `src/app/auth/register/`
- `src/app/auth/confirm-email/`
- `src/app/app.routes.ts`
- `src/app/app.config.ts`
- `src/app/app.component.*`

No se agrega `src/environments/`: los endpoints son relativos same-origin `/auth/*`,
por lo que una URL de Render o configuracion de backend en el browser contradice el
contrato de P3. El link `Sign in` vive en el header persistente, visible tambien en
mobile; en ese breakpoint la marca se compacta visualmente pero conserva su nombre
accesible. W8 no agrega un cuarto destino al bottom nav.

## Checklist

- [x] W8.1 `AuthService` signals `currentUser`, `accessToken`, `isAuthenticated`,
  `loading`, `error`; token solo en memoria.
- [x] W8.2 Register/login/resend usan endpoints same-origin y errores ProblemDetails
  tipados, incluidos `code` y errores de validacion.
- [x] W8.3 Confirm component exige query params `userId` GUID y `code` Base64URL,
  limpia el link antes del `POST /auth/confirm-email`, nunca confirma mediante GET y
  redirige a `/login` sin auto-login.
- [x] W8.4 Login representa 401 generico y 403 `email_unconfirmed` con CTA resend,
  sin mostrar detalles de backend para otras respuestas.
- [x] W8.5 Formularios accesibles con `required`/email/maxLength 256, autocomplete,
  validacion cliente/servidor y estados pending/success/error.
- [x] W8.6 Rutas publicas lazy `/login`, `/register`, `/confirm-email` y link Sign in
  visible en el header de desktop y mobile.

## Acceptance criteria

- Flujo UI funciona con HttpTestingController sin red real.
- Falta de userId/code no llama backend y muestra error accesible.
- Token no aparece en localStorage/sessionStorage.
- No se introducen NgModule ni BehaviorSubject.
- Confirmacion valida usa solo POST; un link ausente/malformado produce cero requests.

## Verification

```powershell
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
rg -n "localStorage.*token|sessionStorage.*token|BehaviorSubject|NgModule|environment" src/app
```

## Evidence

- `npm ci` PASS (2026-07-20); el audit completo conserva los 5 advisories dev-only ya
  aceptados por el gate Angular, sin cambio de lockfile ni `npm audit fix --force`.
- `npm run build` PASS.
- `npm test -- --watch=false --browsers=ChromeHeadless` PASS: 94/94.
- Tests HttpTestingController cubren contratos register/resend/confirm/login, mapeo de
  ProblemDetails, JWT exclusivamente en signals, 401 generico, 403 con resend, rutas
  lazy, validacion de link con cero request, limpieza previa al POST aun cuando falla y
  estados async.

## Handoff

- W9 extiende `AuthService` y es propietaria de bootstrap, refresh, logout, guard e
  interceptor single-flight. W8 no adjunta Bearer, no intenta refresh ni protege rutas.
- W10 migra el shell/APOD y debe conservar una entrada de cuenta visible; puede cambiar
  la navegacion, pero no ocultar `Sign in` sin reemplazarlo por un control de sesion
  accesible.

## Parent sync

- [x] Actualizar `R3.8`, master/readiness, ADR, flow, PRD y estado con evidencia.

## Design clarification - P3-W15 (2026-08-05)

W15 amplía la cuenta con `forgot-password` y `reset-password` lazy. Replica el patrón
W8 de validar GUID/Base64URL y limpiar el código antes del POST, pero no cambia los
contratos ni la evidencia de registro, confirmación y login originales.
