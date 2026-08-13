# Wave P4-W5 - Auditoría y promoción documental

Date: 2026-08-12
Status: PLANNED
Wave ID: `P4-W5`
Source Phase: `P4`
Source Phase Plan: `docs/plans/astronomy-p4-documentation-alignment-plan.md`
Suggested Branch: `wave/p4-w5-documentation-assurance`
Depends On: P4-W2 + P4-W3 + P4-W4 DONE
Unblocks: P4 promotion to `main`

## Goal

Corroborar que la documentación integrada es pública, coherente y verificable antes de
promover el conjunto completo a `main`.

## Checklist

- [ ] W5.1 Revisar diff, enlaces Markdown, capturas y clasificación ADR-0004.
- [ ] W5.2 Ejecutar scan de secretos/datos personales/orígenes internos y revisar falsos
  positivos sin imprimir valores.
- [ ] W5.3 Ejecutar build/test/documentation commands aplicables y smoke público same-origin.
- [ ] W5.4 Verificar que todos los estados P3/P4, checklist R4 y wave plans coinciden.
- [ ] W5.5 Commit final, revisión del orquestador, fast-forward P4 a `main` y revalidación
  pública posterior a la promoción.

## Acceptance criteria

- El repositorio público no contiene secretos, credenciales, connection strings, datos de
  prueba o instrucciones de acceso a infraestructura.
- Los docs y README no contradicen el estado de la aplicación publicada.
- Los gates técnicos pasan o cualquier excepción está explícita, aceptada y no se oculta.
- La promoción no mezcla cambios de aplicación/proveedor ajenos a P4.

## Verification

```powershell
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
dotnet build backend/AstronomyExplorer.sln --no-restore
dotnet test backend/AstronomyExplorer.sln --no-build
docker compose config
git grep -n -I -E "(postgres(ql)?://|password=|BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY|xox[baprs]-|ghp_)" -- . ":!.env.example"
```

Ejecutar también el smoke público autorizado de W1 y revisar manualmente que el resultado
del scan no expone ni omite contexto sensible en la evidencia.

## Parent plan sync

- [ ] Marcar R4.5 y P4 DONE después de promoción/revalidación.
