
# Constants
$projectPath = "../Program.csproj"
$functionAppName = "PHI-DETECTION"
$resourceGroup = "rg-phi-1"

# Build the project
dotnet build $projectPath --configuration Release

# Publish the project
dotnet publish $projectPath --configuration Release --output ./publish

# Deploy to Azure Function App
az functionapp deployment source config-zip `
    --resource-group $resourceGroup `
    --name $functionAppName `
    --src ./publish.zip
