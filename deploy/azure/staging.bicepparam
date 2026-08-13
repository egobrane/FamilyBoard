using './main.bicep'

param location = 'centralus'
param environmentName = 'staging'
param apiHostname = 'api.egobrane.net'
param frontendOrigin = 'https://family.egobrane.net'
param githubRepository = 'egobrane/FamilyBoard'
param githubOidcSubject = 'repo:egobrane@23132912/FamilyBoard@1324023581:environment:staging'
param backendImage = 'ghcr.io/egobrane/familyboard-backend@sha256:6e01123fba9925b55b1f86ba7309e297fed9b14ca318d54c2bef5b08ea08df24'
param enableCustomDomain = true
param enableGoogleAuthentication = false
param googleClientId = ''
param postgresAdminPassword = readEnvironmentVariable('FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD')
