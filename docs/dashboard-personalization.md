# Dashboard personalization

Dashboard personalization belongs to the selected household and is separate from regional configuration. `HouseholdDashboardAppearance` stores optional greeting text, CSS focal coordinates, a concurrency version, and an optional reference to the current `HouseholdPhotoAsset`. The existing public demonstration image is only a fallback; authenticated uploads are private household data.

Adults configure appearance at `/households/{householdId}/settings/appearance`. Reads require household membership. Writes, upload, replacement, and removal require adult administration, antiforgery validation, and parent-PIN elevation on a shared display. The backend never accepts remote image URLs.

## Photo processing and storage

- Accepted inputs are JPEG, PNG, and WebP, verified by decoding the file rather than trusting its name or browser MIME header.
- Uploads are limited to 10 MiB, 40 megapixels, and 12,000 pixels in either dimension.
- The backend applies EXIF orientation, discards the original, strips metadata by re-encoding, and writes sanitized JPEG variants at maximum widths of 720, 1,440, and 2,560 pixels.
- CSS focal X/Y coordinates avoid destructive server-side cropping in this increment.
- Azure uses the existing private storage account and managed identity with a dedicated `household-photos` container. Public blob access and shared keys remain disabled. The API authorizes and streams every image.
- Docker Compose uses the `household-photos` named volume. K3s uses the `family-dashboard-household-photos` PVC. The local filesystem implementation is behind the same `IHouseholdPhotoStore` boundary.

Photo URLs contain an opaque asset ID and variant only. Replacing a photo creates a new ID. Browser responses use `private, no-cache` so every display revalidates the authenticated household boundary rather than retaining a usable year-long copy after logout. Database activation happens only after all variants are stored. A failed database write removes the new prefix. A replaced asset is made inaccessible first, then best-effort cleanup removes its blobs; retained metadata supports cleanup retry without exposing the retired photo.

ImageSharp is pinned to 3.1.12. Version 4 requires a separate Six Labors build-time license key; this open-source project does not commit or inject such a key. Reassess licensing, security notices, and alternatives before upgrading.

## Configuration and rollback

`HouseholdMedia__Enabled` is the feature gate. `HouseholdMedia__Provider` is `FileSystem` locally/K3s and `AzureBlob` in staging. `HouseholdMedia__LocalPath`, `HouseholdMedia__BlobContainerUri`, and the public managed-identity client ID select storage; none is a storage credential. Storage credentials never enter frontend configuration.

Rollback disables `HouseholdMedia__Enabled` or restores the previous API revision. The additive database tables and private blobs remain intact. Do not roll the schema backward by deleting household photos; use a forward fix.
