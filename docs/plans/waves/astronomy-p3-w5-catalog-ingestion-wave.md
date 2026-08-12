# Wave P3-W5 - Ingestion de catalogo resumible

Date: 2026-07-16
Status: DONE
Wave ID: `P3-W5`
Depends On: P3-W4 merged
Suggested Branch: `wave/p3-w5-catalog-ingestion`

## Goal

Crear un comando local idempotente que cargue APOD por rangos, persista checkpoints y
pueda poblar Neon sin usar jobs/compute de Render ni generar costo.

## File scope

- `backend/AstronomyExplorer.Catalog/`
- `backend/AstronomyExplorer.Api/Apod/CatalogStatusEndpoint.cs`
- `backend/AstronomyExplorer.Api/Data/`
- `backend/AstronomyExplorer.Api/Migrations/`
- `backend/AstronomyExplorer.Api.Tests/Catalog/`
- `backend/README.md`

## Checklist

- [x] W5.1 CLI `catalog sync --from --to --batch-size 30 --resume --dry-run`.
- [x] W5.2 Cada batch usa NASA `start_date/end_date`, upsert transaccional y checkpoint
  solo despues de commit.
- [x] W5.3 Retry/backoff acotado; 429 respeta `Retry-After` y detiene/reanuda seguro.
- [x] W5.4 Lock logico evita dos sync del mismo rango; resume no duplica ni salta fechas.
- [x] W5.5 `catalog-status` devuelve coverage/count/status y marca ready solo al completar
  el rango objetivo.
- [x] W5.6 Preflight muestra request count estimado y prohibe ejecutar desde environment
  Production/Render salvo override explicito de desarrollo documentado.

## Acceptance criteria

- Interrumpir y reanudar produce el mismo catalogo que una corrida completa.
- Tests NASA son mock; ninguna suite consume cuota real.
- El comando no corre en API startup ni requiere scheduler.
- Dry-run y status permiten estimar/verificar antes de tocar Neon.
- Runbook deja claro que se usa API key propia y planes $0 sin overages.

## Verification

```powershell
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln --filter "FullyQualifiedName~Catalog"
dotnet run --project backend/AstronomyExplorer.Catalog -- catalog sync --from 2026-01-01 --to 2026-01-31 --dry-run
```

## Parent sync

- [x] Actualizar `R3.5`, master/readiness y estado con evidencia.

## Implementation evidence - 2026-07-17

- `dotnet build backend/AstronomyExplorer.sln`: PASS, 0 warnings y 0 errors.
- Filtro Catalog: 55/55 PASS con mocks HTTP y PostgreSQL 17 Testcontainers.
- Suite backend completa: 132/132 PASS.
- Dry-run de verificacion: rango `2026-01-01..2026-01-31`, batch 30, dos requests
  estimadas, sin leer DB/key ni abrir red.

## Implementation clarification

- El lock advisory es global para el catalogo, no por rango: tambien impide dos corridas
  sobre rangos parcialmente solapados. Una conexion dedicada conserva el lock durante
  toda la corrida; cada fetch NASA ocurre fuera de la transaccion del batch.
- La respuesta de rango puede ser vacia, dispersa o desordenada. Antes de persistir se
  ordena y valida que no haya elementos null, fechas duplicadas ni fechas fuera del
  batch, ademas del contrato image/video/URLs/service v1.
- `apod_entries` upsert y `last_completed_date/status` se confirman en la misma
  transaccion; `synced_entry_count` suma solo filas devueltas, no dias calendario. Un
  batch fallido no deja filas parciales ni adelanta el checkpoint/count.
- 408/5xx/network/timeout son transitorios y dejan `Paused`; 4xx permanente o payload
  invalido dejan `Failed`. 429 no duerme: persiste `retry_not_before`, usa una ventana
  segura de una hora cuando falta `Retry-After`, y un `--resume` temprano falla antes
  de llamar NASA.
- `Catalog__RequiredFrom/To` define el unico target canónico de readiness y W14 lo fija
  al seed aprobado. Sin configuracion/estado usa `not_started`; un sync ad-hoc mas nuevo
  no lo reemplaza. Ready exige Completed, checkpoint final y row count al menos igual a
  `synced_entry_count`.
- Un Completed integro es no-op. Si el row count cae debajo del count sincronizado,
  falla sin `--resume`; con resume reinicia checkpoint/count y reejecuta el rango entero.
- La conexion dedicada del lock tiene heartbeat. Perder la sesion cancela mediante
  `LockLostToken`, deja Paused y evita confirmar el batch en curso.
- Render queda bloqueado sin excepcion. `--allow-local-production` solo habilita una
  consola local marcada Production; no autoriza mutaciones productivas antes de W14.

## Planning clarification (2026-07-22)

- La wave de proveedor/seed/deploy se renumera de W13 a W14 al insertar W13 de UX y
  aceptación local. La seguridad, límites y evidencia implementada de W5 no cambian.

## Design clarification (2026-08-12)

- La ejecución real W14 verificó que una respuesta NASA APOD de 30 fechas puede superar
  los 8 segundos (10,8 s en la validación). El CLI conserva dos intentos acotados y el
  comportamiento resumible, pero su timeout por request pasa a 30 segundos. No afecta
  los endpoints interactivos ni habilita jobs, cron o consumo de Render.
