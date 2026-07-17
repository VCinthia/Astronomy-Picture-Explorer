# Framework Version Policy

Date: 2026-07-08
Status: Active

## Current pinned stack

- Frontend: Angular 19.2, Tailwind CSS 4.3, Node/npm segun `package-lock.json` y entorno
  de desarrollo del proyecto.
- Backend P3: .NET 10 LTS.
- Backend SDK pin: `global.json` con `10.0.x`.
- Backend Docker tags: `mcr.microsoft.com/dotnet/sdk:10.0` y
  `mcr.microsoft.com/dotnet/aspnet:10.0`.

## Maintenance rule

Cada vez que aparezca una version mayor o LTS relevante:

1. Revisar soporte oficial y fecha de EOL.
2. Abrir una tarea de mantenimiento si la version actual entra en ventana de fin de
   soporte o si hay vulnerabilidad/beneficio claro.
3. Actualizar en una rama dedicada:
   - `package.json` / `package-lock.json`
   - `global.json`
   - Dockerfiles
   - CI/deploy docs si existen
   - PRD/master/readiness si cambia el contrato de soporte
4. Ejecutar validacion completa:
   - `npm run build`
   - `npm test -- --watch=false --browsers=ChromeHeadless`
   - `dotnet build backend/AstronomyExplorer.sln`
   - `dotnet test backend/AstronomyExplorer.sln`
   - `docker compose config`
5. Registrar el resultado en `docs/engineering-readiness.md`.

## Review cadence

- Revision ligera mensual mientras P3 este activo.
- Revision obligatoria al abrir una nueva fase.
- Revision obligatoria seis meses antes del EOL de cualquier LTS usada en backend.

## Notes

.NET 10 es LTS y Microsoft lo lista con soporte hasta noviembre de 2028. El proyecto no
debe quedar atado a una version fuera de soporte: si surge .NET 12 LTS y el costo de
migracion es razonable, se agenda como mantenimiento antes de que .NET 10 entre en la
ventana final.
