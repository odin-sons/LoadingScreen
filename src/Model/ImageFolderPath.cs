namespace LoadingScreen.Model;

using System.IO;

public static class ImageFolderPath {
  public const string DefaultFolder = "config/LoadingScreen";

  public static string Resolve(string setting, string bepInExRoot) {
    string folder = string.IsNullOrWhiteSpace(setting) ? DefaultFolder : setting.Trim();
    folder = folder.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    return Path.GetFullPath(Path.IsPathRooted(folder) ? folder : Path.Combine(bepInExRoot, folder));
  }
}
