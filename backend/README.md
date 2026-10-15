# Backend Río Claro

Base ASP.NET Core 8 de demostración sin autenticación, organizada en proyectos de
dominio, aplicación, infraestructura y API. En este checkout no existe el backend
anterior ni están implementados los módulos de facturas, Blob Storage, Document
Intelligence, deduplicación, retención o indicadores; este scaffolding no los
simula.

## Ejecutar

Requiere .NET 8 y una base Azure SQL configurada mediante variables de entorno:

```sh
ConnectionStrings__RioClaro='<connection string>' \
Frontend__Origin='http://localhost:3000' \
dotnet run --project src/RioClaro.Api
```

No guardes credenciales en `appsettings.json`. `Frontend__Origin` debe contener
un único origen absoluto, sin ruta ni barra final. El valor versionado permite
ejecutar el frontend local; en producción, configura el origen público real en
App Settings. CORS no acepta otros orígenes y no habilita credenciales.

Al iniciar el host, un servicio de inicio aplica las migraciones y luego ejecuta
el seeder idempotente.
La organización y el usuario demo usan IDs fijos. Configuración de demostración:

- `Application:Mode`: `Demo`
- `Application:RequireAuthentication`: `false`

La aplicación falla al iniciar si se cambia alguno de esos valores, si falta la
conexión a SQL o si el origen CORS no es válido.

## Rutas disponibles

| Método | Ruta | Respuesta | Acceso |
|---|---|---|---|
| `GET` | `/health` | `{ "status": "ok" }` | Sin token |
| `GET` | `/api/context` | modo, organización y usuario demo | Sin token |
| `GET` | `/api/facturas` | Historial paginado y filtrable por estado | Sin token, solo demo |
| `GET` | `/api/facturas/{invoiceId}` | Detalle y auditoría de revisión | Sin token, solo demo |
| `PUT` | `/api/facturas/{invoiceId}/revision` | Corrección de campos extraídos pendientes | Sin token, solo demo |
| `POST` | `/api/facturas/{invoiceId}/confirmacion` | Aprobar factura revisada | Sin token, solo demo |
| `GET` | `/api/inventario` | Inventario paginado, filtrable por nombre/SKU | Sin token, solo demo |
| `GET` | `/api/inventario/{productId}` | Detalle de producto e inventario | Sin token, solo demo |
| `GET` | `/swagger` | Documentación OpenAPI sin esquema de autenticación | Sin token |

Los contratos propuestos para las superficies de negocio pendientes están en
[docs/api-contracts.md](./docs/api-contracts.md). Esos contratos son una guía
de implementación para la carga/extracción de facturas, chat y reportes. Las
consultas/revisión de facturas y la consulta de inventario ya están
implementadas solo para demo; todavía no existe carga de PDF ni escrituras de
inventario.

El flujo de revisión de facturas solo actualiza los campos existentes de una
factura en `pending_review`; guarda usuario demo, fecha UTC, acción y lista de
campos modificados en `InvoiceReviewHistory`. La confirmación requiere número,
fecha, código de moneda de tres letras y total. Ningún endpoint de carga acepta
PDF actualmente; el límite acordado de 20 MB se aplicará cuando se implemente
la integración real.

No se exponen rutas de login, registro, invitación u organización. El frontend
no envía IDs de organización ni usuario; `/api/context` los obtiene del contexto
fijo del servidor. El mismo singleton `ICurrentUserContext` debe inyectarse en
servicios HTTP y trabajos en segundo plano que se añadan en el futuro.

El proyecto actual apunta a `net8.0`. Antes de desplegar, alinea el runtime de
Azure App Service con ese `TargetFramework` y comprueba el soporte vigente; el
backend no se inicia con `npm start`.

## Validación

Desde `backend/`:

```sh
dotnet test RioClaro.sln
dotnet build RioClaro.sln --configuration Release
```

Las pruebas usan SQLite en memoria y verifican endpoints sin token, OpenAPI sin
esquemas de seguridad, CORS restringido también en producción, migración/seeding
al arranque, persistencia del modelo de negocio, aislamiento de organización en
inventario/facturas, paginación y validación de filtros, revisión/aprobación de
facturas con auditoría, GUID fijos, idempotencia del seeder y registro singleton.
