# Wave P5-W3 - UX Angular para calendario APOD

Date: 2026-08-13
Status: PLANNED
Wave ID: `P5-W3`
Source Phase: `P5`
Source Phase Plan: `docs/plans/astronomy-p5-apod-calendar-plan.md`
Suggested Branch: `wave/p5-w3-angular-calendar-ux`
Depends On: P5-W2 DONE
Unblocks: P5-W4

## Goal

Eliminar el máximo UTC adelantado en Angular sin transformar al navegador en autoridad de
negocio ni alterar los estados honestos de error/Retry.

## File scope

- `src/app/services/astronomy.service.ts` y pruebas.
- Picker de fecha, Home/stepper y sus pruebas.
- Sin rutas nuevas, storage, configuración Netlify ni cambio de contrato HTTP.

## Checklist

- [ ] W3.1 Sustituir el helper UTC por uno `Intl.DateTimeFormat` explícito para
  `America/Argentina/Buenos_Aires`, testeable con un `Date` inyectado.
- [ ] W3.2 Aplicarlo a fecha inicial, validación previa y máximo del date picker.
- [ ] W3.3 Aplicarlo a la habilitación del botón siguiente de Home; la fecha devuelta por
  API debe seguir actualizando el estado mostrado.
- [ ] W3.4 Cubrir antes/después de `03:00Z`, sin depender del timezone de Chrome/CI.
- [ ] W3.5 Mantener Retry para una demora o caída real de NASA.

## Acceptance criteria

- A las 22:00 ART, el picker y Home no exponen ni solicitan el día siguiente.
- El cliente no usa `toISOString` ni el timezone local del navegador para el calendario
  APOD.
- Una API modificada o un cliente antiguo no puede evadir la validación de W1.

## Verification

```powershell
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```
