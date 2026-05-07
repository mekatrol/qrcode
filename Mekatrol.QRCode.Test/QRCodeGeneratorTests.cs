using Mekatrol.QRCode.Common;

namespace Mekatrol.QRCode.Test;

[TestClass]
public sealed class QRCodeGeneratorTests
{
    [TestMethod]
    public void EncodeTextCreatesVersionOneQRCodeForShortText()
    {
        var qrCode = QRCodeGenerator.EncodeText("HELLO WORLD", QRErrorCorrectionLevel.Low);

        Assert.AreEqual(1, qrCode.Version);
        Assert.AreEqual(21, qrCode.Size);
        Assert.IsTrue(qrCode.Mask is >= 0 and <= 7);
    }

    [TestMethod]
    public void GetModuleReturnsFalseForOutOfBoundsCoordinates()
    {
        var qrCode = QRCodeGenerator.EncodeText("HELLO WORLD", QRErrorCorrectionLevel.Low);

        Assert.IsFalse(qrCode.GetModule(-1, 0));
        Assert.IsFalse(qrCode.GetModule(0, -1));
        Assert.IsFalse(qrCode.GetModule(qrCode.Size, 0));
        Assert.IsFalse(qrCode.GetModule(0, qrCode.Size));
    }

    [TestMethod]
    public void FinderPatternTopLeftIsDrawn()
    {
        var qrCode = QRCodeGenerator.EncodeText("HELLO WORLD", QRErrorCorrectionLevel.Low);

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
            QRCodeGenerator.EncodeSegments([segment], QRErrorCorrectionLevel.High, 1, 1, -1, false));
    }
}
