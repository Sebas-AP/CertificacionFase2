---
applyTo: "**/*.tsx, **/*.jsx, **/src/components/**, **/src/app/**, **/app/**"
---

# Frontend, UX y accesibilidad

- Implementa en React/Next.js usando las convenciones, componentes, estilos y librerías que ya existan. No añadas Material UI u otra biblioteca si el repositorio no la usa y el cambio no la requiere.
- Mantén componentes de presentación separados de llamadas HTTP y lógica de negocio. Consume solo contratos REST tipados; nunca incluyas secretos, SDK Azure, conexión SQL o llamadas directas a servicios Azure en código cliente.
- Crea estados explícitos de carga, vacío, éxito y error; evita estados de éxito falsos. Muestra errores comprensibles, permite reintentar cuando sea seguro y previene envíos duplicados.
- En carga de facturas, comunica formatos y límites, progreso/estado, resultado de extracción, campos con baja confianza, errores y la acción necesaria para revisar/corregir. No marques como aprobados valores sugeridos por IA sin revisión cuando el flujo la requiera.
- En chat, muestra el indicador de actividad, historial, errores y fuentes provistas por la API. Distingue la respuesta de IA de las fuentes; no inventes citas ni presentes respuestas sin sustento como hechos.
- En el dashboard, etiqueta periodo, moneda, unidades y definición de cada KPI; indica cuándo no hay datos. No calcules métricas de negocio distintas a las definidas en el contrato compartido.
- Usa HTML semántico, navegación por teclado, foco visible, etiquetas asociadas, contraste suficiente y texto alternativo pertinente. No dependas solo del color para comunicar estado.
- Diseña para móvil y escritorio. Prueba formularios, tablas, paneles, carga y chat con contenido largo y errores, además del caso ideal.
- No almacenes tokens sensibles en `localStorage` ni registres datos de facturas/chat en consola. Gestiona sesiones mediante el mecanismo de autenticación acordado.
