# Framework Version Policy

Date: 2026-07-08
Last reviewed: 2026-07-20
Status: Active — Angular security maintenance closed and validated

## Current pinned stack

- Frontend: Angular 22.0.7 (`core`, CLI, devkit y compiler-cli alineados), TypeScript
  6.0.3, RxJS 7.8.2, Zone.js 0.15.1 y Tailwind CSS 4.3.
- Entorno validado: Node 24.16.0 y npm 11.13.0.
- Backend P3: .NET 10 LTS.
- Backend SDK pin: `global.json` con `10.0.x`.
- Backend Docker tags: `mcr.microsoft.com/dotnet/sdk:10.0` y
  `mcr.microsoft.com/dotnet/aspnet:10.0`.

## Closed frontend security maintenance (2026-07-20)

La rama dedicada `maintenance/angular-22-security-update` resolvio el gate previo a
W8. Se aplico `ng update` de manera secuencial y con sus migraciones obligatorias:

1. Angular 19.2 -> 20.3.
2. Angular 20.3 -> 21.2.
3. Angular 21.2 -> 22.0.7.

El lockfile resultante fija el conjunto instalado y conserva RxJS/Tailwind porque son
compatibles con la version final. No se ejecuto `npm audit fix --force`, ni se aceptaron
las migraciones opcionales que cambiarian Karma/Jasmine o el build system sin ser parte
de esta correccion de seguridad.

Resultado validado: `npm ci`, `npm run build` y 77/77 pruebas ChromeHeadless pasan;
`npm audit --omit=dev` informa `found 0 vulnerabilities`. La configuracion Docker no
existe todavia porque pertenece a W12, por lo que `docker compose config` queda como
validacion pendiente de ese alcance y no como un resultado aprobado de esta rama.

El audit completo del 2026-07-20 no esta limpio: informa 5 advisories de desarrollo
(1 low y 4 moderate). Todos son transitivos de `@angular-devkit/build-angular`:
`webpack-dev-server`/`sockjs`/`uuid` y un `esbuild` anidado bajo Vite. No se incluyen
en el artefacto runtime, como confirma el audit con `--omit=dev`. Se ejecuto
`npm audit fix` sin `--force`; no tuvo cambios compatibles mientras se fija Angular
22.0.7. La correccion que npm propone para el grupo webpack requiere un salto/downgrade
major incompatible del devkit y no esta autorizada. Reintentar el audit y un fix no
forzado en el proximo review mensual (a mas tardar 2026-08-20) o antes si Angular publica
un patch 22.0.x compatible para ese builder.

## Maintenance rule

Cada vez que aparezca una version mayor o LTS relevante:

1. Revisar soporte oficial y fecha de EOL.
2. Abrir una tarea de mantenimiento si la version actual entra en ventana de fin de
   soporte o si hay vulnerabilidad/beneficio claro.
3. Actualizar en una rama dedicada:
   - `package.json` / `package-lock.json`
   - configuracion, fuentes y pruebas que modifique la migracion oficial
   - Dockerfiles/CI/deploy docs solo si ya existen o si el cambio los afecta
   - PRD/master/readiness si cambia el contrato de soporte
4. Ejecutar validacion completa:
   - `npm run build`
   - `npm test -- --watch=false --browsers=ChromeHeadless`
   - `dotnet build backend/AstronomyExplorer.sln`
   - `dotnet test backend/AstronomyExplorer.sln`
   - `docker compose config` cuando W12 haya creado el artefacto Compose
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
