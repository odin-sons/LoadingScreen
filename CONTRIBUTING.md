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
to `bin/Release`. It fails if `lib/ServerSync/ServerSync.dll` does not match the SHA-256 in
`ServerSync.dll.sha256` (the upstream v1.20 release asset), or if `manifest.json` and `PluginVersion` disagree.

Unit tests for the Unity-free code (config parser, selection, placement math) need only the .NET SDK:

```sh
dotnet test tests/LoadingScreen.Tests
```

The config editor in `site/` is plain ES modules with no build step. `SchemaFieldAttribute` on the
model types describes YAML keys, values, and ranges. `ConfigSchemaGenerator` in the test project writes
`site/data/config.schema.json`; the schema staleness test fails when the checked-in file differs.
The site reads key sets, choices, language codes, and parser problem-code rules from this schema.

The hand-written site validator mirrors `ConfigParser`. Both run over the YAML files in
`tests/fixtures/config`. Each `.expected` file lists parser problems as
`line<TAB>code<TAB>message`. Stable codes and their matching rules live in
`src/Model/ProblemCodes.cs`. Add a shared fixture for each new code and keep user-facing messages
in sync. To regenerate the schema and expected lists, then check both runtimes:

```sh
LOADINGSCREEN_UPDATE_FIXTURES=1 dotnet test tests/LoadingScreen.Tests
dotnet test tests/LoadingScreen.Tests
node --test "tests/site/*.test.mjs"
```

`SiteConformanceTests` checks schema names, parser enum values, default sections, keys, and fixture
coverage against the C# parser. Recheck the generated schema whenever an annotated config option changes.

GitHub Actions (`.github/workflows/ci.yml`) runs both test suites and the binary and version checks on every push and
pull request. The Pages deploy runs only after they pass.

## Source layout

- `src/Model`: pure logic and parsing.
- `src/Patches`: Harmony patches.
- `src/Rendering`: loading screen UI.
- `src/Game`: game integration.
- `src/Plugin`: plugin entry point, configuration, and logging.
- `tests/`: unit tests, site tests and the shared config fixtures.
- `site/`: the static config editor published to GitHub Pages.

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
