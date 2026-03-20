/*
  main.bicep - HW3 Extra Credit Bicep Orchestrator
  -------------------------------------------------
  This is the single entry point for the HW3 Extra Credit Bicep deployment.
  It is scoped to the Azure subscription level and orchestrates the following:

    (a) Creates resource group: rg_03-assignment
    (b) Deploys Azure Blob Storage account: sthw3bicepextracredit  --> rg_03-assignment
    (c) Updates existing App Service Plan: asp-cscie94              --> rg_service_app_plan (B1 -> B2)
    (d) Deploys new App Service: app-hw3-bicep-extra-credit         --> rg_03-assignment
        (linked to the existing asp-cscie94 plan)

  -----------------------------------------------------------------------
  HOW TO DEPLOY
  -----------------------------------------------------------------------

  Prerequisites:
    - Azure CLI installed and logged in:  az login
    - Bicep CLI available (comes with Azure CLI 2.20+)
    - Sufficient permissions on the subscription (Contributor or Owner)

  Azure CLI (run from the BicepFiles4XC folder):
  -----------------------------------------------------------------------
  cd C:\Users\schwa\Documents\H_DCE\cloud_computing_openai_e_94\assignments\03-Assignment\HW3NoteKeeper\Bicep\BicepFiles4XC

  az deployment sub create `
    --name ("BicepXC_" + (Get-Date -Format "yyyyMMddHHmmss")) `
    --location eastus `
    --template-file main.bicep

  -----------------------------------------------------------------------
  PowerShell (run from the BicepFiles4XC folder):
  -----------------------------------------------------------------------
  cd C:\Users\schwa\Documents\H_DCE\cloud_computing_openai_e_94\assignments\03-Assignment\HW3NoteKeeper\Bicep\BicepFiles4XC

  New-AzSubscriptionDeployment `
    -Name ("BicepXC_" + (Get-Date -Format "yyyyMMddHHmmss")) `
    -Location 'eastus' `
    -TemplateFile 'main.bicep'

  -----------------------------------------------------------------------
  Notes:
    - The --no-wait flag can be added to return immediately (async deployment).
    - To override any default parameter, append:
        --parameters location=eastus storageAccountName=sthw3bicepextracredit
    - The App Service Plan update is idempotent: if asp-cscie94 is already
      B2, no change is made. If it is B1, it is upgraded to B2.
  -----------------------------------------------------------------------
*/

targetScope = 'subscription'

// -----------------------------------------------------------------------
// Parameters
// -----------------------------------------------------------------------

param location string = 'eastus'

// (a) Resource group to create
param newResourceGroupName string = 'rg_03-assignment'

// (b) Storage account
param storageAccountName string = 'sthw3bicepextracredit'

// (c) Existing App Service Plan to update (lives in a separate RG)
param existingAspResourceGroupName string = 'rg_service_app_plan'
param appServicePlanName string = 'asp-cscie94'
param existingAspLocation string = 'swedencentral' // asp-cscie94 was originally deployed here

// (d) New App Service
param webAppName string = 'app-hw3-bicep-extra-credit'

// -----------------------------------------------------------------------
// (a) Create the new resource group
// -----------------------------------------------------------------------

resource newResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: newResourceGroupName
  location: location
}

// -----------------------------------------------------------------------
// Reference the existing resource group that holds asp-cscie94
// -----------------------------------------------------------------------

resource existingAspResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' existing = {
  name: existingAspResourceGroupName
}

// -----------------------------------------------------------------------
// (b) Deploy Azure Blob Storage account into the new resource group
// -----------------------------------------------------------------------

module storageAccount 'createStorageAccount.bicep' = {
  name: 'deployStorageAccount'
  scope: newResourceGroup
  params: {
    storageAccountName: storageAccountName
    location: location
  }
}

// -----------------------------------------------------------------------
// (c) Update existing App Service Plan asp-cscie94 to B2
//     Scoped to its own resource group (rg_service_app_plan)
// -----------------------------------------------------------------------

module updateAsp 'updateAppServicePlan.bicep' = {
  name: 'updateAppServicePlan'
  scope: existingAspResourceGroup
  params: {
    appServicePlanName: appServicePlanName
    location: existingAspLocation  // must match the region where asp-cscie94 already exists
    skuName: 'B2'
    skuTier: 'Basic'
  }
}

// -----------------------------------------------------------------------
// (d) Deploy new App Service into the new resource group,
//     linked to the (now B2) asp-cscie94 plan
// -----------------------------------------------------------------------

module appService 'createAppService.bicep' = {
  name: 'deployAppService'
  scope: newResourceGroup
  params: {
    webAppName: webAppName
    location: existingAspLocation  // App Service must be in the same region as its App Service Plan
    appServicePlanId: updateAsp.outputs.appServicePlanId
  }
}
