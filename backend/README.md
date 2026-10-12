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
| `GET` | `/swagger` | Documentación OpenAPI sin esquema de autenticación | Sin token |

No se exponen rutas de login, registro, invitación u organización. El frontend
no envía IDs de organización ni usuario; `/api/context` los obtiene del contexto
fijo del servidor. El mismo singleton `ICurrentUserContext` debe inyectarse en
servicios HTTP y trabajos en segundo plano que se añadan en el futuro.

## Validación

Desde `backend/`:

```sh
dotnet test RioClaro.sln
dotnet build RioClaro.sln --configuration Release
```

Las pruebas usan SQLite en memoria y verifican endpoints sin token, OpenAPI sin
esquemas de seguridad, CORS restringido también en producción, migración/seeding
al arranque, GUID fijos, idempotencia del seeder y el registro singleton.
Las pruebas de asignación de `OrganizationId`/`UploadedByUserId` y de protección
de DTOs de factura requieren un módulo de facturas real, que no está incluido en
este alcance.
