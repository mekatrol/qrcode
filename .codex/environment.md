# Codex Environment

## Workspace

- Repository root: `/home/dad/repos/qrcode`
- Shell: `bash`
- Time zone: `Australia/Sydney`

## Project Stack

- Language: C#
- SDK: .NET 10.0 or later
- Solution: `Mekatrol.QRCode.slnx`
- Test framework: MSTest

## Common Commands

```bash
dotnet restore Mekatrol.QRCode.slnx
dotnet build Mekatrol.QRCode.slnx
dotnet test Mekatrol.QRCode.slnx
dotnet run --project Mekatrol.QRCode.GeneratorApp/Mekatrol.QRCode.GeneratorApp.csproj
```

## Repository Notes

- Keep generated `bin/` and `obj/` directories out of source control.
- Prefer project-relative paths in documentation and scripts.
- The console app directory currently present in the workspace is `Mekatrol.QRCode.GeneratorApp`.
