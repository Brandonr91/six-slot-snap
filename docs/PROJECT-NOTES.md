# Project notes

## Origin and behavior

Six Slot Snap was made for a 5K2K ultrawide Windows desktop where six windows
needed to remain useful after placement. The important distinction was
between remembering six rectangles and continuously managing their shared
boundaries.

The resulting model has two shared vertical column boundaries and three
independent horizontal row boundaries. The top and bottom windows in a column
always share its width. The three row dividers can be at different heights.

## Implementation map

All controller code is currently in `src/SixSlotSnap.cs`:

| Component | Responsibility |
| --- | --- |
| `Grid` | Convert ratios into six rectangles and adjust shared boundaries. |
| `Snap` / `PlaceWindow` | Track the selected window and position its visible frame. |
| `Selector` | Provide a nonactivating six-cell selector and collapsed hover tab. |
| `HotkeyWindow` | Receive the six registered global shortcuts. |
| `OnWindowEvent` / `UpdateDrag` | Observe native move/size notifications and update neighbors. |
| `RecoverAssignments` | Reconnect occupied slots from their visible geometry. |
| `LoadLayout` / `SaveLayout` | Persist the two column ratios and three row ratios. |

The controller uses a per-monitor DPI-aware thread context before reading
monitor and window geometry. It compares outer window bounds with DWM visible
frame bounds to account for invisible borders. This mattered on the original
display: DPI-scaled coordinates described a 4096 × 1728 desktop while the
physical display was 5120 × 2160 at 125% scaling.

Native move/size start and end events identify the window being manipulated.
A timer samples its visible rectangle during the drag. Other assigned windows
follow the updated grid; the controller avoids repeatedly positioning the
window currently under the mouse. After release, it aligns the managed windows
and performs a short settling pass to handle delayed native adjustments.

## Lessons from the first version

- A successful six-slot placement test did not establish linked resizing.
- All six assignments must remain connected, including windows in neighboring
  columns. One displaced window can make an otherwise correct layout look broken.
- A saved divider ratio can be stale after a native resize or a restart. Matching
  existing top/bottom pairs provides better recovery evidence in that case.
- Moving a window away and resizing an internal edge are different actions.
  Moving away should release the slot instead of reshaping the entire grid.
- Each column needs a separate row ratio. Applying one global row height would
  violate the intended interaction even when the dividers happen to line up.

## Verification and limits

The committed geometry test runner performs deterministic layout and boundary
checks at two monitor origins, including randomized extreme resize inputs.
It does not touch desktop windows. Development also included controlled six-window
integration checks with native move/size notifications and observed window bounds.

The interactive checks used a development harness; that harness is not included
as a portable integration suite. They do not cover every frame style, privilege
level, app minimum size, or real mouse-drag timing. The project should be treated
as an early utility with known constraints, not a replacement for every Windows
window-management feature.

## Possible next work

- A dedicated cross-process, interactive regression harness.
- Better minimum-size negotiation before resizing neighbors.
- Per-monitor state and display-change handling.
- Configurable shortcuts and layout definitions.
- Explicit assignment persistence that survives app restarts.
- Optional startup registration with an equally simple removal path.
- Code-signing and a reproducible release pipeline if binary downloads are added.

No telemetry or network client is implemented. Logs and ratios stay beside
the built executable and are excluded from version control. Review logs before
sharing them in an issue.
