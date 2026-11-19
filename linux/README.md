# FModel for Linux

A native Linux build of [FModel](https://github.com/4sval/FModel) that tracks the official repository
commit for commit. No Wine, no Proton: the UI runs on [Avalonia](https://avaloniaui.net/), the 3D viewer
(Snooper) on OpenGL through GLFW, and everything else is FModel's own code.

## Getting it

Grab the latest `linux-*` release from the Releases page:

- **FModel-x86_64.AppImage**: `chmod +x FModel-x86_64.AppImage && ./FModel-x86_64.AppImage`.
  Everything is bundled, including the .NET runtime.
- **FModel-linux-x64.tar.gz**: the same build as a plain folder. Run `./FModel` from it, or
  `./install.sh` to put it in `~/.local` with a desktop entry.

Settings live in `~/.config/FModel`. Exports go next to the program when that folder is writable
(the portable layout Windows users know), otherwise to `~/.local/share/FModel/Output`. The AppImage
always uses the latter. You can change it in the settings like on Windows.

The 3D viewer needs a GPU driver with OpenGL 4.6 and bindless textures (current Mesa on AMD and Intel,
or the NVIDIA driver). On Wayland it runs through XWayland; `FMODEL_GLFW_WAYLAND=1` forces native Wayland.

### Things that work differently than on Windows

- **Game detection**: the auto-detected game list reads the Windows registry and Windows launcher
  files, so on Linux you add games with the folder picker or by dropping the `Paks` folder on the window.
- **Unversioned games need mappings**, same as on Windows. If assets fail with *"Package has unversioned
  properties but mapping file is missing"*, add a `.usmap` for the game in the directory settings.
- **Copying images** uses `wl-copy` (Wayland) or `xclip` (X11) when installed, so the image stays on the
  clipboard after FModel closes.
- **Updates**: FModel tells you when a Linux build of a newer official commit is out. Inside the AppImage,
  "Update" replaces the AppImage file in place; elsewhere it opens the releases page.

## Building

Needs the .NET 10 SDK, `cmake` and a C/C++ compiler.

```sh
git clone --recursive https://github.com/Official-Husko/FModel-Linux
cd FModel-Linux
git remote add upstream https://github.com/4sval/FModel.git && git fetch upstream

dotnet run --project linux/FModel.Linux.csproj   # run from source
linux/scripts/build.sh                           # linux/out/FModel-linux-x64(.tar.gz)
linux/scripts/build-appimage.sh                  # linux/out/FModel-x86_64.AppImage
```

The `upstream` remote is optional. With it, the build records which official commit it corresponds to,
which is what the update check compares against.

## How it stays in sync

The rule is simple: **upstream files are not edited here.** `FModel/`, `CUE4Parse/` and `Snooper/` are
exactly what 4sval/FModel has, and everything Linux lives in `linux/`. That is why
`linux/scripts/sync-upstream.sh` (or the scheduled CI job) can merge the official repository without
conflicts, and why this fork never has to fall behind.

`linux/FModel.Linux.csproj` compiles upstream's sources directly and fills the gaps:

| Folder | What it is |
| --- | --- |
| `src/compat/` | The WPF, AdonisUI, AvalonEdit, Ookii and AutoUpdater APIs FModel uses, implemented on Avalonia. This is why the view models, services and exporters compile on Linux without changes. |
| `src/FModel/` | Avalonia views (`.axaml`) and the few WPF-only classes, laid out like `FModel/`. A file here replaces the upstream file with the same path. |
| `src/FModel.Linux/` | Linux-only additions: build info, output folder and tool setup, the self-test. |
| `native/win32compat.c` | Linux versions of the Win32 functions FModel and Snooper call directly (monitor refresh rate, memory status, OS name). |
| `patches/` | Patch queue for the submodules, for fixes that haven't landed upstream yet. Empty is the goal. |

Two upstream files aren't ported by hand at all: `Icons.xaml` and `Colors.xaml` are converted to Avalonia
at build time, so new icons and colours arrive with every sync.

### Syncing by hand

```sh
linux/scripts/sync-upstream.sh
```

This merges `upstream/dev`, updates the submodules, re-applies the patch queue, builds, and prints the
**drift report**: the upstream changes that need a matching change on the Linux side. That is:

- WPF views (`*.xaml`, `*.xaml.cs`) that changed, so the Avalonia version can be updated
- upstream files that are replaced or excluded on Linux and changed
- NuGet packages added or bumped in `FModel/FModel.csproj`
- new P/Invokes into Windows libraries

Port what it lists, then put the upstream commit it reports into `linux/UPSTREAM_PORTED` and commit.
`linux/scripts/drift.sh` prints the report again at any time.

### What CI does

`.github/workflows/fmodel-linux.yml` runs every six hours:

1. merges the latest official `dev`
2. builds the AppImage and the tarball
3. if the build passes, pushes the merge to `dev` and publishes a `linux-*` release; the release notes
   carry the upstream commit (the in-app update check reads it) and the drift report
4. if the merge conflicts or the build breaks, `dev` stays as it was, the merge is parked on an
   `upstream-sync/*` branch, and an issue explains what happened
5. if the drift report lists UI to port, it keeps an issue updated with the list

Upstream's own `qa.yml` and `main.yml` workflows build the Windows version. They are harmless here, but
you can disable them in the Actions tab of the fork.

### Testing exports without clicking around

The Linux build has an unattended export check that drives FModel's real commands (the same ones the
right-click menus use), counts what lands on disk, and reports every error, including the ones bulk exports
otherwise swallow:

```sh
FMODEL_SELFTEST=1 FMODEL_SELFTEST_FOLDER=Game/Content/Characters ./FModel
FMODEL_SELFTEST=1 FMODEL_SELFTEST_FOLDER=Game/Content FMODEL_SELFTEST_ACTIONS=list ./FModel
```

It uses the game configured in the settings, writes `Output/Logs/SelfTest-*.txt`, and exits with the
number of failed exports.

## Credits

The Avalonia views and several building blocks (audio playback, file dialogs, the virtualized explorer)
started from [mcbabel's Linux port](https://github.com/mcbabel/FModel), with ideas from
[r6e/FModel-Linux](https://github.com/r6e/FModel-Linux). FModel itself is by
[4sval and contributors](https://github.com/4sval/FModel/graphs/contributors). Everything here is GPL-3.0,
like FModel.
