# ADR-0003 - Backend, autenticacion, catalogo APOD y despliegue P3

Date: 2026-07-08
Last revised: 2026-07-17
Status: Accepted; P3-W1-W5 implemented

## Context

P3 reemplaza el mock frontend por un backend real. La aplicacion es un proyecto de
portfolio con trafico bajo, debe poder abrirse sin preparacion especial y no puede
generar cargos monetarios. El diseño debe resolver autenticacion, persistencia de
favoritos, exploracion por fecha y busqueda sobre el archivo APOD sin asumir funciones
que NASA no ofrece.

La revision del 2026-07-16 confirma:

- ASP.NET Core Identity debe ser propietario de passwords, stamps y tokens de email.
- La API APOD operativa devuelve `date`, `title`, `explanation`, `media_type`, `url`,
  `hdurl?`, `thumbnail_url?`, `copyright?` y `service_version`, pero no ofrece metadata
  de keywords utilizable; P3 deriva search exclusivamente de titulo y explicacion.
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
- `GET /api/apod/search?q=` busca solo en PostgreSQL.
- PostgreSQL Full Text Search usa un `tsvector` ingles ponderado: titulo peso A y
  explicacion peso B, con indice GIN.
- `pg_trgm` es opcional. Solo se habilita como fallback para coincidencia parcial o
  tolerancia a errores si benchmarks y tests demuestran valor; nunca reemplaza FTS.
- Search valida `q`, limita `pageSize` a 30 y ordena por ranking estable + fecha.

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
- `user_id` siempre se deriva del claim del access JWT.
- `GET /api/favorites` hace join con `apod_entries` y devuelve `ApodEntryDto[]` sin N+1.
- Agregar una fecha no cacheada usa el servicio APOD controlado antes de crear la FK.

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
