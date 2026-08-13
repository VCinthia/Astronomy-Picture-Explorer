# Phase Plan P4 - Documentación pública y alineación de release

Date: 2026-08-12
Status: PLANNED
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

- [ ] **R4.1** Registrar cutover P3, estado canónico e inventario documental (W1).
- [ ] **R4.2** Reescribir README público y reemplazar/remover la captura obsoleta (W2).
- [ ] **R4.3** Alinear documentación técnica y operacional P3 sin exponer datos sensibles
  (W3).
- [ ] **R4.4** Añadir aclaraciones históricas y corroborar los flujos documentados (W4).
- [ ] **R4.5** Ejecutar auditoría final, smoke y promoción documental controlada (W5).

## 6. Waves

| Wave | Dependencia | Resultado acotado |
|---|---|---|
| P4-W1 | P3 cutover | Evidencia post-cutover + estados P3 sincronizados + inventario |
| P4-W2 | W1 | README público actual, captura anónima y guía local segura |
| P4-W3 | W1 | Contratos y runbooks P3 alineados y sanitizados |
| P4-W4 | W1 + W3 | Aclaraciones P1/P2 y revisión de flujos documentados |
| P4-W5 | W2 + W3 + W4 | Auditoría final, gates y promoción P4 |

W2 y W3 pueden ser analizadas en paralelo, pero su implementación se integra de forma
serial para mantener un único conjunto documental coherente.

## 7. Phase exit criteria

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
