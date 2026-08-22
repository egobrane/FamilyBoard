using './main.bicep'

param location = 'centralus'
param environmentName = 'staging'
param apiHostname = 'api.egobrane.net'
param frontendOrigin = 'https://family.egobrane.net'
param githubRepository = 'egobrane/FamilyBoard'
param githubOidcSubject = 'repo:egobrane@23132912/FamilyBoard@1324023581:environment:staging'
param backendImage = 'ghcr.io/egobrane/familyboard-backend@sha256:909e2e98a4cde3e9fce3d7220b26d4b4d0e4edc80e434e4a2b19befbabcad4c0'
param enableCustomDomain = true
param enableGoogleAuthentication = true
param enableParentAccess = true
param enableGoogleCalendar = true
param enableGoogleCalendarEventCreation = true
param googleCalendarClientId = '964271653840-45pheoqseb8obgsf5vaka6nsso7nbjc6.apps.googleusercontent.com'
param googleClientId = '964271653840-i22oeorkf03l0qvqdo705du1e8229re4.apps.googleusercontent.com'
param postgresAdminPassword = readEnvironmentVariable('FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD')
