namespace LoadingScreen.Tests;

using System;

using LoadingScreen.Model;

using Xunit;

public class ImageHeaderTests {
  static byte[] Png(int width, int height) {
    byte[] data = new byte[33];
    byte[] header = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];
    Array.Copy(header, data, header.Length);
    WriteBigEndian(data, 16, width);
    WriteBigEndian(data, 20, height);
    return data;
  }

  static byte[] Jpeg(int width, int height) {
    return [
      0xFF, 0xD8,
      0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00,
      0xFF, 0xC4, 0x00, 0x03, 0x00,
      0xFF, 0xC0, 0x00, 0x11, 0x08, (byte) (height >> 8), (byte) height, (byte) (width >> 8), (byte) width,
      0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];
  }

  static void WriteBigEndian(byte[] data, int offset, int value) {
    data[offset] = (byte) (value >> 24);
    data[offset + 1] = (byte) (value >> 16);
    data[offset + 2] = (byte) (value >> 8);
    data[offset + 3] = (byte) value;
  }

  [Fact]
  public void ReadsPngSize() {
    Assert.True(ImageHeader.TryRead(Png(1920, 1080), out int width, out int height));
    Assert.Equal(1920, width);
    Assert.Equal(1080, height);
  }

  [Fact]
  public void ReadsJpegSizeAfterOtherSegments() {
    Assert.True(ImageHeader.TryRead(Jpeg(2560, 1440), out int width, out int height));
    Assert.Equal(2560, width);
    Assert.Equal(1440, height);
  }

  [Fact]
  public void RejectsUnknownAndTruncatedData() {
    Assert.False(ImageHeader.TryRead(null, out _, out _));
    Assert.False(ImageHeader.TryRead(new byte[100], out _, out _));
    Assert.False(ImageHeader.TryRead(Jpeg(10, 10)[..12], out _, out _));
  }

  [Theory]
  [InlineData(3840, 2160, true)]
  [InlineData(4096, 4096, true)]
  [InlineData(8192, 2048, true)]
  [InlineData(16384, 16384, false)]
  [InlineData(8193, 100, false)]
  [InlineData(0, 100, false)]
  public void LimitsDimensions(int width, int height, bool allowed) {
    Assert.Equal(allowed, ImageHeader.IsAllowed(width, height));
  }
}
