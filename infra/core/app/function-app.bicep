param functionAppName string = 'uniqueFunctionAppName-${uniqueString(resourceGroup().id)}'
param storageAccountName string = 'defaultStorageName'
param appServicePlanName string = 'defaultAppServicePlan'
param location string = resourceGroup().location
param tags object = {}

param kind string = ''
param reserved bool = true
param sku object = { name: 'P0v3' }

param storageAccountId string
param userAssignedIdentityResourceId string

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  sku: sku
  kind: kind
  properties: {
    reserved: reserved
  }
}

resource functionApp 'Microsoft.Web/sites@2020-12-01' = {
  name: functionAppName
  location: resourceGroup().location
  kind: 'functionapp'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityResourceId}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=<redacted>;AccountKey=<redacted>;EndpointSuffix=core.windows.net'
        }
        {
          name: 'STORAGE_ACCOUNT_NAME'
          value: '<redacted'
        }
        {
          name: 'STORAGE_CONTAINER_NAME'
          value: '<redacted>'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'true'
        }
        {
          name: 'COSMOSDB_ENDPOINT'
          value: 'https://<redacted>.documents.azure.com'
        }
        {
          name: 'COSMOSDB_DBNAME'
          value: '<redacted>'
        }
        {
          name: 'LANGUAGE_ENDPOINT'
          value: 'https://<redacted>.cognitiveservices.azure.com'
        }
        {
          name: 'LANGUAGE_KEY'
          value: '<redacted>'
        }
        {
          name: 'MANAGED_IDENTITY_CLIENT_ID'
          value: '<redacted>'
        }
      ]
      alwaysOn: true
      healthCheckPath: '/api/health'
    }
    httpsOnly: true
  }
}

output id string = appServicePlan.id
output name string = appServicePlan.name
