namespace Mekatrol.QRCode.Common;

/// <summary>
/// A QR Code symbol represented as an immutable square grid of dark and light modules.
/// </summary>
public sealed class QRCodeGenerator
{
    /// <summary>The minimum version number supported in the QR Code Model 2 standard.</summary>
    public const int MinVersion = 1;

    /// <summary>The maximum version number supported in the QR Code Model 2 standard.</summary>
    public const int MaxVersion = 40;

    private const int _penaltyN1 = 3;
    private const int _penaltyN2 = 3;
    private const int _penaltyN3 = 40;
    private const int _penaltyN4 = 10;

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

    private readonly bool[][] _modules;
    private bool[][]? _isFunction;

    /// <summary>
    /// Initializes a new instance of the <see cref="QRCodeGenerator"/> class.
    /// </summary>
    /// <param name="version">The version number to use.</param>
    /// <param name="errorCorrectionLevel">The error correction level to use.</param>
    /// <param name="dataCodewords">The data codewords, excluding error correction codewords.</param>
    /// <param name="mask">The mask pattern, or -1 for automatic choice.</param>
    public QRCodeGenerator(int version, QRErrorCorrectionLevel errorCorrectionLevel, byte[] dataCodewords, int mask)
    {
        if (version < MinVersion || version > MaxVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version value out of range");
        }

        if (mask < -1 || mask > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(mask), "Mask value out of range");
        }

        ArgumentNullException.ThrowIfNull(dataCodewords);

        Version = version;
        Size = (version * 4) + 17;
        ErrorCorrectionLevel = errorCorrectionLevel;
        _modules = CreateGrid(Size);
        _isFunction = CreateGrid(Size);

        DrawFunctionPatterns();
        var allCodewords = AddEccAndInterleave(dataCodewords);
        DrawCodewords(allCodewords);

        if (mask == -1)
        {
            var minPenalty = int.MaxValue;
            for (var i = 0; i < 8; i++)
            {
                ApplyMask(i);
                DrawFormatBits(i);
                var penalty = GetPenaltyScore();
                if (penalty < minPenalty)
                {
                    mask = i;
                    minPenalty = penalty;
                }

                ApplyMask(i);
            }
        }

        Mask = mask;
        ApplyMask(mask);
        DrawFormatBits(mask);
        _isFunction = null;
    }

    /// <summary>
    /// Gets the version number of this QR Code.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// Gets the width and height of this QR Code, measured in modules.
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// Gets the error correction level used in this QR Code.
    /// </summary>
    public QRErrorCorrectionLevel ErrorCorrectionLevel { get; }

    /// <summary>
    /// Gets the mask pattern used in this QR Code.
    /// </summary>
    public int Mask { get; }

    /// <summary>
    /// Returns a QR Code representing the specified Unicode text.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <param name="errorCorrectionLevel">The error correction level to use.</param>
    /// <returns>A QR Code representing the text.</returns>
    public static QRCodeGenerator EncodeText(string text, QRErrorCorrectionLevel errorCorrectionLevel)
    {
        ArgumentNullException.ThrowIfNull(text);
        return EncodeSegments(QRSegment.MakeSegments(text), errorCorrectionLevel);
    }

    /// <summary>
    /// Returns a QR Code representing the specified binary data.
    /// </summary>
    /// <param name="data">The binary data to encode.</param>
    /// <param name="errorCorrectionLevel">The error correction level to use.</param>
    /// <returns>A QR Code representing the data.</returns>
    public static QRCodeGenerator EncodeBinary(byte[] data, QRErrorCorrectionLevel errorCorrectionLevel)
    {
        ArgumentNullException.ThrowIfNull(data);
        return EncodeSegments([QRSegment.MakeBytes(data)], errorCorrectionLevel);
    }

    /// <summary>
    /// Returns a QR Code representing the specified segments.
    /// </summary>
    /// <param name="segments">The segments to encode.</param>
    /// <param name="errorCorrectionLevel">The error correction level to use.</param>
    /// <returns>A QR Code representing the segments.</returns>
    public static QRCodeGenerator EncodeSegments(IReadOnlyCollection<QRSegment> segments, QRErrorCorrectionLevel errorCorrectionLevel)
    {
        return EncodeSegments(segments, errorCorrectionLevel, MinVersion, MaxVersion, -1, true);
    }

    /// <summary>
    /// Returns a QR Code representing the specified segments with the specified encoding parameters.
    /// </summary>
    /// <param name="segments">The segments to encode.</param>
    /// <param name="errorCorrectionLevel">The error correction level to use.</param>
    /// <param name="minVersion">The minimum allowed version.</param>
    /// <param name="maxVersion">The maximum allowed version.</param>
    /// <param name="mask">The mask number, or -1 for automatic choice.</param>
    /// <param name="boostErrorCorrectionLevel">Whether to increase the ECC level if it still fits.</param>
    /// <returns>A QR Code representing the segments.</returns>
    public static QRCodeGenerator EncodeSegments(
        IReadOnlyCollection<QRSegment> segments,
        QRErrorCorrectionLevel errorCorrectionLevel,
        int minVersion,
        int maxVersion,
        int mask,
        bool boostErrorCorrectionLevel)
    {
        ArgumentNullException.ThrowIfNull(segments);

        if (minVersion < MinVersion || minVersion > maxVersion || maxVersion > MaxVersion || mask < -1 || mask > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(minVersion), "Invalid value");
        }

        var version = minVersion;
        int dataUsedBits;
        while (true)
        {
            var dataCapacityBits = GetNumDataCodewords(version, errorCorrectionLevel) * 8;
            dataUsedBits = QRSegment.GetTotalBits(segments, version);
            if (dataUsedBits != -1 && dataUsedBits <= dataCapacityBits)
            {
                break;
            }

            if (version >= maxVersion)
            {
                var message = dataUsedBits == -1
                    ? "Segment too long"
                    : $"Data length = {dataUsedBits} bits, Max capacity = {dataCapacityBits} bits";
                throw new DataTooLongException(message);
            }

            version++;
        }

        foreach (var newErrorCorrectionLevel in Enum.GetValues<QRErrorCorrectionLevel>())
        {
            if (boostErrorCorrectionLevel
                && dataUsedBits <= GetNumDataCodewords(version, newErrorCorrectionLevel) * 8)
            {
                errorCorrectionLevel = newErrorCorrectionLevel;
            }
        }

        var buffer = new BitBuffer();
        foreach (var segment in segments)
        {
            buffer.AppendBits(segment.Mode.GetModeBits(), 4);
            buffer.AppendBits(segment.CharacterCount, segment.Mode.GetCharacterCountBits(version));
            buffer.AppendData(segment.GetRawData());
        }

        var dataCapacity = GetNumDataCodewords(version, errorCorrectionLevel) * 8;
        buffer.AppendBits(0, Math.Min(4, dataCapacity - buffer.BitLength));
        buffer.AppendBits(0, (8 - (buffer.BitLength % 8)) % 8);

        for (var padByte = 0xEC; buffer.BitLength < dataCapacity; padByte ^= 0xEC ^ 0x11)
        {
            buffer.AppendBits(padByte, 8);
        }

        var dataCodewords = new byte[buffer.BitLength / 8];
        for (var i = 0; i < buffer.BitLength; i++)
        {
            dataCodewords[i >>> 3] |= (byte)(buffer.GetBit(i) << (7 - (i & 7)));
        }

        return new QRCodeGenerator(version, errorCorrectionLevel, dataCodewords, mask);
    }

    /// <summary>
    /// Returns the color of the module at the specified coordinates.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns><see langword="true"/> if the module is dark; otherwise <see langword="false"/>.</returns>
    public bool GetModule(int x, int y)
    {
        return 0 <= x && x < Size && 0 <= y && y < Size && _modules[y][x];
    }

    internal static bool GetBit(int value, int index)
    {
        return ((value >>> index) & 1) != 0;
    }

    internal static int GetNumDataCodewords(int version, QRErrorCorrectionLevel errorCorrectionLevel)
    {
        return (GetNumRawDataModules(version) / 8)
            - (_errorCorrectionCodewordsPerBlock[(int)errorCorrectionLevel][version]
            * _errorCorrectionBlocks[(int)errorCorrectionLevel][version]);
    }

    private static bool[][] CreateGrid(int size)
    {
        var result = new bool[size][];
        for (var i = 0; i < size; i++)
        {
            result[i] = new bool[size];
        }

        return result;
    }

    private static int GetNumRawDataModules(int version)
    {
        if (version < MinVersion || version > MaxVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version number out of range");
        }

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

    private static byte[] ReedSolomonComputeDivisor(int degree)
    {
        if (degree < 1 || degree > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(degree), "Degree out of range");
        }

        var result = new byte[degree];
        result[degree - 1] = 1;

        var root = 1;
        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < result.Length; j++)
            {
                result[j] = (byte)ReedSolomonMultiply(result[j], root);
                if (j + 1 < result.Length)
                {
                    result[j] ^= result[j + 1];
                }
            }

            root = ReedSolomonMultiply(root, 0x02);
        }

        return result;
    }

    private static byte[] ReedSolomonComputeRemainder(byte[] data, byte[] divisor)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(divisor);

        var result = new byte[divisor.Length];
        foreach (var value in data)
        {
            var factor = value ^ result[0];
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;
            for (var i = 0; i < result.Length; i++)
            {
                result[i] ^= (byte)ReedSolomonMultiply(divisor[i], factor);
            }
        }

        return result;
    }

    private static int ReedSolomonMultiply(int x, int y)
    {
        var z = 0;
        for (var i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >>> 7) * 0x11D);
            z ^= ((y >>> i) & 1) * x;
        }

        return z;
    }

    private byte[] AddEccAndInterleave(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length != GetNumDataCodewords(Version, ErrorCorrectionLevel))
        {
            throw new ArgumentException("Invalid data length", nameof(data));
        }

        var numBlocks = _errorCorrectionBlocks[(int)ErrorCorrectionLevel][Version];
        var blockEccLength = _errorCorrectionCodewordsPerBlock[(int)ErrorCorrectionLevel][Version];
        var rawCodewords = GetNumRawDataModules(Version) / 8;
        var numShortBlocks = numBlocks - (rawCodewords % numBlocks);
        var shortBlockLength = rawCodewords / numBlocks;

        var blocks = new byte[numBlocks][];
        var rsDivisor = ReedSolomonComputeDivisor(blockEccLength);
        var offset = 0;
        for (var i = 0; i < numBlocks; i++)
        {
            var dataLength = shortBlockLength - blockEccLength + (i < numShortBlocks ? 0 : 1);
            var blockData = data[offset..(offset + dataLength)];
            offset += blockData.Length;
            var block = new byte[shortBlockLength + 1];
            Array.Copy(blockData, block, blockData.Length);
            var ecc = ReedSolomonComputeRemainder(blockData, rsDivisor);
            Array.Copy(ecc, 0, block, block.Length - blockEccLength, ecc.Length);
            blocks[i] = block;
        }

        var result = new byte[rawCodewords];
        for (int i = 0, k = 0; i < blocks[0].Length; i++)
        {
            for (var j = 0; j < blocks.Length; j++)
            {
                if (i != shortBlockLength - blockEccLength || j >= numShortBlocks)
                {
                    result[k] = blocks[j][i];
                    k++;
                }
            }
        }

        return result;
    }

    private void DrawFunctionPatterns()
    {
        for (var i = 0; i < Size; i++)
        {
            SetFunctionModule(6, i, i % 2 == 0);
            SetFunctionModule(i, 6, i % 2 == 0);
        }

        DrawFinderPattern(3, 3);
        DrawFinderPattern(Size - 4, 3);
        DrawFinderPattern(3, Size - 4);

        var alignPatternPositions = GetAlignmentPatternPositions();
        for (var i = 0; i < alignPatternPositions.Length; i++)
        {
            for (var j = 0; j < alignPatternPositions.Length; j++)
            {
                if (!((i == 0 && j == 0)
                    || (i == 0 && j == alignPatternPositions.Length - 1)
                    || (i == alignPatternPositions.Length - 1 && j == 0)))
                {
                    DrawAlignmentPattern(alignPatternPositions[i], alignPatternPositions[j]);
                }
            }
        }

        DrawFormatBits(0);
        DrawVersion();
    }

    private void DrawFormatBits(int mask)
    {
        var data = (ErrorCorrectionLevel.GetFormatBits() << 3) | mask;
        var remainder = data;
        for (var i = 0; i < 10; i++)
        {
            remainder = (remainder << 1) ^ ((remainder >>> 9) * 0x537);
        }

        var bits = ((data << 10) | remainder) ^ 0x5412;

        for (var i = 0; i <= 5; i++)
        {
            SetFunctionModule(8, i, GetBit(bits, i));
        }

        SetFunctionModule(8, 7, GetBit(bits, 6));
        SetFunctionModule(8, 8, GetBit(bits, 7));
        SetFunctionModule(7, 8, GetBit(bits, 8));
        for (var i = 9; i < 15; i++)
        {
            SetFunctionModule(14 - i, 8, GetBit(bits, i));
        }

        for (var i = 0; i < 8; i++)
        {
            SetFunctionModule(Size - 1 - i, 8, GetBit(bits, i));
        }

        for (var i = 8; i < 15; i++)
        {
            SetFunctionModule(8, Size - 15 + i, GetBit(bits, i));
        }

        SetFunctionModule(8, Size - 8, true);
    }

    private void DrawVersion()
    {
        if (Version < 7)
        {
            return;
        }

        var remainder = Version;
        for (var i = 0; i < 12; i++)
        {
            remainder = (remainder << 1) ^ ((remainder >>> 11) * 0x1F25);
        }

        var bits = (Version << 12) | remainder;
        for (var i = 0; i < 18; i++)
        {
            var bit = GetBit(bits, i);
            var a = Size - 11 + (i % 3);
            var b = i / 3;
            SetFunctionModule(a, b, bit);
            SetFunctionModule(b, a, bit);
        }
    }

    private void DrawFinderPattern(int x, int y)
    {
        for (var dy = -4; dy <= 4; dy++)
        {
            for (var dx = -4; dx <= 4; dx++)
            {
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                var xx = x + dx;
                var yy = y + dy;
                if (0 <= xx && xx < Size && 0 <= yy && yy < Size)
                {
                    SetFunctionModule(xx, yy, distance != 2 && distance != 4);
                }
            }
        }
    }

    private void DrawAlignmentPattern(int x, int y)
    {
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                SetFunctionModule(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            }
        }
    }

    private void SetFunctionModule(int x, int y, bool isDark)
    {
        _modules[y][x] = isDark;
        _isFunction![y][x] = true;
    }

    private void DrawCodewords(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length != GetNumRawDataModules(Version) / 8)
        {
            throw new ArgumentException("Invalid data length", nameof(data));
        }

        var i = 0;
        for (var right = Size - 1; right >= 1; right -= 2)
        {
            if (right == 6)
            {
                right = 5;
            }

            for (var vertical = 0; vertical < Size; vertical++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? Size - 1 - vertical : vertical;
                    if (!_isFunction![y][x] && i < data.Length * 8)
                    {
                        _modules[y][x] = GetBit(data[i >>> 3], 7 - (i & 7));
                        i++;
                    }
                }
            }
        }
    }

    private void ApplyMask(int mask)
    {
        if (mask < 0 || mask > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(mask), "Mask value out of range");
        }

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var invert = mask switch
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
                _modules[y][x] ^= invert && !_isFunction![y][x];
            }
        }
    }

    private int GetPenaltyScore()
    {
        var result = 0;

        for (var y = 0; y < Size; y++)
        {
            var runColor = false;
            var runX = 0;
            var runHistory = new int[7];
            for (var x = 0; x < Size; x++)
            {
                if (_modules[y][x] == runColor)
                {
                    runX++;
                    result += runX == 5 ? _penaltyN1 : runX > 5 ? 1 : 0;
                }
                else
                {
                    FinderPenaltyAddHistory(runX, runHistory);
                    if (!runColor)
                    {
                        result += FinderPenaltyCountPatterns(runHistory) * _penaltyN3;
                    }

                    runColor = _modules[y][x];
                    runX = 1;
                }
            }

            result += FinderPenaltyTerminateAndCount(runColor, runX, runHistory) * _penaltyN3;
        }

        for (var x = 0; x < Size; x++)
        {
            var runColor = false;
            var runY = 0;
            var runHistory = new int[7];
            for (var y = 0; y < Size; y++)
            {
                if (_modules[y][x] == runColor)
                {
                    runY++;
                    result += runY == 5 ? _penaltyN1 : runY > 5 ? 1 : 0;
                }
                else
                {
                    FinderPenaltyAddHistory(runY, runHistory);
                    if (!runColor)
                    {
                        result += FinderPenaltyCountPatterns(runHistory) * _penaltyN3;
                    }

                    runColor = _modules[y][x];
                    runY = 1;
                }
            }

            result += FinderPenaltyTerminateAndCount(runColor, runY, runHistory) * _penaltyN3;
        }

        for (var y = 0; y < Size - 1; y++)
        {
            for (var x = 0; x < Size - 1; x++)
            {
                var color = _modules[y][x];
                if (color == _modules[y][x + 1]
                    && color == _modules[y + 1][x]
                    && color == _modules[y + 1][x + 1])
                {
                    result += _penaltyN2;
                }
            }
        }

        var dark = 0;
        foreach (var row in _modules)
        {
            foreach (var color in row)
            {
                if (color)
                {
                    dark++;
                }
            }
        }

        var total = Size * Size;
        var k = ((Math.Abs((dark * 20) - (total * 10)) + total - 1) / total) - 1;
        result += k * _penaltyN4;
        return result;
    }

    private int[] GetAlignmentPatternPositions()
    {
        if (Version == 1)
        {
            return [];
        }

        var numAlign = (Version / 7) + 2;
        var step = (((Version * 8) + (numAlign * 3) + 5) / ((numAlign * 4) - 4)) * 2;
        var result = new int[numAlign];
        result[0] = 6;
        for (int i = result.Length - 1, position = Size - 7; i >= 1; i--, position -= step)
        {
            result[i] = position;
        }

        return result;
    }

    private static int FinderPenaltyCountPatterns(int[] runHistory)
    {
        var n = runHistory[1];
        var core = n > 0
            && runHistory[2] == n
            && runHistory[3] == n * 3
            && runHistory[4] == n
            && runHistory[5] == n;
        return (core && runHistory[0] >= n * 4 && runHistory[6] >= n ? 1 : 0)
            + (core && runHistory[6] >= n * 4 && runHistory[0] >= n ? 1 : 0);
    }

    private int FinderPenaltyTerminateAndCount(bool currentRunColor, int currentRunLength, int[] runHistory)
    {
        if (currentRunColor)
        {
            FinderPenaltyAddHistory(currentRunLength, runHistory);
            currentRunLength = 0;
        }

        currentRunLength += Size;
        FinderPenaltyAddHistory(currentRunLength, runHistory);
        return FinderPenaltyCountPatterns(runHistory);
    }

    private void FinderPenaltyAddHistory(int currentRunLength, int[] runHistory)
    {
        if (runHistory[0] == 0)
        {
            currentRunLength += Size;
        }

        Array.Copy(runHistory, 0, runHistory, 1, runHistory.Length - 1);
        runHistory[0] = currentRunLength;
    }
}

/// <summary>
/// The error correction level in a QR Code symbol.
/// </summary>
public enum QRErrorCorrectionLevel
{
    /// <summary>The QR Code can tolerate about 7% erroneous codewords.</summary>
    Low,

    /// <summary>The QR Code can tolerate about 15% erroneous codewords.</summary>
    Medium,

    /// <summary>The QR Code can tolerate about 25% erroneous codewords.</summary>
    Quartile,

    /// <summary>The QR Code can tolerate about 30% erroneous codewords.</summary>
    High,
}

internal static class QRErrorCorrectionLevelExtensions
{
    public static int GetFormatBits(this QRErrorCorrectionLevel level)
    {
        return level switch
        {
            QRErrorCorrectionLevel.Low => 1,
            QRErrorCorrectionLevel.Medium => 0,
            QRErrorCorrectionLevel.Quartile => 3,
            QRErrorCorrectionLevel.High => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(level), "Invalid error correction level"),
        };
    }
}
