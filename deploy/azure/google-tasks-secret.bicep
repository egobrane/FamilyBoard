targetScope = 'resourceGroup'

param keyVaultName string

@secure()
@minLength(1)
@description('Separate Google Tasks OAuth web client secret. Never use sign-in or Calendar secrets here.')
param googleTasksClientSecret string

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

resource googleTasksSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'google-tasks-client-secret'
  properties: {
    value: googleTasksClientSecret
  }
}
