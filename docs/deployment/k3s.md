# K3s Deployment

The manifests use standard Kubernetes resources plus Kustomize syntax supported by `kubectl`. They assume K3s provides a default persistent storage class and ingress controller, but they do not name Traefik or use a K3s-specific API.

For Docker-backed local K3s on macOS, use the [local k3d staging runbook](local-k3s.md) and `deploy/k8s/local` overlay.

## Before deployment

Decide and document:

- API and frontend DNS names;
- TLS certificate management;
- GHCR image visibility and pull credentials;
- PostgreSQL storage class;
- encrypted backup location and restore procedure.

Replace the example hosts in `api-configmap.yaml` and `api-ingress.yaml`. Set the backend image to an immutable release tag or digest in the K3s overlay.

## Runtime secret

Create `family-dashboard-secrets` directly in the cluster or through an approved secret manager. It requires these keys:

- `postgres-database`
- `postgres-username`
- `postgres-password`
- `connection-string`

Use real values only at the terminal or secret-management boundary. Never create a populated Secret YAML file in the repository.

## Initial rollout

1. Render and review the release:

   ```sh
   kubectl kustomize deploy/k8s/k3s
   ```

2. Apply the namespace, configuration, PostgreSQL service, and StatefulSet.
3. Wait for PostgreSQL readiness.
4. Apply the migration Job and wait for it:

   ```sh
   kubectl -n family-dashboard wait --for=condition=complete job/family-dashboard-migrate --timeout=5m
   ```

5. Apply the API Deployment, Service, and Ingress.
6. Verify `/health/live`, `/health/ready`, and the Deployment rollout.

The base Kustomization renders the complete desired state for review and initial setup. A release operator should preserve the sequence above; `kubectl apply -k` alone does not wait for migrations before starting the API. A later controlled deployment workflow should automate this ordering.

Completed Jobs are immutable. Delete the previous completed migration Job before applying a new release job with a different image.

## Database caution

The included single-replica PostgreSQL StatefulSet is a foundation, not high availability. Its PVC does not constitute a backup. Use an external PostgreSQL service or establish automated off-cluster backups and tested restores before treating household data as durable production data.
