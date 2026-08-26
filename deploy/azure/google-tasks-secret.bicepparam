using './google-tasks-secret.bicep'

param keyVaultName = 'familydb-rwzkcdch6czlm'
param googleTasksClientSecret = readEnvironmentVariable('FAMILY_DASHBOARD_GOOGLE_TASKS_CLIENT_SECRET')
