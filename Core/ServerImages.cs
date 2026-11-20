namespace LoadingScreen;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

using BepInEx;

using global::LoadingScreen.Screens;

using static PluginConfig;

public static class ServerImages {
  const string RpcRequestManifest = "LoadingScreen_RequestManifest";
  const string RpcManifest = "LoadingScreen_Manifest";
  const string RpcRequestChunk = "LoadingScreen_RequestChunk";
  const string RpcChunk = "LoadingScreen_Chunk";

  const int ProtocolVersion = 1;
  const int ChunkSize = 32 * 1024;
  const long MaxFileSize = 16L * 1024 * 1024;
  const long MaxTotalSize = 512L * 1024 * 1024;
  const int MaxFiles = 2000;
  const int MaxManifestSize = 4 * 1024 * 1024;

  sealed class ServedFile {
    public string Path;
    public string Hash;
    public long Size;
    public byte[] CompressedData;
    public long TransferSize => CompressedData?.LongLength ?? Size;
    public int ChunkCount => (int) Math.Max(1, (TransferSize + ChunkSize - 1) / ChunkSize);
  }

  sealed class ListedFile {
    public string Path;
    public string Hash;
    public long Size;
    public bool Compressed;
    public long TransferSize;
    public int ChunkCount;
  }

  static readonly Dictionary<string, ServedFile> _served = new(StringComparer.Ordinal);
  static string _servedYaml = string.Empty;

  static readonly Dictionary<string, ListedFile> _listedByPath = new(StringComparer.OrdinalIgnoreCase);
  static readonly List<ListedFile> _downloadQueue = [];
  static ListedFile _downloading;
  static MemoryStream _downloadBuffer;
  static int _nextChunk;
  static bool _manifestRequested;
  static int _downloadedCount;
  static int _failedCount;

  public static string CacheDir => Path.Combine(Paths.CachePath, "LoadingScreen");

  public static void RegisterRpcs() {
    if (ZRoutedRpc.instance == null) {
      return;
    }

    ZRoutedRpc.instance.Register<ZPackage>(RpcRequestManifest, OnRequestManifest);
    ZRoutedRpc.instance.Register<ZPackage>(RpcManifest, OnManifest);
    ZRoutedRpc.instance.Register<ZPackage>(RpcRequestChunk, OnRequestChunk);
    ZRoutedRpc.instance.Register<ZPackage>(RpcChunk, OnChunk);

    if (ZNet.instance && ZNet.instance.IsServer()) {
      BuildServedFiles();
    }
  }

  public static void OnLocalConfigLoaded() {
    if (ZNet.instance && ZNet.instance.IsServer() && ZRoutedRpc.instance != null) {
      BuildServedFiles();
    }
  }

  public static void OnLocalPlayerSpawned() {
    if (_manifestRequested || !DownloadServerImages.Value || ZRoutedRpc.instance == null) {
      return;
    }

    if (!ZNet.instance || ZNet.instance.IsServer()) {
      return;
    }

    _manifestRequested = true;

    ZPackage request = new();
    request.Write(ProtocolVersion);
    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RpcRequestManifest, request);
  }

  public static void Reset() {
    _served.Clear();
    _servedYaml = string.Empty;
    _listedByPath.Clear();
    _downloadQueue.Clear();
    _downloading = null;
    _downloadBuffer = null;
    _manifestRequested = false;
    _downloadedCount = 0;
    _failedCount = 0;
    ScreenConfigLoader.UseLocalConfig();
  }

  public static string Status() {
    int pending = _downloadQueue.Count + (_downloading != null ? 1 : 0);
    return $"Server images: {_listedByPath.Count} listed, {_downloadedCount} downloaded this session, "
        + $"{pending} pending, {_failedCount} failed.";
  }

  static void BuildServedFiles() {
    _served.Clear();
    _servedYaml = string.Empty;

    if (!ServeImagesToClients.Value) {
      return;
    }

    ScreenConfig config = ScreenConfigLoader.LocalConfig;

    if (config.IsEmpty) {
      return;
    }

    _servedYaml = ScreenConfigLoader.ReadLocalYaml();
    long total = 0;

    foreach (string relativePath in ImagesByPriority(config)) {
      string fullPath = ScreenConfigLoader.LocalImagePath(relativePath);

      if (fullPath == null || !File.Exists(fullPath)) {
        continue;
      }

      byte[] data = File.ReadAllBytes(fullPath);

      if (data.LongLength > MaxFileSize || total + data.LongLength > MaxTotalSize || _served.Count >= MaxFiles) {
        LoadingScreen.LogWarning($"Not serving {relativePath}: over the size or count limit.");
        continue;
      }

      ServedFile file = new() { Path = relativePath, Hash = Sha256(data), Size = data.LongLength };

      if (relativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) {
        byte[] compressed = Utils.Compress(data);

        if (compressed.LongLength < data.LongLength) {
          file.CompressedData = compressed;
        }
      }

      total += data.LongLength;
      _served[file.Hash] = file;
    }

    LoadingScreen.LogInfo($"Serving {_served.Count} loading screen images ({total / 1024} KiB) to clients.");
  }

  static IEnumerable<string> ImagesByPriority(ScreenConfig config) {
    IEnumerable<string> layerImages = config.Layouts.Values.SelectMany(layout => layout.Layers)
        .SelectMany(layer => layer.Images);
    IEnumerable<string> general = config.General.Entries.Select(entry => entry.Image);
    IEnumerable<string> death = config.Death.Values.SelectMany(list => list.Entries).Select(entry => entry.Image);
    IEnumerable<string> teleport = config.Teleport.Values.SelectMany(list => list.Entries).Select(entry => entry.Image);

    return layerImages.Concat(general).Concat(death).Concat(teleport).Distinct(StringComparer.OrdinalIgnoreCase);
  }

  static void OnRequestManifest(long sender, ZPackage request) {
    if (!ZNet.instance || !ZNet.instance.IsServer()) {
      return;
    }

    ZPackage manifest = new();
    manifest.Write(ProtocolVersion);

    if (request.ReadInt() != ProtocolVersion || _served.Count == 0) {
      manifest.Write(string.Empty);
      manifest.Write(0);
    } else {
      manifest.Write(_servedYaml);
      manifest.Write(_served.Count);

      foreach (ServedFile file in _served.Values) {
        manifest.Write(file.Path);
        manifest.Write(file.Hash);
        manifest.Write(file.Size);
        manifest.Write(file.CompressedData != null ? 1 : 0);
        manifest.Write(file.TransferSize);
        manifest.Write(file.ChunkCount);
      }
    }

    ZPackage envelope = new();
    envelope.Write(Utils.Compress(manifest.GetArray()));
    ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcManifest, envelope);
  }

  static void OnManifest(long sender, ZPackage envelope) {
    if (!IsFromServer(sender) || !DownloadServerImages.Value) {
      return;
    }

    try {
      byte[] manifestData = BoundedDecompress(envelope.ReadByteArray(), MaxManifestSize);

      if (manifestData == null) {
        LoadingScreen.LogWarning("Loading screen manifest from server is too large; server images ignored.");
        return;
      }

      ZPackage manifest = new(manifestData);

      if (manifest.ReadInt() != ProtocolVersion) {
        LoadingScreen.LogWarning("Server uses another LoadingScreen protocol version; server images ignored.");
        return;
      }

      string yaml = manifest.ReadString();
      int count = manifest.ReadInt();

      if (count <= 0 || string.IsNullOrWhiteSpace(yaml)) {
        LoadingScreen.LogInfo("Server has no loading screen images to share.");
        return;
      }

      ReadListedFiles(manifest, Math.Min(count, MaxFiles));
      ApplyServerConfig(yaml);
    } catch (Exception exception) {
      LoadingScreen.LogError($"Invalid loading screen manifest from server: {exception.Message}");
      _listedByPath.Clear();
    }
  }

  static void ReadListedFiles(ZPackage manifest, int count) {
    _listedByPath.Clear();
    long total = 0;

    for (int i = 0; i < count; i++) {
      ListedFile file = new() {
        Path = manifest.ReadString(),
        Hash = manifest.ReadString().ToLowerInvariant(),
        Size = manifest.ReadLong(),
        Compressed = manifest.ReadInt() == 1,
        TransferSize = manifest.ReadLong(),
        ChunkCount = manifest.ReadInt(),
      };

      bool valid =
          IsHash(file.Hash)
          && file.Size > 0 && file.Size <= MaxFileSize
          && file.TransferSize > 0 && file.TransferSize <= MaxFileSize
          && file.ChunkCount == (int) ((file.TransferSize + ChunkSize - 1) / ChunkSize)
          && CacheExtension(file.Path) != null;

      if (!valid || total + file.Size > MaxTotalSize) {
        LoadingScreen.LogWarning($"Ignoring server image entry '{file.Path}': invalid or over the limits.");
        continue;
      }

      total += file.Size;
      _listedByPath[NormalizePath(file.Path)] = file;
    }
  }

  static void ApplyServerConfig(string yaml) {
    ConfigLoadResult result = ScreenConfigLoader.ParseServerYaml(yaml, path => _listedByPath.ContainsKey(path));

    if (result == null || !result.IsValid || result.Config.IsEmpty) {
      LoadingScreen.LogWarning("Server loading screen config is empty or invalid; using the local one.");
      return;
    }

    foreach (string warning in result.Warnings) {
      LoadingScreen.LogWarning($"server config: {warning}");
    }

    Directory.CreateDirectory(CacheDir);
    ScreenConfigLoader.UseServerConfig(result.Config, CachedPath);

    _downloadQueue.Clear();

    foreach (string path in ImagesByPriority(result.Config)) {
      if (_listedByPath.TryGetValue(path, out ListedFile file) && !File.Exists(CacheFile(file))) {
        _downloadQueue.Add(file);
      }
    }

    LoadingScreen.LogInfo($"{_downloadQueue.Count} server images to download, the rest are cached.");
    RequestNextFile();
  }

  static string CachedPath(string relativePath) {
    if (_listedByPath.TryGetValue(NormalizePath(relativePath), out ListedFile file)) {
      string path = CacheFile(file);
      return File.Exists(path) ? path : null;
    }

    return null;
  }

  static void RequestNextFile() {
    _downloading = null;
    _downloadBuffer = null;

    while (_downloadQueue.Count > 0) {
      ListedFile next = _downloadQueue[0];
      _downloadQueue.RemoveAt(0);

      if (!File.Exists(CacheFile(next))) {
        _downloading = next;
        _downloadBuffer = new MemoryStream((int) next.TransferSize);
        _nextChunk = 0;
        RequestChunk();
        return;
      }
    }
  }

  static void RequestChunk() {
    ZPackage request = new();
    request.Write(_downloading.Hash);
    request.Write(_nextChunk);
    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RpcRequestChunk, request);
  }

  static void OnRequestChunk(long sender, ZPackage request) {
    if (!ZNet.instance || !ZNet.instance.IsServer()) {
      return;
    }

    string hash = request.ReadString();
    int index = request.ReadInt();

    if (!_served.TryGetValue(hash, out ServedFile file) || index < 0 || index >= file.ChunkCount) {
      return;
    }

    long offset = (long) index * ChunkSize;
    int length = (int) Math.Min(ChunkSize, file.TransferSize - offset);
    byte[] chunk = new byte[length];

    if (file.CompressedData != null) {
      Buffer.BlockCopy(file.CompressedData, (int) offset, chunk, 0, length);
    } else {
      string fullPath = ScreenConfigLoader.LocalImagePath(file.Path);

      using FileStream stream = File.OpenRead(fullPath);
      stream.Position = offset;
      int read = 0;

      while (read < length) {
        int count = stream.Read(chunk, read, length - read);

        if (count <= 0) {
          return;
        }

        read += count;
      }
    }

    ZPackage response = new();
    response.Write(hash);
    response.Write(index);
    response.Write(chunk);
    ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcChunk, response);
  }

  static void OnChunk(long sender, ZPackage response) {
    if (!IsFromServer(sender) || _downloading == null) {
      return;
    }

    string hash = response.ReadString();
    int index = response.ReadInt();
    byte[] chunk = response.ReadByteArray();

    if (hash != _downloading.Hash || index != _nextChunk
        || _downloadBuffer.Length + chunk.LongLength > _downloading.TransferSize) {
      return;
    }

    _downloadBuffer.Write(chunk, 0, chunk.Length);
    _nextChunk++;

    if (_nextChunk < _downloading.ChunkCount) {
      RequestChunk();
      return;
    }

    FinishDownload();
    RequestNextFile();
  }

  static void FinishDownload() {
    ListedFile file = _downloading;

    try {
      byte[] data = _downloadBuffer.ToArray();

      if (file.Compressed) {
        data = BoundedDecompress(data, file.Size);
      }

      if (data == null || data.LongLength != file.Size || Sha256(data) != file.Hash) {
        _failedCount++;
        LoadingScreen.LogWarning($"Server image {file.Path} failed verification; discarded.");
        return;
      }

      string target = CacheFile(file);
      string temporary = target + ".part";
      File.WriteAllBytes(temporary, data);

      if (File.Exists(target)) {
        File.Delete(temporary);
      } else {
        File.Move(temporary, target);
      }

      _downloadedCount++;
    } catch (Exception exception) {
      _failedCount++;
      LoadingScreen.LogWarning($"Could not save server image {file.Path}: {exception.Message}");
    }
  }

  static bool IsFromServer(long sender) {
    return ZRoutedRpc.instance != null
        && ZNet.instance
        && !ZNet.instance.IsServer()
        && sender == ZRoutedRpc.instance.GetServerPeerID();
  }

  static string CacheFile(ListedFile file) {
    return Path.Combine(CacheDir, file.Hash + CacheExtension(file.Path));
  }

  static string CacheExtension(string path) {
    string extension = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
    return KnownNames.ImageExtensions.Contains(extension) ? extension : null;
  }

  static bool IsHash(string hash) {
    return hash.Length == 64 && hash.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
  }

  static string NormalizePath(string path) {
    return path.Trim().Replace('\\', '/');
  }

  static byte[] BoundedDecompress(byte[] compressed, long maxSize) {
    using MemoryStream input = new(compressed);
    using GZipStream gzip = new(input, CompressionMode.Decompress);
    using MemoryStream output = new();
    byte[] buffer = new byte[81920];
    int read;

    while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0) {
      if (output.Length + read > maxSize) {
        return null;
      }

      output.Write(buffer, 0, read);
    }

    return output.ToArray();
  }

  static string Sha256(byte[] data) {
    using SHA256 sha = SHA256.Create();
    return string.Concat(sha.ComputeHash(data).Select(b => b.ToString("x2")));
  }
}
