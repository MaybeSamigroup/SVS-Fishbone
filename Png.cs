using System.Collections.Generic;
using System.Linq;

namespace Fishbone
{
    public static class NetworkOrderBytes
    {
        public static byte[] To(uint bytes) =>
            [(byte)(bytes >> 24), (byte)(bytes >> 16), (byte)(bytes >> 8), (byte)bytes];
        public static uint From(IEnumerable<byte> bytes) =>
            ((uint)bytes.ElementAt(0) << 24) | ((uint)bytes.ElementAt(1) << 16) | ((uint)bytes.ElementAt(2) << 8) | bytes.ElementAt(3);
    }
    /// <summary>
    /// Purpose-specific portable network graphics encoder.
    /// </summary>
    public static class Encode
    {
        public static uint CRC32(IEnumerable<byte> bytes) =>
            bytes.Aggregate(0xFFFFFFFFU, (crc32, value) => TABLE[(crc32 ^ value) & 0xff] ^ (crc32 >> 8)) ^ 0xFFFFFFFFU;
        public static byte[] Implant(IEnumerable<byte> pngData, byte[] bytes) =>
            [..pngData.Take(8), ..ProcessSize(pngData.Skip(8), ToChunk([(byte)'f', (byte)'s', (byte)'B', (byte)'N'], bytes))];
        public static byte[] Implant(byte[] data) =>
            [
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                ..ToChunk([(byte)'I', (byte)'H', (byte)'D', (byte)'R'], [0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0]),
                ..ToChunk([(byte)'I', (byte)'D', (byte)'A', (byte)'T'], []),
                ..ToChunk([(byte)'f', (byte)'s', (byte)'B', (byte)'N'], data),
                ..ToChunk([(byte)'I', (byte)'E', (byte)'N', (byte)'D'], [])
            ];
        private static readonly uint[] TABLE = [.. Enumerable.Range(0, 256)
            .Select(i => (uint)i).Select(i => Enumerable.Range(0, 8).Aggregate(i, (i, _) => (i & 1) == 1 ? (0xEDB88320U ^ (i >> 1)) : (i >> 1)))];
        private static IEnumerable<byte> Suffix(IEnumerable<byte> values) =>
            values.Concat(NetworkOrderBytes.To(CRC32(values)));
        private static IEnumerable<byte> ToChunk(IEnumerable<byte> name, IEnumerable<byte> bytes) =>
            Suffix(NetworkOrderBytes.To((uint)bytes.Count()).Concat(Enumerable.Concat(name, bytes)));
        private static IEnumerable<byte> ProcessName(uint size, IEnumerable<byte> bytes, IEnumerable<byte> data) =>
            (bytes.ElementAt(4), bytes.ElementAt(5), bytes.ElementAt(6), bytes.ElementAt(7)) switch
            {
                ((byte)'I', (byte)'E', (byte)'N', (byte)'D') => data.Concat(bytes),
                ((byte)'f', (byte)'s', (byte)'B', (byte)'N') => data.Concat(bytes.Skip((int)size + 12)),
                _ => bytes.Take((int)size + 12).Concat(ProcessSize(bytes.Skip((int)size + 12), data))
            };
        private static IEnumerable<byte> ProcessSize(IEnumerable<byte> bytes, IEnumerable<byte> data) =>
            ProcessName(NetworkOrderBytes.From(bytes), bytes, data);
    }
    /// <summary>
    /// Purpose-specific portable network graphics decoder.
    /// </summary>
    public static class Decode
    {
         public static byte[] Extract(IEnumerable<byte> bytes) =>
            bytes == null ? [] : ProcessSize(bytes?.Skip(8))?.ToArray() ?? [];
        private static IEnumerable<byte> ProcessSize(IEnumerable<byte> bytes) =>
            ProcessName(NetworkOrderBytes.From(bytes.Take(4)), bytes.Skip(4));
        private static IEnumerable<byte> ProcessName(uint size, IEnumerable<byte> bytes) =>
            (bytes.ElementAt(0), bytes.ElementAt(1), bytes.ElementAt(2), bytes.ElementAt(3)) switch
            {
                ((byte)'I', (byte)'E', (byte)'N', (byte)'D') => [],
                ((byte)'f', (byte)'s', (byte)'B', (byte)'N') => bytes.Skip(4).Take((int)size),
                _ => ProcessSize(bytes.Skip((int)size + 8))
            };
    }
}