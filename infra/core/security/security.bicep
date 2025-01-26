metadata description = 'Create role assignment and definition resources.'

param databaseAccountName string

@description('Id of the service principals to assign database and application roles.')
param appPrincipalId string = ''

@description('Id of the user principals to assign database and application roles.')
param userPrincipalId string = ''

@description('Principal type used for the role assignment.')
param principalType string = ''

resource database 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' existing = {
  name: databaseAccountName
}

module nosqlDefinition '../database/cosmos-db/nosql/role/definition.bicep' = {
  name: 'nosql-role-definition'
  params: {
    targetAccountName: database.name // Existing account
    definitionName: 'Write to Azure Cosmos DB for NoSQL data plane' // Custom role name
    permissionsDataActions: [
      'Microsoft.DocumentDB/databaseAccounts/readMetadata' // Read account metadata
      'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/items/*' // Create items
      'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/*' // Manage items
      'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/write' // Write
    ]
  }
}

module nosqlAppAssignment '../database/cosmos-db/nosql/role/assignment.bicep' = if (!empty(appPrincipalId)) {
  name: 'nosql-role-assignment-app'
  params: {
    targetAccountName: database.name // Existing account
    roleDefinitionId: nosqlDefinition.outputs.id // New role definition
    principalId: appPrincipalId // Principal to assign role
    principalType: principalType // Principal type for assigning role
  }
}

module nosqlUserAssignment '../database/cosmos-db/nosql/role/assignment.bicep' = if (!empty(userPrincipalId)) {
  name: 'nosql-role-assignment-user'
  params: {
    targetAccountName: database.name // Existing account
    roleDefinitionId: nosqlDefinition.outputs.id // New role definition
    principalId: userPrincipalId ?? '' // Principal to assign role
    principalType: principalType // Principal type for assigning role
  }
}

module openaiAppAssignment '../security/role/assignment.bicep' = if (!empty(appPrincipalId)) {
  name: 'openai-role-assignment-read-app'
  params: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    ) // Cognitive Services OpenAI User built-in role
    principalId: appPrincipalId // Principal to assign role
    principalType: 'ServicePrincipal' // Specify the principal type // was 'None' but this appears to cause issues
  }
}

module openaiUserAssignment '../security/role/assignment.bicep' = if (!empty(userPrincipalId)) {
  name: 'openai-role-assignment-read-user'
  params: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    ) // Cognitive Services OpenAI User built-in role
    principalId: userPrincipalId // Principal to assign role
    principalType: !empty(principalType) ? principalType : 'User' // Principal type or current deployment user
  }
}

module cosmsodbUserAssignment '../security/role/assignment.bicep' = if (!empty(userPrincipalId)) {
  name: 'cosmosdb-role-assignment-read-user'
  params: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    ) // Cognitive Services OpenAI User built-in role
    principalId: userPrincipalId // Principal to assign role
    principalType: !empty(principalType) ? principalType : 'User' // Principal type or current deployment user
  }
}

output roleDefinitions object = {
  nosql: nosqlDefinition.outputs.id
}

output roleAssignments array = union(
  !empty(appPrincipalId) ? [nosqlAppAssignment.outputs.id, openaiAppAssignment.outputs.id] : [],
  !empty(userPrincipalId) ? [nosqlUserAssignment.outputs.id, openaiUserAssignment.outputs.id] : []
)
