## FunctionApp Deployment - Begin
# Constants
$projectPath = "./Project.csproj"
$functionAppName = "PHI-DETECTION"
$resourceGroup = "rg-phi-1"

# Build the project
dotnet build $projectPath --configuration Release

# Publish the project
dotnet publish $projectPath --configuration Release --output ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip

# Deploy to Azure Function App
az functionapp deployment source config-zip `
    --resource-group $resourceGroup `
    --name $functionAppName `
    --src ./publish.zip
## FunctionApp Deployment - End