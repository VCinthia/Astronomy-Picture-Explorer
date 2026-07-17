# Wave P2-W1 - State Layer + Shared Grid

Date: 2026-07-08
Status: DONE
Wave ID: `P2-W1`
Source Phase: `P2`
Source Phase Issue: `N/A` (local Markdown workflow, no GitHub issue hierarchy)
Source Phase Plan: `docs/plans/astronomy-p2-favorites-search-plan.md`
Depends On: P1 DONE; branch from current `main`
Suggested Branch: `wave/p2-w1-state-and-grid`
Suggested PR Title: `[P2-W1] Add favorites/search signals and shared picture grid`
Related Issues: None (local Markdown workflow)

## Goal

Extender `AstronomyService` con las signals de P2 (`favorites` persistida en
`localStorage`, `searchQuery`, `searchResults` computed) y construir el
`PictureGridComponent` compartido que consumiran Favoritos (P2-W2) y los
resultados de busqueda (P2-W3). Sin UI de nav ni toolbar todavia.

## File Scope

- `src/app/services/astronomy.service.ts` (agregar signals; sin romper contrato P1)
- `src/app/services/astronomy.service.spec.ts`
- `src/app/components/picture-grid/picture-grid.component.ts`
- `src/app/components/picture-grid/picture-grid.component.spec.ts`
- `src/styles.css` (solo para el token semantico de altura de media compacta)
- `src/app/models/apod.model.ts` (solo si hace falta un tipo auxiliar; sin romper `ApodEntry`)

## Checklist

- [x] W1.1 Agregar signal writable internamente `favorites: Signal<string[]>` (array de
  `ApodEntry.date`) en `AstronomyService`, inicializada desde `localStorage` con key
  versionada `ape.favorites.v1`. Validar que el valor sea un array de strings, eliminar
  duplicados y descartar fechas que no existan en el mock; JSON o shape corruptos → `[]`.
- [x] W1.2 Tratar errores de lectura/escritura de `localStorage` sin romper la app.
- [x] W1.3 Persistir `favorites` en `localStorage` via `effect()`; exponer metodos
  `toggleFavorite(date)`, `isFavorite(date)` (o computed helper), sin duplicados.
- [x] W1.4 Agregar signal writable `searchQuery: Signal<string>` (default `''`).
- [x] W1.5 Agregar computed `searchResults: Signal<ApodEntry[]>` que filtra el mock por
  `title` + `explanation` (trim + case-insensitive); `[]` cuando `searchQuery` esta vacio.
- [x] W1.6 Implementar `PictureGridComponent` (standalone): recibe `entries: ApodEntry[]`
  como input, renderiza grilla de cards compactas (3 col desktop / 1 col mobile) usando
  solo tokens. En W1 es presentacional; el boton de favorito se agrega en W2.
- [x] W1.7 Tests: signals de favoritos (toggle, persistencia, dedupe, localStorage corrupto),
  `searchResults` (match, vacio, case-insensitive) y render basico de `PictureGridComponent`.

## Acceptance Criteria

- `AstronomyService` expone `favorites`, `searchQuery`, `searchResults` sin cambiar el
  comportamiento de `selectedDate`/`currentPicture` de P1.
- `favorites` sobrevive a un reload (persistencia real en `localStorage`) y datos
  corruptos no rompen la inicializacion del servicio.
- `searchResults` devuelve resultados correctos y vacio cuando la query esta vacia.
- `PictureGridComponent` renderiza N entradas y colapsa a 1 columna en mobile.
- Componentes Standalone, estado via Signals (sin `NgModule`/`BehaviorSubject`).
- Tailwind solo con tokens nombrados (sin clases arbitrarias `bg-[#...]`).

## Verification

```powershell
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
rg -n "NgModule|BehaviorSubject" src/app
rg -n "bg-\[#|text-\[#|p-\[" src/app
```

## Parent Plan Sync

- [x] El orquestador actualiza el checklist del phase plan (`R2.1`).
- [x] El orquestador mantiene `docs/plans/astronomy-master-plan.md` alineado.
- [x] Registrar estado final como `DONE` o `BLOCKED`.
- [x] No commitear hasta recibir aprobacion explicita de la usuaria.

## Review Evidence

- Orchestrator review: approved after one remediation loop.
- Remediated: own-property archive validation and compact grid layout.
- `npm run build`: PASS.
- `npm test -- --watch=false --browsers=ChromeHeadless`: 52/52 PASS.
- Static checks and `git diff --check`: PASS.
- Pre-approval Git state: changes unstaged and uncommitted.
- User approval received; commit `7d1f031` created and merged fast-forward to `main`.

## Post-implementation clarification (2026-07-16)

Signals localStorage/computed fueron el alcance P2. P3-W10/W11 reemplazan search por FTS
y favorites por estado HTTP autenticado; no se migran datos locales automaticamente.
