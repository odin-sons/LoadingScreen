namespace LoadingScreen.Tests;

using System.IO;

using LoadingScreen.Model;

using Xunit;

public class ImageFolderPathTests {
  static readonly string BepInExRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "BepInEx"));

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void EmptySettingUsesDefaultFolder(string setting) {
    Assert.Equal(
        Path.Combine(BepInExRoot, "config", "LoadingScreen"),
        ImageFolderPath.Resolve(setting, BepInExRoot));
  }

  [Theory]
  [InlineData("modpack/images")]
  [InlineData(@"modpack\images")]
  public void RelativeFolderResolvesAgainstBepInEx(string setting) {
    Assert.Equal(
        Path.Combine(BepInExRoot, "modpack", "images"),
        ImageFolderPath.Resolve(setting, BepInExRoot));
  }

  [Fact]
  public void AbsoluteFolderIsUsedDirectly() {
    string folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "package", "screens"));

    Assert.Equal(folder, ImageFolderPath.Resolve(folder, BepInExRoot));
  }

  [Fact]
  public void ParentSegmentsAreNormalized() {
    Assert.Equal(
        Path.Combine(BepInExRoot, "screens"),
        ImageFolderPath.Resolve("modpack/../screens", BepInExRoot));
  }
}
