# Workspace navigation

Family Dashboard treats Home, Calendar, Tasks, Chores, and Rewards as one primary workspace. The household header and bottom navigation remain mounted while React Router changes the active workspace route.

The canonical routes remain `/`, `/calendar`, `/tasks`, `/chores`, and `/rewards`. URLs, direct links, refreshes, and browser Back/Forward therefore continue to work without depending on animation or gesture support.

## Pointer behavior

- Selecting a bottom tab changes the route and slides the destination in according to its position in the workspace order.
- A horizontal touch swipe or primary-button mouse drag can move to the adjacent route.
- A short drag snaps back. Vertical movement remains browser-controlled scrolling.
- Gestures beginning on links, buttons, form controls, editable content, dialogs, or an element marked `data-workspace-swipe-ignore` never navigate.
- The first and last workspace pages resist outward dragging and do not wrap around.

The gesture is an enhancement, not the only navigation mechanism. The bottom items remain links, the destination main region receives focus after navigation, and reduced-motion preferences disable route animation.

Programmatic route focus is applied only to the non-interactive `#main-content` region so screen readers announce the destination. Its browser-default page-sized outline is suppressed deliberately; links and buttons continue to use the application's high-contrast `:focus-visible` indicator for keyboard operation.

Creation, editing, administration, point-history, and other detail routes remain outside the swipe workspace. This prevents accidental loss of form state and keeps destructive or administrative workflows conventional.

## Layout contract

At wide wall-display sizes, Today and Chores occupy seven of twelve dashboard columns while the household image and Tasks occupy five. Rewards uses a compact full-width summary row. Tablet layouts use balanced columns, and phone layouts stack cards. The five-item navigation dock must remain on one row at every supported viewport.

No external animation or gesture dependency is used. React Router remains the navigation source of truth, Pointer Events provide touch and mouse parity, and CSS supplies progressive animation.

## Staging proof

The owner confirmed the cohesive layout, matching white bordered Calendar, Tasks, Chores, and Rewards surfaces, and removal of the page-sized blue focus outline in production. Current-commit CI independently exercises pointer tab selection, mouse drag, browser routes, responsive overflow, vertical-gesture protection, reduced motion, focus movement, and serious automated accessibility checks. Physical touch swipe and subjective transition behavior remain owner-operated checks rather than something inferred from CI alone.
