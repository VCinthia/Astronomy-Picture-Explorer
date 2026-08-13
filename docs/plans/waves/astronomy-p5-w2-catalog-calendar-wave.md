# Wave P5-W2 - Límite de calendario para catálogo local

Date: 2026-08-13
Status: PLANNED
Wave ID: `P5-W2`
Source Phase: `P5`
Source Phase Plan: `docs/plans/astronomy-p5-apod-calendar-plan.md`
Suggested Branch: `wave/p5-w2-catalog-calendar`
Depends On: P5-W1 DONE
Unblocks: P5-W3

## Goal

Hacer que el operador local de catálogo y su configuración no puedan solicitar una fecha
que el producto todavía considera futura en Argentina.

## File scope

- `backend/AstronomyExplorer.Catalog/` parser/programa y sus pruebas.
- Opciones o pruebas API de catálogo afectadas por la política W1.
- Sin cambiar batching, lock, resume, red NASA, seed existente ni proveedores.

## Checklist

- [ ] W2.1 Reutilizar la política W1 para el límite superior del parser/CLI, sin duplicar
  offset o cálculo UTC.
- [ ] W2.2 Renombrar variables/mensajes que digan `todayUtc` cuando su semántica pasa a ser
  último día APOD soportado.
- [ ] W2.3 Probar los dos lados del borde Argentina para CLI y target de catálogo.
- [ ] W2.4 Confirmar dry-run sigue sin abrir DB/red y que un live sync no puede ejecutarse
  en el host desplegado.

## Acceptance criteria

- CLI y API aceptan/rechazan el mismo rango APOD en un instante controlado.
- No hay re-seed, backfill ni mutación de Neon durante esta wave.
- Los mecanismos de costo, lock, checkpoint y retry siguen sin cambios.

## Verification

```powershell
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln --filter "FullyQualifiedName~Catalog"
dotnet run --project backend/AstronomyExplorer.Catalog -- catalog sync --from 2026-08-01 --to 2026-08-12 --batch-size 30 --dry-run
```
