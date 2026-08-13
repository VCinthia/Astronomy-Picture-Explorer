# Wave P4-W3 - Alineación técnica, operativa e histórica

Date: 2026-08-12
Status: PLANNED
Wave ID: `P4-W3`
Source Phase: `P4`
Source Phase Plan: `docs/plans/astronomy-p4-documentation-alignment-plan.md`
Suggested Branch: `wave/p4-w3-technical-documentation`
Depends On: P4-W1 DONE
Unblocks: P4-W4

## Goal

Sincronizar los contratos técnicos y la evidencia histórica con P3 final, preservando
trazabilidad y los límites de publicación de ADR-0004.

## File scope

- `backend/README.md`, `docs/deploy/`, `docs/architecture/`, PRD, readiness, ADRs y
  phase/wave plans P3 que W1 haya inventariado.
- `netlify.toml` sólo como fuente para documentar comportamiento existente; no cambiar
  configuración ni proveedores en esta wave.

## Checklist

- [ ] W3.1 Alinear backend README con endpoints/rutas actuales, incluyendo confirmación,
  forgot/reset password, favoritos y catálogo; eliminar promesas de trabajo ya cerrado.
- [ ] W3.2 Corregir runbooks/planes/flow P3 para reflejar release final, mantenimiento
  posterior y responsabilidades actuales; sustituir URLs de instancia por placeholders.
- [ ] W3.3 Resolver contradicciones de límites edge: documentar las dos políticas globales
  existentes sin inventar reglas específicas para reset, y mantener el detalle sensible
  fuera del README público.
- [ ] W3.4 Revisar los flujos P3 de APOD, fecha/búsqueda, cuenta, sesión, favoritos,
  confirmación y reset contra rutas/contratos/pruebas; corregir diferencias y registrar
  explícitamente los no-goals.

## Acceptance criteria

- README técnico y documentos P3 describen el mismo contrato de producto.
- Password reset aparece donde corresponde y no se describe como futuro/deferred.
- No quedan instrucciones de desplegar `codex/p3-integration` ni de ejecutar waves P3
  restantes después del cierre confirmado W1.
- La explicación de proxy/seguridad es suficiente para arquitectura, sin datos que
  permitan apuntar a la instancia o recrear sus secretos.
- Las actualizaciones de P3 se limitan a su estado/contrato actual; las deudas P1/P2
  permanecen a cargo de P4-W4.

## Verification

```powershell
rg -n -i "ready for promotion|cutover pending|integration review pending|waves restantes|deferred" docs backend/README.md
rg -n "forgot-password|reset-password" backend README.md docs
npm run build
dotnet test backend/AstronomyExplorer.sln --no-build
```

Los resultados del primer comando se revisan: una nota histórica que explica el pasado
puede permanecer; un estado actual contradictorio no.

## Parent plan sync

- [ ] Marcar R4.3 DONE y pasar a P4-W4 sólo referencias históricas concretas.
