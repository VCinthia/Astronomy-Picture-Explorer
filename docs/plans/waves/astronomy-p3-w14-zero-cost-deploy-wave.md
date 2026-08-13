# Wave P3-W14 - Seed, deploy $0 y smoke productivo

Date: 2026-07-22
Status: DONE - all zero-cost deployment, smoke and P4-W1 cutover acceptance criteria passed
Wave ID: `P3-W14`
Depends On: P3-W5 + P3-W12 + P3-W13 + P3-W15 DONE and merged before external execution
Suggested Branch: `wave/p3-w14-zero-cost-deploy`

## Goal

Configurar exclusivamente recursos gratuitos, cargar el catálogo desde una máquina local,
desplegar y ejecutar el smoke que cierra P3.

## Historical file scope

- configuración del proxy público y su preparación de build;
- contenedor y entrypoint de API;
- `.env.example`, runbooks de deploy/local y documentos de estado P3.

La configuración real de proveedores permaneció fuera de Git. Esta wave no autoriza
recrear proveedores ni reutilizar el procedimiento de 2026-08-12.

## Completion checklist

- [x] W14.1 Revalidar cuotas/condiciones free y registrar resultados sanitizados.
- [x] W14.2 Crear solo recursos gratuitos, sin keepalive, cron, worker, disco ni upgrade.
- [x] W14.3 Migrar, hacer dry-run y completar el seed local resumible del rango aprobado.
- [x] W14.4 Verificar sender transaccional antes del correo real.
- [x] W14.5 Desplegar API sin backfill ni archivos persistentes en startup.
- [x] W14.6 Configurar la frontera same-origin para `/api/*` y `/auth/*` antes del fallback
  SPA, con controles de tráfico agrupados por las dos superficies de rutas públicas.
- [x] W14.7 Verificar cookie host-only, origen, HTTPS y que el navegador no depende de una
  ruta alternativa de API.
- [x] W14.8 Ejecutar smoke: cold start/retry, today, fecha, search, cuenta, confirmación,
  login, refresh/reload, recuperación, favoritos y logout.
- [x] W14.9 Registrar fecha/resultados y limpiar datos de prueba sin conservar identidades,
  enlaces, contraseñas ni códigos.
- [x] W14.10 Sustituir la hipótesis de identidad por headers reenviados por una frontera
  pública validada por la aplicación.
- [x] W14.11 Verificar la protección en reposo del key ring gestionado sin añadir una capa
  paga.
- [x] W14.12 Mantener fixtures y sinks Development-only separados de los proveedores reales.

## Acceptance criteria

- No existe ruta configurada capaz de generar cobro automático.
- El catálogo está ready antes de anunciar producción.
- La primera visita sobre cold start termina en contenido o CTA Retry comprensible.
- Auth y favorites funcionan mediante el origen público con cookie segura.
- La frontera pública aplica controles de tráfico sin aceptar identidad desde headers no
  verificados; links de email sobreviven restart/cold start y el key ring cumple el gate
  en reposo.
- Todos los exit criteria P3 tienen evidencia y docs sincronizados.
- W15 está integrado: reset real no auto-login, invalida refresh sessions y el correo llega
  al mailbox temporal sin exponer el enlace en evidencia.

## Verification performed

```powershell
docker compose config
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```

Los comandos externos de seed y smoke se ejecutaron desde una máquina local con
credenciales efímeras. No se reproducen en este archivo.

## Historical preparation record (2026-07-22)

- La configuración local separó secretos Docker de la configuración gestionada por el
  proveedor y respetó el puerto asignado por la plataforma.
- La verificación local de preparación pasó: backend sin warnings, 172/172 tests,
  frontend build y 118/118 ChromeHeadless, Compose healthy y catálogo fixture `ready`.
- En ese momento, la promoción, creación de recursos, seed real, correo real y evidencia
  externa seguían pendientes de las decisiones del owner.

## Historical external execution record (2026-08-12)

- Se creó PostgreSQL gestionado gratuito, se aplicaron migraciones desde la máquina local y
  la dueña confirmó las tablas en la consola del proveedor.
- El dry-run del rango aprobado pasó; el seed real de 30 fechas se reanudó y completó
  correctamente. El estado público posterior quedó `ready`.
- Un timeout inicial del CLI no avanzó el checkpoint; la recuperación local corrigió solo
  su timeout de operador y `--resume` completó el batch. No cambió el contrato interactivo.
- La configuración del frontend se actualizó a un runtime compatible con Angular 22 antes
  del smoke. Los grupos globales finales de tráfico fueron `/auth/*` y `/api/*`; no se
  desplegaron reglas individuales para recovery.
- El smoke con correo real pasó: registro, confirmación, login, reload/refresh,
  recuperación, contraseña anterior rechazada, contraseña nueva aceptada, favoritos y
  logout. No se registran direcciones, contraseñas, códigos ni enlaces.
- Un enlace de confirmación emitido antes de un reinicio del servicio siguió siendo válido
  después de recuperar health. Esto prueba el key ring de Data Protection persistido en la
  base de datos, no en el filesystem efímero de la instancia.
- El proveedor documenta protección en reposo administrada; este portfolio no requiere una
  capa adicional paga. La cuenta secundaria de prueba y sus datos asociados fueron
  eliminados fuera del repositorio; solo se retuvo una cuenta de portfolio.

## Parent sync

- [x] P4-W1 verificó la promoción previa de la integración P3 a `main`, confirmó ambas
  ramas de despliegue y revalidó health + catálogo público. P3 queda `DONE`; un tag/release
  permanece opcional y no es condición de cierre.
- [x] P4-W3 saneó este registro conforme ADR-0004. Los hechos históricos se conservan, pero
  no se publican URLs, variables, presupuestos, mecanismos de firma ni instrucciones de
  acceso directo.
