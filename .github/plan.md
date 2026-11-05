
Como ambos son ingenieros informáticos y desarrolladores, la mejor estrategia es dividir el proyecto por  **capas de arquitectura** , evitando dependencias excesivas y permitiendo trabajar en paralelo desde el primer día.

# Distribución recomendada

## Integrante 1: Backend + IA + Azure

Responsable de toda la inteligencia del sistema y la infraestructura cloud.

### Módulo 1. Azure AI Foundry

**Tareas:**

* Crear el recurso Azure AI Foundry.
* Configurar proyecto.
* Crear los agentes IA.
* Diseñar prompts.
* Configurar evaluaciones del agente.

**Entregables:**

* Agente de Facturas.
* Agente de Consulta Empresarial.
* Agente Gerencial.

---

### Módulo 2. Azure OpenAI

**Tareas:**

* Configurar GPT-4o.
* Diseñar prompts del chatbot.
* Configurar respuestas contextuales.
* Implementar protección de prompts.

**Entregables:**

* Motor conversacional funcionando.

---

### Módulo 3. Azure AI Search (RAG)

**Tareas:**

* Crear índice.
* Cargar documentos de prueba.
* Configurar búsqueda semántica.
* Conectar con Azure OpenAI.

**Entregables:**

* Sistema de preguntas y respuestas sobre documentos.

---

### Módulo 4. Document Intelligence

**Tareas:**

* Crear servicio.
* Entrenar o configurar modelo de facturas.
* Extraer datos.
* Generar validaciones.

**Entregables:**

* API que recibe PDF.
* JSON estructurado con información de factura.

---

### Módulo 5. Base de Datos

**Tareas:**

* Diseñar modelo entidad-relación.
* Crear Azure SQL Database.
* Crear tablas.
* Crear SPs y consultas.

**Entregables:**

Usuarios

Facturas

Proveedores

Productos

Inventario

Consultas

Reportes

---

### Módulo 6. APIs

**Tareas:**

* Crear Backend (.NET o Node.js).
* Crear endpoints REST.
* Integrar IA y Base de Datos.

**Endpoints**

/api/login

/api/facturas

/api/chat

/api/reportes

/api/inventario

---

## Integrante 2: Frontend + UX + Presentación

Responsable de toda la experiencia de usuario y consumo de servicios.

### Módulo 1. Diseño UX/UI

**Tareas:**

* Crear wireframes.
* Diagrama de navegación.
* Paleta de colores.
* Identidad visual.

**Entregables**

* Mockups en Figma.

---

### Módulo 2. Portal Web

Tecnología sugerida:

React

Next.js

Material UI

**Tareas:**

* Login.
* Dashboard.
* Chat IA.
* Formularios.
* Reportes.

---

### Módulo 3. Pantalla de Facturas

**Tareas:**

* Subida de documentos.
* Vista previa PDF.
* Revisión de datos.
* Corrección manual.

**Pantallas**

Subir factura

Resultado extracción

Historial facturas

---

### Módulo 4. Chat Empresarial

**Tareas:**

* Crear interfaz tipo ChatGPT.
* Historial de conversaciones.
* Indicador de carga.
* Mostrar fuentes encontradas.

---

### Módulo 5. Dashboard Gerencial

**Tareas:**

* Gráficas.
* KPIs.
* Reportes.

Indicadores:

Facturas procesadas

Facturas duplicadas

Ventas

Inventario crítico

Consultas atendidas

---

### Módulo 6. Integración Frontend

**Tareas:**

* Consumir REST APIs.
* Manejar autenticación.
* Manejar errores.
* Responsive design.

---

# Flujo de trabajo recomendado

## Semana 1

### Integrante 1

* Azure AI Foundry
* Azure OpenAI
* Azure SQL

### Integrante 2

* Figma
* Diseño de pantallas
* Base del frontend

---

## Semana 2

### Integrante 1

* Document Intelligence
* Azure Search
* APIs

### Integrante 2

* Dashboard
* Chat
* Módulo de facturas

---

## Semana 3

### Ambos

Integración completa

Frontend

    ↓

Backend API

    ↓

Azure AI Foundry

    ↓

Azure OpenAI

    ↓

Azure Search

    ↓

SQL Database

---

## Semana 4

### Ambos

Pruebas

* Casos de uso.
* Corrección de errores.
* Presentación.

---

# Reparto de carga estimado

### Integrante 1 (50%)

* Azure AI Foundry (15%)
* Azure OpenAI (10%)
* Azure Search (10%)
* Document Intelligence (10%)
* Backend APIs (5%)

### Integrante 2 (50%)

* UX/UI (10%)
* Frontend React (20%)
* Dashboard (10%)
* Chat UI (5%)
* Integración y pruebas (5%)

# Resultado final

**Persona 1 (Arquitecto IA/Backend):**

* Azure AI Foundry
* OpenAI
* AI Search
* Document Intelligence
* SQL
* APIs

**Persona 2 (Frontend/Product Owner Técnico):**

* Figma
* React/Next.js
* Dashboard
* Chat
* Gestión documental
* Presentación y demo

Esta división permite que ambos trabajen simultáneamente casi desde el primer día y que la integración final sea relativamente sencilla.
