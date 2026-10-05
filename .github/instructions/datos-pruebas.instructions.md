---
applyTo: "**/*.sql, **/database/**, **/db/**, **/tests/**, **/__tests__/**, **/*.test.*, **/*.spec.*"
---

# Datos, SQL y pruebas

## Modelo de datos

- Alinea el modelo relacional con usuarios, proveedores, facturas, productos, inventario, consultas y reportes, ajustándolo a las entidades ya existentes. Define claves, relaciones, restricciones e índices según consultas y reglas de negocio.
- Usa transacciones para operaciones que actualizan varias entidades. Define unicidad y estrategia de deduplicación de facturas con el equipo antes de codificarlas.
- Parametriza consultas. No concatentes entradas del usuario en SQL ni expongas acceso libre a la base de datos desde la UI.
- Define permisos y aislamiento por usuario/organización en las consultas, además de en la capa de API.
- Versiona cambios de esquema mediante el mecanismo de migración existente; no ejecutes cambios destructivos ni modifiques datos reales sin aprobación explícita.
- Usa tipos adecuados para moneda, precisión decimal, fechas UTC/offset y estados. No uses `float` para importes.
- No guardes PDF ni credenciales en tablas. Evita incluir PII o datos financieros reales en seed data, fixtures o logs.

## Pruebas

- Añade pruebas unitarias para validaciones, normalización, reglas de factura, deduplicación, fórmulas de KPI y transformaciones de respuesta.
- Añade pruebas de API para autenticación/autorización, validación, códigos de estado, límites de carga y errores de dependencias; verifica que usuarios no accedan a datos ajenos.
- Prueba integraciones de IA con mocks en pruebas unitarias y con casos de evaluación separados para respuestas fundamentadas, falta de evidencia, resultados inválidos y prompt injection. No dependas de Azure real para la suite rápida.
- Usa únicamente datos ficticios. Incluye casos límite: campos ausentes, confianza baja, importes/fechas inválidos, duplicados, archivo no válido, timeout y servicio externo no disponible.
- Ejecuta las pruebas, el lint y el type-check/build disponibles para la superficie modificada. Informa cualquier validación no ejecutada y su motivo; no afirmes que pasó si no se ejecutó.
