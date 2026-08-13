# Wave P4-W2 - README público y captura actual

Date: 2026-08-12
Status: DONE
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

- [x] W2.1 Reescribir introducción, funcionalidades y estado de release desde la experiencia
  real: APOD, fecha, FTS, palette, cuentas, favoritos, confirmación y recuperación.
- [x] W2.2 Documentar Angular, .NET, PostgreSQL, NASA APOD, Docker y proveedores sólo a
  nivel de arquitectura, con flujo navegador -> app -> API -> datos/correo sin orígenes
  internos ni configuración sensible.
- [x] W2.3 Incluir setup local seguro que derive variables de `.env.example`/dashboards
  propios sin valores reales, y describir claramente que los servicios gratuitos pueden
  tener cold start.
- [x] W2.4 Reemplazar la captura P1 obsoleta por una captura anónima del UI actual o
  retirarla si no puede verificarse visualmente; nunca mostrar sesión, correo o proveedor.
- [x] W2.5 Corregir referencias a Figma para que no prometan fidelidad no verificada y
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

- [x] Marcar R4.2 DONE y registrar el activo final elegido.

## Implementation record (2026-08-12)

- Replaced `screenshots/home.png` with an anonymous 1440×900 desktop capture of the
  public, deployed Explorer view. The capture was made with a temporary browser profile,
  selected an APOD date available at capture time, and contains no authenticated session,
  personal data or provider/dashboard content. The established filename remains for link
  stability; the README alt text and caption identify it as Explorer.
- Rewrote the public README around the shipped P3 experience: APOD browsing, date and
  catalog search, palette, accounts, favorites, accessibility and responsive navigation.
- Added a high-level browser-to-application architecture diagram, safe local setup,
  free-tier cold-start note, technology summary and NASA/portfolio attribution. It omits
  operational origins, credentials, provider configuration, security internals and test
  identities.

## Verification record (2026-08-12)

- Visual inspection passed for the replacement capture at desktop size: current primary
  navigation, Explorer-active state, date-first/search-second layout, APOD media and
  palette are visible; no account or provider data appears.
- `npm run build` passed. Angular reported existing stylesheet selector warnings, with no
  build failure.
- `git diff --check` and the focused sensitive-pattern review passed; generic local
  `.env`/secret-file guidance is intentional and contains no real value.
