namespace Mekatrol.QRCode.Common;

public sealed class BitBuffer
{
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
        if (length < 0 || length > 31 || (value >>> length) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Value out of range");
        }

        if (int.MaxValue - _data.Count < length)
        {
            throw new InvalidOperationException("Maximum length reached");
        }

        for (var i = length - 1; i >= 0; i--)
        {
            _data.Add(QRCodeGenerator.GetBit(value, i));
        }
    }

    public void AppendData(BitBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (int.MaxValue - _data.Count < buffer._data.Count)
        {
            throw new InvalidOperationException("Maximum length reached");
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
