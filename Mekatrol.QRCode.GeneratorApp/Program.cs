using Mekatrol.QRCode.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mekatrol.QRCode.GeneratorApp;

internal static class Program
{
    private static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddQRCodeGenerator();
        builder.Services.AddTransient<GeneratorApplication>();

        using var host = builder.Build();
        host.Services.GetRequiredService<GeneratorApplication>().Run();
    }
}

internal sealed class GeneratorApplication(IQRCodeGenerator qrCodeGenerator)
{
    // The quiet zone width is four modules because QR readers require this blank border around the symbol.
    private const int _quietZoneModules = 4;

    // The sample payload limits text to 100 characters to keep the generated QR Code compact and scannable.
    private const int _maximumTextLength = 100;

    // The sample payload uses a four-digit year field to match the receiving system's fixed-width schema.
    private const string _year = "2026";

    // The sample payload uses a four-digit month field to match the receiving system's fixed-width schema.
    private const string _month = "0005";

    // The sample text is deterministic input whose hash is included in the generated payload.
    private const string _text = "sample text";

    // The year field must contain exactly four digits in the payload schema.
    private const int _yearLength = 4;

    // The month field must contain exactly four digits in the payload schema.
    private const int _monthLength = 4;

    // The minimum encoded month is January, represented as 0001 by the fixed-width schema.
    private const int _minimumMonth = 1;

    // The maximum encoded month is December, represented as 0012 by the fixed-width schema.
    private const int _maximumMonth = 12;

    // A GUID is exactly 16 bytes and the payload validates that size after base64 decoding.
    private const int _guidByteLength = 16;

    // A dark QR module is printed as two block characters so console output keeps a square aspect ratio.
    private const string _darkModuleText = "██";

    // A light QR module is printed as two spaces to match the dark module's console width.
    private const string _lightModuleText = "  ";

    // The exception text identifies a sample year that does not match the payload schema.
    private const string _invalidYearMessage = "Year must be exactly four digits.";

    // The exception text identifies a sample month that does not match the payload schema.
    private const string _invalidMonthMessage = "Month must be exactly four digits in the range 0001 through 0012.";

    // The exception text identifies a UUID that does not decode to the 16 bytes required by Guid.
    private const string _invalidUuidMessage = "UUID must be base64 for exactly 16 bytes.";

    public void Run()
    {
        var input = CreateInput();
        var payload = BuildPayload(input);
        var qrCode = qrCodeGenerator.EncodeText(payload, QRErrorCorrectionLevel.Medium);

        Console.WriteLine(payload);
        Console.WriteLine();
        WriteQrCode(qrCode);
    }

    private static QRCodeInput CreateInput()
    {
        if (_year.Length != _yearLength || !_year.All(char.IsDigit))
        {
            throw new InvalidOperationException(_invalidYearMessage);
        }

        if (_month.Length != _monthLength
            || !_month.All(char.IsDigit)
            || !int.TryParse(_month, out var monthNumber)
            || monthNumber is < _minimumMonth or > _maximumMonth)
        {
            throw new InvalidOperationException(_invalidMonthMessage);
        }

        var uuid = Guid.NewGuid();
        var uuidBase64 = Convert.ToBase64String(uuid.ToByteArray());
        if (!TryParseUuidBase64(uuidBase64, out var parsedUuid))
        {
            throw new InvalidOperationException(_invalidUuidMessage);
        }

        if (_text.Length > _maximumTextLength)
        {
            throw new InvalidOperationException($"Text must be {_maximumTextLength} characters or fewer.");
        }

        return new QRCodeInput(_year, _month, parsedUuid, uuidBase64, _text);
    }

    private static bool TryParseUuidBase64(string value, out Guid uuid)
    {
        uuid = Guid.Empty;

        try
        {
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length != _guidByteLength)
            {
                return false;
            }

            uuid = new Guid(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string BuildPayload(QRCodeInput input)
    {
        var payload = new QRCodePayload(
            input.Year,
            input.Month,
            input.UuidBase64,
            input.Uuid,
            HashText(input.Text));

        return JsonSerializer.Serialize(payload);
    }

    private static string HashText(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void WriteQrCode(QRCodeSymbol qrCode)
    {
        for (var y = -_quietZoneModules; y < qrCode.Size + _quietZoneModules; y++)
        {
            for (var x = -_quietZoneModules; x < qrCode.Size + _quietZoneModules; x++)
            {
                Console.Write(qrCode.GetModule(x, y) ? _darkModuleText : _lightModuleText);
            }

            Console.WriteLine();
        }
    }

    private sealed record QRCodeInput(string Year, string Month, Guid Uuid, string UuidBase64, string Text);

    private sealed record QRCodePayload(string Year, string Month, string UuidBase64, Guid Uuid, string TextSha256);
}
