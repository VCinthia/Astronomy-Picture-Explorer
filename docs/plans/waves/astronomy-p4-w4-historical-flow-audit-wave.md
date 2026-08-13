# Wave P4-W4 - Aclaraciones históricas y auditoría de flujos

Date: 2026-08-12
Status: PLANNED
Wave ID: `P4-W4`
Source Phase: `P4`
Source Phase Plan: `docs/plans/astronomy-p4-documentation-alignment-plan.md`
Suggested Branch: `wave/p4-w4-historical-flow-audit`
Depends On: P4-W1 + P4-W3 DONE
Unblocks: P4-W5

## Goal

Resolver la deuda documental de P1/P2 y comprobar que los diagramas/narrativas de los
flujos reflejan el producto P3 sin reescribir cronología ya cerrada.

## File scope

- `docs/figma/frames.md`, `docs/figma/pending-mobile-explorer.md` y la referencia P2
  relacionada.
- P1-W3 y otros planes P1/P2 que retengan un pendiente de deploy/visual ya resuelto.
- `docs/architecture/p3-flow-overview.md` y documentos de flujo complementarios cuando la
  auditoría encuentre una contradicción real.

## Checklist

- [ ] W4.1 Identificar cada pendiente P1/P2 que quedó históricamente abierto y añadir una
  aclaración terminal fechada que apunte al documento canónico P3/P4.
- [ ] W4.2 Resolver la contradicción Figma: el pendiente de vector mobile es obsoleto y no
  puede presentarse como requisito activo de la UI publicada.
- [ ] W4.3 Verificar los flujos de visitante, fecha/búsqueda, sesión, confirmación, reset y
  favoritos contra las rutas/contratos actuales; ajustar sólo las narrativas que difieran.
- [ ] W4.4 Conservar los resultados, hashes y fechas de P1/P2 como evidencia histórica;
  no cambiar sus checklists de implementación para simular trabajo retrospectivo.

## Acceptance criteria

- Ningún documento histórico presenta como trabajo pendiente una corrección ya entregada.
- La cronología P1/P2 sigue visible y las aclaraciones dicen cuándo/cómo cambió el diseño.
- Los flujos citan la autoridad P3/ADR-0003 actual y no contradicen el comportamiento de
  la aplicación publicada.

## Verification

```powershell
rg -n -i "pending|pendiente|obsoleto|deploy.*pending|availableDates" docs/figma docs/plans
rg -n "forgot-password|reset-password|confirm-email|favorites" docs/architecture docs/plans
```

Revisar manualmente los matches para distinguir una aclaración histórica correcta de un
pendiente realmente activo.

## Parent plan sync

- [ ] Marcar R4.4 DONE sin modificar evidencia histórica previa a P4.
