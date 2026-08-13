# Wave P4-W3 - Alineación técnica, operativa e histórica

Date: 2026-08-12
Status: DONE - contracts, runbooks and historical clarification aligned on 2026-08-12
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

- [x] W3.1 Alinear backend README con endpoints/rutas actuales, incluyendo confirmación,
  forgot/reset password, favoritos y catálogo; eliminar promesas de trabajo ya cerrado.
- [x] W3.2 Corregir runbooks/planes/flow P3 para reflejar release final, mantenimiento
  posterior y responsabilidades actuales; sustituir URLs de instancia por placeholders.
- [x] W3.3 Resolver contradicciones de límites edge: documentar las dos políticas globales
  existentes sin inventar reglas específicas para reset, y mantener el detalle sensible
  fuera del README público.
- [x] W3.4 Revisar los flujos P3 de APOD, fecha/búsqueda, cuenta, sesión, favoritos,
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

## Completion record (2026-08-12)

- Recovery aparece ahora en el README técnico y coincide con las rutas backend/frontend y
  sus pruebas de contrato.
- Los runbooks de deploy y hosting conservan hechos y resultados del release sin URLs
  directas, nombres/valores de configuración, presupuestos de rate limit ni instrucciones
  de probing.
- Flow, ADR, PRD, readiness y plan P3 distinguen el orden histórico de W14/W15 del estado
  final P3 `DONE`/`main`.
- La aclaración terminal de P3-W15 resuelve la redacción prospectiva: el contrato final
  usa los dos grupos globales `/auth/*` y `/api/*`, no una regla edge individual de reset.

## Parent plan sync

- [x] R4.3 DONE; P4-W4 recibe sólo las aclaraciones históricas P1/P2 y su revisión de
  flujos documentados.
