# Wave P3-W9 - Bootstrap, guard e interceptor de sesion

Date: 2026-07-16
Status: DONE - 2026-07-20
Wave ID: `P3-W9`
Depends On: P3-W3 + P3-W8 merged
Suggested Branch: `wave/p3-w9-frontend-session`

## Goal

Completar el ciclo de sesion Angular: bootstrap, AuthGuard, Bearer interceptor,
single-flight refresh y logout sin loops.

## File scope

- `src/app/services/auth.service.ts` + tests
- `src/app/auth/auth.guard.ts` + tests
- `src/app/auth/auth.interceptor.ts` + tests
- `src/app/app.config.ts`
- `src/app/app.routes.ts`
- `src/app/app.component.*`
- `src/app/auth/login/`
- `angular.json`
- `proxy.conf.json`

## Checklist

- [x] W9.1 Bootstrap intenta un refresh una vez y expone estado `checking/auth/anon`.
- [x] W9.2 Guard espera bootstrap y redirige `/favorites` a
  `/login?returnUrl=/favorites` solo si anon.
- [x] W9.3 Interceptor adjunta Bearer solo a API propia.
- [x] W9.4 Varios 401 comparten una unica operacion refresh; cada request se reintenta
  maximo una vez con el nuevo token.
- [x] W9.5 Login/refresh/logout se excluyen del auto-refresh.
- [x] W9.6 Fallo refresh limpia estado, falla la cola y redirige una sola vez.
- [x] W9.7 Logout llama endpoint, limpia memoria incluso si red falla y no deja loops.

## Acceptance criteria

- Test concurrente demuestra exactamente una llamada refresh para multiples 401.
- No existe retry infinito ni refresh recursivo.
- Reload puede restaurar sesion mediante cookie same-origin.
- Desarrollo usa proxy Angular, no CORS abierto ni URL Render desde browser.
- Login solo consume `returnUrl` interno normalizado; URL protocol-relative, host o
  esquema externo queda descartada. Header ofrece logout y borra memoria antes de que
  una red colgada pueda demorar la limpieza local.

## Verification

```powershell
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```

## Evidence

- `npm ci` PASS (2026-07-20). `npm audit --omit=dev` reporta 0 vulnerabilidades runtime;
  el audit completo conserva los 5 advisories transitivos de desarrollo ya registrados
  por el gate Angular y no se ejecuto un fix forzado.
- `npm run build` PASS.
- `npm test -- --watch=false --browsers=ChromeHeadless` PASS: 110/110.
- Tests `HttpTestingController` prueban bootstrap exactamente una vez y anonimo sin
  redirect; guard `/favorites`; Bearer unicamente para rutas relativas `/api/*`; una
  rotacion para dos 401 y retry maximo una vez; endpoints `/auth/*`/URL externa sin
  Bearer ni refresh; fallo concurrente que limpia memoria y navega una vez; logout
  sincrono best-effort, incluido un refresh previo que no puede restaurar sesion tras
  logout o reintentar una request vieja con una cuenta nueva; proxy development y
  `returnUrl` interno seguro.

## Handoff

- W10 migra la experiencia APOD/mock sin mover ni remover la inicializacion de sesion,
  el interceptor o el control de cuenta del header.
- W11 consume `AuthService.sessionChange` (`previousUserId`, `currentUser`) para vaciar
  favoritos al logout o cambio de usuario; no debe inferir identidad desde storage.

## Parent sync

- [x] Actualizar `R3.9`, master/readiness y estado con evidencia.
