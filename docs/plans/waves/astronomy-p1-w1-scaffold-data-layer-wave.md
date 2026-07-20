# Wave P1-W1 - Scaffold And Data Layer

Date: 2026-06-10
Status: DONE
Wave ID: `P1-W1`
Source Phase: `P1`
Source Phase Plan: `docs/plans/astronomy-p1-frontend-mock-plan.md`
Suggested Branch: `wave/p1-w1-scaffold-data-layer`
Suggested PR Title: `[P1-W1] Scaffold Angular project and data layer`

## Goal

Crear el scaffold Angular 19 Standalone + Tailwind CSS, definir el contrato de datos `ApodEntry`/`ApodMock`, poblar `apod.json` con el mock minimo requerido, e implementar `AstronomyService` con sus signals base.

## File Scope

- `package.json`, `angular.json`, `tsconfig*.json`
- `src/app/app.config.ts`, `src/app/app.routes.ts` (esqueleto)
- `tailwind.config.ts`, `src/styles.css` (o `global.css`)
- `src/utils/cn.ts`
- `src/app/models/apod.model.ts`
- `src/assets/mock/apod.json`
- `src/app/services/astronomy.service.ts`

## Checklist

- [x] W1.1 Generar scaffold Angular 19 Standalone con `provideRouter()`, sin `NgModule`.
- [x] W1.2 Instalar y configurar Tailwind CSS; validar si el CLI genera v3 o v4 y ajustar sintaxis en consecuencia. → **Tailwind v4.3.0** via `.postcssrc.json` + `@tailwindcss/postcss` + `@import "tailwindcss"`.
- [x] W1.3 Definir tokens (colores `space.*`/`content.*`/`accent`, `fontFamily.sans: Inter`, `fontSize` display/title/body/caption, `spacing.page/card`, `borderRadius.card/chip/swatch`) y variables CSS. → declarados en el bloque **`@theme` de `src/styles.css`** (CSS-first de v4, ver ADR-0001 Implementation Note), no en `tailwind.config.ts`.
- [x] W1.4 Agregar `cn()` (clsx + tailwind-merge) en `src/utils/cn.ts`.
- [x] W1.5 Crear `src/app/models/apod.model.ts` con `ApodEntry` y `ApodMock`.
- [x] W1.6 Crear `src/assets/mock/apod.json` con >= 15 entradas: incluir `2004-01-16`, `2026-05-22` (WR 134), y >= 1 entrada `media_type: "video"`. → 15 entradas, todas con URLs NASA verificadas (HTTP 200). **Correccion factual:** el APOD real del `2004-01-16` no es "Hubble birthday" sino *"Martian Surface in Perspective"* (rover Spirit); el "What did Hubble see on your birthday" es un feature de marketing separado del APOD. Se uso el APOD verdadero de esa fecha.
- [x] W1.7 Implementar `AstronomyService` con `getByDate(date)` y signals `selectedDate`, `currentPicture` (computed), `loading`, `error`.
- [x] W1.8 Test unitario de `AstronomyService.getByDate` (caso existente, caso inexistente, caso video).

## Acceptance Criteria

- `npm install` y `npm run build` pasan.
- `ng serve` levanta sin errores (puede mostrar solo el shell por ahora).
- No hay `NgModule` ni `BehaviorSubject` en `src/app`.
- No hay clases Tailwind arbitrarias (`bg-[#...]`, `p-[...]`) en lo que se haya tocado.
- `apod.json` tiene >= 15 entradas, incluye los 3 casos requeridos (Hubble birthday, WR 134, video).

## Verification

```powershell
npm install
npm run build
npm test
ng serve
rg -n "NgModule|BehaviorSubject" src/app
rg -n "bg-\[#|text-\[#|p-\[" src/app
```

## Evidence (2026-06-10)

Entorno: Node v24.14.0, npm 11.13.0, `@angular/cli@19.2.27`, Tailwind v4.3.0,
clsx 2.1.1, tailwind-merge 3.6.0. Branch `wave/p1-w1-scaffold-data-layer`,
commit `feat: scaffold Angular 19 and APOD data layer`.

| Check | Resultado |
|---|---|
| `npm install` | OK |
| `npm run build` | OK, sin warnings; `styles.css` 6.73 kB, bundle inicial 224 kB |
| `npm test -- --watch=false --browsers=ChromeHeadless` | **12/12 SUCCESS** (3 AppComponent + 9 AstronomyService) |
| `ng serve` (`npm start`) | Levanta OK; `/` sirve `app-root`; `/assets/mock/apod.json` → 200, 15 entradas |
| `rg "NgModule\|BehaviorSubject" src` | sin coincidencias |
| `rg "bg-\[#\|text-\[#\|p-\[" src` | solo dentro de un comentario en `styles.css` (no es una clase real) |
| Tokens en CSS de salida | `.bg-space-base`, `.text-display`, `.px-page`, `.py-page`, `.text-content-secondary`, `--color-space-base`, `--text-display` presentes |

`apod.json`: 15 entradas (14 `image` + 1 `video`), todas con URLs `apod.nasa.gov`
verificadas HTTP 200 (incluidas las `hdurl`). Casos requeridos: `2004-01-16`
(*Martian Surface in Perspective*, rover Spirit — APOD real de la fecha, ver
correccion en W1.6), `2026-05-22` (*The Nebulous Realm of WR 134*), y
`2026-05-24` (*A Martian Eclipse: Phobos Crosses the Sun*, `media_type: "video"`,
mp4 self-hosted + `thumbnail_url` de JPL).

### Desviaciones / decisiones

- **Tailwind v4 con `@theme` en `styles.css`** en lugar de `tailwind.config.ts`
  (archivo eliminado). Razon tecnica y de compatibilidad con el builder de Karma
  documentada en ADR-0001 (Implementation Note). Estrategia de tokens intacta.
- **`2004-01-16` no es "Hubble birthday"** (error factual del doc origen);
  se uso el APOD verdadero de esa fecha.
- **Valores de tokens** (paleta dark, escalas) derivados de ADR-0001; pendientes
  de reconciliar contra el sistema de diseno de Figma en P1-W2.
- **Deploy target** (Netlify/Vercel/GitHub Pages) sigue sin decidir; corresponde
  a P1-W3, no bloquea W1.

## Parent Plan Sync

- [x] Actualizar `R1.1`, `R1.2`, `R1.3` en `docs/plans/astronomy-p1-frontend-mock-plan.md`.
- [x] Actualizar estado de `P1` en `docs/plans/astronomy-master-plan.md`.
- [x] Registrar estado final como `DONE` o `BLOCKED`. → **DONE**.

## Post-implementation clarification (2026-07-16)

El mock/provider contract y `availableDates` fueron una base temporal correcta. P3-W10
elimina `service_version`, mock imports y listas de fechas del runtime en favor del DTO
app-owned y consultas calendario reales. La evidencia de esta wave permanece valida.

## Design-change clarification (2026-07-20)

Esta wave conserva como evidencia historica el scaffold Angular 19.2 y su commit. La
rama dedicada `maintenance/angular-22-security-update` actualizo despues el baseline a
Angular 22.0.7 para resolver vulnerabilidades runtime antes de W8. Sus migraciones
obligatorias no alteran el alcance ni los resultados registrados de P1-W1.
