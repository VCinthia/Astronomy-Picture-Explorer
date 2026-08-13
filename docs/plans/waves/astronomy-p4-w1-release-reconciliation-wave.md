# Wave P4-W1 - Reconciliación de release e inventario documental

Date: 2026-08-12
Status: PLANNED
Wave ID: `P4-W1`
Source Phase: `P4`
Source Phase Plan: `docs/plans/astronomy-p4-documentation-alignment-plan.md`
Suggested Branch: `wave/p4-w1-release-reconciliation`
Depends On: P3-W14 and P3-W15 evidence PASS; `main` fast-forwarded to `48ac901` or successor
Unblocks: P4-W2 and P4-W3

## Goal

Cerrar administrativamente P3 sólo con evidencia post-cutover y producir el inventario que
delimita qué documentación requiere actualización, aclaración histórica o saneamiento.

## File scope

- Estados P3 canónicos: PRD, readiness, master, P3 phase, ADR-0003, flow overview, W14,
  W15 y runbook de deploy.
- `docs/plans/astronomy-p4-documentation-alignment-plan.md` y documentos P4 relacionados.
- Inventario de P4, incorporado al phase plan o como anexo trazable.

## Checklist

- [ ] W1.1 Confirmar en Netlify que la production branch es `main` y que su deploy usa el
  commit P3 promocionado; confirmar Render en `main`/mismo commit o sucesor compatible.
- [ ] W1.2 Repetir después del cutover el smoke público same-origin de catálogo y la sonda
  de salud autorizada; registrar sólo fecha, commit y resultado sanitizado.
- [ ] W1.3 Marcar P3/W14/W15 como `DONE` en todos los documentos canónicos, únicamente si
  W1.1 y W1.2 pasan; conservar aclaraciones históricas con una nota terminal P4.
- [ ] W1.4 Clasificar cada documento relevante como público, técnico, operativo saneado o
  local/secreto, y crear la lista exacta de correcciones W2/W3.
- [ ] W1.5 Verificar que `.env`, `.secrets/` e `IMPLEMENTATION_NOTES.md` siguen fuera de
  Git; no añadir `docs/` al ignore.

## Acceptance criteria

- Ningún documento afirma P3 `DONE` sin evidencia de Netlify `main` y smoke posterior.
- Ningún documento canónico mantiene el estado contradictorio “ready for promotion” una
  vez que el gate pasó.
- El inventario enumera README/captura obsoleta, backend README, planes/runbooks P3 y las
  deudas históricas P1/P2 encontradas, con dueño W2 o W3.
- La evidencia no contiene secretos, datos personales, URLs de administración ni enlaces
  de correo.

## Verification

```powershell
git log --oneline main -1
git check-ignore -v .env .secrets IMPLEMENTATION_NOTES.md
git ls-files .env .secrets docs
```

Completar además la verificación manual de proveedor y el smoke descritos en el runbook
saneado; no copiar su configuración a la wave.

## Parent plan sync

- [ ] Marcar R4.1 y P3 como DONE sólo tras la evidencia W1.
- [ ] Actualizar master, PRD, readiness, ADR-0003, P3 flow/plan y W14/W15 de forma atómica.
