namespace LoadingScreen;

using System;
using System.IO;

using global::LoadingScreen.Model;

public static class ExampleConfig {
  public static void Write(string path) {
    try {
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      File.WriteAllText(path, ExampleConfigText.Text.Replace("\r\n", "\n"));
      LoadingScreen.LogInfo($"Created example config: {path}");
    } catch (Exception exception) {
      LoadingScreen.LogWarning($"Could not create example config {path}: {exception.Message}");
    }
  }
}
