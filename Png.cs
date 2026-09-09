using System;
using System.Linq;
using System.Collections.Generic;
using CoastalSmell;
using Chunk = (uint Size, (byte, byte, byte, byte) Name, byte[] Data, uint CRC32);

namespace Fishbone
{
    /// <summary>
    /// Purpose-specific portable network graphics encoder.
    /// </summary>
    public static class Encode
    {
        /// <summary>Embed a custom `fsBN` chunk into an existing PNG byte sequence.</summary>
        /// <param name="pngData">Original PNG bytes.</param>
        /// <param name="bytes">Payload bytes to embed inside the `fsBN` chunk.</param>
        /// <returns>New PNG bytes containing the embedded chunk.</returns>
        public static byte[] Implant(byte[] pngData, byte[] bytes) => [
            .. pngData[0..8],
            .. PNG.ReadChunks(pngData[8..], out _)
                .Where(chunk => chunk.Name is not ((byte)'f', (byte)'s', (byte)'B', (byte)'N'))
                .Where(chunk => chunk.Name is not ((byte)'I', (byte)'E', (byte)'N', (byte)'D'))
                .SelectMany(chunk => chunk.ToBytes()),
            .. Chunk(((byte)'f', (byte)'s', (byte)'B', (byte)'N'), bytes).ToBytes(),
            .. Chunk(((byte)'I', (byte)'E', (byte)'N', (byte)'D'), []).ToBytes()
        ];

        /// <summary>Create a minimal PNG file containing a single `fsBN` chunk with the given payload.</summary>
        /// <param name="data">Payload bytes to place into the `fsBN` chunk.</param>
        /// <returns>PNG file bytes containing the payload.</returns>
        public static byte[] Implant(byte[] data) =>
            [
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                .. Chunk(((byte)'I', (byte)'H', (byte)'D', (byte)'R'), [0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0]).ToBytes(),
                .. Chunk(((byte)'I', (byte)'D', (byte)'A', (byte)'T'), []).ToBytes(),
                .. Chunk(((byte)'f', (byte)'s', (byte)'B', (byte)'N'), data).ToBytes(),
                .. Chunk(((byte)'I', (byte)'E', (byte)'N', (byte)'D'), []).ToBytes()
            ];

        static Chunk Chunk((byte, byte, byte, byte) name, byte[] data) =>
            ((uint)data.Length, name, data, PNG.CRC32([name.Item1, name.Item2, name.Item3, name.Item4, ..data]));

    }
    /// <summary>
    /// Purpose-specific portable network graphics decoder.
    /// </summary>
    public static class Decode
    {
        /// <summary>Extract the payload bytes from an `fsBN` chunk inside a PNG file.</summary>
        /// <param name="bytes">PNG file bytes (or null).</param>
        /// <returns>Payload bytes if present; otherwise an empty array.</returns>
        public static byte[] Extract(byte[] bytes) =>
            ReadSpans.ReadMagicBytes(bytes, out var output) is not
                (0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A) ? [] :
                    PNG.ReadChunks(output, out _)
                        .Where(chunk => chunk.Name is ((byte)'f' , (byte)'s', (byte)'B', (byte)'N'))
                        .Select(chunk => chunk.Data).FirstOrDefault([]);
    }
    
    internal static class PNG
    {
        internal static ReadSpan<IEnumerable<Chunk>> ReadChunks = (Span<byte> input, out Span<byte> output) =>
            ReadChunk(input, out output) switch
            {
                var chunk => chunk is (_, ((byte)'I', (byte)'E', (byte)'N', (byte)'D'), _, _) ? [chunk] : [chunk, .. ReadChunks(output, out output)]
            };

        static ReadSpan<Chunk> ReadChunk = (Span<byte> input, out Span<byte> output) =>
            ReadBE.Uint(input, out output) switch
            {
                var size => (size, ReadName(output, out output), ReadSpans.ReadBytes((int)size)(output, out output), ReadBE.Uint(output, out output))
            };

        static ReadSpan<(byte, byte, byte, byte)> ReadName = (Span<byte> input, out Span<byte> output) =>
            (output = input.Slice(4)) switch { _ => (input[0], input[1], input[2], input[3]) };

        static byte[] ToBytes(uint bytes) =>
            [(byte)(bytes >> 24), (byte)(bytes >> 16), (byte)(bytes >> 8), (byte)bytes];

        internal static IEnumerable<byte> ToBytes(this Chunk chunk) => [
            .. ToBytes((uint)chunk.Data.Length), chunk.Name.Item1, chunk.Name.Item2, chunk.Name.Item3, chunk.Name.Item4, .. chunk.Data, .. ToBytes(chunk.CRC32)
        ];

        private static readonly uint[] CRC32_TABLE = [.. Enumerable.Range(0, 256)
            .Select(i => (uint)i).Select(i => Enumerable.Range(0, 8).Aggregate(i, (i, _) => (i & 1) == 1 ? (0xEDB88320U ^ (i >> 1)) : (i >> 1)))];

        static ReadSpan<Func<uint, uint>> ReadCRC32 = (input, out output) =>
            (output = input.Slice(1)) switch { _ => input[0] switch { var value => crc32 => CRC32_TABLE[(crc32 ^ value) & 0xff] ^ (crc32 >> 8) } };

        internal static uint CRC32(Span<byte> data) =>
            Enumerable.Repeat(ReadSpans.Lift(ReadCRC32), data.Length)
                .Aggregate(ReadSpans.Identity<Func<uint, uint>>(), ReadSpans.Plus).Invoke(data, out var _)
                .Aggregate(0xFFFFFFFFu, (crc32, f) => f(crc32)) ^ 0xFFFFFFFFu;
    }
}