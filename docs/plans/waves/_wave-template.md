# Wave Template - Astronomy Picture Explorer

Date: `<YYYY-MM-DD>`
Status: Draft
Wave ID: `<P?-W?>`
Source Phase: `<P?>`
Source Phase Plan: `docs/plans/<phase-plan>.md`
Suggested Branch: `wave/<p-w-slug>`
Suggested PR Title: `[<P-W>] <title>`

## Goal

`<Una frase concreta de objetivo.>`

## File Scope

- `<paths futuros>`

## Checklist

- [ ] W?.1 `<accion>`
- [ ] W?.2 `<accion>`

## Acceptance Criteria

- Comportamiento observable y testeable.
- Componentes Standalone, estado via Signals (sin `NgModule`/`BehaviorSubject`).
- Tailwind solo con tokens nombrados (sin clases arbitrarias `bg-[#...]`).
- Si toca imagenes/Canvas, contempla CORS y fallback de paleta (ADR-0002).

## Verification

```powershell
npm run build
npm test
ng serve
```

## Parent Plan Sync

- [ ] Actualizar checklist del phase plan (`R?.?`).
- [ ] Mantener `docs/plans/astronomy-master-plan.md` alineado.
- [ ] Registrar estado final como `DONE` o `BLOCKED`.
