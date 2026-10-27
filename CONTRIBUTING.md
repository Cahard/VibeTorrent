# Contributing to VibeTorrent

Thanks for taking the time to improve a local-first torrent client.

## Before you start

- Use **Windows 10/11 x64** and the **.NET 10 SDK**.
- Discuss non-trivial changes in an issue first.
- Keep the UI dark, Fluent-inspired, and consistent with `Themes/Tokens.xaml`.
- Never add telemetry, analytics, ads, or cloud dependencies.

## Build and test

```powershell
# from the repository root
./build.ps1
```

Or with the SDK already on `PATH`:

```powershell
dotnet restore VibeTorrent.slnx
dotnet build VibeTorrent.slnx -c Release
dotnet test tests\VibeTorrent.Tests\VibeTorrent.Tests.csproj -c Release
```

## Pull requests

- Keep changes focused; one concern per PR.
- Add or update xUnit tests for Core behaviour.
- Do not commit `bin/`, `obj/`, `artifacts/`, `.dotnet/`, `.tools/`, `.deploy/`,
  torrents, downloaded payloads, secrets, or publish output.
- Describe the user-visible change and the tests you ran.
- UI changes need a screenshot or short recording.
- Engine changes must preserve session compatibility and `PathSafety` guarantees.

## Code style

- C#: nullable enabled, file-scoped namespaces, `ConfigureAwait(false)` in Core.
- MVVM: view models do not reference controls; services are injected.
- All long-running work is cancellable and never blocks the UI thread.
- New Russian UI strings must be readable and consistent with the existing tone.
- New file names from torrent metadata must pass `PathSafety.ValidateRelativePath`.

## Security

For vulnerability reports, follow [SECURITY.md](SECURITY.md) instead of opening
a public issue.
