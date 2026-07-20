# Phase Plan P1 - Frontend Con Mock

Date: 2026-06-10
Status: DONE — v1.0.0 tagged (2026-06-25), 36/36 tests, deployed at https://astronomy-picture-explorer.netlify.app/
Phase: `P1`
Source master plan: `docs/plans/astronomy-master-plan.md`

## 1. Goal

Implementar Etapa 1 completa: app Angular 19 Standalone + Signals + Tailwind que muestra la imagen del dia y permite explorar el archivo APOD por fecha, con paleta de colores generada en cliente, leyendo de un mock JSON que replica el contrato real de NASA, deployada publicamente antes del 11 Jun 2026.

## 2. Scope

### Included

- Scaffold Angular 19 Standalone + Tailwind CSS.
- Tipos `ApodEntry` / `ApodMock` y `src/assets/mock/apod.json` (>= 15 entradas, incluye `2004-01-16`, `2026-05-22` y >= 1 `media_type: "video"`).
- `AstronomyService` con `getByDate(date)` y signals `selectedDate`, `currentPicture` (computed), `loading`, `error`.
- `PictureCardComponent`: imagen o video segun `media_type`, titulo, fecha, descripcion (truncada en mobile), copyright opcional, paleta embebida.
- `ColorPaletteComponent`: N colores dominantes via Canvas API, fallback si CORS falla (ADR-0002).
- `DatePickerComponent`: selector de fecha navegable por teclado, validado contra fechas presentes en el mock.
- `AppComponent`: layout base + routing (`provideRouter`, lazy loading) para Home y Explorador.
- Verificacion de accesibilidad WCAG AA basica (alt, contraste, teclado, ARIA).
- Deploy publico (Netlify, Vercel o GitHub Pages).

### Excluded

- Favoritos (P2).
- Busqueda por keyword (P2).
- Backend, autenticacion, base de datos (P3).

## 3. Dependencies and Gates

- Depends on: `docs/adr/0001-angular-standalone-signals-tailwind.md`, `docs/adr/0002-canvas-color-palette-extraction.md` (ambos `Accepted`).
- Gate to open phase: ninguno (es la primera fase).
- Gate to close phase: deploy publico accesible + checklist WCAG AA basico verificado + `npm run build` y `npm test` pasan.

## 4. Checklist

- [x] R1.1 Scaffold Angular 19 Standalone + Tailwind CSS  (P1-W1, DONE — Tailwind **v4.3.0**; tokens en `@theme` de `src/styles.css`, ver ADR-0001 Implementation Note)
	Files:
	- `package.json`
	- `angular.json`
	- `tailwind.config.ts`
	- `src/styles.css` (o `global.css`)
	- `src/app/app.config.ts`
	- `src/utils/cn.ts`
	Acceptance criteria:
	- Proyecto generado sin `NgModule`; `app.config.ts` usa `provideRouter()`.
	- Tailwind configurado con los tokens del brief de diseno: colores `space.*`/`content.*`/`accent`, `fontFamily.sans: Inter`, `fontSize` (`display/title/body/caption`), `spacing.page/card`, `borderRadius.card/chip/swatch`.
	- Si el CLI genera Tailwind v3, se documenta el ajuste de sintaxis (`@tailwind base/components/utilities` vs `@import "tailwindcss"`) sin perder la estrategia de tokens.
	- `cn()` (clsx + tailwind-merge) disponible para clases condicionales.
	Verification:
	- `npm install`
	- `npm run build`
	- `ng serve` levanta sin errores

- [x] R1.2 Modelos y mock JSON  (P1-W1, DONE — 15 entradas, URLs NASA verificadas 200; `2004-01-16` = APOD real *Martian Surface in Perspective*, no "Hubble birthday")
	Files:
	- `src/app/models/apod.model.ts`
	- `src/assets/mock/apod.json`
	Acceptance criteria:
	- `ApodEntry` y `ApodMock` (`Record<string, ApodEntry>`) coinciden con el contrato documentado abajo.
	- `apod.json` tiene >= 15 entradas, indexadas por `date` (`YYYY-MM-DD`).
	- Incluye `2004-01-16` (Hubble birthday, URL verificada contra la fuente oficial) y `2026-05-22` (WR 134 Nebula).
	- Incluye al menos 1 entrada con `media_type: "video"` y `thumbnail_url`.
	Verification:
	- `npm run build` (type-check de `apod.model.ts` contra `apod.json`)
	- Conteo manual de entradas en `apod.json`

- [x] R1.3 AstronomyService + signals base  (P1-W1, DONE — `getByDate` O(1) sobre mock importado; 12/12 tests)
	Files:
	- `src/app/services/astronomy.service.ts`
	Acceptance criteria:
	- `getByDate(date: string): ApodEntry | undefined` hace lookup directo sobre el objeto mock (O(1)).
	- Signals expuestos/consumidos: `selectedDate` (signal), `currentPicture` (computed desde `selectedDate` + servicio), `loading`, `error`.
	Verification:
	- `npm test -- astronomy.service`

- [x] R1.4 PictureCardComponent  (P1-W2, DONE — image+video, sin iframe, paleta embebida)
	Files:
	- `src/app/components/picture-card/picture-card.component.ts`
	- `src/app/components/picture-card/picture-card.component.html`
	Acceptance criteria:
	- Si `media_type === 'image'`: renderiza `<img [src]="url" [alt]="explanation">`, link opcional a `hdurl`.
	- Si `media_type === 'video'`: renderiza thumbnail (`thumbnail_url`) + link al video (`url`), sin `<iframe>`.
	- Muestra `title`, `date`, `explanation` (truncada en mobile via Tailwind `line-clamp` o equivalente), `copyright` si existe.
	- Embebe `ColorPaletteComponent` con la imagen activa.
	Verification:
	- `npm test -- picture-card`
	- Revision visual en `ng serve` para una entrada `image` y una `video`

- [x] R1.5 ColorPaletteComponent  (P1-W2, DONE — Canvas crossOrigin + fallback; nota: apod.nasa.gov sin CORS -> fallback siempre, decision de proxy en W3)
	Files:
	- `src/app/components/color-palette/color-palette.component.ts`
	- `src/app/utils/palette/extract-palette.ts`
	Acceptance criteria:
	- Funcion pura `extractPalette(imageData, n)` cuantiza y devuelve N colores dominantes; testeable sin DOM completo.
	- Componente dibuja la imagen en `<canvas>`/`OffscreenCanvas` con `crossOrigin="anonymous"` y usa `extractPalette`.
	- Si `getImageData` falla (CORS/`SecurityError`) o la imagen no carga, se usa la paleta de fallback documentada en ADR-0002.
	- Contraste de texto sobre cada color de la paleta verificado (WCAG AA) antes de usarse como fondo de texto.
	Verification:
	- `npm test -- extract-palette` (con `ImageData` fixture)
	- Revision manual: forzar el caso de fallback con una imagen sin `crossOrigin` permitido

- [x] R1.6 DatePickerComponent + Explorador por fecha  (P1-W2, DONE — listbox ARIA por teclado; /explorer lazy)
	Files:
	- `src/app/components/date-picker/date-picker.component.ts`
	- `src/app/pages/explorer/explorer.component.ts`
	- `src/app/app.routes.ts`
	Acceptance criteria:
	- `DatePickerComponent` solo permite seleccionar fechas presentes en `apod.json` (deshabilita o filtra el resto).
	- Operable por teclado: foco visible, navegacion con flechas/`Enter` segun patron ARIA de date picker.
	- Seleccionar una fecha actualiza la signal `selectedDate` y la `ExplorerComponent` muestra el `PictureCardComponent` correspondiente.
	- Ruta `/explorer` cargada con lazy loading via `app.routes.ts`.
	Verification:
	- `npm test -- date-picker`
	- Navegacion manual por teclado en `ng serve`

- [x] R1.7 AppComponent layout + Home  (P1-W2, DONE — header+stepper, Home muestra entrada del dia/ultima)
	Files:
	- `src/app/app.component.ts`
	- `src/app/app.component.html`
	- `src/app/pages/home/home.component.ts`
	Acceptance criteria:
	- `AppComponent` define layout base (header/nav simple, `<router-outlet>`).
	- `HomeComponent` (ruta `/`) muestra `PictureCardComponent` para la fecha "del dia" o, si no existe en el mock, la entrada mas reciente del mock.
	- Layout usa solo tokens Tailwind (sin clases arbitrarias).
	Verification:
	- `npm run build`
	- `ng serve` y revision visual de `/` y `/explorer`

- [x] R1.8 Accesibilidad, verificacion final y deploy  (P1-W3 DONE — a11y AA + Canvas proxy + Netlify deployed + v1.0.0 tagged)
	Files:
	- todo lo anterior
	- config de deploy (`netlify.toml`, `vercel.json` o GitHub Pages workflow, segun se decida)
	Acceptance criteria:
	- Checklist WCAG AA basico: todas las imagenes tienen `alt` descriptivo; contraste de texto sobre paleta y sobre fondo `space.*` cumple AA; `DatePickerComponent` totalmente operable por teclado; roles ARIA presentes donde el HTML semantico no alcanza.
	- `npm run build` produce build de produccion sin errores.
	- App deployada y accesible publicamente; URL registrada en `README.md` y `docs/plans/astronomy-master-plan.md`.
	Verification:
	- `npm run build`
	- `npm test`
	- Revision manual de accesibilidad (alt, contraste, teclado, ARIA)
	- Acceder a la URL de deploy desde un navegador

## 5. Exit Criteria

- App Angular Standalone+Signals+Tailwind funcional con Home + Explorador por fecha.
- `apod.json` con >= 15 entradas, incluyendo el caso `media_type: "video"`.
- Paleta de colores generada en cliente via Canvas, con fallback documentado.
- Checklist WCAG AA basico verificado.
- `npm run build` y `npm test` pasan.
- App deployada publicamente con URL registrada en la documentacion.

## 6. Wave Split

- `docs/plans/waves/astronomy-p1-w1-scaffold-data-layer-wave.md` (R1.1, R1.2, R1.3)
- `docs/plans/waves/astronomy-p1-w2-ui-components-wave.md` (R1.4, R1.5, R1.6, R1.7)
- `docs/plans/waves/astronomy-p1-w3-accessibility-deploy-wave.md` (R1.8)

## 7. Post-implementation design clarification (2026-07-16)

P1 permanece DONE y no se altera retrospectivamente. Para P3 se aclara:

- El contrato mock incluyo `service_version` por fidelidad al proveedor; P3 migra a un
  DTO app-owned que lo elimina y normaliza `hdurl`, `thumbnail_url` y `copyright` a null.
- `availableDates`, listbox/chips y stepper por indice fueron correctos para el mock
  acotado. P3 los elimina y consulta una fecha calendario real.
- `apod.json` puede conservarse como fixture/historia, pero deja de importarse en runtime.
- Estas sustituciones pertenecen a P3-W10 y no invalidan acceptance/evidence de P1.

## 8. Design-change clarification (2026-07-20)

El scaffold y la evidencia P1 se conservan como registro historico de Angular 19.2. Una
rama de mantenimiento posterior actualizo el baseline a Angular 22.0.7, de forma
secuencial y sin `npm audit fix --force`, para cerrar vulnerabilidades runtime antes de
W8. No altera los entregables, tests ni commits evidenciados para P1.
