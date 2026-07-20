# Engineering Readiness - Astronomy Picture Explorer

Date: 2026-07-20
Status: P2 DONE in production; P3 IN PROGRESS - W1-W6 DONE

## Verdict

P1 y P2 estan cerradas. El commit de cierre P2-W4 es `b72c7e2`; la aplicacion
productiva responde en Netlify y el smoke del 2026-07-16 valido navegacion, search,
persistencia de favoritos entre rutas y breakpoints desktop/mobile.

P3 fue corregida antes de iniciar codigo. P3-W1 implemento la foundation .NET 10,
Identity user-only, schema PostgreSQL, FTS ponderado y health DB-aware. P3-W2 completo
registro, email y confirmacion con key ring persistido y rate limits. P3-W3 implemento
sesiones seguras, P3-W4 cerro NASA today/date con cache y P3-W5 completo la ingestion
local resumible; las 8 waves restantes
conservan el contrato aceptado.

## Closed gates

- P1 DONE, tag `v1.0.0`, deploy publico activo.
- P2-W1..W4 integradas a `main` y `origin/main` (`0 ahead / 0 behind`).
- Build/test de cierre P2: 77/77 tests PASS; build PASS.
- Produccion HTTP: `/`, `/explorer` y `/favorites` servidas por Netlify.
- Smoke headless Chrome, `2026-07-16T18:55:59Z`:
  - Home renderizo marca y links `/`, `/explorer`, `/favorites`.
  - Toggle guardo `2026-06-09` en `ape.favorites.v1`; `/favorites` mostro
    `Thor's Helmet` despues de navegar.
  - Search `Hydra` encontro `Hydra Cluster` despues del debounce.
  - viewport 390x844 oculto nav `Primary` y mostro `Mobile primary` fixed.
  - almacenamiento temporal del smoke fue limpiado al finalizar.
- ADR-0003 revisado y aceptado el 2026-07-16.
- Contrato APOD operativo verificado con respuestas NASA image/video.

## P3-W1 completion gate

P3-W1 se cerro sin cuentas externas ni mutaciones productivas:

- Build backend PASS, 0 warnings y 0 errors.
- 11/11 tests PASS con PostgreSQL 17 Testcontainers y migracion real.
- PK/FK/checks/delete behaviors, nullable APOD, UTC, `tsvector` stored/GIN verificados.
- `/health` devuelve 200/503 segun disponibilidad de PostgreSQL.
- OpenAPI existe solo en Development.
- EF lista la migracion sin conexion; runtime/database update fallan cerrados sin config.

## P3-W2 completion gate

P3-W2 se cerro sin cuentas Resend ni mutaciones productivas:

- Build backend PASS, 0 warnings y 0 errors.
- 13/13 tests Account y 24/24 backend completos PASS con PostgreSQL Testcontainers.
- Register/email fake/confirm POST, duplicado, resend, invalido, vencido y reutilizado
  tienen evidencia observable.
- Data Protection persiste el key ring en PostgreSQL; confirmacion factory A -> factory
  B prueba supervivencia a reinicio.
- Limites IP/email normalizado devuelven 429 ProblemDetails; adaptador Resend se prueba
  con handler en memoria y `User-Agent`, sin red real.
- Format, migraciones y audit NuGet vulnerable/transitive PASS.

## P3-W3 completion gate

- Build backend PASS con 0 warnings y 0 errors.
- 23/23 tests Sessions y 47/47 backend PASS con PostgreSQL Testcontainers.
- JWT valida HMAC/issuer/audience/lifetime sin clock skew y expone claims requeridos.
- Cookie refresh es host-only, HttpOnly, SameSite=Lax, Path `/auth`, expiracion explicita
  y Secure salvo HTTP loopback exclusivamente en Development.
- Advisory lock transaccional por familia serializa rotate/logout; carreras de replay o
  logout contra rotation terminan sin sesiones activas en esa familia y no afectan otra.
- Refresh/logout validan Origin exacto en Production antes de mutar DB/cookie; logout no
  depende de un Bearer vigente.
- Login se limita por IP sin particion email para no habilitar DoS dirigido. W13 mantiene
  el gate de forwarders confiables antes de interpretar la IP publica.

## P3-W4 completion gate

- Build backend PASS con 0 warnings y 0 errors.
- 31/31 tests W4 y 78/78 backend PASS; cache/persistencia usan PostgreSQL Testcontainers.
- `GET /api/apod/today` usa fecha UTC inyectable y `/date/{date}` acepta exclusivamente
  `1995-06-16..hoy`; formato/rango y upstream devuelven ProblemDetails observables.
- El adaptador valida imagen/video, URLs HTTP(S), fecha solicitada y `service_version=v1`;
  opcionales vacios pasan a null y metadata NASA no cruza el DTO app-owned.
- `X-Api-Key` queda fuera de la query y redacted en logging; redirects y 429 no se
  reintentan. Timeout/network/5xx usan como maximo dos intentos y errores sanitizados.
- Memory cache tiene lifetime/capacidad validados. Single-flight por fecha usa scope
  propio, timeout operativo y cleanup; PostgreSQL persiste con upsert `ON CONFLICT`.
- Tests prueban reutilizacion entre instancias, concurrencia y retry posterior a fallo;
  handlers/fakes en memoria garantizan cero llamadas NASA reales.

## P3-W5 completion gate

- Build backend PASS con 0 warnings y 0 errors; 55/55 Catalog y 132/132 backend PASS.
- Dry-run calcula rango/batches sin leer environment ni abrir DB/red. Live exige
  PostgreSQL y key NASA personal; `DEMO_KEY` y cualquier entorno Render fallan cerrados.
- Cliente range envia solo `start_date`, `end_date`, `thumbs=true`; la key viaja en
  `X-Api-Key`, nunca en query/logs. Acepta arrays historicos vacios/dispersos/desordenados
  y rechaza null, fechas duplicadas/fuera del batch o payload invalido antes de persistir.
- Lock advisory global con conexion dedicada excluye tambien rangos solapados. Fetch
  ocurre fuera de transaccion; upserts y checkpoint se confirman o revierten juntos.
- 408/5xx/network/timeout pausan; payload/4xx permanente fallan. 429 persiste
  `retry_not_before` con fallback de una hora y bloquea resume temprano sin llamar NASA.
- `synced_entry_count` avanza atomicamente con cada checkpoint y cuenta entries NASA,
  no dias calendario. Completed con drift exige resume y repara desde el inicio.
- `Catalog__RequiredFrom/To` fija el target canónico; sin config/state status es
  `not_started`. Ready exige Completed + checkpoint final + row count >= synced count;
  un sync pequeño posterior no puede sustituirlo.
- Heartbeat sobre la conexion dedicada detecta perdida del advisory lock, cancela el
  trabajo linked y deja Paused sin adelantar checkpoint.

## P3-W6 completion gate

- Build Release PASS con 0 warnings/errors; 18/18 tests W6 y 150/150 backend PASS sobre
  PostgreSQL 17 Testcontainers.
- `GET /api/apod/search` devuelve `ApodEntryDto[]`, recorta `q`, limita query a 200,
  `page` a 1..1000 y `pageSize` a 1..30 con default 12, antes de readiness/DB.
- Query parametrizada usa `websearch_to_tsquery('english', q)`, vector A/B, `ts_rank`,
  fecha descendente, projection y limites en PostgreSQL. `EXPLAIN` confirma el GIN.
- Readiness es una politica interna compartida por status/search. Target ausente,
  incompleto o con drift produce `503 catalog_not_ready`; search nunca llama NASA.
- Stemming ingles funciona; prefijos y typos no. `pg_trgm` queda deshabilitado porque el
  beneficio no justifica extension, indice adicional ni ranking mixto en este portfolio.

Antes de cada wave restante:

- Confirmar que ADR-0003, P3 y wave siguen sincronizados.
- Crear branch desde `codex/p3-integration` limpio y sincronizado.
- Mantener secrets fuera del repo.
- Ejecutar los comandos de verification de la wave.

Recursos externos se habilitan just-in-time:

- Resend/dominio: necesario para smoke real W13; W2 usa fake en tests.
- NASA key propia: necesaria para el seed real autorizado en W13; W5 usa mocks/dry-run.
- Neon: necesario para seed/deploy, no para Testcontainers W1.
- Render/URL final: necesario solo en W13.

## Current validation contract

Frontend:

```powershell
npm ci
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```

Backend desde W1:

```powershell
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln
```

Stack completo desde W12:

```powershell
docker compose config
docker compose up -d --build
Invoke-WebRequest http://localhost:<api-port>/health
docker compose down
```

## P3 risks and mitigations

| Risk | Required mitigation |
|---|---|
| NASA no ofrece keyword search remoto | PostgreSQL FTS sobre title+explanation |
| Catalogo incompleto | CLI resumible + checkpoint + politica shared readiness/status; search 503 until ready |
| Backfill suspende Render/consume recursos | Ejecutar desde desarrollo contra Neon; nunca en API startup |
| Cookie third-party bloqueada | Browser usa Netlify same-origin proxy; cookie host-only SameSite=Lax |
| CSRF sobre refresh/logout | SameSite=Lax + Origin exacto; CORS no se usa como defensa |
| 401 simultaneos consumen refresh dos veces | Angular single-flight + backend rotation atomica |
| Token de confirmacion ambiguo/filtrado | userId+code Base64URL; Angular POST; no mutacion GET |
| Key ring perdido en filesystem efimero | Data Protection keys en PostgreSQL; revisar cifrado en reposo en W13 |
| IP real oculta por Netlify/Render | Fail-closed sobre peer; W13 configura solo proxies verificados y prueba particiones |
| Render cold start confunde primera visita | Estado connecting + timeout + Retry accesible |
| Search costoso | tsvector + GIN; q max 200, page max 1000, pageSize max 30; sin trigram |
| Cuota/cargo inesperado | Free-only, no keepalive/cron/overages; fail closed y revalidar en W13 |
| DTO diverge de NASA/frontend | DTO app-owned congelado + contract tests image/video/nulls |
| Angular 19 tiene 6 high + 1 moderate en `npm audit --omit=dev` | Decidir upgrade mayor en rama dedicada antes de W8-W11/W13; nunca aplicar `--force` dentro de una wave backend |

## Recommended next step

Ejecutar P3-W7 desde `codex/p3-integration`. W7 implementa Favorites API protegida e
hidratada sobre las bases de auth/APOD ya cerradas. Neon/Render/Resend, seed productivo
y NASA real siguen postergados hasta W13.
