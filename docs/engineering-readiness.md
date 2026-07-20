# Engineering Readiness - Astronomy Picture Explorer

Date: 2026-07-20
Status: P2 DONE in production; P3 IN PROGRESS - W1-W9 DONE

## Verdict

P1 y P2 estan cerradas. El commit de cierre P2-W4 es `b72c7e2`; la aplicacion
productiva responde en Netlify y el smoke del 2026-07-16 valido navegacion, search,
persistencia de favoritos entre rutas y breakpoints desktop/mobile.

P3 fue corregida antes de iniciar codigo. P3-W1 implemento la foundation .NET 10,
Identity user-only, schema PostgreSQL, FTS ponderado y health DB-aware. P3-W2 completo
registro, email y confirmacion con key ring persistido y rate limits. P3-W3 implemento
sesiones seguras, P3-W4 cerro NASA today/date con cache, P3-W5 completo la ingestion
local resumible, P3-W6 cerro FTS, P3-W7 completo Favorites API protegida y P3-W8
materializo las pantallas Angular de cuenta. P3-W9 completo bootstrap/guard/interceptor
y logout frontend; las waves restantes conservan el contrato aceptado.

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

## P3-W7 completion gate

- Los tres endpoints `/api/favorites` requieren Bearer y derivan el usuario exclusivamente
  del claim JWT `sub`; la ausencia de Bearer devuelve 401 y un `sub` firmado no GUID
  devuelve `401 invalid_authenticated_user` sin aceptar identificadores del cliente.
- POST acepta solo `{ "apod_date": "YYYY-MM-DD" }`; POST/DELETE validan
  `1995-06-16..UTC today` antes de acceder a cache/NASA y sus fechas invalidas devuelven
  `400 invalid_favorite_apod_date`.
- Un miss de POST usa `ApodCacheService`; errores NASA conservan ProblemDetails APOD
  sanitizados. `ON CONFLICT DO NOTHING` deja una sola relacion bajo POST concurrente.
- GET hace una unica proyeccion/join `favorites -> apod_entries`, filtrada por usuario,
  ordenada por fecha APOD descendente y devuelve solo `ApodEntryDto[]`. No pagina ni
  limita silenciosamente porque W11 carga la coleccion completa por sesion de portfolio.
- Build Release quedo PASS sin warnings/errors; Favorites 9/9 y backend 159/159 PASS
  sobre PostgreSQL 17 Testcontainers. Review independiente, `dotnet format
  --verify-no-changes` y `git diff --check` tambien PASS.

## Angular security-maintenance gate (2026-07-20) — CLOSED

- La rama dedicada `maintenance/angular-22-security-update` actualizo Angular 19.2 de
  forma secuencial por 20.3 y 21.2 hasta Angular 22.0.7, con CLI, devkit y
  compiler-cli alineados y TypeScript 6.0.3.
- Solo se aplicaron las migraciones obligatorias de `ng update`; no se uso
  `npm audit fix --force` ni se introdujo la migracion opcional Karma -> Vitest/build
  system en una correccion de seguridad.
- `npm ci`, `npm run build`, 77/77 pruebas ChromeHeadless, `npm audit --omit=dev`
  (0 vulnerabilidades runtime), `dotnet build` sin warnings y 159/159 backend PASS.
- El audit completo conserva 5 advisories de desarrollo (1 low, 4 moderate), todos
  transitivos del devkit: `webpack-dev-server`/`sockjs`/`uuid` y `esbuild` bajo Vite.
  `npm audit fix` sin `--force` no encontro una actualizacion compatible con Angular
  22.0.7; no se incluyen en runtime. Revalidar al proximo review mensual o con un patch
  Angular 22.0.x compatible, lo que ocurra primero.
- `docker compose config` no es ejecutable aun: no hay archivo Compose antes de W12.
  W12 debe crear ese artefacto y ejecutar su validacion; esto no bloquea W10.

## P3-W8 completion gate

- Angular 22.0.7 registra `provideHttpClient()` y `AuthService` usa solamente signals
  para `currentUser`, `accessToken`, `isAuthenticated`, `loading` y ProblemDetails.
  El access JWT no se persiste ni toca localStorage/sessionStorage.
- Register/resend manejan la respuesta 202 anti-enumeracion; login guarda respuesta 200
  en memoria, representa 401 con texto generico y ofrece reenvio solo para el `code`
  `email_unconfirmed` de un 403. Otros detalles backend no se muestran al visitante.
- `/confirm-email` valida `userId` GUID y `code` Base64URL antes de solicitar red. El
  caso invalido hace cero requests; el valido limpia la URL antes de ejecutar solo POST,
  incluyendo fallo de red/400/5xx, y va a `/login` sin auto-login al confirmar.
- Las tres rutas publicas son lazy; formularios Reactive Forms incluyen labels,
  autocomplete, validacion cliente/servidor y estados aria-live/alert. `Sign in` se
  descubre desde el header tanto en desktop como mobile; no se anade un item bottom-nav.
- `npm ci`, `npm run build`, 94/94 pruebas ChromeHeadless y `git diff --check` PASS.
  Los 5 advisories audit son solo dev transitivos
  previamente aceptados; W8 no cambia dependencias ni fuerza un fix.

## P3-W9 completion gate

- `provideAppInitializer` ejecuta un solo refresh same-origin por vida de la SPA y el
  servicio expone `checking/auth/anon`. Falta de cookie/fallo bootstrap queda anonimo y
  no inicia redireccion de login.
- `/favorites` conserva lazy loading y usa guard que espera bootstrap; el anonimo recibe
  solamente `/login?returnUrl=/favorites`. Login acepta solo retornos internos
  normalizados y descarta URLs protocol-relative, hosts, esquemas y `/auth/*`.
- El interceptor funcional agrega Bearer solo a `/api/*` relativo con token en memoria.
  `/auth/*`, URL externa, retry marcado internamente y API sin token no disparan refresh.
  401 concurrentes comparten una rotacion y cada request se reintenta una sola vez.
- Fallo del refresh automatico limpia usuario/JWT y redirige una vez; logout visible en
  header borra memoria sincronicamente antes de su POST best-effort. `sessionChange`
  entrega `previousUserId/currentUser` para el aislamiento de favoritos W11.
- `proxy.conf.json` y `angular.json` configuran development `/api` y `/auth` hacia
  `http://localhost:5179`, sin `withCredentials`, CORS abierto ni URL Render en browser.
- `npm ci`, `npm run build`, 110/110 ChromeHeadless y `git diff --check` PASS. Audit
  runtime queda en 0; los 5 advisories transitivos development permanecen documentados.

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
| Vulnerabilidades Angular | Runtime: gate cerrado con Angular 22.0.7 y `npm audit --omit=dev` en 0. Desarrollo: 5 advisories transitivos del builder, sin fix compatible no forzado; revalidar mensualmente o ante patch 22.0.x |

## Recommended next step

Ejecutar P3-W10 desde `codex/p3-integration`. W10 reemplaza mock/`availableDates` por
APOD/date/search HTTP, conserva el bootstrap, interceptor y control de cuenta W9, y no
mueve el JWT a Web Storage. Neon/Render/Resend, seed productivo y NASA real siguen
postergados hasta W13.
