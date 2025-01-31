// Params

targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention.')
param environmentName string

@minLength(1)
@allowed([
  'canadacentral'
  'centralus'
  'eastus'
  'eastus2'
  'northcentralus'
  'southcentralus'
  'westus'
  'westus2'
  'westus3'
])
@description('Primary location for all resources.')
param location string

param functionAppName string
param functionAppContainerName string = 'functionapp'
param appServicePlanName string = '' // Set in main.parameters.json

resource resourceGroup 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: environmentName
  location: location
  tags: tags
}

var abbrs = loadJsonContent('abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }
@description('Id of the principal to assign database and application roles.')
param principalId string = ''
param userAssignedIdentityName string = ''
var principalType = 'User'
param applicationInsightsName string = ''
param logAnalyticsName string = ''

@allowed(['None', 'AzureServices'])
@description('If allowedIp is set, whether azure services are allowed to bypass the storage and AI services firewall.')
param bypass string = 'AzureServices'

@description('Public network access value for all deployed resources')
@allowed(['Enabled', 'Disabled'])
param publicNetworkAccess string = 'Enabled'

@description('Use Application Insights for monitoring and performance tracing')
param useApplicationInsights bool = false

param storageAccountName string = 'defaultStorageName'
param storageContainerName string = 'content'
param storageSkuName string // Set in main.parameters.json

param cosmosDbAccountName string = '' // Set in main.parameters.json
param cosmosDbDatabaseName string = '' // Set in main.parameters.json

// modules

// Monitor application with Azure Monitor
module monitoring 'core/monitor/monitoring.bicep' = if (useApplicationInsights) {
  name: 'monitoring'
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    applicationInsightsName: !empty(applicationInsightsName)
      ? applicationInsightsName
      : '${abbrs.insightsComponents}${resourceToken}'
    logAnalyticsName: !empty(logAnalyticsName)
      ? logAnalyticsName
      : '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    publicNetworkAccess: publicNetworkAccess
  }
}

module identity 'core/security/identity/identity.bicep' = {
  name: 'identity'
  scope: resourceGroup
  params: {
    name: !empty(userAssignedIdentityName) ? userAssignedIdentityName : '${abbrs.userAssignedIdentities}-${resourceToken}'
    location: location
    tags: tags
  }
}

module security 'core/security/security.bicep' = {
  name: 'security'
  scope: resourceGroup
  params: {
    databaseAccountName: database.outputs.accountName
    appPrincipalId: identity.outputs.principalId
    userPrincipalId: !empty(principalId) ? principalId : null
    principalType: principalType
  }
}

module storage 'core/storage/storage-account.bicep' = {
  name: 'storage'
  scope: resourceGroup
  params: {
    name: !empty(storageAccountName) ? storageAccountName : '${abbrs.storageStorageAccounts}${resourceToken}'
    location: location
    tags: tags
    publicNetworkAccess: publicNetworkAccess
    bypass: bypass
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    sku: {
      name: storageSkuName
    }
    deleteRetentionPolicy: {
      enabled: true
      days: 2
    }
    containers: [
      {
        name: storageContainerName
        publicAccess: 'None'
      }
      {
        name: functionAppContainerName
        publicAccess: 'None'
      }
    ]
  }
}

module database 'core/database/database.bicep' = {
  name: 'database'
  scope: resourceGroup
  params: {
    databaseName: !empty(cosmosDbDatabaseName) ? cosmosDbDatabaseName : '${abbrs.cosmosDatabases}-${resourceToken}'
    accountName: !empty(cosmosDbAccountName) ? cosmosDbAccountName : '${abbrs.cosmosDatabases}-${resourceToken}'
    location: location
    tags: tags
  }
}

module functionApp 'core/app/function-app.bicep' = {
  name: 'functionApp'
  scope: resourceGroup
  params: {
    storageAccountId: storage.outputs.id
    functionAppName: functionAppName
    appServicePlanName: !empty(appServicePlanName) ? appServicePlanName : '${abbrs.appServicePlans}${resourceToken}'
    userAssignedIdentityResourceId: identity.outputs.resourceId
  }
}

// module languageService 'core/app/language-service.bicep' = {
//   name: 'languageService'
//   scope: resourceGroup
//   params: {
//     languageServiceName: '${abbrs.cognitiveServicesLanguage}${resourceToken}'
//     location: location
//     tags: tags
//     userManagedIdentityId: identity.outputs.principalId
//   }
// }

// Managed Identity Grants

// Contributor Grant
module contributorRoleUser 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'contributor-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: 'b24988ac-6180-42a0-ab88-20f7382dd24c'
    principalType: principalType
  }
}

// Blob Data Contributor Grant
module blobContributorRoleUser 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'blobContributor-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: '6648690f-0f37-4652-b6ae-68c5995dc409'
    principalType: principalType
  }
}

module blobReaderRoleFunctionApp 'core/security/role.bicep' = {
  scope: resourceGroup
  name: 'blobReaderRoleFunctionApp'
  params: {
    principalId: identity.outputs.principalId
    roleDefinitionId: '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1' // Storage Blob Data Reader
    principalType: 'ServicePrincipal'
  }
}

// Language Service Grant


// Database outputs
output AZURE_COSMOS_DB_ENDPOINT string = database.outputs.endpoint
output AZURE_COSMOS_DB_DATABASE_NAME string = database.outputs.database.name
