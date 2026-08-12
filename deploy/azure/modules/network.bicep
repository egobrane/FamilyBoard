param location string
param nameStem string
param tags object

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: '${nameStem}-vnet'
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.42.0.0/16'
      ]
    }
    subnets: [
      {
        name: '${nameStem}-aca-subnet'
        properties: {
          addressPrefix: '10.42.0.0/24'
          delegations: [
            {
              name: 'container-apps-environments'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: '${nameStem}-postgres-subnet'
        properties: {
          addressPrefix: '10.42.1.0/28'
          delegations: [
            {
              name: 'postgres-flexible-servers'
              properties: {
                serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers'
              }
            }
          ]
        }
      }
    ]
  }
}

resource postgresPrivateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: '${nameStem}.postgres.database.azure.com'
  location: 'global'
  tags: tags
}

resource postgresDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: postgresPrivateDnsZone
  name: '${nameStem}-vnet-link'
  location: 'global'
  tags: tags
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

output containerAppsSubnetId string = resourceId('Microsoft.Network/virtualNetworks/subnets', virtualNetwork.name, '${nameStem}-aca-subnet')
output postgresSubnetId string = resourceId('Microsoft.Network/virtualNetworks/subnets', virtualNetwork.name, '${nameStem}-postgres-subnet')
output postgresPrivateDnsZoneId string = postgresPrivateDnsZone.id
