# ClaroGestion AI

Proyecto final de certificación — plataforma de gestión documental e indicadores
para **Río Claro**, con revisión asistida de facturas, consulta empresarial por
chat y reportes gerenciales, apoyada en agentes de IA sobre Azure.

> Demostración académica. Usa una identidad y organización de demostración fija,
> no implementa autenticación real y no debe usarse con datos de producción.

## Tabla de contenido

- [Arquitectura](#arquitectura)
- [Stack tecnológico](#stack-tecnológico)
- [Estructura del repositorio](#estructura-del-repositorio)
- [Puesta en marcha rápida](#puesta-en-marcha-rápida)
- [Documentación](#documentación)
- [Pruebas](#pruebas)
- [Despliegue en Azure](#despliegue-en-azure)
- [Límites conocidos de la demo](#límites-conocidos-de-la-demo)

## Arquitectura

```
┌─────────────────┐      REST/JSON      ┌───────────────────────┐
│  Frontend        │ ──────────────────▶ │  Backend               │
│  Next.js (React) │ ◀────────────────── │  ASP.NET Core 8        │
└─────────────────┘                      └───────────┬───────────┘
                                                      │
                     ┌────────────────────────────────┼────────────────────────────────┐
                     │                                │                                │
             ┌───────▼────────┐           ┌───────────▼───────────┐          ┌─────────▼─────────┐
             │ Azure SQL        │           │ Azure Blob Storage     │          │ Document Intelligence│
             │ (datos de negocio)│           │ (PDFs de facturas)      │          │ (extracción de datos) │
             └─────────────────┘           └───────────────────────┘          └───────────────────┘
                                                      │
                                           ┌───────────▼───────────┐
                                           │ Azure AI Foundry        │
                                           │ Agentes: negocio y facturas│
                                           └───────────────────────┘
```

El navegador solo habla con el backend; ninguna credencial ni recurso de Azure
se expone directamente al cliente. El backend orquesta la persistencia, el
almacenamiento de PDFs, la extracción de datos y la invocación de los agentes
de Foundry.

## Stack tecnológico

| Capa | Tecnología |
|---|---|
| Frontend | Next.js 16 (App Router), React 19, TypeScript |
| Backend | ASP.NET Core 8, Entity Framework Core |
| Base de datos | Azure SQL |
| Almacenamiento de documentos | Azure Blob Storage |
| Extracción de datos de facturas | Azure AI Document Intelligence (`prebuilt-invoice`) |
| Agentes conversacionales | Azure AI Foundry (agente de negocio y agente de facturas) |
| Infraestructura como código | Bicep (`infra/`), Azure Developer CLI (`azd`) |
| Pruebas | xUnit (backend), Node test runner + Playwright (frontend) |

## Estructura del repositorio

```
.
├── backend/          API ASP.NET Core 8 (Domain, Application, Infrastructure, Api, tests)
├── frontend/          Aplicación Next.js (dashboard, facturas, chat, reportes, estado)
├── foundry/           Definición de los agentes de Azure AI Foundry y su OpenAPI de herramientas
├── infra/             Plantillas Bicep para la infraestructura de Azure
├── scripts/           Scripts de despliegue y limpieza de los agentes Foundry
├── azure.yaml         Definición del proyecto para Azure Developer CLI (azd)
└── docs/              (en MANUAL.md) Documentación funcional y manual de usuario
```

Cada módulo principal tiene su propia documentación técnica:

- [backend/README.md](./backend/README.md) — cómo compilar, configurar y ejecutar la API.
- [backend/docs/api-contracts.md](./backend/docs/api-contracts.md) — contratos REST completos.
- [frontend/FRONTEND-IMPLEMENTATION-STATUS.md](./frontend/FRONTEND-IMPLEMENTATION-STATUS.md) — estado de cada pantalla y sus contratos.

## Puesta en marcha rápida

Requisitos: Node.js 22.6+ (validado con Node 26), npm y .NET SDK 8.

1. **Backend** — sigue [backend/README.md](./backend/README.md) para configurar
   la cadena de conexión a Azure SQL y levantar la API (por defecto en
   `http://localhost:5000`).
2. **Frontend**:

   ```sh
   cd frontend
   npm ci
   cp .env.example .env.local
   npm run dev
   ```

3. Abre `http://localhost:3000`. La raíz redirige al dashboard; no hay
   pantalla de login en esta demo.

Para el manual funcional completo (qué hace cada pantalla y cómo usarla),
consulta [MANUAL.md](./MANUAL.md).

## Documentación

| Documento | Contenido |
|---|---|
| [MANUAL.md](./MANUAL.md) | Manual de usuario y documentación funcional del sistema completo |
| [backend/README.md](./backend/README.md) | Guía técnica del backend: configuración, rutas, retención de facturas |
| [backend/docs/api-contracts.md](./backend/docs/api-contracts.md) | Contratos REST detallados |
| [frontend/FRONTEND-IMPLEMENTATION-STATUS.md](./frontend/FRONTEND-IMPLEMENTATION-STATUS.md) | Estado de implementación por pantalla |
| [DEPLOYMENT-READINESS.md](./DEPLOYMENT-READINESS.md) | Checklist de validación previa a despliegue en Azure |

## Pruebas

Backend (desde `backend/`):

```sh
dotnet restore RioClaro.sln
dotnet build RioClaro.sln --configuration Release
dotnet test RioClaro.sln --configuration Release
```

Frontend (desde `frontend/`):

```sh
npm run lint
npm run typecheck
npm test
npm run test:e2e
npm run build
```

Las pruebas E2E usan respuestas simuladas interceptadas por Playwright y no
requieren backend ni Azure. La primera ejecución necesita
`npx playwright install chromium`.

## Despliegue en Azure

El repositorio despliega los agentes de Azure AI Foundry mediante Azure
Developer CLI (`azd`) y `azure.yaml`, y provisiona infraestructura adicional
con las plantillas Bicep de `infra/`.

```sh
./scripts/deploy-foundry-agents.sh   # requiere azd 1.36.0+ y confirmación manual
./scripts/cleanup-foundry-demo.sh    # elimina los recursos de la demo
```

Variables de entorno necesarias en `azd env` o en la configuración de la app:
consulta [.env.example](./.env.example).

## Límites conocidos de la demo

- Identidad y organización fijas; no hay registro, login ni multiusuario.
- Sin descarga/preview seguro de PDF, exportación CSV ni búsqueda avanzada de facturas.
- El chat y la explicación de facturas dependen de que los agentes Foundry estén
  configurados; si no lo están, la interfaz lo indica explícitamente en vez de
  simular una respuesta.
- Inventario es de solo lectura.

Detalle completo en [MANUAL.md](./MANUAL.md#limitaciones-conocidas).
