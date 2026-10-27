# Deployment Readiness

## Fecha

2026-10-26T17:27:37Z

## Sistema

- CachyOS Linux 4.0.6
- x86_64
- shell principal: fish
- usuario: sebastian
- sudo disponible: sí

## Azure Developer CLI

- versión anterior: 1.25.6
- ruta anterior: /usr/bin/azd
- paquete propietario: azure-dev-cli-bin
- paquete versión: 1.25.6-1
- versión activa: 1.37.1
- ruta activa: /home/sebastian/.local/bin/azd
- ejecutable efectivo: /home/sebastian/.local/bin/azd-linux-amd64
- resultado en fish: azd version 1.37.1
- resultado en bash: azd version 1.37.1
- resultado en bash sin perfil: azd version 1.37.1
- riesgo residual: hay una instalación antigua del sistema en /usr/bin/azd, pero la PATH del usuario prioriza $HOME/.local/bin; la configuración efectiva no usa la versión antigua. La duplicada no se elimina en esta tarea porque el sistema la mantiene como paquete del sistema.

## Azure CLI

- versión: no instalado
- ruta: no disponible
- estado sesión: no verificado con `az account show` porque `az` no existe
- estado de azd auth: `Logged in to Azure as almedasebastian412@hotmail.com`
- suscripción: no verificada localmente
- tenant: no verificado localmente
- bloqueo real: falta Azure CLI para validar recursos reales y permisos de Azure

## Foundry CLI

- `azd ai --help` disponible
- `azd ai agent --help` disponible
- comandos relevantes: `agent`, `connection`, `inspector`, `project`, `toolbox`, `init`, `invoke`, `show`, `doctor`, `monitor`, `eval`, `sessions`, `endpoint`, `run`, `sample`
- extensión instalada: `azure.ai.agents` versión 1.0.0-beta.21
- `azd extension list` muestra las extensiones de Foundry que el proyecto puede usar; no es necesario instalar extensiones no oficiales
- la configuración del repositorio usa `azd ai agent` y `azd up` con comprobaciones de versión y de secretos, pero no se ejecutó ninguna provisión real

## Repositorio

- rama: no se validó como rama activa en la salida de git; el estado operativo se revisó con git status y git diff --check
- commit: git log -1 --oneline se revisó y la salida no fue bloqueante para esta validación
- estado de Git: hay cambios previos del repositorio, pero no se revirtieron; no se tocaron archivos que ya estaban correctos
- archivos analizados: azure.yaml, foundry/agent-tools.openapi.yaml, foundry/agents/business.agent.yaml, foundry/agents/invoice.agent.yaml, scripts/deploy-foundry-agents.sh, .env.example, backend/README.md, backend/docs/api-contracts.md
- archivos creados en esta validación: DEPLOYMENT-READINESS.md

## Seguridad

- no se encontró un valor literal real de `AGENT_TOOLS_API_KEY` en el repositorio
- el patrón seguro se usa en azure.yaml y scripts/deploy-foundry-agents.sh: `${AGENT_TOOLS_API_KEY}`
- la clave debe venir del entorno del usuario o de un secreto seguro de azd; no se documenta ni se registra el valor real
- comprobación de secretos: `git grep -n 'AGENT_TOOLS_API_KEY'` mostró solo referencias a variables y documentación; no hubo valor real
- referencia de seguridad en OpenAPI: `Authorization: ******` y `AgentToolBearer` solo aparecen como placeholders documentados, no como secretos reales
- `git diff --check` no reportó errores

## Agentes

### rio-claro-business-agent

- nombre: rio-claro-business-agent
- tipo: prompt agent
- modelo: `${AZURE_AI_MODEL_DEPLOYMENT_NAME}`
- herramientas: OpenAPI de solo lectura apuntando a `rio_claro_read_only`
- conexión: `rio-claro-agent-tools-auth`
- estado de validación: perfilado por configuración del repositorio; no se desplegó ni se invocó

### rio-claro-invoice-agent

- nombre: rio-claro-invoice-agent
- tipo: prompt agent
- modelo: `${AZURE_AI_MODEL_DEPLOYMENT_NAME}`
- instrucción: explicar facturas sin modificar contenido ni atributos
- estado de validación: configuración revisada, no desplegada

## Pruebas

- backend restore/build/test: ejecutado
- resultado actual: 44/44 correctas, 0 fallidas
- no se ejecutó azd up ni azd down
- no se modificó infraestructura ni recursos Azure
- no se ejecutaron validaciones de despliegue real ni what-if

## Infraestructura

Los recursos previstos por azd y el proyecto son, a grandes rasgos:

- proyecto Microsoft Foundry
- conexión de herramientas `rio-claro-agent-tools-auth`
- agente `rio-claro-business-agent`
- agente `rio-claro-invoice-agent`
- modelo Azure AI deployment apuntado por `AZURE_AI_MODEL_DEPLOYMENT_NAME`
- backend HTTPS público, configurado con App Service o equivalente, para exponer `AGENT_TOOLS_BASE_URL`
- dependencia de configuración de agente: `AGENT_TOOLS_API_KEY` con longitud mínima de 32 caracteres
- no se identificó Bicep ni Terraform en el repositorio raíz; la infraestructura se declara en `azure.yaml` y en agentes/ficheros de Foundry del proyecto
- sin Azure CLI, la infraestructura real no se puede validar beyond local config checks

## Variables necesarias

- `AGENT_TOOLS_BASE_URL`: Present, debe establecerse con la URL pública del backend desplegado
- `AGENT_TOOLS_API_KEY`: Present en el entorno del usuario, no versionado; debe cumplir longitud mínima de 32 caracteres
- `AZURE_LOCATION`: Missing/Not Applicable hasta que se conozca el entorno Azure real
- `AZURE_AI_MODEL_DEPLOYMENT_NAME`: Missing/Not Applicable hasta el entorno de despliegue Azure
- `Foundry__ProjectEndpoint`, `Foundry__BusinessAgentName`, `Foundry__BusinessAgentVersion`, `Foundry__InvoiceAgentName`, `Foundry__InvoiceAgentVersion`, `Foundry__ModelDeploymentName`, `AgentTools__ApiKey`: Not Applicable hasta que exista despliegue real del backend

## Bloqueos

- no hay Azure CLI instalado; un despliegue real o validación de recursos Azure no es posible desde este entorno
- la instalación antigua de azd en /usr/bin/azd existe, pero no tiene efecto sobre la PATH efectiva actual; se documenta como riesgo residual, no como bloqueo inmediato
- no existe una sesión Azure verificada a través de `az account show` porque `az` no está instalado
- no se ejecutaron despliegues reales ni validaciones de infraestructura en Azure, por requisito

## Estado final

BLOCKED_BY_CONFIGURATION

## Próximo comando

No se ejecuta. El siguiente comando recomendado, una vez se tenga Azure CLI y que el usuario quiera continuar en un entorno autenticado, es:

az login

## Limpieza

No se ejecuta. El comando esperado para limpiar recursos Azure solo se ejecutaría en un entorno autorizado y claramente confirmado, y no debe ejecutarse durante esta revisión.

## Conclusión corta

El repositorio ya cumple la parte de la validación local del proyecto: azd está actualizado a 1.37.1 y la configuración de scripts exige 1.36.0+, la clave de agente se maneja como variable de entorno, y la suite de backend sigue pasando 44/44. El bloqueo real es de configuración del entorno Azure: falta Azure CLI y no se verifican permisos ni infraestructura real en Azure desde este entorno.
