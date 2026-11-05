# Manual de usuario y documentación funcional — ClaroGestion AI

Proyecto final de certificación. Este documento describe **qué hace** el
sistema y **cómo usarlo**. Para la documentación técnica de instalación y
configuración, consulta el [README.md](./README.md) y el
[backend/README.md](./backend/README.md).

## Tabla de contenido

1. [¿Qué es ClaroGestion AI?](#qué-es-clarogestion-ai)
2. [Acceso al sistema](#acceso-al-sistema)
3. [Dashboard](#1-dashboard)
4. [Gestión de facturas](#2-gestión-de-facturas)
5. [Chat empresarial](#3-chat-empresarial)
6. [Reportes](#4-reportes)
7. [Estado del sistema](#5-estado-del-sistema)
8. [Preguntas frecuentes](#preguntas-frecuentes)
9. [Limitaciones conocidas](#limitaciones-conocidas)
10. [Glosario](#glosario)

## ¿Qué es ClaroGestion AI?

ClaroGestion AI es una plataforma de demostración construida para **Río
Claro** que centraliza tres necesidades de negocio:

- **Revisión de facturas de proveedores**: subir el PDF, extraer sus datos
  automáticamente con IA y validarlos antes de aprobarlos.
- **Consulta empresarial conversacional**: preguntar en lenguaje natural por
  información del negocio a través de un agente de IA.
- **Indicadores gerenciales**: ver métricas de facturación e inventario por
  periodo.

El sistema combina una interfaz web (frontend) con una API (backend) que es
la única que se comunica con los servicios de Azure (base de datos,
almacenamiento, extracción de documentos y agentes de IA). El navegador nunca
accede directamente a esos servicios.

## Acceso al sistema

Esta es una demostración académica: **no existe pantalla de inicio de
sesión**. Al abrir la aplicación, la raíz (`/`) redirige automáticamente al
**Dashboard**, usando una organización y un usuario de demostración fijos
asignados por el backend.

La navegación principal (barra superior o menú, según el tamaño de
pantalla) da acceso a cinco secciones: **Dashboard**, **Facturas**, **Chat**,
**Reportes** y **Estado**.

## 1. Dashboard

**Ruta:** `/dashboard`

Pantalla de inicio con un resumen gerencial: los indicadores clave del
negocio (ver [Reportes](#4-reportes)) y un acceso directo para **subir una
nueva factura**.

Si el backend no tiene datos aún o no puede responder, el panel muestra un
estado vacío o de error explícito — nunca inventa cifras.

## 2. Gestión de facturas

### 2.1 Listado de facturas

**Ruta:** `/facturas`

Muestra el historial de facturas cargadas, con:

- Filtro por estado: *Pendiente de revisión*, *Requiere validación*,
  *Extraída*, *Aprobada*, *Extracción fallida*, *Duplicada*.
- Paginación.
- Monto, moneda y fecha de cada factura.
- Acceso al detalle de cada una.

### 2.2 Subir una factura nueva

**Ruta:** `/facturas/nueva`

1. Arrastra el PDF al área de carga o selecciónalo desde el explorador de
   archivos.
2. El sistema valida en el navegador el tipo y tamaño del archivo antes de
   enviarlo (PDF, hasta el límite configurado — ver
   [backend/README.md](./backend/README.md)).
3. Al confirmar, el backend guarda el PDF, calcula su huella digital para
   detectar duplicados exactos y ejecuta la extracción automática de datos
   con Azure AI Document Intelligence.
4. Si la factura es un duplicado exacto de una ya existente, no se vuelve a
   almacenar ni a analizar.

### 2.3 Detalle y revisión de una factura

**Ruta:** `/facturas/{id}`

Aquí se revisa el resultado de la extracción automática:

- **Campos extraídos**: número de factura, fecha de emisión, moneda,
  subtotal, impuestos y total.
- **Editar y corregir**: si algún campo se extrajo mal o quedó incompleto,
  puede corregirse manualmente indicando un motivo del cambio (se guarda un
  historial de revisión).
- **Reintentar extracción**: si la extracción falló, puede reintentarse
  sobre el PDF original (hasta un máximo de tres intentos).
- **Aprobar**: una vez los datos son correctos, la factura se marca como
  aprobada.
- **Explicar factura**: solicita al agente de IA de facturas una explicación
  en lenguaje natural de los campos y advertencias detectadas, sin modificar
  la factura.

Si el agente de explicación de facturas no está configurado en el entorno
actual, la pantalla lo indica explícitamente en vez de simular una
respuesta.

## 3. Chat empresarial

**Ruta:** `/chat`

Conversación en lenguaje natural con el **agente de negocio** de Azure AI
Foundry para consultar información del negocio (por ejemplo, indicadores o
datos de facturación ya almacenados).

- Cada respuesta puede incluir **fuentes** (enlaces) que respaldan la
  información entregada; solo se muestran enlaces seguros (`http`/`https`).
- Si el agente de Foundry no está configurado o no está disponible, el chat
  informa el error en vez de responder con información inventada.
- El historial de la conversación vive únicamente en la sesión del
  navegador; no se persiste entre visitas.

## 4. Reportes

**Ruta:** `/reportes` (versión resumida también visible en el **Dashboard**)

Permite seleccionar un periodo (fecha inicial y final) y consultar, para ese
rango, los indicadores calculados por el backend directamente desde la base
de datos:

- Totales de facturas aprobadas, agrupados por moneda (no se suman monedas
  distintas entre sí).
- Porcentaje de facturas duplicadas detectadas en el periodo.
- Conteo de inventario en estado crítico (por debajo del punto de reorden),
  que refleja el estado actual y no depende del periodo seleccionado.

> La exportación a CSV **no está disponible** en esta versión: el contrato
> actual del backend no expone un endpoint para generarla, y la interfaz lo
> indica en vez de construir un archivo local que podría quedar desalineado
> con los datos reales.

## 5. Estado del sistema

**Ruta:** `/estado`

Panel de diagnóstico pensado para soporte técnico o para verificar antes de
una demo:

- **Estado de la API** (`/health`): confirma que el backend está activo.
- **Estado de los agentes de Foundry** (`/api/agents/health`): confirma si
  el agente de negocio y el de facturas están configurados y con qué
  versión. Los estados posibles son *Disponible*, *Degradado* y
  *No configurado*.

Este panel solo consulta rutas públicas; nunca expone claves ni información
sensible de la integración con Azure.

## Preguntas frecuentes

**¿Por qué no puedo iniciar sesión?**
Esta demostración usa una identidad y organización fijas para simplificar la
evaluación del proyecto; no implementa autenticación de usuarios.

**Subí una factura y quedó en "Extracción fallida". ¿Qué hago?**
Usa el botón de reintento en el detalle de la factura. Si Azure AI Document
Intelligence no está configurado en el entorno, la extracción seguirá
fallando hasta que se configure (ver [backend/README.md](./backend/README.md)).

**El chat responde con un error 503.**
Significa que el agente de negocio de Azure AI Foundry no está configurado o
no está disponible en este entorno. Revisa `/estado` para confirmarlo.

**¿Puedo descargar el PDF original de una factura desde la interfaz?**
No en esta versión; el contrato actual no incluye un endpoint de descarga o
previsualización segura del documento.

## Limitaciones conocidas

- Identidad y organización fijas: no hay registro, login ni multiusuario.
- Sin descarga/previsualización segura de PDF ni exportación CSV.
- El listado de facturas no admite búsqueda por proveedor, rango de fechas
  ni ordenamiento personalizado (solo filtro por estado y paginación).
- El inventario es de solo lectura; no se pueden registrar movimientos desde
  la interfaz.
- El chat no persiste el historial de conversaciones entre sesiones.
- La demo no agrega facturas ni métricas ficticias: si una pantalla aparece
  vacía, es porque no hay datos reales todavía, no un error.

## Glosario

| Término | Significado |
|---|---|
| **Factura pendiente de revisión** | Fue cargada y extraída, pero aún no fue aprobada por un usuario. |
| **Factura duplicada** | Su contenido (hash) coincide exactamente con una factura ya cargada; no se vuelve a procesar. |
| **Extracción fallida** | Azure AI Document Intelligence no pudo procesar el PDF; puede reintentarse. |
| **Agente de negocio** | Agente de Azure AI Foundry que responde preguntas de negocio en el chat. |
| **Agente de facturas** | Agente de Azure AI Foundry que explica el contenido de una factura ya extraída. |
| **Punto de reorden** | Umbral de inventario por debajo del cual un producto se considera crítico. |
