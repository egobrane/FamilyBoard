param location string
param nameStem string
param tags object
param infrastructureSubnetId string
param logAnalyticsWorkspaceId string
param backendImage string

@secure()
param postgresConnectionString string

param frontendOrigin string
param apiHostname string
param enableCustomDomain bool
param enableGoogleAuthentication bool
param googleClientId string
param googleClientSecretUri string
param runtimeIdentityId string
param runtimeIdentityClientId string
param dataProtectionBlobUri string
param dataProtectionKeyIdentifier string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: last(split(logAnalyticsWorkspaceId, '/'))
}

resource environment 'Microsoft.App/managedEnvironments@2025-01-01' = {
  name: '${nameStem}-env'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: workspace.properties.customerId
        sharedKey: workspace.listKeys().primarySharedKey
      }
    }
    vnetConfiguration: {
      infrastructureSubnetId: infrastructureSubnetId
      internal: false
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

resource managedCertificate 'Microsoft.App/managedEnvironments/managedCertificates@2025-01-01' = if (enableCustomDomain) {
  parent: environment
  name: '${nameStem}-api-certificate'
  location: location
  tags: tags
  properties: {
    subjectName: apiHostname
    domainControlValidation: 'CNAME'
  }
}

resource api 'Microsoft.App/containerApps@2025-01-01' = {
  name: '${nameStem}-api'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${runtimeIdentityId}': {}
    }
  }
  properties: {
    environmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Multiple'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        customDomains: enableCustomDomain ? [
          {
            name: apiHostname
            bindingType: 'SniEnabled'
            certificateId: managedCertificate.id
          }
        ] : []
      }
      secrets: concat([
          {
            name: 'postgres-connection'
            value: postgresConnectionString
          }
        ], enableGoogleAuthentication ? [
          {
            name: 'google-client-secret'
            keyVaultUrl: googleClientSecretUri
            identity: runtimeIdentityId
          }
        ] : [])
    }
    template: {
      containers: [
        {
          name: 'api'
          image: backendImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: concat([
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Staging'
            }
            {
              name: 'ASPNETCORE_HTTP_PORTS'
              value: '8080'
            }
            {
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
              value: 'true'
            }
            {
              name: 'Cors__AllowedOrigins__0'
              value: frontendOrigin
            }
            {
              name: 'ConnectionStrings__FamilyDashboard'
              secretRef: 'postgres-connection'
            }
            {
              name: 'Authentication__FrontendOrigin'
              value: frontendOrigin
            }
            {
              name: 'Authentication__Google__Enabled'
              value: string(enableGoogleAuthentication)
            }
            {
              name: 'Authentication__Google__ClientId'
              value: googleClientId
            }
            {
              name: 'DataProtection__UseAzure'
              value: 'true'
            }
            {
              name: 'DataProtection__ApplicationName'
              value: 'FamilyDashboard'
            }
            {
              name: 'DataProtection__BlobUri'
              value: dataProtectionBlobUri
            }
            {
              name: 'DataProtection__KeyIdentifier'
              value: dataProtectionKeyIdentifier
            }
            {
              name: 'DataProtection__ManagedIdentityClientId'
              value: runtimeIdentityClientId
            }
          ], enableGoogleAuthentication ? [
            {
              name: 'Authentication__Google__ClientSecret'
              secretRef: 'google-client-secret'
            }
          ] : [])
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 3
              periodSeconds: 5
              failureThreshold: 30
              timeoutSeconds: 3
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 15
              failureThreshold: 3
              timeoutSeconds: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
                scheme: 'HTTP'
              }
              periodSeconds: 10
              failureThreshold: 6
              timeoutSeconds: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 2
        rules: [
          {
            name: 'http-requests'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

resource migrationJob 'Microsoft.App/jobs@2025-01-01' = {
  name: '${nameStem}-mig'
  location: location
  tags: tags
  properties: {
    environmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 1
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      secrets: [
        {
          name: 'postgres-connection'
          value: postgresConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'migration'
          image: backendImage
          args: [
            '--migrate'
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Staging'
            }
            {
              name: 'ConnectionStrings__FamilyDashboard'
              secretRef: 'postgres-connection'
            }
          ]
        }
      ]
    }
  }
}

output apiName string = api.name
output apiDefaultHostname string = api.properties.configuration.ingress.fqdn
output customDomainVerificationId string = environment.properties.customDomainConfiguration.customDomainVerificationId
output migrationJobName string = migrationJob.name
