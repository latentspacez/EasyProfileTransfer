# EasyProfileTransfer

Standalone Slay the Spire 2 mod that lets you copy vanilla profile saves into the modded save tree from the main menu, with a preview of profile stats before overwriting.

## Built against

- EasyProfileTransfer: `0.1.11`
- Slay the Spire 2: `v0.107.1`

## Installation

Subscribe to the Steam Workshop item for `EasyProfileTransfer`.

## Publishing

Use the shared lane-aware script (see `workshop/README.md`):

```bash
E:/Projects/Modding/SlayTheSpire2/Tools/publish-workshop.sh test v0_108_0 E:/Projects/Modding/SlayTheSpire2/TheSpireChronicles/EasyProfileTransfer
E:/Projects/Modding/SlayTheSpire2/Tools/publish-workshop.sh release v0_108_0 E:/Projects/Modding/SlayTheSpire2/TheSpireChronicles/EasyProfileTransfer
```

## Usage

On the fully loaded main menu, use the **Transfer Profile** button near the top-left profile selector. Review vanilla vs modded profile stats, then confirm to overwrite modded saves with your vanilla copies.

## Compatibility

- Compiled-against versions and artifact naming (`EasyProfileTransfer_<mod_version>_StS2_<compiled_versions>.zip`) are controlled by `Directory.Build.props`.
- No other mods are required.
