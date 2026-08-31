param location string
@maxLength(26)
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
param enableGoogleCalendar bool
param enableGoogleCalendarEventCreation bool
param enableGoogleCalendarEventManagement bool
param googleCalendarClientId string
param googleCalendarClientSecretUri string
param enableGoogleTasks bool
param enableGoogleTaskMutations bool
param googleTasksClientId string
param googleTasksClientSecretUri string
param enableParentAccess bool
param parentAccessPepperSecretUri string
param runtimeIdentityId string
param runtimeIdentityClientId string
param dataProtectionBlobUri string
param dataProtectionKeyIdentifier string
param enableHouseholdMedia bool
param householdPhotosContainerUri string
param enableWeather bool
param choreGenerationHorizonHours int
param choreGenerationMaximumAssignmentsPerRun int

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
        ] : [], enableParentAccess ? [
          {
            name: 'parent-access-pepper'
            keyVaultUrl: parentAccessPepperSecretUri
            identity: runtimeIdentityId
          }
        ] : [], enableGoogleCalendar ? [
          {
            name: 'google-calendar-client-secret'
            keyVaultUrl: googleCalendarClientSecretUri
            identity: runtimeIdentityId
          }
        ] : [], enableGoogleTasks ? [
          {
            name: 'google-tasks-client-secret'
            keyVaultUrl: googleTasksClientSecretUri
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
            {
              name: 'ChoreGeneration__HorizonHours'
              value: string(choreGenerationHorizonHours)
            }
            {
              name: 'ChoreGeneration__MaximumAssignmentsPerRun'
              value: string(choreGenerationMaximumAssignmentsPerRun)
            }
            {
              name: 'HouseholdMedia__Enabled'
              value: string(enableHouseholdMedia)
            }
            {
              name: 'HouseholdMedia__Provider'
              value: 'AzureBlob'
            }
            {
              name: 'HouseholdMedia__BlobContainerUri'
              value: householdPhotosContainerUri
            }
            {
              name: 'HouseholdMedia__ManagedIdentityClientId'
              value: runtimeIdentityClientId
            }
            {
              name: 'Weather__Enabled'
              value: string(enableWeather)
            }
            {
              name: 'Weather__Provider'
              value: 'Nws'
            }
          ], enableGoogleAuthentication ? [
            {
              name: 'Authentication__Google__ClientSecret'
              secretRef: 'google-client-secret'
            }
          ] : [], enableParentAccess ? [
            {
              name: 'ParentAccess__Enabled'
              value: 'true'
            }
            {
              name: 'ParentAccess__Pepper'
              secretRef: 'parent-access-pepper'
            }
          ] : [], enableGoogleCalendar ? [
            {
              name: 'GoogleCalendar__Enabled'
              value: 'true'
            }
            {
              name: 'GoogleCalendar__EventCreationEnabled'
              value: string(enableGoogleCalendarEventCreation)
            }
            {
              name: 'GoogleCalendar__EventManagementEnabled'
              value: string(enableGoogleCalendarEventManagement)
            }
            {
              name: 'GoogleCalendar__ClientId'
              value: googleCalendarClientId
            }
            {
              name: 'GoogleCalendar__ClientSecret'
              secretRef: 'google-calendar-client-secret'
            }
            {
              name: 'GoogleCalendar__CallbackUrl'
              value: 'https://${apiHostname}/api/integrations/google-calendar/callback'
            }
          ] : [], enableGoogleTasks ? [
            {
              name: 'GoogleTasks__Enabled'
              value: 'true'
            }
            {
              name: 'GoogleTasks__MutationsEnabled'
              value: string(enableGoogleTaskMutations)
            }
            {
              name: 'GoogleTasks__ClientId'
              value: googleTasksClientId
            }
            {
              name: 'GoogleTasks__ClientSecret'
              secretRef: 'google-tasks-client-secret'
            }
            {
              name: 'GoogleTasks__CallbackUrl'
              value: 'https://${apiHostname}/api/integrations/google-tasks/callback'
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

resource choreGeneratorJob 'Microsoft.App/jobs@2025-01-01' = {
  name: '${nameStem}-chore'
  location: location
  tags: tags
  properties: {
    environmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Schedule'
      replicaTimeout: 1800
      replicaRetryLimit: 2
      scheduleTriggerConfig: {
        cronExpression: '7 * * * *'
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
          name: 'generator'
          image: backendImage
          args: [
            '--generate-chore-assignments'
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
            {
              name: 'ChoreGeneration__HorizonHours'
              value: string(choreGenerationHorizonHours)
            }
            {
              name: 'ChoreGeneration__MaximumAssignmentsPerRun'
              value: string(choreGenerationMaximumAssignmentsPerRun)
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
output choreGeneratorJobName string = choreGeneratorJob.name
