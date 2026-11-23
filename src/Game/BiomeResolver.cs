namespace LoadingScreen;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using global::LoadingScreen.Model;

public sealed class BiomeResolver : IBiomeResolver {
  public static BiomeResolver Instance { get; } = new();

  readonly Dictionary<string, int> _custom = new(StringComparer.OrdinalIgnoreCase);
  Type _manager;
  MethodInfo _tryGetBiome;
  MethodInfo _getTerrain;
  MethodInfo _tryGetDisplayName;
  PropertyInfo _hasData;
  FieldInfo _hasDataField;
  int _lastAssemblyCount;

  BiomeResolver() { }

  public bool IsInstalled => FindManager() != null;
  public bool IsReady => !IsInstalled || ReadHasData();

  public bool TryResolve(string name, out int biome) {
    biome = default;
    if (Enum.TryParse(name, true, out Heightmap.Biome vanilla)
        && Enum.IsDefined(typeof(Heightmap.Biome), vanilla)) {
      biome = (int)vanilla;
      return true;
    }

    if (_custom.TryGetValue(name, out biome)) return true;
    if (FindManager() == null || !ReadHasData() || _tryGetBiome == null) return false;

    try {
      object[] args = [name, Heightmap.Biome.None];
      if ((bool)_tryGetBiome.Invoke(null, args)) {
        biome = (int)(Heightmap.Biome)args[1];
        _custom[name] = biome;
        return true;
      }
    } catch (Exception) {
      return false;
    }

    return false;
  }

  public int GetBaseBiome(int biome) {
    if (FindManager() == null || !ReadHasData() || _getTerrain == null) return biome;
    try {
      return (int)(Heightmap.Biome)_getTerrain.Invoke(null, [(Heightmap.Biome)biome]);
    } catch (Exception) {
      return biome;
    }
  }

  public bool TryGetDisplayName(int biome, out string name) {
    name = null;
    if (FindManager() == null || !ReadHasData() || _tryGetDisplayName == null) return false;
    try {
      object[] args = [(Heightmap.Biome)biome, null];
      if ((bool)_tryGetDisplayName.Invoke(null, args)) {
        name = args[1] as string;
        return !string.IsNullOrEmpty(name);
      }
    } catch (Exception) {
      return false;
    }

    return false;
  }

  public void ClearCache() => _custom.Clear();

  Type FindManager() {
    if (_manager != null) return _manager;
    Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
    if (assemblies.Length == _lastAssemblyCount) return null;
    _lastAssemblyCount = assemblies.Length;
    _manager = assemblies
        .Select(assembly => assembly.GetType("ExpandWorldData.Biome.BiomeManager", false))
        .FirstOrDefault(type => type != null);
    if (_manager == null) return null;

    BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
    _tryGetBiome = _manager.GetMethod("TryGetBiome", flags);
    _getTerrain = _manager.GetMethod("GetTerrain", flags);
    _tryGetDisplayName = _manager.GetMethod("TryGetDisplayName", flags);
    _hasData = _manager.GetProperty("HasData", flags);
    _hasDataField = _manager.GetField("HasData", flags);
    return _manager;
  }

  bool ReadHasData() {
    try {
      return _hasData != null ? (bool)_hasData.GetValue(null)
          : _hasDataField != null && (bool)_hasDataField.GetValue(null);
    } catch (Exception) {
      return false;
    }
  }
}
