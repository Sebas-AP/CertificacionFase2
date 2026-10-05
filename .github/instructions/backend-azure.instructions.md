---
applyTo: "**/app/api/**, **/pages/api/**, **/src/server/**, **/src/services/**, **/src/lib/azure/**"
---

# Backend, APIs e integraciones Azure

- Implementa las rutas REST en el servidor Next.js/API ya existente; conserva la convención de routing del proyecto. El navegador se comunica únicamente con estas APIs.
- Valida autenticación y autorización en el servidor para cada operación y recurso. Aplica mínimo privilegio y aislamiento de datos; ocultar controles en la UI no autoriza una petición.
- Valida cuerpos, parámetros, tipos MIME y tamaños de archivos en el límite de confianza. Rechaza entradas inválidas con errores accionables y no filtra stack traces ni información interna.
- Usa timeouts y manejo explícito de fallos de red/cuotas en dependencias Azure. Reintenta solo operaciones idempotentes o con una clave de idempotencia; no repitas cargas ni escrituras a ciegas.
- Lee configuración del entorno y falla con un error claro al iniciar si falta una configuración requerida. No uses valores de ejemplo como fallback en producción. Para App Service conserva `FOUNDRY_PROJECT_ENDPOINT` y `FOUNDRY_AGENT_NAME`; incorpora otras variables solo cuando una integración real las necesite y documenta su propósito, no su valor.
- Usa identidad administrada y credenciales de Microsoft Entra cuando estén disponibles. No codifiques claves, connection strings, tokens o endpoints privados en el código, frontend, logs ni archivos versionados.
- Usa SDK/API vigentes y soportados para Microsoft Foundry y Azure OpenAI. Comprueba la documentación oficial para la versión y el runtime antes de seleccionar paquetes o patrones; no copies clientes o APIs legacy sin una razón documentada.
- Mantén llamadas a Foundry/OpenAI, AI Search, Document Intelligence y SQL en adaptadores del servidor con tipos estables. La API debe traducir errores del proveedor a respuestas seguras y consistentes.
- Para `GET /health`, retorna un estado mínimo que confirme que el proceso responde. No reveles configuración ni consultes dependencias sensibles desde el health check básico. La verificación de despliegue usa `/health`.
- Los logs deben incluir correlación, operación, duración y resultado, pero nunca el PDF, contenido completo de prompts/respuestas, PII, secretos o credenciales. Redacta los datos sensibles antes de registrar.
