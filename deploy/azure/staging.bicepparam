using './main.bicep'

param location = 'centralus'
param environmentName = 'staging'
param apiHostname = 'api.egobrane.net'
param frontendOrigin = 'https://family.egobrane.net'
param githubRepository = 'egobrane/FamilyBoard'
param githubOidcSubject = 'repo:egobrane@23132912/FamilyBoard@1324023581:environment:staging'
param backendImage = 'ghcr.io/egobrane/familyboard-backend@sha256:fd123bd13a3998ca6c65d374122559eb3fe64d447410ae923833e352a7dc0ea5'
param enableCustomDomain = true
param enableGoogleAuthentication = true
param googleClientId = '964271653840-i22oeorkf03l0qvqdo705du1e8229re4.apps.googleusercontent.com'
param postgresAdminPassword = readEnvironmentVariable('FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD')
