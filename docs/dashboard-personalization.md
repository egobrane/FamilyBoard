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

## Household-member profile photos

Increment 2 reuses the same processor, private `household-photos` container, managed identity, and authenticated-delivery boundary for adult and child household-member profiles. Adult photos belong to the member profile inside one household rather than to the global Google account, so an adult who belongs to multiple households may have a different photo in each. Children remain profile-only and cannot administer their own photos.

- Sanitized JPEG variants use maximum edges of 128, 320, and 640 pixels for compact lists, touch dialogs, and larger profile presentation. Square avatars use CSS `object-fit` plus member-specific focal X/Y coordinates; the safely resized aspect ratio is retained and no destructive crop is stored.
- Blob prefixes are isolated as `members/{householdId}/{memberId}/{opaqueAssetId}` inside the existing private container. The API returns only household-scoped authenticated routes—never Blob URLs, SAS tokens, storage keys, original filenames, or remote image URLs.
- `HouseholdMemberPhotoAsset` retains lifecycle and attribution metadata. Upload writes a pending record and all variants before atomically switching the member's active reference. Replacement or removal makes the old asset inaccessible before best-effort blob deletion; retired metadata remains for audit and cleanup.
- `PhotoVersion` provides optimistic concurrency for upload, focal-position changes, replacement, and removal. A stale client receives `409 household_member_photo_conflict` and must refresh rather than overwriting a newer photo.
- Adults may manage any member photo in a private session. Shared-display changes require household-scoped parent-PIN elevation. Routine authenticated viewing and member attribution remain available while a shared display is locked.
- Missing, removed, or failed image loads fall back to the member's initials and configured avatar color. Inactive members remain visible in retained history and may retain an active private photo until an adult explicitly removes it.

The member-photo implementation is locally validated but not yet recorded as deployed. The existing Increment 1 household-photo staging evidence below remains the current production claim.

## Staging status

Increment 1 is deployed and owner verified. Migration `20260831212841_AddDashboardPersonalizationAndWeather` is applied; the existing locked-down storage account contains private container `household-photos`; the API uses its user-assigned managed identity rather than a storage key; and the corrected multipart endpoint accepts the application antiforgery header without invoking ASP.NET's separate form-antiforgery metadata. Owner testing confirmed upload, authenticated display, replacement, removal, focal positioning, custom and automatic greetings, and fallback behavior. The correction is covered by a real-cookie PostgreSQL endpoint test so the original staging-only failure cannot regress silently.

## Configuration and rollback

`HouseholdMedia__Enabled` is the feature gate. `HouseholdMedia__Provider` is `FileSystem` locally/K3s and `AzureBlob` in staging. `HouseholdMedia__LocalPath`, `HouseholdMedia__BlobContainerUri`, and the public managed-identity client ID select storage; none is a storage credential. Storage credentials never enter frontend configuration.

Rollback disables `HouseholdMedia__Enabled` or restores the previous API revision. The additive database tables and private blobs remain intact. Do not roll the schema backward by deleting household photos; use a forward fix.
