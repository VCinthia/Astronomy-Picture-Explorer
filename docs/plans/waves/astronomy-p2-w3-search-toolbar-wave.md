# Wave P2-W3 - Search + Explorer Dual-Mode Toolbar

Date: 2026-07-08
Status: DONE
Wave ID: `P2-W3`
Source Phase: `P2`
Source Phase Issue: `N/A` (local Markdown workflow, no GitHub issue hierarchy)
Source Phase Plan: `docs/plans/astronomy-p2-favorites-search-plan.md`
Depends On: P2-W2 approved, committed and merged to `main`
Suggested Branch: `wave/p2-w3-search-toolbar`
Suggested PR Title: `[P2-W3] Add search toolbar and desktop Favorites nav`
Related Issues: None (local Markdown workflow)

## Goal

Construir `SearchBarComponent` y convertir `ExplorerComponent` en una toolbar de 2
columnas (search + date) que convive en desktop y se apila en 2 filas en mobile,
alternando entre keyword mode (grilla) y date mode (hero) segun `searchQuery`.
Implementa DD-01 y DD-03, y agrega el acceso desktop a `/favorites` para que la ruta
entregada en W2 sea descubrible. No cambia los internos de `DatePickerComponent`
(chips → P3).

## File Scope

- `src/app/components/search-bar/search-bar.component.ts`
- `src/app/components/search-bar/search-bar.component.spec.ts`
- `src/app/pages/explorer/explorer.component.ts` (toolbar 2-col + switch de modo)
- `src/app/pages/explorer/explorer.component.spec.ts`
- `src/app/components/date-picker/date-picker.component.ts` (solo reposicion/estilos, sin cambio interno)
- `src/app/app.component.html` (link desktop Favorites)
- `src/app/app.component.spec.ts` (navegacion desktop y estado activo)

## Checklist

- [x] W3.1 Implementar `SearchBarComponent`: input con debounce 300 ms, icono lupa a la
  izquierda y boton × para limpiar. Mantiene un borrador local, recibe el query actual
  por input y emite cambios; `ExplorerComponent` escribe en `searchQuery`. Limpiar emite
  `''` inmediatamente, cancela el debounce pendiente; `label`/`aria` correctos.
- [x] W3.1a Implementar search, clear y cualquier icono nuevo como SVG inline; no usar
  emojis/glifos Unicode como iconos de controles.
- [x] W3.2 `ExplorerComponent` desktop: toolbar de 2 columnas en una fila — `SearchBarComponent`
  (~820 px izquierda) + `DatePickerComponent` (~360 px derecha); ambos siempre visibles.
- [x] W3.3 `ExplorerComponent` mobile: misma toolbar apilada en 2 filas full-width (search
  fila 1, date fila 2), height ~42 px, gap 8 px (DD-03).
- [x] W3.4 Switch de modo por `searchQuery`: con valor → keyword mode → `PictureGridComponent`
  con `searchResults`; vacio → date mode → `PictureCardComponent` (hero, comportamiento P1).
- [x] W3.5 Limpiar el search (×) vuelve al hero de la fecha seleccionada.
- [x] W3.6 Mostrar un estado explicito y accesible cuando una busqueda no tiene resultados.
- [x] W3.7 Tests: debounce, cancelacion y limpiado de `SearchBarComponent`;
  `ExplorerComponent` alterna entre grilla y hero segun `searchQuery`; estado sin resultados.
- [x] W3.8 Agregar `Favorites` a la nav desktop (Home | Explore | Favorites), con
  `routerLinkActive`, estado activo `accent`, `aria-current="page"`, foco visible y tests.

## Acceptance Criteria

- Desktop: search y date visibles lado a lado; mobile: apilados en 2 filas.
- Escribir filtra en grilla; limpiar (×) vuelve al hero de la fecha seleccionada.
- El `DatePickerComponent` conserva su comportamiento P1 (solo se reposiciona/estiliza).
- `SearchBarComponent` operable por teclado, con foco visible y `aria-label`.
- Desktop nav muestra Home | Explore | Favorites; el destino activo usa `accent` y
  expone `aria-current="page"`.
- Sin `NgModule`/`BehaviorSubject`; Tailwind solo con tokens nombrados.

## Verification

```powershell
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
rg -n "NgModule|BehaviorSubject" src/app
rg -n "bg-\[#|text-\[#|p-\[" src/app
```

## Parent Plan Sync

- [x] El orquestador actualiza el checklist del phase plan (`R2.3`).
- [x] El orquestador mantiene `docs/plans/astronomy-master-plan.md` alineado.
- [x] Registrar estado final como `DONE` o `BLOCKED`.
- [x] No commitear hasta recibir aprobacion explicita de la usuaria.

## Review Evidence

- Orchestrator review: approved after the search/a11y and nav-breakpoint remediation loops.
- Remediated: unique reusable input IDs, accessible results `h1`, mutually exclusive
  nav colors and the 768px desktop breakpoint.
- `npm run build`: PASS.
- `npm test -- --watch=false --browsers=ChromeHeadless`: 71/71 PASS.
- Browser smoke: debounce/results/clear/hero, responsive toolbar and desktop nav PASS.
- Static checks and `git diff --check`: PASS.
- Pre-approval Git state: unstaged and uncommitted.
- Scope extension approved by the user: desktop Favorites nav moved from P2-W4 to P2-W3.
- User approval received; commit `0bde545` created and merged fast-forward to `main`.

## Post-implementation clarification (2026-07-16)

P3-W10 conserva debounce y toolbar, reemplaza `availableDates`/mock por fecha real y
PostgreSQL FTS sobre title+explanation, sin metadata externa de keywords.
