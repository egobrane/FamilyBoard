targetScope = 'resourceGroup'

@description('Azure region for every regional Family Dashboard resource.')
param location string = resourceGroup().location

@allowed([
  'staging'
])
param environmentName string = 'staging'

@description('Immutable public GHCR image reference, preferably pinned by digest.')
param backendImage string

@secure()
@minLength(16)
@description('Generated PostgreSQL administrator password. Never commit or log this value.')
param postgresAdminPassword string

@description('Enable only after the custom-domain CNAME and TXT validation records resolve.')
param enableCustomDomain bool = false

param apiHostname string = 'api.egobrane.net'
param frontendOrigin string = 'https://family.egobrane.net'
param githubRepository string = 'egobrane/FamilyBoard'

var prefix = 'family-dashboard'
var nameStem = '${prefix}-${environmentName}'
var resourceTags = {
  application: prefix
  environment: environmentName
  'managed-by': 'bicep'
  repository: githubRepository
}

module network 'modules/network.bicep' = {
  name: '${nameStem}-network'
  params: {
    location: location
    nameStem: nameStem
    tags: resourceTags
  }
}

module monitoring 'modules/monitoring.bicep' = {
  name: '${nameStem}-monitoring'
  params: {
    location: location
    nameStem: nameStem
    tags: resourceTags
  }
}

module postgres 'modules/postgres.bicep' = {
  name: '${nameStem}-postgres'
  params: {
    location: location
    nameStem: nameStem
    tags: resourceTags
    delegatedSubnetId: network.outputs.postgresSubnetId
    privateDnsZoneId: network.outputs.postgresPrivateDnsZoneId
    administratorPassword: postgresAdminPassword
  }
}

var postgresConnectionString = 'Host=${postgres.outputs.fullyQualifiedDomainName};Port=5432;Database=${postgres.outputs.databaseName};Username=${postgres.outputs.administratorLogin};Password=${postgresAdminPassword};SSL Mode=Require;Trust Server Certificate=false'

module containerApps 'modules/container-apps.bicep' = {
  name: '${nameStem}-container-apps'
  params: {
    location: location
    nameStem: nameStem
    tags: resourceTags
    infrastructureSubnetId: network.outputs.containerAppsSubnetId
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
    backendImage: backendImage
    postgresConnectionString: postgresConnectionString
    frontendOrigin: frontendOrigin
    apiHostname: apiHostname
    enableCustomDomain: enableCustomDomain
  }
}

module deploymentIdentity 'modules/deployment-identity.bicep' = {
  name: '${nameStem}-deployment-identity'
  params: {
    location: location
    nameStem: nameStem
    tags: resourceTags
    githubRepository: githubRepository
    apiName: containerApps.outputs.apiName
    migrationJobName: containerApps.outputs.migrationJobName
  }
}

output apiName string = containerApps.outputs.apiName
output apiDefaultHostname string = containerApps.outputs.apiDefaultHostname
output apiCustomHostname string = enableCustomDomain ? apiHostname : ''
output customDomainVerificationId string = containerApps.outputs.customDomainVerificationId
output migrationJobName string = containerApps.outputs.migrationJobName
output postgresServerName string = postgres.outputs.serverName
output postgresFullyQualifiedDomainName string = postgres.outputs.fullyQualifiedDomainName
output githubClientId string = deploymentIdentity.outputs.clientId
output githubPrincipalId string = deploymentIdentity.outputs.principalId
output tenantId string = tenant().tenantId
output subscriptionId string = subscription().subscriptionId
