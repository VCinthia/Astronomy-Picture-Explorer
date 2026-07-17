# Engineering Readiness - Astronomy Picture Explorer

Date: 2026-07-16
Status: P2 DONE in production; P3 READY FOR IMPLEMENTATION

## Verdict

P1 y P2 estan cerradas. `main` y `origin/main` apuntan al commit P2-W4 `b72c7e2`, la
aplicacion productiva responde en Netlify y el smoke del 2026-07-16 valido navegacion,
search, persistencia de favoritos entre rutas y breakpoints desktop/mobile.

P3 fue corregida antes de iniciar codigo. La baseline aceptada usa .NET 10, Identity,
PostgreSQL FTS sobre titulo/explicacion, ingestion local resumible, proxy same-origin, refresh
sessions seguras, favoritos hidratados, Testcontainers y 13 waves acotadas.

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

## Gate to start P3-W1

P3-W1 puede comenzar porque no necesita cuentas externas. Antes de cada wave:

- Confirmar que ADR-0003, P3 y wave siguen sincronizados.
- Crear branch desde `main` limpio.
- Mantener secrets fuera del repo.
- Ejecutar los comandos de verification de la wave.

Recursos externos se habilitan just-in-time:

- Resend/dominio: necesario para smoke real W13; W2 usa fake en tests.
- NASA key propia: necesaria para seed W5, no para W1.
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
| Catalogo incompleto | CLI resumible + checkpoint + public catalog-status; search 503 until ready |
| Backfill suspende Render/consume recursos | Ejecutar desde desarrollo contra Neon; nunca en API startup |
| Cookie third-party bloqueada | Browser usa Netlify same-origin proxy; cookie host-only SameSite=Lax |
| CSRF sobre refresh/logout | SameSite=Lax + Origin exacto; CORS no se usa como defensa |
| 401 simultaneos consumen refresh dos veces | Angular single-flight + backend rotation atomica |
| Token de confirmacion ambiguo/filtrado | userId+code Base64URL; Angular POST; no mutacion GET |
| Render cold start confunde primera visita | Estado connecting + timeout + Retry accesible |
| Search costoso | tsvector ponderado + GIN + pageSize max 30; trigram solo con evidencia |
| Cuota/cargo inesperado | Free-only, no keepalive/cron/overages; fail closed y revalidar en W13 |
| DTO diverge de NASA/frontend | DTO app-owned congelado + contract tests image/video/nulls |

## Recommended next step

Ejecutar P3-W1 desde `main`. No crear Neon/Render/Resend prematuramente: la planificacion
deliberadamente posterga mutaciones externas hasta la wave que las necesita.
