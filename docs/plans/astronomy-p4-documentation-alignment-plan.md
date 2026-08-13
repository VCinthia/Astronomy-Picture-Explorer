# Phase Plan P4 - Documentación pública y alineación de release

Date: 2026-08-12
Status: IN PROGRESS - P4-W1 DONE
Phase: `P4`
Source master plan: `docs/plans/astronomy-master-plan.md`
Documentation boundary: `docs/adr/0004-public-documentation-boundaries.md`
Depends on: P3 release cutover verification

## 1. Goal

Convertir la documentación de la aplicación ya desplegada en una referencia coherente,
pública y segura: un README útil para portfolio, documentación técnica alineada con el
estado P3 real y evidencia operativa sanitizada. P4 no agrega funcionalidades de producto,
proveedores, credenciales ni costo.

## 2. Scope

### Included

- Verificación factual del cutover de P3 y sincronización de sus estados canónicos.
- Inventario y clasificación de documentación: pública, técnica versionada y operativa
  saneada.
- README público actualizado, captura anónima actual y explicación de experiencia, stack,
  arquitectura de alto nivel, ejecución local y límites explícitos.
- Alineación de PRD, readiness, plan maestro, P3, ADR, flujo, runbooks y README de backend.
- Aclaraciones terminales para documentos P1/P2 históricos desactualizados, preservando su
  evidencia original.
- Revisión de flujos documentados: APOD, fecha/búsqueda, cuenta, sesión, favoritos,
  confirmación y recuperación de contraseña.
- Auditoría final de enlaces, activos, estado, secretos y comandos de verificación.

### Excluded

- Cambiar contratos, UX, lógica Angular/API, schema o configuración de proveedores.
- Publicar orígenes directos, connection strings, valores de entorno, claves, tokens,
  correos personales, enlaces de correo, números de límite o instrucciones de bypass.
- Mover `docs/` a `.gitignore`; los documentos seguros siguen siendo parte trazable del
  repositorio.
- Crear una automatización de catálogo, keepalive, plan pago o despliegue intermedio.

## 3. Documentation contract

| Clase | Propósito | Puede versionarse | Restricción |
|---|---|---:|---|
| Pública | README, captura anónima, arquitectura de alto nivel | Sí | Sin datos operativos de producción |
| Técnica | ADR, contratos, phase/wave plans y guía local | Sí | Placeholders para configuración sensible |
| Operativa saneada | Runbooks y evidencia de smoke | Sí | Resultados/fechas, nunca valores o datos personales |
| Secreta/local | `.env`, `.secrets/`, credenciales y respuestas de dashboard | No | Ignorada y fuera de Git |

ADR-0004 es la autoridad cuando una explicación es útil pero podría revelar información de
operación o seguridad.

## 4. Execution model

- P4 acumula únicamente cambios documentales en `codex/p4-integration`, creada desde
  `main` en el commit `48ac901` o su sucesor fast-forward.
- Cada wave trabaja en su subrama, se verifica, revisa y se integra a P4. `main` no recibe
  un README parcialmente reconciliado.
- P4-W1 es el gate: no debe marcar P3 `DONE` ni afirmar que Netlify publica `main` sin
  comprobarlo en el dashboard y repetir el smoke público posterior al cutover.
- P4-W5 revisa el conjunto completo y sólo entonces autoriza la promoción P4 -> `main`.

## 5. Requirements checklist

- [x] **R4.1** Registrar cutover P3, estado canónico e inventario documental (W1).
- [ ] **R4.2** Reescribir README público y reemplazar/remover la captura obsoleta (W2).
- [ ] **R4.3** Alinear documentación técnica y operacional P3 sin exponer datos sensibles
  (W3).
- [ ] **R4.4** Añadir aclaraciones históricas y corroborar los flujos documentados (W4).
- [ ] **R4.5** Ejecutar auditoría final, smoke y promoción documental controlada (W5).

## 6. Inventario P4-W1 y responsables de acción (2026-08-12)

P4-W1 verificó que `main` contiene `48ac901`, la dueña confirmó que ambas superficies de
producción despliegan desde `main` y el smoke posterior de health/catálogo same-origin
pasó. El siguiente inventario clasifica la documentación versionada; no registra
credenciales de proveedor, datos personales, rutas de dashboards ni orígenes operativos
directos.

| Clase | Artefactos relevantes y hallazgo actual | Responsable |
|---|---|---|
| Pública | `README.md` aún describe la experiencia pre-P3/local; `screenshots/home.png` representa UI anterior. | P4-W2 reemplaza el relato público y la captura anónima. |
| Técnica, canónica | PRD, readiness, master, plan P3, ADR-0003, flow overview, P3-W14 y P3-W15 ahora registran P3 como DONE. | P4-W3 revisa redacción, contratos y enlaces contra la implementación actual. |
| Técnica, de apoyo | `backend/README.md`, runbooks local/deploy/setup y política de framework omiten o anteceden partes de P3, incluida recuperación de contraseña y cierre de proveedores. | P4-W3 los alinea y sanea. |
| Operativa, saneada | `docs/deploy/p3-deploy-runbook.md` preserva evidencia fechada de smoke y el cierre P4-W1. | P4-W3 elimina detalle operativo innecesario y conserva guía segura reproducible. |
| Histórica | Planes/waves P1/P2 y notas Figma conservan su evidencia original; la nota Figma mobile-pending contradice la UI responsive implementada. | P4-W4 añade aclaraciones terminales y valida referencias de flujo sin reescribir historia. |
| Control P4 | ADR-0004, este phase plan y las waves P4 definen el límite de documentación pública. | P4-W5 audita el conjunto final antes de promover. |
| Local/secreta | `.env`, `.secrets/` e `IMPLEMENTATION_NOTES.md` están ignorados y no trackeados; `docs/` sigue trackeado. | W1 solo verificó; no se autoriza cambiar `.gitignore`. |

Las comprobaciones cruzadas abiertas asignadas a W3/W4 incluyen el texto de límites edge
de W15, referencias de proveedor en futuro, la separación README público/local y las
referencias Figma P1/P2. Son únicamente tareas documentales: P4 no cambia proveedores ni
configuración de la aplicación.

## 7. Waves

| Wave | Dependencia | Resultado acotado |
|---|---|---|
| P4-W1 | P3 cutover | Evidencia post-cutover + estados P3 sincronizados + inventario ✅ |
| P4-W2 | W1 | README público actual, captura anónima y guía local segura |
| P4-W3 | W1 | Contratos y runbooks P3 alineados y sanitizados |
| P4-W4 | W1 + W3 | Aclaraciones P1/P2 y revisión de flujos documentados |
| P4-W5 | W2 + W3 + W4 | Auditoría final, gates y promoción P4 |

W2 y W3 pueden ser analizadas en paralelo, pero su implementación se integra de forma
serial para mantener un único conjunto documental coherente.

## 8. Phase exit criteria

- El README describe la aplicación que un visitante puede usar hoy, no el mock P1/P2.
- P3 aparece como `DONE` sólo después del gate de W1; todos los documentos canónicos
  comparten el mismo estado y fecha de evidencia.
- Los flujos documentados coinciden con rutas/contratos existentes, incluidos reset y
  favoritos autenticados.
- El escaneo no encuentra secretos, connection strings, correos/códigos/enlaces de prueba
  ni orígenes internos de producción en los documentos a publicar.
- Build/test aplicables y el smoke público same-origin pasan; enlaces y screenshot del
  README son válidos.
- P4 se promueve en un único fast-forward desde `codex/p4-integration` a `main`.
