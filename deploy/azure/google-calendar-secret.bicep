targetScope = 'resourceGroup'

param keyVaultName string

@secure()
@minLength(1)
@description('Separate Google Calendar OAuth web client secret. Never use the Google sign-in secret here.')
param googleCalendarClientSecret string

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' existing = {
  name: keyVaultName
}

resource googleCalendarSecret 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: keyVault
  name: 'google-calendar-client-secret'
  properties: {
    value: googleCalendarClientSecret
  }
}
