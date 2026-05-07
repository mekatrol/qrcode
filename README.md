# Mekatrol QR Code

C# solution for QR code generation utilities.

## Projects

- `Mekatrol.QRCode.Common` - shared library code.
- `Mekatrol.QRCode.GeneratorApp` - console application entry point.
- `Mekatrol.QRCode.Test` - MSTest test project.

All projects currently target `.NET 10.0` with nullable reference types and implicit usings enabled.

## Implementation

`Mekatrol.QRCode.Common` contains a C# QR Code generator implementation based on Project Nayuki's QR Code generator library. The implementation follows the same core model used by Nayuki's Java source: QR code generation from text, binary data, or explicit segments; all standard Model 2 versions from 1 through 40; the four standard error correction levels; automatic mask selection; and raw module access for rendering by callers.

The public API separates generation from the generated symbol:

- `IQRCodeGenerator` / `QRCodeGenerator` generate QR code symbols.
- `QRCodeSymbol` is the immutable generated model with version, size, mask, error correction level, and module access.

Register the generator with dependency injection using transient scope:

```csharp
services.AddQRCodeGenerator();
```

Then inject `IQRCodeGenerator` and encode data:

```csharp
public sealed class Example(IQRCodeGenerator generator)
{
    public QRCodeSymbol Create(string text)
    {
        return generator.EncodeText(text, QRErrorCorrectionLevel.Medium);
    }
}
```

References:

- Nayuki QR Code generator library, Java fast notes: <https://www.nayuki.io/page/qr-code-generator-library#java-fast>
- Nayuki Java source tree: <https://github.com/nayuki/QR-Code-generator/tree/master/java/src/main/java/io/nayuki/qrcodegen>

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

## Linting and Formatting

```bash
dotnet build Mekatrol.QRCode.slnx /p:EnforceCodeStyleInBuild=true
```

## Check Packages

```bash
dotnet package list --project Mekatrol.QRCode.slnx --vulnerable --format json
dotnet package list --project Mekatrol.QRCode.slnx --deprecated --format json
```

## Repository Notes

- Keep generated `bin/` and `obj/` directories out of source control.
- Add shared QR code behavior to `Mekatrol.QRCode.Common` and keep the console app focused on command-line concerns.
