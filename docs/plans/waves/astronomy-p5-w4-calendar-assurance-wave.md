# Wave P5-W4 - Aseguramiento y promoción del calendario

Date: 2026-08-13
Status: PLANNED
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

- [ ] W4.1 Alinear documentos con ADR-0005 y conservar UTC sólo donde describe instantes
  de seguridad/persistencia.
- [ ] W4.2 Ejecutar build, tests frontend/backend, Compose y los tests específicos de
  frontera; registrar bloqueos externos sin llamarlos PASS.
- [ ] W4.3 Verificar que `main` no avanzó, fast-forward de P5 y repetir smoke público
  same-origin después de promoción.
- [ ] W4.4 Comprobar manualmente el horario nocturno o con tiempo controlado que Home no
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
