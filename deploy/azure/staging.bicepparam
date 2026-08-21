using './main.bicep'

param location = 'centralus'
param environmentName = 'staging'
param apiHostname = 'api.egobrane.net'
param frontendOrigin = 'https://family.egobrane.net'
param githubRepository = 'egobrane/FamilyBoard'
param githubOidcSubject = 'repo:egobrane@23132912/FamilyBoard@1324023581:environment:staging'
param backendImage = 'ghcr.io/egobrane/familyboard-backend@sha256:028a47778229f74aae12725df0665f0a9042476169c6b69b7ec60ac35e40d318'
param enableCustomDomain = true
param enableGoogleAuthentication = true
param enableParentAccess = true
param enableGoogleCalendar = true
param enableGoogleCalendarEventCreation = true
param googleCalendarClientId = '964271653840-45pheoqseb8obgsf5vaka6nsso7nbjc6.apps.googleusercontent.com'
param googleClientId = '964271653840-i22oeorkf03l0qvqdo705du1e8229re4.apps.googleusercontent.com'
param postgresAdminPassword = readEnvironmentVariable('FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD')
