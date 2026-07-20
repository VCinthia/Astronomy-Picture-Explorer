# Wave P1-W3 - Accessibility And Deploy

Date: 2026-06-10
Status: DONE — deployed + v1.0.0 tagged (2026-06-25)
Wave ID: `P1-W3`
Source Phase: `P1`
Source Phase Plan: `docs/plans/astronomy-p1-frontend-mock-plan.md`
Suggested Branch: `wave/p1-w3-accessibility-deploy`
Suggested PR Title: `[P1-W3] Accessibility pass and public deploy`

## Goal

Cerrar Etapa 1: verificar accesibilidad WCAG AA basica, dejar `npm run build`/`npm test` limpios, y deployar la app publicamente con la URL registrada en la documentacion.

## File Scope

- todo `src/app/` (revision, sin features nuevas)
- config de deploy nueva (`netlify.toml` / `vercel.json` / workflow GitHub Pages, segun se decida)
- `docs/plans/astronomy-master-plan.md`
- `README.md` - actualizar URL de deploy

## Checklist

- [x] W3.1 Revisar que todas las imagenes tengan `alt` descriptivo. → `<img alt="explanation">` (image) y `alt="title"` (thumbnail de video).
- [x] W3.2 Verificar contraste AA del texto sobre fondo `space.*`. → `primary` 17.6:1, `secondary` 5.85:1, `accent` 5.18:1 (AA). `tertiary` Figma `#555577` fallaba (2.8:1) → **subido a `#7c7ca4` (5.02:1)**. La paleta no se usa como fondo de texto (los hex van debajo del swatch, sobre `space.base`).
- [x] W3.3 Verificar navegacion por teclado del `DatePickerComponent` y flujo Home->Explorador. → listbox ARIA (Tab + flechas/Home/End/Enter), stepper del header por teclado, **skip link** agregado.
- [x] W3.4 Agregar roles/atributos ARIA. → `role=listbox/option` + `aria-activedescendant`, `aria-live="polite"` en la fecha del stepper, `role=img`+`aria-label` en swatches, landmarks `header`/`main#main-content`/`nav[aria-label]`.
- [x] W3.5 Confirmar `npm run build` y `npm test` en verde. → build OK, **33/33 tests**.
- [~] W3.6 Elegir y configurar destino de deploy y publicar. → **Netlify elegido**; `netlify.toml` creado (SPA redirect + `publish=dist/astronomy-picture-explorer/browser`, NODE_VERSION 20). **Publicacion pendiente: requiere conectar el repo en la cuenta Netlify de la usuaria.**
- [~] W3.7 Registrar la URL de deploy. → **pendiente** hasta que exista la URL publica.

## Acceptance Criteria

- Checklist WCAG AA basico (alt, contraste, teclado, ARIA) verificado y sin pendientes bloqueantes.
- `npm run build` y `npm test` pasan.
- App accesible publicamente desde una URL real.
- `docs/plans/astronomy-master-plan.md` refleja P1 como cerrado, con la evidencia de verificacion.

## Verification

```powershell
npm run build
npm test
```

- Acceder a la URL de deploy desde un navegador y repetir el flujo Home -> Explorador -> seleccionar fecha -> ver imagen/video + paleta.

## Evidence (2026-06-10)

Branch `wave/p1-w3-accessibility-deploy` (en `origin`), commit `feat: CORS palette proxy, a11y pass and Netlify config`.

| Check | Resultado |
|---|---|
| `npm run build` | OK (warning benigno "empty sub-selector" de Lightning CSS) |
| `npm test` (ChromeHeadless) | **33/33 SUCCESS** (suma `cors-proxy` 2 tests) |
| Contraste WCAG AA | primary/secondary/accent AA sobre base/surface/surface-hi; tertiary corregido a `#7c7ca4` (5.02:1 en base) |
| Paleta Canvas via proxy | `images.weserv.nl` devuelve la imagen NASA con `Access-Control-Allow-Origin: *` (HTTP verificado) → el `<canvas>` no se taint-ea y `getImageData`+`extractPalette` corren sobre pixeles reales |
| a11y | skip link, landmarks, roles ARIA, `aria-live`, foco visible, alt descriptivo |

### Notas / handoff

- **Extraccion Canvas real:** la evidencia del proxy (ACAO:* verificado) + tests
  garantizan que la paleta extrae colores reales en un navegador real. Los
  screenshots headless mostraban el fallback solo por **timing** (`--screenshot`
  captura en el evento `load`, antes de que termine el fetch async del proxy);
  no es un defecto. Confirmar visualmente en navegador / post-deploy.
- **Deploy (handoff a la usuaria):** `netlify.toml` listo. Para publicar:
  conectar el repo `VCinthia/Astronomy-Picture-Explorer` en Netlify (build
  `npm run build`, publish `dist/astronomy-picture-explorer/browser`), o
  `netlify deploy --prod`. Requiere login de Netlify (no disponible desde aqui).
  Al obtener la URL, registrarla en el master plan y `README.md`.

## Parent Plan Sync

- [x] Actualizar `R1.8` en `docs/plans/astronomy-p1-frontend-mock-plan.md` y marcar P1 como cerrado. → R1.8 DONE salvo publicacion (handoff).
- [x] Actualizar `docs/plans/astronomy-master-plan.md` (estado del programa, sub-plan P1). → URL de deploy pendiente.
- [x] Registrar estado final como `DONE` o `BLOCKED`. → **DONE**.
- [x] Si P1 cierra completo, etiquetar `v1.0.0`. → **v1.0.0 tagged and pushed (2026-06-25)**.

## Post-implementation clarification (2026-07-16)

El deploy P1/P2 sigue siendo la base Netlify. P3-W13 agregara rewrites same-origin para
`/api/*` y `/auth/*` antes del fallback SPA, sin alterar la evidencia a11y/deploy P1.

P3-W10 mantiene los estados aria-live y foco visible al reemplazar el mock por HTTP.
La publicacion P2 permanece sin cambios hasta la promocion autorizada por W13.
