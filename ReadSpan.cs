using System;
using System.Text;
using System.Linq;
using System.Buffers.Binary;
using System.Collections.Generic;
using MagicBytes = (byte, byte, byte, byte, byte, byte, byte, byte);

namespace CoastalSmell
{
    public delegate T ReadSpan<T>(Span<byte> input, out Span<byte> output);

    public static class ReadSpans
    {
        public static ReadSpan<IEnumerable<T>> Identity<T>() =>
            (Span<byte> input, out Span<byte> output) => (output = input) switch { _ => [] };

        public static ReadSpan<IEnumerable<T>> Lift<T>(ReadSpan<T> f) =>
            (Span<byte> input, out Span<byte> output) => [f(input, out output)];

        public static ReadSpan<IEnumerable<T>> Plus<T>(ReadSpan<IEnumerable<T>> f, ReadSpan<IEnumerable<T>> g) =>
            (Span<byte> input, out Span<byte> output) => [.. f(input, out output), .. g(output, out output)];

        public static T[] ReadArrayBE<T>(this ReadSpan<T> accumulate, Span<byte> input, out Span<byte> output) =>
            Enumerable.Repeat(Lift(accumulate), ReadBE.Int(input, out output))
                .Aggregate(Identity<T>(), Plus).Invoke(output, out output).ToArray();
 
        public static T[] ReadArrayLE<T>(this ReadSpan<T> accumulate, Span<byte> input, out Span<byte> output) =>
            Enumerable.Repeat(Lift(accumulate), ReadLE.Int(input, out output))
                .Aggregate(Identity<T>(), Plus).Invoke(output, out output).ToArray();

        public static ReadSpan<string> CString = (Span<byte> input, out Span<byte> output) =>
            Encoding.UTF8.GetString(ReadBytes(input.IndexOf((byte)0) + 1)(input, out output));

        public static ReadSpan<byte[]> ReadBytes(int length) =>
            length switch
            {
                0 => (Span<byte> input, out Span<byte> output) => (output = input) switch { _ => [] },
                _ => (Span<byte> input, out Span<byte> output) => (output = input.Slice(length)) switch { _ => input[0 .. length].ToArray() }
            };

        public static ReadSpan<MagicBytes> ReadMagicBytes = (input, out output) =>
        (output = input.Slice(8)) switch {
            _ => (input[0], input[1], input[2], input[3], input[4], input[5], input[6], input[7])
        };
    }
    public static class ReadLE {
 
        public static ReadSpan<ushort> Ushort = (Span<byte> input, out Span<byte> output) =>
            (output = input.Slice(2)) switch { _ => BinaryPrimitives.ReadUInt16LittleEndian(input[0..2]) };

        public static ReadSpan<uint> Uint = (Span<byte> input, out Span<byte> output) =>
            (output = input.Slice(4)) switch { _ => BinaryPrimitives.ReadUInt32LittleEndian(input[0..4]) };

        public static ReadSpan<int> Int = (Span<byte> input, out Span<byte> output) =>
            (output = input.Slice(4)) switch { _ => BinaryPrimitives.ReadInt32LittleEndian(input[0..4]) };

        public static ReadSpan<long> Long = (Span<byte> input, out Span<byte> output) =>
            (output = input.Slice(8)) switch { _ => BinaryPrimitives.ReadInt64LittleEndian(input[0..8]) };
    }

    public static class ReadBE {
        public static ReadSpan<ushort> Ushort = (Span<byte> input, out Span<byte> output) =>
            (output = input.Slice(2)) switch { _ => BinaryPrimitives.ReadUInt16BigEndian(input[0..2]) };

        public static ReadSpan<uint> Uint = (Span<byte> input, out Span<byte> output) =>
            (output = input.Slice(4)) switch { _ => BinaryPrimitives.ReadUInt32BigEndian(input[0..4]) };

        public static ReadSpan<int> Int = (Span<byte> input, out Span<byte> output) =>
            (output = input.Slice(4)) switch { _ => BinaryPrimitives.ReadInt32BigEndian(input[0..4]) };

        public static ReadSpan<long> Long = (Span<byte> input, out Span<byte> output) =>
            (output = input.Slice(8)) switch { _ => BinaryPrimitives.ReadInt64BigEndian(input[0..8]) };
    }
}