# Wave P3-W8 - Frontend de cuenta y autenticacion

Date: 2026-07-16
Status: READY - Not Started
Wave ID: `P3-W8`
Depends On: P3-W2 + P3-W3 merged
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
- `src/environments/`

## Checklist

- [ ] W8.1 `AuthService` signals `currentUser`, `accessToken`, `isAuthenticated`,
  `loading`, `error`; token solo en memoria.
- [ ] W8.2 Register/login/resend usan endpoints same-origin y errores tipados.
- [ ] W8.3 Confirm component exige query params `userId` y `code`, envia
  `POST /auth/confirm-email` y no confirma mediante GET.
- [ ] W8.4 Login representa 401 generico y 403 unconfirmed con CTA resend.
- [ ] W8.5 Formularios accesibles, validacion cliente coherente y estados pending/success.
- [ ] W8.6 Rutas publicas lazy `/login`, `/register`, `/confirm-email`.

## Acceptance criteria

- Flujo UI funciona con HttpTestingController sin red real.
- Falta de userId/code no llama backend y muestra error accesible.
- Token no aparece en localStorage/sessionStorage.
- No se introducen NgModule ni BehaviorSubject.

## Verification

```powershell
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
rg -n "localStorage.*token|sessionStorage.*token|BehaviorSubject|NgModule" src/app
```

## Parent sync

- [ ] Actualizar `R3.8`, master/readiness y estado con evidencia.
