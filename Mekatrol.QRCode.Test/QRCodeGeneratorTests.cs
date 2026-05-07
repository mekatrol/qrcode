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
    // The short text fixture is small enough to fit in a version 1 QR Code at low ECC.
    private const string _shortTextFixture = "HELLO WORLD";

    // The numeric fixture includes leading zeroes so decode tests verify numeric group padding.
    private const string _numericTextFixture = "007123";

    // The byte text fixture contains lowercase letters, forcing byte-mode text encoding.
    private const string _byteTextFixture = "hello world";

    // Stress tests are opt-in under this category because they generate a large number of symbols.
    private const string _stressCategory = "Stress";

    // This environment variable enables the long-running random payload stress test outside a debugger.
    private const string _runStressEnvironmentVariable = "RUN_QR_STRESS";

    // The enabled value is "1" so the stress switch is easy to set in shells and CI jobs.
    private const string _runStressEnabledValue = "1";

    // The inconclusive message explains how to opt into the intentionally long stress test.
    private const string _stressInconclusiveMessage =
        "Set RUN_QR_STRESS=1 or run under a debugger to generate and validate 1,000,000 QR codes.";

    // The random text _randomTextAlphabet exercises common printable payload characters without creating invalid JSON strings.
    private const string _randomTextAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,;:-_/@#%+=[]{}()";

    // The fixed-width numeric format keeps generated years and months aligned with the app payload schema.
    private const string _fixedWidthNumberFormat = "D4";

    // The exception text identifies invalid mask values in the test decoder helper.
    private const string _maskOutOfRangeMessage = "Mask value out of range";

    // The stress test count is high enough to exercise many random payload sizes and mask choices.
    private const int _stressQrCodeCount = 1_000_000;

    // The generated random text matches the application payload's maximum supported text length.
    private const int _maximumTextLength = 100;

    // Version 1 is expected for the short text fixture because it is the smallest QR version.
    private const int _expectedShortTextVersion = 1;

    // Version 1 QR Codes are 21 by 21 modules.
    private const int _expectedVersionOneSize = 21;

    // QR Code mask pattern numbers start at 0.
    private const int _minimumMask = 0;

    // One is the common offset used when moving from a count to the last valid index.
    private const int _lastIndexOffset = 1;

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

    // The test uses 32 bytes to exceed version 1 high-ECC capacity and force DataTooLongException.
    private const int _oversizedVersionOneHighEccByteCount = 32;

    // Automatic mask selection is represented by -1 in the encoder API.
    private const int _automaticMask = -1;

    // The deterministic random seed makes the stress payload sequence repeatable.
    private const int _randomSeed = 0x5EED;

    // Random years are formatted from 0 through 9999 to fill a four-digit field.
    private const int _randomYearExclusiveMaximum = 10_000;

    // Random months start at January.
    private const int _randomMonthInclusiveMinimum = 1;

    // Random months use an exclusive upper bound one greater than December.
    private const int _randomMonthExclusiveMaximum = 13;

    // A GUID is exactly 16 bytes, and the stress payload validates base64 GUID round-tripping.
    private const int _guidByteLength = 16;

    // Byte mode is identified by the 0100 mode indicator in the QR bitstream.
    private const int _byteModeIndicator = 0x4;

    // Segment headers use a 4-bit mode indicator before the character count and payload data.
    private const int _modeIndicatorBitLength = 4;

    // One byte contains 8 bits and QR codewords are byte-sized.
    private const int _bitsPerByte = 8;

    // The right shift from a bit index to its containing byte index.
    private const int _bitIndexToByteShift = 3;

    // The mask for a bit index within one byte.
    private const int _bitIndexInByteMask = 7;

    // The highest bit position in a byte, used when packing bits in most-significant-bit order.
    private const int _highestBitIndexInByte = 7;

    // Data codewords are drawn two columns at a time in the QR zig-zag placement pattern.
    private const int _dataPlacementColumnPairWidth = 2;

    // The data-placement loop skips the vertical timing pattern column.
    private const int _dataPlacementTimingColumn = 6;

    // After skipping the timing column, placement resumes immediately to its left.
    private const int _dataPlacementColumnBeforeTiming = 5;

    // The mask-placement direction test uses bit 1 of the right-column coordinate.
    private const int _dataPlacementDirectionMask = 2;

    // The x coordinate of both QR timing patterns is 6 where they cross the finder separators.
    private const int _timingPatternCoordinate = 6;

    // Finder pattern centers are 3 modules in from the symbol edge.
    private const int _finderPatternCenterOffset = 3;

    // Finder pattern centers at the far edge are 4 modules back from the symbol size.
    private const int _finderPatternFarEdgeOffset = 4;

    // Finder pattern drawing covers a radius of four modules around the center.
    private const int _finderPatternRadius = 4;

    // Alignment pattern drawing covers a radius of two modules around its center.
    private const int _alignmentPatternRadius = 2;

    // Format information has 15 bits.
    private const int _formatBitLength = 15;

    // The vertical format-bit line is interrupted after bit 5 by the timing pattern.
    private const int _formatFirstVerticalRunLastBit = 5;

    // Format bit 7 is written after the skipped timing coordinate.
    private const int _formatSkippedTimingCoordinate = 7;

    // Format bit 8 is written at the central format coordinate.
    private const int _formatCenterCoordinate = 8;

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

    // Version information exists only for QR versions 7 and above.
    private const int _minimumVersionWithVersionInformation = 7;

    // Version information has 18 total function modules per copy.
    private const int _versionInformationBitLength = 18;

    // Version information is placed 11 modules back from the far edge.
    private const int _versionInformationFarEdgeOffset = 11;

    // Version information is arranged in three columns.
    private const int _versionInformationColumnCount = 3;

    // Version 1 has no alignment patterns.
    private const int _firstVersionWithoutAlignmentPatterns = 1;

    // Alignment pattern center count grows by one every seven QR versions.
    private const int _alignmentPatternVersionDivisor = 7;

    // Version 2 starts with two alignment pattern centers per axis.
    private const int _alignmentPatternMinimumCentersPerAxis = 2;

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

    // Test raw-size calculation uses 17 because version 1 size equals version * 4 + 17.
    private const int _versionSizeFormulaOffset = 17;

    // QR Code Model 2 grows by 4 modules per side for each version in the test decoder's size formula.
    private const int _additionalModulesPerVersion = 4;

    // The far alignment position formula uses version * 4 + 10.
    private const int _alignmentPatternFarPositionOffset = 10;

    // Finder pattern raw-region size is 8 by 8 modules in the raw-module count helper.
    private const int _finderPatternRawRegionModulesPerSide = 8;

    // There are three finder pattern regions in a QR Code symbol.
    private const int _finderPatternCount = 3;

    // Format information is written in two locations.
    private const int _formatInformationCopyCount = 2;

    // The fixed dark module contributes one module in the raw-module count helper.
    private const int _darkModuleCount = 1;

    // Timing patterns reserve two lines after subtracting the overlapping finder pattern regions.
    private const int _timingPatternLineCount = 2;

    // Timing patterns begin after the finder pattern border, leaving 16 modules excluded at both ends.
    private const int _timingPatternExcludedEndModules = 16;

    // One alignment pattern's raw function region is 5 by 5 modules.
    private const int _alignmentPatternModulesPerSide = 5;

    // Two alignment positions overlap finder timing zones in the raw-module count formula.
    private const int _alignmentPatternTimingOverlapCount = 2;

    // One alignment/timing overlap contributes 20 modules in the raw-module count formula.
    private const int _alignmentPatternTimingOverlapModules = 20;

    // Version information has 6 rows or columns in each placement area.
    private const int _versionInformationShortSide = 6;

    // Version information is written in two placement areas.
    private const int _versionInformationCopyCount = 2;

    // Byte-mode character count fields are 8 bits for versions 1 through 9.
    private const int _byteCharacterCountBitsShortVersions = 8;

    // Byte-mode character count fields are 16 bits for versions 10 through 40.
    private const int _byteCharacterCountBitsLongVersions = 16;

    // Version 9 is the last QR version using the short byte-mode character-count field.
    private const int _maximumShortByteCountVersion = 9;

    private readonly QRCodeGenerator _generator = new();

    // The binary fixture includes low, high, and non-printable byte values to verify byte-mode decoding.
    private static readonly byte[] _binaryFixture = [0x00, 0x10, 0x7F, 0xFF];

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

    [TestMethod]
    public void EncodeTextCreatesVersionOneQRCodeForShortText()
    {
        var qrCode = _generator.EncodeText(_shortTextFixture, QRErrorCorrectionLevel.Low);

        Assert.AreEqual(_expectedShortTextVersion, qrCode.Version);
        Assert.AreEqual(_expectedVersionOneSize, qrCode.Size);
        Assert.IsTrue(qrCode.Mask is >= _minimumMask and <= _maximumMask);
    }

    [TestMethod]
    public void GetModuleReturnsFalseForOutOfBoundsCoordinates()
    {
        var qrCode = _generator.EncodeText(_shortTextFixture, QRErrorCorrectionLevel.Low);

        Assert.IsFalse(qrCode.GetModule(-1, 0));
        Assert.IsFalse(qrCode.GetModule(0, -1));
        Assert.IsFalse(qrCode.GetModule(qrCode.Size, 0));
        Assert.IsFalse(qrCode.GetModule(0, qrCode.Size));
    }

    [TestMethod]
    public void FinderPatternTopLeftIsDrawn()
    {
        var qrCode = _generator.EncodeText(_shortTextFixture, QRErrorCorrectionLevel.Low);

        Assert.IsTrue(qrCode.GetModule(0, 0));
        Assert.IsTrue(qrCode.GetModule(_finderPatternCenterOffset, _finderPatternCenterOffset));
        Assert.IsTrue(qrCode.GetModule(_timingPatternCoordinate, _timingPatternCoordinate));
        Assert.IsFalse(qrCode.GetModule(_formatSkippedTimingCoordinate, _formatSkippedTimingCoordinate));
    }

    [TestMethod]
    public void EncodeSegmentsThrowsWhenDataCannotFitRequestedVersion()
    {
        var segment = QRSegment.MakeBytes(new byte[_oversizedVersionOneHighEccByteCount]);

        Assert.ThrowsExactly<DataTooLongException>(() =>
            _generator.EncodeSegments(
                [segment],
                QRErrorCorrectionLevel.High,
                _expectedShortTextVersion,
                _expectedShortTextVersion,
                _automaticMask,
                false));
    }

    [TestMethod]
    public void DecodeTextReturnsOriginalNumericText()
    {
        var qrCode = _generator.EncodeText(_numericTextFixture, QRErrorCorrectionLevel.Low);

        var result = _generator.DecodeText(qrCode);

        Assert.AreEqual(_numericTextFixture, result);
    }

    [TestMethod]
    public void DecodeTextReturnsOriginalAlphanumericText()
    {
        var qrCode = _generator.EncodeText(_shortTextFixture, QRErrorCorrectionLevel.Low);

        var result = _generator.DecodeText(qrCode);

        Assert.AreEqual(_shortTextFixture, result);
    }

    [TestMethod]
    public void DecodeTextReturnsOriginalByteModeText()
    {
        var qrCode = _generator.EncodeText(_byteTextFixture, QRErrorCorrectionLevel.Medium);

        var result = _generator.DecodeText(qrCode);

        Assert.AreEqual(_byteTextFixture, result);
        Assert.AreEqual(_byteTextFixture, DecodeByteModePayload(qrCode));
    }

    [TestMethod]
    public void DecodeBinaryReturnsOriginalBytes()
    {
        var qrCode = _generator.EncodeBinary(_binaryFixture, QRErrorCorrectionLevel.Medium);

        var result = _generator.DecodeBinary(qrCode);

        CollectionAssert.AreEqual(_binaryFixture, result);
    }

    [TestMethod]
    public void DecodeBinaryThrowsForNonByteModeData()
    {
        var qrCode = _generator.EncodeText(_shortTextFixture, QRErrorCorrectionLevel.Low);

        Assert.ThrowsExactly<NotSupportedException>(() => _generator.DecodeBinary(qrCode));
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
    [TestCategory(_stressCategory)]
    public void EncodeTextGeneratesAndValidatesOneMillionRandomProgramPayloads()
    {
        if (!Debugger.IsAttached && Environment.GetEnvironmentVariable(_runStressEnvironmentVariable) != _runStressEnabledValue)
        {
            Assert.Inconclusive(_stressInconclusiveMessage);
        }

        var random = new Random(_randomSeed);

        for (var i = 0; i < _stressQrCodeCount; i++)
        {
            var input = CreateRandomProgramInput(random);
            var expectedPayload = BuildProgramPayload(input);
            var qrCode = _generator.EncodeText(expectedPayload, QRErrorCorrectionLevel.Medium);

            var actualPayload = _generator.DecodeText(qrCode);
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
        var year = random.Next(_minimumMask, _randomYearExclusiveMaximum).ToString(_fixedWidthNumberFormat);
        var month = random.Next(_randomMonthInclusiveMinimum, _randomMonthExclusiveMaximum)
            .ToString(_fixedWidthNumberFormat);
        var uuidBytes = new byte[_guidByteLength];
        random.NextBytes(uuidBytes);
        var uuid = new Guid(uuidBytes);
        var uuidBase64 = Convert.ToBase64String(uuidBytes);
        var text = CreateRandomText(random);

        return new ProgramInput(year, month, uuid, uuidBase64, text);
    }

    private static string CreateRandomText(Random random)
    {
        var length = random.Next(_minimumMask, _maximumTextLength + _lastIndexOffset);
        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            builder.Append(_randomTextAlphabet[random.Next(_randomTextAlphabet.Length)]);
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

        Assert.AreEqual(_byteModeIndicator, reader.ReadBits(_modeIndicatorBitLength));
        var byteCount = reader.ReadBits(GetByteModeCharacterCountBits(qrCode.Version));
        var bytes = new byte[byteCount];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)reader.ReadBits(_bitsPerByte);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static byte[] ExtractCodewords(QRCodeSymbol qrCode)
    {
        var isFunction = CreateFunctionMask(qrCode.Version);
        var result = new byte[GetNumRawDataModules(qrCode.Version) / _bitsPerByte];
        var bitIndex = 0;

        for (var right = qrCode.Size - _lastIndexOffset;
            right >= _lastIndexOffset;
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
                    var upward = ((right + _lastIndexOffset) & _dataPlacementDirectionMask) == 0;
                    var y = upward ? qrCode.Size - _lastIndexOffset - vertical : vertical;
                    if (!isFunction[y][x] && bitIndex < result.Length * _bitsPerByte)
                    {
                        var bit = qrCode.GetModule(x, y) ^ MaskApplies(qrCode.Mask, x, y);
                        result[bitIndex >>> _bitIndexToByteShift] |= (byte)((bit ? _lastIndexOffset : 0)
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
            blocks[i] = new byte[shortBlockLength + _lastIndexOffset];
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
                + (i < numShortBlocks ? _minimumMask : _lastIndexOffset);
            Array.Copy(blocks[i], 0, result, offset, dataLength);
            offset += dataLength;
        }

        return result;
    }

    private static bool[][] CreateFunctionMask(int version)
    {
        var size = (version * _additionalModulesPerVersion) + _versionSizeFormulaOffset;
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
            isFunction[i][_formatCenterCoordinate] = true;
        }

        isFunction[_formatSkippedTimingCoordinate][_formatCenterCoordinate] = true;
        isFunction[_formatCenterCoordinate][_formatCenterCoordinate] = true;
        isFunction[_formatCenterCoordinate][_formatSkippedTimingCoordinate] = true;
        for (var i = _formatSecondRunFirstBit; i <= _formatLastBit; i++)
        {
            isFunction[_formatCenterCoordinate][_formatMirrorCoordinateBase - i] = true;
        }

        for (var i = 0; i < _bitsPerByte; i++)
        {
            isFunction[_formatCenterCoordinate][size - _lastIndexOffset - i] = true;
        }

        for (var i = _formatSecondVerticalRunFirstBit; i <= _formatLastBit; i++)
        {
            isFunction[size - _formatBitLength + i][_formatCenterCoordinate] = true;
        }

        isFunction[size - _darkModuleBottomOffset][_formatCenterCoordinate] = true;
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
        for (int i = result.Length - _lastIndexOffset,
            position = (version * _additionalModulesPerVersion) + _alignmentPatternFarPositionOffset;
            i >= _lastIndexOffset;
            i--, position -= step)
        {
            result[i] = position;
        }

        return result;
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

    private static int GetNumDataCodewords(int version, QRErrorCorrectionLevel errorCorrectionLevel)
    {
        return (GetNumRawDataModules(version) / _bitsPerByte)
            - (_errorCorrectionCodewordsPerBlock[(int)errorCorrectionLevel][version]
            * _errorCorrectionBlocks[(int)errorCorrectionLevel][version]);
    }

    private static int GetNumRawDataModules(int version)
    {
        var size = (version * _additionalModulesPerVersion) + _versionSizeFormulaOffset;
        var result = size * size;
        result -= _finderPatternRawRegionModulesPerSide * _finderPatternRawRegionModulesPerSide * _finderPatternCount;
        result -= (_formatBitLength * _formatInformationCopyCount) + _darkModuleCount;
        result -= (size - _timingPatternExcludedEndModules) * _timingPatternLineCount;
        if (version >= _alignmentPatternMinimumCentersPerAxis)
        {
            var numAlign = (version / _alignmentPatternVersionDivisor) + _alignmentPatternMinimumCentersPerAxis;
            result -= (numAlign - _lastIndexOffset)
                * (numAlign - _lastIndexOffset)
                * _alignmentPatternModulesPerSide
                * _alignmentPatternModulesPerSide;
            result -= (numAlign - _alignmentPatternTimingOverlapCount)
                * _alignmentPatternTimingOverlapCount
                * _alignmentPatternTimingOverlapModules;
            if (version >= _minimumVersionWithVersionInformation)
            {
                result -= _versionInformationShortSide
                    * _versionInformationColumnCount
                    * _versionInformationCopyCount;
            }
        }

        return result;
    }

    private static int GetByteModeCharacterCountBits(int version)
    {
        return version <= _maximumShortByteCountVersion
            ? _byteCharacterCountBitsShortVersions
            : _byteCharacterCountBitsLongVersions;
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
                result = (result << _lastIndexOffset)
                    | ((data[_index >>> _bitIndexToByteShift]
                        >>> (_highestBitIndexInByte - (_index & _bitIndexInByteMask))) & _lastIndexOffset);
                _index++;
            }

            return result;
        }
    }
}
