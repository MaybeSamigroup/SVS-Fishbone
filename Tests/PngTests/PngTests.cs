using System;
using System.Linq;
using Xunit;

namespace Fishbone.Tests
{
    public class PngTests
    {
        [Fact]
        public void ImplantAndExtract_Roundtrip()
        {
            var payload = new byte[] { 1, 2, 3, 4, 5 };
            var png = Fishbone.Encode.Implant(payload);
            var extracted = Fishbone.Decode.Extract(png);
            Assert.Equal(payload, extracted);
        }

        [Fact]
        public void ImplantIntoExistingPng_ExtractsLatest()
        {
            var first = new byte[] { 10, 20 };
            var second = new byte[] { 30, 40, 50 };
            var png = Fishbone.Encode.Implant(first);
            var png2 = Fishbone.Encode.Implant(png, second);
            var extracted = Fishbone.Decode.Extract(png2);
            Assert.Equal(second, extracted);
        }
    }
}
