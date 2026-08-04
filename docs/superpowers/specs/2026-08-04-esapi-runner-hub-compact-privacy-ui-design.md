# ESAPI Runner Hub Compact Privacy UI Design

**Status:** Approved design direction (Design A)

**Date:** 2026-08-04

**Target release:** v0.3.0, build 19

## Purpose

The ESAPI Runner Hub shall present patient context, application discovery, and recent activity in one compact, readable window without horizontal scrolling. The redesign also makes screenshots safe to share by adding an explicit privacy display mode and makes all product-facing text and public documentation English.

The release keeps the Hub's current operational model: one ESAPI-backed process owns the patient search and context selection, while each launched tool runs in an independent child process. A child error must not close the Hub.

## Goals

- Make the catalogue the visual center of the window and remove the narrow category rail.
- Keep patient, course, plan, plan sum, structure set, and image selection immediately available.
- Fit the complete workflow into a 1920 x 1080 screenshot and remain usable at a 1080 x 680 minimum window size.
- Eliminate horizontal scrolling from the catalogue, filters, category navigation, and recent activity.
- Make `Run again` react immediately when application readiness or protected context availability changes.
- Explain why replay is unavailable instead of presenting an unexplained disabled button.
- Add a one-click privacy display mode for screenshots and demonstrations.
- Use English for all user-visible application, launcher, script-host, and live catalogue text.
- Document why a single published Citrix Hub is useful for standalone and context-aware ESAPI applications.

## Non-goals

- The privacy mode is not anonymization of stored logs, settings, command-line arguments, or ESAPI objects.
- The redesign does not change ESAPI permissions, Eclipse write-safety behavior, or clinical approval responsibilities.
- The Hub does not become a general-purpose shell or execute arbitrary commands.
- The release does not change which clinical context a tool requires.
- Internal STR Hub operational documentation outside this repository is not translated wholesale; product copy referenced by it may be updated as part of the release registration.

## Evidence and current defect

Activity rows are loaded before asynchronous application path probes have necessarily completed. `RefreshRelaunchAvailability` later updates `ActivityRowViewModel.CanRunAgain`, but the shared `RunAgainCommand` is not told that its `CanExecute` result changed. WPF can therefore continue rendering the replay button as disabled even after the row becomes eligible.

This is a command-notification defect, not a failure of the protected context history. The repair must preserve the existing DPAPI-protected activity context and the current application-readiness checks.

## Information architecture

### Window frame

The window uses three vertical areas:

1. A compact header, approximately 76 px high.
2. A flexible main area that consumes the remaining height.
3. A recent-activity area, approximately 210–220 px high.

The minimum supported window size is 1080 x 680. The target documentation screenshot is 1920 x 1080.

### Main area

The main area changes from three columns to two:

- **Patient context:** a fixed 328–340 px panel on the left.
- **Application catalogue:** the remaining width on the right.

The separate category side rail is removed. This gives the catalogue enough width for readable cards and eliminates the category rail's horizontal scrollbar.

### Catalogue toolbar

One centered toolbar above the cards contains, in this order:

1. A tool search box.
2. A category selector with `All categories` as its first entry.
3. A format selector with `All formats` as its first entry.
4. A compact `Reset filters` action that is enabled only when a filter is active.

The toolbar wraps only at the minimum supported width. Its controls remain centered as a group, and it never requires horizontal scrolling.

### Application cards

The catalogue uses a vertically scrolling, horizontally disabled `ScrollViewer` containing a centered wrapping panel. Cards use a compact width of approximately 316–320 px with consistent 6–8 px spacing.

- At 1920 x 1080, the catalogue shall show four cards per row when space permits.
- At the minimum window width, it shall show at least two cards per row.
- Card paths wrap to a second line when necessary.
- Path text remains shortened from the `Physik-Skripte` anchor when that anchor is available.
- Full paths may be exposed in a tooltip only while privacy mode is off.
- Categories, format, access mode, readiness, context requirements, documentation, and launch actions remain visible on each card.

### Patient context

The existing selection workflow remains in the left panel:

- Patient search and selected patient summary.
- Course.
- Plan, displayed with its course where needed to disambiguate it.
- Plan sum.
- Structure set.
- Image.

The panel remains vertically usable at minimum height. Selection controls must not introduce a horizontal scrollbar.

### Recent activity

Recent activity spans the full window width. The fixed-width `GridView` is replaced or restyled as a responsive row grid with proportional columns for application, format, context, start time, status, and action.

The activity list may scroll vertically, but not horizontally. Context text truncates with an ellipsis at small widths and shows its full value in a tooltip only while privacy mode is off.

## Replay behavior

`Run again` remains subject to all current safety gates:

- The application still exists in the catalogue.
- Its configured path is ready.
- The required protected context can be decrypted and resolved.
- The activity is not currently in a starting or running state.

Whenever one of these inputs changes, the replay command must raise `CanExecuteChanged`. This includes initial history loading, completion of application path probes, catalogue refresh, context availability refresh, and child-process terminal-state transitions.

Every activity row exposes a replay availability message. The button tooltip communicates one of these states:

- `Ready to run again`
- `Application path is unavailable`
- `Protected context is unavailable`
- `Application was removed from the catalogue`
- `The application is still running`

A terminal `Completed`, `Exited`, or `Failed` row becomes replayable immediately when the other safety gates pass. Replaying uses the protected context stored with that row, not the context currently selected in the patient panel.

## Privacy display mode

### Control

The header gains a toggle button next to `Settings`:

- Off: `Privacy blur`
- On: `Show details`

The mode is off at startup and is not persisted. It is a presentation-only state on the main view model.

### Protected visual elements

When enabled, a consistent blur or opaque privacy treatment covers values that could identify a patient or reveal internal infrastructure:

- Patient search input and suggestions.
- Selected patient name and identifier.
- Context counts and selected course, plan, plan sum, structure set, and image values.
- Patient-specific text embedded in card launch buttons.
- Application path text.
- Activity context summaries.

Labels, application names, formats, access modes, statuses, and general controls remain readable. Tooltips that would reveal a full path or patient context are suppressed while privacy mode is on; visual blur alone is not sufficient.

The mode must not mutate, redact, or replace:

- ESAPI objects or the selected clinical context.
- Child-process arguments or command requests.
- Activity history or protected context payloads.
- Runtime or audit logs.
- `settings.ini` values.

The GitHub screenshot is produced from the offline UI smoke mode with synthetic data and privacy mode enabled. It must contain no real patient information and no complete institutional file path.

## English product copy

The release converts user-visible product copy to English in:

- The main Hub and settings window.
- ESAPI Script Host dialogs.
- Citrix launcher messages.
- Live application names, descriptions, categories, requirement messages, and status text in `dist/settings.ini` where translation is applicable.
- Public README and Citrix/CLI documentation in this repository.

Established product names, executable names, clinical identifiers, and filesystem paths are not translated. Existing live path settings must be preserved during packaging.

## Citrix documentation message

The public README gains a concise `Why a Citrix runner?` section explaining:

- A Citrix Published Application commonly exposes one configured application rather than an application-server desktop.
- Without a Hub, each standalone ESAPI utility typically needs its own published Citrix application, a dedicated link in the blue ARIA toolbar, or interactive remote-desktop access to the application server.
- The Hub provides one stable published entry point while individual tools remain independently configured and versioned.
- Patient and treatment context can be selected once and reused for multiple compatible tools.
- Independent child processes prevent one tool crash from closing the Hub and allow users to continue with another tool.
- The request-based CLI route supports reproducible, user-scoped debugging without turning the Hub into a remote shell.

The section also states that the Hub is an operational launcher, not a substitute for Eclipse permissions, tool validation, or clinical review.

## Documentation screenshot

The release creates `docs/images/esapi-runner-hub-overview.png` and embeds it near the README overview and Citrix rationale.

The screenshot shall:

- Use a 1920 x 1080 window.
- Use offline/synthetic patient and application data.
- Show privacy mode enabled.
- Show the centered combined filter toolbar.
- Show four catalogue cards in the first row when layout space permits.
- Include recent activity with a visibly enabled replay action on an eligible terminal row.
- Contain no real patient data, request identifier, or complete institutional path.

## Test strategy

Implementation starts with failing tests for the behavior being changed.

### View-model and replay tests

- A history row that becomes ready after an asynchronous path probe raises replay command `CanExecuteChanged` and becomes executable.
- A terminal eligible row exposes `Ready to run again`.
- Missing application, unavailable path, unavailable protected context, and running-state rows expose the expected disabled reason.
- Replaying still uses the row's protected context rather than the current selection.
- The privacy command toggles the presentation state and defaults to off.

### UI shape tests

- The category side rail is absent.
- Search, category, and format controls are in one centered toolbar.
- Catalogue and activity horizontal scrolling are disabled.
- Card paths wrap instead of forcing horizontal growth.
- Sensitive display regions and sensitive tooltips bind to privacy mode.
- Recent activity uses responsive columns rather than fixed widths that exceed the supported window.

### Localization and documentation checks

- User-visible launcher and script-host strings are English.
- Live catalogue descriptions are English while configured paths remain unchanged.
- README contains the Citrix rationale and the privacy-safe screenshot.

### Verification

- Run the complete unit-test suite.
- Run both launcher integration suites.
- Run the vendor-free dependency check.
- Build all release binaries successfully.
- Visually inspect offline UI smoke mode at 1920 x 1080 and 1080 x 680.
- Confirm no horizontal scrollbar appears in the toolbar, catalogue, or activity area at either size.
- Inspect the final screenshot for patient and path leakage before publishing it.

## Packaging and release

This is a meaningful UI and documentation release and therefore targets v0.3.0, build 19.

Packaging must:

- Preserve the productive `dist/settings.ini`, especially all live paths and Citrix routing settings.
- Create an immutable `ESAPI-Runner-Hub.v0.3.0.exe` binary.
- Update `current.txt` only after verification succeeds.
- Update `versionInfo.json` and `CHANGELOG.md`.
- Commit and tag the release, push the local and public Git remotes, and create the GitHub release.
- Update the relevant STR Hub InHouse entries and verify the visible version.

## Acceptance criteria

1. At 1920 x 1080, the combined filter toolbar is centered, the catalogue has no horizontal scrollbar, and four cards fit per row when the available width permits.
2. At 1080 x 680, patient selection and at least two catalogue cards per row remain usable without horizontal scrolling.
3. A completed, exited, or failed history row becomes replayable as soon as its application and protected context are available.
4. Every disabled replay action has a specific English explanation.
5. Privacy mode obscures every patient/context/path display listed above and suppresses revealing tooltips without changing runtime data.
6. All product-facing UI, launcher, script-host, live catalogue, and public repository documentation text is English.
7. The README explains the Citrix publication advantage and embeds a synthetic, privacy-safe overview screenshot.
8. Existing ESAPI safety gates, protected activity history, child-process isolation, live paths, and application definitions continue to work.
9. All automated release checks and both target-size visual inspections pass before v0.3.0 is published.
