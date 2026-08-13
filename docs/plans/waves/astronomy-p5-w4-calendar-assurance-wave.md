# Wave P5-W4 - Aseguramiento y promoción del calendario

Date: 2026-08-13
Status: READY FOR PROMOTION
Wave ID: `P5-W4`
Source Phase: `P5`
Source Phase Plan: `docs/plans/astronomy-p5-apod-calendar-plan.md`
Suggested Branch: `wave/p5-w4-calendar-assurance`
Depends On: P5-W3 DONE
Unblocks: P5 promotion to `main`

## Goal

Cerrar la coherencia de contrato/documentación y promover P5 sólo después de una regresión
completa y un smoke que confirme la fecha Argentina sin revelar configuración productiva.

## File scope

- PRD, readiness, master, ADR-0003, flow P3, README técnico y plan/waves P5 que indiquen
  semántica UTC de fecha APOD.
- Pruebas o correcciones mínimas descubiertas por la regresión.
- No proveedores, `.gitignore`, secretos, regiones, seed ni cambios de costo.

## Checklist

- [x] W4.1 Alinear documentos con ADR-0005 y conservar UTC sólo donde describe instantes
  de seguridad/persistencia.
- [x] W4.2 Ejecutar build, tests frontend/backend, Compose y los tests específicos de
  frontera; registrar bloqueos externos sin llamarlos PASS.
- [x] W4.3 Verificar que `main` no avanzó y que P5 conserva un fast-forward limpio; el
  smoke público same-origin queda como gate posterior a promoción.
- [x] W4.4 Comprobar con tiempo controlado que Home no
  selecciona mañana Argentina; documentar sólo resultado sanitizado.

## Acceptance criteria

- Los contratos, UI y catálogo comparten el calendario Argentina definido por ADR-0005.
- La aplicación publicada no solicita anticipadamente la fecha siguiente durante la noche
  de Argentina.
- P5 se publica como una unidad funcional completa y no contiene cambios operativos o de
  proveedor no autorizados.

## Verification

```powershell
git diff --check
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
docker compose config
```

## Assurance record

- Se saneó la documentación activa para distinguir fecha APOD de producto Argentina de
  instantes UTC de seguridad/infraestructura. Los registros P3 que describen UTC quedaron
  marcados como históricos y ADR-0005 es la autoridad vigente.
- `git diff --check`, build/frontend y 131 tests ChromeHeadless pasaron. Build .NET y 27
  tests focalizados de calendario/catálogo pasaron; se conservan las advertencias
  preexistentes de CSS y NU1903 como mantenimiento separado.
- `docker compose config` pasó, pero Docker Desktop no estaba disponible. No existían
  listeners locales para smoke y la regresión Testcontainers/APOD/favoritos queda
  **BLOCKED**, no PASS. Esta wave no inició ni detuvo contenedores, ni llamó NASA, ni
  sincronizó catálogo.
- `main` y `origin/main` permanecían en el mismo commit base de P5; la integración sólo
  está por delante y puede promoverse en fast-forward. Tras repetir el bloqueo Docker,
  queda pendiente el smoke público same-origin posterior.
- Limitación aceptada: una pestaña abierta no actualiza controles automáticamente al
  cruzar 00:00 Argentina. Se reevalúan con recarga o interacción/re-render relevante; no
  se añadió timer y la API conserva la autoridad.
