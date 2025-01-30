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

# Create publish directory if it does not exist
$publishDir = "./publish"
if (-Not (Test-Path -Path $publishDir)) {
    New-Item -ItemType Directory -Path $publishDir
}

# Publish the project
dotnet publish $projectPath --configuration Release --output $publishDir
Compress-Archive -Path "$publishDir/*" -DestinationPath ./publish.zip -Force

# Check if publish.zip exists and remove it if it does
if (Test-Path -Path "./publish.zip") {
    # Deploy to Azure Function App
    az functionapp deployment source config-zip `
        --resource-group $resourceGroupName `
        --name $functionAppName `
        --src ./publish.zip
}

Remove-Item -Recurse -Force $publishDir
## FunctionApp Deployment - End