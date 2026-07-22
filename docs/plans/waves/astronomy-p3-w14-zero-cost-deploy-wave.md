# Wave P3-W14 - Seed, deploy $0 y smoke productivo

Date: 2026-07-16
Status: READY - Not Started
Wave ID: `P3-W14`
Depends On: P3-W5 + P3-W12 + P3-W13 DONE and merged
Suggested Branch: `wave/p3-w14-zero-cost-deploy`

## Goal

Configurar exclusivamente recursos gratuitos, cargar catalogo en Neon desde local,
desplegar y ejecutar el smoke que cierra P3.

## File scope

- `netlify.toml`
- `render.yaml` o `docs/deploy/render-setup.md`
- `docs/deploy/p3-deploy-runbook.md`
- `.env.example`
- `docs/deploy/p3-local-runbook.md` (reference only; do not repurpose its local secrets)
- documentos de estado P3

## Checklist

- [ ] W14.1 Revalidar el mismo dia cuotas/terminos oficiales de Netlify, Render, Neon y
  Resend; registrar enlaces, fecha y comportamiento al exceder.
- [ ] W14.2 Crear solo planes Free; sin keepalive/cron/worker pago/overages/upgrades.
  Configurar gasto cero o no registrar metodo de pago cuando aplique.
- [ ] W14.3 Aplicar migraciones Neon y ejecutar CLI local `--dry-run`, seed resumible y
  verificacion `catalog-status ready`; fijar `Catalog__RequiredFrom/To` exactamente al
  rango seed aprobado y registrar conteo/tamaño sin secrets.
- [ ] W14.4 Verificar dominio/sender Resend y rate limits antes del email real.
- [ ] W14.5 Render recibe env vars en dashboard; no ejecuta backfill ni guarda archivos.
- [ ] W14.6 Netlify proxifica `/api/*` y `/auth/*` a Render antes de `/* -> index.html`.
- [ ] W14.7 Verificar cookie host-only/Lax, Origin, HTTPS y que browser no llame Render
  directamente.
- [ ] W14.8 Smoke: cold start/retry, today, fecha, search, register, email, confirm POST,
  login, refresh/reload, favorite/list/delete, aislamiento y logout.
- [ ] W14.9 Registrar fecha/URLs/cuenta de prueba/resultados y limpiar datos de prueba.
- [ ] W14.10 Verificar la cadena Netlify -> Render y configurar Forwarded Headers solo
  para proxies/redes confiables; demostrar dos IP cliente separadas y rechazo de spoofing.
- [ ] W14.11 Verificar el cifrado/controles en reposo de Neon para el XML del key ring
  Data Protection y documentar cualquier proteccion adicional $0 requerida.
- [ ] W14.12 Sustituir de forma explicita los fixtures/sinks Development-only de W12 por
  configuracion real solo en dashboards de proveedor; no copiar `.env`/`.secrets` locales
  ni habilitar HTTP NASA fuera de loopback/mock.

## Acceptance criteria

- No existe ruta configurada capaz de generar cobro automatico.
- Catalogo esta ready antes de anunciar produccion.
- Primera visita sobre cold start termina en contenido o CTA Retry comprensible.
- Auth y favorites funcionan mediante origen Netlify con cookie segura.
- Rate limiting usa la IP real sin confiar en headers falsificables; links de email
  sobreviven restart/cold start y el key ring cumple el gate en reposo.
- Todos los exit criteria P3 tienen evidencia y docs sincronizados.

## Verification

```powershell
docker compose config
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
# comandos seed y smoke exactos se registran en docs/deploy/p3-deploy-runbook.md
```

## Parent sync

- [ ] Actualizar `R3.14`, marcar P3 DONE solo tras smoke, sincronizar PRD/master/readiness
  y registrar tag/release segun el execution contract.
