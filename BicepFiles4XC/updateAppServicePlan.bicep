/*
  Module: updateAppServicePlan.bicep
  Description: Creates or updates an Azure App Service Plan.
               When the named plan already exists, Bicep's declarative model will
               update it in-place (e.g., upgrading SKU from B1 to B2).

  Parameters:
  - appServicePlanName (string): Name of the App Service Plan to create/update.
  - location (string): Azure region. Default: 'eastus'.
  - skuName (string): SKU name. Default: 'B2'.
  - skuTier (string): SKU tier. Default: 'Basic'.

  Outputs:
  - appServicePlanId (string): Resource ID of the App Service Plan (used by the App Service module).
*/

param appServicePlanName string
param location string = 'eastus'
param skuName string = 'B2'
param skuTier string = 'Basic'

resource appServicePlan 'Microsoft.Web/serverfarms@2021-01-01' = {
  name: appServicePlanName
  location: location
  properties: {
    reserved: true // Linux hosting
  }
  sku: {
    name: skuName
    tier: skuTier
  }
}

output appServicePlanId string = appServicePlan.id
