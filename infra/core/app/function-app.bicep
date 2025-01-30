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
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${listKeys(storageAccountId, '2021-04-01').keys[0].value}'
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
          value: 'https://cosmos-tqzqvek3clzau.documents.azure.com'
        }
        {
          name: 'COSMOSDB_DBNAME'
          value: 'cosmoscopilotdb'
        }
        {
          name: 'LANGUAGE_ENDPOINT'
          value: 'https://cog-ta-tqzqvek3clzau.cognitiveservices.azure.com'
        }
        {
          name: 'LANGUAGE_KEY'
          value: 'Cx7C9uJT3mdCKCt2otzhYUWvjgcszaKlINp38OvmMtID4HjOJRNmJQQJ99BAACHYHv6XJ3w3AAAaACOGzeOQ'
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
