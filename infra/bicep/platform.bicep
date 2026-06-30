// Resource-group-scoped module deployed by main.bicep. Splitting like this
// keeps subscription-level concerns (RG, policies) separate from the
// per-environment platform.

@description('Resource name prefix.')
param prefix string

@description('Azure region.')
param location string

@description('SQL administrator login.')
param sqlAdministratorLogin string

@secure()
param sqlAdministratorPassword string

@description('AAD object IDs for Key Vault administrators.')
param keyVaultAdministrators array

// -----------------------------------------------------------------------------
// SQL Server + database — production tier with geo-redundant backup
// -----------------------------------------------------------------------------
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: '${prefix}-sql'
  location: location
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'iretp'
  location: location
  sku: {
    name: 'GP_Gen5_4'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 4
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 268435456000   // 250 GB
    zoneRedundant: false
  }
}

resource sqlFirewallAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// -----------------------------------------------------------------------------
// Storage — encrypted backups + monthly Escrow reports
// -----------------------------------------------------------------------------
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: replace('${prefix}sa', '-', '')
  location: location
  sku: { name: 'Standard_GRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
    encryption: {
      services: {
        blob: { enabled: true }
        file: { enabled: true }
      }
      keySource: 'Microsoft.Storage'
    }
  }
}

// -----------------------------------------------------------------------------
// Key Vault — JWT signing key, SQL admin password, AI provider API keys
// -----------------------------------------------------------------------------
resource keyVault 'Microsoft.KeyVault/vaults@2024-04-01-preview' = {
  name: '${prefix}-kv'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

resource kvAdminAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for adminId in keyVaultAdministrators: {
  name: guid(keyVault.id, adminId, 'kv-admin')
  scope: keyVault
  properties: {
    // Key Vault Administrator built-in role
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '00482a5a-887f-4fb3-b363-3b7fe8e74483')
    principalId: adminId
    principalType: 'User'
  }
}]

// -----------------------------------------------------------------------------
// Application Insights — performance SLA monitoring (RFP §10.1)
// -----------------------------------------------------------------------------
resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-law'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 90
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${prefix}-ai'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    IngestionMode: 'LogAnalytics'
  }
}

// -----------------------------------------------------------------------------
// App Service Plan — single Linux plan hosts WebAPI, AdminAPI and the Blazor
// portal so they can scale together. Premium tier required for VNet
// integration (DESC ISR §3.4 network isolation).
// -----------------------------------------------------------------------------
resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${prefix}-plan'
  location: location
  sku: {
    name: 'P1v3'
    tier: 'PremiumV3'
    capacity: 2  // multi-instance for the 99.9% uptime SLA in §10.1
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

var dotnetStack = 'DOTNETCORE|9.0'
var commonAppSettings = {
  WEBSITES_ENABLE_APP_SERVICE_STORAGE: 'false'
  APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.properties.ConnectionString
  ASPNETCORE_ENVIRONMENT: 'Production'
}

resource webApi 'Microsoft.Web/sites@2024-04-01' = {
  name: '${prefix}-webapi'
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: dotnetStack
      alwaysOn: true
      http20Enabled: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [for setting in items(commonAppSettings): {
        name: setting.key
        value: setting.value
      }]
    }
  }
}

resource adminApi 'Microsoft.Web/sites@2024-04-01' = {
  name: '${prefix}-adminapi'
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: dotnetStack
      alwaysOn: true
      http20Enabled: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [for setting in items(commonAppSettings): {
        name: setting.key
        value: setting.value
      }]
    }
  }
}

resource webPortal 'Microsoft.Web/sites@2024-04-01' = {
  name: '${prefix}-web'
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: dotnetStack
      alwaysOn: true
      http20Enabled: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [for setting in items(commonAppSettings): {
        name: setting.key
        value: setting.value
      }]
    }
  }
}

output webApiUrl string = 'https://${webApi.properties.defaultHostName}'
output adminApiUrl string = 'https://${adminApi.properties.defaultHostName}'
output webPortalUrl string = 'https://${webPortal.properties.defaultHostName}'
output keyVaultName string = keyVault.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output storageAccountName string = storage.name
