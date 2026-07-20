# Wave P3-W11 - Migracion frontend de favoritos

Date: 2026-07-16
Status: DONE - 2026-07-20
Wave ID: `P3-W11`
Depends On: P3-W7 + P3-W9 + P3-W10 merged
Suggested Branch: `wave/p3-w11-frontend-favorites`

## Goal

Reemplazar favoritos localStorage por la API autenticada manteniendo UX, accesibilidad
y navegacion desktop/mobile de P2.

## W10 handoff

W10 ya no tiene mock ni un catalogo local con el que hidratar fechas persistidas. Hasta
esta wave, `AstronomyService` conserva una fachada P2 minima solo para no mezclar la
reescritura de favoritos con la migracion APOD. W11 la reemplaza en el mismo cambio: no
lee, escribe ni migra `ape.favorites.v1`, ni asume que una fecha guardada anonimamente
corresponde al usuario que acaba de autenticarse.

## File scope

- `src/app/services/favorites.service.ts` + tests
- `src/app/auth/return-url.ts`
- `src/app/auth/login/login.component.ts`
- `src/app/services/astronomy.service.ts` + tests
- `src/app/pages/favorites/`
- `src/app/components/picture-card/`
- `src/app/components/picture-grid/`

## Checklist

- [x] W11.1 Estado `ApodEntry[]`/date set carga una vez por sesion autenticada.
- [x] W11.2 Add/delete async con pending por fecha, error recuperable e idempotencia.
- [x] W11.3 `/favorites` protegido consume listado hidratado, nunca N+1.
- [x] W11.4 Toggle anonimo ofrece CTA login con returnUrl y semantica accesible.
- [x] W11.5 Logout limpia favoritos de memoria; cambio de usuario nunca filtra estado.
  Debe reaccionar a `AuthService.sessionChange` (`previousUserId`, `currentUser`),
  contrato entregado por W9, no a localStorage ni a un identificador de request.
- [x] W11.6 Eliminar `ape.favorites.v1` como fuente runtime. No migrar favoritos anonimos
  para evitar asociarlos silenciosamente a otra cuenta.

## Implementation evidence

- `FavoritesService` carga `GET /api/favorites` una vez por usuario/sesion y conserva
  la lista hidratada; no consulta APOD por card.
- Add usa `POST /api/favorites` con `{ "apod_date": "YYYY-MM-DD" }`; delete usa
  `DELETE /api/favorites/{date}`. Ambos esperan `204`, bloquean doble toggle por fecha y
  no aceptan una respuesta vieja despues de logout o switch de cuenta.
- El servicio consume el contrato W9 `sessionChange` y `currentUser`: borra lista,
  pending, errores y requests en vuelo antes de cargar la siguiente cuenta. Las lecturas
  y callbacks tambien comparan el usuario actual directamente, por lo que una respuesta
  A no puede mutar ni mostrarse para B antes de que Angular programe el effect de limpieza.
  La signal de identidad activa invalida valores publicos ya leidos al
  activar B y luego exponen su coleccion hidratada.
- Un toggle anonimo navega a login con retorno interno normalizado. La etiqueta accesible
  del control explica que iniciar sesion permite guardar el favorito.
- `AstronomyService` ya no posee fechas favoritas, memoria de hidratacion ni Web Storage.
  No se migra `ape.favorites.v1`; una sesion nueva comienza solo desde la API.

## Acceptance criteria

- Dos sesiones consecutivas no comparten favoritos en UI.
- Pending evita doble toggle y mantiene `aria-pressed` consistente.
- No queda lectura/escritura runtime de localStorage para favoritos.
- Desktop/mobile nav y estados empty/error conservan WCAG AA.

## Verification

```powershell
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
npm audit --omit=dev
rg -n "ape\.favorites\.v1" src/app
git diff --check
```

## Parent sync

- [x] Actualizar `R3.11`, master/readiness y estado con evidencia.
