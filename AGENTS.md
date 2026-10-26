# Working on Six Slot Snap

## Build and test

Run `./Build.ps1 -Test` on Windows. It compiles the controller with the local
.NET Framework C# compiler and runs geometry tests without moving desktop windows.
Do not introduce external packages or replace this build path without a reason.

## Behavior to preserve

- Six slots: three columns and two rows.
- Shared left/right boundaries connect neighboring columns in both rows.
- Each column has its own independent top/bottom divider. Never move all
  three horizontal dividers merely because they happen to be aligned.
- Preserve physical-pixel DPI handling and invisible-frame compensation.
- Keep the visual selector nonactivating and retain clean hotkey cleanup.
- A move-out releases a slot; a resize keeps neighboring edges joined.
- Keep the controller local: no network service or AI runtime dependency.

## Verification

Add geometry regression coverage for meaningful layout changes. Geometry tests
do not prove native dragging works: report which desktop interactions were
actually checked. Use disposable windows for interactive tests. Do not rearrange
real apps, change system settings, enable startup, or launch a second controller
unless the user requests it. Prefer a small reviewable change.

## Repository hygiene

Do not commit `bin/`, runtime logs, layout preferences, account details, or
machine-specific helper scripts. Document user-visible changes and limits.
Do not publish, upload a release, or enable automatic startup unless requested.
