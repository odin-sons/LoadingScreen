namespace LoadingScreen.Model;

public interface IBiomeResolver {
  bool TryResolve(string name, out int biome);
  int GetBaseBiome(int biome);
}
