//param location string = resourceGroup().location
param location string = 'eastus'
param languageServiceName string
param userManagedIdentityId string
param tags object = {}

resource languageService 'Microsoft.CognitiveServices/accounts@2022-12-01' = {
  name: languageServiceName
  location: location
  kind: 'TextAnalytics'
  sku: {
    name: 'F0'
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userManagedIdentityId}': {}
    }
  }
  properties: {
    publicNetworkAccess: 'Enabled'
  }
  tags: tags
}
