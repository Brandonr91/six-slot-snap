# Recreate, extend, or adapt this with Codex

This project is an example of turning a concrete desktop need into a small
local utility. You can ask Codex to work on this source or use the same
approach to create another tool. Codex is used during development; the
finished controller does not call an AI service.

## Work on this repository

Download or clone the repository, open its folder in your Codex coding
environment, and give it a specific task. The root `AGENTS.md` describes the
behavior that changes must preserve. OpenAI documents persistent repository
instructions in [Custom instructions with AGENTS.md](https://learn.chatgpt.com/docs/agent-configuration/agents-md).

Start with an inspection task:

```text
Read README.md, AGENTS.md, src/SixSlotSnap.cs, and tests/GridTests.cs.
Explain how the six slots, saved ratios, window assignments, and native
resize notifications interact. Build it and run the geometry tests.
Do not launch another controller or move desktop windows during this review.
Identify one concrete improvement and describe how you would verify it.
```

Then request one change at a time, with a visible result and an acceptance
check. Examples:

### Make shortcuts configurable

```text
Add a local configuration file for the six snap shortcuts. Keep Ctrl+Alt+1
through Ctrl+Alt+6 as defaults. Validate duplicate shortcuts, release any
already registered shortcuts on failure, and show a useful conflict message.
Preserve the visual selector and all existing resizing rules. Update the
documentation and run Build.ps1 -Test. Do not publish or launch the change.
```

### Support independent monitors

```text
Replace the single active grid with per-monitor state. Each monitor should
keep its own assigned windows, column ratios, and three row dividers. Handle
negative monitor coordinates and different DPI settings. Preserve the rule
that a horizontal divider affects only its own two windows. Add geometry
tests and document a manual monitor-disconnect test before calling it done.
```

### Add optional login startup

```text
Add an explicit opt-in way to start this controller at login, plus a matching
removal command. Keep the default portable behavior. Make the installed path
visible and prevent duplicate instances. Prepare and test the scripts without
enabling startup on this computer until I request it.
```

### Diagnose a real resizing bug

```text
With all six slots occupied, dragging the middle-top window's left edge
does not keep the left column joined. Reproduce this with disposable test
windows, inspect native resize notifications and tracked assignments, and
fix the cause. Verify both sides of the middle column from both rows while
the three horizontal row dividers are at different heights. Preserve those
independent row heights. Report what was actually tested and any remaining
app-specific uncertainty.
```

## Recreate the original behavior from a specification

If you want Codex to build a similar utility rather than modify this one,
give it the desired interactions and constraints. For example:

```text
Build a small portable Windows 11 tiling controller using Windows APIs and
C#, with no external packages, Explorer injection, or replacement taskbar.

Create six logical slots: three columns by two rows. Use physical monitor
work-area coordinates and support 125% display scaling correctly. Add
Ctrl+Alt+1 through Ctrl+Alt+6 placement shortcuts and a compact nonactivating
visual selector at the top of the screen.

Link left/right boundaries across neighboring columns. Give each column
its own top/bottom divider: moving one horizontal divider resizes only its
two windows. Keep windows joined during resizing, save proportions, and
provide a tray Exit command and an equal-layout reset.

Account for invisible frame borders, app minimum sizes, maximized/minimized
windows, and hotkey conflicts. Separate pure geometry tests from interactive
desktop tests. Build and verify the geometry first. Use disposable windows
for interactive tests, and do not rearrange my real apps until I request it.
Document how to start, stop, troubleshoot, and remove the utility.
```

State your actual requirements explicitly. A two-column, three-row layout,
one shared row divider, or separate widths for each row is a different
layout model and should be described as such.

## A practical development loop

1. Describe the interaction: which edge moves, which neighbors follow, and
   which dimensions must remain independent.
2. Let Codex inspect the implementation and identify the smallest useful change.
3. Ask for tests of geometry and invariants before testing real desktop windows.
4. Build with `Build.ps1 -Test`. A successful build does not prove mouse dragging
   works across different app processes.
5. For interactive checks, stop the existing controller and use disposable
   windows. Check all six slots, both sides of each boundary, mixed row heights,
   app minimum sizes, and restart recovery.
6. Review the diff and the result, then commit the change. Keep publication and
   automatic startup as explicit actions.

These task examples and checks are project-specific suggestions. For general
Codex workflow guidance, see OpenAI's [Best practices](https://learn.chatgpt.com/docs/learn/best-practices).

## Similar functions you could build

The same approach can support a presentation workspace, a two-window research
pair, an ultrawide coding layout, a monitor-aware window launcher, or keyboard
commands that resize windows by a fixed amount. Start with a narrow behavior
and use documented operating-system interfaces wherever possible.

For another operating system, ask Codex to investigate that system's window
management and accessibility APIs first. This project's Win32 calls are
Windows-specific and cannot simply be copied to macOS or Linux.

Do not treat an AI-generated implementation or a large count of geometry
assertions as proof of desktop compatibility. Observe the interaction,
check the failure cases, and keep the tool easy to stop.
