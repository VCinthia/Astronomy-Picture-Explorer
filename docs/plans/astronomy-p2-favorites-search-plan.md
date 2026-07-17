# Phase Plan P2 - Favoritos Y Busqueda Por Keyword

Date: 2026-06-10
Status: DONE in production (2026-07-16)
Phase: `P2`
Source master plan: `docs/plans/astronomy-master-plan.md`

## 1. Goal

Agregar interactividad persistente y busqueda sin backend: favoritos guardados en
`localStorage`, busqueda por keyword sobre el mock JSON, navegacion mobile via bottom
navigation bar, y barra de herramientas de 2 columnas (search + date) en el Explorador.
Sin backend ni autenticacion.

## 2. Scope

### Included

**Favoritos**
- Toggle de favorito con icono SVG outline/filled en cada card (`PictureCardComponent`
  y `PictureGridComponent`);
  persistencia de `ApodEntry.date` en `localStorage` (key versionada
  `ape.favorites.v1`) via `effect()` sobre signal `favorites`.
- Nueva ruta `/favorites` con `FavoritesComponent`: grilla `PictureGridComponent` de
  entradas guardadas; estado vacío si no hay favoritos.

**Búsqueda**
- `SearchBarComponent`: input de texto con debounce 300 ms, ícono lupa, botón × para limpiar.
  Mantiene un borrador local y emite el valor debounced; `ExplorerComponent` actualiza
  `AstronomyService.searchQuery`. Limpiar emite `''` inmediatamente y cancela el debounce.
- Filtra `apod.json` por `title` y `explanation` (case-insensitive, sin backend).
- Signal `searchQuery` (writable); `searchResults` (computed desde `searchQuery` sobre mock).

**ExplorerComponent — toolbar 2 modos** (DD-01)
- Desktop: toolbar de 2 columnas en una fila — `SearchBarComponent` (~820 px izquierda) +
  `DatePickerComponent` (~360 px derecha). Ambos siempre visibles.
- Mobile: misma toolbar apilada en 2 filas full-width — search arriba, date abajo (DD-03).
- Modo activo determinado por `searchQuery`:
  - `searchQuery` con valor → **keyword mode** → muestra `PictureGridComponent` con resultados.
  - `searchQuery` vacío → **date mode** → muestra `PictureCardComponent` (hero, comportamiento P1).

**Navegación**
- Desktop `AppComponent`: agregar link "Favorites" a la nav existente (Home | Explore | **Favorites**).
- Mobile: `BottomNavComponent` fijo al pie (`< 768 px`) — 3 tabs con iconos SVG:
  Home | Explore | Favorites;
  height 56 px; `position: fixed; bottom: 0`; contenido con `padding-bottom: 56 px` (DD-02).

**Componentes nuevos**
- `FavoritesComponent` (página `/favorites`)
- `SearchBarComponent` (input de búsqueda reutilizable)
- `PictureGridComponent` (grilla de cards compactas, usada en Favorites y en search results)
- `BottomNavComponent` (nav mobile, incluida en `AppComponent` solo en breakpoint mobile)

**Signals nuevas en `AstronomyService`**
- `favorites: Signal<string[]>` — array de `ApodEntry.date` guardadas; writable internamente;
  sincronizada
  con `localStorage` via `effect()`.
- `searchQuery: Signal<string>` — writable, string del input de búsqueda.
- `searchResults: Signal<ApodEntry[]>` — computed; filtra el mock por `searchQuery`
  sobre `title` y `explanation`; vacío si `searchQuery` es vacío.

**DatePickerComponent** — sin cambios internos en P2. Solo se reposiciona:
  - Desktop: columna derecha del toolbar (antes estaba debajo del header a full width).
  - Mobile: fila 2 del toolbar apilado.
  - En P3 se reemplaza el interior (chips → `<input type="date">`) sin tocar el layout.

### Excluded

- Backend real, autenticacion, base de datos (P3).
- Cambios internos al `DatePickerComponent` (chips → calendar input — P3).
- Recuperacion de password, OAuth (P3).
- `AuthGuard` en `/favorites`: la ruta existe sin guard en P2 (no hay auth);
  el guard se agrega en P3 junto con `AuthService`.
- Migración de signals `favorites` y `searchResults` a llamadas HTTP: en P2
  son client-side (localStorage y computed sobre mock); en P3 apuntan al backend.

## 3. Dependencies and Gates

- Depends on: P1 cerrado (`docs/plans/astronomy-p1-frontend-mock-plan.md`, 36/36 tests,
  deployed en https://astronomy-picture-explorer.netlify.app/).
- Gate to open phase: `apod.json` y `AstronomyService` estables — contrato `ApodEntry`
  sin cambios rompedores en P2.
- Gate to close phase:
  - `/favorites` funcional con persistencia real en `localStorage`.
  - Búsqueda por keyword retorna resultados correctos sobre el mock.
  - Toggle favorito visible y funcional en `PictureCardComponent` y `PictureGridComponent`.
  - Desktop nav muestra Home | Explore | Favorites.
  - Bottom nav visible y funcional en mobile (`< 768 px`, Tailwind `md`); nav desktop
    visible desde `768 px`.
  - `npm run build` y `npm test` en verde.
  - Deploy en Netlify actualizado.

## 4. Checklist

Agrupado por requisito; el detalle Files/Acceptance/Verification vive en cada wave.

- [x] **R2.1 — State layer + shared grid** (wave P2-W1): signals `favorites` (localStorage),
  `searchQuery`, `searchResults`; `PictureGridComponent` reutilizable.
- [x] **R2.2 — Favoritos** (wave P2-W2): toggle con icono de corazon en card y grid;
  ruta `/favorites` +
  `FavoritesComponent` con estado vacio.
- [x] **R2.3 — Busqueda + toolbar + nav desktop** (wave P2-W3): `SearchBarComponent`
  con debounce; toolbar 2-col desktop / apilado mobile; switch keyword/date mode;
  link Favorites desktop (DD-01, DD-03).
- [x] **R2.4 — Navegacion mobile + icon polish** (wave P2-W4): `BottomNavComponent`
  mobile y normalizacion de iconos heredados (DD-02).

Execution: P2-W1 (`7d1f031`), P2-W2 (`b50cb87`) y P2-W3 (`0bde545`) estan
aprobadas, commiteadas y mergeadas fast-forward a `main`. W3 incluye search, toolbar
responsive y el link desktop Favorites movido desde W4; build OK, 71/71 tests y smoke
browser OK. P2-W4 fue aprobada, commiteada como `b72c7e2` e integrada fast-forward a
`main`; build OK, 77/77 tests y smoke responsive OK. Las cuatro waves y todos los
requisitos R2.1-R2.4 estan implementados. `main` y `origin/main` coinciden en `b72c7e2`.
El deploy Netlify y smoke productivo fueron verificados el 2026-07-16; P2 queda DONE.

## 5. Exit Criteria

- `/favorites` funcional con persistencia real en `localStorage`.
- Busqueda por keyword retorna resultados correctos sobre el mock.
- Toggle favorito visible y funcional en `PictureCardComponent` y `PictureGridComponent`.
- Toolbar del Explorador: 2 columnas en desktop, 2 filas apiladas en mobile; alterna
  keyword/date mode segun `searchQuery`.
- Desktop nav muestra Home | Explore | Favorites; bottom nav funcional en mobile
  (`< 768 px`, Tailwind `md`).
- `npm run build` y `npm test` en verde; deploy en Netlify actualizado.

Evidencia productiva de cierre (`2026-07-16T18:55:59Z`): Home/nav renderizados; favorito
`Thor's Helmet` persistido entre `/` y `/favorites`; search `Hydra` encontro
`Hydra Cluster`; viewport 390x844 oculto nav desktop y mostro bottom nav fixed.

## 6. Wave Split

- `docs/plans/waves/astronomy-p2-w1-state-and-grid-wave.md` — signals + `PictureGridComponent`.
- `docs/plans/waves/astronomy-p2-w2-favorites-wave.md` — toggle favorito + `/favorites`.
- `docs/plans/waves/astronomy-p2-w3-search-toolbar-wave.md` — search bar + toolbar 2-col + nav desktop.
- `docs/plans/waves/astronomy-p2-w4-navigation-wave.md` — bottom nav mobile + icon polish.

Orden: W1 (base de estado) → W2 (favoritos) → W3 (busqueda/toolbar) → W4 (navegacion).
W2 y W3 dependen de las signals de W1; W4 depende de la ruta `/favorites` de W2.

### Execution and approval gate

- Cada wave se implementa en su rama sugerida mediante un worktree dedicado.
- W1 nace de `main`. Cada wave posterior nace de `main` una vez que la wave anterior
  fue revisada, aprobada explicitamente por la usuaria, commiteada y mergeada.
- Los agentes implementadores no hacen `git add`, commit, merge ni push durante la
  implementacion o el loop de correcciones.
- El orquestador revisa codigo, tests y criterios. Cuando la wave queda lista, presenta
  evidencia y espera aprobacion de la usuaria antes de autorizar el commit.
- `docs/` esta ignorado por Git: el orquestador, no el agente implementador, mantiene
  sincronizados este phase plan, la wave y el master plan local.
- P2 solo se marca DONE despues de integrar W4, desplegar `main` en Netlify y completar
  un smoke test de Home, Explore, Favorites, persistencia y busqueda.

### Iconography rule

- Usar SVG inline o el sistema de iconos del proyecto; no usar emojis/glifos Unicode
  como iconos de controles.
- Unica excepcion aprobada: el diamante/estrella azul del nombre de la aplicacion.

---

## 7. Design Decisions (pre-implementation)

### DD-01 · Explorer toolbar: búsqueda por keyword + selector de fecha en 2 columnas

**Decisión (2026-07-08):** El `ExplorerComponent` muestra una barra de herramientas de 2 columnas en la misma fila:

```
[ search-icon  Buscar por título o descripción...  clear-icon ]  [ calendar-icon  Jun 09, 2026  chevron ]
              ~820px (~68%)                              ~360px (~30%)
```

**Comportamiento:**
- Usuario escribe en la lupa → resultados filtrados en grilla debajo (keyword mode).
- Usuario cambia la fecha → hero card abajo (date mode, comportamiento heredado de P1).
- Limpiar el search (×) → vuelve al hero de la fecha seleccionada.
- Ambos controles siempre visibles; se pueden usar independientemente.

**Referencia visual:** frame `Desktop – Explorer + Search` en Figma (`miqqmNJAcF0Mbe1WizAJIu`).

**Motivación:**
El `DatePickerComponent` actual (chips de fechas del mock) funciona para 8 entradas pero
es inviable con las ~10.950 entradas reales de la NASA API (desde 1995-06-16).
Separar los dos controles en columnas permite que en P3 se reemplace únicamente el
interior del date picker (chips → `<input type="date" min="1995-06-16">`) sin tocar
el layout de `ExplorerComponent`.

**Implicaciones para P2:**
- `SearchBarComponent` nuevo: input de texto con debounce (300 ms), ícono lupa, botón ×.
- `DatePickerComponent` existente: sin cambios de layout, solo se integra a la derecha.
- `ExplorerComponent`: maneja dos modos mutuamente excluyentes via signals:
  - `searchQuery` (signal) con valor → keyword mode → muestra `PictureGridComponent`.
  - `searchQuery` vacío → date mode → muestra `PictureCardComponent` (hero).

**Implicaciones para P3:**
- Solo cambiar los internos de `DatePickerComponent` a `<input type="date">`.
- El layout del toolbar y el contrato de signals no cambian.

---

### DD-02 · Navegación mobile: bottom navigation bar (no hamburguesa)

**Decisión (2026-07-08):** Con 3 destinos (Home | Explore | Favorites), se usa una
**bottom navigation bar** fija al pie de la pantalla en lugar de un menú hamburguesa.

**Spec:**
- Height: 56px, fondo `space-surface`, borde superior `space-border`
- 3 tabs de 130px con SVG: `Home` | `Explore` | `Favorites`
- Tab activo: `accent` (#4d78ff) | inactivos: `content-secondary`
- CSS: `position: fixed; bottom: 0; z-index: 50` + `padding-bottom: 56px` en el contenido

**Por qué no hamburguesa:**
- Con 3 items, el menú drawer requiere un tap extra innecesario.
- La bottom nav es zona del pulgar (thumb-friendly) en pantallas altas.
- Patrón estándar en apps móviles web y nativas (Instagram, YouTube, Twitter).

**Frames Figma afectados:**
- `Mobile – Home`: agregar bottom nav (Home activo)
- `Mobile – Favorites`: agregar bottom nav (Favorites activo)
- `Mobile – Explorer` (nuevo): agregar bottom nav (Explore activo)
- Pendiente de ejecución: ver `docs/figma/pending-mobile-explorer.md`

---

### DD-03 · Mobile Explorer toolbar: apilado vertical

**Decisión (2026-07-08):** El toolbar de 2 columnas del desktop (search 820px + date 360px)
se adapta a mobile como **2 filas full-width apiladas**:

```
[ search-icon  Search by title or description... ]  ← 350px, row 1
[ calendar-icon  Jun 09, 2026  chevron           ]  ← 350px, row 2
```

Cada input: height 42px, radio 8px, borde `space-border` inactivo / `accent` cuando focused.
Gap entre filas: 8px. Contenido debajo: hero card (date mode) o grilla (search mode).

---

## 8. Post-implementation design clarification for P3 (2026-07-16)

P2 conserva su diseño y evidencia historica. P3 sustituye fuentes temporales:

- Search title/explanation pasa de computed mock a PostgreSQL FTS.
- Favorites localStorage pasa a API autenticada y no se importa silenciosamente.
- Chips/`availableDates` pasan a input de fecha real; el toolbar responsive no cambia.
- La ruta `/favorites` recibe AuthGuard cuando existe bootstrap de sesion.
- Los cambios viven en P3-W9..W11 y no modifican lo que P2 entrego.
