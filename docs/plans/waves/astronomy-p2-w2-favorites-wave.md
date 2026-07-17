# Wave P2-W2 - Favorites Feature

Date: 2026-07-08
Status: DONE
Wave ID: `P2-W2`
Source Phase: `P2`
Source Phase Issue: `N/A` (local Markdown workflow, no GitHub issue hierarchy)
Source Phase Plan: `docs/plans/astronomy-p2-favorites-search-plan.md`
Depends On: P2-W1 approved, committed and merged to `main`
Suggested Branch: `wave/p2-w2-favorites`
Suggested PR Title: `[P2-W2] Add favorite toggle and /favorites page`
Related Issues: None (local Markdown workflow)

## Goal

Exponer los favoritos en la UI: boton de toggle con icono SVG outline/filled en
`PictureCardComponent` y en
las cards de `PictureGridComponent`, y la ruta `/favorites` con `FavoritesComponent`
(grilla de guardadas + estado vacio). Depende de las signals de P2-W1.

## File Scope

- `src/app/components/picture-card/picture-card.component.ts` (agregar boton favorito)
- `src/app/components/picture-card/picture-card.component.html`
- `src/app/components/picture-card/picture-card.component.css` (solo si el layout lo requiere)
- `src/app/components/picture-card/picture-card.component.spec.ts`
- `src/app/components/picture-grid/picture-grid.component.ts` (boton favorito en cada card)
- `src/app/pages/favorites/favorites.component.ts`
- `src/app/pages/favorites/favorites.component.spec.ts`
- `src/app/app.routes.ts` (ruta `/favorites`, lazy)

## Checklist

- [x] W2.1 Agregar boton de favorito (SVG filled si guardada / outline si no) en
  `PictureCardComponent`,
  posicionado arriba-derecha de la imagen (circulo, ver `docs/figma/tokens.md`); llama a
  `toggleFavorite(date)`; `aria-pressed` + `aria-label` descriptivo, operable por teclado.
- [x] W2.2 Agregar el mismo boton en las cards de `PictureGridComponent`, reflejando estado
  desde la signal `favorites`.
- [x] W2.3 Mantener los botones como siblings superpuestos de los links de media; no
  anidar `button` dentro de `a` ni crear controles interactivos anidados.
- [x] W2.4 Implementar `FavoritesComponent` (ruta `/favorites`, lazy via `loadComponent`):
  deriva las `ApodEntry` cuyos `date` estan en `favorites` y las pasa a
  `PictureGridComponent`.
- [x] W2.5 Estado vacio de `/favorites`: mensaje + CTA a `/explorer` cuando no hay favoritos.
- [x] W2.6 Ordenar la grilla de favoritos por fecha descendente e ignorar fechas
  persistidas que ya no existan en el mock.
- [x] W2.7 Tests: toggle marca/desmarca y persiste; `/favorites` lista las guardadas y
  muestra el estado vacio; `aria-pressed` refleja el estado.

## Acceptance Criteria

- El toggle de favorito es visible y funcional en `PictureCardComponent` y en las cards de
  `PictureGridComponent`, y el estado persiste (via P2-W1) tras reload.
- `/favorites` muestra la grilla de guardadas o el estado vacio con CTA.
- El boton es operable por teclado y anuncia su estado (`aria-pressed`/`aria-label`).
- Sin `NgModule`/`BehaviorSubject`; Tailwind solo con tokens nombrados.

## Verification

```powershell
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
rg -n "NgModule|BehaviorSubject" src/app
rg -n "bg-\[#|text-\[#|p-\[" src/app
```

## Parent Plan Sync

- [x] El orquestador actualiza el checklist del phase plan (`R2.2`).
- [x] El orquestador mantiene `docs/plans/astronomy-master-plan.md` alineado.
- [x] Registrar estado final como `DONE` o `BLOCKED`.
- [x] No commitear hasta recibir aprobacion explicita de la usuaria.

## Review Evidence

- Orchestrator review: approved after two remediation loops.
- Remediated: mutually exclusive active color classes; SVG icon replaces Unicode hearts.
- Favorite control: 36x36 px; SVG heart: 20x20 px; active color `#4d78ff`.
- `npm run build`: PASS.
- `npm test -- --watch=false --browsers=ChromeHeadless`: 57/57 PASS.
- Browser smoke: add/persist/list/remove/empty state PASS.
- Static checks and `git diff --check`: PASS.
- Pre-approval Git state: unstaged and uncommitted.
- User approval received; commit `b50cb87` created and merged fast-forward to `main`.

## Post-implementation clarification (2026-07-16)

P3-W11 conserva card/grid/nav y accesibilidad, pero la API hidratada reemplaza
`ape.favorites.v1`; `/favorites` queda protegida despues del bootstrap de P3-W9.
