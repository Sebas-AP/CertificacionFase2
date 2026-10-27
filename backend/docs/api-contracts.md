# Contratos REST

Las rutas documentadas como implementadas están disponibles en modo Demo. No
hay autenticación real: no usar datos reales ni exponer la API públicamente
fuera del entorno académico.

## Convenciones

- La API usa JSON `camelCase`, GUIDs y fechas ISO 8601. Los timestamps se
  normalizan a UTC.
- Todos los listados paginados aceptan `page` (predeterminado 1) y `pageSize`
  (predeterminado 25, máximo 100).
- Los errores usan `application/problem+json`; incluyen `code` y `traceId`
  cuando los produce una operación de la API. No incluyen stack traces,
  secretos ni mensajes internos de proveedores.
- Las rutas de demostración requieren la identidad fija del servidor, pero no
  autenticación de usuario y no deben exponerse con datos reales. Las únicas
  rutas con autenticación propia son `/api/agent-tools/v1/*`, que exigen
  `Authorization: Bearer <clave>` configurada fuera del repositorio. Swagger
  declara ese esquema solo para las herramientas del agente. El servidor asigna
  `OrganizationId` y `UserId` desde `ICurrentUserContext`; el cliente y el
  agente no pueden elegir esos IDs.
- La demo solo expone identidad fija. Su modo público no sustituye
  autenticación, autorización ni aislamiento para producción.

## Rutas implementadas

| Método y ruta | Entrada | Respuesta |
|---|---|---|
| `GET /health` | — | `200 { "status": "ok" }`; no consulta dependencias. |
| `GET /api/context` | — | Contexto de organización y usuario demo. |
| `GET /api/facturas?page=&pageSize=&estado=` | Filtros paginados | Historial; estados `pending_review`, `approved`, `extraction_failed`, `extracted`, `needs_validation`, `duplicate`. |
| `GET /api/facturas/{invoiceId}` | GUID de factura | Detalle y auditoría; `404` si no existe en la organización demo. |
| `POST /api/invoices/upload` | Un PDF en `multipart/form-data`, campo `file` | `201` con la factura; `400` inválido, `409` duplicado exacto, `413` sobre el límite, `503` si Blob no está disponible. |
| `POST /api/invoices/{invoiceId}/retry-extraction` | GUID de factura | Reutiliza el Blob existente; `404`, `409` si no se puede reintentar, `503` si falla la extracción/acceso. Máximo tres intentos. |
| `PUT /api/facturas/{invoiceId}/revision` | Campos corregibles y motivo opcional | `200`; `400` validación, `404` no encontrado, `409` estado incompatible. |
| `POST /api/facturas/{invoiceId}/confirmacion` | GUID de factura | `200` al aprobar; `404` no encontrada, `409` incompleta o en estado incompatible. |
| `GET /api/inventario?page=&pageSize=&buscar=` | Búsqueda opcional | Inventario paginado de SQL, limitado a la organización demo. |
| `GET /api/inventario/{productId}` | GUID de producto | Detalle; `404` ProblemDetails también para otras organizaciones. |
| `GET /api/reportes?fromUtc=&toUtc=` | Instantes ISO 8601; `fromUtc < toUtc` | KPIs consultados de SQL para `[fromUtc, toUtc)`. |
| `POST /api/chat` | `{ "message": "..." }`; máximo 4000 caracteres | `200` con `{ "reply": "...", "sources": [] }`; `400` inválido o `conversationId` presente; `502` respuesta inválida/demasiado grande; `503` agente no configurado/no disponible; `504` timeout. |
| `POST /api/invoices/{invoiceId}/explain` | GUID de factura | Explicación del agente basada en campos, advertencias y duplicados exactos de la organización del servidor; `404` si no existe; `502`, `503` o `504` ante fallos controlados del proveedor. |
| `GET /api/agents/health` | — | Estado seguro (`Healthy`, `Unhealthy`, `NotConfigured`) por agente; consulta las versiones configuradas. |
| `GET /api/agent-tools/v1/invoices?page=&pageSize=` | Página, máximo 50 | Facturas recientes sin RFC ni metadatos de Blob; `401` clave inválida; `503` clave no configurada. |
| `GET /api/agent-tools/v1/invoices/{invoiceId}` | GUID de factura | Campos estructurados, confianza y advertencias; `404` también para facturas fuera de la organización. |
| `GET /api/agent-tools/v1/invoices/{invoiceId}/duplicates` | GUID de factura | Evidencia de hash SHA-256 o coincidencia exacta de RFC normalizado, número, fecha e importe; no calcula puntuaciones de similitud. |
| `GET /api/agent-tools/v1/reports?fromUtc=&toUtc=` | Periodo UTC máximo de 366 días | Indicadores actuales de SQL para `[fromUtc, toUtc)`; `400` si el periodo no es válido. |
| `GET /api/agent-tools/v1/inventory?page=&pageSize=&search=` | Paginación, tamaño máximo 50 y búsqueda opcional | Inventario actual de SQL limitado a la organización del servidor. |
| `GET /api/agent-tools/v1/integrations/health` | — | Disponibilidad/configuración segura sin endpoints ni credenciales. |
| `POST /api/admin/retention/run` | — | Ejecuta una limpieza de retención en Demo; `200` con conteos o `503` ProblemDetails si hubo fallos. |
| `GET /swagger` | — | OpenAPI; solo las operaciones de agente requieren el esquema Bearer `AgentToolBearer`. |

Las rutas desconocidas y los errores de negocio devuelven
`application/problem+json`. `POST /api/login` permanece ausente para no fingir
un flujo de autenticación. Inventario es de solo lectura.

## Facturas

`POST /api/invoices/upload` recibe un solo `file` y no acepta
`organizationId` ni `userId`. El backend valida extensión, MIME, firma,
integridad, contraseña, tamaño exactamente de 20 MiB (20,971,520 bytes) y
hasta 50 páginas. Copia el contenido en bloques a un archivo temporal y calcula
SHA-256 durante la misma lectura; no carga el PDF completo a un arreglo en
memoria. Busca el hash por organización antes de guardar el Blob o invocar
Document Intelligence, por lo que el mismo archivo no vuelve a analizarse.

Los PDFs se guardan en un contenedor sin acceso público. La extracción usa
`prebuilt-invoice`; los campos extraídos, confianza, advertencias y líneas se
validan antes de persistir. La segunda deduplicación usa RFC/proveedor
normalizado, número, fecha e importe. Los importes se almacenan como `decimal`
y las fechas de factura como `DateOnly`.

Si falla Blob Storage, no se crea la factura ni se llama a Document
Intelligence. Si falla Document Intelligence después de conservar el original,
la fila queda en `extraction_failed`, se conserva el PDF y se permite reintentar
con el Blob existente (máximo tres intentos). Si falla SQL tras subir el Blob,
la aplicación intenta borrar el Blob como compensación. Si esa compensación
también falla, se registra el tipo de excepción sin ruta Blob ni contenido y se
deja el error explícito en logs.

El vencimiento se fija exactamente a 60 días después de la carga. El proceso
automático revisa cada 24 horas, borra solamente el PDF y conserva la fila SQL.
`LegalHold` impide seleccionar y borrar ese PDF. El trabajo es idempotente:
usa borrado idempotente de Blob y marca la fecha de borrado únicamente tras la
operación. El endpoint manual de retención solo se registra en modo Demo y no
debe exponerse fuera de él. No existe una ruta de administración para cambiar
`LegalHold`.

Configuración opcional: `AzureStorage__ServiceUri` con identidad administrada,
o `AzureStorage__ConnectionString`; `AzureDocumentIntelligence__Endpoint` y,
solo para desarrollo, opcionalmente `AzureDocumentIntelligence__ApiKey`. Si
falta la configuración, la API inicia y devuelve un error controlado cuando se
usa la operación dependiente. No guardar claves ni cadenas de conexión en
`appsettings*.json`.

## Reportes

`GET /api/reportes` requiere `fromUtc` y `toUtc` válidos, en orden ascendente.
Los valores se normalizan a UTC. Respuesta:

```json
{
  "fromUtc": "2026-10-01T00:00:00Z",
  "toUtc": "2026-11-01T00:00:00Z",
  "processedInvoiceCount": 0,
  "duplicateInvoiceCount": 0,
  "duplicateRate": 0,
  "approvedTotalsByCurrency": [],
  "criticalInventoryItemCount": 0
}
```

- Los conteos de facturas filtran por `UploadedAtUtc` usando `[fromUtc, toUtc)`
  y `OrganizationId` del servidor.
- `processedInvoiceCount` cuenta filas con extracción exitosa.
- `duplicateInvoiceCount` cuenta filas marcadas como duplicadas. La tasa es
  ese conteo dividido entre todas las facturas del periodo; si no hay filas,
  vale `0`.
- `approvedTotalsByCurrency` suma `Total` de facturas aprobadas del periodo
  dentro de cada `CurrencyCode`; monedas diferentes nunca se suman entre sí.
- `criticalInventoryItemCount` cuenta el estado actual con
  `QuantityOnHand <= ReorderThreshold`; no es un valor histórico ni depende del
  intervalo.
- No se reportan conversaciones atendidas: no hay almacenamiento de chat.

## Agentes Microsoft Foundry

`POST /api/chat` limita el mensaje a 1–4000 caracteres e invoca la versión
configurada de `rio-claro-business-agent` con `DefaultAzureCredential`. La
respuesta mantiene `sources` como lista vacía porque esta integración aún no
extrae citas desde las llamadas a herramientas ni usa AI Search; no se fabrican
citas. La propiedad opcional heredada
`conversationId` se rechaza con `400 chat_conversation_not_supported`: la API no
persiste conversaciones ni mantiene memoria entre solicitudes.

`POST /api/invoices/{invoiceId}/explain` primero carga la factura desde SQL,
aplicando la organización fija del servidor, y envía al
`rio-claro-invoice-agent` solo los campos estructurados, confianza, advertencias
y coincidencias exactas. El RFC del proveedor, nombre original del PDF, ruta
Blob, binario y datos de otras organizaciones no se incluyen. Los textos de
campos y advertencias se delimitan como datos no confiables; el prompt del
agente prohíbe obedecer instrucciones embebidas. La explicación nunca se
persiste ni cambia el estado de revisión.

Las herramientas externas `/api/agent-tools/v1/*` requieren una clave de al
menos 32 caracteres mediante `AgentTools__ApiKey`. La comparación se realiza
en tiempo constante. La misma clave debe guardarse como secreto en la conexión
de Foundry `rio-claro-agent-tools-auth`; `azure.yaml` contiene solo la
referencia `${AGENT_TOOLS_API_KEY}`, nunca el valor del secreto. Si falta la
clave, las herramientas fallan cerradas con `503 agent_tools_not_configured`;
una clave inválida devuelve `401 agent_tool_unauthorized`. La API solo acepta
operaciones GET, aplica topes de paginación y jamás recibe `organizationId`
como parámetro.
El modelo no recibe RFC completo, clave, datos del Blob ni configuración del
proveedor.

`GET /api/agents/health` consulta las versiones exactas configuradas usando
`AgentAdministrationClient`; el `GET /health` básico sigue siendo independiente
y no consulta Azure. En salud, fallas de autenticación, ausencia de versión,
error del proveedor o timeout se reportan como `Unhealthy` sin exponer detalles.

Configuración del backend mediante variables de entorno/App Settings:

| Clave | Propósito |
|---|---|
| `Foundry__ProjectEndpoint` | Endpoint HTTPS del proyecto Foundry. |
| `Foundry__BusinessAgentName` / `Foundry__BusinessAgentVersion` | Identidad inmutable de la versión del agente empresarial. |
| `Foundry__InvoiceAgentName` / `Foundry__InvoiceAgentVersion` | Identidad inmutable de la versión del agente de facturas. |
| `Foundry__ModelDeploymentName` | Nombre público del deployment usado para diagnóstico. |
| `Foundry__RequestTimeoutSeconds` | Timeout por solicitud/sondeo, entre 1 y 120 segundos. |
| `Foundry__MaxResponseCharacters` | Máximo de caracteres aceptado, entre 1 y 16000. |
| `AgentTools__ApiKey` | Secreto compartido de las herramientas; mínimo 32 caracteres. |

Las opciones de Foundry son opcionales para iniciar la API. Sin endpoints o
versiones, el chat y la explicación devuelven un error explícito `503`; nunca se
responde con resultados simulados. Azure usa identidad administrada mediante
`DefaultAzureCredential`.

## Despliegue de los agentes

`azure.yaml` declara el proveedor `microsoft.foundry`, el proyecto, la conexión
de herramientas y ambos agentes; las instrucciones se encuentran en
[`foundry/agents/`](../../foundry/agents/) y el esquema OpenAPI de herramientas
en [`foundry/agent-tools.openapi.yaml`](../../foundry/agent-tools.openapi.yaml).
El deployment de modelo se elige durante el aprovisionamiento para no asumir
disponibilidad regional. La aplicación backend debe estar desplegada primero,
con `AgentTools__ApiKey` configurada y un endpoint HTTPS público accesible desde
Foundry; se usa el mismo valor en `AGENT_TOOLS_API_KEY` del entorno `azd`.

Se requiere Azure Developer CLI 1.36.0 o posterior con las extensiones de
Foundry compatibles. Completa los valores de [`../.env.example`](../../.env.example)
en el entorno local o el entorno `azd`, y ejecuta desde la raíz del repositorio:

```sh
./scripts/deploy-foundry-agents.sh
```

El script requiere confirmación escrita antes de `azd up`, que puede crear
recursos facturables. Después del despliegue, consulta los nombres y versiones
con `azd ai agent show business-agent` y `azd ai agent show invoice-agent`;
configura esos valores junto con el endpoint del proyecto en App Settings. Para
retirar los recursos del entorno, indica explícitamente
`RIO_CLARO_AZD_ENVIRONMENT` y ejecuta `./scripts/cleanup-foundry-demo.sh`; el
script requiere confirmación por nombre de entorno y no usa `--purge`.

## Migraciones y datos demo

El backend aplica migraciones al iniciar y después ejecuta un seeder idempotente
que crea únicamente la organización y el usuario demo. Las pruebas de API
aplican el esquema a una base SQLite en memoria inicialmente vacía y no
requieren Azure. El modelo de negocio usa relaciones compuestas para impedir
referencias de factura/inventario cruzadas entre organizaciones.

## Limitaciones y configuración pendiente

- No existe autenticación ni permisos por usuario; no usar con información
  real. El modo demo debe mantenerse fuera de producción.
- Completar e integrar un proveedor real de chat, recuperación autorizada,
  citas y evaluación antes de habilitar respuestas conversacionales.
- Añadir persistencia de conversaciones antes de calcular consultas atendidas.
- `LegalHold` se respeta, pero no existe una ruta para administrar el valor.
- La limpieza diaria puede borrar un PDF hasta casi 24 horas después del
  vencimiento; `/api/admin/retention/run` permite forzarla solo en Demo.
- No hay endpoints de login ni de escritura de inventario.
