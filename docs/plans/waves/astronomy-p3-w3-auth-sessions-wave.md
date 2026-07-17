# Wave P3-W3 - Login y sesiones seguras

Date: 2026-07-16
Status: READY - Not Started
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

- [ ] W3.1 Login: credencial invalida -> `401 invalid_credentials`; password valida sin
  confirmar -> `403 email_unconfirmed`; ok emite JWT 10 min + refresh.
- [ ] W3.2 Cookie host-only `Secure`, `HttpOnly`, `SameSite=Lax`, `Path=/auth`, Max-Age
  explicito; excepcion HTTP local solo en Development.
- [ ] W3.3 DB guarda hash unico y metadata de sesion; nunca token raw.
- [ ] W3.4 Refresh consume/rota atomicamente; concurrencia permite un solo consumidor.
- [ ] W3.5 Reuse revoca familia; logout revoca sesion y expira cookie.
- [ ] W3.6 Refresh/logout exigen `Origin` exacto configurado en Production.
- [ ] W3.7 Issuer/audience/key y allowed origin solo por config segura.

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

## Parent sync

- [ ] Actualizar `R3.3`, master/readiness y estado con evidencia.
