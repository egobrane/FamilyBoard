using './main.bicep'

param location = 'centralus'
param environmentName = 'staging'
param apiHostname = 'api.egobrane.net'
param frontendOrigin = 'https://family.egobrane.net'
param githubRepository = 'egobrane/FamilyBoard'
param githubOidcSubject = 'repo:egobrane@23132912/FamilyBoard@1324023581:environment:staging'
param backendImage = 'ghcr.io/egobrane/familyboard-backend@sha256:d9192d8eb64163b0afeb612d009c7dad4b105957f8f8c3c80091961813fc320c'
param enableCustomDomain = true
param enableGoogleAuthentication = true
param enableParentAccess = true
param googleClientId = '964271653840-i22oeorkf03l0qvqdo705du1e8229re4.apps.googleusercontent.com'
param postgresAdminPassword = readEnvironmentVariable('FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD')
