# ADR-0004 - Límites de documentación pública y evidencia operativa

Date: 2026-08-12
Status: Accepted
Builds on: ADR-0003

## Context

El repositorio es parte del portfolio público y, después de P3, contiene una aplicación
Angular desplegada, una API, autenticación, un catálogo PostgreSQL y correo transaccional.
La documentación debe permitir que un evaluador entienda el producto y pueda reproducirlo
localmente, sin convertir el README ni los runbooks versionados en una guía para atacar la
instancia publicada o descubrir configuración sensible.

Ocultar documentación no protege secretos que hayan sido publicados previamente. La
protección real sigue siendo: secretos fuera de Git, controles de autenticación/autorización,
configuración de proveedor y validación en la API. Por ello `docs/` permanece versionado y
se sanea; no se agrega al `.gitignore` como mecanismo de seguridad.

## Decision

1. El `README.md` es documentación pública orientada a portfolio. Describe experiencia,
   arquitectura de alto nivel, stack, límites de costo, accesibilidad y ejecución local. No
   publica orígenes internos, URLs directas de proveedor, valores de variables, secretos,
   presupuestos de rate limiting, claims de firma ni pasos administrativos de dashboards.
2. Las decisiones técnicas versionadas explican contratos y consecuencias, pero usan
   placeholders para infraestructura (`<api-origin>`, `<connection-string>`) y nunca
   reproducen valores de producción o datos personales. Los secretos se configuran sólo en
   el entorno local del desarrollador o en dashboards de proveedor.
3. Los runbooks operativos conservan procedimientos reproducibles y evidencia sanitizada:
   fechas, resultado PASS/FAIL y clases de recursos. No conservan correos, enlaces efímeros,
   identificadores de usuario, contraseñas, códigos ni URLs de administración.
4. Los documentos históricos no se reescriben para fingir que decisiones posteriores
   existían antes. Cuando estén desactualizados, P4 agrega una aclaración terminal con el
   estado actual y la referencia canónica.
5. Capturas del README deben representar la UI pública actual y no mostrar datos de cuenta,
   tokens, direcciones de correo, herramientas de proveedor ni estado autenticado.

## Consequences

### Positive

- El portfolio explica el trabajo real sin depender de información confidencial.
- La documentación puede permanecer revisable, trazable y útil para futuras waves.
- La evidencia de despliegue comunica resultados sin ampliar superficie de ataque.

### Negative

- Un revisor que necesite operar proveedores debe usar su propia configuración local y los
  permisos del dashboard; el repositorio no es una copia de la configuración productiva.
- Cada cambio de arquitectura o de proveedor exige revisar la clasificación documental.

## Verification impact

P4 debe buscar secretos y referencias de infraestructura productiva antes de promover su
rama, verificar enlaces/activos públicos y ejecutar los gates de build/test aplicables.

## P4-W1 application record (2026-08-12)

P4-W1 confirmó el requisito de cierre: ambas superficies de producción despliegan desde
`main`, que contiene `48ac901`, y el smoke posterior de health y catálogo same-origin
pasó. P3 puede declararse `DONE`; los controles restantes de este ADR se aplican a las
waves documentales P4-W2 a P4-W5.
