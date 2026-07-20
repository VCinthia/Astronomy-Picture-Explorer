# ADR-0003 - Backend, autenticacion, catalogo APOD y despliegue P3

Date: 2026-07-08
Last revised: 2026-07-20
Status: Accepted; P3-W1-W11 implemented

## Context

P3 reemplaza el mock frontend por un backend real. La aplicacion es un proyecto de
portfolio con trafico bajo, debe poder abrirse sin preparacion especial y no puede
generar cargos monetarios. El diseño debe resolver autenticacion, persistencia de
favoritos, exploracion por fecha y busqueda sobre el archivo APOD sin asumir funciones
que NASA no ofrece.

La revision del 2026-07-16 confirma:

- ASP.NET Core Identity debe ser propietario de passwords, stamps y tokens de email.
- La API APOD operativa devuelve exactamente `date`, `title`, `explanation`, `media_type`,
  `url`, `hdurl|null`, `thumbnail_url|null` y `copyright|null`. No expone
  `service_version` ni metadata de keywords; P3 deriva search exclusivamente de titulo y
  explicacion.
- NASA permite consultas por `date` y por rango `start_date`/`end_date`, pero no una
  busqueda remota por keyword.
- Una cookie de refresh entre dominios `netlify.app` y `onrender.com` seria third-party
  y degradaria la accesibilidad del portfolio en navegadores que la bloquean.
- Render Free no ofrece one-off jobs y puede limitar trafico saliente inusual; un
  backfill historico no debe ejecutarse durante el arranque del servicio web.

## Decision

### Runtime y persistencia

- Backend ASP.NET Core en .NET 10 LTS, fijado mediante `global.json` `10.0.x`.
- EF Core + Npgsql sobre PostgreSQL.
- Integracion de persistencia validada con Testcontainers PostgreSQL, no SQLite.
- Produccion en Neon Free; si se alcanza una cuota, el sistema debe degradarse o
  suspenderse, nunca cambiar automaticamente a un recurso pago.

### Contrato HTTP APOD propiedad de la aplicacion

El backend adapta la respuesta NASA a este contrato estable, compartido conceptualmente
con Angular:

```text
ApodEntryDto
  date: string                 requerido, YYYY-MM-DD
  title: string                requerido
  explanation: string          requerido
  media_type: "image"|"video" requerido
  url: string                  requerido
  hdurl: string|null           opcional en origen; null si no existe
  thumbnail_url: string|null   opcional; string vacio de NASA se normaliza a null
  copyright: string|null       opcional
```

`service_version` se valida al deserializar NASA, pero no se expone ni persiste porque
es metadata del proveedor y la UI no la usa. `resource` y cualquier metadata auxiliar
quedan fuera del contrato. El contrato P1/P2 puede cambiar en P3 porque hasta entonces
solo existia el mock/localStorage; W10 actualiza el modelo TypeScript y sus fixtures.

Las respuestas usan los nombres JSON snake_case ya consumidos por Angular. La
nulabilidad anterior es parte del contrato y se cubre con tests de imagen y video.

### Catalogo y busqueda

- `apod_entries.date` es la clave primaria.
- Se guarda metadata, nunca blobs de imagen/video.
- `GET /api/apod/today` y `GET /api/apod/date/{date}` consultan memoria, PostgreSQL y
  NASA en ese orden, y hacen upsert de la respuesta.
- `GET /api/apod/search?q=&page=&pageSize=` busca solo en PostgreSQL y devuelve un
  `ApodEntryDto[]` top-level.
- PostgreSQL Full Text Search usa un `tsvector` ingles ponderado: titulo peso A y
  explicacion peso B, con indice GIN.
- W6 no habilita `pg_trgm`: FTS resuelve stemming y la mejora limitada en prefijos/typos
  no justifica extension, indice ni mezcla de rankings para este portfolio. Reabrir la
  decision requiere nueva evidencia reproducible; trigram nunca reemplazaria FTS.
- Search recorta y limita `q` a 200 caracteres, usa pagina default 1/maximo 1000,
  `pageSize` default 12/maximo 30 y ordena por ranking estable + fecha descendente.

### Ingestion de catalogo sin costo

- Un comando CLI versionado ejecuta backfill por rangos configurables (default 30 dias),
  con retry/backoff, checkpoint persistente, reanudacion e idempotencia.
- La carga historica se ejecuta manualmente desde la maquina de desarrollo contra Neon,
  nunca en el startup de Render ni mediante un job pago.
- Antes de cada lote se comprueban limites NASA; ante 429 se respeta `Retry-After` y se
  detiene sin perder el checkpoint.
- `catalog_sync_state` registra rango objetivo, ultima fecha confirmada, estado y error.
- `GET /api/apod/catalog-status` expone conteo/cobertura sin datos sensibles.
- Search devuelve `503 catalog_not_ready` hasta completar la carga inicial acordada.
- Status y search consumen una politica de readiness interna compartida; search no
  depende de una llamada HTTP a `catalog-status`.
- Despues del seed, `/today` y `/date` incorporan entradas solicitadas. La actualizacion
  incremental completa es un comando manual y no requiere scheduler/keepalive.

Esta operacion privilegia costo cero sobre actualizacion automatica diaria. Para un
portfolio de bajo trafico, una carga inicial completa y actualizaciones manuales
ocasionales son suficientes y observables.

### Usuarios y confirmacion de email

- `ApplicationUser : IdentityUser<Guid>`; Identity gestiona hashing y token providers.
- Registro y reenvio responden de forma generica y tienen rate limiting para proteger la
  cuota gratuita de Resend.
- El backend genera el token Identity y lo codifica Base64URL.
- El email enlaza a `/confirm-email?userId=<guid>&code=<base64url>` en Angular.
- Angular envia `POST /auth/confirm-email` con `{ userId, code }`.
- El backend decodifica el codigo y llama `ConfirmEmailAsync(user, token)`.
- Codigo invalido, vencido o reutilizado produce error controlado sin revelar estado
  interno. No se persisten tokens de confirmacion raw.

### Access token y refresh sessions

- Access JWT corto (objetivo: 10 minutos) en memoria Angular, nunca Web Storage.
- Refresh token opaco de alta entropia; DB guarda un hash criptografico, no el valor raw.
- Cada refresh revoca el token usado, crea otro y enlaza ambos mediante `family_id` y
  `replaced_by_token_id`.
- Reuso de un token revocado invalida la familia completa.
- Logout revoca la sesion actual y elimina la cookie.
- Rotacion y revocacion son transacciones atomicas; una constraint/lock evita que dos
  requests consuman el mismo refresh token correctamente.

### Topologia same-origin, cookie y CSRF

- El navegador llama `/api/*` y `/auth/*` sobre el origen Netlify.
- Rewrites `200` de Netlify proxifican esas rutas al backend Render antes del fallback
  SPA. Desarrollo usa proxy Angular equivalente.
- La cookie refresh es host-only, `Secure`, `HttpOnly`, `SameSite=Lax`, `Path=/auth` y
  tiene expiracion explicita. No declara `Domain` ni atributos cross-site.
- En produccion, refresh y logout rechazan requests cuyo encabezado `Origin` no coincide
  exactamente con el origen publico configurado. CORS no se considera defensa CSRF.
- El backend no habilita CORS amplio. Acceso directo desde un origen de navegador queda
  fuera del contrato productivo.
- El interceptor Angular implementa single-flight: todos los 401 concurrentes esperan
  un unico refresh y luego se reintentan una sola vez.
- Login, refresh y logout quedan excluidos del auto-refresh para evitar loops.
- Si refresh falla: se limpia token/usuario en memoria, se resuelve la cola con error y
  se redirige a login una sola vez.
- El bootstrap de la SPA intenta refresh una vez antes de resolver el estado de sesion.

### Favorites

- `favorites` guarda `user_id + apod_date`, unico por usuario/fecha.
- `user_id` siempre se deriva del claim `sub` literal del access JWT. `MapInboundClaims`
  permanece `false`; no se usa `NameIdentifier` ni se acepta un user ID del cliente.
- `GET /api/favorites` hace una unica proyeccion/join con `apod_entries`, filtrada por
  ese usuario, ordenada por fecha APOD descendente y devuelve `ApodEntryDto[]` sin N+1.
  No pagina ni introduce un limite silencioso: W11 carga una vez la coleccion completa de
  la sesion autenticada y el portfolio no persigue escala comercial.
- `POST /api/favorites` recibe solo `{ "apod_date": "YYYY-MM-DD" }`, valida el rango
  APOD antes de cache/NASA y usa el servicio APOD controlado si falta la entry. Inserta
  con `ON CONFLICT DO NOTHING`; alta y repeticion devuelven `204`.
- `DELETE /api/favorites/{date}` valida el mismo rango, filtra por `user_id + date` y
  devuelve `204` tanto si la relacion existia como si ya estaba ausente.
- Fechas invalidas son `400 invalid_favorite_apod_date`; un JWT autenticado con `sub`
  no GUID es `401 invalid_authenticated_user`. Los errores upstream reutilizan los
  ProblemDetails APOD sanitizados.

### Restriccion de costo cero

- Frontend Netlify Free, API Render Free, DB Neon Free y email Resend Free.
- No se habilitan keepalive, cron pago, workers pagos, overages ni upgrades automaticos.
- El runbook registra cuotas vigentes, alertas y comportamiento de suspension.
- Si un proveedor exige metodo de pago, se configura limite de gasto cero cuando exista
  o se deja sin metodo de pago. Superar una cuota debe fallar cerrado, no facturar.
- El cold start de Render se comunica en UI con estado de conexion y reintento; la
  experiencia de primera visita no debe parecer un error permanente.

## Implementation clarification - P3-W1 (2026-07-17)

- El schema Identity es user-only mediante `IdentityUserContext<ApplicationUser, Guid>`;
  no crea tablas de roles ni expone endpoints automaticos de Identity.
- `NormalizedEmail` tiene indice unico fisico y `RequireUniqueEmail` esta habilitado.
- `refresh_sessions.replaced_by_token_id` usa self-FK `NO ACTION`. Esto evita cascadas
  ciclicas y permite que el cascade desde usuario elimine una familia completa.
- `search_vector` se materializa como generated stored `tsvector`, title peso A y
  explanation peso B, con indice GIN desde la migracion inicial.
- `CatalogSyncStatus` se guarda como string con CHECK; el checkpoint es unico por rango
  y valida rango, ultima fecha y orden de timestamps.
- EF design time puede construir el modelo sin connection string para operaciones
  `--no-connect`; runtime y mutaciones de DB siguen exigiendo configuracion explicita.

## Implementation clarification - P3-W2 (2026-07-17)

- Los endpoints account viven bajo `/auth`: register/resend responden 202 generico y
  confirmacion invalida, vencida, inexistente o reutilizada comparte un 400 controlado.
- El maximo de email queda en 256 para coincidir con las columnas Identity; rate limit
  normaliza solo dentro de ese limite y usa hash, expiracion y capacidad acotada.
- El adaptador Resend es un `IEmailSender` app-owned sobre `POST /emails`, Bearer y
  `User-Agent`; tests lo sustituyen o usan handler en memoria, nunca red real.
- Identity Data Protection usa application name `AstronomyExplorer` y persiste el key
  ring en PostgreSQL mediante una segunda migracion. Esto evita invalidar links cuando
  Render Free pierde su filesystem al dormir/reiniciar.
- La persistencia EF guarda XML de claves sin un certificado externo. W13 debe verificar
  cifrado/controles en reposo de Neon y decidir si exige una capa adicional compatible
  con costo cero antes del deploy.
- W2 usa `RemoteIpAddress` fail-closed y no confia directamente en headers reenviados.
  W13 debe verificar la cadena Netlify -> Render, configurar solo forwarders confiables
  y demostrar que el limiter separa visitantes sin aceptar spoofing.

## Implementation clarification - P3-W3 (2026-07-17)

- JWT usa HMAC SHA-256 con key de al menos 32 bytes, lifetime default 10 minutos,
  `ClockSkew=0` y validacion completa de issuer/audience/firma/expiracion. Secrets faltantes
  impiden arrancar mediante opciones tipadas `ValidateOnStart`.
- Refresh genera 32 bytes criptograficos Base64URL; PostgreSQL conserva solo SHA-256
  hexadecimal. Rotate/logout toman un advisory lock transaccional estable por `family_id`,
  reconsultan despues del lock y serializan cualquier mutacion de la familia completa.
- Dos consumidores del mismo token producen un solo 200. El perdedor observa la
  revocacion como replay y revoca toda la familia; W9 previene este caso normal mediante
  single-flight, pero el backend falla cerrado si ocurre.
- Refresh/logout requieren un unico Origin exacto en Production antes de leer o mutar
  estado. Logout usa la cookie, revoca toda su familia activa y sigue funcionando con
  Bearer ausente o vencido; otras familias del usuario permanecen vigentes.
- Login se limita por IP de transporte, default 10 intentos/15 minutos y queue 0. No se
  agrega particion email ni lockout por cuenta para evitar DoS dirigido; W13 conserva el
  gate de proxies confiables para resolver la IP real.
- Cookie refresh no declara Domain y usa HttpOnly, SameSite=Lax, Path `/auth`, Max-Age,
  Expires y Secure. Solo Development sobre HTTP loopback permite `Secure=false`.

## Implementation clarification - P3-W4 (2026-07-17)

- NASA autentica mediante `X-Api-Key` redacted, no query string. Las requests APOD
  contienen solo `date` y `thumbs=true`; redirects automaticos estan deshabilitados para
  impedir reenviar el header a otro host.
- `service_version` se deserializa y valida como `v1` dentro del adaptador, pero no entra
  al DTO, entidad ni JSON publico. URLs requeridas/opcionales deben ser HTTP(S) absolutas;
  strings opcionales vacios se normalizan a null.
- `today` usa la fecha UTC inyectable de `TimeProvider`. Formato/rango invalido devuelve
  400; rate limit NASA 503, timeout 504 y respuesta/falla upstream 502, siempre mediante
  ProblemDetails sanitizado.
- 429 y redirects no se reintentan. Network, timeout de HttpClient y 5xx permiten como
  maximo dos intentos; una operacion APOD completa tiene timeout adicional y cancela al
  detenerse la aplicacion.
- La cache en memoria tiene lifetime/capacidad validados. El single-flight removible por
  fecha crea scope propio; lee PostgreSQL antes de NASA y persiste mediante `ON CONFLICT`
  atomico, por lo que tambien tolera carreras entre instancias.

## Implementation clarification - P3-W5 (2026-07-17)

- La ingestion vive en `AstronomyExplorer.Catalog`, con entry point namespaced. Dry-run
  valida rango `1995-06-16..UTC today`, batch `1..30` y estima requests antes de leer
  connection/key o construir clientes. Live rechaza `DEMO_KEY` y cualquier Render.
- La exclusion advisory es global al catalogo, mantenida por una conexion PostgreSQL
  dedicada durante toda la corrida. Esto impide simultaneidad tambien entre rangos
  distintos o solapados. Un heartbeat cancela la operacion si se pierde la sesion,
  evitando continuar sin poseer el lock.
- NASA range usa `start_date`, `end_date`, `thumbs=true` y `X-Api-Key`. Antes del primer
  write acepta arrays vacios, dispersos o desordenados, pero rechaza items null, fechas
  duplicadas/fuera del batch, campos invalidos, URLs no HTTP(S) o service distinto de v1.
  Ningun response body/error interno se imprime.
- Fetch queda fuera de transaccion. Cada batch confirma `apod_entries` upserts y avance
  de checkpoint mas `synced_entry_count` en una sola transaccion. El count suma entries
  devueltas, no dias calendario; rollback conserva ambos valores previos.
- 408/5xx/network/timeout son transitorios y dejan Paused; 4xx permanente o payload
  invalido dejan Failed. 429 no espera una ventana larga: persiste `retry_not_before`,
  usando una hora desde el reloj inyectable si falta `Retry-After`, y resume temprano
  falla antes de llamar NASA.
- `Catalog__RequiredFrom/To` define el target canónico de produccion que W13 fija al
  seed. `GET /api/apod/catalog-status` ignora estados ad-hoc para readiness y serializa
  estados lowercase. Ready exige Completed, checkpoint final y row count al menos igual
  al synced count. Completed con drift solo se repara reejecutando el rango con resume.

## Implementation clarification - P3-W6 (2026-07-20)

- `websearch_to_tsquery('english', q)` queda parametrizado mediante Npgsql/EF Core y se
  compara contra el `search_vector` stored de W1. No existe concatenacion SQL.
- `ts_rank` respeta pesos A/B; los empates se ordenan por fecha descendente. Projection,
  `LIMIT` y `OFFSET` ocurren en PostgreSQL y solo exponen el DTO app-owned.
- Input invalido responde 400 estable; catalogo no ready responde 503; cero matches
  responde un array JSON vacio con 200. Ninguna ruta de search inyecta el cliente NASA.
- `page` fuera de 1..1000 y `pageSize` fuera de 1..30 se rechazan antes de evaluar
  readiness o ejecutar SQL, acotando tanto respuesta como profundidad de offset.
- La misma `CatalogReadinessService` evalua target configurado, estado Completed,
  checkpoint final y cantidad sincronizada para status y search, evitando divergencias.
- PostgreSQL real confirma ranking, stemming, web syntax, caracteres especiales,
  paginacion, seguridad ante payload SQL y uso del indice GIN. Prefijo `nebul` y typo
  `neubla` quedan deliberadamente sin fallback; `pg_trgm` no fue habilitado.

## Implementation clarification - P3-W7 (2026-07-20)

- `/api/favorites` exige JWT y lee exclusivamente el `sub` literal; un principal
  autenticado con un valor no GUID falla con el ProblemDetails app-owned 401, sin
  reinterpretar claims inbound ni aceptar un identificador suministrado por el cliente.
- POST valida la fecha antes de activar cache/NASA. Un miss usa `ApodCacheService` y
  conserva sus errores upstream sanitizados; el insert PostgreSQL `ON CONFLICT DO
  NOTHING` hace que altas repetidas o concurrentes converjan en una sola relacion.
- DELETE ejecuta un delete set-based filtrado por `user_id + apod_date`; POST y DELETE
  responden 204 en ambos resultados idempotentes.
- GET proyecta `favorites -> apod_entries` en una unica consulta EF/Npgsql, ordenada por
  fecha descendente. La prueba de integracion cuenta una sola lectura SQL, confirma la
  forma exacta de `ApodEntryDto` y demuestra aislamiento entre dos usuarios.
- W7 cerro con build Release limpio, 9/9 tests focalizados y 159/159 backend PASS sobre
  PostgreSQL 17 Testcontainers; review independiente, format verification y diff check
  tambien aprobados.

## Implementation clarification - P3-W8 (2026-07-20)

- Angular registra `provideHttpClient()` y `AuthService` conserva usuario, access JWT,
  autenticacion, estado pending y ProblemDetails exclusivamente en signals. No se crea
  environment para URL de backend: la SPA llama rutas relativas same-origin `/auth/*`.
- Register/resend consumen el contrato 202 generico. Login almacena solo la respuesta
  exitosa en memoria; 401 siempre se representa generico y el CTA de reenvio se habilita
  exclusivamente ante ProblemDetails 403 con `code=email_unconfirmed`.
- `/confirm-email` valida un GUID y Base64URL localmente, por lo que un link ausente o
  malformado no toca el backend. Un link valido reemplaza la URL sin `code` antes de
  ejecutar solamente POST, inclusive ante error de red/400/5xx, y redirige a `/login`
  sin realizar auto-login ni registrar el codigo.
- El header aporta una entrada `Sign in` en todos los breakpoints. W9 es la unica wave
  autorizada a anexar bootstrap, refresh, logout, guard e interceptor; W10 puede
  reorganizar el shell pero debe preservar una entrada de cuenta accesible.

## Implementation clarification - P3-W9 (2026-07-20)

- El bootstrap Angular se registra como inicializador y comparte un unico
  `POST /auth/refresh` por vida de la SPA. Expone `checking/auth/anon`; una cookie
  ausente o refresh invalido deja estado anonimo sin redirigir una ruta publica.
- `/favorites` espera ese bootstrap. El interceptor solo adjunta `Authorization: Bearer`
  a requests relativos `/api/*` cuando existe access token en memoria. Los endpoints
  `/auth/*`, URL externas y retries quedan fuera; el marker de retry usa `HttpContext`,
  por lo que no viaja al API.
- Los 401 con Bearer comparten refresh. Tras exito cada request original se reintenta una
  vez; tras error se limpia memoria, se rechaza toda la cola y se navega una sola vez a
  login. El bootstrap anonimo no toma ese camino de redirect.
- Cada 401 captura la generacion de su sesion en memoria. Si logout o un login posterior
  la reemplaza antes de resolver refresh, la request vieja solo propaga error: no puede
  reintentar con el token nuevo, limpiar la sesion nueva ni redirigirla.
- Logout del header borra usuario/JWT sincronicamente y luego intenta `POST /auth/logout`
  best-effort, evitando que una red colgada retenga credenciales locales. `sessionChange`
  expone usuario previo/actual para que W11 limpie datos por logout o switch de cuenta.
- Desarrollo usa el proxy Angular `/api|/auth -> http://localhost:5179`; no se habilita
  CORS ni `withCredentials` cross-site. Login acepta solo `returnUrl` interno normalizado.

## Implementation clarification - P3-W10 (2026-07-20)

- Angular adopta el DTO app-owned exacto: los tres campos opcionales siempre aceptan
  `null` y `service_version` deja de existir en tipo, fixtures y runtime. La validacion
  NASA de esa metadata queda estrictamente dentro de W4/W5.
- La ruta canonica publica es `/home`; `/` solo redirige. Home llama `GET /api/apod/today`,
  Explorer llama `GET /api/apod/date/{date}` y search llama el endpoint paginado con
  `page=1&pageSize=12`, todas rutas relativas same-origin cubiertas por W9.
- El estado conserva por separado `requestedDate` (input valido pendiente) y
  `selectedDate` (fecha confirmada por la respuesta APOD). `switchMap` cancela consultas
  de fecha/search obsoletas e incluso una validacion cliente fallida cancela la consulta
  anterior para que no reemplace su error.
- El input nativo de fecha usa UTC `1995-06-16..hoy`; el stepper suma/resta dias UTC.
  Los estados loading, upstream/cold-start, empty y `catalog_not_ready` son recuperables
  mediante Retry. La deuda local de favoritos queda limitada a W11 y no puede promoverse
  como comportamiento de una cuenta.

## Implementation clarification - P3-W11 (2026-07-20)

- `FavoritesService` es el unico propietario frontend de `ApodEntry[]` favoritos. Al
  autenticar carga una unica vez `GET /api/favorites`; cada card recibe esa proyeccion
  hidratada y no emite lecturas APOD adicionales.
- El add usa exactamente `POST /api/favorites` con JSON snake_case
  `{ "apod_date": "YYYY-MM-DD" }`; el remove usa `DELETE /api/favorites/{date}`.
  El control queda pending por fecha hasta el 204, con error/retry recuperable e
  idempotencia en ambos sentidos.
- La frontera de sesion W9 se consume mediante `sessionChange` y `currentUser`: logout o
  cambio A->B borra lista, pending y error, cancela solicitudes observables e ignora
  cualquier respuesta perteneciente a la sesion anterior. Lecturas y callbacks cotejan
  tambien el usuario actual para negar ese resultado antes de que el effect programe la
  limpieza del nuevo limite de sesion. La identidad activa tambien es un signal, de modo
  que la transicion invalida las proyecciones publicas que ya se hubieran leido.
- No existe fallback ni migracion de Web Storage. Un toggle anonimo se expresa como CTA
  accesible a login con `returnUrl` interno normalizado; nunca asocia estado anonimo a
  una cuenta autenticada.

## Consequences

- Search sigue siendo real y eficiente usando campos APOD disponibles.
- El DTO queda deliberadamente desacoplado de metadata irrelevante del proveedor.
- El proxy same-origin mejora compatibilidad de cookies y reduce superficie CORS, a
  cambio del limite de timeout del proxy Netlify; ningun endpoint interactivo puede
  ejecutar el backfill.
- La ingestion es operable y resumible, pero requiere un paso manual previo al deploy.
- La actualizacion diaria completa no es automatica; esta concesion evita cualquier
  riesgo de costo o suspension por trafico saliente.
- Las responsabilidades de P3 requieren mas waves pequenas que el borrador original.

## References

- NASA Open APIs: `https://api.nasa.gov/`
- NASA APOD repository: `https://github.com/nasa/apod-api`
- NASA API rate limits: `https://api.nasa.gov/assets/html/authentication.html`
- PostgreSQL text search: `https://www.postgresql.org/docs/current/textsearch.html`
- PostgreSQL `pg_trgm`: `https://www.postgresql.org/docs/current/pgtrgm.html`
- ASP.NET Core Identity: `https://learn.microsoft.com/aspnet/core/security/authentication/identity`
- ASP.NET Core rate limiting: `https://learn.microsoft.com/aspnet/core/performance/rate-limit`
- Netlify rewrites/proxies: `https://docs.netlify.com/manage/routing/redirects/rewrites-proxies/`
- Render Free: `https://render.com/docs/free`
- Neon plans: `https://neon.com/docs/introduction/plans`
- Resend pricing: `https://resend.com/pricing`
