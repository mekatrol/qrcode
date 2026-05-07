namespace Mekatrol.QRCode.Common;

/// <summary>
/// Generates QR Code symbols from text, binary data, or explicit segments.
/// </summary>
public interface IQRCodeGenerator
{
    /// <summary>
    /// Returns a QR Code representing the specified Unicode text.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <param name="errorCorrectionLevel">The error correction level to use.</param>
    /// <returns>A QR Code representing the text.</returns>
    QRCodeSymbol EncodeText(string text, QRErrorCorrectionLevel errorCorrectionLevel);

    /// <summary>
    /// Returns a QR Code representing the specified binary data.
    /// </summary>
    /// <param name="data">The binary data to encode.</param>
    /// <param name="errorCorrectionLevel">The error correction level to use.</param>
    /// <returns>A QR Code representing the data.</returns>
    QRCodeSymbol EncodeBinary(byte[] data, QRErrorCorrectionLevel errorCorrectionLevel);

    /// <summary>
    /// Returns a QR Code representing the specified segments.
    /// </summary>
    /// <param name="segments">The segments to encode.</param>
    /// <param name="errorCorrectionLevel">The error correction level to use.</param>
    /// <returns>A QR Code representing the segments.</returns>
    QRCodeSymbol EncodeSegments(IReadOnlyCollection<QRSegment> segments, QRErrorCorrectionLevel errorCorrectionLevel);

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
    QRCodeSymbol EncodeSegments(
        IReadOnlyCollection<QRSegment> segments,
        QRErrorCorrectionLevel errorCorrectionLevel,
        int minVersion,
        int maxVersion,
        int mask,
        bool boostErrorCorrectionLevel);
}
