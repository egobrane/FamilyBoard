using './google-calendar-secret.bicep'

param keyVaultName = 'familydb-rwzkcdch6czlm'
param googleCalendarClientSecret = readEnvironmentVariable('FAMILY_DASHBOARD_GOOGLE_CALENDAR_CLIENT_SECRET')
