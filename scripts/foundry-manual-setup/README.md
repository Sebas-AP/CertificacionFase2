# Aprovisionamiento manual de agentes Foundry

`./scripts/deploy-foundry-agents.sh` (azd) está bloqueado por un bug confirmado de
Microsoft: el proveedor `microsoft.foundry` (probado en azd 1.36.0 y 1.37.2, las
dos versiones estables disponibles a 2026-11-03) genera una plantilla ARM interna
cuya variable `uniqueSuffix` usa `resourceGroup()` en un scope de suscripción,
donde esa función no es válida. azd no permite inspeccionar ni corregir esa
plantilla para este proveedor (`azd infra generate` devuelve "this project does
not contain any infrastructure to generate").

Mientras ese bug no se corrija, el despliegue de los agentes se hace en dos pasos:

1. **Infraestructura "core" de Foundry** (cuenta AIServices, modelo, proyecto,
   conexión `rio-claro-agent-tools-auth`, rol de la identidad del backend) —
   `infra/foundry-manual.bicep`, desplegado con `az deployment group create`.
2. **Los dos agentes** (`rio-claro-business-agent`, `rio-claro-invoice-agent`) —
   este script, que usa directamente el mismo paquete NuGet
   (`Azure.AI.Projects.Agents`) que el backend, sin pasar por azd.

El script lee las instrucciones y el spec OpenAPI de `foundry/agents/*.yaml` y
`foundry/agent-tools.openapi.yaml` (fuente única de verdad, igual que el flujo de
azd), así que no duplica ese contenido.

## Prerrequisitos

- `az login` ya hecho. El script usa `DefaultAzureCredential`, que recoge la
  sesión de Azure CLI.
- Tu cuenta necesita permisos de administrador sobre el recurso Foundry
  (el rol de runtime "Foundry User"/"Azure AI User" que usa la identidad
  administrada del backend no basta para crear agentes).
- El backend ya debe estar desplegado y accesible en `AGENT_TOOLS_BASE_URL`.

## Uso

```bash
export FOUNDRY_PROJECT_ENDPOINT="https://<account>.cognitiveservices.azure.com/api/projects/<project>"
export FOUNDRY_CONNECTION_ID="/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>/connections/rio-claro-agent-tools-auth"
export FOUNDRY_MODEL_DEPLOYMENT_NAME="gpt-5-mini"
export AGENT_TOOLS_BASE_URL="https://<backend>.azurewebsites.net"

dotnet run --project scripts/foundry-manual-setup
```

Cada ejecución crea una **nueva versión** del agente (el modelo es append-only:
nombre + versión). La salida imprime los números de versión creados — cópialos a
los app settings del backend (`Foundry__BusinessAgentVersion`,
`Foundry__InvoiceAgentVersion`).

## Región

Despliega la cuenta Foundry (`infra/foundry-manual.bicep`) en la **misma región**
que el App Service del backend. La llamada que Foundry hace a la herramienta
OpenAPI (`AGENT_TOOLS_BASE_URL`) es sensible a la latencia entre regiones; en
pruebas, tenerlas en regiones distintas (Foundry en `centralus`, backend en
`canadacentral`) causó fallos intermitentes (`chat_provider_unavailable` o una
respuesta con el texto `(remote call in progress)`) en la mayoría de las
consultas que requerían invocar la herramienta.

## Inestabilidad conocida de la herramienta OpenAPI

Incluso con Foundry y el backend en la misma región, la invocación de la
herramienta OpenAPI vía conexión de proyecto (`OpenApiProjectConnectionAuthenticationDetails`)
sigue siendo intermitente a 2026-11-03: un chat sin necesidad de herramienta
(saludo, pregunta general) responde bien de forma consistente, pero una
consulta que requiere llamar la herramienta (facturas, inventario) falla una
parte significativa de las veces con `chat_provider_unavailable`, sin relación
aparente con la versión del agente ni con qué operación específica se invoca.
Esto se comportó igual en dos regiones distintas y con dos proyectos Foundry
distintos, por lo que parece una limitación de la plataforma (la función es
muy nueva) y no algo corregible desde este repositorio. Si Microsoft la
estabiliza, debería dejar de ser necesario mencionar esto aquí.

## Revisar si el bug de azd ya se corrigió

```bash
azd version
azd up --no-prompt
```

Si `azd up` ya no falla con `InvalidTemplate`/`RESOURCEGROUP function not
expected`, el flujo original (`scripts/deploy-foundry-agents.sh`) vuelve a ser
válido y este script manual deja de ser necesario para desplegar desde cero
(sigue siendo útil para iterar sobre instrucciones/versiones sin recrear toda la
infraestructura).
