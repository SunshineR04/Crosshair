# Standalone Release Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Make the independent WPF + Widget release installable and reproducible for ordinary users, while making file synchronization reliable and keeping AppService correct for future packaged deployments.

**Architecture:** The supported release remains two coordinated products: a self-contained WPF executable and a separately installed Xbox Game Bar Widget MSIX. The Widget file envelope is the authoritative standalone synchronization channel; AppService is repaired but optional. A PowerShell release pipeline builds a clean staging directory, validates the MSIX and manifest, writes package-location metadata, and emits a hash/version manifest consumed by the installer.

**Tech Stack:** .NET 8 WPF, C# 8 UWP/.NET Native, SkiaSharp, Win32 layered windows, Xbox Game Bar SDK, System.Text.Json, Newtonsoft.Json, xUnit, PowerShell, MakeAppx/SignTool/MSBuild.

## Global Constraints

- Preserve the supported standalone architecture: WPF EXE + Widget MSIX; do not make combined MSIX a release prerequisite.
- Keep Widget source compatible with `LangVersion=8.0`; shared files linked into Widget must use C# 8-compatible syntax and block-scoped namespaces.
- Preserve backward compatibility with existing bare `CrosshairProfile` JSON files; treat them as synchronization revision `0`.
- Keep atomic file replacement for Widget settings and never update the accepted-content marker before successful parse, sanitize, and revision checks.
- Never put signing passwords, private keys, or generated release payloads into Git.
- Do not use `Add-AppxPackage -ForceUpdateFromAnyVersion` in the user installer.
- Do not claim a build or clean-machine installation is verified until the corresponding command/environment has actually been run.
- Do not commit changes unless the user explicitly requests a commit.

---

## File Map

### New files

- `CrosshairOverlay/Models/CrosshairSyncEnvelope.cs` — C# 8-compatible cross-process envelope model, linked into the Widget project.
- `CrosshairOverlay/Services/IProfileSyncService.cs` — testable abstraction for profile pushes and initial sync.
- `build/ReleaseVersion.json` — single source for the public release version and package version.
- `build/Build-Release.ps1` — clean WPF/Widget staging, version injection, optional signing, dependency collection, ZIP creation, and release manifest generation.
- `build/Validate-Release.ps1` — archive allowlist, hash, version, certificate, dependency, and embedded-manifest validation.
- `build/Validate-Manifests.ps1` — structural checks for the standalone Widget manifest and the retained combined-package manifest.

### Modified files

- `CrosshairOverlay/Models/CrosshairProfile.cs` — only if required to preserve serialization compatibility; do not add envelope fields to the profile itself.
- `CrosshairOverlay/Services/ISettingsService.cs` — expose persistent sync revision operations and revision-aware Widget writes.
- `CrosshairOverlay/Services/SettingsService.cs` — write envelopes, persist revisions atomically, consume installer package-location metadata, and log unavailable fallback paths.
- `CrosshairOverlay/Services/AppServiceServer.cs` — implement `IProfileSyncService`, carry revisions, and return/handle protocol acknowledgements.
- `CrosshairOverlay/ViewModels/MainViewModel.cs` — route every effective profile change, including visibility, through one revision/publish path.
- `CrosshairOverlay/App.xaml.cs` — initialize the first synchronized snapshot with a revision and keep shutdown ordering intact.
- `CrosshairOverlay.Widget/CrosshairOverlay.Widget.csproj` — link the shared envelope model if compatible.
- `CrosshairOverlay.Widget/Services/AppServiceClient.cs` — send ACK/error responses, apply revision ordering, and fix deferral/connection ownership.
- `CrosshairOverlay.Widget/CrosshairPage.xaml.cs` — serialized cancellable fallback reads, envelope/legacy parsing, revision arbitration, dynamic metrics refresh, and safe centering.
- `CrosshairOverlay.Widget/CrosshairPage.xaml` — only adjust hit testing after verifying Xbox Game Bar host behavior; retain the explicit click-through UX warning if host controls it.
- `CrosshairOverlay/Rendering/OverlayHost.cs` — target-monitor DPI lookup and synchronized ForceTopmost timer/HWND lifecycle.
- `CrosshairOverlay/Package/PackageLayout/AppxManifest.xml` — reconcile semantic drift with the canonical combined manifest if the file remains supported.
- `Release/setup.ps1` — consume release-manifest metadata, validate trust/version/dependencies, write actual Widget package location, and stop on failed installation.
- `Release/setup.bat` — keep as a thin wrapper and update only messages/exit handling if needed.
- `README.md` — document standalone release behavior, prerequisites, file fallback, package limitations, and exact build/release commands.
- `AGENTS.md` — update commands and architecture notes to match the actual standalone release pipeline.
- `.gitignore` — ignore generated build staging/output while allowing release scripts, version metadata, and validation scripts.
- `CrosshairOverlay.Tests/MainViewModelTests.cs` — fake sync service and visibility/revision regression tests.
- `CrosshairOverlay.Tests/SettingsServiceTests.cs` — envelope, revision, legacy, malformed, and package-location tests.
- `CrosshairOverlay.Tests/AppServiceServerTests.cs` — initial/push revision contract tests through the new abstraction.
- `CrosshairOverlay.Tests/CrosshairSyncEnvelopeTests.cs` — envelope and legacy compatibility tests.

---

## Task 1: Add a revisioned, backward-compatible sync contract

**Files:**
- Create: `CrosshairOverlay/Models/CrosshairSyncEnvelope.cs`
- Modify: `CrosshairOverlay.Widget/CrosshairOverlay.Widget.csproj`
- Modify: `CrosshairOverlay/Services/ISettingsService.cs`
- Create: `CrosshairOverlay/Services/IProfileSyncService.cs`
- Modify: `CrosshairOverlay/Services/AppServiceServer.cs`
- Test: `CrosshairOverlay.Tests/CrosshairSyncEnvelopeTests.cs`

**Interfaces:**
- `CrosshairSyncEnvelope` exposes `int SchemaVersion`, `long Revision`, `DateTime UpdatedUtc`, and `CrosshairProfile Profile`.
- `ISettingsService` exposes `long CurrentSyncRevision { get; }`, `long NextSyncRevision()`, and `void SaveForWidget(CrosshairProfile profile, long revision)` while retaining a compatibility overload for existing callers.
- `IProfileSyncService` exposes `Task InitializeAsync(CrosshairProfile profile, long revision)`, `Task PushProfile(CrosshairProfile profile, long revision)`, and `void Dispose()`.
- `AppServiceServer` implements the new interface; its latest snapshot stores both profile and revision.

- [ ] **Step 1: Write failing model and serialization tests.**

  Add tests that construct an envelope, assert all fields round-trip through `System.Text.Json`, and assert a bare legacy profile can still be deserialized independently as revision `0`.

- [ ] **Step 2: Run the focused tests and verify the new contract is absent/fails.**

  Run:
  ```powershell
  dotnet test CrosshairOverlay.Tests --no-restore --filter FullyQualifiedName~CrosshairSyncEnvelope
  ```
  Expected: compilation/test failure because the envelope and new members do not exist yet.

- [ ] **Step 3: Implement the C# 8-compatible envelope.**

  Use a block-scoped namespace and auto-properties only; do not use file-scoped namespaces, target-typed `new`, nullable syntax unsupported by the Widget toolchain, or lambda discard syntax. Link this file into the Widget project instead of duplicating its schema.

- [ ] **Step 4: Add revision-aware interfaces and server signatures.**

  Keep `CrosshairProfile` unchanged so old settings remain readable. Change the AppService server’s stored snapshot and `InitializeAsync`/`PushProfile` signatures to carry revision while preserving a compatibility overload that uses revision `0` for existing standalone callers.

- [ ] **Step 5: Run the focused tests again.**

  Expected: all envelope and legacy compatibility tests pass; the rest of the existing suite still compiles.

- [ ] **Step 6: Run the full desktop test suite.**

  Run:
  ```powershell
  dotnet test CrosshairOverlay.Tests --no-restore
  ```
  Expected: no regressions before moving to storage changes.

---

## Task 2: Persist revisions and route every WPF change through one sync path

**Files:**
- Modify: `CrosshairOverlay/Services/SettingsService.cs`
- Modify: `CrosshairOverlay/Services/ISettingsService.cs`
- Modify: `CrosshairOverlay/ViewModels/MainViewModel.cs`
- Modify: `CrosshairOverlay/App.xaml.cs`
- Modify: `CrosshairOverlay.Tests/SettingsServiceTests.cs`
- Modify: `CrosshairOverlay.Tests/MainViewModelTests.cs`
- Modify: `CrosshairOverlay.Tests/AppServiceServerTests.cs`

**Interfaces:**
- `SettingsService` persists a monotonic revision beside the normal settings file and writes `widget_settings.json` as `CrosshairSyncEnvelope`.
- `MainViewModel.ApplyProfile()` obtains one new revision, writes that exact revision to the fallback file, and pushes that same revision to `IProfileSyncService`.
- `IsVisible` must call the same publish path after local show/hide handling, while still writing immediately rather than waiting for the 400ms settings debounce.

- [ ] **Step 1: Add failing SettingsService tests.**

  Cover: first revision is positive, revisions increase across service instances, envelope contains the supplied revision and profile, bare legacy settings still load, malformed main settings still return defaults, and an injected package-location directory is used.

- [ ] **Step 2: Run the focused settings tests and confirm the expected failures.**

  Run:
  ```powershell
  dotnet test CrosshairOverlay.Tests --no-restore --filter FullyQualifiedName~SettingsServiceTests
  ```
  Expected: failures for revision/envelope assertions.

- [ ] **Step 3: Implement atomic revision persistence.**

  Add a revision metadata file under the injected app-data directory. Read invalid/missing values as `0`; allocate the next positive revision under a lock; persist it with the existing atomic writer. Serialize the Widget envelope with `System.Text.Json` and preserve the existing settings file format for the desktop profile.

- [ ] **Step 4: Add failing MainViewModel visibility/sync tests.**

  Replace the concrete AppService dependency in tests with a fake `IProfileSyncService`. Assert that a visible-to-hidden transition calls the fake with a newer revision and that `SaveForWidget` receives the same revision. Assert that a failed local `Show()` does not publish a visible profile.

- [ ] **Step 5: Implement the single publish path.**

  Change `MainViewModel` to accept `IProfileSyncService?`, centralize sanitization, revision allocation, fallback write, IPC push, and save-debounce startup in `ApplyProfile()`. Do not increment revision for invalid color input or failed overlay show. Preserve shutdown `SaveSettings()` before `Dispose()`.

- [ ] **Step 6: Update startup initialization.**

  In `App.xaml.cs`, allocate/write an initial revision once after loading the profile, pass that revision to `InitializeAsync`, and remove duplicate unversioned Widget writes. Ensure standalone mode remains valid when `Package.Current` is unavailable.

- [ ] **Step 7: Run focused and full tests.**

  Run:
  ```powershell
  dotnet test CrosshairOverlay.Tests --no-restore --filter "FullyQualifiedName~SettingsServiceTests|FullyQualifiedName~MainViewModelTests|FullyQualifiedName~AppServiceServerTests"
  dotnet test CrosshairOverlay.Tests --no-restore
  ```
  Expected: all tests pass and visibility regression is covered.

---

## Task 3: Make Widget file fallback ordered, cancellable, and recoverable

**Files:**
- Modify: `CrosshairOverlay.Widget/Services/AppServiceClient.cs`
- Modify: `CrosshairOverlay.Widget/CrosshairPage.xaml.cs`
- Modify: `CrosshairOverlay.Widget/Models/CrosshairProfile.cs` only if required by the linked envelope model
- Add/link: `CrosshairOverlay/Models/CrosshairSyncEnvelope.cs`

**Interfaces:**
- `AppServiceClient.CurrentRevision` exposes the latest accepted revision.
- `CrosshairPage` accepts a file envelope or legacy profile; only a profile with a revision greater than the current accepted revision is applied, except the initial revision-0 legacy load when no profile has been accepted.

- [ ] **Step 1: Define the read/arbitration test cases before implementation.**

  The test matrix must cover: valid envelope wins over older file content, IPC wins over an in-flight older file read, malformed JSON can be retried after the file is repaired, legacy JSON is accepted once, and page unload cancels pending reads.

- [ ] **Step 2: Refactor `LoadSettingsFromFile` from `async void` to a serialized task.**

  Use a `CancellationTokenSource` owned by the page, a `SemaphoreSlim` or equivalent single-reader gate, and a generation/state check before applying results. Stop/cancel the token in `OnPageUnloaded`; recreate it in `OnPageLoaded`.

- [ ] **Step 3: Parse before updating `_lastJson`.**

  Read the text, parse envelope first, fall back to `CrosshairProfile` for legacy JSON, sanitize, compare revision, then assign `_lastJson` only after acceptance. Leave the marker unchanged on parse failure so the next timer tick retries repaired content.

- [ ] **Step 4: Reconcile connection state before applying file data.**

  Immediately before assigning `_profile`, re-check the current AppService revision/connection generation. A stale file result must be discarded if IPC has accepted a newer revision.

- [ ] **Step 5: Keep fallback polling conditional but safe.**

  Continue skipping the file poll while a current AppService snapshot is active, but restart fallback after service closure without allowing an older file snapshot to overwrite the last accepted profile.

- [ ] **Step 6: Build the Widget project where the required VS/UWP toolchain exists.**

  Run the repository’s VS 2019 MSBuild command in Release/x64. If unavailable, report the exact environmental limitation and rely on desktop compilation plus static C# 8 compatibility checks; do not claim Widget verification.

---

## Task 4: Repair AppService protocol and lifecycle for future packaged mode

**Files:**
- Modify: `CrosshairOverlay.Widget/Services/AppServiceClient.cs`
- Modify: `CrosshairOverlay/Services/AppServiceServer.cs`
- Modify: `CrosshairOverlay.Widget/App.xaml.cs`
- Modify: `CrosshairOverlay.Tests/AppServiceServerTests.cs`

**Interfaces:**
- Widget request responses contain `status="ok"` and accepted `revision`, or `status="error"` plus a stable error code/message.
- Desktop sends `command`, `profileJson`, and `revision`; it treats only `AppServiceResponseStatus.Success` with an OK payload as success.

- [ ] **Step 1: Add protocol helper tests or pure contract assertions.**

  Assert exact keys and values for update, success, malformed profile, and unknown command messages. Keep command constants in one shared protocol definition where the UWP compiler permits it.

- [ ] **Step 2: Implement response handling in `RequestReceived`.**

  For every request, acquire a deferral, validate command/profile/revision, apply only newer profiles, call `SendResponseAsync` before completing the deferral, and send an error response on exceptions. Complete the deferral on every early return, including duplicate connection initialization.

- [ ] **Step 3: Make connection ownership explicit.**

  On replacement, detach handlers and dispose the prior connection. Track a connection generation so callbacks from an old connection cannot clear current state. Ensure `ServiceClosed` resets state only for the owned connection.

- [ ] **Step 4: Synchronize state access.**

  Marshal profile/state notifications to the UI dispatcher or use a lock-backed snapshot. Do not expose a partially updated combination of `IsConnected`, `HasCurrentProfile`, and `CurrentRevision`.

- [ ] **Step 5: Run desktop tests and package compilation.**

  Run `dotnet test CrosshairOverlay.Tests --no-restore`; then run the Widget VS/MSBuild command if available. The standalone release must still work if AppService is unavailable.

---

## Task 5: Remove deployment-specific hardcoding and harden installation

**Files:**
- Modify: `CrosshairOverlay/Services/SettingsService.cs`
- Modify: `Release/setup.ps1`
- Modify: `Release/setup.bat`
- Create: `build/ReleaseVersion.json`
- Create: `build/Build-Release.ps1`
- Create: `build/Validate-Release.ps1`
- Create: `build/Validate-Manifests.ps1`
- Modify: `.gitignore`

**Interfaces:**
- `Release/setup.ps1` accepts the package directory as its root, reads `release-manifest.json`, and writes `%LOCALAPPDATA%\CrosshairOverlay\widget-package.json` containing the installed Widget `PackageFamilyName` and `LocalState` path.
- `SettingsService` reads and validates that metadata before choosing the Widget file path; an injected directory still takes precedence in tests.
- `Build-Release.ps1` accepts explicit `-VersionFile`, `-SigningCertificatePath`, `-CertificatePassword`/secure input, `-DependencyDirectory`, `-MsBuildPath`, and `-OutputDirectory` parameters. Private signing material is never copied into the ZIP.

- [ ] **Step 1: Add tests for package-location metadata and missing Widget behavior.**

  Use an injected app-data root to assert valid metadata is selected, invalid/missing metadata is logged and does not throw, and the desktop settings service remains usable without a Widget installation.

- [ ] **Step 2: Implement metadata-based package resolution.**

  Keep the current package-family value only as a backward-compatible migration fallback with a warning. Prefer installer metadata and validate that the LocalState directory belongs to the expected package family before writing.

- [ ] **Step 3: Add a single version source.**

  Store semantic release version and four-part MSIX version in `build/ReleaseVersion.json`. Validate that the four-part version is monotonic and inject it into the Widget manifest and artifact names during packaging. Do not hand-edit version strings in generated output.

- [ ] **Step 4: Implement clean staging in `Build-Release.ps1`.**

  Delete/recreate a generated staging directory, publish WPF self-contained win-x64, build Widget Release/x64 with the supplied MSBuild path, copy only the current Widget MSIX payload, copy current `setup.bat`/`setup.ps1`, collect approved dependency packages, and generate `release-manifest.json` with commit, version, file hashes, certificate thumbprint, and package identity.

- [ ] **Step 5: Implement post-build validation.**

  `Validate-Release.ps1` must fail on missing scripts, stale/extra payloads, PDBs in the public payload, hash mismatches, wrong version, wrong publisher, missing dependencies, or an embedded manifest without the Widget/Game Bar extension and optional AppService declaration. `Validate-Manifests.ps1` must compare semantic nodes between source and retained layout manifests or explicitly mark the combined layout non-release.

- [ ] **Step 6: Harden `setup.ps1`.**

  Validate the release manifest and file hashes before any trust/import operation; validate certificate thumbprint/subject/publisher/expiry; import only the expected certificate into current-user `TrustedPeople`; check/install exact dependency packages; install the MSIX without `-ForceUpdateFromAnyVersion`; verify the installed package identity/version/publisher; write package-location metadata; and start the desktop EXE only after all checks pass.

- [ ] **Step 7: Make the batch wrapper fail closed.**

  Preserve UTF-8 and PowerShell invocation, propagate the exact exit code, pause on failure, and never launch the desktop executable itself. The PowerShell script owns all installation and success decisions.

- [ ] **Step 8: Update ignore rules and validate the scripts statically.**

  Ignore only generated build staging/output and local signing material. Keep release scripts, version metadata, and validators tracked. Run PowerShell syntax checks where available and execute validators against a deliberately incomplete staging directory to confirm they fail closed.

---

## Task 6: Fix monitor/DPI and native-resource lifecycle issues

**Files:**
- Modify: `CrosshairOverlay/Rendering/OverlayHost.cs`
- Modify: `CrosshairOverlay/Helpers/NativeMethods.cs` if a monitor-DPI API declaration is needed
- Modify: `CrosshairOverlay.Widget/CrosshairPage.xaml.cs`
- Modify: `CrosshairOverlay.Widget/CrosshairPage.xaml` only if hit-testing is validated
- Add tests to `CrosshairOverlay.Tests` for pure coordinate/DPI helpers if extracted

**Interfaces:**
- Extract pure monitor-center/DPI calculations into internal helpers where possible so tests do not require a live overlay HWND.

- [ ] **Step 1: Add pure tests for target-monitor DPI and center calculations.**

  Cover 100%/150% DPI, negative virtual-screen origins, zero/uninitialized bounds, and a secondary monitor distinct from the primary monitor.

- [ ] **Step 2: Implement target-monitor DPI lookup.**

  Use the selected monitor handle or a per-monitor API rather than `GetDpiForWindow(GetDesktopWindow())`. Refresh the scale when the overlay is shown on a different monitor or the monitor metrics change.

- [ ] **Step 3: Synchronize ForceTopmost timer shutdown.**

  Guard HWND reads with a lifecycle lock or equivalent. Stop the timer, prevent new callbacks, and only then destroy the HWND; callbacks must re-check disposed/zero handle state before `SetWindowPos`.

- [ ] **Step 4: Refresh Widget display metrics on bounds/display changes.**

  Re-read `DisplayInformation` and raw pixel scale when bounds change, then recalculate using the actual target coordinate system and virtual-screen origin. Observe `CenterWindowAsync` failures and retry after bounds become valid.

- [ ] **Step 5: Verify Game Bar hit-testing behavior manually.**

  Do not assume `Background="Transparent"` or `AllowForegroundTransparency` means pointer pass-through. If `IsHitTestVisible="False"` is safe under Xbox Game Bar, add it and test; otherwise retain the documented mouse click-through action.

- [ ] **Step 6: Run desktop tests/build and Widget build where available.**

  Run `dotnet test CrosshairOverlay.Tests --no-restore` and `dotnet build CrosshairOverlay --no-restore`; record Widget toolchain availability separately.

---

## Task 7: Expand regression coverage and documentation

**Files:**
- Modify: `CrosshairOverlay.Tests/SettingsServiceTests.cs`
- Modify: `CrosshairOverlay.Tests/MainViewModelTests.cs`
- Modify: `CrosshairOverlay.Tests/AppServiceServerTests.cs`
- Modify: `CrosshairOverlay.Tests/CrosshairRendererTests.cs`
- Create: additional focused test files only when a test class would otherwise mix storage, sync, and rendering responsibilities.
- Modify: `README.md`
- Modify: `AGENTS.md`

- [ ] **Step 1: Add settings failure-path tests.**

  Test malformed/truncated JSON, invalid colors/ranges, atomic replacement leftovers, repeated revision allocation, and unavailable Widget metadata. Assert behavior and logs/return values rather than merely executing methods.

- [ ] **Step 2: Add synchronization regression tests.**

  Assert visibility push, revision monotonicity, stale profile rejection, legacy profile compatibility, and no publish after a failed overlay show.

- [ ] **Step 3: Add renderer behavior tests.**

  For all six styles, assert representative pixel geometry, base/outline colors, opacity, and DPI-scaled dimensions. Keep tests deterministic and avoid relying only on non-empty bitmap checks.

- [ ] **Step 4: Add release validation tests or script fixtures.**

  Build fixture directories with missing `setup.ps1`, wrong hash, wrong version, missing dependency, PDB, and manifest drift. Assert `Validate-Release.ps1` exits nonzero for each fixture and succeeds for a valid fixture.

- [ ] **Step 5: Update README and AGENTS.**

  Document Windows 10 build floor, x64/Game Bar/UWP prerequisites, the one-ZIP user flow, dependency behavior, standalone file synchronization, optional AppService status, click-through requirement, clean release commands, and the fact that the combined manifest is not the supported standalone release payload.

- [ ] **Step 6: Run the complete verification matrix.**

  ```powershell
  dotnet build CrosshairOverlay --no-restore
  dotnet test CrosshairOverlay.Tests --no-restore
  powershell -NoProfile -ExecutionPolicy Bypass -File build/Validate-Manifests.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File build/Validate-Release.ps1 -ReleaseDirectory <validated-output>
  ```

  Run the VS/MSBuild Widget Release/x64 build and clean Windows 10/11 x64 install/upgrade/uninstall smoke tests when those environments are available. Report unavailable environments explicitly.

---

## Execution Order and Checkpoints

1. Tasks 1–2 establish the revision contract and fix the documented visibility synchronization bug.
2. Task 3 fixes the standalone Widget fallback and must pass before packaging changes are trusted.
3. Task 4 repairs optional AppService behavior without making it a release dependency.
4. Task 5 creates the reproducible user-facing release path and is the release gate.
5. Task 6 handles platform-specific reliability after the data-flow contract is stable.
6. Task 7 closes test/documentation gaps and runs the final matrix.

At each task boundary, inspect the diff for unrelated changes. Do not publish or claim complete installation support until the release validator and a clean-machine smoke test both pass.
