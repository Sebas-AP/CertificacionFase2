# Contratos REST propuestos

Estado: contratos para desarrollo; salvo `/health`, `/api/context` y Swagger,
las rutas descritas todavía no están implementadas.

## Convenciones

- Base local: `http://localhost:5000`; las rutas de negocio usan el prefijo
  `/api`.
- JSON usa `camelCase`, IDs son GUID y fechas/horas son ISO 8601 en UTC. Los
estados de factura son `pending_review`, `approved` y `extraction_failed`.
- Errores usan `application/problem+json` con `type`, `title`, `status`,
  `detail`, `instance`, `code` y `traceId`. `detail` no incluye stack traces,
  secretos ni mensajes internos de proveedores. Errores de validación añaden
  `errors` por campo.
- Listados paginados aceptan `page` (por defecto 1) y `pageSize` (por defecto
  25; máximo 100), y responden `{ "items": [], "page": 1, "pageSize": 25,
  "totalItems": 0, "totalPages": 0 }`.
- La identidad actual es fija y solo para demostración. El servidor obtiene
  usuario y organización de `ICurrentUserContext`; el cliente no envía ni puede
  seleccionar esos IDs. No considerar este modo autenticación de producción.
- La API no devuelve valores simulados cuando una dependencia no está
  disponible. Se usan `400` para entrada inválida, `401`/`403` cuando exista
  autenticación y autorización reales, `404` para recursos inexistentes,
  `409` para conflictos, `413` para cargas que exceden el límite acordado y
  `503` para dependencias temporalmente no disponibles.

## Rutas actualmente implementadas

| Método y ruta | Permiso actual | Respuesta |
|---|---|---|
| `GET /health` | Anónimo | `200 { "status": "ok" }`; no consulta dependencias. |
| `GET /api/context` | Anónimo, solo demo | Contexto fijo de organización y usuario. |
| `GET /api/facturas?page=&pageSize=&estado=` | Anónimo, solo demo | Historial paginado; `estado` acepta `pending_review`, `approved` o `extraction_failed`. |
| `GET /api/facturas/{id}` | Anónimo, solo demo | Detalle y auditoría de revisión; `404` si no existe en la organización demo. |
| `PUT /api/facturas/{id}/revision` | Anónimo, solo demo | Actualiza campos corregibles solo en `pending_review`; `400` si los valores no son válidos, `404` si no es de la organización o `409` si ya cambió de estado. |
| `POST /api/facturas/{id}/confirmacion` | Anónimo, solo demo | Aprueba con `200`; requiere factura pendiente y número, fecha, moneda e importe total. `404` fuera de la organización y `409` si no es aprobable. |
| `GET /api/inventario?page=&pageSize=&buscar=` | Anónimo, solo demo | Inventario paginado y filtrado de la organización fija. `page` predeterminado 1, `pageSize` 25 (máximo 100), `buscar` busca SKU/nombre (máximo 100 caracteres). Devuelve cantidades como decimal y umbral; ambos son `null` si aún no existe registro de stock. |
| `GET /api/inventario/{productId}` | Anónimo, solo demo | Detalle de producto e inventario; `404` también si el producto pertenece a otra organización. |
| `GET /swagger` | Anónimo | OpenAPI de las rutas implementadas. |

## Rutas de negocio propuestas

### Autenticación

| Método y ruta | Estado |
|---|---|
| `POST /api/login` | Pendiente de elegir y configurar el proveedor de autenticación. No crear una respuesta de login ficticia; la ruta permanece ausente mientras se use la identidad demo. |

### Facturas: siguientes operaciones

`GET /api/facturas`, detalle, corrección y confirmación ya están implementados
en modo demo. Cada modificación registra acción, campos cambiados, motivo
opcional, usuario demo y fecha UTC en la misma transacción de base de datos.
La carga/extracción `POST /api/facturas` sigue pendiente de las integraciones
reales descritas a continuación.

La respuesta de factura incluye, como mínimo, `id`, `estado`, `proveedor`,
`numero`, `fechaEmision`, `moneda`, `subtotal`, `impuestos`, `total` y
`lineas`. Los importes llevan moneda explícita y se calculan con decimales, no
con punto flotante binario. La API no devuelve URL pública del PDF. El tamaño
máximo acordado es 20 MB. La carga privada en Blob Storage y extracción siguen
pendientes; la retención acordada es de dos meses desde la carga y falta definir
el mecanismo que aplicará/verificará el borrado y los fallos parciales.

Cuando se implemente la carga, errores particulares: `400` para PDF inválido,
`413` para tamaño excedido, `409` para duplicado según las reglas acordadas y
`503` si Document Intelligence no está disponible. Se probará qué estado
conserva un PDF si la extracción falla tras la carga.

### Chat empresarial

| Método y ruta | Entrada | Éxito |
|---|---|---|
| `POST /api/chat` | `{ "mensaje": "...", "conversacionId": "guid opcional" }` | `200 { "conversacionId": "guid", "respuesta": "...", "fuentes": [{ "titulo": "...", "referencia": "..." }] }` |

La recuperación filtra por la organización obtenida del contexto del servidor.
`fuentes` puede ser una lista vacía cuando no hay evidencia disponible; en ese
caso la respuesta debe expresarlo y no inventar datos. No se permite que
instrucciones incluidas en mensajes o documentos cambien las reglas del
sistema. Errores de Foundry/Search no se convierten en respuestas generadas.

### Reportes

| Método y ruta | Parámetros | Éxito |
|---|---|---|
| `GET /api/reportes?desde={fecha}&hasta={fecha}` | Periodo ISO 8601; ambos límites deben formar un intervalo válido. | `200` con `periodo`, `moneda` y métricas. |

Métricas iniciales propuestas: facturas procesadas y duplicadas por fecha de
procesamiento, ventas como suma de facturas aprobadas por fecha de emisión,
inventario crítico como productos cuya cantidad no supera su umbral mínimo, y
consultas atendidas como conversaciones con respuesta fundamentada. Las
fórmulas, estados incluidos y periodo máximo deben confirmarse con negocio
antes de publicar los valores como indicadores.

### Inventario: siguiente alcance

La consulta de inventario ya está implementada en modo demo y lee datos SQL
reales, filtrados por la organización del servidor. La primera entrega es de
solo lectura. Las actualizaciones requieren definir permisos y auditoría de
movimientos antes de añadir un endpoint de escritura.

El esquema inicial incluye organización/usuario, proveedor, factura/líneas,
producto e inventario; relaciones de factura y producto incluyen la
organización para impedir referencias cruzadas entre organizaciones en SQL.
SKU es único dentro de una organización en esta primera versión y debe
confirmarse como regla de negocio antes de cargar datos reales.

## Decisiones previas a implementar

- Proveedor y configuración de autenticación real; hasta entonces no existe
  `POST /api/login` y el contexto demo no protege datos.
- Mecanismo de borrado, acceso y verificación de expiración de PDFs privados;
  el plazo acordado es de dos meses desde la carga.
- Límite máximo de carga PDF y estado durable cuando falle la extracción.
- Criterios de deduplicación, estados de factura y campos de corrección.
- Fórmulas y periodos de KPI con fuentes de datos aprobadas.
- Acciones de escritura permitidas para inventario.
