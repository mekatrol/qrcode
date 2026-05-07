using System.Text;
using System.Text.RegularExpressions;

namespace Mekatrol.QRCode.Common;

/// <summary>
/// A segment of character, binary, or control data in a QR Code symbol.
/// </summary>
public sealed partial class QRSegment
{
    private const string _alphanumericCharset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

    private readonly BitBuffer _data;

    /// <summary>
    /// Initializes a new instance of the <see cref="QRSegment"/> class.
    /// </summary>
    /// <param name="mode">The segment mode.</param>
    /// <param name="characterCount">The data length in characters or bytes.</param>
    /// <param name="data">The data bits.</param>
    public QRSegment(QRSegmentMode mode, int characterCount, BitBuffer data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (characterCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(characterCount), "Invalid value");
        }

        Mode = mode;
        CharacterCount = characterCount;
        _data = data.Copy();
    }

    /// <summary>
    /// Gets the mode indicator of this segment.
    /// </summary>
    public QRSegmentMode Mode { get; }

    /// <summary>
    /// Gets the length of this segment's unencoded data.
    /// </summary>
    public int CharacterCount { get; }

    /// <summary>
    /// Returns a segment representing the specified binary data encoded in byte mode.
    /// </summary>
    /// <param name="bytes">The binary data.</param>
    /// <returns>A segment containing the data.</returns>
    public static QRSegment MakeBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var buffer = new BitBuffer();
        foreach (var value in bytes)
        {
            buffer.AppendBits(value, 8);
        }

        return new QRSegment(QRSegmentMode.Byte, bytes.Length, buffer);
    }

    /// <summary>
    /// Returns a segment representing decimal digits encoded in numeric mode.
    /// </summary>
    /// <param name="digits">The numeric text.</param>
    /// <returns>A segment containing the text.</returns>
    public static QRSegment MakeNumeric(string digits)
    {
        ArgumentNullException.ThrowIfNull(digits);

        if (!IsNumeric(digits))
        {
            throw new ArgumentException("String contains non-numeric characters", nameof(digits));
        }

        var buffer = new BitBuffer();
        for (var i = 0; i < digits.Length;)
        {
            var length = Math.Min(digits.Length - i, 3);
            buffer.AppendBits(int.Parse(digits.AsSpan(i, length)), (length * 3) + 1);
            i += length;
        }

        return new QRSegment(QRSegmentMode.Numeric, digits.Length, buffer);
    }

    /// <summary>
    /// Returns a segment representing text encoded in alphanumeric mode.
    /// </summary>
    /// <param name="text">The alphanumeric text.</param>
    /// <returns>A segment containing the text.</returns>
    public static QRSegment MakeAlphanumeric(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!IsAlphanumeric(text))
        {
            throw new ArgumentException("String contains unencodable characters in alphanumeric mode", nameof(text));
        }

        var buffer = new BitBuffer();
        var i = 0;
        for (; i <= text.Length - 2; i += 2)
        {
            var value = (_alphanumericCharset.IndexOf(text[i], StringComparison.Ordinal) * 45)
                + _alphanumericCharset.IndexOf(text[i + 1], StringComparison.Ordinal);
            buffer.AppendBits(value, 11);
        }

        if (i < text.Length)
        {
            buffer.AppendBits(_alphanumericCharset.IndexOf(text[i], StringComparison.Ordinal), 6);
        }

        return new QRSegment(QRSegmentMode.Alphanumeric, text.Length, buffer);
    }

    /// <summary>
    /// Returns zero or more segments representing the specified Unicode text.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <returns>A new mutable list of segments.</returns>
    public static List<QRSegment> MakeSegments(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var result = new List<QRSegment>();
        if (text.Length == 0)
        {
            return result;
        }

        if (IsNumeric(text))
        {
            result.Add(MakeNumeric(text));
        }
        else if (IsAlphanumeric(text))
        {
            result.Add(MakeAlphanumeric(text));
        }
        else
        {
            result.Add(MakeBytes(Encoding.UTF8.GetBytes(text)));
        }

        return result;
    }

    /// <summary>
    /// Returns a segment representing an Extended Channel Interpretation designator.
    /// </summary>
    /// <param name="assignmentValue">The ECI assignment number.</param>
    /// <returns>A segment containing the ECI designator.</returns>
    public static QRSegment MakeEci(int assignmentValue)
    {
        var buffer = new BitBuffer();
        if (assignmentValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(assignmentValue), "ECI assignment value out of range");
        }
        else if (assignmentValue < (1 << 7))
        {
            buffer.AppendBits(assignmentValue, 8);
        }
        else if (assignmentValue < (1 << 14))
        {
            buffer.AppendBits(0b10, 2);
            buffer.AppendBits(assignmentValue, 14);
        }
        else if (assignmentValue < 1_000_000)
        {
            buffer.AppendBits(0b110, 3);
            buffer.AppendBits(assignmentValue, 21);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(assignmentValue), "ECI assignment value out of range");
        }

        return new QRSegment(QRSegmentMode.Eci, 0, buffer);
    }

    /// <summary>
    /// Tests whether the specified string can be encoded as numeric mode.
    /// </summary>
    /// <param name="text">The text to test.</param>
    /// <returns><see langword="true"/> if the string is numeric.</returns>
    public static bool IsNumeric(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return NumericRegex().IsMatch(text);
    }

    /// <summary>
    /// Tests whether the specified string can be encoded as alphanumeric mode.
    /// </summary>
    /// <param name="text">The text to test.</param>
    /// <returns><see langword="true"/> if the string is alphanumeric.</returns>
    public static bool IsAlphanumeric(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return AlphanumericRegex().IsMatch(text);
    }

    internal BitBuffer GetData()
    {
        return _data.Copy();
    }

    internal static int GetTotalBits(IReadOnlyCollection<QRSegment> segments, int version)
    {
        ArgumentNullException.ThrowIfNull(segments);

        long result = 0;
        foreach (var segment in segments)
        {
            ArgumentNullException.ThrowIfNull(segment);

            var characterCountBits = segment.Mode.GetCharacterCountBits(version);
            if (segment.CharacterCount >= (1 << characterCountBits))
            {
                return -1;
            }

            result += 4L + characterCountBits + segment._data.BitLength;
            if (result > int.MaxValue)
            {
                return -1;
            }
        }

        return (int)result;
    }

    internal BitBuffer GetRawData()
    {
        return _data;
    }

    [GeneratedRegex("^[0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericRegex();

    [GeneratedRegex("^[A-Z0-9 $%*+./:-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex AlphanumericRegex();
}

/// <summary>
/// Describes how a segment's data bits are interpreted.
/// </summary>
public enum QRSegmentMode
{
    /// <summary>Numeric mode.</summary>
    Numeric,

    /// <summary>Alphanumeric mode.</summary>
    Alphanumeric,

    /// <summary>Byte mode.</summary>
    Byte,

    /// <summary>Kanji mode.</summary>
    Kanji,

    /// <summary>Extended Channel Interpretation mode.</summary>
    Eci,
}

internal static class QRSegmentModeExtensions
{
    public static int GetModeBits(this QRSegmentMode mode)
    {
        return mode switch
        {
            QRSegmentMode.Numeric => 0x1,
            QRSegmentMode.Alphanumeric => 0x2,
            QRSegmentMode.Byte => 0x4,
            QRSegmentMode.Kanji => 0x8,
            QRSegmentMode.Eci => 0x7,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), "Invalid segment mode"),
        };
    }

    public static int GetCharacterCountBits(this QRSegmentMode mode, int version)
    {
        if (version < QRCodeGenerator.MinVersion || version > QRCodeGenerator.MaxVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Version number out of range");
        }

        var offset = (version + 7) / 17;
        return mode switch
        {
            QRSegmentMode.Numeric => new[] { 10, 12, 14 }[offset],
            QRSegmentMode.Alphanumeric => new[] { 9, 11, 13 }[offset],
            QRSegmentMode.Byte => new[] { 8, 16, 16 }[offset],
            QRSegmentMode.Kanji => new[] { 8, 10, 12 }[offset],
            QRSegmentMode.Eci => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), "Invalid segment mode"),
        };
    }
}
