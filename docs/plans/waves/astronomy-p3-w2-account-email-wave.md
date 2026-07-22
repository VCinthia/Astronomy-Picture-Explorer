# Wave P3-W2 - Registro, email y confirmacion

Date: 2026-07-16
Last revised: 2026-07-17
Status: DONE
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
- `backend/AstronomyExplorer.Api/Data/AppDbContext.cs`
- `backend/AstronomyExplorer.Api/Migrations/`
- `backend/AstronomyExplorer.Api/AstronomyExplorer.Api.csproj`
- `backend/AstronomyExplorer.Api.Tests/Auth/Account/`

## Checklist

- [x] W2.1 `POST /auth/register` crea Identity user no confirmado y devuelve respuesta
  generica para duplicados.
- [x] W2.2 `IEmailSender` + Resend adapter; tests usan fake y nunca red real.
- [x] W2.3 Link frontend exacto `/confirm-email?userId=<guid>&code=<base64url>`.
- [x] W2.4 `POST /auth/confirm-email {userId, code}` decodifica y usa
  `ConfirmEmailAsync`; invalido/vencido/reusado es controlado.
- [x] W2.5 `POST /auth/resend-confirmation` es generico y solo envia cuando corresponde.
- [x] W2.6 Rate limiting por IP/email normalizado protege register/resend y cuota Resend.

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
dotnet test backend/AstronomyExplorer.sln
dotnet format backend/AstronomyExplorer.sln --verify-no-changes
dotnet ef migrations list --project backend/AstronomyExplorer.Api --no-connect
dotnet list backend/AstronomyExplorer.sln package --vulnerable --include-transitive
```

## Parent sync

- [x] Actualizar `R3.2`, master/readiness y estado con evidencia.

## Completion evidence - 2026-07-17

- Build PASS con 0 warnings y 0 errors.
- 13/13 tests Account y 24/24 tests backend completos PASS sobre PostgreSQL
  Testcontainers; el sender fake evita red real.
- Register -> email capturado -> confirm POST activa el usuario; otra instancia de la
  aplicacion confirma el mismo link usando el key ring persistido en PostgreSQL.
- Duplicado, resend de inexistente/confirmado y confirmacion invalida, vencida o
  reutilizada mantienen contratos genericos/controlados.
- Limites independientes por IP y email normalizado producen 429
  `application/problem+json`; las particiones email usan hash, limite y expiracion.
- El adaptador Resend cumple `POST /emails`, Bearer, payload oficial y `User-Agent` sin
  realizar una llamada externa.
- El maximo de email es 256, alineado con las columnas Identity; 257 caracteres no
  crean usuario ni email y nunca alcanzan una truncacion PostgreSQL.
- Format, diff-check, listado de ambas migraciones y audit NuGet vulnerable/transitive
  PASS sin hallazgos.

## Implemented design clarifications

- Identity Data Protection ya no depende del filesystem efimero del host. La migracion
  W2 `PersistDataProtectionKeys` guarda el key ring compartido en PostgreSQL con
  application name estable `AstronomyExplorer`; la migracion inicial W1 no fue alterada.
- La proteccion en reposo del XML del key ring depende de los controles/cifrado del
  proveedor PostgreSQL. W14 debe verificar esa garantia y documentar si requiere
  proteccion adicional antes del deploy.
- W2 limita la IP de transporte (`RemoteIpAddress`) y no confia ciegamente en
  `X-Forwarded-For`. W14 debe verificar la cadena Netlify -> Render y configurar solo
  proxies confiables antes de afirmar que la particion representa al visitante real.

## Planning clarification (2026-07-22)

- La promoción productiva originalmente numerada W13 pasa a W14 porque se incorpora W13
  para UX final y aceptación local. No cambia el contrato, la seguridad ni la evidencia
  implementada de W2; únicamente desplaza sus gates de proveedor a la wave correcta.

## Design clarification - P3-W14 (2026-07-22)

La decisión pendiente sobre forwarders se resolvió sin reescribir esta evidencia W2:
Render no acepta `X-Forwarded-For` como IP pública. Producción usa redirects Netlify
firmadas y límites de borde por IP; la API conserva el límite por email normalizado y
rechaza bypass directo/spoof antes de llegar a estos endpoints. La evidencia real de
proveedores sigue pendiente en W14.
