# Mekatrol QR Code

C# solution for QR code generation utilities.

## Projects

- `Mekatrol.QRCode.Common` - shared library code.
- `Mekatrol.QRCode.GeneratorApp` - console application entry point.
- `Mekatrol.QRCode.Test` - MSTest test project.

All projects currently target `.NET 10.0` with nullable reference types and implicit usings enabled.

## Prerequisites

- .NET SDK 10.0 or later.

Check the installed SDK:

```bash
dotnet --version
```

## Build

```bash
dotnet build Mekatrol.QRCode.slnx
```

## Run

```bash
dotnet run --project Mekatrol.QRCode.GeneratorApp/Mekatrol.QRCode.GeneratorApp.csproj
```

## Test

```bash
dotnet test Mekatrol.QRCode.slnx
```

## Repository Notes

- Keep generated `bin/` and `obj/` directories out of source control.
- Add shared QR code behavior to `Mekatrol.QRCode.Common` and keep the console app focused on command-line concerns.
