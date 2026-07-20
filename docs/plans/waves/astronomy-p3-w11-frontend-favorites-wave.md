# Wave P3-W11 - Migracion frontend de favoritos

Date: 2026-07-16
Status: READY - Not Started
Wave ID: `P3-W11`
Depends On: P3-W7 + P3-W9 + P3-W10 merged
Suggested Branch: `wave/p3-w11-frontend-favorites`

## Goal

Reemplazar favoritos localStorage por la API autenticada manteniendo UX, accesibilidad
y navegacion desktop/mobile de P2.

## File scope

- `src/app/services/favorites.service.ts` + tests
- `src/app/pages/favorites/`
- `src/app/components/picture-card/`
- `src/app/components/picture-grid/`
- `src/app/app.routes.ts`
- `src/app/app.component.*`, `src/app/components/bottom-nav/*`

## Checklist

- [ ] W11.1 Estado `ApodEntry[]`/date set carga una vez por sesion autenticada.
- [ ] W11.2 Add/delete async con pending por fecha, error recuperable e idempotencia.
- [ ] W11.3 `/favorites` protegido consume listado hidratado, nunca N+1.
- [ ] W11.4 Toggle anonimo ofrece CTA login con returnUrl y semantica accesible.
- [ ] W11.5 Logout limpia favoritos de memoria; cambio de usuario nunca filtra estado.
  Debe reaccionar a `AuthService.sessionChange` (`previousUserId`, `currentUser`),
  contrato entregado por W9, no a localStorage ni a un identificador de request.
- [ ] W11.6 Eliminar `ape.favorites.v1` como fuente runtime. No migrar favoritos anonimos
  para evitar asociarlos silenciosamente a otra cuenta.

## Acceptance criteria

- Dos sesiones consecutivas no comparten favoritos en UI.
- Pending evita doble toggle y mantiene `aria-pressed` consistente.
- No queda lectura/escritura runtime de localStorage para favoritos.
- Desktop/mobile nav y estados empty/error conservan WCAG AA.

## Verification

```powershell
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
rg -n "ape\.favorites\.v1|localStorage" src/app
```

## Parent sync

- [ ] Actualizar `R3.11`, master/readiness y estado con evidencia.
