# Wave P5-W2 - Límite de calendario para catálogo local

Date: 2026-08-13
Status: DONE
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

- [x] W2.1 Reutilizar la política W1 para el límite superior del parser/CLI, sin duplicar
  offset o cálculo UTC.
- [x] W2.2 Renombrar variables/mensajes que digan `todayUtc` cuando su semántica pasa a ser
  último día APOD soportado.
- [x] W2.3 Probar los dos lados del borde Argentina para CLI y target de catálogo.
- [x] W2.4 Confirmar dry-run sigue sin abrir DB/red y que un live sync no puede ejecutarse
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

## Implementation record

- `CatalogProgram` crea `ApodProductCalendar` con `TimeProvider.System` y pasa su fecha
  máxima al parser; no replica una zona, offset ni cálculo UTC.
- El parser ahora recibe `latestSupportedDate` y devuelve un error de rango con esa
  semántica. Batching, resume, lock, retry, red NASA y persistencia permanecen intactos.
- Las pruebas de dry-run cubren `02:59:59Z` (rechaza el 13) y `03:00:00Z` (lo acepta), sin
  leer entorno, abrir base ni red. Las pruebas W1 de `CatalogOptionsValidator` ya cubren el
  mismo borde para el target de catálogo; `CatalogPreflight` continúa prohibiendo live sync
  en Render.
- La suite de integración de catálogo fue intentada, pero sus casos PostgreSQL no pudieron
  iniciar porque Docker Engine local no estaba disponible. No se registra como PASS: los
  tests puros focalizados y el dry-run sí pasaron, sin abrir proveedor ni base.
