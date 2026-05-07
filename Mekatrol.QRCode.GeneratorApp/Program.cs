using Mekatrol.QRCode.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mekatrol.QRCode.GeneratorApp;

internal static class Program
{
    private const int _quietZoneModules = 4;
    private const int _maximumTextLength = 100;
    private const string _year = "2026";
    private const string _month = "0005";
    private const string _text = "sample text";

    private static void Main()
    {
        var input = CreateInput();
        var payload = BuildPayload(input);
        var qrCode = QRCodeGenerator.EncodeText(payload, QRErrorCorrectionLevel.Medium);

        Console.WriteLine(payload);
        Console.WriteLine();
        WriteQrCode(qrCode);
    }

    private static QRCodeInput CreateInput()
    {
        if (_year.Length != 4 || !_year.All(char.IsDigit))
        {
            throw new InvalidOperationException("Year must be exactly four digits.");
        }

        if (_month.Length != 4 || !_month.All(char.IsDigit) || !int.TryParse(_month, out var monthNumber) || monthNumber is < 1 or > 12)
        {
            throw new InvalidOperationException("Month must be exactly four digits in the range 0001 through 0012.");
        }

        var uuid = Guid.NewGuid();
        var uuidBase64 = Convert.ToBase64String(uuid.ToByteArray());
        if (!TryParseUuidBase64(uuidBase64, out var parsedUuid))
        {
            throw new InvalidOperationException("UUID must be base64 for exactly 16 bytes.");
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
            if (bytes.Length != 16)
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

    private static void WriteQrCode(QRCodeGenerator qrCode)
    {
        for (var y = -_quietZoneModules; y < qrCode.Size + _quietZoneModules; y++)
        {
            for (var x = -_quietZoneModules; x < qrCode.Size + _quietZoneModules; x++)
            {
                Console.Write(qrCode.GetModule(x, y) ? "██" : "  ");
            }

            Console.WriteLine();
        }
    }

    private sealed record QRCodeInput(string Year, string Month, Guid Uuid, string UuidBase64, string Text);

    private sealed record QRCodePayload(string Year, string Month, string UuidBase64, Guid Uuid, string TextSha256);
}
