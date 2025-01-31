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
          value: 'DefaultEndpointsProtocol=https;AccountName=stmniguahfc7i72;AccountKey=74B0V6LPceY66pnRW4Tdx1v2IPz2pJnJ944lmVILjhW+Ugjfu1xxDPu487Af1TdeRYTu9lHK1JEX+AStM/N3Fw==;EndpointSuffix=core.windows.net'
          //value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${listKeys(storageAccountId, '2021-04-01').keys[0].value}'
        }
        {
          name: 'STORAGE_ACCOUNT_NAME'
          value: 'sttqzqvek3clzau'
        }
        {
          name: 'STORAGE_CONTAINER_NAME'
          value: 'content'
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
          value: 'https://cos-mniguahfc7i72.documents.azure.com'
        }
        {
          name: 'COSMOSDB_DBNAME'
          value: 'cos-mniguahfc7i72'
        }
        {
          name: 'LANGUAGE_ENDPOINT'
          value: 'https://cog-ta-tqzqvek3clzau.cognitiveservices.azure.com'
        }
        {
          name: 'LANGUAGE_KEY'
          value: 'Cx7C9uJT3mdCKCt2otzhYUWvjgcszaKlINp38OvmMtID4HjOJRNmJQQJ99BAACHYHv6XJ3w3AAAaACOGzeOQ'
        }
        {
          name: 'MANAGED_IDENTITY_CLIENT_ID'
          value: 'e685809a-9494-4a58-bd40-c1b76f2e8ae1'
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
