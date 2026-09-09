using System.IO;
using System.Reflection;
using Xunit;

namespace Fishbone
{
    public class PngTests
    {
        [Fact]
        public void ImplantAndExtract_Roundtrip()
        {
            var payload = new byte[] { 1, 2, 3, 4, 5 };
            var png = Encode.Implant(payload);
            var extracted = Decode.Extract(png);
            Assert.Equal(payload, extracted);
        }

        [Fact]
        public void ImplantIntoExistingPng_ExtractsLatest()
        {
            var first = new byte[] { 10, 20 };
            var second = new byte[] { 30, 40, 50 };
            var png = Encode.Implant(first);
            var png2 = Encode.Implant(png, second);
            var extracted = Decode.Extract(png2);
            Assert.Equal(second, extracted);
        }
        
        [Fact]
        public void CrcMatchesOriginal()
        {
            foreach(var chunk in PNG.ReadChunks(new BinaryReader(
                Assembly.GetExecutingAssembly().GetManifestResourceStream("AlphaSample.png")
            ).ReadBytes(156)[8..], out var _))
            {
                Assert.Equal(chunk.CRC32, PNG.CRC32([
                    chunk.Name.Item1, chunk.Name.Item2, chunk.Name.Item3, chunk.Name.Item4, ..chunk.Data]));
            }
        }
    }
}
