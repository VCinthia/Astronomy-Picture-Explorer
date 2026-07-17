# Wave P3-W9 - Bootstrap, guard e interceptor de sesion

Date: 2026-07-16
Status: READY - Not Started
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
- `proxy.conf.json`

## Checklist

- [ ] W9.1 Bootstrap intenta un refresh una vez y expone estado `checking/auth/anon`.
- [ ] W9.2 Guard espera bootstrap y redirige `/favorites` a
  `/login?returnUrl=/favorites` solo si anon.
- [ ] W9.3 Interceptor adjunta Bearer solo a API propia.
- [ ] W9.4 Varios 401 comparten una unica operacion refresh; cada request se reintenta
  maximo una vez con el nuevo token.
- [ ] W9.5 Login/refresh/logout se excluyen del auto-refresh.
- [ ] W9.6 Fallo refresh limpia estado, falla la cola y redirige una sola vez.
- [ ] W9.7 Logout llama endpoint, limpia memoria incluso si red falla y no deja loops.

## Acceptance criteria

- Test concurrente demuestra exactamente una llamada refresh para multiples 401.
- No existe retry infinito ni refresh recursivo.
- Reload puede restaurar sesion mediante cookie same-origin.
- Desarrollo usa proxy Angular, no CORS abierto ni URL Render desde browser.

## Verification

```powershell
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```

## Parent sync

- [ ] Actualizar `R3.9`, master/readiness y estado con evidencia.
