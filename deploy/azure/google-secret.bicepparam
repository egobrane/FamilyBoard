using './google-secret.bicep'

param keyVaultName = 'familydb-rwzkcdch6czlm'
param googleClientSecret = readEnvironmentVariable('FAMILY_DASHBOARD_GOOGLE_CLIENT_SECRET')
