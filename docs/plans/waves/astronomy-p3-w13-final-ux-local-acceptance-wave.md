# Wave P3-W13 - UX final y aceptación local

Date: 2026-07-22
Status: READY - Not Started
Wave ID: `P3-W13`
Source Phase: `P3`
Source Phase Plan: `docs/plans/astronomy-p3-backend-plan.md`
Depends On: P3-W8 + P3-W10 + P3-W11 + P3-W12 DONE and merged
Suggested Branch: `wave/p3-w13-final-ux-local-acceptance`
Suggested PR Title: `[P3-W13] Polish final navigation and local acceptance`

## Goal

Cerrar la jerarquía visual y los recorridos de cuenta locales para que la experiencia
que se promueva en P3-W14 sea la versión final de portfolio, sin tocar proveedores ni
recursos productivos.

## Decisions and boundaries

- El calendario queda primero en Explorer y Search segundo. En escritorio ambos controles
  comparten el borde inferior; en pantallas angostas se apilan calendario -> búsqueda.
- El selector anterior/siguiente deja de ser parte del header global. Solo Home lo ofrece
  sobre el extremo derecho de la imagen APOD, con contraste, foco y márgenes responsivos.
- Desktop y mobile conservan `aria-current="page"`, color de acento y agregan una línea
  inferior fina para la ruta activa. La ruta de cuenta no marca falsamente Home/Explorer/
  Favorites.
- Search continúa enviando el texto que escribió la persona; PostgreSQL FTS ya es
  case-insensitive. Esta wave agrega evidencia para minúsculas, MAYÚSCULAS y PascalCase,
  pero no instala `pg_trgm` ni cambia ranking, stemming o el contrato HTTP.
- Al autenticar correctamente, Login reemplaza el formulario entero por el único mensaje
  `Signed in successfully.` y redirige en un plazo breve y acotado. Si existe un
  `returnUrl` interno normalizado, se conserva (por ejemplo, `/favorites`); sin él, el
  destino es `/home`. No se muestran campos, botón ni texto técnico de sesión tras éxito.
- Email/confirmación no se rediseña como proveedor ni se expone un token en la SPA. El
  stack W12 usa `LocalLog`: la aceptación local lee el enlace efímero solo desde el log
  de API y verifica register -> confirm POST -> login -> favorites. `401 /auth/refresh`
  sin cookie es el bootstrap anónimo esperado; `403 email_unconfirmed` debe mostrar el
  CTA de reenvío, no un error genérico.
- Esta wave no crea cuentas de proveedor, no configura Resend/NASA/Neon/Render/Netlify y
  no despliega. Esa autoridad se mantiene exclusivamente en P3-W14.

## File scope

- `src/app/app.component.{ts,html,spec.ts}`
- `src/app/components/bottom-nav/bottom-nav.component.{ts,spec.ts}`
- `src/app/pages/home/home.component.{ts,spec.ts}`
- `src/app/pages/explorer/explorer.component.{ts,spec.ts}`
- `src/app/components/picture-card/`
- `src/app/components/search-bar/search-bar.component.spec.ts`
- `src/app/services/astronomy.service.spec.ts`
- `src/app/auth/login/login.component.spec.ts`
- `backend/AstronomyExplorer.Api/Dockerfile` (solo compatibilidad de script Linux al
  construir desde Windows)
- `docs/deploy/p3-local-runbook.md` (solo si requiere aclarar la aceptación; nunca copiar
  enlaces/códigos de confirmación)
- documentos de estado P3

## Checklist

- [ ] W13.1 Reordenar Explorer como DatePicker -> SearchBar y alinear inferiormente ambos
  controles en desktop sin degradar el layout mobile ni los labels accesibles.
- [ ] W13.2 Trasladar el stepper de fecha del shell global a Home, posicionado sobre la
  imagen APOD con separación y contraste correctos; conservar límites UTC y cancelar
  requests obsoletos mediante el estado existente de `AstronomyService`.
- [ ] W13.3 Añadir indicador de ruta activa con línea inferior fina y color de acento en
  navegación desktop y mobile; conservar `aria-current`, focus visible y matching exacto.
- [ ] W13.4 Probar que `astronomy`, `ASTRONOMY` y `Astronomy` producen el mismo resultado
  con el catálogo local preparado, sin lowercasing destructivo de la entrada ni cambio de
  contrato/ranking FTS.
- [ ] W13.5 Completar y registrar el smoke local de cuenta: bootstrap anónimo 401 esperado,
-  register, enlace `LocalLog`, confirm POST, login con pantalla transitoria y redirección
  a `returnUrl`/Home, reload/refresh, favorite, logout y CTA resend para 403
  `email_unconfirmed`; el código de confirmación nunca se conserva en una captura, commit,
  issue ni documentación.
- [ ] W13.6 Garantizar que `docker compose up -d --build` funciona desde Windows y Linux:
  el entrypoint normaliza CRLF dentro de la imagen antes de ejecutar como usuario no-root.

## Acceptance criteria

- En la captura desktop de Explorer, la fecha precede a Search y sus controles se apoyan
  sobre el mismo borde inferior; en mobile siguen siendo legibles, tocables y ordenados.
- El header no contiene botones de fecha. Home sí permite avanzar/retroceder su APOD sin
  invadir el contenido, con botones deshabilitados en los límites.
- Existe exactamente una ruta primaria activa en cada breakpoint y su línea inferior es
  visible; cuenta y confirmación no activan ninguna ruta de contenido por error.
- Las tres capitalizaciones de la consulta devuelven la misma entrada local; no se
  habilita `pg_trgm`, no se consulta NASA durante Search y se conserva el máximo de 30.
- Una cuenta local confirmada puede iniciar sesión y usar Favorites. Antes de confirmar,
  Login muestra el mensaje y CTA de reenvío previstos; el 401 de refresh sin sesión no se
  presenta como fallo de la aplicación.
- Después de login correcto solo se percibe `Signed in successfully.` antes de navegar; el
  formulario no queda disponible ni parece aceptar un segundo login. Un `returnUrl`
  interno válido mantiene su destino y el caso normal llega a Home.
- Build, pruebas, Compose y smoke local pasan. No se realizan llamadas a NASA/Resend ni
  mutaciones de proveedor.

## Verification

```powershell
docker compose config
docker compose up -d --build
docker compose ps
Invoke-WebRequest http://localhost:5179/health
Invoke-RestMethod 'http://localhost:8080/api/apod/search?q=astronomy&page=1&pageSize=12'
Invoke-RestMethod 'http://localhost:8080/api/apod/search?q=ASTRONOMY&page=1&pageSize=12'
Invoke-RestMethod 'http://localhost:8080/api/apod/search?q=Astronomy&page=1&pageSize=12'
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
dotnet test backend/AstronomyExplorer.sln -c Release --no-restore
docker compose down
```

## Parent plan sync

- [ ] Actualizar `R3.13`, master, readiness, PRD y flow overview al cerrar esta wave.
- [ ] Mantener P3-W14 como única wave autorizada a proveedores/seed/deploy productivo.
- [ ] Registrar evidencia visual desktop/mobile y resultado del smoke local sin secretos.
