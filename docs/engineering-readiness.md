# Engineering Readiness - Astronomy Picture Explorer

Date: 2026-07-22
Status: P2 DONE in production; P3 IN PROGRESS - W1-W13 DONE; W14 deployment preparation in progress

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
y logout frontend. P3-W10 reemplazo el runtime mock por APOD/date/search HTTP; P3-W11
reemplazo la fachada local de favoritos por la API autenticada. W13 cerró UX y
aceptación local antes de producción; W14 conserva proveedores/deploy. P3-W12 completo
el stack local reproducible, con secretos locales
file-backed, sin proveedores productivos ni llamadas NASA/Resend.

P3-W13 cerró el 2026-07-22 con fecha antes de búsqueda en Explorer, stepper UTC solo
sobre la imagen Home, subrayado activo desktop/mobile y búsqueda FTS probada sin
distinción de mayúsculas. El login muestra exclusivamente `Signed in successfully.`
durante 650 ms antes de navegar a retorno interno o Home. El smoke LocalLog y Compose
con entrada CRLF normalizada pasaron sin servicios externos.

W14 preparó el 2026-07-22 la configuración de proveedor sin mutar cuentas: el contenedor
acepta secretos del dashboard/puerto Render, Netlify tiene rewrites firmadas con límites
por IP y la API rechaza rutas de aplicación directas o `X-Forwarded-For` falsificado. El
seed Neon, la configuración de dashboards, el correo real y el smoke siguen pendientes.

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
- Login se limita por IP sin particion email para no habilitar DoS dirigido. En
  producción W14 traslada esa partición a Netlify firmado y no interpreta forwarded
  headers en Render.

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
- La ausencia de Compose era una condicion previa a W12; W12 la resolvio y dejo el
  smoke local/documentacion como gate cerrado abajo.

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

## P3-W10 completion gate

- `ApodEntry` coincide con `ApodEntryDto`: JSON snake_case de ocho campos, sin
  `service_version` y con `hdurl`, `thumbnail_url` y `copyright` como `string | null`.
- `/` redirige a `/home`; Home hace `GET /api/apod/today`, Explorer fecha valida usa
  `GET /api/apod/date/{date}` y search paginado llama `/api/apod/search` con `page=1` y
  `pageSize=12`. No queda import o asset runtime del mock ni `availableDates`.
- `switchMap` cancela solicitudes APOD/search obsoletas. `requestedDate` mantiene el
  input valido en curso y `selectedDate` se actualiza exclusivamente con la respuesta;
  una fecha invalida cancela el request previo antes de exponer su error.
- El control de fecha nativo limita UTC `1995-06-16..hoy` y el stepper opera dias UTC.
  Home/Explorer presentan loading, upstream/cold-start, empty y `catalog_not_ready` con
  Retry y aria-live. Auth, logout y control de cuenta W9 permanecen same-origin.
- `npm ci`, `npm run build`, 100/100 ChromeHeadless y `git diff --check` PASS. `npm audit
  --omit=dev` mantiene 0 vulnerabilidades runtime; los cinco advisories de dev siguen
  documentados y no se fuerza una actualizacion fuera de mantenimiento.

## P3-W11 completion gate

- `FavoritesService` carga una unica coleccion `ApodEntry[]` desde `GET /api/favorites`
  por usuario/sesion y la entrega hidratada a `/favorites` y cards, sin N+1 ni limite
  silencioso.
- POST usa solo `{ "apod_date": "YYYY-MM-DD" }` en `/api/favorites`; DELETE usa
  `/api/favorites/{date}`. Pending por fecha deshabilita el doble toggle y mantiene
  `aria-pressed`; listados y mutaciones exponen error/retry accesible.
- El servicio escucha `AuthService.sessionChange` y `currentUser`; logout o A->B limpia
  lista/pending/error, cancela trabajo en vuelo y descarta cualquier respuesta anterior.
  Lecturas/callbacks validan de inmediato `currentUser`, cerrando el intervalo anterior
  a la ejecucion asincrona del effect Angular; la signal de identidad activa invalida
  cualquier lectura publica cacheada durante la transicion.
- Un visitante anonimo recibe una etiqueta CTA de login y retorna solo a un path interno
  validado. No queda `ape.favorites.v1`, fallback ni migracion de favoritos anonimos.
- `npm ci`, `npm run build`, 115/115 ChromeHeadless, `npm audit --omit=dev` con 0
  vulnerabilidades runtime y `git diff --check` PASS.

## P3-W12 completion gate

- `docker compose config` PASS sin password ni Session signing key interpolados: ambos
  se entregan desde archivos ignorados como Docker secrets y se leen dentro del
  entrypoint API, no en capas de imagen ni output de Compose.
- `docker compose up -d --build` PASS. PostgreSQL y `nasa-mock` quedan healthy;
  `migrator` aplica EF una sola vez, `demo-seed` Development-only queda `Exited (0)`, y
  API/frontend non-root quedan healthy. API no ejecuta migration ni catalog/backfill al
  arrancar.
- El fixture se limita a `2020-01-01`, es idempotente y marca ese rango local ready;
  test focalizado demuestra que actualiza URL/metadatos ante cambio de origen local en
  lugar de duplicar filas. El catalogo historico W5/W14 no se ejecuta.
- HTTP local PASS: `/health` 200 por loopback, catalog `completed/ready`, search FTS,
  APOD fixture date, APOD today mock y `/home` same-origin devuelven resultado.
- E2E sin proveedor PASS: register 202, LocalLog confirmation POST 204, login 200,
  favorite POST 204/GET una entrada y logout 204. El sender local y LocalFixtures fallan
  cerrados fuera de Development; BaseUrl NASA HTTP acepta solo `nasa-mock`/loopback en
  Development y tests rechazan HTTP arbitrary/Production.
- `docker compose down` sin `-v` seguido de `up -d` preservo favoritos; conteo final
  `3 migrations | 1 fixture | 1 catalog state | 1 favorite`, sin duplicados.

## P3-W13 completion gate

- No se usó proveedor, cuenta externa, key NASA, sender Resend ni despliegue.
- Explorer/Home/nav, casing FTS y el success+redirect de Login tienen pruebas de
  componente/servicio; `npm run build` y 117/117 ChromeHeadless PASS.
- LocalLog verificó registro 202, `email_unconfirmed` 403, confirmación 204, login y
  refresh 200, favorite 204/GET una entrada y logout 204. Los enlaces/códigos efímeros
  no quedaron registrados como evidencia.
- Compose reconstruyó correctamente en Windows tras normalizar CRLF dentro de la imagen;
  API/frontend healthy y migrator/demo-seed exit 0. W14 queda como la única wave con
  autoridad de proveedor, seed real y deploy.

## P3-W14 preparation gate

- Preparación sin proveedores PASS: Netlify proxy signed/edge-rate-limited, validación
  JWS API y runbooks de Render/Neon/Resend no contienen secrets ni URLs reales.
- Backend build sin warnings y 172/172 tests PASS; las pruebas nuevas cubren firma
  válida, directa/spoof, claims, expiración, HMAC, health y configuración Production
  fail-closed.
- Frontend build y 118/118 ChromeHeadless PASS; Compose reconstruido sin volumen,
  healthy y smoke HTTP local `health`/catalog ready/search PASS.
- Este gate no reemplaza R3.14: no hay proveedores configurados, seed real, correo real
  ni smoke productivo aún.

Antes de cada wave restante:

- Confirmar que ADR-0003, P3 y wave siguen sincronizados.
- Crear branch desde `codex/p3-integration` limpio y sincronizado.
- Mantener secrets fuera del repo.
- Ejecutar los comandos de verification de la wave.

Recursos externos se habilitan just-in-time:

- Resend/dominio: necesario para smoke real W14; W2 usa fake en tests.
- NASA key propia: necesaria para el seed real autorizado en W14; W5 usa mocks/dry-run.
- Neon: necesario para seed/deploy, no para Testcontainers W1.
- Render/URL final: necesario solo en W14.

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
Invoke-WebRequest http://localhost:5179/health
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
| Key ring perdido en filesystem efimero | Data Protection keys en PostgreSQL; revisar cifrado en reposo en W14 |
| IP real oculta por Netlify/Render | Netlify firmado limita por dominio+IP; API rechaza URL directa/spoof y no acepta forwarded headers |
| Render cold start confunde primera visita | Estado connecting + timeout + Retry accesible |
| Search costoso | tsvector + GIN; q max 200, page max 1000, pageSize max 30; sin trigram |
| Cuota/cargo inesperado | Free-only, no keepalive/cron/overages; fail closed y revalidar en W14 |
| DTO diverge de NASA/frontend | DTO app-owned congelado + contract tests image/video/nulls |
| Vulnerabilidades Angular | Runtime: gate cerrado con Angular 22.0.7 y `npm audit --omit=dev` en 0. Desarrollo: 5 advisories transitivos del builder, sin fix compatible no forzado; revalidar mensualmente o ante patch 22.0.x |

## Recommended next step

Para completar P3-W14 aún hace falta la autorización y acceso de la dueña para revalidar
precios/cuotas, configurar Neon/Render/Netlify/Resend, confirmar dominio y NASA key,
ejecutar el seed y obtener evidencia del smoke. El procedimiento exacto está en
[`docs/deploy/p3-deploy-runbook.md`](deploy/p3-deploy-runbook.md); nada de eso se infiere
desde el gate local cerrado.
