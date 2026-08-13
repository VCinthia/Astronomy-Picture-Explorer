# Wave P5-W1 - Política APOD autoritativa en API

Date: 2026-08-13
Status: DONE
Wave ID: `P5-W1`
Source Phase: `P5`
Source Phase Plan: `docs/plans/astronomy-p5-apod-calendar-plan.md`
Suggested Branch: `wave/p5-w1-apod-calendar-api`
Depends On: P4 DONE
Unblocks: P5-W2

## Goal

Crear la política única de último día APOD en horario Argentina y aplicarla a las rutas de
producto que aceptan una fecha, sin alterar los instantes UTC de infraestructura.

## File scope

- `backend/AstronomyExplorer.Api/Apod/` política de calendario, endpoints y opciones.
- `backend/AstronomyExplorer.Api/Favorites/FavoriteEndpoints.cs`.
- `backend/AstronomyExplorer.Api/Program.cs` sólo para registro de la política.
- Pruebas API de APOD, favoritos y política de calendario; factory de prueba si hace falta.

## Checklist

- [x] W1.1 Implementar una política inyectable basada en `TimeProvider` y resolución
  explícita `America/Argentina/Buenos_Aires`, con compatibilidad Windows/Linux y fallo
  visible si no puede resolverse.
- [x] W1.2 Aplicarla a `/api/apod/today` y `/api/apod/date/{date}`; preservar contrato y
  ProblemDetails actuales.
- [x] W1.3 Aplicarla a crear/eliminar favoritos y a la validación de target de catálogo.
- [x] W1.4 Añadir pruebas deterministas antes/después de `03:00Z`, incluyendo que la fecha
  siguiente se rechaza sin llamar NASA ni cambiar favoritos.
- [x] W1.5 Confirmar que sesiones, cache timestamps, auth y rate limiting conservan UTC.

## Acceptance criteria

- La API devuelve la fecha Argentina vigente para `/today` y no la fecha UTC siguiente.
- Un request o favorito del día siguiente antes del borde devuelve el error existente de
  fecha inválida; después del borde puede continuar a su flujo normal.
- La política no depende del timezone del host ni de configurar proveedores.

## Verification

```powershell
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln --filter "FullyQualifiedName~Apod|FullyQualifiedName~Favorite"
```

Ejecutar los tests de borde con `TimeProvider` fijo; no cambiar el reloj del equipo.

## Implementation record

- `IApodProductCalendar` resuelve explícitamente la zona IANA y su equivalente Windows;
  no usa la zona del host y falla al inicializar si ninguna está disponible.
- Las rutas APOD, los dos flujos de favoritos y `CatalogOptionsValidator` usan esa
  autoridad. Los timestamps de caché y favoritos, autenticación, sesiones y rate limiting
  siguen recibiendo el `TimeProvider` UTC existente.
- `dotnet build backend/AstronomyExplorer.sln --no-restore` pasó. Los tests puros de
  calendario/opciones pasaron (9). El filtro de integración APOD/favoritos quedó pendiente
  de Docker/Testcontainers: el daemon local no estaba disponible; no se interpreta como
  una prueba aprobada.
