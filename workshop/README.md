# EasyProfileTransfer Workshop source files

This folder contains **source** files for Steam Workshop publishing. The uploader workspace is generated under `publish/workshop/`.

`dotnet publish EasyProfileTransfer.csproj -c Release -p:GameVersionFolder=v0_108_0` creates an uploader-ready workspace at:

```text
publish/workshop/
```

Upload it with the shared lane-aware script:

```bash
E:/Projects/Modding/SlayTheSpire2/Tools/publish-workshop.sh test v0_108_0 E:/Projects/Modding/SlayTheSpire2/TheSpireChronicles/EasyProfileTransfer
E:/Projects/Modding/SlayTheSpire2/Tools/publish-workshop.sh release v0_108_0 E:/Projects/Modding/SlayTheSpire2/TheSpireChronicles/EasyProfileTransfer
```

## Source files in `workshop/`

Keep only:

```text
workshop.json.template
README.md
mod_id.test.txt
mod_id.release.txt
```

Do not put `image.png` here. The listing thumbnail is copied into `publish/workshop/image.png` during publish from `screenshots/workshop_preview.png` (see `WorkshopPreviewImagePath` in `EasyProfileTransfer.csproj`).

## Workshop lanes

The `test` lane publishes a private Workshop item with a `Test ` title prefix.
The `release` lane publishes the final public Workshop item without the prefix.

Workshop item IDs are stored in `mod_id.test.txt` and `mod_id.release.txt`.
The shared script copies the selected lane ID into the generated workspace as `mod_id.txt`.
If a lane has no saved ID yet, the uploader creates a new item and the script saves the generated ID back to that lane's file.
