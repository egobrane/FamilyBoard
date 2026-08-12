param location string
param nameStem string
param tags object
param githubOidcSubject string
param apiName string
param migrationJobName string

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${nameStem}-github'
  location: location
  tags: tags
}

resource githubFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identity
  name: 'github-staging-environment'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: githubOidcSubject
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

resource api 'Microsoft.App/containerApps@2025-01-01' existing = {
  name: apiName
}

resource migrationJob 'Microsoft.App/jobs@2025-01-01' existing = {
  name: migrationJobName
}

var containerAppsContributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '358470bc-b998-42bd-ab17-a7e34c199c0f')
var containerAppsJobsContributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4e3d2b60-56ae-4dc6-a233-09c8e5a82e68')

resource apiDeploymentRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(api.id, identity.id, containerAppsContributorRoleId)
  scope: api
  properties: {
    roleDefinitionId: containerAppsContributorRoleId
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource migrationDeploymentRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(migrationJob.id, identity.id, containerAppsJobsContributorRoleId)
  scope: migrationJob
  properties: {
    roleDefinitionId: containerAppsJobsContributorRoleId
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output clientId string = identity.properties.clientId
output principalId string = identity.properties.principalId
