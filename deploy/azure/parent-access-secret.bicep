targetScope = 'resourceGroup'

param keyVaultName string

@secure()
@minLength(44)
@description('Base64-encoded random 32-byte parent access pepper. Never commit or log this value.')
param parentAccessPepper string

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

resource parentAccessPepperSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'parent-access-pepper-v1'
  properties: {
    value: parentAccessPepper
  }
}
