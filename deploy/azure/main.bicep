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

@description('Enable only after the Google client secret exists in Key Vault.')
param enableGoogleAuthentication bool = false

@description('Public Google OAuth client ID. The client secret remains in Key Vault.')
param googleClientId string = ''

@description('Enable only after the separate Google Calendar client secret exists in Key Vault.')
param enableGoogleCalendar bool = false

@description('Enable only after Calendar write scopes are approved and Increment 2 is deployed and migrated.')
param enableGoogleCalendarEventCreation bool = false

@description('Enable only after the Calendar event-management migration and reviewed API image are deployed.')
param enableGoogleCalendarEventManagement bool = false

@description('Public client ID for the separate Google Calendar OAuth web client.')
param googleCalendarClientId string = ''

@description('Enable only after the separate Google Tasks client secret exists in Key Vault.')
param enableGoogleTasks bool = false
param enableGoogleTaskMutations bool = false

@description('Public client ID for the separate Google Tasks OAuth web client.')
param googleTasksClientId string = ''

@description('Enable only after parent-access-pepper-v1 exists in Key Vault.')
param enableParentAccess bool = false

@description('Enable private household photo upload and delivery through the API.')
param enableHouseholdMedia bool = false

@description('Enable provider-neutral household weather backed by the US National Weather Service.')
param enableWeather bool = false

@minValue(1)
@maxValue(168)
param choreGenerationHorizonHours int = 36

@minValue(1)
@maxValue(1000)
param choreGenerationMaximumAssignmentsPerRun int = 100

@description('Exact immutable GitHub Actions OIDC subject for the protected staging environment.')
param githubOidcSubject string

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

module authenticationSecurity 'modules/authentication-security.bicep' = {
  name: '${nameStem}-authentication-security'
  params: {
    location: location
    nameStem: nameStem
    tags: resourceTags
    privateEndpointsSubnetId: network.outputs.privateEndpointsSubnetId
    blobPrivateDnsZoneId: network.outputs.blobPrivateDnsZoneId
    keyVaultPrivateDnsZoneId: network.outputs.keyVaultPrivateDnsZoneId
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
    enableGoogleAuthentication: enableGoogleAuthentication
    googleClientId: googleClientId
    googleClientSecretUri: authenticationSecurity.outputs.googleClientSecretUri
    enableGoogleCalendar: enableGoogleCalendar
    enableGoogleCalendarEventCreation: enableGoogleCalendarEventCreation
    enableGoogleCalendarEventManagement: enableGoogleCalendarEventManagement
    googleCalendarClientId: googleCalendarClientId
    googleCalendarClientSecretUri: authenticationSecurity.outputs.googleCalendarClientSecretUri
    enableGoogleTasks: enableGoogleTasks
    enableGoogleTaskMutations: enableGoogleTaskMutations
    googleTasksClientId: googleTasksClientId
    googleTasksClientSecretUri: authenticationSecurity.outputs.googleTasksClientSecretUri
    enableParentAccess: enableParentAccess
    parentAccessPepperSecretUri: authenticationSecurity.outputs.parentAccessPepperSecretUri
    runtimeIdentityId: authenticationSecurity.outputs.runtimeIdentityId
    runtimeIdentityClientId: authenticationSecurity.outputs.runtimeIdentityClientId
    dataProtectionBlobUri: authenticationSecurity.outputs.dataProtectionBlobUri
    dataProtectionKeyIdentifier: authenticationSecurity.outputs.dataProtectionKeyIdentifier
    enableHouseholdMedia: enableHouseholdMedia
    householdPhotosContainerUri: authenticationSecurity.outputs.householdPhotosContainerUri
    enableWeather: enableWeather
    choreGenerationHorizonHours: choreGenerationHorizonHours
    choreGenerationMaximumAssignmentsPerRun: choreGenerationMaximumAssignmentsPerRun
  }
}

module deploymentIdentity 'modules/deployment-identity.bicep' = {
  name: '${nameStem}-deployment-identity'
  params: {
    location: location
    nameStem: nameStem
    tags: resourceTags
    githubOidcSubject: githubOidcSubject
    apiName: containerApps.outputs.apiName
    migrationJobName: containerApps.outputs.migrationJobName
    choreGeneratorJobName: containerApps.outputs.choreGeneratorJobName
  }
}

output apiName string = containerApps.outputs.apiName
output apiDefaultHostname string = containerApps.outputs.apiDefaultHostname
output apiCustomHostname string = enableCustomDomain ? apiHostname : ''
output customDomainVerificationId string = containerApps.outputs.customDomainVerificationId
output migrationJobName string = containerApps.outputs.migrationJobName
output choreGeneratorJobName string = containerApps.outputs.choreGeneratorJobName
output postgresServerName string = postgres.outputs.serverName
output postgresFullyQualifiedDomainName string = postgres.outputs.fullyQualifiedDomainName
output githubClientId string = deploymentIdentity.outputs.clientId
output githubPrincipalId string = deploymentIdentity.outputs.principalId
output runtimeIdentityClientId string = authenticationSecurity.outputs.runtimeIdentityClientId
output keyVaultName string = authenticationSecurity.outputs.keyVaultName
output storageAccountName string = authenticationSecurity.outputs.storageAccountName
output tenantId string = tenant().tenantId
output subscriptionId string = subscription().subscriptionId
