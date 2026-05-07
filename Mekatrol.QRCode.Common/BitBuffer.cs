namespace Mekatrol.QRCode.Common;

public sealed class BitBuffer
{
    // The largest bit count accepted by AppendBits because the value argument is a 32-bit signed integer.
    private const int _maximumAppendBitLength = 31;

    // One is the additive offset used to walk from the highest requested bit down to the lowest bit.
    private const int _bitIndexOffset = 1;

    // The exception text is reused for overflow paths so callers receive a consistent capacity failure message.
    private const string _maximumLengthReachedMessage = "Maximum length reached";

    // The exception text identifies invalid bit append requests without exposing internal validation details.
    private const string _valueOutOfRangeMessage = "Value out of range";

    private readonly List<bool> _data = [];

    public int BitLength => _data.Count;

    public int GetBit(int index)
    {
        if (index < 0 || index >= _data.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _data[index] ? 1 : 0;
    }

    public void AppendBits(int value, int length)
    {
        if (length < 0 || length > _maximumAppendBitLength || (value >>> length) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), _valueOutOfRangeMessage);
        }

        if (int.MaxValue - _data.Count < length)
        {
            throw new InvalidOperationException(_maximumLengthReachedMessage);
        }

        for (var i = length - _bitIndexOffset; i >= 0; i--)
        {
            _data.Add(QRCodeGenerator.GetBit(value, i));
        }
    }

    public void AppendData(BitBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (int.MaxValue - _data.Count < buffer._data.Count)
        {
            throw new InvalidOperationException(_maximumLengthReachedMessage);
        }

        _data.AddRange(buffer._data);
    }

    public BitBuffer Copy()
    {
        var result = new BitBuffer();
        result._data.AddRange(_data);
        return result;
    }
}
