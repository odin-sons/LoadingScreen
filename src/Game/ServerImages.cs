namespace LoadingScreen;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using BepInEx;

using global::LoadingScreen.Model;

using UnityEngine;

using static PluginConfig;

public static class ServerImages {
  const string RpcRequestManifest = "LoadingScreen_RequestManifest";
  const string RpcManifest = "LoadingScreen_Manifest";
  const string RpcRequestChunk = "LoadingScreen_RequestChunk";
  const string RpcChunk = "LoadingScreen_Chunk";

  const int ProtocolVersion = 2;
  const int ChunkSize = 32 * 1024;
  const int NotAvailable = -1;
  const long MaxFileSize = 16L * 1024 * 1024;
  const long MaxTotalSize = 512L * 1024 * 1024;
  const long MaxCacheSize = 1024L * 1024 * 1024;
  const int MaxFiles = 2000;
  const int MaxYamlLength = 256 * 1024;
  const int MaxManifestSize = 4 * 1024 * 1024;
  const int MaxCaptionLength = 500;
  const float ChunkTimeoutSeconds = 10f;
  const int MaxChunkRetries = 3;
  const int MaxChunkRequestsPerSecond = 64;
  const float ManifestRequestCooldownSeconds = 30f;

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

  sealed class PeerLimits {
    public float WindowStart;
    public int ChunkRequests;
    public float LastManifestTime = float.MinValue;
  }

  static readonly Dictionary<string, ServedFile> _servedByPath = new(StringComparer.OrdinalIgnoreCase);
  static readonly Dictionary<string, ServedFile> _servedByHash = new(StringComparer.Ordinal);
  static readonly Dictionary<ZRpc, PeerLimits> _peerLimits = [];
  static byte[] _manifestData;

  static readonly Dictionary<string, ListedFile> _listedByPath = new(StringComparer.OrdinalIgnoreCase);
  static readonly List<ListedFile> _downloadQueue = [];
  static ZRpc _serverRpc;
  static ListedFile _downloading;
  static MemoryStream _downloadBuffer;
  static int _nextChunk;
  static int _retries;
  static float _lastRequestTime;
  static bool _manifestRequested;
  static bool _manifestReceived;
  static int _downloadedCount;
  static int _failedCount;

  public static string CacheDir => Path.Combine(Paths.CachePath, "LoadingScreen");

  public static void OnNewConnection(ZNetPeer peer) {
    if (peer?.m_rpc == null || !ZNet.instance) {
      return;
    }

    if (ZNet.instance.IsServer()) {
      peer.m_rpc.Register<ZPackage>(RpcRequestManifest, OnRequestManifest);
      peer.m_rpc.Register<ZPackage>(RpcRequestChunk, OnRequestChunk);
    } else {
      _serverRpc = peer.m_rpc;
      peer.m_rpc.Register<ZPackage>(RpcManifest, OnManifest);
      peer.m_rpc.Register<ZPackage>(RpcChunk, OnChunk);
    }
  }

  public static void OnGameStart() {
    if (ZNet.instance && ZNet.instance.IsServer()) {
      BuildServedFiles();
    }
  }

  public static void OnLocalConfigLoaded() {
    if (ZNet.instance && ZNet.instance.IsServer()) {
      BuildServedFiles();
    }
  }

  public static void OnLocalPlayerSpawned() {
    if (_manifestRequested || !DownloadServerImages.Value || _serverRpc == null || !_serverRpc.IsConnected()) {
      return;
    }

    if (!ZNet.instance || ZNet.instance.IsServer()) {
      return;
    }

    _manifestRequested = true;

    ZPackage request = new();
    request.Write(ProtocolVersion);
    _serverRpc.Invoke(RpcRequestManifest, request);
  }

  public static void Tick() {
    if (_downloading == null || Time.realtimeSinceStartup - _lastRequestTime < ChunkTimeoutSeconds) {
      return;
    }

    if (_serverRpc == null || !_serverRpc.IsConnected()) {
      _downloading = null;
      _downloadQueue.Clear();
      return;
    }

    if (++_retries > MaxChunkRetries) {
      _failedCount++;
      LoadingScreen.LogWarning($"Server image {_downloading.Path} timed out; skipped.");
      RequestNextFile();
    } else {
      RequestChunk();
    }
  }

  public static void Reset() {
    _servedByPath.Clear();
    _servedByHash.Clear();
    _peerLimits.Clear();
    _manifestData = null;
    _listedByPath.Clear();
    _downloadQueue.Clear();
    _serverRpc = null;
    _downloading = null;
    _downloadBuffer = null;
    _manifestRequested = false;
    _manifestReceived = false;
    _downloadedCount = 0;
    _failedCount = 0;
    ScreenConfigLoader.UseLocalConfig();
  }

  public static string Status() {
    int pending = _downloadQueue.Count + (_downloading != null ? 1 : 0);
    return $"Server files: {_listedByPath.Count} listed, {_downloadedCount} downloaded this session, "
        + $"{pending} pending, {_failedCount} failed.";
  }

  static void BuildServedFiles() {
    _servedByPath.Clear();
    _servedByHash.Clear();
    _manifestData = null;

    if (!ServeImagesToClients.Value) {
      return;
    }

    ScreenConfig config = ScreenConfigLoader.LocalConfig;
    string yaml = ScreenConfigLoader.ReadLocalYaml();

    if (config.IsEmpty) {
      return;
    }

    if (yaml.Length > MaxYamlLength) {
      LoadingScreen.LogWarning($"{ScreenConfigLoader.ConfigFileName} is over {MaxYamlLength / 1024} KiB; not shared.");
      return;
    }

    long total = 0;

    foreach (string relativePath in AssetsByPriority(config)) {
      if (_servedByPath.Count >= MaxFiles) {
        break;
      }

      try {
        string fullPath = ScreenConfigLoader.LocalImagePath(relativePath);

        if (fullPath == null || !File.Exists(fullPath)) {
          continue;
        }

        bool audio = KnownNames.AudioExtensions.Contains(Path.GetExtension(relativePath).ToLowerInvariant());
        long limit = audio ? AudioFile.MaxBytes : MaxFileSize;
        if (new FileInfo(fullPath).Length > limit) {
          LoadingScreen.LogWarning($"Not sharing {relativePath}: over the size limit.");
          continue;
        }
        byte[] data = File.ReadAllBytes(fullPath);

        if (data.LongLength == 0 || data.LongLength > limit || total + data.LongLength > MaxTotalSize
            || (audio && !AudioFile.IsValid(relativePath, data))) {
          LoadingScreen.LogWarning($"Not sharing {relativePath}: empty or over the size limit.");
          continue;
        }

        ServedFile file = new() { Path = relativePath, Hash = Sha256(data), Size = data.LongLength };

        if (_servedByHash.TryGetValue(file.Hash, out ServedFile same)) {
          file.CompressedData = same.CompressedData;
        } else if (relativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) {
          byte[] compressed = Utils.Compress(data);
          file.CompressedData = compressed.LongLength < data.LongLength ? compressed : null;
        }

        total += data.LongLength;
        _servedByPath[relativePath] = file;
        _servedByHash[file.Hash] = file;
      } catch (Exception exception) {
        LoadingScreen.LogWarning($"Not sharing {relativePath}: {exception.Message}");
      }
    }

    ZPackage manifest = new();
    manifest.Write(ProtocolVersion);
    manifest.Write(yaml);
    manifest.Write(_servedByPath.Count);

    foreach (ServedFile file in _servedByPath.Values) {
      manifest.Write(file.Path);
      manifest.Write(file.Hash);
      manifest.Write(file.Size);
      manifest.Write(file.CompressedData != null ? 1 : 0);
      manifest.Write(file.TransferSize);
      manifest.Write(file.ChunkCount);
    }

    _manifestData = Utils.Compress(manifest.GetArray());
    LoadingScreen.LogInfo($"Sharing {_servedByPath.Count} loading screen files ({total / 1024} KiB) with clients.");
  }

  static IEnumerable<string> AssetsByPriority(ScreenConfig config) {
    IEnumerable<string> layerImages = config.Layouts.Values.SelectMany(layout => layout.Layers)
        .SelectMany(layer => layer.Images);
    IEnumerable<string> general = config.General.Entries.Select(entry => entry.Image);
    IEnumerable<string> death = config.Death.Values.SelectMany(list => list.Entries).Select(entry => entry.Image);
    IEnumerable<string> teleport = config.Teleport.Values.SelectMany(list => list.Entries).Select(entry => entry.Image);

    return layerImages.Concat(general).Concat(death).Concat(teleport).Concat(AudioFile.Paths(config))
        .Where(image => image != null)
        .Distinct(StringComparer.OrdinalIgnoreCase);
  }

  static PeerLimits LimitsFor(ZRpc rpc) {
    if (!_peerLimits.TryGetValue(rpc, out PeerLimits limits)) {
      if (_peerLimits.Count > 256) {
        foreach (ZRpc stale in _peerLimits.Keys.Where(key => !key.IsConnected()).ToList()) {
          _peerLimits.Remove(stale);
        }
      }

      limits = new();
      _peerLimits[rpc] = limits;
    }

    return limits;
  }

  static void OnRequestManifest(ZRpc rpc, ZPackage request) {
    if (!ZNet.instance || !ZNet.instance.IsServer()) {
      return;
    }

    PeerLimits limits = LimitsFor(rpc);
    float now = Time.realtimeSinceStartup;

    if (now - limits.LastManifestTime < ManifestRequestCooldownSeconds) {
      return;
    }

    limits.LastManifestTime = now;

    ZPackage envelope = new();
    bool compatible = request.ReadInt() == ProtocolVersion;
    envelope.Write(compatible && _manifestData != null ? _manifestData : []);
    rpc.Invoke(RpcManifest, envelope);
  }

  static void OnManifest(ZRpc rpc, ZPackage envelope) {
    if (rpc != _serverRpc || !_manifestRequested || _manifestReceived || !DownloadServerImages.Value) {
      return;
    }

    _manifestReceived = true;

    try {
      byte[] compressed = envelope.ReadByteArray();

      if (compressed.Length == 0) {
        LoadingScreen.LogInfo("Server has no loading screen images to share.");
        return;
      }

      byte[] manifestData = BoundedDecompress(compressed, MaxManifestSize);

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

      if (yaml.Length > MaxYamlLength || count <= 0 || count > MaxFiles) {
        LoadingScreen.LogWarning("Loading screen manifest from server is empty or over the limits; ignored.");
        return;
      }

      ReadListedFiles(manifest, count);
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
        Path = NormalizePath(manifest.ReadString()),
        Hash = manifest.ReadString().ToLowerInvariant(),
        Size = manifest.ReadLong(),
        Compressed = manifest.ReadInt() == 1,
        TransferSize = manifest.ReadLong(),
        ChunkCount = manifest.ReadInt(),
      };

      bool valid =
          IsHash(file.Hash)
          && file.Size > 0 && file.Size <= FileLimit(file.Path)
          && file.TransferSize > 0 && file.TransferSize <= FileLimit(file.Path)
          && file.ChunkCount == (int) ((file.TransferSize + ChunkSize - 1) / ChunkSize)
          && CacheExtension(file.Path) != null;

      if (!valid || total + file.Size > MaxTotalSize) {
        LoadingScreen.LogWarning($"Ignoring server image entry '{file.Path}': invalid or over the limits.");
        continue;
      }

      total += file.Size;
      _listedByPath[file.Path] = file;
    }
  }

  static void ApplyServerConfig(string yaml) {
    ConfigLoadResult result = ScreenConfigLoader.ParseServerYaml(yaml, path => _listedByPath.ContainsKey(path));

    if (result == null || !result.IsValid || result.Config.IsEmpty) {
      LoadingScreen.LogWarning("Server loading screen config is empty or invalid; using the local one.");
      return;
    }

    foreach (string warning in result.Warnings.Take(50)) {
      LoadingScreen.LogWarning($"server config: {warning}");
    }

    foreach (Entry entry in result.Config.AllLists.SelectMany(list => list.Entries)) {
      entry.Text = SanitizeCaption(entry.Text);
    }

    Directory.CreateDirectory(CacheDir);
    PruneCache();
    ScreenConfigLoader.UseServerConfig(result.Config, CachedPath);

    _downloadQueue.Clear();

    foreach (string path in AssetsByPriority(result.Config)) {
      if (_listedByPath.TryGetValue(path, out ListedFile file) && !File.Exists(CacheFile(file))
          && !_downloadQueue.Contains(file)) {
        _downloadQueue.Add(file);
      }
    }

    LoadingScreen.LogInfo($"{_downloadQueue.Count} server images to download, the rest are cached.");
    RequestNextFile();
  }

  static string SanitizeCaption(string text) {
    if (string.IsNullOrEmpty(text)) {
      return text;
    }

    string plain = Regex.Replace(text, "<[^>]*>", string.Empty);
    return plain.Length > MaxCaptionLength ? plain.Substring(0, MaxCaptionLength) : plain;
  }

  static void PruneCache() {
    try {
      HashSet<string> keep = new(_listedByPath.Values.Select(CacheFile), StringComparer.OrdinalIgnoreCase);
      List<FileInfo> files = new DirectoryInfo(CacheDir).GetFiles().OrderBy(file => file.LastWriteTimeUtc).ToList();

      foreach (FileInfo file in files.Where(file => file.Name.EndsWith(".part", StringComparison.Ordinal))) {
        file.Delete();
      }

      foreach (FileInfo file in files.Where(file => keep.Contains(file.FullName) && File.Exists(file.FullName))) {
        file.LastWriteTimeUtc = DateTime.UtcNow;
      }

      List<FileInfo> remaining = files.Where(file => File.Exists(file.FullName)).ToList();
      long total = remaining.Sum(file => file.Length);

      foreach (FileInfo file in remaining.Where(file => !keep.Contains(file.FullName))) {
        if (total <= MaxCacheSize) {
          break;
        }

        total -= file.Length;
        file.Delete();
      }
    } catch (Exception exception) {
      LoadingScreen.LogWarning($"Could not clean the image cache: {exception.Message}");
    }
  }

  static string CachedPath(string relativePath) {
    if (relativePath != null && _listedByPath.TryGetValue(NormalizePath(relativePath), out ListedFile file)) {
      string path = CacheFile(file);
      return File.Exists(path) ? path : null;
    }

    return null;
  }

  static void RequestNextFile() {
    _downloading = null;
    _downloadBuffer = null;
    _retries = 0;

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
    _lastRequestTime = Time.realtimeSinceStartup;

    if (_serverRpc == null || !_serverRpc.IsConnected()) {
      return;
    }

    ZPackage request = new();
    request.Write(_downloading.Hash);
    request.Write(_nextChunk);
    _serverRpc.Invoke(RpcRequestChunk, request);
  }

  static void OnRequestChunk(ZRpc rpc, ZPackage request) {
    if (!ZNet.instance || !ZNet.instance.IsServer()) {
      return;
    }

    PeerLimits limits = LimitsFor(rpc);
    float now = Time.realtimeSinceStartup;

    if (now - limits.WindowStart >= 1f) {
      limits.WindowStart = now;
      limits.ChunkRequests = 0;
    }

    if (++limits.ChunkRequests > MaxChunkRequestsPerSecond) {
      return;
    }

    string hash = request.ReadString();
    int index = request.ReadInt();
    byte[] chunk = ReadChunk(hash, index);

    ZPackage response = new();
    response.Write(hash);
    response.Write(chunk == null ? NotAvailable : index);
    response.Write(chunk ?? []);
    rpc.Invoke(RpcChunk, response);
  }

  static byte[] ReadChunk(string hash, int index) {
    if (!_servedByHash.TryGetValue(hash, out ServedFile file) || index < 0 || index >= file.ChunkCount) {
      return null;
    }

    long offset = (long) index * ChunkSize;
    int length = (int) Math.Min(ChunkSize, file.TransferSize - offset);
    byte[] chunk = new byte[length];

    if (file.CompressedData != null) {
      Buffer.BlockCopy(file.CompressedData, (int) offset, chunk, 0, length);
      return chunk;
    }

    try {
      using FileStream stream = File.OpenRead(ScreenConfigLoader.LocalImagePath(file.Path));

      if (stream.Length != file.Size) {
        return null;
      }

      stream.Position = offset;
      int read = 0;

      while (read < length) {
        int count = stream.Read(chunk, read, length - read);

        if (count <= 0) {
          return null;
        }

        read += count;
      }

      return chunk;
    } catch (Exception) {
      return null;
    }
  }

  static void OnChunk(ZRpc rpc, ZPackage response) {
    if (rpc != _serverRpc || _downloading == null) {
      return;
    }

    string hash = response.ReadString();
    int index = response.ReadInt();

    if (hash != _downloading.Hash) {
      return;
    }

    if (index == NotAvailable) {
      _failedCount++;
      LoadingScreen.LogWarning($"Server image {_downloading.Path} is no longer available; skipped.");
      RequestNextFile();
      return;
    }

    byte[] chunk = response.ReadByteArray();

    if (index != _nextChunk || _downloadBuffer.Length + chunk.LongLength > _downloading.TransferSize) {
      return;
    }

    _downloadBuffer.Write(chunk, 0, chunk.Length);
    _nextChunk++;
    _retries = 0;

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

      bool audio = KnownNames.AudioExtensions.Contains(CacheExtension(file.Path));
      if (audio ? !AudioFile.IsValid(file.Path, data)
          : !ImageHeader.TryRead(data, out int width, out int height) || !ImageHeader.IsAllowed(width, height)) {
        _failedCount++;
        LoadingScreen.LogWarning($"Server file {file.Path} is invalid or too large; discarded.");
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

  static string CacheFile(ListedFile file) {
    return Path.Combine(CacheDir, file.Hash + CacheExtension(file.Path));
  }

  static string CacheExtension(string path) {
    string extension;

    try {
      extension = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
    } catch (ArgumentException) {
      return null;
    }

    return KnownNames.ImageExtensions.Contains(extension) || KnownNames.AudioExtensions.Contains(extension)
        ? extension : null;
  }

  static long FileLimit(string path) {
    return KnownNames.AudioExtensions.Contains(CacheExtension(path)) ? AudioFile.MaxBytes : MaxFileSize;
  }

  static bool IsHash(string hash) {
    return hash.Length == 64 && hash.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
  }

  static string NormalizePath(string path) {
    return (path ?? string.Empty).Trim().Replace('\\', '/');
  }

  static byte[] BoundedDecompress(byte[] compressed, long maxSize) {
    try {
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
    } catch (InvalidDataException) {
      return null;
    }
  }

  static string Sha256(byte[] data) {
    using SHA256 sha = SHA256.Create();
    return string.Concat(sha.ComputeHash(data).Select(b => b.ToString("x2")));
  }
}
