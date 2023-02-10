# Deploy Web App + SQL DB + Redis Cache in Azure

This template intends to simplify resource creation experiences for web apps that use Cache to accelerate Database query performance. All resources are secured through Azure Network settings.

## Pre-requisites
* Install Azure CLI: [install](https://docs.microsoft.com/cli/azure/install-azure-cli)

## Deploy template
1. Log in to Azure CLI from a Command Prompt
```
az login
```

2. Select subscription to deploy the resources in
```
azure account set --s {YOUR_SUBSCRIPTION_ID}
```

3. Create a resource group
```
az group create --name {YOUR_RESOURCEGROUP_NAME} --location {YOUR_RESOURCEGROUP_LOCATION. e.g. eastus}
```

4. Open azuredeploy.parameters.json. Edit the parameters. IMPORTANT: use the name of resource group you just created for "serverFarmResourceGroup" parameter

5. Go back to the Command Prompt where you logged in to Az CLI and selected subscriptions. Run the following command with your parameter. IMPORTANT: don't miss the '@' symbol in front of the paramter files for the command to work.
```
az deployment group create --name ExampleDeployment --resource-group {YOUR_RESOURCEGROUP_NAME} --template-file azuredeploy.json --parameters @azuredeploy.parameters.json
```
