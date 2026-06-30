// =============================================================================
// IRETP — Integrated Real Estate Transparency Platform
// Reference Bicep deployment for the Azure (UAE North) hosting option.
//
// Deploys: SQL Server + database, App Service Plan + 3 Web Apps, Key Vault,
//          Application Insights, Storage account (backups + reports), and
//          a Container Registry for the Hangfire worker image.
//
// Pinned to UAE North to satisfy RFP §11.3 data-residency requirement. The
// SKUs below are sized for production-grade pilot — adjust before scale-out.
//
// Usage:
//   az deployment sub create \
//     --location uaenorth \
//     --template-file infra/bicep/main.bicep \
//     --parameters environmentName=prod sqlAdministratorPassword=<secret>
// =============================================================================

targetScope = 'subscription'

@minLength(2)
@maxLength(8)
@description('Lowercase environment tag — appears in every resource name.')
param environmentName string = 'prod'

@description('Region. RFP §11.3 mandates UAE residency for production.')
@allowed([ 'uaenorth', 'uaecentral' ])
param location string = 'uaenorth'

@description('SQL administrator login.')
param sqlAdministratorLogin string = 'iretpadmin'

@secure()
@description('SQL administrator password — supply via deployment parameter, never check in.')
param sqlAdministratorPassword string

@description('Object IDs of DLD administrators to grant Key Vault access.')
param keyVaultAdministrators array = []

var prefix          = 'iretp-${environmentName}'
var resourceGroupName = '${prefix}-rg'

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: {
    Programme: 'IRETP'
    Owner: 'Dubai Land Department'
    Compliance: 'DESC-ISR-v3'
  }
}

module platform 'platform.bicep' = {
  name: '${prefix}-platform'
  scope: rg
  params: {
    prefix: prefix
    location: location
    sqlAdministratorLogin: sqlAdministratorLogin
    sqlAdministratorPassword: sqlAdministratorPassword
    keyVaultAdministrators: keyVaultAdministrators
  }
}

output resourceGroup string = rg.name
output webApiUrl string = platform.outputs.webApiUrl
output adminApiUrl string = platform.outputs.adminApiUrl
output webPortalUrl string = platform.outputs.webPortalUrl
output keyVaultName string = platform.outputs.keyVaultName
output appInsightsConnectionString string = platform.outputs.appInsightsConnectionString
