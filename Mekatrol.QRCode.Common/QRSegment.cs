using System.Text;

namespace Mekatrol.QRCode.Common;

/// <summary>
/// A segment of character, binary, or control data in a QR Code symbol.
/// </summary>
public sealed class QRSegment
{
    // The QR alphanumeric mode character set is fixed by ISO/IEC 18004 and maps characters to base-45 values.
    private const string _alphanumericCharset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

    // Byte mode stores each input byte as exactly 8 bits in the segment data stream.
    private const int _byteModeBitsPerByte = 8;

    // Numeric mode encodes up to 3 decimal digits at a time per the QR segment packing rules.
    private const int _numericModeDigitsPerGroup = 3;

    // Numeric mode uses 3 bits per digit plus one final control bit for a full or partial group.
    private const int _numericModeBitsPerDigit = 3;

    // Numeric mode's grouped bit length includes one extra bit in the QR specification formula.
    private const int _numericModeBitLengthOffset = 1;

    // Alphanumeric mode packs two characters into one base-45 value.
    private const int _alphanumericModeCharactersPerGroup = 2;

    // The QR alphanumeric radix is 45 because the specification allows exactly 45 characters.
    private const int _alphanumericModeRadix = 45;

    // Two alphanumeric characters require 11 bits in QR alphanumeric mode.
    private const int _alphanumericModePairBitLength = 11;

    // A trailing single alphanumeric character requires 6 bits in QR alphanumeric mode.
    private const int _alphanumericModeSingleBitLength = 6;

    // ECI assignment values below 2^7 use the shortest QR ECI encoding form.
    private const int _singleByteEciAssignmentLimit = 1 << 7;

    // ECI assignment values below 2^14 use the two-byte QR ECI encoding form.
    private const int _twoByteEciAssignmentLimit = 1 << 14;

    // ECI assignment values are defined by the QR specification only up to six decimal digits.
    private const int _maximumEciAssignmentValueExclusive = 1_000_000;

    // The two-byte ECI prefix marks the following 14 bits as the assignment value.
    private const int _twoByteEciPrefix = 0b10;

    // The two-byte ECI prefix itself is two bits wide.
    private const int _twoByteEciPrefixBitLength = 2;

    // The two-byte ECI encoding carries 14 assignment-value bits.
    private const int _twoByteEciAssignmentBitLength = 14;

    // The three-byte ECI prefix marks the following 21 bits as the assignment value.
    private const int _threeByteEciPrefix = 0b110;

    // The three-byte ECI prefix itself is three bits wide.
    private const int _threeByteEciPrefixBitLength = 3;

    // The three-byte ECI encoding carries 21 assignment-value bits.
    private const int _threeByteEciAssignmentBitLength = 21;

    // Segment headers use a 4-bit mode indicator before the character count and payload data.
    internal const int ModeIndicatorBitLength = 4;

    // The return value used when a segment length cannot be represented for the requested QR version.
    internal const int InvalidTotalBitLength = -1;

    // The exception text identifies invalid segment character counts.
    private const string _invalidValueMessage = "Invalid value";

    // The exception text identifies text that cannot be represented in numeric QR mode.
    private const string _nonNumericTextMessage = "String contains non-numeric characters";

    // The exception text identifies text that cannot be represented in alphanumeric QR mode.
    private const string _nonAlphanumericTextMessage = "String contains unencodable characters in alphanumeric mode";

    // The exception text identifies ECI assignment values outside the QR specification range.
    private const string _eciAssignmentOutOfRangeMessage = "ECI assignment value out of range";

    // Unicode char values below this limit are covered by the ASCII alphanumeric lookup table.
    private const int _asciiCharacterCount = 128;

    // Characters not present in the QR alphanumeric table are marked with -1.
    private const int _invalidAlphanumericValue = -1;

    // ASCII digit characters start at '0' and are the only characters accepted by QR numeric mode.
    private const char _minimumNumericCharacter = '0';

    // ASCII digit characters end at '9' and are the only characters accepted by QR numeric mode.
    private const char _maximumNumericCharacter = '9';

    // QR numeric parsing uses decimal place-value accumulation.
    private const int _decimalRadix = 10;

    private readonly BitBuffer _data;

    // This table maps ASCII characters to QR alphanumeric mode values for O(1) encoding and validation.
    private static readonly sbyte[] _alphanumericValues = CreateAlphanumericValues();

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
            throw new ArgumentOutOfRangeException(nameof(characterCount), _invalidValueMessage);
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

        var buffer = BitBuffer.FromBytes(bytes);

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
            throw new ArgumentException(_nonNumericTextMessage, nameof(digits));
        }

        var buffer = new BitBuffer();
        for (var i = 0; i < digits.Length;)
        {
            var length = Math.Min(digits.Length - i, _numericModeDigitsPerGroup);
            buffer.AppendBits(
                ParseNumericValue(digits.AsSpan(i, length)),
                (length * _numericModeBitsPerDigit) + _numericModeBitLengthOffset);
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
            throw new ArgumentException(_nonAlphanumericTextMessage, nameof(text));
        }

        var buffer = new BitBuffer();
        var i = 0;
        for (; i <= text.Length - _alphanumericModeCharactersPerGroup; i += _alphanumericModeCharactersPerGroup)
        {
            var value = (GetAlphanumericValue(text[i]) * _alphanumericModeRadix)
                + GetAlphanumericValue(text[i + 1]);
            buffer.AppendBits(value, _alphanumericModePairBitLength);
        }

        if (i < text.Length)
        {
            buffer.AppendBits(GetAlphanumericValue(text[i]), _alphanumericModeSingleBitLength);
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
            throw new ArgumentOutOfRangeException(nameof(assignmentValue), _eciAssignmentOutOfRangeMessage);
        }
        else if (assignmentValue < _singleByteEciAssignmentLimit)
        {
            buffer.AppendBits(assignmentValue, _byteModeBitsPerByte);
        }
        else if (assignmentValue < _twoByteEciAssignmentLimit)
        {
            buffer.AppendBits(_twoByteEciPrefix, _twoByteEciPrefixBitLength);
            buffer.AppendBits(assignmentValue, _twoByteEciAssignmentBitLength);
        }
        else if (assignmentValue < _maximumEciAssignmentValueExclusive)
        {
            buffer.AppendBits(_threeByteEciPrefix, _threeByteEciPrefixBitLength);
            buffer.AppendBits(assignmentValue, _threeByteEciAssignmentBitLength);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(assignmentValue), _eciAssignmentOutOfRangeMessage);
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
        foreach (var character in text.AsSpan())
        {
            if (character is < _minimumNumericCharacter or > _maximumNumericCharacter)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Tests whether the specified string can be encoded as alphanumeric mode.
    /// </summary>
    /// <param name="text">The text to test.</param>
    /// <returns><see langword="true"/> if the string is alphanumeric.</returns>
    public static bool IsAlphanumeric(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var character in text.AsSpan())
        {
            if (GetAlphanumericValue(character) < 0)
            {
                return false;
            }
        }

        return true;
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
                return InvalidTotalBitLength;
            }

            result += ModeIndicatorBitLength + characterCountBits + segment._data.BitLength;
            if (result > int.MaxValue)
            {
                return InvalidTotalBitLength;
            }
        }

        return (int)result;
    }

    internal BitBuffer GetRawData()
    {
        return _data;
    }

    private static int ParseNumericValue(ReadOnlySpan<char> digits)
    {
        var result = 0;
        foreach (var digit in digits)
        {
            result = (result * _decimalRadix) + (digit - _minimumNumericCharacter);
        }

        return result;
    }

    private static int GetAlphanumericValue(char character)
    {
        return character < _asciiCharacterCount ? _alphanumericValues[character] : _invalidAlphanumericValue;
    }

    private static sbyte[] CreateAlphanumericValues()
    {
        var result = new sbyte[_asciiCharacterCount];
        Array.Fill(result, (sbyte)_invalidAlphanumericValue);
        for (var i = 0; i < _alphanumericCharset.Length; i++)
        {
            result[_alphanumericCharset[i]] = (sbyte)i;
        }

        return result;
    }
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
    // Numeric mode is identified by the 0001 mode indicator in the QR bitstream.
    private const int _numericModeIndicator = 0x1;

    // Alphanumeric mode is identified by the 0010 mode indicator in the QR bitstream.
    private const int _alphanumericModeIndicator = 0x2;

    // Byte mode is identified by the 0100 mode indicator in the QR bitstream.
    private const int _byteModeIndicator = 0x4;

    // Kanji mode is identified by the 1000 mode indicator in the QR bitstream.
    private const int _kanjiModeIndicator = 0x8;

    // ECI mode is identified by the 0111 mode indicator in the QR bitstream.
    private const int _eciModeIndicator = 0x7;

    // Version groups are separated by QR versions 1-9, 10-26, and 27-40 for character-count widths.
    private const int _characterCountVersionGroupOffset = 7;

    // The divisor maps a version plus offset into the three QR character-count width groups.
    private const int _characterCountVersionGroupDivisor = 17;

    // ECI mode carries no character count field because its payload is only the assignment designator.
    private const int _eciCharacterCountBitLength = 0;

    // The exception text identifies segment modes outside this implementation's enum values.
    private const string _invalidSegmentModeMessage = "Invalid segment mode";

    // The exception text identifies QR versions outside the model 2 supported range.
    private const string _versionOutOfRangeMessage = "Version number out of range";

    // Numeric character-count field widths are fixed by QR version group and must stay grouped as spec data.
    private static readonly int[] _numericCharacterCountBitsByVersionGroup = [10, 12, 14];

    // Alphanumeric character-count field widths are fixed by QR version group and must stay grouped as spec data.
    private static readonly int[] _alphanumericCharacterCountBitsByVersionGroup = [9, 11, 13];

    // Byte character-count field widths are fixed by QR version group and must stay grouped as spec data.
    private static readonly int[] _byteCharacterCountBitsByVersionGroup = [8, 16, 16];

    // Kanji character-count field widths are fixed by QR version group and must stay grouped as spec data.
    private static readonly int[] _kanjiCharacterCountBitsByVersionGroup = [8, 10, 12];

    public static int GetModeBits(this QRSegmentMode mode)
    {
        return mode switch
        {
            QRSegmentMode.Numeric => _numericModeIndicator,
            QRSegmentMode.Alphanumeric => _alphanumericModeIndicator,
            QRSegmentMode.Byte => _byteModeIndicator,
            QRSegmentMode.Kanji => _kanjiModeIndicator,
            QRSegmentMode.Eci => _eciModeIndicator,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), _invalidSegmentModeMessage),
        };
    }

    public static int GetCharacterCountBits(this QRSegmentMode mode, int version)
    {
        if (version < QRCodeGenerator.MinVersion || version > QRCodeGenerator.MaxVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(version), _versionOutOfRangeMessage);
        }

        var offset = (version + _characterCountVersionGroupOffset) / _characterCountVersionGroupDivisor;
        return mode switch
        {
            QRSegmentMode.Numeric => _numericCharacterCountBitsByVersionGroup[offset],
            QRSegmentMode.Alphanumeric => _alphanumericCharacterCountBitsByVersionGroup[offset],
            QRSegmentMode.Byte => _byteCharacterCountBitsByVersionGroup[offset],
            QRSegmentMode.Kanji => _kanjiCharacterCountBitsByVersionGroup[offset],
            QRSegmentMode.Eci => _eciCharacterCountBitLength,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), _invalidSegmentModeMessage),
        };
    }
}
