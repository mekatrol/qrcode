namespace Mekatrol.QRCode.Common;

/// <summary>
/// Thrown when the supplied data does not fit any requested QR Code version.
/// </summary>
public class DataTooLongException : ArgumentException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataTooLongException"/> class.
    /// </summary>
    public DataTooLongException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataTooLongException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public DataTooLongException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataTooLongException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DataTooLongException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
