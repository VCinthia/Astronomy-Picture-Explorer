# Wave P2-W4 - Mobile Bottom Nav + Icon Polish

Date: 2026-07-08
Status: DONE
Wave ID: `P2-W4`
Source Phase: `P2`
Source Phase Issue: `N/A` (local Markdown workflow, no GitHub issue hierarchy)
Source Phase Plan: `docs/plans/astronomy-p2-favorites-search-plan.md`
Depends On: P2-W3 approved, committed and merged to `main`
Suggested Branch: `wave/p2-w4-navigation`
Suggested PR Title: `[P2-W4] Add mobile bottom nav and icon polish`
Related Issues: None (local Markdown workflow)

## Goal

Cerrar P2 con `BottomNavComponent` fijo al pie en mobile (Home | Explore | Favorites,
con iconos SVG), en lugar de menu hamburguesa, y normalizar la iconografia heredada.
Implementa DD-02. El link desktop Favorites se entrega en P2-W3.

## File Scope

- `src/app/app.component.ts` (montar bottom nav en mobile)
- `src/app/app.component.html`
- `src/app/app.component.css` (solo si el layout lo requiere)
- `src/app/app.component.spec.ts`
- `src/app/components/bottom-nav/bottom-nav.component.ts`
- `src/app/components/bottom-nav/bottom-nav.component.spec.ts`
- `src/app/components/picture-card/picture-card.component.html` (normalizacion de iconos)
- `src/app/components/picture-card/picture-card.component.spec.ts` (regresion de iconos)

## Checklist

- [x] W4.1 Implementar `BottomNavComponent`: 3 tabs con iconos SVG (Home | Explore |
  Favorites), sin emojis/glifos Unicode,
  height 56 px, fondo `space-surface`, borde superior `space-border`; tab activo `accent`,
  inactivos `content-secondary`; usa `routerLink`/`routerLinkActive`.
- [x] W4.2 Montar `BottomNavComponent` solo en breakpoint mobile (`< 768 px`, Tailwind
  `md`), `position: fixed;
  bottom: 0; z-index: 50`; agregar `padding-bottom: 56px` al contenido para que la nav no tape
  el footer/ultima card.
- [x] W4.3 Ocultar la bottom nav en desktop y la nav horizontal (o su version reducida) segun
  breakpoint, evitando doble navegacion visible a la vez.
- [x] W4.4 Tests: la bottom nav marca el tab activo segun la ruta; los 3 links navegan; a11y
  (`nav` landmark, `aria-current` en el activo).
- [x] W4.5 Reemplazar flechas y marcadores externos Unicode heredados en App/PictureCard
  por SVG inline; conservar solo el diamante/estrella de marca como excepcion aprobada.

## Acceptance Criteria

- Bottom nav visible y funcional en mobile (`< 768 px`), oculta en desktop; el contenido no
  queda tapado por la barra.
- Navegacion operable por teclado; `aria-current` en el destino activo.
- Sin `NgModule`/`BehaviorSubject`; Tailwind solo con tokens nombrados.

## Verification

```powershell
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
rg -n "NgModule|BehaviorSubject" src/app
rg -n "bg-\[#|text-\[#|p-\[" src/app
```

## Parent Plan Sync

- [x] El orquestador actualiza el checklist del phase plan (`R2.4` completado tras integrar W4).
- [x] El orquestador mantiene `docs/plans/astronomy-master-plan.md` alineado.
- [x] Preparar el cierre de P2; el estado DONE requiere merge de W4, deploy de `main`
  y smoke test final.
- [x] Registrar estado final como `DONE` o `BLOCKED`.
- [x] No commitear hasta recibir aprobacion explicita de la usuaria.

## Review Evidence

- Branch/worktree: `wave/p2-w4-navigation` / `Astronomy-Picture-Explorer-p2-w4`.
- `npm run build`: PASS.
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS, 77/77.
- `git diff --check`: PASS.
- Browser smoke en `390x844`: bottom nav 390x56, tres tabs de 130 px, nav desktop oculta,
  `Favorites` activo con `aria-current="page"` y padding inferior de 56 px.
- Browser smoke en `1280x800`: nav desktop visible y bottom nav oculta.
- Revision de codigo: sin findings accionables; aprobada por la usuaria.
- Aprobacion explicita recibida; commit `b72c7e2` y merge fast-forward a `main`.
- Regresion post-merge en `main`: build PASS y 77/77 tests.

## Post-implementation clarification (2026-07-16)

P3 cambia Home de `/` a `/home` con redirect y añade AuthGuard en Favorites; P3-W10/W11
actualizan links/active state conservando el breakpoint y patron bottom-nav entregado.

P3-W10 ya hizo el redirect `/` -> `/home` y actualizo ambos navs. El guard de Favorites
es el contrato W9; W11 sustituye la fuente de datos sin modificar esta evidencia visual.
