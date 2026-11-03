# Estado de implementación del frontend

## Arquitectura encontrada

Al iniciar la tarea, `frontend/` estaba vacío y no existían `package.json`,
lockfile, configuración de Next, componentes ni rutas rastreados. Solo había
artefactos compilados antiguos en `.next/` y dependencias instaladas sin un
manifiesto. Se estableció el App Router de Next.js con Node.js 26.10.0, npm
11.19.1, Next.js 16.4.4, React 19.3.0 y TypeScript 5.9.3. Se conservaron
Next.js/React y no se añadió una biblioteca de componentes.

El agente `.github/agents/frontend-ui-ux.agent.md` y las skills locales de
integración de contratos, Playwright y accesibilidad indicadas en la solicitud
no existen en este repositorio. Se siguieron las instrucciones generales de
frontend y arquitectura.

La validación inicial de scripts no fue posible: no había manifiesto ni scripts
frontend y `npm run` terminaba con `ENOENT` por falta de `package.json`. El
frontend se creó dentro del directorio vacío `frontend/`, sin tocar las
modificaciones previas en `.github/agents/arquitecto.agent.md`,
`.github/copilot-instructions.md` ni `.github/image/`.

## Inventario y matriz

| Superficie | Ruta | Estado actual | Endpoint requerido | Estado endpoint | Tarea pendiente | Prioridad | Prueba necesaria |
|---|---|---|---|---|---|---:|---|
| Shell y navegación | Todas; `/` redirige | Complete | No requerido para identidad demo | NotApplicable | Ninguna | 1 | Inicio sin login, navegación por teclado y menú móvil |
| Dashboard | `/dashboard` | Partial | `GET /api/reportes?fromUtc=&toUtc=` | Complete | Añadir series/comparativos solo si el backend los define | 2 | Datos, vacío, error 503, UTC y responsive |
| Listado de facturas | `/facturas` | Partial | `GET /api/facturas?page=&pageSize=&estado=` | Complete | Búsqueda, proveedor, fechas y ordenamiento requieren soporte backend | 3 | Filtro real, paginación, vacío y responsive |
| Carga de factura | `/facturas/nueva` | Complete | `POST /api/invoices/upload` | Complete | Configurar Blob/Document Intelligence para el procesamiento real | 4 | PDF seleccionado, límite/tipo, éxito API y duplicado |
| Detalle y revisión | `/facturas/{id}` | Partial | `GET /api/facturas/{id}`, `PUT /api/facturas/{id}/revision`, `POST /api/facturas/{id}/confirmacion`, `POST /api/invoices/{id}/retry-extraction`, `POST /api/invoices/{id}/explain` | Complete | Preview/descarga segura requiere endpoint backend; Foundry requiere configuración | 5 | Carga, edición, aprobación, reintento fallido, accesibilidad |
| Coincidencias/decisión de duplicado | `/facturas/{id}` | BlockedByBackend | Endpoint público de coincidencias y decisión humana | BlockedByBackend | Definir contrato público para coincidencias y revisión; no exponer clave de herramientas | 6 | Confirmar que no se ofrecen acciones no soportadas |
| Chat empresarial | `/chat` | Partial | `POST /api/chat` | Complete; Foundry puede no estar configurado | Persistencia de conversaciones/fuentes depende del backend y el agente | 7 | Respuesta con red mock, 503, fuentes seguras y teclado |
| Reportes | `/reportes` | Partial | `GET /api/reportes?fromUtc=&toUtc=` | Complete | Exportación CSV requiere endpoint backend | 8 | Periodos, validación, error, vacío y ausencia de export falso |
| Estado del sistema | `/estado` | Partial | `GET /health`, `GET /api/agents/health` | Complete | Estado Blob/Document Intelligence requiere endpoint seguro apto para el navegador | 9 | API/Foundry disponibles o no configurados |
| Accesibilidad | Todas | Complete | No aplica | NotApplicable | Auditoría manual completa pendiente fuera de las rutas y estados probados | 10 | Axe: cero errores críticos/serios en rutas principales; teclado/E2E |
| Responsive | Dashboard, facturas, detalle, chat y reportes | Complete | No aplica | NotApplicable | Ninguna conocida | 10 | 360, 768, 1024 y 1440 px; sin overflow global |
| Fixtures | Runtime de todas las rutas | Complete | No aplica | NotApplicable | Mantener datos ficticios únicamente dentro de pruebas | 10 | E2E intercepta API; runtime no tiene mocks |

Estados permitidos: Complete, Partial, Missing, BlockedByBackend,
BlockedByConfiguration y NotApplicable.

## Rutas y contratos

- `/` redirige a `/dashboard`; no hay login, registro, selección de
  organización ni autenticación ficticia.
- El cliente central está en `src/lib/api.ts`. Usa
  `NEXT_PUBLIC_API_BASE_URL`, `AbortSignal`, JSON/multipart, errores seguros y
  referencias `traceId`. No incluye credenciales ni reintentos automáticos.
- El listado solo envía estado/página porque el endpoint no admite búsqueda,
  fechas, proveedor ni ordenamiento configurable.
- Las métricas se obtienen del endpoint de reportes. La tasa se muestra como
  proporción reportada por la API; los totales permanecen separados por moneda.
- El estado de integración del navegador consulta solo las rutas públicas de
  API y agentes. No llama `GET /api/agent-tools/v1/integrations/health`, porque
  requiere una clave que no debe exponerse al cliente.
- El frontend no descarga directamente de Blob Storage. La falta de una ruta
  de documento seguro y de exportación CSV se indica en la interfaz.

## Bloqueos de contrato/configuración

**Backend:** no hay endpoint seguro de lectura del PDF, exportación CSV,
series temporales, filtros adicionales de facturas ni operaciones para decidir
coincidencias de duplicado. Las funciones relacionadas quedan explícitamente
marcadas y no se simulan.

**Configuración:** Foundry todavía no está desplegado. Blob Storage y Document
Intelligence requieren configuración antes de que las operaciones dependientes
puedan procesar PDFs. La interfaz muestra errores/estados reales del backend y
no intenta sustituciones locales.

## Validación ejecutada

La suite E2E intercepta la red con respuestas ficticias tipadas para las
pruebas; no habilita mocks en el runtime. Se requiere Node.js 22.6+ para el
runner de pruebas integrado con el runtime de tipos de Node.

| Comando | Resultado |
|---|---|
| `npm ci` | Correcto; lockfile reproducible |
| `npm run lint` | Correcto |
| `npm run typecheck` | Correcto; incluye app, pruebas E2E y configuración Playwright |
| `npm test` | 6/6 pruebas correctas |
| `npm run test:e2e -- --workers=4` | 16/16 pruebas correctas |
| `npm run build` | Correcto; rutas estáticas y detalle dinámico generados |
| Axe en rutas principales | 0 errores críticos o serios en dashboard, listado, carga, detalle, chat, reportes y estado |
| Responsive | Sin overflow global en 360, 768, 1024 y 1440 px en dashboard, facturas, detalle, chat y reportes |
| `npm audit --omit=dev` | 0 vulnerabilidades de dependencias de producción |
| `npm audit` completo | 10 avisos moderados en dependencias de desarrollo bajo `minimatch`; no se aplicó un `audit fix --force` que degradaría ESLint |
| `git diff --check` | Correcto |

La dependencia Next.js se actualizó a 16.4.4 porque la versión inicialmente
instalada estaba afectada por avisos críticos publicados por npm audit. Los
avisos restantes son de herramientas de desarrollo y no alcanzan dependencias
de producción.

## Estado final

**FRONTEND_PARTIAL** — las rutas y operaciones que el contrato backend soporta
están implementadas y verificadas; el preview/descarga PDF, CSV, funciones
avanzadas de filtrado y decisiones de duplicados quedan bloqueados por contrato
backend. Foundry/Blob/Document Intelligence también dependen de configuración
Azure externa y no se desplegaron ni modificaron recursos.
