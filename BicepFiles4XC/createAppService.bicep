/*
  Module: createAppService.bicep
  Description: Deploys an Azure App Service (Web App) attached to an existing App Service Plan.
               The plan may live in a different resource group — its full resource ID is passed in.

  Parameters:
  - webAppName (string): Name of the Web App to create.
  - location (string): Azure region. Default: 'eastus'.
  - appServicePlanId (string): Full resource ID of the App Service Plan to use.
*/

param webAppName string
param location string = 'eastus'
param appServicePlanId string

resource webApp 'Microsoft.Web/sites@2021-01-01' = {
  name: webAppName
  location: location
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
  }
}
