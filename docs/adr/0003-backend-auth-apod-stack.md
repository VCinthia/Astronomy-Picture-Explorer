# ADR-0003 - Backend, autenticacion, catalogo APOD y despliegue P3

Date: 2026-07-08
Last revised: 2026-07-17
Status: Accepted; P3-W1 implemented

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
