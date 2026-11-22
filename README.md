# Auto Edge Markings

**English** | [Русский](README.ru.md)

Automatic lane markings for ordinary town roads in **Cities: Skylines II** — no external asset dependencies, compatible with Road Builder.

## Features

- **Curb-side edge line** — ordinary city roads with 3 m lanes (car, tram and public-transport lanes) get the edge line that highways already have.
- **US yellow left line** — in North American-theme cities, one-way and divided roads (highways included) get a yellow line along the left, median-side edge. Optional, on by default; European themes are unaffected.
- **Parallel parking markings** — a line along parallel street-parking zones with a tick at each end of the block.
- Each feature can be switched off in the options screen and offers vanilla or built-in crisp line styles.
- When a new city is started from a map, the map's existing roads are rebuilt so they get the markings too.

## Known limitations

- **Purely visual.** Markings never change how vehicles drive.
- Style changes take effect after restarting the game and apply to roads built or rebuilt afterwards; roads already in a save keep the markings they were saved with.
- Roads without the matching vanilla lanes (for example some asymmetric or one-way 3-lane variants without a `Parking Lane 2`) stay unmarked.

## How it works

The mod clones vanilla marking prefabs, gives them its own meshes and host lanes, and registers them before the game initializes prefabs. The game's own `SecondaryLaneSystem` then places them like any vanilla marking, so vehicles, traffic lanes and Road Builder roads are untouched. Built-in crisp line textures are generated procedurally in memory.

Log: `Logs/AutoEdgeMarkings.Mod.log` (a few dozen lines per session). For more detail add `"VerboseLogging": true` to `AutoEdgeMarkings.coc` and restart the game.

## Building

Requires the official CS2 modding toolchain (`CSII_TOOLPATH` set up by the game's mod project wizard) and the .NET 6 runtime for the toolchain's post-build step.

```powershell
dotnet build src/AutoEdgeMarkings/AutoEdgeMarkings.csproj -c Release
```

The build compiles the C# systems and deploys them to the local `Mods/AutoEdgeMarkings` folder.

## Project layout

- `src/AutoEdgeMarkings/`
  - `Systems/Prefabs/` — clones vanilla marking prefabs for the edge, yellow-left and parking lines, plus the built-in line meshes and textures;
  - `Systems/Emission/` — rebuilds the map's roads once when a new city is started;
  - `Localization/` — options screen text (English, Russian);
  - `Diagnostics/` — developer-only prefab dump, Debug builds only.

## Credits

- Based on [Town Road Lane](https://github.com/mxerf/town-road-lane) by **mxerf**. This version removes the intersection marking editor and area fills, keeps only the automatic markings, and makes the mod standalone (no G87 / ExtraAssetsImporter dependency).

## License

[GPL-3.0](LICENSE)
