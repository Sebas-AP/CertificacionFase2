# Backend Río Claro

Backend ASP.NET Core 8 organizado en proyectos de dominio, aplicación,
infraestructura y API. La ejecución disponible es una demostración pública con
organización y usuario fijos; no tiene autenticación y no debe exponerse como
servicio de producción ni usarse con datos reales.

## Requisitos

- .NET SDK 8.
- Una base Azure SQL vacía o existente y una identidad/cuenta con permisos para
  aplicar migraciones y ejecutar lecturas/escrituras.
- Para carga de facturas: una cuenta Azure Blob Storage privada y un recurso
  Azure AI Document Intelligence con el modelo `prebuilt-invoice`.
- Para la ejecución local, configurar `Frontend:Origin` con el origen exacto
  del frontend, sin ruta ni barra final.

## Restaurar, compilar y probar

Desde `backend/`:

```sh
dotnet restore RioClaro.sln
dotnet build RioClaro.sln --configuration Release
dotnet test RioClaro.sln --configuration Release
```

Las pruebas de API usan SQLite en memoria y no requieren servicios Azure.
Incluyen migración desde una base vacía, seeding, facturas, cargas, duplicados,
retención, reportes, errores y OpenAPI.

## Configurar y ejecutar

1. Crea una base de datos Azure SQL vacía. El backend aplica las migraciones y
   ejecuta un seeder idempotente al iniciar. El principal de conexión necesita
   permisos de esquema para las migraciones, además de lectura y escritura.
2. Configura `ConnectionStrings__RioClaro` y `Frontend__Origin` en variables
   de entorno o User Secrets. No agregues valores secretos a archivos del
   repositorio.
3. Para procesar facturas, asigna a la identidad administrada permisos mínimos
   para crear/verificar/escribir/borrar blobs privados y para llamar a Document
   Intelligence. Localmente puede usarse `AzureStorage__ConnectionString` y
   `AzureDocumentIntelligence__ApiKey` mediante User Secrets.
4. Desde `backend/`, inicia la API:

   ```sh
   dotnet run --project src/RioClaro.Api
   ```

   Si SQL o la configuración base falta, el proceso falla al iniciar. La falta
   de una configuración Azure opcional no impide el inicio: la operación
   correspondiente devuelve un error explícito.
5. Comprueba `GET /health`, abre `/swagger` y consulta la API en
   `http://localhost:5000` o el puerto que indique Kestrel.

Ejemplo de configuración sin valores reales:

```sh
ConnectionStrings__RioClaro='<connection string>' \
Frontend__Origin='http://localhost:3000' \
AzureStorage__ServiceUri='https://<account>.blob.core.windows.net' \
AzureDocumentIntelligence__Endpoint='https://<resource>.cognitiveservices.azure.com' \
dotnet run --project src/RioClaro.Api
```

Para desarrollo local, `dotnet user-secrets init --project src/RioClaro.Api`
y `dotnet user-secrets set <clave> <valor> --project src/RioClaro.Api` permiten
guardar la cadena y claves fuera del repositorio. Se prefiere identidad
administrada en Azure. La cuenta crea el contenedor si no existe y comprueba
que no tenga acceso público; si un contenedor ya existente es público, la carga
falla de forma segura.

`Application:Mode` debe ser `Demo` y `Application:RequireAuthentication`
`false`. El proyecto exige exactamente 20 MiB (20,971,520 bytes), 50 páginas y
60 días de retención. No cambies esos valores: el servicio falla al iniciar si
no coinciden. El proyecto apunta a `net8.0`; al desplegar, alinea con ese
framework el runtime de App Service. No uses `npm start` para el backend.

## Rutas

Las rutas de demostración no tienen autenticación de usuario; no expongas el
backend con datos reales. Las rutas `/api/agent-tools/v1/*` sí requieren un
Bearer key de herramienta, pero la identidad de negocio sigue siendo fija y
esto no sustituye autenticación de producción. El cliente/agente no puede
elegir `OrganizationId` ni `UserId`. Consulta contratos, errores y parámetros en
[docs/api-contracts.md](./docs/api-contracts.md).

| Método | Ruta | Comportamiento |
|---|---|---|
| `GET` | `/health` | Estado mínimo del proceso; no consulta dependencias. |
| `GET` | `/api/context` | Contexto fijo de demostración. |
| `GET` | `/api/facturas` | Historial paginado, filtrado por estado. |
| `GET` | `/api/facturas/{invoiceId}` | Detalle y auditoría de revisión. |
| `POST` | `/api/invoices/upload` | Valida, deduplica, guarda y analiza PDF. |
| `POST` | `/api/invoices/{invoiceId}/retry-extraction` | Reintenta con el Blob original; máximo tres intentos. |
| `PUT` | `/api/facturas/{invoiceId}/revision` | Corrige campos pendientes. |
| `POST` | `/api/facturas/{invoiceId}/confirmacion` | Aprueba una factura revisada. |
| `GET` | `/api/inventario` | Inventario paginado de SQL. |
| `GET` | `/api/inventario/{productId}` | Detalle de producto e inventario. |
| `GET` | `/api/reportes?fromUtc=&toUtc=` | KPIs desde SQL para un periodo semiabierto. |
| `POST` | `/api/chat` | Invoca `rio-claro-business-agent`; fallos de configuración/proveedor se devuelven con ProblemDetails. |
| `POST` | `/api/invoices/{invoiceId}/explain` | Explica campos y advertencias de una factura, sin modificarla. |
| `GET` | `/api/agents/health` | Verifica las versiones configuradas de Foundry. |
| `GET` | `/api/agent-tools/v1/*` | Herramientas GET protegidas para Foundry; se limitan a la organización del servidor. |
| `POST` | `/api/admin/retention/run` | Ejecuta limpieza manual solo en modo Demo. No exponer fuera de la demostración. |

## Agentes Microsoft Foundry

El proyecto raíz [azure.yaml](../azure.yaml) y las instrucciones en
[foundry/agents/](../foundry/agents/) declaran `rio-claro-business-agent` y
`rio-claro-invoice-agent`, además de una conexión Bearer para los endpoints
protegidos de solo lectura. La especificación está en
[foundry/agent-tools.openapi.yaml](../foundry/agent-tools.openapi.yaml).

Configura `Foundry__ProjectEndpoint`, nombres y versiones de agentes y
`AgentTools__ApiKey` mediante variables de entorno/App Settings. Usa la misma
clave en el entorno `azd` como `AGENT_TOOLS_API_KEY`. La clave debe tener al
menos 32 caracteres, nunca se incluye en los archivos versionados y no vuelve
a aparecer en respuestas o logs.

Desde la raíz del repositorio, después de desplegar y configurar el backend,
ejecuta `./scripts/deploy-foundry-agents.sh`. El script confirma antes de
`azd up` y exige azd 1.36.0 o posterior. Configura en App Service los números de
versión que muestra `azd ai agent show`. Para eliminación, fija
`RIO_CLARO_AZD_ENVIRONMENT` al entorno exacto y ejecuta
`./scripts/cleanup-foundry-demo.sh`; se pide una confirmación escrita y no se
usa purga de recursos recuperables.
| `GET` | `/swagger` | OpenAPI sin esquema de autenticación. |

`POST /api/login` y las escrituras de inventario permanecen ausentes: no se
simula autenticación ni se habilitan cambios sin permisos y auditoría acordados.

## Facturas, privacidad y retención

La carga acepta un único archivo `file` en `multipart/form-data`. El backend
valida tamaño, extensión, MIME, firma, integridad, contraseña y número de
páginas. Copia y calcula SHA-256 en bloques a un archivo temporal, comprueba el
hash antes de llamar a Document Intelligence y no carga el PDF completo en un
arreglo de memoria administrado por la aplicación. Los duplicados exactos no
vuelven a almacenarse ni analizarse. Los importes son `decimal` y las fechas
contables son `DateOnly`.

Si la extracción falla después de guardar el PDF, la factura y el original se
conservan con estado `extraction_failed` y pueden reintentarse. Si falla la
escritura SQL posterior al Blob, se intenta borrar el Blob como compensación.
Los PDFs se guardan en un contenedor privado. La limpieza automática revisa
cada 24 horas; `POST /api/admin/retention/run` permite ejecutarla en Demo. Se
conserva la fila de factura y se elimina solamente el Blob vencido. `LegalHold`
excluye la factura de la limpieza; la demo no ofrece una ruta para cambiar ese
indicador.

Los logs no incluyen PDF, claves, RFC completo, ruta Blob ni respuestas
completas de IA/proveedores. El contexto demo usa organización
`11111111-1111-1111-1111-111111111111` y usuario
`22222222-2222-2222-2222-222222222222`.

## Indicadores y límites de la demo

`GET /api/reportes` consulta SQL. El periodo aplica a `UploadedAtUtc` con
límites `[fromUtc, toUtc)`. El porcentaje de duplicados usa duplicados
registrados dividido entre todas las facturas cargadas en el intervalo y es
cero si no hay facturas. Los totales aprobados se agrupan por código de moneda,
sin sumarlos entre monedas. El inventario crítico es un conteo del estado
actual (`QuantityOnHand <= ReorderThreshold`) y no depende del periodo.
Conversaciones atendidas no se reportan porque el chat aún no persiste
conversaciones ni existe una integración real de búsqueda/generación.

El seeder solo crea la organización y el usuario demo; no agrega transacciones
ni métricas ficticias. Sin credenciales Azure, la carga puede persistirse con
extracción fallida y el chat responde `503`; el modo demo nunca significa que
la extracción o las respuestas de IA sean reales.
