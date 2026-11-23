# Contributing

## Building

Requires the .NET SDK and a Valheim install with BepInEx.

```sh
dotnet build LoadingScreen.csproj -c Release
```

MSBuild properties:

- `GamePath`: the Valheim install (the folder with `valheim_Data`). Found through the Steam registry keys or the default
  Steam locations if not set.
- `BepInExPath`: the `BepInEx` folder, default `$(GamePath)/BepInEx`. Set it when BepInEx lives in a mod manager
  profile, for example `-p:BepInExPath="<profile>/BepInEx"`.
- `PluginOutputPath`: where the built DLL is copied, default `$(BepInExPath)/plugins`.

Game assemblies are publicized at build time by `BepInEx.AssemblyPublicizer.MSBuild`, so no pre-generated
`publicized_assemblies` folder is needed. The build also writes a Thunderstore package `LoadingScreen_v<version>.zip`
to `bin/Release`.

Unit tests for the Unity-free code (config parser, selection, placement math) need only the .NET SDK:

```sh
dotnet test tests/LoadingScreen.Tests
```

## Source layout

- `src/Model`: pure logic and parsing.
- `src/Patches`: Harmony patches.
- `src/Rendering`: loading screen UI.
- `src/Game`: game integration.
- `src/Plugin`: plugin entry point, configuration, and logging.
- `tests/`: unit tests.

## Branches and commits

Use GitFlow: `main` holds tagged releases `vX.Y.Z` only. Start work from `develop` on
`feature/<issue>-slug` or `bugfix/<issue>-slug` branches and merge into `develop`
with `--no-ff`. Prepare releases on `release/X.Y.Z`.

Use Conventional Commit titles and sign every commit with GPG.

## Changelog

Start each release entry with one summary sentence. Then use `#### Features`,
`#### Changes` only for breaking changes, and `#### Fixes` as needed.
Write items in this form:

```markdown
  * **Gist.** Describe the user-visible change.
```
