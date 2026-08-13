# Wave P5-W1 - Política APOD autoritativa en API

Date: 2026-08-13
Status: PLANNED
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

- [ ] W1.1 Implementar una política inyectable basada en `TimeProvider` y resolución
  explícita `America/Argentina/Buenos_Aires`, con compatibilidad Windows/Linux y fallo
  visible si no puede resolverse.
- [ ] W1.2 Aplicarla a `/api/apod/today` y `/api/apod/date/{date}`; preservar contrato y
  ProblemDetails actuales.
- [ ] W1.3 Aplicarla a crear/eliminar favoritos y a la validación de target de catálogo.
- [ ] W1.4 Añadir pruebas deterministas antes/después de `03:00Z`, incluyendo que la fecha
  siguiente se rechaza sin llamar NASA ni cambiar favoritos.
- [ ] W1.5 Confirmar que sesiones, cache timestamps, auth y rate limiting conservan UTC.

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
