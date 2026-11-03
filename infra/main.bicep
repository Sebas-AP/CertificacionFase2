// Infraestructura "core" para el backend/frontend de RioClaro / ClaroGestion AI.
// Los agentes de Azure AI Foundry se aprovisionan por separado con `azd up`
// (ver azure.yaml y scripts/deploy-foundry-agents.sh) — este Bicep no los toca.
// Secretos y endpoints reales (connection strings, AgentTools__ApiKey, Frontend__Origin,
// Foundry__*) se configuran después del despliegue vía `az webapp config appsettings set`,
// nunca como parámetros de esta plantilla.

targetScope = 'resourceGroup'

@description('Región de Azure para todos los recursos.')
param location string = 'centralus'

@description('Región para el App Service Plan/App Service. Separada de "location" porque algunas suscripciones (p. ej. Azure for Students) tienen cupo 0 de App Service Basic en ciertas regiones permitidas.')
param appServiceLocation string = 'centralus'

@description('Prefijo usado para nombrar los recursos.')
param resourcePrefix string = 'rioclaro'

@description('SKU del plan de App Service (Linux).')
param appServicePlanSku string = 'B1'

@description('Login de administrador de Azure SQL.')
param sqlAdministratorLogin string

@secure()
@description('Password de administrador de Azure SQL.')
param sqlAdministratorPassword string

@description('SKU de la base de datos Azure SQL (serverless General Purpose por defecto).')
param sqlDatabaseSkuName string = 'GP_S_Gen5_1'

@description('SKU del plan de App Service (Linux) para el frontend. Deployado en el mismo plan que el backend para ahorrar costo.')
param frontendLinuxFxVersion string = 'NODE|22-lts'

@description('SKU del recurso de Azure AI Document Intelligence.')
param documentIntelligenceSku string = 'S0'

@description('Nombre del contenedor Blob privado para los PDF de facturas (debe coincidir con AzureStorage:InvoiceContainerName).')
param invoiceContainerName string = 'invoice-originals'

var uniqueSuffix = uniqueString(resourceGroup().id)
var storageAccountName = toLower('${resourcePrefix}st${uniqueSuffix}')
var sqlServerName = toLower('${resourcePrefix}-sql-${uniqueSuffix}')
var sqlDatabaseName = '${resourcePrefix}db'
var documentIntelligenceName = '${resourcePrefix}-doc-intel-${uniqueSuffix}'
var appServicePlanName = '${resourcePrefix}-plan'
var appServiceName = '${resourcePrefix}-api-${uniqueSuffix}'
var frontendAppServiceName = '${resourcePrefix}-web-${uniqueSuffix}'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource invoiceContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: invoiceContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource documentIntelligence 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: documentIntelligenceName
  location: location
  kind: 'FormRecognizer'
  sku: {
    name: documentIntelligenceSku
  }
  properties: {
    customSubDomainName: documentIntelligenceName
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorPassword
    version: '12.0'
  }
}

resource sqlFirewallAllowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: sqlDatabaseSkuName
    tier: 'GeneralPurpose'
  }
  properties: {
    autoPauseDelay: 60
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: appServiceLocation
  kind: 'linux'
  sku: {
    name: appServicePlanSku
  }
  properties: {
    reserved: true
  }
}

resource appService 'Microsoft.Web/sites@2023-01-01' = {
  name: appServiceName
  location: appServiceLocation
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
    }
  }
}

resource frontendAppService 'Microsoft.Web/sites@2023-01-01' = {
  name: frontendAppServiceName
  location: appServiceLocation
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: frontendLinuxFxVersion
      appCommandLine: 'node server.js'
    }
  }
}

var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var cognitiveServicesUserRoleId = 'a97b65f3-24c7-4388-baec-2e87135dc908'

resource storageRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, appService.id, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource documentIntelligenceRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(documentIntelligence.id, appService.id, cognitiveServicesUserRoleId)
  scope: documentIntelligence
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesUserRoleId)
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output appServiceName string = appService.name
output appServiceDefaultHostName string = appService.properties.defaultHostName
output appServicePrincipalId string = appService.identity.principalId
output storageAccountName string = storageAccount.name
output storageServiceUri string = storageAccount.properties.primaryEndpoints.blob
output documentIntelligenceEndpoint string = documentIntelligence.properties.endpoint
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output frontendAppServiceName string = frontendAppService.name
output frontendAppServiceDefaultHostName string = frontendAppService.properties.defaultHostName
