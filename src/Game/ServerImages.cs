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

  const int ProtocolVersion = 3;
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
  static float _lastManifestRequestTime;
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

  public static void OnSettingsChanged() {
    if (!IsModEnabled.Value || !ShareScreensWithPlayers.Value) {
      _manifestRequested = false;
      _manifestReceived = false;
      _listedByPath.Clear();
      _downloadQueue.Clear();
      _downloading = null;
      _downloadBuffer = null;
      _nextChunk = 0;
      _retries = 0;
      _downloadedCount = 0;
      _failedCount = 0;
      ScreenConfigLoader.UseLocalConfig();
    }

    OnGameStart();
    if (IsModEnabled.Value && ShareScreensWithPlayers.Value) OnLocalPlayerSpawned();
  }

  public static void OnLocalConfigLoaded() {
    if (ZNet.instance && ZNet.instance.IsServer()) {
      BuildServedFiles();
    }
  }

  public static void OnLocalPlayerSpawned() {
    if (_manifestRequested || !IsModEnabled.Value || !ShareScreensWithPlayers.Value
        || _serverRpc == null || !_serverRpc.IsConnected()) {
      return;
    }

    if (!ZNet.instance || ZNet.instance.IsServer()) {
      return;
    }

    _manifestRequested = true;
    RequestManifest();
  }

  static void RequestManifest() {
    _lastManifestRequestTime = Time.realtimeSinceStartup;
    ZPackage request = new();
    request.Write(ProtocolVersion);
    _serverRpc.Invoke(RpcRequestManifest, request);
  }

  public static void Tick() {
    if (_manifestRequested && !_manifestReceived && IsModEnabled.Value && ShareScreensWithPlayers.Value
        && _serverRpc != null && _serverRpc.IsConnected()
        && Time.realtimeSinceStartup - _lastManifestRequestTime > ManifestRequestCooldownSeconds + 1f) {
      RequestManifest();
    }

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
      Log.Server.Warning(LogContext.Message("Server file download timed out; skipped file.",
          ("side", "client"), ("file", _downloading.Path), ("retries", _retries - 1),
          ("timeoutSeconds", ChunkTimeoutSeconds)));
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

    if (!IsModEnabled.Value || !ShareScreensWithPlayers.Value) {
      return;
    }

    ScreenConfig config = ScreenConfigLoader.LocalConfig;
    string yaml = ScreenConfigLoader.ReadLocalYaml();
    string animationsYaml = ScreenConfigLoader.ReadLocalAnimationsYaml();

    if (config.IsEmpty) {
      return;
    }

    if (yaml.Length > MaxYamlLength || animationsYaml.Length > MaxYamlLength) {
      Log.Server.Warning(LogContext.Message("Loading screen configuration exceeds the sharing limit.",
          ("side", "server"), ("file", yaml.Length > MaxYamlLength
              ? ScreenConfigLoader.ConfigFileName : ScreenConfigLoader.AnimationsFileName),
          ("bytes", Math.Max(yaml.Length, animationsYaml.Length)), ("maxBytes", MaxYamlLength)));
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
          Log.Server.Warning(LogContext.Message("Server file exceeds the sharing limit.",
              ("side", "server"), ("file", relativePath), ("bytes", new FileInfo(fullPath).Length),
              ("maxBytes", limit)));
          continue;
        }
        byte[] data = File.ReadAllBytes(fullPath);

        if (data.LongLength == 0 || data.LongLength > limit || total + data.LongLength > MaxTotalSize
            || (audio && !AudioFile.IsValid(relativePath, data))) {
          Log.Server.Warning(LogContext.Message("Server file was rejected for sharing.",
              ("side", "server"), ("file", relativePath), ("bytes", data.LongLength),
              ("maxFileBytes", limit), ("totalBytes", total + data.LongLength),
              ("maxTotalBytes", MaxTotalSize), ("validAudio", !audio || AudioFile.IsValid(relativePath, data))));
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
        Log.Server.Warning(LogContext.Message("Could not read server file for sharing.",
            ("side", "server"), ("file", relativePath), ("error", exception.Message)));
        Log.Server.Debug(LogContext.Message("Server file sharing exception.",
            ("side", "server"), ("file", relativePath), ("exception", exception)));
      }
    }

    ZPackage manifest = new();
    manifest.Write(ProtocolVersion);
    manifest.Write(yaml);
    manifest.Write(animationsYaml);
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
    Log.Server.Info(LogContext.Message("Sharing loading screen files with clients.",
        ("side", "server"), ("files", _servedByPath.Count), ("totalBytes", total)));
  }

  static IEnumerable<string> AssetsByPriority(ScreenConfig config) {
    IEnumerable<string> layerImages = config.Layouts.Values.SelectMany(layout => layout.Layers)
        .SelectMany(layer => layer.Images);
    IEnumerable<string> general = config.General.Entries.Select(entry => entry.Image);
    IEnumerable<string> sleep = config.Sleep.Entries.Select(entry => entry.Image);
    IEnumerable<string> death = config.Death.Values.SelectMany(list => list.Entries).Select(entry => entry.Image);
    IEnumerable<string> biome = config.Biome.Values.SelectMany(list => list.Entries).Select(entry => entry.Image);

    return layerImages.Concat(general).Concat(sleep).Concat(death).Concat(biome).Concat(AudioFile.Paths(config))
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
    if (rpc != _serverRpc || !_manifestRequested || _manifestReceived
        || !IsModEnabled.Value || !ShareScreensWithPlayers.Value) {
      return;
    }

    _manifestReceived = true;

    try {
      byte[] compressed = envelope.ReadByteArray();

      if (compressed.Length == 0) {
        Log.Server.Info(LogContext.Message("Server has no loading screen files to share.",
            ("side", "client"), ("manifestBytes", compressed.Length)));
        return;
      }

      byte[] manifestData = BoundedDecompress(compressed, MaxManifestSize);

      if (manifestData == null) {
        Log.Server.Warning(LogContext.Message("Server loading screen manifest exceeds the limit; server files ignored.",
            ("side", "client"), ("compressedBytes", compressed.Length), ("maxBytes", MaxManifestSize)));
        return;
      }

      ZPackage manifest = new(manifestData);

      int protocol = manifest.ReadInt();
      if (protocol != ProtocolVersion) {
        Log.Server.Warning(LogContext.Message("Server loading screen protocol is incompatible; server files ignored.",
            ("side", "client"), ("serverVersion", protocol), ("supportedVersion", ProtocolVersion)));
        return;
      }

      string yaml = manifest.ReadString();
      string animationsYaml = manifest.ReadString();
      int count = manifest.ReadInt();

      if (yaml.Length > MaxYamlLength || animationsYaml.Length > MaxYamlLength || count <= 0 || count > MaxFiles) {
        Log.Server.Warning(LogContext.Message("Server loading screen manifest has invalid limits; server files ignored.",
            ("side", "client"), ("configBytes", yaml.Length), ("animationsBytes", animationsYaml.Length),
            ("files", count), ("maxYamlBytes", MaxYamlLength), ("allowedFiles", $"1..{MaxFiles}")));
        return;
      }

      ReadListedFiles(manifest, count);
      ApplyServerConfig(yaml, animationsYaml);
    } catch (Exception exception) {
      Log.Server.Warning(LogContext.Message("Could not read server loading screen manifest; server files ignored.",
          ("side", "client"), ("input", "server manifest"), ("error", exception.Message)));
      Log.Server.Debug(LogContext.Message("Server manifest parsing exception.",
          ("side", "client"), ("input", "server manifest"), ("exception", exception)));
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
        Log.Server.Warning(LogContext.Message("Server file manifest entry was rejected.",
            ("side", "client"), ("file", file.Path), ("bytes", file.Size),
            ("transferBytes", file.TransferSize), ("chunks", file.ChunkCount),
            ("maxFileBytes", FileLimit(file.Path)), ("maxTotalBytes", MaxTotalSize)));
        continue;
      }

      total += file.Size;
      _listedByPath[file.Path] = file;
    }
  }

  static void ApplyServerConfig(string yaml, string animationsYaml) {
    ConfigLoadResult result = ScreenConfigLoader.ParseServerYaml(
        yaml, animationsYaml, path => _listedByPath.ContainsKey(path));

    if (result == null || !result.IsValid || result.Config.IsEmpty) {
      Log.Server.Warning(LogContext.Message("Server loading screen configuration was rejected; using local screens.",
          ("side", "client"), ("file", ScreenConfigLoader.ConfigFileName),
          ("reason", result == null ? "parser unavailable" : !result.IsValid ? "invalid YAML" : "no entries")));
      return;
    }

    foreach (string warning in result.Warnings.Take(50)) {
      Log.Server.Warning(LogContext.Message(warning, ("source", "server"), ("side", "client")));
    }

    foreach (Entry entry in result.Config.AllLists.SelectMany(list => list.Entries)) {
      entry.Text = SanitizeCaption(entry.Text);
      foreach (string code in entry.Translations.Keys.ToList()) {
        entry.Translations[code] = SanitizeCaption(entry.Translations[code]);
      }
    }

    Directory.CreateDirectory(CacheDir);
    PruneCache();
    ScreenConfigLoader.UseServerConfig(result.Config, CachedPath, result.Warnings.Count);

    _downloadQueue.Clear();

    foreach (string path in AssetsByPriority(result.Config)) {
      if (_listedByPath.TryGetValue(path, out ListedFile file) && !File.Exists(CacheFile(file))
          && !_downloadQueue.Contains(file)) {
        _downloadQueue.Add(file);
      }
    }

    Log.Server.Info(LogContext.Message("Prepared server loading screen file downloads.",
        ("side", "client"), ("pending", _downloadQueue.Count),
        ("cached", _listedByPath.Count - _downloadQueue.Count)));
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
      Log.Server.Warning(LogContext.Message("Could not prune the server loading screen cache.",
          ("side", "client"), ("folder", "BepInEx/cache/LoadingScreen"), ("error", exception.Message)));
      Log.Server.Debug(LogContext.Message("Server cache pruning exception.",
          ("side", "client"), ("folder", "BepInEx/cache/LoadingScreen"), ("exception", exception)));
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
        Log.Server.Debug(LogContext.Message("Downloading server loading screen file.",
            ("side", "client"), ("file", next.Path), ("bytes", next.Size), ("chunks", next.ChunkCount)));
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
    Log.Server.Debug(LogContext.Message("Requesting server loading screen file chunk.",
        ("side", "client"), ("file", _downloading.Path), ("chunk", _nextChunk + 1),
        ("totalChunks", _downloading.ChunkCount)));
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
      Log.Server.Warning(LogContext.Message("Server file is no longer available; skipped download.",
          ("side", "client"), ("file", _downloading.Path), ("chunk", _nextChunk + 1)));
      RequestNextFile();
      return;
    }

    byte[] chunk = response.ReadByteArray();

    if (index != _nextChunk || _downloadBuffer.Length + chunk.LongLength > _downloading.TransferSize) {
      return;
    }

    _downloadBuffer.Write(chunk, 0, chunk.Length);
    Log.Server.Debug(LogContext.Message("Received server loading screen file chunk.",
        ("side", "client"), ("file", _downloading.Path), ("chunk", index + 1),
        ("totalChunks", _downloading.ChunkCount), ("bytes", chunk.Length)));
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
        Log.Server.Warning(LogContext.Message("Server file failed size or hash verification; discarded.",
            ("side", "client"), ("file", file.Path), ("expectedBytes", file.Size),
            ("actualBytes", data?.LongLength ?? -1)));
        return;
      }

      bool audio = KnownNames.AudioExtensions.Contains(CacheExtension(file.Path));
      if (audio ? !AudioFile.IsValid(file.Path, data)
          : !ImageHeader.TryRead(data, out int width, out int height) || !ImageHeader.IsAllowed(width, height)) {
        _failedCount++;
        Log.Server.Warning(LogContext.Message("Server file failed format or dimension validation; discarded.",
            ("side", "client"), ("file", file.Path), ("kind", audio ? "audio" : "image"),
            ("maxAudioBytes", AudioFile.MaxBytes), ("maxImageSidePixels", ImageHeader.MaxSide),
            ("maxImagePixels", ImageHeader.MaxPixels)));
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
      Log.Server.Debug(LogContext.Message("Saved server loading screen file.",
          ("side", "client"), ("file", file.Path), ("bytes", file.Size)));
    } catch (Exception exception) {
      _failedCount++;
      Log.Server.Warning(LogContext.Message("Could not save server loading screen file.",
          ("side", "client"), ("file", file.Path), ("error", exception.Message)));
      Log.Server.Debug(LogContext.Message("Server file saving exception.",
          ("side", "client"), ("file", file.Path), ("exception", exception)));
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
