# Wave P3-W14 - Seed, deploy $0 y smoke productivo

Date: 2026-07-22
Status: IN PROGRESS - external functional smoke PASS; test-data cleanup and promotion to main pending
Wave ID: `P3-W14`
Depends On: P3-W5 + P3-W12 + P3-W13 + P3-W15 DONE and merged before external execution
Suggested Branch: `wave/p3-w14-zero-cost-deploy`

## Goal

Configurar exclusivamente recursos gratuitos, cargar catalogo en Neon desde local,
desplegar y ejecutar el smoke que cierra P3.

## File scope

- `netlify.toml`
- `scripts/prepare-netlify-redirects.mjs`
- `backend/AstronomyExplorer.Api/Security/NetlifyProxySignature.cs`
- `backend/AstronomyExplorer.Api/docker-entrypoint.sh`
- `backend/AstronomyExplorer.Api/Dockerfile`
- `docs/deploy/render-setup.md`
- `docs/deploy/p3-deploy-runbook.md`
- `.env.example`
- `docs/deploy/p3-local-runbook.md` (reference only; do not repurpose its local secrets)
- documentos de estado P3

## Checklist

- [x] W14.1 Revalidar el 2026-07-22 y el 2026-08-12 cuotas/terminos oficiales de Netlify, Render, Neon y
  Resend; registrar enlaces, fecha y comportamiento al exceder en el runbook. Revalidar
  nuevamente en el mismo día de la mutación de proveedores.
- [x] W14.2 Crear solo planes Free; sin keepalive/cron/worker pago/overages/upgrades.
  Configurar gasto cero o no registrar metodo de pago cuando aplique.
- [x] W14.3a Aplicar migraciones Neon y ejecutar CLI local `--dry-run` y seed resumible
  para el rango aprobado, registrando conteo sin secretos.
- [x] W14.3b Fijar `Catalog__RequiredFrom/To` exactamente al rango seed aprobado y
  verificar `catalog-status ready` a través del origen público después de Render/Netlify.
- [x] W14.4 Verificar dominio/sender Resend y rate limits antes del email real.
- [x] W14.5 Render recibe env vars en dashboard; no ejecuta backfill ni guarda archivos.
- [x] W14.6 Netlify queda preparado para proxificar `/api/*` y `/auth/*` a Render antes de
  `/* -> index.html`, con placeholder inválido que solo el build productivo sustituye.
  Cada proxy usa JWS firmado por Netlify y rate limits de borde por visitante.
- [x] W14.7 Verificar cookie host-only/Lax, Origin, HTTPS y que browser no llame Render
  directamente.
- [x] W14.8 Smoke: cold start/retry, today, fecha, search, register, email, confirm POST,
  login, refresh/reload, recuperación de contraseña, favorite/list/delete, aislamiento y logout.
- [ ] W14.9 Registrar fecha/URLs/cuenta de prueba/resultados y limpiar datos de prueba.
- [x] W14.10 Sustituir la hipótesis de Forwarded Headers: Render Free no ofrece una cadena
  de ingress verificable para interpretar `X-Forwarded-For` sin spoofing. La API rechaza
  rutas de aplicación directas y valida el JWS `x-nf-sign` (issuer, sitio, deploy
  production, expiración y HMAC); Netlify limita las redirects por IP real. El smoke
  completado demostró bypass directo/spoof rechazado sin convertir headers falsificados
  en identidad de visitante.
- [x] W14.11 Verificar el cifrado/controles en reposo de Neon para el XML del key ring
  Data Protection y documentar cualquier proteccion adicional $0 requerida.
- [x] W14.12 Sustituir de forma explicita los fixtures/sinks Development-only de W12 por
  configuracion real solo en dashboards de proveedor; no copiar `.env`/`.secrets` locales
  ni habilitar HTTP NASA fuera de loopback/mock.

## Acceptance criteria

- No existe ruta configurada capaz de generar cobro automatico.
- Catalogo esta ready antes de anunciar produccion.
- Primera visita sobre cold start termina en contenido o CTA Retry comprensible.
- Auth y favorites funcionan mediante origen Netlify con cookie segura.
- Rate limiting productivo usa la IP real de Netlify sin confiar en headers falsificables;
  la URL directa de Render no sirve rutas de aplicación; links de email
  sobreviven restart/cold start y el key ring cumple el gate en reposo.
- Todos los exit criteria P3 tienen evidencia y docs sincronizados.
- W15 está integrado: reset real no auto-login, invalida refresh sessions y el correo llega
  al mailbox temporal sin exponer el enlace en evidencia.

## Verification

```powershell
docker compose config
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
node scripts/prepare-netlify-redirects.mjs
# comandos seed y smoke exactos se registran en docs/deploy/p3-deploy-runbook.md
```

## Preparation record (2026-07-22)

- La configuración local/Render ya separa secretos Docker locales de variables secretas
  de dashboard y acepta el `PORT` asignado por Render.
- `docs/deploy/render-setup.md` y `docs/deploy/p3-deploy-runbook.md` contienen el único
  procedimiento de proveedores, seed y smoke. No contienen secretos ni mutan cuentas.
- Verificación local de preparación PASS: backend build sin warnings, 172/172 tests,
  frontend build y 118/118 ChromeHeadless, Compose healthy con `/health` 200 y catálogo
  fixture `ready`; el script de redirects rechazó silenciosamente el origen ausente y
  sustituyó/validó un origen HTTPS de prueba sin persistirlo.
- La promoción, creación de recursos, seed real, correo real y toda evidencia externa
  permanecen pendientes de las decisiones de dueña enumeradas en el runbook.

## External execution record (2026-08-12)

- Neon Free fue creado en AWS US East 2 (Ohio), con PostgreSQL 17 y Neon Auth desactivado.
- Las migraciones se aplicaron desde la consola local contra la conexión directa de Neon.
  La dueña confirmó las tablas en la consola de Neon.
- El dry-run del rango aprobado `2026-07-13..2026-08-11` pasó sin abrir Neon ni NASA. El
  seed real de 30 fechas se reanudó y completó correctamente; la dueña confirmó los 30
  registros APOD en Neon.
- El primer intento se pausó sin checkpoint por un timeout de 8 segundos. La medición
  externa mostró una respuesta válida de NASA de 10,8 segundos; el fix
  `c8fdbb4` elevó únicamente el timeout del CLI a 30 segundos y `--resume` completó el
  batch. Sigue pendiente comprobar `catalog-status ready` mediante el deploy público.
- El primer build de Netlify alcanzó correctamente la preparación de redirects, pero falló
  antes de Angular porque la configuración histórica fijaba Node 20. El fix operativo
  posterior eleva el pin de Netlify a Node 22.22.3, mínimo compatible con Angular 22, y
  adapta seis aliases/templates y cuatro fixtures de test a su sintaxis pública. No
  modifica rutas, secretos, UX, ni el contrato del proxy firmado.
- La configuración inicial declaraba siete límites por redirect, pero Netlify Free admite
  solo dos reglas code-based por proyecto. El fix posterior conserva proxy firmado y
  límites por visitante con exactamente dos presupuestos: `/auth/*` 10/180 s y `/api/*`
  120/60 s. Los límites normalizados por email del backend siguen protegiendo los envíos
  de Resend. La corrección se desplegó antes del smoke de cuenta y no introduce rutas,
  secretos ni reglas adicionales.
- Render quedó en Free con `/health` como única ruta pública de sonda; Netlify despliega
  el candidato `codex/p3-integration` y lo proxifica firmado hacia Render. La URL directa
  de catálogo y el mismo intento con `X-Forwarded-For` fabricado devolvieron `403`, mientras
  que la ruta same-origin de Netlify devolvió `ready: true`.
- El estado público del catálogo confirmó `ready: true` para el target
  `2026-07-13..2026-08-11`. El seed inicial fue de 30 entradas; una consulta posterior
  añadió la entrada de fecha actual al cache, por lo que el contador observado fue 31 sin
  cambiar el rango objetivo ni ejecutar un backfill.
- El dominio de envío de Resend fue verificado y el smoke de cuenta pasó con correo real:
  registro, confirmación, login, reload/refresh, recuperación, contraseña anterior
  rechazada, contraseña nueva aceptada, favorito add/list/delete y logout. No se registran
  direcciones, contraseñas, códigos ni enlaces.
- Un enlace de confirmación emitido antes de un `Restart service` de Render siguió siendo
  válido después de que `/health` respondió healthy y permitió confirmar e iniciar sesión.
  Esto prueba el key ring de Data Protection persistido en Neon, no en el filesystem de
  la instancia.
- Neon documenta cifrado AES-256 en reposo y gestión/rotación de claves del proveedor;
  para este portfolio no se requiere una capa adicional paga sobre el XML de Data
  Protection. Fuente: <https://neon.com/docs/security/security-overview>.

## Parent sync

- [ ] Resolver explícitamente la limpieza o retención autorizada de las cuentas de prueba,
  actualizar `R3.14`, marcar P3 DONE y registrar tag/release solo después de promover
  `codex/p3-integration` a `main` según el execution contract.
