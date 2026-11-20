# FlowFree-Tools

A Unity Editor tool that **generates solvable Flow Free–style puzzle levels** with the [Z3](https://github.com/Z3Prover/z3) constraint solver and writes them to CSV files the game can load. Built to give Game Designers a fast way to produce and validate levels instead of authoring every board by hand.

<!-- TODO: add a short GIF of the Map Generator window producing a batch, e.g. docs/generator.gif -->

Write-up (how it works and what I learned): https://haidang.space/blog/flow-free-level-generator/

## Features

- **Constraint-based generation:** each cell gets an integer color variable and Z3 enforces the rules of a valid board (full fill, every cell connects to 1 or 2 same-color neighbors, exactly two endpoints per color, path-length bounds).
- **Independent validation:** because local constraints cannot rule out stray closed loops, every candidate board is walked in C# and rejected if a color is not one single path between its two endpoints.
- **Reproducible batches:** a seed controls the random variety, so the same seed and settings reproduce a batch.
- **Responsive Editor window:** solving runs on a background thread with a per-solve timeout and a **Stop** button, so the Editor stays usable.
- **Safe output:** each map is written to CSV and read back to check it round-trips before it is kept. Every batch gets its own timestamped folder and a `manifest.txt` recording the settings.
- **Editor-only:** the solver lives in an `Editor` folder and never ships in a game build.

## Requirements

- Unity **2022.3.62f2** (the version this project was created with)
- Windows (the project bundles `Microsoft.Z3.dll` and `libz3.dll` under `Assets/Scripts/Game/_GenMap/Editor/Plugins`)

## Quick start

1. Clone the repo and open it in Unity 2022.3.
2. Open **Tools → FlowFree → Map Generator**.
3. Set the board and click **Generate**.
4. Find the results in `Assets/Resources/Maps/Generated/<yyyyMMdd_HHmmss>/`.

### Settings

| Field | Default | Meaning |
|---|---|---|
| Width / Height | 5 / 5 | Board size (each ≥ 2) |
| Colors | 3 | Number of color paths on the board |
| Maps to generate | 5 | How many levels to produce in the batch |
| Min path length | 2 | Shortest allowed path (a path needs two endpoints) |
| Max path length | 25 | Longest allowed path |
| Random seed | 0 | Seed for reproducible variety |
| Per-solve timeout (ms) | 3000 | Give up on one solve after this long (≥ 100) |
| Reset solver per map | on | Faster; accepted maps do not constrain later ones, so maps may repeat |

Settings are validated before a run. For example, `colors × minLength` must not exceed the number of cells, and `colors × maxLength` must be at least the number of cells so the board can be fully filled.

## Output format

Each batch folder contains one CSV per level plus a manifest:

```
Assets/Resources/Maps/Generated/20260813_113156/
├── 1.csv … 5.csv
└── manifest.txt
```

A level is a solved grid: one color number per cell, one row per line.

```
4,4,4,4,3
2,2,3,3,3
2,5,2,2,2
2,5,1,1,2
2,2,2,2,2
```

`manifest.txt` records the settings used for the batch:

```
batchId=20260813_113156
createdUtc=2026-08-13T04:31:56.2552314Z
width=5
height=5
colors=5
maps=5
minLength=2
maxLength=25
seed=0
perSolveTimeoutMs=3000
resetSolverPerMap=True
```

## Project layout

```
Assets/Scripts/Game/
├── _GenMap/Editor/
│   ├── MapGeneratorWindow.cs   # Editor window, worker thread, progress and Stop
│   ├── MapGenerator.cs         # Z3 model and solve loop
│   ├── GenConfig.cs            # settings and validation
│   ├── PathValidator.cs        # checks a board is one valid path per color
│   ├── CsvMapWriter.cs         # CSV writing and round-trip check
│   └── Plugins/                # Microsoft.Z3.dll, libz3.dll
├── Data/                       # level data
└── View/                       # in-scene board view
```

## Viewing a level

`GameView` has a custom inspector with a **Display Map** button under the level-to-load field, which draws the loaded level in the scene.

<!-- TODO: add a screenshot of a generated level in SampleScene -->

## License

[MIT](LICENSE) © 2026 nguyendang111999

## Author

Nguyen Hai Dang, Unity game developer: [my website](https://nguyendang111999.github.io/haidang-website/)