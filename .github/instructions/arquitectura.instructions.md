---
applyTo: "**"
---

# Arquitectura y coordinación

- Lee primero `.github/copilot-instructions.md` y las instrucciones específicas aplicables. Inspecciona la estructura real del repositorio antes de proponer rutas o añadir archivos.
- Conserva las capas: UI React/Next.js → API REST → servicios de aplicación/orquestación → adaptadores Azure y repositorios → Azure AI Foundry, OpenAI, AI Search, Document Intelligence y Azure SQL.
- Mantén dependencias unidireccionales: UI → contratos de API; orquestación → interfaces de integración; implementaciones Azure/SQL detrás de adaptadores. No importes SDK de Azure ni módulos SQL en componentes cliente.
- Separa los tres agentes por responsabilidad: facturas (extracción y validaciones), consulta empresarial (preguntas con recuperación autorizada) y gerencial (síntesis de métricas disponibles). Comparte utilidades cuando tengan el mismo comportamiento; no dupliques lógica ni mezcles permisos.
- Antes de implementar una integración, fija su contrato: entrada/salida tipada, errores, timeout, reintentos seguros, límites y trazabilidad. No ocultes indisponibilidad de Azure mediante respuestas simuladas o valores vacíos.
- Acordad temprano contratos REST y esquemas compartidos. Usa fixtures con datos claramente ficticios para desbloquear el trabajo paralelo de interfaz, sin presentar fixtures como resultados reales.
- Implementa por fases: contratos y estructura común; base UX/API; extracción/RAG/SQL; integración de extremo a extremo; evaluación, pruebas, correcciones y demo. Mantén separables y desplegables las capas.
- Si una decisión de producto, persistencia, identidad o proveedor no está especificada, señala la decisión pendiente y evita introducir una alternativa incompatible. No agregues complejidad de producción que no exija el alcance académico.

## Contratos de API a definir

Documenta cada ruta en un lugar compartido y mantenla alineada con su consumidor:

| Ruta | Responsabilidad |
|---|---|
| `POST /api/login` | Iniciar o completar el flujo de autenticación configurado; no crear autenticación ficticia. |
| `/api/facturas` | Cargar, revisar, consultar historial y estado de facturas. |
| `POST /api/chat` | Recibir el mensaje y contexto conversacional autorizado; retornar respuesta y fuentes disponibles. |
| `/api/reportes` | Retornar métricas agregadas con periodo y definiciones explícitas. |
| `/api/inventario` | Consultar y, si el alcance lo permite, actualizar inventario autorizado. |

Define método, esquema, autorización, códigos de estado y formato de error para cada operación. Usa IDs estables, fechas ISO 8601 y cantidades monetarias con moneda explícita. No filtres datos de otros usuarios/organizaciones.
