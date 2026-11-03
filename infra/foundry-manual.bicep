// Usa la MISMA región que el App Service del backend (appServiceLocation en main.bicep):
// la llamada de la herramienta OpenAPI del agente hacia el backend es sensible a la
// latencia entre regiones y puede fallar intermitentemente si quedan en regiones distintas.
param location string = 'canadacentral'
param accountName string = 'rioclarofoundry01'
param projectName string = 'rioclaro-project'
param modelDeploymentName string = 'gpt-5-mini'
param modelName string = 'gpt-5-mini'
param modelVersion string = '2025-08-07'
param agentToolsBaseUrl string
@secure()
param agentToolsApiKey string
param backendPrincipalId string

resource foundryAccount 'Microsoft.CognitiveServices/accounts@2026-11-15-preview' = {
  name: accountName
  location: location
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: accountName
    publicNetworkAccess: 'Enabled'
    allowProjectManagement: true
    disableLocalAuth: false
  }
}

resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2026-11-15-preview' = {
  parent: foundryAccount
  name: modelDeploymentName
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2026-11-15-preview' = {
  parent: foundryAccount
  name: projectName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: 'Rio Claro Project'
    description: 'ClaroGestion AI demo project'
  }
}

resource agentToolsConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2026-11-15-preview' = {
  parent: project
  name: 'rio-claro-agent-tools-auth'
  properties: {
    category: 'CustomKeys'
    target: agentToolsBaseUrl
    authType: 'CustomKeys'
    credentials: {
      keys: {
        Authorization: 'Bearer ${agentToolsApiKey}'
      }
    }
    isSharedToAll: true
  }
}

var azureAIUserRoleId = '53ca6127-db72-4b80-b1b0-d745d6d5456d'

resource backendRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, backendPrincipalId, azureAIUserRoleId)
  scope: foundryAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', azureAIUserRoleId)
    principalId: backendPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output accountEndpoint string = foundryAccount.properties.endpoint
output projectEndpoint string = '${foundryAccount.properties.endpoint}api/projects/${projectName}'
