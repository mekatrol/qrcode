using Mekatrol.QRCode.Common;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mekatrol.QRCode.Test;

[TestClass]
public sealed class QRCodeGeneratorTests
{
    private const int _stressQrCodeCount = 1_000_000;
    private const int _maximumTextLength = 100;
    private readonly QRCodeGenerator _generator = new();

    private static readonly int[][] _errorCorrectionCodewordsPerBlock =
    [
        [-1, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [-1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28],
        [-1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [-1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
    ];

    private static readonly int[][] _errorCorrectionBlocks =
    [
        [-1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25],
        [-1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49],
        [-1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68],
        [-1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81],
    ];

    [TestMethod]
    public void EncodeTextCreatesVersionOneQRCodeForShortText()
    {
        var qrCode = _generator.EncodeText("HELLO WORLD", QRErrorCorrectionLevel.Low);

        Assert.AreEqual(1, qrCode.Version);
        Assert.AreEqual(21, qrCode.Size);
        Assert.IsTrue(qrCode.Mask is >= 0 and <= 7);
    }

    [TestMethod]
    public void GetModuleReturnsFalseForOutOfBoundsCoordinates()
    {
        var qrCode = _generator.EncodeText("HELLO WORLD", QRErrorCorrectionLevel.Low);

        Assert.IsFalse(qrCode.GetModule(-1, 0));
        Assert.IsFalse(qrCode.GetModule(0, -1));
        Assert.IsFalse(qrCode.GetModule(qrCode.Size, 0));
        Assert.IsFalse(qrCode.GetModule(0, qrCode.Size));
    }

    [TestMethod]
    public void FinderPatternTopLeftIsDrawn()
    {
        var qrCode = _generator.EncodeText("HELLO WORLD", QRErrorCorrectionLevel.Low);

        Assert.IsTrue(qrCode.GetModule(0, 0));
        Assert.IsTrue(qrCode.GetModule(3, 3));
        Assert.IsTrue(qrCode.GetModule(6, 6));
        Assert.IsFalse(qrCode.GetModule(7, 7));
    }

    [TestMethod]
    public void EncodeSegmentsThrowsWhenDataCannotFitRequestedVersion()
    {
        var segment = QRSegment.MakeBytes(new byte[32]);

        Assert.ThrowsExactly<DataTooLongException>(() =>
            _generator.EncodeSegments([segment], QRErrorCorrectionLevel.High, 1, 1, -1, false));
    }

    [TestMethod]
    public void AddQRCodeGeneratorRegistersTransientService()
    {
        IServiceCollection services = new ServiceCollection();

        var result = services.AddQRCodeGenerator();

        Assert.AreSame(services, result);
        Assert.IsTrue(services.Any(service =>
            service.ServiceType == typeof(IQRCodeGenerator)
            && service.ImplementationType == typeof(QRCodeGenerator)
            && service.Lifetime == ServiceLifetime.Transient));
    }

    [TestMethod]
    [TestCategory("Stress")]
    public void EncodeTextGeneratesAndValidatesOneMillionRandomProgramPayloads()
    {
        if (!Debugger.IsAttached && Environment.GetEnvironmentVariable("RUN_QR_STRESS") != "1")
        {
            Assert.Inconclusive("Set RUN_QR_STRESS=1 or run under a debugger to generate and validate 1,000,000 QR codes.");
        }

        var random = new Random(0x5EED);

        for (var i = 0; i < _stressQrCodeCount; i++)
        {
            var input = CreateRandomProgramInput(random);
            var expectedPayload = BuildProgramPayload(input);
            var qrCode = _generator.EncodeText(expectedPayload, QRErrorCorrectionLevel.Medium);

            var actualPayload = DecodeByteModePayload(qrCode);
            var actual = JsonSerializer.Deserialize<ProgramPayload>(actualPayload);

            Assert.IsNotNull(actual);
            Assert.AreEqual(expectedPayload, actualPayload);
            Assert.AreEqual(input.Year, actual.Year);
            Assert.AreEqual(input.Month, actual.Month);
            Assert.AreEqual(input.UuidBase64, actual.UuidBase64);
            Assert.AreEqual(input.Uuid, actual.Uuid);
            Assert.AreEqual(HashText(input.Text), actual.TextSha256);
        }
    }

    private static ProgramInput CreateRandomProgramInput(Random random)
    {
        var year = random.Next(0, 10_000).ToString("D4");
        var month = random.Next(1, 13).ToString("D4");
        var uuidBytes = new byte[16];
        random.NextBytes(uuidBytes);
        var uuid = new Guid(uuidBytes);
        var uuidBase64 = Convert.ToBase64String(uuidBytes);
        var text = CreateRandomText(random);

        return new ProgramInput(year, month, uuid, uuidBase64, text);
    }

    private static string CreateRandomText(Random random)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,;:-_/@#%+=[]{}()";
        var length = random.Next(0, _maximumTextLength + 1);
        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            builder.Append(alphabet[random.Next(alphabet.Length)]);
        }

        return builder.ToString();
    }

    private static string BuildProgramPayload(ProgramInput input)
    {
        var payload = new ProgramPayload(input.Year, input.Month, input.UuidBase64, input.Uuid, HashText(input.Text));
        return JsonSerializer.Serialize(payload);
    }

    private static string HashText(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string DecodeByteModePayload(QRCodeSymbol qrCode)
    {
        var codewords = ExtractCodewords(qrCode);
        var dataCodewords = DeinterleaveDataCodewords(codewords, qrCode.Version, qrCode.ErrorCorrectionLevel);
        var reader = new BitReader(dataCodewords);

        Assert.AreEqual(0x4, reader.ReadBits(4));
        var byteCount = reader.ReadBits(GetByteModeCharacterCountBits(qrCode.Version));
        var bytes = new byte[byteCount];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)reader.ReadBits(8);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static byte[] ExtractCodewords(QRCodeSymbol qrCode)
    {
        var isFunction = CreateFunctionMask(qrCode.Version);
        var result = new byte[GetNumRawDataModules(qrCode.Version) / 8];
        var bitIndex = 0;

        for (var right = qrCode.Size - 1; right >= 1; right -= 2)
        {
            if (right == 6)
            {
                right = 5;
            }

            for (var vertical = 0; vertical < qrCode.Size; vertical++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? qrCode.Size - 1 - vertical : vertical;
                    if (!isFunction[y][x] && bitIndex < result.Length * 8)
                    {
                        var bit = qrCode.GetModule(x, y) ^ MaskApplies(qrCode.Mask, x, y);
                        result[bitIndex >>> 3] |= (byte)((bit ? 1 : 0) << (7 - (bitIndex & 7)));
                        bitIndex++;
                    }
                }
            }
        }

        return result;
    }

    private static byte[] DeinterleaveDataCodewords(byte[] codewords, int version, QRErrorCorrectionLevel errorCorrectionLevel)
    {
        var numBlocks = _errorCorrectionBlocks[(int)errorCorrectionLevel][version];
        var blockEccLength = _errorCorrectionCodewordsPerBlock[(int)errorCorrectionLevel][version];
        var rawCodewords = GetNumRawDataModules(version) / 8;
        var numShortBlocks = numBlocks - (rawCodewords % numBlocks);
        var shortBlockLength = rawCodewords / numBlocks;
        var blocks = new byte[numBlocks][];
        for (var i = 0; i < blocks.Length; i++)
        {
            blocks[i] = new byte[shortBlockLength + 1];
        }

        for (int i = 0, k = 0; i < blocks[0].Length; i++)
        {
            for (var j = 0; j < blocks.Length; j++)
            {
                if (i != shortBlockLength - blockEccLength || j >= numShortBlocks)
                {
                    blocks[j][i] = codewords[k];
                    k++;
                }
            }
        }

        var result = new byte[GetNumDataCodewords(version, errorCorrectionLevel)];
        var offset = 0;
        for (var i = 0; i < blocks.Length; i++)
        {
            var dataLength = shortBlockLength - blockEccLength + (i < numShortBlocks ? 0 : 1);
            Array.Copy(blocks[i], 0, result, offset, dataLength);
            offset += dataLength;
        }

        return result;
    }

    private static bool[][] CreateFunctionMask(int version)
    {
        var size = (version * 4) + 17;
        var result = CreateGrid(size);
        for (var i = 0; i < size; i++)
        {
            result[i][6] = true;
            result[6][i] = true;
        }

        DrawFinderFunctionPattern(result, 3, 3);
        DrawFinderFunctionPattern(result, size - 4, 3);
        DrawFinderFunctionPattern(result, 3, size - 4);

        var alignPatternPositions = GetAlignmentPatternPositions(version);
        for (var i = 0; i < alignPatternPositions.Length; i++)
        {
            for (var j = 0; j < alignPatternPositions.Length; j++)
            {
                if (!((i == 0 && j == 0)
                    || (i == 0 && j == alignPatternPositions.Length - 1)
                    || (i == alignPatternPositions.Length - 1 && j == 0)))
                {
                    DrawAlignmentFunctionPattern(result, alignPatternPositions[i], alignPatternPositions[j]);
                }
            }
        }

        DrawFormatFunctionModules(result);
        DrawVersionFunctionModules(result, version);
        return result;
    }

    private static bool[][] CreateGrid(int size)
    {
        var result = new bool[size][];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new bool[size];
        }

        return result;
    }

    private static void DrawFinderFunctionPattern(bool[][] isFunction, int x, int y)
    {
        for (var dy = -4; dy <= 4; dy++)
        {
            for (var dx = -4; dx <= 4; dx++)
            {
                var xx = x + dx;
                var yy = y + dy;
                if (0 <= xx && xx < isFunction.Length && 0 <= yy && yy < isFunction.Length)
                {
                    isFunction[yy][xx] = true;
                }
            }
        }
    }

    private static void DrawAlignmentFunctionPattern(bool[][] isFunction, int x, int y)
    {
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                isFunction[y + dy][x + dx] = true;
            }
        }
    }

    private static void DrawFormatFunctionModules(bool[][] isFunction)
    {
        var size = isFunction.Length;
        for (var i = 0; i <= 5; i++)
        {
            isFunction[i][8] = true;
        }

        isFunction[7][8] = true;
        isFunction[8][8] = true;
        isFunction[8][7] = true;
        for (var i = 9; i < 15; i++)
        {
            isFunction[8][14 - i] = true;
        }

        for (var i = 0; i < 8; i++)
        {
            isFunction[8][size - 1 - i] = true;
        }

        for (var i = 8; i < 15; i++)
        {
            isFunction[size - 15 + i][8] = true;
        }

        isFunction[size - 8][8] = true;
    }

    private static void DrawVersionFunctionModules(bool[][] isFunction, int version)
    {
        if (version < 7)
        {
            return;
        }

        var size = isFunction.Length;
        for (var i = 0; i < 18; i++)
        {
            var a = size - 11 + (i % 3);
            var b = i / 3;
            isFunction[b][a] = true;
            isFunction[a][b] = true;
        }
    }

    private static int[] GetAlignmentPatternPositions(int version)
    {
        if (version == 1)
        {
            return [];
        }

        var numAlign = (version / 7) + 2;
        var step = (((version * 8) + (numAlign * 3) + 5) / ((numAlign * 4) - 4)) * 2;
        var result = new int[numAlign];
        result[0] = 6;
        for (int i = result.Length - 1, position = (version * 4) + 10; i >= 1; i--, position -= step)
        {
            result[i] = position;
        }

        return result;
    }

    private static bool MaskApplies(int mask, int x, int y)
    {
        return mask switch
        {
            0 => (x + y) % 2 == 0,
            1 => y % 2 == 0,
            2 => x % 3 == 0,
            3 => (x + y) % 3 == 0,
            4 => ((x / 3) + (y / 2)) % 2 == 0,
            5 => ((x * y) % 2) + ((x * y) % 3) == 0,
            6 => (((x * y) % 2) + ((x * y) % 3)) % 2 == 0,
            7 => (((x + y) % 2) + ((x * y) % 3)) % 2 == 0,
            _ => throw new ArgumentOutOfRangeException(nameof(mask), "Mask value out of range"),
        };
    }

    private static int GetNumDataCodewords(int version, QRErrorCorrectionLevel errorCorrectionLevel)
    {
        return (GetNumRawDataModules(version) / 8)
            - (_errorCorrectionCodewordsPerBlock[(int)errorCorrectionLevel][version]
            * _errorCorrectionBlocks[(int)errorCorrectionLevel][version]);
    }

    private static int GetNumRawDataModules(int version)
    {
        var size = (version * 4) + 17;
        var result = size * size;
        result -= 8 * 8 * 3;
        result -= (15 * 2) + 1;
        result -= (size - 16) * 2;
        if (version >= 2)
        {
            var numAlign = (version / 7) + 2;
            result -= (numAlign - 1) * (numAlign - 1) * 25;
            result -= (numAlign - 2) * 2 * 20;
            if (version >= 7)
            {
                result -= 6 * 3 * 2;
            }
        }

        return result;
    }

    private static int GetByteModeCharacterCountBits(int version)
    {
        return version <= 9 ? 8 : 16;
    }

    private sealed record ProgramInput(string Year, string Month, Guid Uuid, string UuidBase64, string Text);

    private sealed record ProgramPayload(string Year, string Month, string UuidBase64, Guid Uuid, string TextSha256);

    private sealed class BitReader(byte[] data)
    {
        private int _index;

        public int ReadBits(int length)
        {
            var result = 0;
            for (var i = 0; i < length; i++)
            {
                result = (result << 1) | ((data[_index >>> 3] >>> (7 - (_index & 7))) & 1);
                _index++;
            }

            return result;
        }
    }
}
