using System;
using System.IO;
using System.Text.Json;
using System.Text.Unicode;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using UnityEngine;

namespace CoastalSmell
{
    public static partial class Util
    {
        internal static readonly JsonSerializerOptions JsonOpts = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            NumberHandling =
                JsonNumberHandling.WriteAsString |
                JsonNumberHandling.AllowReadingFromString |
                JsonNumberHandling.AllowNamedFloatingPointLiterals,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };
    }
    public struct Float2 : IEquatable<Float2>
    {
        public float x { get; set; }
        public float y { get; set; }
        public Float2(float v1, float v2) =>
           (x, y) = (v1, v2);
        public static implicit operator Float2(Vector2 s) => new(s.x, s.y);
        public static implicit operator Vector2(Float2 s) => new(s.x, s.y);
        public bool Equals(Float2 s) =>
          (x, y) == (s.x, s.y);
    }
    public struct Float3 : IEquatable<Float3>
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public Float3(float v1, float v2, float v3) =>
           (x, y, z) = (v1, v2, v3);
        public static implicit operator Float3(Vector3 s) => new(s.x, s.y, s.z);
        public static implicit operator Vector3(Float3 s) => new(s.x, s.y, s.z);
        public static implicit operator Float3(Color s) => new(s.r, s.g, s.b);
        public static implicit operator Color(Float3 s) => new(s.x, s.y, s.z);
        public bool Equals(Float3 s) =>
          (x, y, z) == (s.x, s.y, s.z);
    }
    public struct Float4 : IEquatable<Float4>
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public float w { get; set; }
        public Float4(float v1, float v2, float v3, float v4) =>
          (x, y, z, w) = (v1, v2, v3, v4);
        public static implicit operator Float4(Vector4 s) => new(s.x, s.y, s.z, s.w);
        public static implicit operator Vector4(Float4 s) => new(s.x, s.y, s.z, s.w);
        public static implicit operator Float4(Color s) => new(s.r, s.g, s.b, s.a);
        public static implicit operator Color(Float4 s) => new(s.x, s.y, s.z, s.w);
        public bool Equals(Float4 s) =>
          (x, y, z, w) == (s.x, s.y, s.z, s.w);
    }
    public static class Json<T>
    {
        public static Func<Stream, T> Deserialize =
            (stream) => JsonSerializer.Deserialize<T>(stream, Util.JsonOpts);
        public static Action<T, Stream> Serialize =
            (data, stream) => JsonSerializer.Serialize(stream, data, Util.JsonOpts);
    }

}