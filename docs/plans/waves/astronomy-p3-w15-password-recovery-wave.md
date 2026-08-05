# Wave P3-W15 - Recuperacion de contraseña segura

Date: 2026-08-05
Status: PLANNED
Wave ID: `P3-W15`
Depends On: P3-W2 + P3-W3 + P3-W8 + P3-W9 + P3-W12 + P3-W13 DONE and merged
Unblocks: external execution and production smoke of P3-W14
Suggested Branch: `wave/p3-w15-password-recovery`

## Objective

Permitir a una cuenta confirmada recuperar su contraseña sin enumerar usuarios, sin
persistir códigos en el navegador y sin dejar refresh sessions renovables con la clave
anterior. La demostración debe funcionar con `Email:Provider=LocalLog`; W14 sólo añade el
sender real y el smoke externo.

## File scope

- `backend/AstronomyExplorer.Api/Auth/AccountEndpoints.cs` y DTOs de cuenta.
- `backend/AstronomyExplorer.Api/Auth/RefreshSessionService.cs` y, si mejora la
  atomicidad, un servicio dedicado de reset que comparta el `AppDbContext`/Identity store.
- `backend/AstronomyExplorer.Api/Security/AccountRateLimiting.cs`, `Program.cs` y
  `netlify.toml` para límites request/reset coherentes entre local y edge.
- `backend/AstronomyExplorer.Api/Email/` para el factory de enlace y plantilla de reset;
  se reutiliza `IEmailSender` y no se agrega un proveedor.
- `backend/AstronomyExplorer.Api.Tests/Auth/Account/` y `Auth/Sessions/`.
- `src/app/services/auth.service.ts` y pruebas, `app.routes.ts`, Login y los nuevos
  componentes lazy `forgot-password`/`reset-password` con sus pruebas.
- Los documentos P3, W14, runbooks y flujo actualizados en este commit de planificación.

No se modifica el esquema, no se crean cuentas de proveedor y no se agrega Web Storage,
OAuth, MFA, cambio de email ni auto-login.

## Contract and security decisions

1. `POST /auth/forgot-password` recibe `{ email }` y devuelve el mismo `202`/mensaje para
   email existente, inexistente, no confirmado, vacío o inválido. Solo el usuario existente
   y confirmado recibe correo. Se limita por IP y hash de email normalizado.
2. El correo usa un token de password-reset de ASP.NET Core Identity, Base64URL y
   HTML-encoding. Enlaza a `/reset-password?userId=<guid>&code=<base64url>`; no hay token
   propio, persistencia raw ni logs productivos del enlace.
3. `POST /auth/reset-password` recibe `{ userId, code, password }`. GUID/código inválido,
   usuario ausente, token vencido/reutilizado o fallo Identity devuelven el mismo `400`
   ProblemDetails sin distinguir el motivo. El éxito es `204` sin cookie/JWT.
4. El reset exitoso actualiza la contraseña Identity y revoca todas las refresh sessions
   activas del usuario dentro de la misma unidad de persistencia antes del `204`; además
   expira una refresh cookie aportada y Angular limpia su JWT/usuario en memoria. JWT ya
   emitido en otro navegador puede vivir sólo hasta su expiración corta; no podrá refresh.
5. Angular valida el link y hace `Location.replaceState('/reset-password')` antes de
   mostrar el formulario; el código permanece en memoria y se descarta tras un intento.
   Tras éxito navega a Login con estado transitorio, sin auto-login.
6. W14 extiende las rewrites firmadas con límites específicos para request/reset y prueba
   el flujo con Resend. La URL Render directa sigue bloqueada por la firma Netlify.
   Request/reset son anónimos, `no-store` y no usan el filtro Origin de refresh/logout:
   el token Identity es la capacidad de un solo uso que autoriza el reset.

## Implementation checklist

- [ ] W15.1 Agregar DTOs y endpoints genéricos request/reset, validación Base64URL y
  ProblemDetails controlado; preservar anti-enumeración y no-store donde corresponda.
- [ ] W15.2 Generar plantilla/link de reset con Identity + `IEmailSender`; LocalLog entrega
  el enlace sólo en Development y Resend conserva el mismo contrato genérico.
- [ ] W15.3 Revocar en bloque refresh sessions al éxito y verificar que una cookie previa
  no puede renovar, mientras la contraseña vieja no puede login.
- [ ] W15.4 Agregar límites IP/hash-email locales y rewrites/edge limits para ambos POSTs
  sin confiar en forwarded headers ni crear un secret nuevo.
- [ ] W15.5 Añadir rutas/componentes Angular, link desde Login, validación accesible,
  URL scrub, limpieza de sesión propia, estados success/error y pruebas sin token en storage.
- [ ] W15.6 Actualizar el smoke LocalLog y W14 production smoke para recuperación; no
  registrar links, contraseñas, correos ni valores secretos en evidencia.

## Acceptance criteria

- La respuesta request es indistinguible entre cuenta existente, ausente y no confirmada;
  solo la existente/confirmada agrega un mensaje al fake/LocalLog sender.
- Enlaces con GUID/código Base64URL correctos se resetean una vez; inválidos, vencidos,
  reusados o de usuario inexistente reciben el mismo `400` sin sesión nueva.
- Reset exitoso revoca todas las refresh sessions: una cookie previa responde `401` en
  refresh, se expira en ese navegador, Angular limpia memoria y el password anterior no
  puede login; el password nuevo sí.
- Login enlaza al request; la SPA no conserva código, contraseña, JWT ni refresh token en
  localStorage/sessionStorage y no muestra información de existencia de cuenta.
- Los límites request/reset producen `429` ProblemDetails locales y sus redirects firmadas
  quedan antes de `/auth/*` genérico.
- Backend, frontend, Compose y smoke local cubren registro -> confirmación -> login ->
  forgot -> reset -> nuevo login -> favoritos -> logout sin proveedor externo.
- W14 queda explícitamente pendiente de ejecutar este flujo contra Resend/Netlify/Render;
  `main` no se promociona por W15 sola.

## Verification

```powershell
dotnet build backend/AstronomyExplorer.sln --no-restore
dotnet test backend/AstronomyExplorer.sln --no-build
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
docker compose config
docker compose up -d --build
docker compose ps
Invoke-WebRequest http://localhost:5179/health
```

Ejecutar además el flujo completo de `docs/deploy/p3-local-runbook.md`, incluida la
extracción local del último enlace reset. No ejecutar seed Neon, Resend, Render ni
Netlify: siguen siendo autoridad exclusiva de W14.
