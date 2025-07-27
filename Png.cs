using System.Collections.Generic;
using System.Linq;

namespace Fishbone
{
    /// <summary>
    /// Purpose-specific portable network graphics encoder.
    /// </summary>
    public static partial class Encode
    {
        public static uint CRC32(this IEnumerable<byte> values) =>
            values.Aggregate(0xFFFFFFFFU, (crc32, value) => TABLE[(crc32 ^ value) & 0xff] ^ (crc32 >> 8)) ^ 0xFFFFFFFFU;

        public static byte[] ToNetworkOrderBytes(this uint value) =>
            [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

        public static byte[] Implant(this IEnumerable<byte> pngData, byte[] data) =>
            [..pngData.Take(8), ..pngData.Skip(8).ProcessSize(ToChunk([(byte)'f', (byte)'s', (byte)'B', (byte)'N'], data))];

        public static byte[] Implant(this byte[] data) =>
            [
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                ..ToChunk([(byte)'I', (byte)'H', (byte)'D', (byte)'R'], [0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0]),
                ..ToChunk([(byte)'I', (byte)'D', (byte)'A', (byte)'T'], []),
                ..ToChunk([(byte)'f', (byte)'s', (byte)'B', (byte)'N'], data),
                ..ToChunk([(byte)'I', (byte)'E', (byte)'N', (byte)'D'], [])
            ];

        private static readonly uint[] TABLE = [.. Enumerable.Range(0, 256)
            .Select(i => (uint)i).Select(i => Enumerable.Range(0, 8).Aggregate(i, (i, _) => (i & 1) == 1 ? (0xEDB88320U ^ (i >> 1)) : (i >> 1)))];

        private static IEnumerable<byte> Suffix(this IEnumerable<byte> values) =>
            values.Concat(CRC32(values).ToNetworkOrderBytes());

        private static IEnumerable<byte> ToChunk(IEnumerable<byte> name, IEnumerable<byte> values) =>
            ((uint)values.Count()).ToNetworkOrderBytes().Concat(Enumerable.Concat(name, values).Suffix());

        private static IEnumerable<byte> ProcessName(this uint size, IEnumerable<byte> image, IEnumerable<byte> data) =>
            (image.ElementAt(4), image.ElementAt(5), image.ElementAt(6), image.ElementAt(7)) switch
            {
                ((byte)'I', (byte)'E', (byte)'N', (byte)'D') => data.Concat(image),
                ((byte)'f', (byte)'s', (byte)'B', (byte)'N') => data.Concat(image.Skip((int)size + 12)),
                _ => image.Take((int)size + 12).Concat(image.Skip((int)size + 12).ProcessSize(data))
            };

        private static IEnumerable<byte> ProcessSize(this IEnumerable<byte> image, IEnumerable<byte> data) =>
            image.FromNetworkOrderBytes().ProcessName(image, data);
    }
    /// <summary>
    /// Purpose-specific portable network graphics decoder.
    /// </summary>
    public static partial class Decode
    {
        public static uint FromNetworkOrderBytes(this IEnumerable<byte> values) =>
            ((uint)values.ElementAt(0) << 24) | ((uint)values.ElementAt(1) << 16) | ((uint)values.ElementAt(2) << 8) | values.ElementAt(3);

        public static byte[] Extract(this IEnumerable<byte> values) =>
            values?.Skip(8)?.ProcessSize()?.ToArray() ?? [];
        private static IEnumerable<byte> ProcessSize(this IEnumerable<byte> image) =>
            image.Take(4).FromNetworkOrderBytes().ProcessName(image.Skip(4));

        private static IEnumerable<byte> ProcessName(this uint size, IEnumerable<byte> image) =>
            (image.ElementAt(0), image.ElementAt(1), image.ElementAt(2), image.ElementAt(3)) switch
            {
                ((byte)'I', (byte)'E', (byte)'N', (byte)'D') => [],
                ((byte)'f', (byte)'s', (byte)'B', (byte)'N') => image.Skip(4).Take((int)size),
                _ => image.Skip((int)size + 8).ProcessSize()
            };
    }
}