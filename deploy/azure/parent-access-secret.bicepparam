using './parent-access-secret.bicep'

param keyVaultName = 'familydb-rwzkcdch6czlm'
param parentAccessPepper = readEnvironmentVariable('FAMILY_DASHBOARD_PARENT_ACCESS_PEPPER')
