using System.Collections.Generic;
using System.Linq;

namespace Fishbone
{
    /// <summary>
    /// Helpers for converting 32-bit unsigned integers to/from network (big-endian) byte order.
    /// </summary>
    public static class NetworkOrderBytes
    {
        /// <summary>Convert a 32-bit unsigned integer to a 4-byte big-endian array.</summary>
        /// <param name="bytes">Value to convert.</param>
        /// <returns>4-byte big-endian representation.</returns>
        public static byte[] To(uint bytes) =>
            [(byte)(bytes >> 24), (byte)(bytes >> 16), (byte)(bytes >> 8), (byte)bytes];

        /// <summary>Read a 32-bit unsigned integer from 4 big-endian bytes.</summary>
        /// <param name="bytes">Enumerable of bytes (expects at least 4 bytes).</param>
        /// <returns>Parsed 32-bit unsigned integer.</returns>
        public static uint From(IEnumerable<byte> bytes) =>
            ((uint)bytes.ElementAt(0) << 24) | ((uint)bytes.ElementAt(1) << 16) | ((uint)bytes.ElementAt(2) << 8) | bytes.ElementAt(3);
    }
    /// <summary>
    /// Purpose-specific portable network graphics encoder.
    /// </summary>
    public static class Encode
    {
        /// <summary>Compute CRC32 for the provided data (standard IEEE 802.3 polynomial).</summary>
        /// <param name="bytes">Data to checksum.</param>
        /// <returns>CRC32 value.</returns>
        public static uint CRC32(IEnumerable<byte> bytes) =>
            bytes.Aggregate(0xFFFFFFFFU, (crc32, value) => TABLE[(crc32 ^ value) & 0xff] ^ (crc32 >> 8)) ^ 0xFFFFFFFFU;

        /// <summary>Embed a custom `fsBN` chunk into an existing PNG byte sequence.</summary>
        /// <param name="pngData">Original PNG bytes.</param>
        /// <param name="bytes">Payload bytes to embed inside the `fsBN` chunk.</param>
        /// <returns>New PNG bytes containing the embedded chunk.</returns>
        public static byte[] Implant(IEnumerable<byte> pngData, byte[] bytes) =>
            [.. pngData.Take(8), .. ProcessSize(pngData.Skip(8), ToChunk([(byte)'f', (byte)'s', (byte)'B', (byte)'N'], bytes))];

        /// <summary>Create a minimal PNG file containing a single `fsBN` chunk with the given payload.</summary>
        /// <param name="data">Payload bytes to place into the `fsBN` chunk.</param>
        /// <returns>PNG file bytes containing the payload.</returns>
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
        /// <summary>Extract the payload bytes from an `fsBN` chunk inside a PNG file.</summary>
        /// <param name="bytes">PNG file bytes (or null).</param>
        /// <returns>Payload bytes if present; otherwise an empty array.</returns>
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