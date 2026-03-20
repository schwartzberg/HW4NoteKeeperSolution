/*
  Module: createStorageAccount.bicep
  Description: Deploys an Azure Blob Storage account (StorageV2) with configurable parameters.

  Parameters:
  - storageAccountName (string): The name of the storage account to create.
  - skuName (string): The SKU/redundancy tier. Default: 'Standard_LRS'.
  - location (string): Azure region. Default: 'eastus'.
  - accessTier (string): Hot or Cool access tier. Default: 'Hot'.
  - allowPublicAccess (bool): Whether to allow public blob access. Default: false.
  - networkDefaultAction (string): Default network ACL action. Default: 'Allow'.

  Outputs:
  - storageAccountId (string): Resource ID of the created storage account.
*/

param storageAccountName string
param skuName string = 'Standard_LRS'
param location string = 'eastus'
param accessTier string = 'Hot'
param allowPublicAccess bool = false
param networkDefaultAction string = 'Allow'

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: skuName
  }
  kind: 'StorageV2'
  properties: {
    accessTier: accessTier
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: allowPublicAccess
    networkAcls: {
      defaultAction: networkDefaultAction
    }
  }
}

output storageAccountId string = storageAccount.id
