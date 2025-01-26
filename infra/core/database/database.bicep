metadata description = 'Create database accounts.'

param databaseName string
param accountName string
param location string = resourceGroup().location
param tags object = {}

module cosmosDbAccount './cosmos-db/nosql/account.bicep' = {
  name: 'cosmos-db-account'
  params: {
    name: accountName
    location: location
    tags: tags
    enableServerless: true
    enableVectorSearch: true
    enableNoSQLFullTextSearch: true
    disableKeyBasedAuth: true
  }
}

module cosmosDbDatabase './cosmos-db/nosql/database.bicep' = {
  name: 'cosmos-db-database-${databaseName}'
  params: {
    name: databaseName
    parentAccountName: cosmosDbAccount.outputs.name
    tags: tags
    setThroughput: false
  }
}

output endpoint string = cosmosDbAccount.outputs.endpoint
output accountName string = cosmosDbAccount.outputs.name

output database object = {
  name: cosmosDbDatabase.outputs.name
}
