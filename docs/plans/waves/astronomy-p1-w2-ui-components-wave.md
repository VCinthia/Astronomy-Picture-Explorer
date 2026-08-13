# Wave P1-W2 - UI Components

Date: 2026-06-10
Status: DONE
Wave ID: `P1-W2`
Source Phase: `P1`
Source Phase Plan: `docs/plans/astronomy-p1-frontend-mock-plan.md`
Suggested Branch: `wave/p1-w2-ui-components`
Suggested PR Title: `[P1-W2] Build picture card, palette, date picker and pages`

## Goal

Construir los componentes visuales (`PictureCardComponent`, `ColorPaletteComponent`, `DatePickerComponent`), las paginas Home y Explorador, y conectarlos via routing y `AstronomyService`/Signals de P1-W1.

## File Scope

- `src/app/components/picture-card/`
- `src/app/components/color-palette/`
- `src/app/utils/palette/extract-palette.ts`
- `src/app/components/date-picker/`
- `src/app/pages/home/`
- `src/app/pages/explorer/`
- `src/app/app.component.ts`, `src/app/app.component.html`
- `src/app/app.routes.ts`

## Checklist

- [x] W2.1 Implementar `extractPalette(imageData, n)` (funcion pura, cuantizacion simple) + test con `ImageData` fixture. → quantizacion por buckets 4-bit/canal; 7 tests.
- [x] W2.2 Implementar `ColorPaletteComponent`: dibuja imagen en canvas con `crossOrigin="anonymous"`, usa `extractPalette`, aplica fallback ante CORS/`SecurityError`. → fallback de tokens marca; **verificado en runtime que apod.nasa.gov no envia CORS y se activa el fallback** (ver Hallazgos).
- [x] W2.3 Implementar `PictureCardComponent`: rama `image` (`<img alt="explanation">` + link `hdurl`) y rama `video` (thumbnail + link, **sin `<iframe>`**), `title`/`date`/`explanation` truncada en mobile (`line-clamp-4`), `copyright` opcional, embebe `ColorPaletteComponent`.
- [x] W2.4 Implementar `DatePickerComponent`: solo fechas presentes en `apod.json`, navegable por teclado (listbox ARIA + `aria-activedescendant`, flechas/Home/End/Enter), actualiza `selectedDate`.
- [x] W2.5 Implementar `HomeComponent` (ruta `/`) y `ExplorerComponent` (ruta `/explorer`, **lazy loaded** via `loadComponent`).
- [x] W2.6 Implementar `AppComponent` (header con logo + stepper de fechas ←/→, `<router-outlet>`) usando solo tokens Tailwind.
- [x] W2.7 Tests de `PictureCardComponent` (caso image y caso video) y `DatePickerComponent`.

## Acceptance Criteria

- `/` muestra `PictureCardComponent` para la entrada "del dia" o la mas reciente del mock si no hay match.
- `/explorer` permite elegir cualquier fecha del mock via `DatePickerComponent` y actualiza la card mostrada.
- El caso `media_type: "video"` se renderiza sin `<iframe>`.
- `ColorPaletteComponent` muestra colores derivados de la imagen activa o el fallback documentado.
- `DatePickerComponent` es operable solo con teclado (Tab + flechas/Enter).
- Sin `NgModule`, `BehaviorSubject` ni clases Tailwind arbitrarias en lo tocado.

## Verification

```powershell
npm run build
npm test
ng serve
rg -n "NgModule|BehaviorSubject" src/app
rg -n "bg-\[#|text-\[#|p-\[" src/app
rg -n "<iframe" src/app
```

## Evidence (2026-06-10)

Branch `wave/p1-w2-ui-components`, commit `feat: build UI components, pages and date nav`.

| Check | Resultado |
|---|---|
| `npm run build` | OK; lazy chunks `home-component` (~0.8 kB) y `explorer-component` (~31 kB) confirman lazy loading. Warning benigno de Lightning CSS "empty sub-selector" (reglas vacias descartadas; sin perdida de estilos, verificado por screenshot). |
| `npm test` (ChromeHeadless) | **31/31 SUCCESS** (extractPalette 7, PictureCard 6, DatePicker 6, AstronomyService 9, AppComponent 3). |
| `ng serve` + screenshots | `/` y `/explorer` en desktop 1440 y mobile 390 renderizan fieles al Figma (header+stepper, hero, paleta, chip/titulo/descripcion/credito, fade inferior; listbox de fechas en Explorer). |
| `rg "NgModule\|BehaviorSubject" src` | sin coincidencias |
| `rg "bg-\[#\|...\|tracking-\[" src` | solo el comentario en `styles.css` (sin clases arbitrarias reales) |
| `rg "<iframe" src` | sin coincidencias (video usa thumbnail + link) |

Tokens reconciliados 1:1 con Figma (`#08080f`/`#11111c`/`#191927`/`#1e1e30`, texto `#f0f0f5`/`#8888aa`/`#555577`, **acento `#4d78ff`**, Inter, escalas 40/26/17/15/13/11/10/9, radios 10/8/6/4).

### Hallazgos / decisiones

- **CORS de las imagenes APOD (importante para el demo):** `apod.nasa.gov` no envia
  `Access-Control-Allow-Origin`, por lo que `getImageData` lanza `SecurityError`
  con `crossOrigin="anonymous"` y la paleta **siempre cae al fallback** en runtime
  (confirmado por screenshot: swatches = `#191927 #1E1E30 #4D78FF #8888AA #555577`).
  El fallback cumple el criterio de R1.5, pero la extraccion Canvas real (una
  funcionalidad distintiva del portfolio) no se ve. Opciones para P1-W3: (a) proxy de imagen con CORS
  (p.ej. `images.weserv.nl`) solo para el muestreo de paleta; (b) bundlear algunas
  imagenes en `assets` (same-origin); (c) aceptar el fallback y documentarlo.
  **Recomendado:** (a). Decision pendiente de la usuaria.
- **DatePicker:** patron listbox + `aria-activedescendant` (un solo Tab stop), no
  calendario grid, por mock disperso de ~15 fechas (decidido con la usuaria).
- **Header date stepper** (←/→) global en `AppComponent`, ademas del listbox del
  Explorer; ambos manejan `selectedDate`.

## Parent Plan Sync

- [x] Actualizar `R1.4`, `R1.5`, `R1.6`, `R1.7` en `docs/plans/astronomy-p1-frontend-mock-plan.md`.
- [x] Actualizar estado de `P1` en `docs/plans/astronomy-master-plan.md`.
- [x] Registrar estado final como `DONE` o `BLOCKED`. → **DONE**.

## Post-implementation clarification (2026-07-16)

El date picker/listbox y stepper indexado se diseñaron para un archivo mock acotado.

P3-W10 conserva layout/accesibilidad, reemplaza chips por `input[type=date]` y hace
prev/next por dia calendario contra backend.

P3-W10 se implemento con limites UTC `1995-06-16..hoy`, stepper de dias UTC y estados
accesibles de loading/error/retry. El patron listbox original queda como evidencia P1,
no como contrato activo de la rama P3.

## Aclaración terminal P4-W4 - decisión histórica de paleta (2026-08-12)

La recomendación de proxy para la extracción Canvas era una decisión abierta de este
momento histórico; P1-W3 documentó su resolución y las pruebas correspondientes. P3
reemplazó después el origen mock de imágenes. Esta nota conserva el hallazgo original,
pero evita interpretarlo como un pendiente de la UI o del despliegue actual.
