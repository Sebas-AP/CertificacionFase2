# Contratos REST del backend

Estado: borrador para acordar con el frontend antes de conectar pantallas. Las rutas de negocio aún no están implementadas; no se deben sustituir por respuestas ficticias. Todas las respuestas de negocio usan JSON y fechas ISO 8601. Los importes incluyen `amount` decimal y `currency` ISO 4217.

## Convenciones comunes

- Salvo el flujo de autenticación, las rutas requieren una identidad Microsoft Entra validada por el servidor. Cada consulta debe restringirse al usuario y organización autorizados.
- Los errores tienen la forma `{ "error": { "code": "VALIDATION_ERROR", "message": "Descripción segura", "correlationId": "..." } }`. Nunca incluyen trazas, credenciales ni mensajes internos de Azure/SQL.
- Usar `400` para entradas inválidas, `401` sin sesión válida, `403` sin permiso, `404` para recursos no visibles, `409` para conflictos de estado/deduplicación, `413` para cargas sobre el límite, `415` para formatos no admitidos, `429` al aplicar límites y `502`/`503` para dependencias externas no disponibles.
- Las colecciones usan `limit` y `cursor`; el servidor impone el máximo. El valor máximo, la expiración de sesión y los límites de carga se deben acordar antes de habilitar las rutas correspondientes.

## Rutas

| Método y ruta | Solicitud | Respuesta exitosa | Autorización |
|---|---|---|---|
| `POST /api/login` | Pendiente de concretar con el flujo Entra ID elegido. No acepta credenciales locales ni crea sesiones simuladas. | Resultado de inicio/callback y sesión configurada, solo después de acordar el flujo seguro. | Pública únicamente para iniciar/completar el flujo configurado. |
| `GET /api/facturas?limit=&cursor=&status=` | Filtros y paginación opcionales. | `{ "items": [InvoiceSummary], "nextCursor": null }`. | Usuario autenticado; ámbito autorizado. |
| `POST /api/facturas` | `multipart/form-data` con un PDF. Límites de bytes, tiempo y concurrencia por acordar. | `202` y `{ "id": "...", "status": "processing" }` si el procesamiento es asíncrono; errores explícitos si falla. | Usuario autenticado con permiso de carga. |
| `GET /api/facturas/{id}` | Identificador estable de factura. | `InvoiceDetail` con campos extraídos, confianza/origen disponibles y estado de revisión. | Solo factura dentro del ámbito autorizado. |
| `PATCH /api/facturas/{id}` | Campos corregidos permitidos y versión/estado esperado para evitar sobrescrituras. | Factura actualizada con estado de revisión y procedencia del cambio. | Usuario autenticado con permiso de revisión. |
| `POST /api/chat` | `{ "conversationId": "opcional", "message": "..." }`; no se acepta contexto documental arbitrario como evidencia autorizada. | `{ "conversationId": "...", "answer": "...", "sources": [{ "id": "...", "title": "..." }] }`. | Usuario autenticado; recuperación filtrada por ámbito antes de entregar contenido al modelo. |
| `GET /api/reportes?from=&to=` | Periodo explícito en ISO 8601. | `{ "period": { "from": "...", "to": "..." }, "metrics": [...] }`; cada métrica incluye valor, unidad, fórmula y fuente. | Usuario autenticado con permiso de lectura gerencial. |
| `GET /api/inventario?limit=&cursor=&criticalOnly=` | Filtros y paginación opcionales. | `{ "items": [InventoryItem], "nextCursor": null }`. | Usuario autenticado; ámbito autorizado. |

## Esquemas de dominio mínimos

- `InvoiceSummary`: `id`, `supplierId`, `invoiceNumber`, `issueDate`, `total` (`amount`, `currency`), `status`, `createdAt`.
- `InvoiceDetail`: resumen más `fields` tipados con `value`, `confidence` y `source` cuando estén disponibles, `reviewedBy` y `reviewedAt` cuando aplique.
- `InventoryItem`: `id`, `productId`, `name`, `quantity`, `unit`, `reorderThreshold`, `updatedAt`.
- Los campos obligatorios, estados exactos, tipos decimales y restricciones deben alinearse con el esquema SQL y acordarse antes de persistir datos.

## Decisiones necesarias antes de habilitar operaciones

- Confirmar el flujo de usuario Entra ID y cómo se mapea a una organización en datos.
- Fijar tamaño máximo de PDF, concurrencia, tiempo de procesamiento y política de retención. No se persiste el binario en SQL ni se presupone almacenamiento durable.
- Acordar normalización de importes/monedas/fechas, estados de revisión y criterios de deduplicación.
- Definir fórmulas, periodos y fuentes de cada KPI; las métricas no disponibles deben comunicarse como no disponibles, nunca inventarse.
- Inventario queda de solo lectura hasta que se acuerden explícitamente operaciones de escritura y permisos.
