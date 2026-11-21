namespace LoadingScreen.Model;

public static class ImageHeader {
  public const int MaxSide = 8192;
  public const long MaxPixels = 4096L * 4096L;

  public static bool IsAllowed(int width, int height) {
    return width > 0 && height > 0 && width <= MaxSide && height <= MaxSide && (long) width * height <= MaxPixels;
  }

  public static bool TryRead(byte[] data, out int width, out int height) {
    width = 0;
    height = 0;

    if (data == null || data.Length < 24) {
      return false;
    }

    if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
        && data[12] == 0x49 && data[13] == 0x48 && data[14] == 0x44 && data[15] == 0x52) {
      width = ReadInt32BigEndian(data, 16);
      height = ReadInt32BigEndian(data, 20);
      return width > 0 && height > 0;
    }

    if (data[0] == 0xFF && data[1] == 0xD8) {
      return TryReadJpeg(data, out width, out height);
    }

    return false;
  }

  static bool TryReadJpeg(byte[] data, out int width, out int height) {
    width = 0;
    height = 0;
    int position = 2;

    while (position + 4 <= data.Length) {
      if (data[position] != 0xFF) {
        return false;
      }

      byte marker = data[position + 1];

      if (marker == 0xFF) {
        position++;
        continue;
      }

      if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) {
        position += 2;
        continue;
      }

      if (marker == 0xD9 || marker == 0xDA) {
        return false;
      }

      int length = (data[position + 2] << 8) | data[position + 3];

      if (length < 2) {
        return false;
      }

      bool isStartOfFrame = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;

      if (isStartOfFrame) {
        if (position + 9 > data.Length) {
          return false;
        }

        height = (data[position + 5] << 8) | data[position + 6];
        width = (data[position + 7] << 8) | data[position + 8];
        return width > 0 && height > 0;
      }

      position += 2 + length;
    }

    return false;
  }

  static int ReadInt32BigEndian(byte[] data, int offset) {
    return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
  }
}
