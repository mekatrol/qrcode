namespace Mekatrol.QRCode.Common;

/// <summary>
/// A QR Code symbol represented as an immutable square grid of dark and light modules.
/// </summary>
public sealed class QRCodeSymbol
{
    /// <summary>
    /// The number of modules on each side of a version 1 QR Code symbol.
    /// </summary>
    /// <remarks>
    /// QR Code Model 2 defines version 1 as a 21 by 21 module grid. Higher versions
    /// are larger grids, but version 1 is the fixed baseline for the size formula.
    /// </remarks>
    internal const int VersionOneModulesPerSide = 21;

    /// <summary>
    /// The number of modules added to each side when the QR Code version increases by one.
    /// </summary>
    /// <remarks>
    /// QR Code Model 2 grows by 4 modules per side for each version so alignment,
    /// timing, format, version, and data areas can scale in the standard layout.
    /// </remarks>
    internal const int AdditionalModulesPerVersion = 4;

    private readonly bool[][] _modules;

    internal QRCodeSymbol(int version, QRErrorCorrectionLevel errorCorrectionLevel, int mask, bool[][] modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        Version = version;
        Size = CalculateSize(version);
        ErrorCorrectionLevel = errorCorrectionLevel;
        Mask = mask;
        _modules = CopyModules(modules);
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
    /// Returns the color of the module at the specified coordinates.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns><see langword="true"/> if the module is dark; otherwise <see langword="false"/>.</returns>
    public bool GetModule(int x, int y)
    {
        return 0 <= x && x < Size && 0 <= y && y < Size && _modules[y][x];
    }

    internal static int CalculateSize(int version)
    {
        // QR Code Model 2 version 1 is 21 modules wide, and each higher version
        // adds 4 modules per side.
        return VersionOneModulesPerSide + ((version - QRCodeGenerator.MinVersion) * AdditionalModulesPerVersion);
    }

    private static bool[][] CopyModules(bool[][] modules)
    {
        var result = new bool[modules.Length][];
        for (var y = 0; y < modules.Length; y++)
        {
            result[y] = new bool[modules[y].Length];
            Array.Copy(modules[y], result[y], modules[y].Length);
        }

        return result;
    }
}
