# Wave P4-W2 - README público y captura actual

Date: 2026-08-12
Status: PLANNED
Wave ID: `P4-W2`
Source Phase: `P4`
Source Phase Plan: `docs/plans/astronomy-p4-documentation-alignment-plan.md`
Suggested Branch: `wave/p4-w2-public-readme`
Depends On: P4-W1 DONE
Unblocks: P4-W4

## Goal

Ofrecer un README de portfolio que describa correctamente la aplicación desplegada y una
captura pública actual, sin convertir el repositorio en documentación de operación sensible.

## File scope

- `README.md`
- `screenshots/home.png` o su reemplazo/remoción justificada
- Referencias públicas mínimas hacia ADR/guías locales que sobrevivan la clasificación W1

## Checklist

- [ ] W2.1 Reescribir introducción, funcionalidades y estado de release desde la experiencia
  real: APOD, fecha, FTS, palette, cuentas, favoritos, confirmación y recuperación.
- [ ] W2.2 Documentar Angular, .NET, PostgreSQL, NASA APOD, Docker y proveedores sólo a
  nivel de arquitectura, con flujo navegador -> app -> API -> datos/correo sin orígenes
  internos ni configuración sensible.
- [ ] W2.3 Incluir setup local seguro que derive variables de `.env.example`/dashboards
  propios sin valores reales, y describir claramente que los servicios gratuitos pueden
  tener cold start.
- [ ] W2.4 Reemplazar la captura P1 obsoleta por una captura anónima del UI actual o
  retirarla si no puede verificarse visualmente; nunca mostrar sesión, correo o proveedor.
- [ ] W2.5 Corregir referencias a Figma para que no prometan fidelidad no verificada y
  comprobar enlaces/Markdown.

## Acceptance criteria

- Un visitante entiende qué puede probar en la aplicación y qué tecnología demuestra.
- El README no menciona `codex/p3-integration` como producción, mock runtime, header
  stepper global ni estado P2 como release actual.
- No hay origen directo de API, connection string, nombre/valor de secreto, límite exacto,
  claim, correo, token, código ni enlace efímero.
- La captura corresponde a la navegación actual o no se incluye ninguna engañosa.

## Verification

```powershell
npm run build
rg -n -i "(connection ?string|api[_ -]?key|secret|token=|password=|@.*\\.(com|ar))" README.md screenshots
```

Revisar la captura a tamaño desktop y mobile antes de integrar; el segundo comando se
evalúa manualmente para distinguir nombres genéricos de una filtración real.

## Parent plan sync

- [ ] Marcar R4.2 DONE y registrar el activo final elegido.
