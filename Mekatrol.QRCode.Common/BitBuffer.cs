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

    // The exception text identifies invalid initial buffer capacity requests.
    private const string _capacityOutOfRangeMessage = "Capacity out of range";

    // One byte contains 8 bits; the buffer stores bits packed most-significant-bit first for direct QR codeword export.
    private const int _bitsPerByte = 8;

    // The right shift from a bit index to its containing byte index.
    private const int _bitIndexToByteShift = 3;

    // The mask for a bit index within one byte.
    private const int _bitIndexInByteMask = 7;

    // The highest bit position in a byte, used when packing bits in most-significant-bit order.
    private const int _highestBitIndexInByte = 7;

    // This is the largest whole-byte input whose bit length can fit in an Int32-backed BitBuffer.
    private const int _maximumByteLength = int.MaxValue / _bitsPerByte;

    private readonly List<byte> _data = [];
    private int _bitLength;

    public BitBuffer()
    {
    }

    public BitBuffer(int bitCapacity)
    {
        if (bitCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCapacity), _capacityOutOfRangeMessage);
        }

        _data.Capacity = BitsToBytes(bitCapacity);
    }

    public int BitLength => _bitLength;

    public int GetBit(int index)
    {
        if (index < 0 || index >= _bitLength)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return (_data[index >>> _bitIndexToByteShift] >>> (_highestBitIndexInByte - (index & _bitIndexInByteMask))) & 1;
    }

    public void AppendBits(int value, int length)
    {
        if (length < 0 || length > _maximumAppendBitLength || (value >>> length) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), _valueOutOfRangeMessage);
        }

        if (int.MaxValue - _bitLength < length)
        {
            throw new InvalidOperationException(_maximumLengthReachedMessage);
        }

        if (length == _bitsPerByte)
        {
            AppendByte((byte)value);
            return;
        }

        for (var i = length - _bitIndexOffset; i >= 0; i--)
        {
            AppendBit(QRCodeGenerator.GetBit(value, i));
        }
    }

    public void AppendByte(byte value)
    {
        if (int.MaxValue - _bitLength < _bitsPerByte)
        {
            throw new InvalidOperationException(_maximumLengthReachedMessage);
        }

        var offset = _bitLength & _bitIndexInByteMask;
        if (offset == 0)
        {
            _data.Add(value);
        }
        else
        {
            _data[^1] |= (byte)(value >>> offset);
            _data.Add((byte)(value << (_bitsPerByte - offset)));
        }

        _bitLength += _bitsPerByte;
    }

    public void AppendData(BitBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (int.MaxValue - _bitLength < buffer._bitLength)
        {
            throw new InvalidOperationException(_maximumLengthReachedMessage);
        }

        if (((_bitLength | buffer._bitLength) & _bitIndexInByteMask) == 0)
        {
            _data.AddRange(buffer._data);
            _bitLength += buffer._bitLength;
            return;
        }

        var wholeByteCount = buffer._bitLength >>> _bitIndexToByteShift;
        for (var i = 0; i < wholeByteCount; i++)
        {
            AppendByte(buffer._data[i]);
        }

        for (var i = wholeByteCount * _bitsPerByte; i < buffer._bitLength; i++)
        {
            AppendBit(buffer.GetBit(i) != 0);
        }
    }

    public BitBuffer Copy()
    {
        var result = new BitBuffer();
        result._data.AddRange(_data);
        result._bitLength = _bitLength;
        return result;
    }

    public byte[] ToByteArray()
    {
        var result = _data.ToArray();
        if ((_bitLength & _bitIndexInByteMask) != 0)
        {
            var finalByteMask = byte.MaxValue << (_bitsPerByte - (_bitLength & _bitIndexInByteMask));
            result[^1] &= (byte)finalByteMask;
        }

        return result;
    }

    private void AppendBit(bool bit)
    {
        var offset = _bitLength & _bitIndexInByteMask;
        if (offset == 0)
        {
            _data.Add(0);
        }

        if (bit)
        {
            _data[^1] |= (byte)(1 << (_highestBitIndexInByte - offset));
        }

        _bitLength++;
    }

    internal static BitBuffer FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > _maximumByteLength)
        {
            throw new InvalidOperationException(_maximumLengthReachedMessage);
        }

        var result = new BitBuffer(bytes.Length * _bitsPerByte);
        foreach (var value in bytes)
        {
            result._data.Add(value);
        }

        result._bitLength = bytes.Length * _bitsPerByte;
        return result;
    }

    private static int BitsToBytes(int bitCount)
    {
        return (bitCount + _bitIndexInByteMask) >>> _bitIndexToByteShift;
    }
}
