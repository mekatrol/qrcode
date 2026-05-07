using System.Globalization;
using System.Text;

namespace Mekatrol.QRCode.Common;

/// <summary>
/// Generates QR Code symbols from text, binary data, or explicit segments.
/// </summary>
public sealed class QRCodeGenerator : IQRCodeGenerator
{
    /// <summary>The minimum version number supported in the QR Code Model 2 standard.</summary>
    public const int MinVersion = 1;

    /// <summary>The maximum version number supported in the QR Code Model 2 standard.</summary>
    public const int MaxVersion = 40;

    // The value that asks the encoder to choose the mask pattern with the lowest penalty score.
    private const int _automaticMask = -1;

    // QR Code mask pattern numbers start at 0.
    private const int _minimumMask = 0;

    // QR Code Model 2 defines exactly eight mask patterns, numbered 0 through 7.
    private const int _maximumMask = 7;

    // Mask pattern 0 inverts modules where (x + y) is divisible by 2.
    private const int _maskPattern0 = 0;

    // Mask pattern 1 inverts modules where y is divisible by 2.
    private const int _maskPattern1 = 1;

    // Mask pattern 2 inverts modules where x is divisible by 3.
    private const int _maskPattern2 = 2;

    // Mask pattern 3 inverts modules where (x + y) is divisible by 3.
    private const int _maskPattern3 = 3;

    // Mask pattern 4 inverts modules using the QR mixed row/column block formula.
    private const int _maskPattern4 = 4;

    // Mask pattern 5 inverts modules using the QR product modulo sum formula.
    private const int _maskPattern5 = 5;

    // Mask pattern 6 inverts modules using mask pattern 5 reduced modulo 2.
    private const int _maskPattern6 = 6;

    // Mask pattern 7 inverts modules using the QR coordinate-sum/product mixed formula.
    private const int _maskPattern7 = 7;

    // One byte contains 8 bits and QR codewords are byte-sized.
    private const int _bitsPerByte = 8;

    // Segment headers use a 4-bit mode indicator before the character count and payload data.
    private const int _modeIndicatorBitLength = QRSegment.ModeIndicatorBitLength;

    // Four terminator bits are added when capacity remains after all QR data segments.
    private const int _terminatorBitLength = 4;

    // The first alternating pad codeword required by ISO/IEC 18004 data padding.
    private const int _padCodewordFirst = 0xEC;

    // The second alternating pad codeword required by ISO/IEC 18004 data padding.
    private const int _padCodewordSecond = 0x11;

    // The right shift from a bit index to its containing byte index.
    private const int _bitIndexToByteShift = 3;

    // The mask for a bit index within one byte.
    private const int _bitIndexInByteMask = 7;

    // The highest bit position in a byte, used when packing bits in most-significant-bit order.
    private const int _highestBitIndexInByte = 7;

    // The finder pattern occupies an 8 by 8 function region in the raw-module count calculation.
    private const int _finderPatternRawRegionModulesPerSide = 8;

    // There are three finder pattern regions in a QR Code symbol.
    private const int _finderPatternCount = 3;

    // Format information has 15 bits.
    private const int _formatBitLength = 15;

    // Format information is written in two locations.
    private const int _formatInformationCopyCount = 2;

    // The fixed dark module contributes one module in the raw-module count calculation.
    private const int _darkModuleCount = 1;

    // Timing patterns reserve two lines after subtracting the overlapping finder pattern regions.
    private const int _timingPatternLineCount = 2;

    // Timing patterns begin after the finder pattern border, leaving 16 modules excluded at both ends.
    private const int _timingPatternExcludedEndModules = 16;

    // Alignment pattern center count grows by one every seven QR versions.
    private const int _alignmentPatternVersionDivisor = 7;

    // Version 2 starts with two alignment pattern centers per axis.
    private const int _alignmentPatternMinimumCentersPerAxis = 2;

    // One alignment pattern's raw function region is 5 by 5 modules.
    private const int _alignmentPatternModulesPerSide = 5;

    // Two alignment positions overlap finder timing zones in the raw-module count formula.
    private const int _alignmentPatternTimingOverlapCount = 2;

    // One alignment/timing overlap contributes 20 modules in the raw-module count formula.
    private const int _alignmentPatternTimingOverlapModules = 20;

    // Version information exists only for QR versions 7 and above.
    private const int _minimumVersionWithVersionInformation = 7;

    // Version information has 6 rows or columns in each placement area.
    private const int _versionInformationShortSide = 6;

    // Version information has 3 columns or rows in each placement area.
    private const int _versionInformationLongSide = 3;

    // Version information is written in two placement areas.
    private const int _versionInformationCopyCount = 2;

    // Reed-Solomon divisor degrees are byte-polynomial degrees and must fit in a byte-sized field.
    private const int _reedSolomonMaximumDegree = 255;

    // The first Reed-Solomon divisor coefficient is initialized to the multiplicative identity.
    private const int _reedSolomonIdentity = 1;

    // Reed-Solomon generator roots advance by multiplying by 2 in GF(2^8).
    private const int _reedSolomonGeneratorRootFactor = 0x02;

    // Reed-Solomon multiplication iterates across the 8 bits of a byte.
    private const int _reedSolomonBitCount = 8;

    // The reducing polynomial x^8 + x^4 + x^3 + x^2 + 1 used by QR Reed-Solomon arithmetic.
    private const int _reedSolomonReducingPolynomial = 0x11D;

    // The x coordinate of both QR timing patterns is 6 where they cross the finder separators.
    private const int _timingPatternCoordinate = 6;

    // Finder pattern centers are 3 modules in from the symbol edge.
    private const int _finderPatternCenterOffset = 3;

    // Finder pattern centers at the far edge are 4 modules back from the symbol size.
    private const int _finderPatternFarEdgeOffset = 4;

    // The format BCH remainder has 10 bits.
    private const int _formatRemainderBitLength = 10;

    // The top bit index of the format BCH remainder during division.
    private const int _formatRemainderTopBit = 9;

    // The QR format BCH generator polynomial.
    private const int _formatGeneratorPolynomial = 0x537;

    // The QR format bits are XOR-masked by this fixed pattern to avoid problematic all-zero fields.
    private const int _formatMaskPattern = 0x5412;

    // The vertical format-bit line is interrupted after bit 5 by the timing pattern.
    private const int _formatFirstVerticalRunLastBit = 5;

    // Format bit 6 is placed after the skipped timing coordinate on the vertical line.
    private const int _formatSkippedTimingBit = 6;

    // Format bit 7 is written at the central format coordinate.
    private const int _formatCenterBit = 7;

    // Format bit 8 is written next to the central format coordinate.
    private const int _formatPostCenterBit = 8;

    // Format bits 9 through 14 are mirrored along the top-left horizontal format line.
    private const int _formatSecondRunFirstBit = 9;

    // The last format bit index is 14 because format information has 15 bits.
    private const int _formatLastBit = 14;

    // Format bit mirroring around the top-left finder uses coordinate 14 - bit index.
    private const int _formatMirrorCoordinateBase = 14;

    // The second vertical format copy starts at bit 8.
    private const int _formatSecondVerticalRunFirstBit = 8;

    // The dark module is placed 8 modules above the lower-left corner.
    private const int _darkModuleBottomOffset = 8;

    // The version BCH remainder has 12 bits.
    private const int _versionRemainderBitLength = 12;

    // The top bit index of the version BCH remainder during division.
    private const int _versionRemainderTopBit = 11;

    // The QR version BCH generator polynomial.
    private const int _versionGeneratorPolynomial = 0x1F25;

    // Version information has 18 total bits after appending the 12-bit BCH remainder.
    private const int _versionInformationBitLength = 18;

    // Version information is placed 11 modules back from the far edge.
    private const int _versionInformationFarEdgeOffset = 11;

    // Version information is arranged in three columns.
    private const int _versionInformationColumnCount = 3;

    // Finder pattern drawing covers a radius of four modules around the 3-by-3 center.
    private const int _finderPatternRadius = 4;

    // Finder pattern ring distance 2 is light and separates the dark center from the outer ring.
    private const int _finderPatternLightRingInnerDistance = 2;

    // Finder pattern ring distance 4 is the light separator around the outer dark ring.
    private const int _finderPatternLightRingOuterDistance = 4;

    // Alignment pattern drawing covers a radius of two modules around its center.
    private const int _alignmentPatternRadius = 2;

    // Alignment pattern ring distance 1 is light and separates the dark center from the outer ring.
    private const int _alignmentPatternLightRingDistance = 1;

    // Data codewords are drawn two columns at a time in the QR zig-zag placement pattern.
    private const int _dataPlacementColumnPairWidth = 2;

    // The data-placement loop skips the vertical timing pattern column.
    private const int _dataPlacementTimingColumn = 6;

    // After skipping the timing column, placement resumes immediately to its left.
    private const int _dataPlacementColumnBeforeTiming = 5;

    // The mask-placement direction test uses bit 1 of the right-column coordinate.
    private const int _dataPlacementDirectionMask = 2;

    // The penalty-rule run history tracks the seven latest run lengths needed to detect finder-like patterns.
    private const int _finderPenaltyRunHistoryLength = 7;

    // Penalty rule N1 starts charging extra points when a same-color run reaches 5 modules.
    private const int _penaltyLongRunThreshold = 5;

    // Penalty rule N4 compares dark modules against 50% using tenths of 5%.
    private const int _penaltyDarkModuleMultiplier = 20;

    // Penalty rule N4 uses 50% as the ideal dark-module percentage.
    private const int _penaltyIdealDarkModuleMultiplier = 10;

    // One is subtracted after rounding N4's distance-to-ideal percentage as required by the QR penalty formula.
    private const int _penaltyDarkModuleAdjustment = 1;

    // Version 1 has no alignment patterns.
    private const int _firstVersionWithoutAlignmentPatterns = 1;

    // Alignment-pattern spacing uses 8 modules per version in the QR position formula.
    private const int _alignmentPositionVersionMultiplier = 8;

    // Alignment-pattern spacing adds three modules per alignment center in the QR position formula.
    private const int _alignmentPositionCenterMultiplier = 3;

    // Alignment-pattern spacing adds this rounding bias before dividing by the available gaps.
    private const int _alignmentPositionRoundingBias = 5;

    // Alignment-pattern spacing has four gaps per center except for the shared edge gap.
    private const int _alignmentPositionGapMultiplier = 4;

    // Alignment-pattern positions are forced to even module coordinates.
    private const int _alignmentPositionEvenStepMultiplier = 2;

    // The first alignment-pattern center is always at coordinate 6.
    private const int _alignmentPatternFirstPosition = 6;

    // The last alignment-pattern center is seven modules back from the far edge.
    private const int _alignmentPatternFarEdgeOffset = 7;

    // Finder-like penalty patterns have a 1:1:3:1:1 dark/light run core.
    private const int _finderPenaltyCoreRunMultiplier = 3;

    // Finder-like penalty patterns require a quiet light run four times the base width on either side.
    private const int _finderPenaltyQuietRunMultiplier = 4;

    // The QR alphanumeric mode character set is fixed by ISO/IEC 18004 and maps base-45 values back to characters.
    private const string _alphanumericCharset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

    // Numeric mode decodes up to 3 decimal digits from one bit group.
    private const int _numericModeDigitsPerGroup = 3;

    // Numeric mode encodes three digits in 10 bits.
    private const int _numericModeFullGroupBitLength = 10;

    // Numeric full groups must decode below 1000 because they represent exactly three decimal digits.
    private const int _numericModeFullGroupValueLimit = 1_000;

    // Numeric mode encodes two digits in 7 bits.
    private const int _numericModeTwoDigitBitLength = 7;

    // Numeric two-digit groups must decode below 100 because they represent exactly two decimal digits.
    private const int _numericModeTwoDigitValueLimit = 100;

    // Numeric mode encodes one digit in 4 bits.
    private const int _numericModeSingleDigitBitLength = 4;

    // Numeric single-digit groups must decode below 10 because they represent exactly one decimal digit.
    private const int _numericModeSingleDigitValueLimit = 10;

    // Numeric mode output pads decoded full groups to preserve leading zeroes.
    private const string _numericModeFullGroupFormat = "D3";

    // Numeric mode output pads decoded two-digit groups to preserve leading zeroes.
    private const string _numericModeTwoDigitFormat = "D2";

    // Alphanumeric mode packs two characters into one 11-bit base-45 value.
    private const int _alphanumericModePairBitLength = 11;

    // A trailing single alphanumeric character is stored in 6 bits.
    private const int _alphanumericModeSingleBitLength = 6;

    // Alphanumeric mode uses base 45 because its specification character set has 45 entries.
    private const int _alphanumericModeRadix = 45;

    // A zero mode indicator marks the end of the QR segment stream.
    private const int _terminatorModeIndicator = 0;

    // ECI assignment values below 2^7 use the shortest QR ECI encoding form.
    private const int _singleByteEciAssignmentLimit = 1 << 7;

    // The two-byte ECI prefix is identified by the leading bit pattern 10.
    private const int _twoByteEciPrefix = 0b10;

    // The three-byte ECI prefix is identified by the leading bit pattern 110.
    private const int _threeByteEciPrefix = 0b110;

    // Two-byte ECI designators carry 14 assignment-value bits after the prefix.
    private const int _twoByteEciAssignmentBitLength = 14;

    // Three-byte ECI designators carry the final 16 assignment-value bits after the first byte.
    private const int _threeByteEciRemainingBitLength = 16;

    // UTF-8 uses ECI assignment value 26 in QR Code ECI payloads.
    private const int _utf8EciAssignmentValue = 26;

    // ISO-8859-1 uses ECI assignment value 3 and is the QR default byte interpretation.
    private const int _iso88591EciAssignmentValue = 3;

    // The exception text identifies QR versions outside the model 2 supported range.
    private const string _versionOutOfRangeMessage = "Version number out of range";

    // The exception text identifies constructor version arguments outside the model 2 supported range.
    private const string _versionValueOutOfRangeMessage = "Version value out of range";

    // The exception text identifies mask values outside the QR mask pattern range.
    private const string _maskOutOfRangeMessage = "Mask value out of range";

    // The exception text identifies invalid encoder option combinations.
    private const string _invalidValueMessage = "Invalid value";

    // The exception text identifies segment payloads too large for the requested QR version range.
    private const string _segmentTooLongMessage = "Segment too long";

    // The exception text format reports when encoded data bits exceed the selected QR version capacity.
    private const string _dataLengthExceedsCapacityMessageFormat = "Data length = {0} bits, Max capacity = {1} bits";

    // The exception text identifies generated data streams whose codeword count does not match the QR version.
    private const string _invalidDataLengthMessage = "Invalid data length";

    // The exception text identifies invalid Reed-Solomon divisor degrees.
    private const string _degreeOutOfRangeMessage = "Degree out of range";

    // The exception text identifies malformed QR payload bit streams.
    private const string _invalidQrCodeDataMessage = "Invalid QR Code data";

    // The exception text identifies QR modes this decoder intentionally does not interpret.
    private const string _unsupportedSegmentModeMessage = "Unsupported QR segment mode";

    // The exception text identifies non-byte payloads passed to binary decoding.
    private const string _nonByteModeDataMessage = "QR Code contains non-byte-mode data";

    // The exception text identifies byte payloads that use an unsupported ECI character set.
    private const string _unsupportedEciAssignmentMessage = "Unsupported ECI assignment value";

    // Penalty rule N1 adds 3 points for each same-color run of five modules before longer-run increments.
    private const int _penaltyN1 = 3;

    // Penalty rule N2 adds 3 points for each 2-by-2 same-color block.
    private const int _penaltyN2 = 3;

    // Penalty rule N3 adds 40 points for each finder-like run pattern.
    private const int _penaltyN3 = 40;

    // Penalty rule N4 adds 10 points for each 5% dark-module deviation band from 50%.
    private const int _penaltyN4 = 10;

    // The number of ECC codewords in each block is fixed by QR version and ECC level, so table data stays grouped.
    private static readonly int[][] _errorCorrectionCodewordsPerBlock =
    [
        [-1, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [-1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28],
        [-1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [-1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
    ];

    // The number of ECC blocks is fixed by QR version and ECC level, so table data stays grouped.
    private static readonly int[][] _errorCorrectionBlocks =
    [
        [-1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25],
        [-1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49],
        [-1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68],
        [-1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81],
    ];

    private bool[][] _modules = [];
    private bool[][]? _isFunction;

    /// <summary>
    /// Initializes a new instance of the <see cref="QRCodeGenerator"/> class.
    /// </summary>
    public QRCodeGenerator()
    {
    }

    private QRCodeGenerator(int version, QRErrorCorrectionLevel errorCorrectionLevel, byte[] dataCodewords, int mask)
    {
        if (version < MinVersion || version > MaxVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(version), _versionValueOutOfRangeMessage);
        }

        if (mask < _automaticMask || mask > _maximumMask)
        {
            throw new ArgumentOutOfRangeException(nameof(mask), _maskOutOfRangeMessage);
        }

        ArgumentNullException.ThrowIfNull(dataCodewords);

        Version = version;
        Size = QRCodeSymbol.CalculateSize(version);
        ErrorCorrectionLevel = errorCorrectionLevel;
        _modules = CreateGrid(Size);
        _isFunction = CreateGrid(Size);

        DrawFunctionPatterns();
        var allCodewords = AddEccAndInterleave(dataCodewords);
        DrawCodewords(allCodewords);

        if (mask == _automaticMask)
        {
            var minPenalty = int.MaxValue;
            for (var i = _minimumMask; i <= _maximumMask; i++)
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

    private int Version { get; }

    private int Size { get; }

    private QRErrorCorrectionLevel ErrorCorrectionLevel { get; }

    private int Mask { get; }

    /// <summary>
    /// Returns a QR Code representing the specified Unicode text.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <param name="errorCorrectionLevel">The error correction level to use.</param>
    /// <returns>A QR Code representing the text.</returns>
    public QRCodeSymbol EncodeText(string text, QRErrorCorrectionLevel errorCorrectionLevel)
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
    public QRCodeSymbol EncodeBinary(byte[] data, QRErrorCorrectionLevel errorCorrectionLevel)
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
    public QRCodeSymbol EncodeSegments(IReadOnlyCollection<QRSegment> segments, QRErrorCorrectionLevel errorCorrectionLevel)
    {
        return EncodeSegments(segments, errorCorrectionLevel, MinVersion, MaxVersion, _automaticMask, true);
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
    public QRCodeSymbol EncodeSegments(
        IReadOnlyCollection<QRSegment> segments,
        QRErrorCorrectionLevel errorCorrectionLevel,
        int minVersion,
        int maxVersion,
        int mask,
        bool boostErrorCorrectionLevel)
    {
        ArgumentNullException.ThrowIfNull(segments);

        if (minVersion < MinVersion
            || minVersion > maxVersion
            || maxVersion > MaxVersion
            || mask < _automaticMask
            || mask > _maximumMask)
        {
            throw new ArgumentOutOfRangeException(nameof(minVersion), _invalidValueMessage);
        }

        var version = minVersion;
        int dataUsedBits;
        while (true)
        {
            var dataCapacityBits = GetNumDataCodewords(version, errorCorrectionLevel) * _bitsPerByte;
            dataUsedBits = QRSegment.GetTotalBits(segments, version);
            if (dataUsedBits != QRSegment.InvalidTotalBitLength && dataUsedBits <= dataCapacityBits)
            {
                break;
            }

            if (version >= maxVersion)
            {
                var message = dataUsedBits == QRSegment.InvalidTotalBitLength
                    ? _segmentTooLongMessage
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        _dataLengthExceedsCapacityMessageFormat,
                        dataUsedBits,
                        dataCapacityBits);
                throw new DataTooLongException(message);
            }

            version++;
        }

        foreach (var newErrorCorrectionLevel in Enum.GetValues<QRErrorCorrectionLevel>())
        {
            if (boostErrorCorrectionLevel
                && dataUsedBits <= GetNumDataCodewords(version, newErrorCorrectionLevel) * _bitsPerByte)
            {
                errorCorrectionLevel = newErrorCorrectionLevel;
            }
        }

        var buffer = new BitBuffer();
        foreach (var segment in segments)
        {
            buffer.AppendBits(segment.Mode.GetModeBits(), _modeIndicatorBitLength);
            buffer.AppendBits(segment.CharacterCount, segment.Mode.GetCharacterCountBits(version));
            buffer.AppendData(segment.GetRawData());
        }

        var dataCapacity = GetNumDataCodewords(version, errorCorrectionLevel) * _bitsPerByte;
        buffer.AppendBits(0, Math.Min(_terminatorBitLength, dataCapacity - buffer.BitLength));
        buffer.AppendBits(0, (_bitsPerByte - (buffer.BitLength % _bitsPerByte)) % _bitsPerByte);

        for (var padByte = _padCodewordFirst;
            buffer.BitLength < dataCapacity;
            padByte ^= _padCodewordFirst ^ _padCodewordSecond)
        {
            buffer.AppendBits(padByte, _bitsPerByte);
        }

        var dataCodewords = buffer.ToByteArray();

        return new QRCodeGenerator(version, errorCorrectionLevel, dataCodewords, mask).CreateQRCode();
    }

    /// <summary>
    /// Decodes a QR Code symbol into Unicode text.
    /// </summary>
    /// <param name="qrCode">The QR Code symbol to decode.</param>
    /// <returns>The decoded text.</returns>
    public string DecodeText(QRCodeSymbol qrCode)
    {
        ArgumentNullException.ThrowIfNull(qrCode);

        var result = new StringBuilder();
        var encoding = Encoding.UTF8;
        foreach (var segment in DecodePayloadSegments(qrCode))
        {
            switch (segment.Mode)
            {
                case QRSegmentMode.Numeric:
                case QRSegmentMode.Alphanumeric:
                    result.Append(segment.Text);
                    break;
                case QRSegmentMode.Byte:
                    result.Append(encoding.GetString(segment.Bytes));
                    break;
                case QRSegmentMode.Eci:
                    encoding = GetEciEncoding(segment.AssignmentValue);
                    break;
                default:
                    throw new NotSupportedException(_unsupportedSegmentModeMessage);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Decodes byte-mode payload data from a QR Code symbol.
    /// </summary>
    /// <param name="qrCode">The QR Code symbol to decode.</param>
    /// <returns>The decoded binary data.</returns>
    public byte[] DecodeBinary(QRCodeSymbol qrCode)
    {
        ArgumentNullException.ThrowIfNull(qrCode);

        using var result = new MemoryStream();
        foreach (var segment in DecodePayloadSegments(qrCode))
        {
            if (segment.Mode == QRSegmentMode.Eci)
            {
                continue;
            }

            if (segment.Mode != QRSegmentMode.Byte)
            {
                throw new NotSupportedException(_nonByteModeDataMessage);
            }

            result.Write(segment.Bytes);
        }

        return result.ToArray();
    }

    private QRCodeSymbol CreateQRCode()
    {
        return new QRCodeSymbol(Version, ErrorCorrectionLevel, Mask, _modules);
    }

    internal static bool GetBit(int value, int index)
    {
        return ((value >>> index) & 1) != 0;
    }

    internal static int GetNumDataCodewords(int version, QRErrorCorrectionLevel errorCorrectionLevel)
    {
        return (GetNumRawDataModules(version) / _bitsPerByte)
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
            throw new ArgumentOutOfRangeException(nameof(version), _versionOutOfRangeMessage);
        }

        var size = QRCodeSymbol.CalculateSize(version);
        var result = size * size;
        result -= _finderPatternRawRegionModulesPerSide * _finderPatternRawRegionModulesPerSide * _finderPatternCount;
        result -= (_formatBitLength * _formatInformationCopyCount) + _darkModuleCount;
        result -= (size - _timingPatternExcludedEndModules) * _timingPatternLineCount;
        if (version >= _alignmentPatternMinimumCentersPerAxis)
        {
            var numAlign = (version / _alignmentPatternVersionDivisor) + _alignmentPatternMinimumCentersPerAxis;
            result -= (numAlign - _reedSolomonIdentity)
                * (numAlign - _reedSolomonIdentity)
                * _alignmentPatternModulesPerSide
                * _alignmentPatternModulesPerSide;
            result -= (numAlign - _alignmentPatternTimingOverlapCount)
                * _alignmentPatternTimingOverlapCount
                * _alignmentPatternTimingOverlapModules;
            if (version >= _minimumVersionWithVersionInformation)
            {
                result -= _versionInformationShortSide
                    * _versionInformationLongSide
                    * _versionInformationCopyCount;
            }
        }

        return result;
    }

    private static byte[] ReedSolomonComputeDivisor(int degree)
    {
        if (degree < _reedSolomonIdentity || degree > _reedSolomonMaximumDegree)
        {
            throw new ArgumentOutOfRangeException(nameof(degree), _degreeOutOfRangeMessage);
        }

        var result = new byte[degree];
        result[degree - _reedSolomonIdentity] = _reedSolomonIdentity;

        var root = _reedSolomonIdentity;
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

            root = ReedSolomonMultiply(root, _reedSolomonGeneratorRootFactor);
        }

        return result;
    }

    private static byte[] ReedSolomonComputeRemainder(ReadOnlySpan<byte> data, ReadOnlySpan<byte> divisor)
    {
        var result = new byte[divisor.Length];
        foreach (var value in data)
        {
            var factor = value ^ result[0];
            result.AsSpan(1).CopyTo(result);
            result[^1] = 0;
            for (var i = 0; i < result.Length; i++)
            {
                result[i] ^= (byte)ReedSolomonMultiply(divisor[i], factor);
            }
        }

        return result;
    }

    private static IReadOnlyList<DecodedSegment> DecodePayloadSegments(QRCodeSymbol qrCode)
    {
        var codewords = ExtractCodewords(qrCode);
        var dataCodewords = DeinterleaveDataCodewords(codewords, qrCode.Version, qrCode.ErrorCorrectionLevel);
        var reader = new BitReader(dataCodewords);
        var result = new List<DecodedSegment>();

        while (reader.RemainingBits >= _modeIndicatorBitLength)
        {
            var modeBits = reader.ReadBits(_modeIndicatorBitLength);
            if (modeBits == _terminatorModeIndicator)
            {
                break;
            }

            var mode = QRSegmentModeExtensions.FromModeBits(modeBits);
            if (mode == QRSegmentMode.Eci)
            {
                result.Add(DecodedSegment.ForEci(ReadEciAssignmentValue(reader)));
                continue;
            }

            var characterCount = reader.ReadBits(mode.GetCharacterCountBits(qrCode.Version));
            result.Add(mode switch
            {
                QRSegmentMode.Numeric => DecodedSegment.ForText(mode, DecodeNumericSegment(reader, characterCount)),
                QRSegmentMode.Alphanumeric => DecodedSegment.ForText(mode, DecodeAlphanumericSegment(reader, characterCount)),
                QRSegmentMode.Byte => DecodedSegment.ForBytes(ReadByteSegment(reader, characterCount)),
                QRSegmentMode.Kanji => throw new NotSupportedException(_unsupportedSegmentModeMessage),
                _ => throw new FormatException(_invalidQrCodeDataMessage),
            });
        }

        return result;
    }

    private static string DecodeNumericSegment(BitReader reader, int characterCount)
    {
        var result = new StringBuilder(characterCount);
        while (characterCount >= _numericModeDigitsPerGroup)
        {
            var value = reader.ReadBits(_numericModeFullGroupBitLength);
            if (value >= _numericModeFullGroupValueLimit)
            {
                throw new FormatException(_invalidQrCodeDataMessage);
            }

            result.Append(value.ToString(_numericModeFullGroupFormat, CultureInfo.InvariantCulture));
            characterCount -= _numericModeDigitsPerGroup;
        }

        if (characterCount == _dataPlacementColumnPairWidth)
        {
            var value = reader.ReadBits(_numericModeTwoDigitBitLength);
            if (value >= _numericModeTwoDigitValueLimit)
            {
                throw new FormatException(_invalidQrCodeDataMessage);
            }

            result.Append(value.ToString(_numericModeTwoDigitFormat, CultureInfo.InvariantCulture));
        }
        else if (characterCount == _reedSolomonIdentity)
        {
            var value = reader.ReadBits(_numericModeSingleDigitBitLength);
            if (value >= _numericModeSingleDigitValueLimit)
            {
                throw new FormatException(_invalidQrCodeDataMessage);
            }

            result.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        return result.ToString();
    }

    private static string DecodeAlphanumericSegment(BitReader reader, int characterCount)
    {
        var result = new StringBuilder(characterCount);
        while (characterCount >= _dataPlacementColumnPairWidth)
        {
            var value = reader.ReadBits(_alphanumericModePairBitLength);
            if (value >= _alphanumericModeRadix * _alphanumericModeRadix)
            {
                throw new FormatException(_invalidQrCodeDataMessage);
            }

            result.Append(GetAlphanumericCharacter(value / _alphanumericModeRadix));
            result.Append(GetAlphanumericCharacter(value % _alphanumericModeRadix));
            characterCount -= _dataPlacementColumnPairWidth;
        }

        if (characterCount == _reedSolomonIdentity)
        {
            result.Append(GetAlphanumericCharacter(reader.ReadBits(_alphanumericModeSingleBitLength)));
        }

        return result.ToString();
    }

    private static char GetAlphanumericCharacter(int value)
    {
        if (value < 0 || value >= _alphanumericCharset.Length)
        {
            throw new FormatException(_invalidQrCodeDataMessage);
        }

        return _alphanumericCharset[value];
    }

    private static byte[] ReadByteSegment(BitReader reader, int byteCount)
    {
        var result = new byte[byteCount];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (byte)reader.ReadBits(_bitsPerByte);
        }

        return result;
    }

    private static int ReadEciAssignmentValue(BitReader reader)
    {
        var firstByte = reader.ReadBits(_bitsPerByte);
        if (firstByte < _singleByteEciAssignmentLimit)
        {
            return firstByte;
        }

        if ((firstByte >>> (_bitsPerByte - _dataPlacementColumnPairWidth)) == _twoByteEciPrefix)
        {
            return ((firstByte & ((1 << (_bitsPerByte - _dataPlacementColumnPairWidth)) - _reedSolomonIdentity))
                << _bitsPerByte)
                | reader.ReadBits(_bitsPerByte);
        }

        if ((firstByte >>> (_bitsPerByte - _numericModeDigitsPerGroup)) == _threeByteEciPrefix)
        {
            return ((firstByte & ((1 << (_bitsPerByte - _numericModeDigitsPerGroup)) - _reedSolomonIdentity))
                << _threeByteEciRemainingBitLength)
                | reader.ReadBits(_threeByteEciRemainingBitLength);
        }

        throw new FormatException(_invalidQrCodeDataMessage);
    }

    private static Encoding GetEciEncoding(int assignmentValue)
    {
        return assignmentValue switch
        {
            _utf8EciAssignmentValue => Encoding.UTF8,
            _iso88591EciAssignmentValue => Encoding.Latin1,
            _ => throw new NotSupportedException(_unsupportedEciAssignmentMessage),
        };
    }

    private static byte[] ExtractCodewords(QRCodeSymbol qrCode)
    {
        var isFunction = CreateFunctionMask(qrCode.Version);
        var result = new byte[GetNumRawDataModules(qrCode.Version) / _bitsPerByte];
        var bitIndex = 0;

        for (var right = qrCode.Size - _reedSolomonIdentity;
            right >= _reedSolomonIdentity;
            right -= _dataPlacementColumnPairWidth)
        {
            if (right == _dataPlacementTimingColumn)
            {
                right = _dataPlacementColumnBeforeTiming;
            }

            for (var vertical = 0; vertical < qrCode.Size; vertical++)
            {
                for (var j = 0; j < _dataPlacementColumnPairWidth; j++)
                {
                    var x = right - j;
                    var upward = ((right + _reedSolomonIdentity) & _dataPlacementDirectionMask) == 0;
                    var y = upward ? qrCode.Size - _reedSolomonIdentity - vertical : vertical;
                    if (!isFunction[y][x] && bitIndex < result.Length * _bitsPerByte)
                    {
                        var bit = qrCode.GetModule(x, y) ^ MaskApplies(qrCode.Mask, x, y);
                        result[bitIndex >>> _bitIndexToByteShift] |= (byte)((bit ? _reedSolomonIdentity : 0)
                            << (_highestBitIndexInByte - (bitIndex & _bitIndexInByteMask)));
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
        var rawCodewords = GetNumRawDataModules(version) / _bitsPerByte;
        var numShortBlocks = numBlocks - (rawCodewords % numBlocks);
        var shortBlockLength = rawCodewords / numBlocks;
        var blocks = new byte[numBlocks][];
        for (var i = 0; i < blocks.Length; i++)
        {
            blocks[i] = new byte[shortBlockLength + _reedSolomonIdentity];
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
            var dataLength = shortBlockLength
                - blockEccLength
                + (i < numShortBlocks ? 0 : _reedSolomonIdentity);
            Array.Copy(blocks[i], 0, result, offset, dataLength);
            offset += dataLength;
        }

        return result;
    }

    private static bool[][] CreateFunctionMask(int version)
    {
        var size = QRCodeSymbol.CalculateSize(version);
        var result = CreateGrid(size);
        for (var i = 0; i < size; i++)
        {
            result[i][_timingPatternCoordinate] = true;
            result[_timingPatternCoordinate][i] = true;
        }

        DrawFinderFunctionPattern(result, _finderPatternCenterOffset, _finderPatternCenterOffset);
        DrawFinderFunctionPattern(result, size - _finderPatternFarEdgeOffset, _finderPatternCenterOffset);
        DrawFinderFunctionPattern(result, _finderPatternCenterOffset, size - _finderPatternFarEdgeOffset);

        var alignPatternPositions = GetAlignmentPatternPositions(version);
        for (var i = 0; i < alignPatternPositions.Length; i++)
        {
            for (var j = 0; j < alignPatternPositions.Length; j++)
            {
                if (!((i == 0 && j == 0)
                    || (i == 0 && j == alignPatternPositions.Length - _reedSolomonIdentity)
                    || (i == alignPatternPositions.Length - _reedSolomonIdentity && j == 0)))
                {
                    DrawAlignmentFunctionPattern(result, alignPatternPositions[i], alignPatternPositions[j]);
                }
            }
        }

        DrawFormatFunctionModules(result);
        DrawVersionFunctionModules(result, version);
        return result;
    }

    private static void DrawFinderFunctionPattern(bool[][] isFunction, int x, int y)
    {
        for (var dy = -_finderPatternRadius; dy <= _finderPatternRadius; dy++)
        {
            for (var dx = -_finderPatternRadius; dx <= _finderPatternRadius; dx++)
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
        for (var dy = -_alignmentPatternRadius; dy <= _alignmentPatternRadius; dy++)
        {
            for (var dx = -_alignmentPatternRadius; dx <= _alignmentPatternRadius; dx++)
            {
                isFunction[y + dy][x + dx] = true;
            }
        }
    }

    private static void DrawFormatFunctionModules(bool[][] isFunction)
    {
        var size = isFunction.Length;
        for (var i = 0; i <= _formatFirstVerticalRunLastBit; i++)
        {
            isFunction[i][_finderPatternRawRegionModulesPerSide] = true;
        }

        isFunction[_highestBitIndexInByte][_finderPatternRawRegionModulesPerSide] = true;
        isFunction[_finderPatternRawRegionModulesPerSide][_finderPatternRawRegionModulesPerSide] = true;
        isFunction[_finderPatternRawRegionModulesPerSide][_highestBitIndexInByte] = true;
        for (var i = _formatSecondRunFirstBit; i <= _formatLastBit; i++)
        {
            isFunction[_finderPatternRawRegionModulesPerSide][_formatMirrorCoordinateBase - i] = true;
        }

        for (var i = 0; i < _bitsPerByte; i++)
        {
            isFunction[_finderPatternRawRegionModulesPerSide][size - _reedSolomonIdentity - i] = true;
        }

        for (var i = _formatSecondVerticalRunFirstBit; i <= _formatLastBit; i++)
        {
            isFunction[size - _formatBitLength + i][_finderPatternRawRegionModulesPerSide] = true;
        }

        isFunction[size - _darkModuleBottomOffset][_finderPatternRawRegionModulesPerSide] = true;
    }

    private static void DrawVersionFunctionModules(bool[][] isFunction, int version)
    {
        if (version < _minimumVersionWithVersionInformation)
        {
            return;
        }

        var size = isFunction.Length;
        for (var i = 0; i < _versionInformationBitLength; i++)
        {
            var a = size - _versionInformationFarEdgeOffset + (i % _versionInformationColumnCount);
            var b = i / _versionInformationColumnCount;
            isFunction[b][a] = true;
            isFunction[a][b] = true;
        }
    }

    private static bool MaskApplies(int mask, int x, int y)
    {
        return mask switch
        {
            _maskPattern0 => (x + y) % _dataPlacementColumnPairWidth == 0,
            _maskPattern1 => y % _dataPlacementColumnPairWidth == 0,
            _maskPattern2 => x % _versionInformationColumnCount == 0,
            _maskPattern3 => (x + y) % _versionInformationColumnCount == 0,
            _maskPattern4 => ((x / _versionInformationColumnCount) + (y / _dataPlacementColumnPairWidth))
                % _dataPlacementColumnPairWidth == 0,
            _maskPattern5 => ((x * y) % _dataPlacementColumnPairWidth)
                + ((x * y) % _versionInformationColumnCount) == 0,
            _maskPattern6 => (((x * y) % _dataPlacementColumnPairWidth)
                + ((x * y) % _versionInformationColumnCount))
                % _dataPlacementColumnPairWidth == 0,
            _maskPattern7 => (((x + y) % _dataPlacementColumnPairWidth)
                + ((x * y) % _versionInformationColumnCount))
                % _dataPlacementColumnPairWidth == 0,
            _ => throw new ArgumentOutOfRangeException(nameof(mask), _maskOutOfRangeMessage),
        };
    }

    private static int ReedSolomonMultiply(int x, int y)
    {
        var z = 0;
        for (var i = _reedSolomonBitCount - _reedSolomonIdentity; i >= 0; i--)
        {
            z = (z << _reedSolomonIdentity)
                ^ ((z >>> (_reedSolomonBitCount - _reedSolomonIdentity)) * _reedSolomonReducingPolynomial);
            z ^= ((y >>> i) & _reedSolomonIdentity) * x;
        }

        return z;
    }

    private byte[] AddEccAndInterleave(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length != GetNumDataCodewords(Version, ErrorCorrectionLevel))
        {
            throw new ArgumentException(_invalidDataLengthMessage, nameof(data));
        }

        var numBlocks = _errorCorrectionBlocks[(int)ErrorCorrectionLevel][Version];
        var blockEccLength = _errorCorrectionCodewordsPerBlock[(int)ErrorCorrectionLevel][Version];
        var rawCodewords = GetNumRawDataModules(Version) / _bitsPerByte;
        var numShortBlocks = numBlocks - (rawCodewords % numBlocks);
        var shortBlockLength = rawCodewords / numBlocks;

        var blocks = new byte[numBlocks][];
        var rsDivisor = ReedSolomonComputeDivisor(blockEccLength);
        var offset = 0;
        for (var i = 0; i < numBlocks; i++)
        {
            var dataLength = shortBlockLength - blockEccLength + (i < numShortBlocks ? 0 : 1);
            var blockData = data.AsSpan(offset, dataLength);
            offset += blockData.Length;
            var block = new byte[shortBlockLength + 1];
            blockData.CopyTo(block);
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
            SetFunctionModule(_timingPatternCoordinate, i, i % _dataPlacementColumnPairWidth == 0);
            SetFunctionModule(i, _timingPatternCoordinate, i % _dataPlacementColumnPairWidth == 0);
        }

        DrawFinderPattern(_finderPatternCenterOffset, _finderPatternCenterOffset);
        DrawFinderPattern(Size - _finderPatternFarEdgeOffset, _finderPatternCenterOffset);
        DrawFinderPattern(_finderPatternCenterOffset, Size - _finderPatternFarEdgeOffset);

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
        var data = (ErrorCorrectionLevel.GetFormatBits() << _versionInformationColumnCount) | mask;
        var remainder = data;
        for (var i = 0; i < _formatRemainderBitLength; i++)
        {
            remainder = (remainder << _reedSolomonIdentity)
                ^ ((remainder >>> _formatRemainderTopBit) * _formatGeneratorPolynomial);
        }

        var bits = ((data << _formatRemainderBitLength) | remainder) ^ _formatMaskPattern;

        for (var i = 0; i <= _formatFirstVerticalRunLastBit; i++)
        {
            SetFunctionModule(_finderPatternRawRegionModulesPerSide, i, GetBit(bits, i));
        }

        SetFunctionModule(_finderPatternRawRegionModulesPerSide, _highestBitIndexInByte, GetBit(bits, _formatSkippedTimingBit));
        SetFunctionModule(_finderPatternRawRegionModulesPerSide, _finderPatternRawRegionModulesPerSide, GetBit(bits, _formatCenterBit));
        SetFunctionModule(_highestBitIndexInByte, _finderPatternRawRegionModulesPerSide, GetBit(bits, _formatPostCenterBit));
        for (var i = _formatSecondRunFirstBit; i <= _formatLastBit; i++)
        {
            SetFunctionModule(_formatMirrorCoordinateBase - i, _finderPatternRawRegionModulesPerSide, GetBit(bits, i));
        }

        for (var i = 0; i < _bitsPerByte; i++)
        {
            SetFunctionModule(Size - _reedSolomonIdentity - i, _finderPatternRawRegionModulesPerSide, GetBit(bits, i));
        }

        for (var i = _formatSecondVerticalRunFirstBit; i <= _formatLastBit; i++)
        {
            SetFunctionModule(_finderPatternRawRegionModulesPerSide, Size - _formatBitLength + i, GetBit(bits, i));
        }

        SetFunctionModule(_finderPatternRawRegionModulesPerSide, Size - _darkModuleBottomOffset, true);
    }

    private void DrawVersion()
    {
        if (Version < _minimumVersionWithVersionInformation)
        {
            return;
        }

        var remainder = Version;
        for (var i = 0; i < _versionRemainderBitLength; i++)
        {
            remainder = (remainder << _reedSolomonIdentity)
                ^ ((remainder >>> _versionRemainderTopBit) * _versionGeneratorPolynomial);
        }

        var bits = (Version << _versionRemainderBitLength) | remainder;
        for (var i = 0; i < _versionInformationBitLength; i++)
        {
            var bit = GetBit(bits, i);
            var a = Size - _versionInformationFarEdgeOffset + (i % _versionInformationColumnCount);
            var b = i / _versionInformationColumnCount;
            SetFunctionModule(a, b, bit);
            SetFunctionModule(b, a, bit);
        }
    }

    private void DrawFinderPattern(int x, int y)
    {
        for (var dy = -_finderPatternRadius; dy <= _finderPatternRadius; dy++)
        {
            for (var dx = -_finderPatternRadius; dx <= _finderPatternRadius; dx++)
            {
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                var xx = x + dx;
                var yy = y + dy;
                if (0 <= xx && xx < Size && 0 <= yy && yy < Size)
                {
                    SetFunctionModule(
                        xx,
                        yy,
                        distance != _finderPatternLightRingInnerDistance
                            && distance != _finderPatternLightRingOuterDistance);
                }
            }
        }
    }

    private void DrawAlignmentPattern(int x, int y)
    {
        for (var dy = -_alignmentPatternRadius; dy <= _alignmentPatternRadius; dy++)
        {
            for (var dx = -_alignmentPatternRadius; dx <= _alignmentPatternRadius; dx++)
            {
                SetFunctionModule(
                    x + dx,
                    y + dy,
                    Math.Max(Math.Abs(dx), Math.Abs(dy)) != _alignmentPatternLightRingDistance);
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

        if (data.Length != GetNumRawDataModules(Version) / _bitsPerByte)
        {
            throw new ArgumentException(_invalidDataLengthMessage, nameof(data));
        }

        var i = 0;
        for (var right = Size - _reedSolomonIdentity;
            right >= _reedSolomonIdentity;
            right -= _dataPlacementColumnPairWidth)
        {
            if (right == _dataPlacementTimingColumn)
            {
                right = _dataPlacementColumnBeforeTiming;
            }

            for (var vertical = 0; vertical < Size; vertical++)
            {
                for (var j = 0; j < _dataPlacementColumnPairWidth; j++)
                {
                    var x = right - j;
                    var upward = ((right + _reedSolomonIdentity) & _dataPlacementDirectionMask) == 0;
                    var y = upward ? Size - _reedSolomonIdentity - vertical : vertical;
                    if (!_isFunction![y][x] && i < data.Length * _bitsPerByte)
                    {
                        _modules[y][x] = GetBit(
                            data[i >>> _bitIndexToByteShift],
                            _highestBitIndexInByte - (i & _bitIndexInByteMask));
                        i++;
                    }
                }
            }
        }
    }

    private void ApplyMask(int mask)
    {
        if (mask < _minimumMask || mask > _maximumMask)
        {
            throw new ArgumentOutOfRangeException(nameof(mask), _maskOutOfRangeMessage);
        }

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var invert = mask switch
                {
                    _maskPattern0 => (x + y) % _dataPlacementColumnPairWidth == 0,
                    _maskPattern1 => y % _dataPlacementColumnPairWidth == 0,
                    _maskPattern2 => x % _versionInformationColumnCount == 0,
                    _maskPattern3 => (x + y) % _versionInformationColumnCount == 0,
                    _maskPattern4 => ((x / _versionInformationColumnCount) + (y / _dataPlacementColumnPairWidth))
                        % _dataPlacementColumnPairWidth == 0,
                    _maskPattern5 => ((x * y) % _dataPlacementColumnPairWidth)
                        + ((x * y) % _versionInformationColumnCount) == 0,
                    _maskPattern6 => (((x * y) % _dataPlacementColumnPairWidth)
                        + ((x * y) % _versionInformationColumnCount))
                        % _dataPlacementColumnPairWidth == 0,
                    _maskPattern7 => (((x + y) % _dataPlacementColumnPairWidth)
                        + ((x * y) % _versionInformationColumnCount))
                        % _dataPlacementColumnPairWidth == 0,
                    _ => throw new ArgumentOutOfRangeException(nameof(mask), _maskOutOfRangeMessage),
                };
                _modules[y][x] ^= invert && !_isFunction![y][x];
            }
        }
    }

    private int GetPenaltyScore()
    {
        var result = 0;

        Span<int> runHistory = stackalloc int[_finderPenaltyRunHistoryLength];
        for (var y = 0; y < Size; y++)
        {
            var runColor = false;
            var runX = 0;
            runHistory.Clear();
            for (var x = 0; x < Size; x++)
            {
                if (_modules[y][x] == runColor)
                {
                    runX++;
                    result += runX == _penaltyLongRunThreshold
                        ? _penaltyN1
                        : runX > _penaltyLongRunThreshold ? _reedSolomonIdentity : 0;
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
            runHistory.Clear();
            for (var y = 0; y < Size; y++)
            {
                if (_modules[y][x] == runColor)
                {
                    runY++;
                    result += runY == _penaltyLongRunThreshold
                        ? _penaltyN1
                        : runY > _penaltyLongRunThreshold ? _reedSolomonIdentity : 0;
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
        var k = ((Math.Abs((dark * _penaltyDarkModuleMultiplier)
            - (total * _penaltyIdealDarkModuleMultiplier))
            + total
            - _penaltyDarkModuleAdjustment) / total) - _penaltyDarkModuleAdjustment;
        result += k * _penaltyN4;
        return result;
    }

    private int[] GetAlignmentPatternPositions()
    {
        return GetAlignmentPatternPositions(Version);
    }

    private static int[] GetAlignmentPatternPositions(int version)
    {
        if (version == _firstVersionWithoutAlignmentPatterns)
        {
            return [];
        }

        var numAlign = (version / _alignmentPatternVersionDivisor) + _alignmentPatternMinimumCentersPerAxis;
        var step = (((version * _alignmentPositionVersionMultiplier)
                + (numAlign * _alignmentPositionCenterMultiplier)
                + _alignmentPositionRoundingBias)
            / ((numAlign * _alignmentPositionGapMultiplier) - _alignmentPositionGapMultiplier))
            * _alignmentPositionEvenStepMultiplier;
        var result = new int[numAlign];
        result[0] = _alignmentPatternFirstPosition;
        var size = QRCodeSymbol.CalculateSize(version);
        for (int i = result.Length - _reedSolomonIdentity, position = size - _alignmentPatternFarEdgeOffset;
            i >= _reedSolomonIdentity;
            i--, position -= step)
        {
            result[i] = position;
        }

        return result;
    }

    private static int FinderPenaltyCountPatterns(ReadOnlySpan<int> runHistory)
    {
        var n = runHistory[1];
        var core = n > 0
            && runHistory[2] == n
            && runHistory[3] == n * _finderPenaltyCoreRunMultiplier
            && runHistory[4] == n
            && runHistory[5] == n;
        return (core
                && runHistory[0] >= n * _finderPenaltyQuietRunMultiplier
                && runHistory[_finderPenaltyRunHistoryLength - _reedSolomonIdentity] >= n
                    ? _reedSolomonIdentity
                    : 0)
            + (core
                && runHistory[_finderPenaltyRunHistoryLength - _reedSolomonIdentity]
                    >= n * _finderPenaltyQuietRunMultiplier
                && runHistory[0] >= n
                    ? _reedSolomonIdentity
                    : 0);
    }

    private int FinderPenaltyTerminateAndCount(bool currentRunColor, int currentRunLength, Span<int> runHistory)
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

    private void FinderPenaltyAddHistory(int currentRunLength, Span<int> runHistory)
    {
        if (runHistory[0] == 0)
        {
            currentRunLength += Size;
        }

        runHistory[..^1].CopyTo(runHistory[1..]);
        runHistory[0] = currentRunLength;
    }

    private sealed record DecodedSegment(
        QRSegmentMode Mode,
        string Text,
        byte[] Bytes,
        int AssignmentValue)
    {
        public static DecodedSegment ForText(QRSegmentMode mode, string text)
        {
            return new DecodedSegment(mode, text, [], 0);
        }

        public static DecodedSegment ForBytes(byte[] bytes)
        {
            return new DecodedSegment(QRSegmentMode.Byte, string.Empty, bytes, 0);
        }

        public static DecodedSegment ForEci(int assignmentValue)
        {
            return new DecodedSegment(QRSegmentMode.Eci, string.Empty, [], assignmentValue);
        }
    }

    private sealed class BitReader(byte[] data)
    {
        private int _index;

        public int RemainingBits => (data.Length * _bitsPerByte) - _index;

        public int ReadBits(int length)
        {
            if (length < 0 || length > RemainingBits)
            {
                throw new FormatException(_invalidQrCodeDataMessage);
            }

            var result = 0;
            for (var i = 0; i < length; i++)
            {
                result = (result << _reedSolomonIdentity)
                    | ((data[_index >>> _bitIndexToByteShift]
                        >>> (_highestBitIndexInByte - (_index & _bitIndexInByteMask))) & _reedSolomonIdentity);
                _index++;
            }

            return result;
        }
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
    // Low ECC uses format bits 01 per the QR format-information specification.
    private const int _lowFormatBits = 1;

    // Medium ECC uses format bits 00 per the QR format-information specification.
    private const int _mediumFormatBits = 0;

    // Quartile ECC uses format bits 11 per the QR format-information specification.
    private const int _quartileFormatBits = 3;

    // High ECC uses format bits 10 per the QR format-information specification.
    private const int _highFormatBits = 2;

    // The exception text identifies ECC levels outside this implementation's enum values.
    private const string _invalidErrorCorrectionLevelMessage = "Invalid error correction level";

    public static int GetFormatBits(this QRErrorCorrectionLevel level)
    {
        return level switch
        {
            QRErrorCorrectionLevel.Low => _lowFormatBits,
            QRErrorCorrectionLevel.Medium => _mediumFormatBits,
            QRErrorCorrectionLevel.Quartile => _quartileFormatBits,
            QRErrorCorrectionLevel.High => _highFormatBits,
            _ => throw new ArgumentOutOfRangeException(nameof(level), _invalidErrorCorrectionLevelMessage),
        };
    }
}
