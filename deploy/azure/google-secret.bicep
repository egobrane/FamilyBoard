targetScope = 'resourceGroup'

param keyVaultName string

@secure()
@minLength(1)
@description('Google web OAuth client secret. Supplied only through the secure parameter file environment lookup.')
param googleClientSecret string

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

resource googleSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'google-client-secret'
  properties: {
    value: googleClientSecret
  }
}
