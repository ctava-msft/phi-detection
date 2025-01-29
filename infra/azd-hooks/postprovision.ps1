## FunctionApp Deployment - Begin
# Parse main.parameters.json to extract parameters
$parametersPath = "../main.parameters.json"
$parameters = Get-Content -Raw -Path $parametersPath | ConvertFrom-Json

$functionAppName = $parameters.parameters.functionAppName.value
$environmentName = $parameters.parameters.environmentName.value
$resourceGroup = "rg-$environmentName"

# Constants
$projectPath = "./Project.csproj"

# Build the project
dotnet build $projectPath --configuration Release

# Publish the project
dotnet publish $projectPath --configuration Release --output ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip -Force

# Deploy to Azure Function App
az functionapp deployment source config-zip `
    --resource-group $resourceGroup `
    --name $functionAppName `
    --src ./publish.zip
## FunctionApp Deployment - End