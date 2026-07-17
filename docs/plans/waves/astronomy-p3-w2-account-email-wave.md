# Wave P3-W2 - Registro, email y confirmacion

Date: 2026-07-16
Status: READY - Not Started
Wave ID: `P3-W2`
Depends On: P3-W1 merged
Suggested Branch: `wave/p3-w2-account-email`

## Goal

Implementar el ciclo de cuenta previo a sesiones: registro, envio/reenvio mediante
Resend abstraction y confirmacion POST con `userId + code` Base64URL.

## File scope

- `backend/AstronomyExplorer.Api/Auth/AccountEndpoints.cs`
- `backend/AstronomyExplorer.Api/Auth/Dtos/`
- `backend/AstronomyExplorer.Api/Email/`
- `backend/AstronomyExplorer.Api/Security/`
- `backend/AstronomyExplorer.Api/Program.cs`
- `backend/AstronomyExplorer.Api.Tests/Auth/Account/`

## Checklist

- [ ] W2.1 `POST /auth/register` crea Identity user no confirmado y devuelve respuesta
  generica para duplicados.
- [ ] W2.2 `IEmailSender` + Resend adapter; tests usan fake y nunca red real.
- [ ] W2.3 Link frontend exacto `/confirm-email?userId=<guid>&code=<base64url>`.
- [ ] W2.4 `POST /auth/confirm-email {userId, code}` decodifica y usa
  `ConfirmEmailAsync`; invalido/vencido/reusado es controlado.
- [ ] W2.5 `POST /auth/resend-confirmation` es generico y solo envia cuando corresponde.
- [ ] W2.6 Rate limiting por IP/email normalizado protege register/resend y cuota Resend.

## Acceptance criteria

- Register -> email fake -> confirm POST activa la cuenta.
- El link contiene ambos parametros y el token es URL-safe.
- Duplicado, usuario inexistente y reenvio no permiten enumeracion directa.
- No se guarda token raw ni se llama Resend en tests.
- Limites producen 429/ProblemDetails observable.

## Verification

```powershell
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln --filter "FullyQualifiedName~Account"
```

## Parent sync

- [ ] Actualizar `R3.2`, master/readiness y estado con evidencia.
