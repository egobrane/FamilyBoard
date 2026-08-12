using './main.bicep'

param location = 'centralus'
param environmentName = 'staging'
param apiHostname = 'api.egobrane.net'
param frontendOrigin = 'https://family.egobrane.net'
param githubRepository = 'egobrane/FamilyBoard'
param backendImage = 'ghcr.io/egobrane/familyboard-backend@sha256:ddcae5448e13448b70ff9d81fd1f3dcf5e2ca832fa5de69c3b71a3c304558b56'
param enableCustomDomain = true
param postgresAdminPassword = readEnvironmentVariable('FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD')
