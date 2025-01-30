## FunctionApp Deployment - Begin
# Parse main.parameters.json to extract parameters
$parametersPath = "./infra/main.parameters.json"
$parameters = Get-Content -Raw -Path $parametersPath | ConvertFrom-Json

# Get a handle on .azure directory
$azureFolderPath = Join-Path -Path (Split-Path -Parent (Split-Path $PSScriptRoot)) '.azure'
# Extract resource group name from .azure folder
$resourceGroupName = (Get-ChildItem -Path $azureFolderPath -Directory | Select-Object -First 1).Name

# Load config.json from the resource group directory
$configPath = Join-Path -Path $azureFolderPath -ChildPath "$resourceGroupName/config.json"
$config = Get-Content -Raw -Path $configPath | ConvertFrom-Json

# Extract function app name from config.json
$functionAppName = $config.infra.parameters.functionAppName

# Constants
$projectPath = "./Project.csproj"

# Build the project
dotnet build $projectPath --configuration Release

# Publish the project
dotnet publish $projectPath --configuration Release --output ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip -Force

# Deploy to Azure Function App
az functionapp deployment source config-zip `
    --resource-group $resourceGroupName `
    --name $functionAppName `
    --src ./publish.zip
## FunctionApp Deployment - End