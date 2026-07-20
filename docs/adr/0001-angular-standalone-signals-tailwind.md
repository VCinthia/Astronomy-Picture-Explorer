# ADR 0001: Angular 19 Standalone + Signals + Tailwind CSS

Date: 2026-06-10
Status: Accepted
Builds on: none

## Context

Astronomy Picture Explorer Etapa 1 sirve como muestra de portfolio frontend. El proyecto prioriza explicitamente Angular 19+ con Signals y componentes Standalone, Tailwind CSS, accesibilidad y calidad visual fiel al diseno de referencia.

El repo `Astronomy-Picture-Explorer` esta recien clonado y vacio (solo `.git/`). No hay scaffold previo ni decisiones tecnicas tomadas todavia.

## Decision

La implementacion usara:

- Angular 19+ generado con `@angular/cli`, todos los componentes **Standalone** (sin `NgModule`).
- Estado manejado con **Signals** (`signal()`, `computed()`, `effect()`); no se usa `BehaviorSubject` ni NgRx.
- Routing con `provideRouter()` y lazy loading por ruta (Home / Explorador).
- Tailwind CSS con tokens nombrados en `tailwind.config.ts`, mapeados a variables CSS (`--color-bg`, `--color-surface`, `--color-text-primary`, etc.) declaradas en `global.css`. Sin clases arbitrarias (`bg-[#08080F]`).
- Utilidad `cn()` (clsx + tailwind-merge) para clases condicionales.
- Tipografia Inter, escala `display/title/body/caption` (40/26/15/11px) definida como tokens `fontSize` de Tailwind.
- npm como package manager.
- Tracking local en Markdown bajo `docs/` (sin GitHub issue hierarchy obligatoria para este proyecto solo).

## Alternatives Considered

1. NgModules clasicos - rechazado, requerimiento explicito del puesto es Standalone.
2. NgRx / Akita para estado global - rechazado, Signals cubre las necesidades de Etapa 1-2 (selectedDate, currentPicture, favorites, searchQuery) sin la sobrecarga de un store externo.
3. CSS plano / SCSS modules - rechazado, requerimiento explicito es Tailwind con diseno pixel-perfect desde Figma.
4. Tailwind v3 (`tailwind.config.js` + `@tailwind base/components/utilities`) - se evalua en P1-W1 contra lo que genera la version actual del Angular CLI; si el scaffold trae Tailwind v3, se documenta el ajuste de sintaxis sin cambiar la estrategia de tokens.

## Implementation Note (P1-W1, 2026-06-10)

El Angular CLI **no** instala Tailwind automaticamente, asi que la version se
eligio manualmente. Se instalo **Tailwind CSS v4.3.0** (entorno: Node v24.14.0,
npm 11.13.0, `@angular/cli@19.2.27`, plenamente compatibles entre si).

Ajuste de sintaxis respecto a lo que asumia esta ADR:

- Integracion via `.postcssrc.json` con el plugin `@tailwindcss/postcss` y
  `@import "tailwindcss"` en `src/styles.css` (no `@tailwind base/...`).
- Los tokens nombrados se declaran en el bloque **`@theme` de `src/styles.css`**
  (enfoque CSS-first idiomatico de v4), **no** en `tailwind.config.ts`. Cada
  token (`--color-space-*`, `--color-content-*`, `--color-accent`, `--font-sans`,
  `--text-display|title|body|caption`, `--spacing-page|card`,
  `--radius-card|chip|swatch`) genera a la vez la variable CSS en `:root` y la
  utilidad correspondiente (`bg-space-base`, `text-display`, `px-page`,
  `rounded-card`, ...). La estrategia "tokens nombrados + variables CSS, sin
  clases arbitrarias" (regla #4 del master plan) se mantiene intacta; solo cambia
  el archivo donde viven los tokens.

Motivo de no usar `tailwind.config.ts` con el puente `@config`: el builder de
Karma (webpack) de Angular 19 auto-detecta cualquier `tailwind.config.*` en la
raiz e intenta cargar `tailwindcss` v3 como plugin PostCSS, lo que rompe
`npm test` con Tailwind v4 (`getStylesConfig`). Eliminar el archivo de config y
usar `@theme` deja `npm run build` y `npm test` pasando con un unico enfoque.

## Consequences

### Positive

- Alineacion directa con el stack objetivo del portfolio; demuestra conocimiento real, no decorativo.
- Signals simplifican el estado de una app sin backend (P1/P2).
- Tokens Tailwind hacen el diseno mantenible y legible para cualquier revisor.
- Lazy loading mantiene el bundle inicial liviano.

### Negative

- Sin NgRx, lógica de sincronizacion mas compleja (ej. `favorites` en Etapa 2) recae en `effect()` y servicios; debe mantenerse simple para no reintroducir un store ad-hoc.
- Requiere mantener `tailwind.config.ts` y `global.css` sincronizados con el diseno de Figma en cada wave.

## Verification Impact

P1 debe verificar:

```powershell
npm install
npm run build
npm test
ng serve
```

Checks adicionales recomendados:

- `rg -n "NgModule" src/app` -> sin resultados.
- `rg -n "BehaviorSubject|NgRx" src/app` -> sin resultados.
- `rg -n "bg-\[#|text-\[#|p-\[" src/app` -> sin resultados (sin clases arbitrarias de color/spacing).

## Design-change clarification (2026-07-20)

Esta ADR conserva la decision y el entorno originales de P1, incluido Angular 19.2.
Una rama de mantenimiento posterior actualizo el proyecto secuencialmente a Angular
22.0.7 para resolver vulnerabilidades runtime antes de W8. Se mantienen Standalone,
Signals, Tailwind v4 y la estrategia `@theme`; no se cambia retrospectivamente la
evidencia P1 ni se adopta una migracion opcional de test/build fuera de ese alcance.
