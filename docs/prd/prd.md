# PRD - Astronomy Picture Explorer

Date: 2026-07-08
Last revised: 2026-07-22
Status: P1 DONE; P2 DONE; P3 IN PROGRESS - W1-W12 DONE

## Vision

Aplicacion web de portfolio para explorar Astronomy Picture of the Day de NASA, navegar
por fecha, buscar por titulo/descripcion, generar paletas de color y guardar favoritos
por usuario. Debe demostrar Angular moderno, calidad visual, accesibilidad y una
evolucion full-stack segura sin costo monetario de operacion.

## Product goals

- Angular 22.0.7 standalone + Signals, sin NgModules/BehaviorSubject.
- Tailwind CSS con tokens y fidelidad visual.
- Canvas palette client-side sin dependencia de terceros para el algoritmo.
- WCAG 2.1 AA minimo.
- P1/P2 entregan valor sin backend; P3 incorpora .NET/PostgreSQL/Auth.
- Primera visita entendible incluso durante cold start de servicios gratuitos.
- Arquitectura operable con planes $0 y sin riesgo de cargos automaticos.

## Users and stakeholders

- Usuaria/desarrolladora: CinthiaRV.
- Evaluadores tecnicos y visitantes de portfolio, normalmente de una sola visita.
- Implementadores/revisores que ejecutan waves desde este repositorio.

## Delivered scope P1

- Home y Explorer sobre mock APOD.
- Image/video card, date picker acotado al mock y header stepper.
- Canvas color palette + fallback, accesibilidad y deploy Netlify.
- Angular standalone/signals + Tailwind tokenizado.

## Delivered scope P2

- Favorites en localStorage y ruta `/favorites`.
- Search client-side sobre title/explanation con debounce.
- Shared picture grid, desktop nav y mobile bottom nav.
- Explorer toolbar responsive search + date.
- Build/test/smoke productivo cerrado el 2026-07-16.

## Scope P3

### Public APOD experience

- `/home` usa APOD real del dia.
- `/explorer` acepta una fecha real entre `1995-06-16` y hoy.
- Home ofrece el selector anterior/siguiente sobre la imagen APOD; el header global no
  controla fechas fuera de esa ruta.
- `availableDates`, chips y mock desaparecen del runtime.
- Search consulta catalogo PostgreSQL por title + explanation con FTS case-insensitive.
- Explorer presenta primero la fecha y luego la búsqueda, alineados en desktop y apilados
  en ese orden en mobile.
- La navegación primaria comunica su ruta activa con color, `aria-current` y una línea
  inferior fina en desktop y mobile.
- Estados loading, empty, upstream error, catalog-not-ready y cold-start son accesibles y
  recuperables mediante Retry.

### App-owned APOD contract

```text
date, title, explanation, media_type, url,
hdurl|null, thumbnail_url|null, copyright|null
```

- JSON conserva snake_case.
- NASA `service_version` no se expone porque la UI no lo necesita.
- No se usa metadata externa de keywords; search deriva de title/explanation.
- Imagen y video, incluidos opcionales ausentes/vacios, tienen contract tests.

### Search/catalog

- PostgreSQL FTS ingles pondera title sobre explanation e indexa con GIN.
- W6 no habilita `pg_trgm`: stemming FTS aporta valor suficiente y los casos de
  prefijo/typo no justifican extension, indice y ranking secundarios. Reabrirlo exige
  nueva evidencia reproducible.
- Search acepta `q` recortado de 1..200, page 1..1000 y maximo 30 resultados por
  request, con orden estable por relevancia + fecha descendente.
- Catalogo historico se carga mediante CLI local resumible por rangos NASA.
- Dry-run estima requests sin DB/key/red; live exige key propia y nunca corre en Render.
- Batches atomicos y lock global con heartbeat evitan saltos, duplicados y rangos
  solapados; 429 conserva una ventana de resume segura entre procesos.
- APOD historico puede ser disperso: progreso distingue rango consultado de cantidad de
  entries devueltas y puede reparar drift mediante resume completo.
- Status publico informa cobertura/readiness del target canónico configurado para el
  seed; ready exige estado completo, checkpoint final y conservar las filas
  sincronizadas, por lo que un sync pequeño no simula completitud.
- Status y search comparten la misma politica interna de readiness. Target ausente,
  incompleto o con drift impide search con `503 catalog_not_ready` y no llama NASA.
- No existe backfill automatico en Render ni scheduler/keepalive.

### Authentication

- Registro email/password con ASP.NET Core Identity.
- Email de confirmacion mediante Resend con rate limiting.
- Link Angular contiene `userId + code` Base64URL.
- Confirmacion muta mediante `POST /auth/confirm-email`, no GET.
- Login anti-enumeracion, access JWT corto en memoria y refresh opaco rotado.
- Login exitoso oculta el formulario, confirma el éxito de forma transitoria y navega al
  destino interno solicitado o a Home.
- Replay revoca familia; logout revoca sesion.
- Bootstrap, guard e interceptor single-flight en Angular.

### Browser/backend topology

- Netlify sirve Angular y proxifica same-origin `/api/*` y `/auth/*` a Render.
- Cookie refresh host-only `Secure`, `HttpOnly`, `SameSite=Lax`, `Path=/auth`.
- Refresh/logout validan `Origin` exacto; no se confia en CORS como CSRF defense.
- Desarrollo usa proxy Angular equivalente.

### Favorites

- `/favorites` requiere sesion.
- Favorites se persisten por `user_id + apod_date` en PostgreSQL.
- Usuario se obtiene exclusivamente del `sub` literal del JWT, nunca del request body.
- POST acepta solo `{ "apod_date": "YYYY-MM-DD" }`, valida la fecha APOD antes de
  cache/NASA y es idempotente (`204` para alta o repeticion).
- DELETE filtra `sub + date`, valida el mismo rango y es idempotente (`204` para
  existente o ausente).
- GET devuelve `ApodEntry[]` hidratado sin N+1, ordenado por fecha descendente mediante
  una unica proyeccion/join; no pagina ni limita silenciosamente la coleccion por sesion.
- LocalStorage deja de ser fuente runtime y no se migra silenciosamente a una cuenta.

### Infrastructure/deploy

- .NET 10 LTS, EF Core/Npgsql, PostgreSQL y Testcontainers.
- Docker Compose local para frontend/API/PostgreSQL, con migrator y demo fixture
  Development-only separados del API startup; secretos locales solo como Docker-secret
  files ignorados.
- Netlify Free + Render Free + Neon Free + Resend Free.
- Sin keepalive, cron, workers pagos, overages ni upgrade automatico.
- Si se alcanza una cuota, el sistema falla cerrado/suspendido antes que cobrar.

## P3 non-goals

- OAuth/social login, password recovery, roles/admin UI.
- Busqueda semantica/IA o tags externos.
- Guardar archivos multimedia en DB.
- Always-on gratuito o cero cold-start.
- Actualizacion automatica diaria de todo el catalogo.
- Escala comercial/miles de usuarios.

## Functional requirements P3

1. Home muestra APOD del dia desde backend o un estado recuperable.
2. Explorer consulta exactamente la fecha elegida; no depende de `availableDates`.
3. Image/video se renderizan con fallback cuando hdurl/thumbnail/copyright son null.
4. Search solo consulta PostgreSQL y rechaza catalogo no ready con estado explicito.
5. Registro/reenvio no revelan directamente si un email existe.
6. Confirmacion requiere `userId + code` y un POST valido.
7. Login invalido devuelve mensaje generico; cuenta no confirmada recibe CTA resend.
8. Multiples 401 simultaneos provocan un solo refresh del navegador.
9. Refresh replay revoca la familia y fuerza nuevo login.
10. Favoritos estan aislados por usuario y GET devuelve cards completas.
11. Logout limpia cookie, JWT y datos de usuario/favoritos en memoria.
12. Primera visita durante cold start comunica conexion y permite reintentar.
13. La navegación activa y los controles de fecha/search conservan jerarquía visual,
    foco y orden comprensible en desktop y mobile.
14. El stack local permite demostrar registro, confirmación por `LocalLog`, login y
    favoritos sin proveedor externo ni exponer códigos de confirmación en la SPA.
15. Login exitoso no deja controles que inviten a reautenticarse: muestra un éxito breve
    y conserva un `returnUrl` interno válido o navega a Home.

## Non-functional requirements P3

- Security: Identity, no raw tokens, secrets por env, Origin validation, rate limits.
- Reliability: cache controlada, retries acotados, ingestion resumible, errores tipados.
- Performance: FTS indexado, page max 1000, pageSize max 30, no N+1, lazy routes.
- Accessibility: WCAG AA, teclado, foco, aria-live para estados async.
- Maintainability: DTO app-owned, ADR vigente y wave verification ejecutable.
- Cost: costo obligatorio $0; no fallback automatico a pago.
- Observability: health, catalog status y runbook con smoke fechado.

## Success criteria

- P1/P2 permanecen documentados como entregas historicas cerradas.
- P3 W1-W14 completadas con build/tests en verde; W12-W13 deben permitir UX y E2E local
  sin proveedor externo, y W14 conserva exclusivamente el smoke con proveedores reales.
- Catalogo inicial listo en Neon y search FTS probado.
- Auth/favorites/APOD funcionan E2E desde Netlify mediante proxy same-origin.
- Cold-start UX verificada.
- Produccion y quotas documentadas sin secretos ni riesgo de cargo.

## Clarificaciones posteriores sobre P1/P2

Estas aclaraciones no reescriben lo implementado; documentan decisiones P3 que
reemplazaran contratos temporales:

- El mock P1 fue deliberadamente provider-shaped e incluyo `service_version`. P3 adopta
  un DTO app-owned y elimina ese campo porque nunca fue usado por UI.
- `availableDates`, chips y stepper indexado fueron adecuados para ocho entradas mock;
  P3 los reemplaza por fecha calendario real.
- Search P2 sobre title/explanation queda conceptualmente alineado con P3, pero pasa de
  computed local a PostgreSQL FTS.
- Favorites P2 localStorage queda como comportamiento anonimo historico; P3 lo sustituye
  por persistencia autenticada y no lo importa automaticamente a una cuenta.
- El scaffold historico P1 uso Angular 19.2. El 2026-07-20 una rama de mantenimiento
  dedicada lo actualizo secuencialmente a Angular 22.0.7 para cerrar vulnerabilidades
  runtime antes de W8; no modifica la evidencia historica de P1/P2.
- W8 incorpora las pantallas publicas de cuenta sobre `/auth/*` same-origin. El JWT de
  acceso es estado transitorio en memoria, confirmacion usa POST y limpia su codigo de
  la URL; bootstrap/refresh/guard/interceptor quedan para W9. El header muestra Sign in
  en todos los breakpoints sin reescribir la navegacion P2 historica.
- W9 completa la restauracion de sesion: bootstrap hace un refresh una vez, `/favorites`
  espera guard y el interceptor coordina 401 con una sola rotacion/retry. JWT y usuario
  se limpian sincronicamente en logout; `sessionChange` permite que W11 aisle favoritos
  por usuario. Desarrollo proxifica `/api` y `/auth` al backend local y login solo sigue
  un `returnUrl` interno seguro.
- W10 reemplaza efectivamente el mock en la rama de integracion: `/` redirige a `/home`,
  APOD/date/search usan HTTP same-origin y el calendario es real. `selectedDate` queda
  confirmado por la respuesta APOD y una seleccion valida pendiente se representa por
  separado para no aceptar una respuesta obsoleta. Los favoritos locales restantes son
  una fachada transitoria que W11 elimina sin migrar datos anonimos.
- W11 vuelve favoritos una capacidad autenticada: la coleccion `ApodEntry[]` se obtiene
  una vez por sesion con `GET /api/favorites`; alta `{ "apod_date": date }` y delete por
  fecha son idempotentes, tienen pending/error/retry accesibles y no generan consultas
  por card. Logout o cambio de cuenta limpia el estado antes de exponer otra sesion. El
  corazon anonimo lleva a login con retorno interno y no existe migracion desde
  `ape.favorites.v1`.
