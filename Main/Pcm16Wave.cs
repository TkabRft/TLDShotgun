using System.Buffers.Binary;

namespace ModBuild;

internal sealed class Pcm16Wave
{
    internal int Channels { get; private init; }
    internal int SampleRate { get; private init; }
    internal int Frames { get; private init; }
    internal float[] Samples { get; private init; } = Array.Empty<float>();

    internal static Pcm16Wave Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 || !bytes[..4].SequenceEqual("RIFF"u8) || !bytes.Slice(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Expected RIFF/WAVE");
        if ((long)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4)) + 8 != bytes.Length)
            throw new InvalidDataException("RIFF size does not match resource length");
        int fmt = -1, data = -1, dataLength = 0;
        for (int position = 12; position < bytes.Length;)
        {
            if (bytes.Length - position < 8) throw new InvalidDataException("Truncated RIFF chunk header");
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(position + 4, 4));
            int start = position + 8;
            long end = (long)start + length;
            long next = end + (length & 1);
            if (next > bytes.Length) throw new InvalidDataException("RIFF chunk exceeds resource or lacks alignment padding");
            var kind = bytes.Slice(position, 4);
            if (kind.SequenceEqual("fmt "u8))
            {
                if (fmt >= 0 || length < 16) throw new InvalidDataException("Duplicate or short fmt chunk");
                fmt = start;
            }
            else if (kind.SequenceEqual("data"u8))
            {
                if (data >= 0) throw new InvalidDataException("Duplicate data chunk");
                data = start; dataLength = checked((int)length);
            }
            position = checked((int)next);
        }
        if (fmt < 0 || data < 0) throw new InvalidDataException("Missing fmt or data chunk");
        ushort codec = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(fmt, 2));
        int channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(fmt + 2, 2));
        uint rate = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(fmt + 4, 4));
        uint byteRate = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(fmt + 8, 4));
        int alignment = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(fmt + 12, 2));
        int bits = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(fmt + 14, 2));
        if (codec != 1 || bits != 16) throw new InvalidDataException("Only signed little-endian PCM16 WAV is supported by the shotgun loader");
        if (channels is not (1 or 2) || rate == 0 || rate > int.MaxValue)
            throw new InvalidDataException("Invalid PCM channel count or sample rate");
        if (alignment != channels * 2 || byteRate != (long)rate * alignment)
            throw new InvalidDataException("Invalid PCM block alignment or byte rate");
        if (dataLength == 0 || dataLength % alignment != 0)
            throw new InvalidDataException("PCM data must contain complete nonempty channel frames");
        var samples = new float[dataLength / 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(data + i * 2, 2)) / 32768f;
        return new Pcm16Wave { Channels = channels, SampleRate = (int)rate, Frames = dataLength / alignment, Samples = samples };
    }
}
