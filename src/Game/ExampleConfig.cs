namespace LoadingScreen;

using System;
using System.IO;

using global::LoadingScreen.Model;

public static class ExampleConfig {
  public static void Write(string path) {
    try {
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      File.WriteAllText(path, ExampleConfigText.Text.Replace("\r\n", "\n"));
      Log.Config.Info(LogContext.Message("Created example configuration.", ("file", LogContext.File(path))));
    } catch (Exception exception) {
      Log.Config.Warning(LogContext.Message("Could not create example configuration.",
          ("file", LogContext.File(path)), ("error", exception.Message)));
      Log.Config.Debug(exception);
    }
  }
}
