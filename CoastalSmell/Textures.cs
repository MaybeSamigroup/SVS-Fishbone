using System;
using UnityEngine;
using DdsBaseHeader = (
    uint Flags,
    uint Height,
    uint Width,
    uint PitchOrLinierSize,
    uint Depth,
    uint MipMapCount,
    (uint, uint, uint, uint, uint, uint, uint, uint, uint, uint, uint) Reserved1,
    (uint Size, uint Flags, uint FourCC, uint RGBBitCount, uint RBitMask, uint GBitMask, uint BBitMask, uint ABitMask) PixelFormat,
    uint Cpas,
    uint Caps2,
    uint Caps3,
    uint Caps4,
    uint Reserved2
);
using DdsDx10Header = (
    uint Format,
    uint Dimension,
    uint MiscFlags,
    uint ArraySize,
    uint miscFlags2
);

namespace CoastalSmell
{
    public static class TextureExtension
    {
        public static Texture2D ToTexture2D(this byte[] input) =>
            ReadSpans.ReadMagicBytes(input, out var output) switch
            {
                // PNG
                (0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A) =>
                    new Texture2D(0, 0).With(t2d => ImageConversion.LoadImage(t2d, input)),
                // DDS
                (0x44, 0x44, 0x53, 0x20, 0x7C, 0x00, 0x00, 0x00) => DDS.ToTexture2D(output),
                _ => null                    
            };
    }

    file static class DDS {

        static T NotifyNotSupported<T>(string msg)
        {
            Plugin.Instance.Log.LogInfo(msg);
            return default;
        }

        internal static Texture2D ToTexture2D(Span<byte> input) => ReadBaseHeader(input, out var output) switch
        {
            var baseHeader when (baseHeader.Flags & 0x1000u) is 0 => NotifyNotSupported<Texture2D>($"InvalidFlag:{baseHeader.Flags:b}"),
            var baseHeader when (baseHeader.PixelFormat.Flags & 0x4u, baseHeader.PixelFormat.FourCC) is not (04u, FORMAT_DX10) =>
                ToTextureFormat(baseHeader.PixelFormat.FourCC, out var format) ? ToTexture2D(baseHeader, format, [..output]) : null,
            var baseHeader =>
                ToTextureFormat(ReadDx10Header(output, out output), out var format) ? ToTexture2D(baseHeader, format, [..output]) : null
        };

        static Texture2D ToTexture2D(DdsBaseHeader header, TextureFormat format, byte[] rawData) =>
            new Texture2D((int)header.Width, (int)header.Height, format,
                (header.Flags & 0x20000, header.MipMapCount) is not (0, _) or (_, 0))
                .With(t2d => t2d.LoadRawTextureData(rawData)).With(t2d => t2d.Apply());


        const uint FORMAT_DXT1 = 0x31_54_58_44u;
        const uint FORMAT_DXT5 = 0x35_54_58_44u;
        const uint FORMAT_DX10 = 0x30_31_58_44u;
        static bool ToTextureFormat(uint fourCC, out TextureFormat format) =>
            (format = fourCC switch
            {
                FORMAT_DXT1 => TextureFormat.DXT1,
                FORMAT_DXT5 => TextureFormat.DXT5,
                _ => 0

            }) is not 0;

        const uint DXGI_BC4_TYPELESS = 79;
        const uint DXGI_BC4_UNORM = 80;
        const uint DXGI_BC4_SNORM = 81;
        const uint DXGI_BC5_TYPELESS = 82;
        const uint DXGI_BC5_UNORM = 83;
        const uint DXGI_BC5_SNORM = 84;
        const uint DXGI_BC6H_TYPELESS = 94;
        const uint DXGI_BC6H_UF16 = 95;
        const uint DXGI_BC6H_SF16 = 96;
        const uint DXGI_BC7_TYPELESS = 97;
        const uint DXGI_BC7_UNORM = 98;
        const uint DXGI_BC7_UNORM_SRGB = 99;
        static bool ToTextureFormat(DdsDx10Header header, out TextureFormat format) =>
            (format = header.Format switch
            {
                DXGI_BC4_TYPELESS or
                DXGI_BC4_UNORM or
                DXGI_BC4_SNORM => TextureFormat.BC4,
                DXGI_BC5_TYPELESS or
                DXGI_BC5_UNORM or
                DXGI_BC5_SNORM => TextureFormat.BC5,
                DXGI_BC6H_TYPELESS or
                DXGI_BC6H_UF16 or
                DXGI_BC6H_SF16 => TextureFormat.BC6H,
                DXGI_BC7_TYPELESS or
                DXGI_BC7_UNORM or
                DXGI_BC7_UNORM_SRGB => TextureFormat.BC7,
                 _ => 0
            }) is not 0;

        static ReadSpan<DdsBaseHeader> ReadBaseHeader = (input, out output) => (
            ReadLE.Uint(input, out output), //Flags
            ReadLE.Uint(output, out output), //Height
            ReadLE.Uint(output, out output), //Width
            ReadLE.Uint(output, out output), //PitchOrLinierSize
            ReadLE.Uint(output, out output), //Depth
            ReadLE.Uint(output, out output), //MipMapCount
            (
                ReadLE.Uint(output, out output),
                ReadLE.Uint(output, out output),
                ReadLE.Uint(output, out output),
                ReadLE.Uint(output, out output),
                ReadLE.Uint(output, out output),
                ReadLE.Uint(output, out output),
                ReadLE.Uint(output, out output),
                ReadLE.Uint(output, out output),
                ReadLE.Uint(output, out output),
                ReadLE.Uint(output, out output),
                ReadLE.Uint(output, out output)
            ), //Reserved1
            (
                ReadLE.Uint(output, out output), //Size
                ReadLE.Uint(output, out output), //Flags
                ReadLE.Uint(output, out output), //FourCC
                ReadLE.Uint(output, out output), //RGBBitCount
                ReadLE.Uint(output, out output), //RBitMask
                ReadLE.Uint(output, out output), //GBitMask
                ReadLE.Uint(output, out output), //BBitMask
                ReadLE.Uint(output, out output)  //ABitMask
            ), // PixelFormat
            ReadLE.Uint(output, out output), //Cap1
            ReadLE.Uint(output, out output), //Cap2
            ReadLE.Uint(output, out output), //Cap3
            ReadLE.Uint(output, out output), //Cap4
            ReadLE.Uint(output, out output) //Reserved2
        );

        static ReadSpan<DdsDx10Header> ReadDx10Header = (input, out output) => (
            ReadLE.Uint(input, out output),
            ReadLE.Uint(output, out output),
            ReadLE.Uint(output, out output),
            ReadLE.Uint(output, out output),
            ReadLE.Uint(output, out output)
        );
    }
}