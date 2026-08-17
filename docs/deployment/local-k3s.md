# Local K3s Staging on macOS

K3s requires Linux. On macOS, this project uses [k3d](https://k3d.io/) to run K3s nodes inside Docker Desktop while preserving standard Kubernetes resources and commands.

## Local topology

- cluster: `family-dashboard-staging`;
- K3s server: one containerized server node;
- ingress HTTP: `http://api.family-dashboard.localhost:8081`;
- ingress HTTPS port reservation: `8443`;
- backend image: locally built `family-dashboard-api:local-staging` imported into k3d;
- PostgreSQL: the existing single-replica StatefulSet and persistent volume.

This proves the container, migration, Kubernetes resources, storage, service, ingress, and health checks. It does not create a public HTTPS API. An HTTPS Netlify page must not call this local HTTP origin; a real staging API domain and trusted TLS are required before frontend/API integration or Google OAuth testing.

## Prerequisites

```sh
brew install k3d
docker info
k3d version
kubectl version --client
```

## Create the cluster

```sh
k3d cluster create family-dashboard-staging \
  --servers 1 \
  --agents 0 \
  --port "8081:80@loadbalancer" \
  --port "8443:443@loadbalancer" \
  --wait
```

k3d updates the active kubeconfig context to `k3d-family-dashboard-staging`.

## Build and import the API

```sh
docker build \
  --file src/backend/FamilyDashboard.Api/Dockerfile \
  --target production \
  --tag family-dashboard-api:local-staging .

k3d image import \
  --cluster family-dashboard-staging \
  family-dashboard-api:local-staging
```

## Create the runtime secret

Apply the namespace first:

```sh
kubectl apply -f deploy/k8s/base/namespace.yaml
```

Create `family-dashboard-secrets` directly in the cluster using the local-only values from the ignored root `.env` file. The command recorded in shell history contains variable references, not their values:

```sh
set -a
source .env
set +a

kubectl -n family-dashboard create secret generic family-dashboard-secrets \
  --from-literal="postgres-database=${POSTGRES_DB}" \
  --from-literal="postgres-username=${POSTGRES_USER}" \
  --from-literal="postgres-password=${POSTGRES_PASSWORD}" \
  --from-literal="connection-string=Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}" \
  --from-literal="parent-access-pepper=${ParentAccess__Pepper}"
```

`ParentAccess__Pepper` must be a locally generated, base64-encoded random 32-byte value. For repeat runs, delete and recreate only this named local secret, or use a local secret-management workflow. Restart the API deployment and recreate any pending migration job after changing the secret because environment variables in existing pods do not update automatically. Never commit `.env` or a populated Kubernetes Secret manifest.

## Deploy and verify

```sh
kubectl apply -k deploy/k8s/local
kubectl -n family-dashboard rollout status statefulset/family-dashboard-postgres --timeout=5m
kubectl -n family-dashboard wait --for=condition=complete job/family-dashboard-migrate --timeout=5m
kubectl -n family-dashboard rollout status deployment/family-dashboard-api --timeout=5m

curl --fail http://api.family-dashboard.localhost:8081/health/live
curl --fail http://api.family-dashboard.localhost:8081/health/ready
```

The current health-only API can start while the migration job runs. Before business endpoints are deployed, production release automation must preserve the documented database-first ordering rather than relying on this local convenience flow.

## Inspect and stop

```sh
kubectl -n family-dashboard get pods,services,ingress,persistentvolumeclaims
k3d cluster stop family-dashboard-staging
```

Restart with:

```sh
k3d cluster start family-dashboard-staging
```

Deleting the cluster removes the local K3s nodes and their local volumes:

```sh
k3d cluster delete family-dashboard-staging
```

Do not delete the cluster when its local PostgreSQL data is needed and has not been backed up.
