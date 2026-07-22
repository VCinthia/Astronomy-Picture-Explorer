# Wave P3-W3 - Login y sesiones seguras

Date: 2026-07-16
Status: DONE - 2026-07-17
Wave ID: `P3-W3`
Depends On: P3-W2 merged
Suggested Branch: `wave/p3-w3-auth-sessions`

## Goal

Implementar login, access JWT, refresh rotation/reuse y logout con transacciones
atomicas, cookie same-origin y defensa CSRF por `Origin`.

## File scope

- `backend/AstronomyExplorer.Api/Auth/SessionEndpoints.cs`
- `backend/AstronomyExplorer.Api/Auth/JwtTokenService.cs`
- `backend/AstronomyExplorer.Api/Auth/RefreshSessionService.cs`
- `backend/AstronomyExplorer.Api/Security/AllowedOriginFilter.cs`
- `backend/AstronomyExplorer.Api/Program.cs`
- `backend/AstronomyExplorer.Api.Tests/Auth/Sessions/`

## Checklist

- [x] W3.1 Login: credencial invalida -> `401 invalid_credentials`; password valida sin
  confirmar -> `403 email_unconfirmed`; ok emite JWT 10 min + refresh.
- [x] W3.2 Cookie host-only `Secure`, `HttpOnly`, `SameSite=Lax`, `Path=/auth`, Max-Age
  explicito; excepcion HTTP local solo en Development.
- [x] W3.3 DB guarda hash unico y metadata de sesion; nunca token raw.
- [x] W3.4 Refresh consume/rota atomicamente; concurrencia permite un solo consumidor.
- [x] W3.5 Reuse revoca familia; logout revoca sesion y expira cookie.
- [x] W3.6 Refresh/logout exigen `Origin` exacto configurado en Production.
- [x] W3.7 Issuer/audience/key y allowed origin solo por config segura.

## Acceptance criteria

- Suite cubre login, expiracion, rotation, dos refresh simultaneos, replay y logout.
- Origin faltante/no permitido falla en Production; permitido funciona via proxy.
- JWT nunca se persiste server-side y refresh raw nunca aparece en DB/log/error.
- Endpoints de sesiones no permiten loops ni respuestas sensibles.

## Verification

```powershell
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln --filter "FullyQualifiedName~Sessions"
```

Evidence 2026-07-17:

- Backend build PASS con 0 warnings/0 errors.
- Sessions 23/23 y suite backend 47/47 PASS sobre PostgreSQL 17 Testcontainers.
- Refresh concurrente entrega un solo 200; el request perdedor detecta replay y revoca
  toda la familia activa.
- Format, modelo EF sin cambios pendientes, audit NuGet y `git diff --check` PASS.

## Implemented design clarifications

- JWT HMAC valida firma, issuer, audience y lifetime con `ClockSkew=0`; contiene `sub`,
  `email`, `jti`, `iat` y `client_id`. El access lifetime default es 10 minutos.
- Refresh usa 32 bytes aleatorios Base64URL y persiste SHA-256 hexadecimal. Rotate y
  logout toman un advisory lock PostgreSQL transaccional por familia, reconsultan estado
  bajo el lock y revocan la familia activa ante replay/logout.
- Login inexistente verifica un hash dummy con el mismo `IPasswordHasher`; password
  incorrecta o vacia ejecuta una verificacion sin medir tiempos fragiles.
- JWT restringe algoritmos validos a HS256; un HS512 bien firmado con la misma key se
  rechaza por contrato.
- Login tiene fixed-window limiter solo por IP de transporte (10/15 min, queue 0). No se
  particiona por email para evitar un DoS dirigido contra una cuenta concreta.
- Produccion exige un unico Origin exacto antes de leer/mutar cookie o DB. Development
  solo permite cookie no Secure para HTTP sobre un host loopback.
- W9 debe mantener single-flight: concurrencia normal no debe consumir dos veces un
  refresh; si ocurre, el backend la trata como replay y cierra la familia.

## Design clarification - P3-W14 (2026-07-22)

La IP pública de las policies de cuenta no se deriva de Forwarded Headers en Render.
Netlify firmará los proxy redirects y aplicará el límite por visitante; la API valida el
JWS antes de `/auth/*` y deja sin cuota su limitador por IP de transporte en Production
para no agrupar usuarios tras el proxy. Refresh/logout y su validación Origin no cambian.

## Parent sync

- [x] Actualizar `R3.3`, master/readiness y estado con evidencia.
