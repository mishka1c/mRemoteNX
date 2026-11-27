# mRemoteNX — build and development notes

mRemoteNX is a password-protected fork of mRemoteNG (forked from the mRemoteNG Community Edition 1.84.0).

## Conventions

- **Talk to the maintainer in Ukrainian.** Chat replies, questions and task lists are in Ukrainian.
- **Code comments and commit messages are in English**, like the rest of the code base.
- **User documentation is in Ukrainian:** `README.md`, `CHANGELOG.md`, `SECURITY.md`, `CONTRIBUTING.md`,
  `docs/USER-GUIDE.md`, the issue template and the release notes in the workflows.
- **Developer documentation is in English:** this file, `docs/ARCHITECTURE.md`, `docs/UI-MAP.md`,
  `docs/UI-LAYOUT.md`, `docs/RUNTIME_DIAGNOSTICS.md`, `docs/SSH-TRANSFER.md`.

## Repositories

| Remote | URL | Role |
|---|---|---|
| `origin` | `https://gitea.mykino.uk/mishka1c/mRemoteNX.git` | main repository |
| `github` | `https://github.com/mishka1c/mRemoteNX.git` | mirror; CI builds and releases |
| `upstream` | `https://github.com/robertpopa22/mRemoteNG.git` | the fork mRemoteNX came from |

Push to both `origin` and `github`. Work on `main`.

## What mRemoteNX changes

- **Application password** — `mRemoteNG/Security/AppPasswordPolicy.cs`. The password is the
  connections master password (root node `PasswordString`); the whole connections file is always
  written fully encrypted (AES-256-GCM, PBKDF2). Settings in `Properties/OptionsSecurityPage`:
  `RequireAppPassword`, `AppPasswordMaxAttempts`, `AutoLockIdleMinutes`; UI in
  `UI/Forms/OptionsPages/SecurityPage.AppPassword.cs`. Startup enforcement:
  `App/Initialization/CredsAndConsSetup.cs`; idle lock and unlock: `UI/Forms/frmMain.cs`.
- **Separate application** — product name `mRemoteNX Connection Manager` (decides the AppData folders
  and log name), company `mRemoteNX`, mutex `mRemoteNX_SingleInstanceMutex`, policy key
  `HKLM\SOFTWARE\mRemoteNX`, own MSI `UpgradeCode`. The executable is still `mRemoteNG.exe`.
- **Updates and links** — `App/Info/GeneralAppInfo.cs` and `App/Info/UpdateReleaseInfo.cs` point to
  `mishka1c/mRemoteNX`.
- **Branding** — `branding/` holds the SVG sources; icons and installer images are rendered from them.

Security rule: never weaken key derivation, encryption, certificate or host-key validation, or
credential handling to make something work. When a change touches security, say in the commit body
which property was examined and why it still holds.

## Build

**Do NOT use `dotnet build`** — it fails with `MSB4803` on the COM reference (`MSTSCLib`, the RDP
ActiveX control). Use full Visual Studio / Build Tools MSBuild:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File build.ps1            # restore + build
pwsh -NoProfile -ExecutionPolicy Bypass -File build.ps1 -NoRestore # incremental
pwsh -NoProfile -ExecutionPolicy Bypass -File build.ps1 -SelfContained
```

`build.ps1` auto-detects Visual Studio (2026 before 2022). Without Visual Studio, build on GitHub:
**Actions → PR_Validation / Nightly Build → Run workflow** (`gh workflow run PR_Validation --repo mishka1c/mRemoteNX --ref main`).

## Tests

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File run-tests.ps1 -Headless
pwsh -NoProfile -ExecutionPolicy Bypass -File run-tests.ps1 -Headless -NoBuild
```

Single group:

```bash
dotnet test "mRemoteNGTests/bin/x64/Release/mRemoteNGTests.dll" --results-directory /tmp/mrt --verbosity normal --filter "FullyQualifiedName~mRemoteNGTests.Tools"
```

- `--verbosity normal` only — minimal/quiet crashes the test host on .NET 10.
- `--results-directory` outside the repository.
- Run the DLL, not the `.csproj`.
- No interactive tests: no dialogs, message boxes or user input; mock UI dependencies.
- No `[assembly: Parallelizable]` — shared singletons race.
- For ObjectListView/FrmOptions tests use `Application.Run(form)` + `Application.ExitThread()` in `finally`.
- Every test failure is fixed before finishing (fix the code, fix the test, or remove an obsolete
  test); never `[Ignore]`.
- `test-config.json` defines the test groups used by `run-tests.ps1` / `run-tests-core.sh`.

## CI and releases

- Builds run **on demand only**: `PR_Validation` (x86/x64/ARM64 build) and `Nightly Build`
  (x64 build + tests + ZIP/MSI, published as the `nightly` prerelease) use `workflow_dispatch`.
- A release is cut by pushing a `vX.Y.Z` tag: `Build_mR-NB.yml` builds all variants and publishes
  the release. Its notes come from the first `## [` section of `CHANGELOG.md`, so rename
  `## [Unreleased]` to `## [X.Y.Z] - YYYY-MM-DD` before tagging, and bump `<Version>` in
  `mRemoteNG/mRemoteNG.csproj`.
- CodeQL runs weekly; secret scanning runs on push.
- Builds are not code-signed; the SignPath steps stay inert without their secrets.

## Menu paths

Look up menu, dialog and option names in [docs/UI-MAP.md](docs/UI-MAP.md) (or the designer source)
before writing them anywhere. Options... is under **File**, not Tools. When a new option is added,
add its row to the UI map in the same commit.

## Code quality

- Analyzers: .NET analyzers, Roslynator and Meziantou run in the local build (warnings, not errors;
  configured in `Directory.Build.props` and the two `.editorconfig` files — `mRemoteNG/.editorconfig`
  is `root=true`).
- `EnforceCodeStyleInBuild=true`, `AnalysisLevel=latest-recommended`.
- Noisy rules suppressed in `.editorconfig`: MA0004, MA0011, MA0076; MA0049 is suppressed for legacy code.
