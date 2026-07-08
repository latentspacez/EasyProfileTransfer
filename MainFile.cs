using System;
using Godot;
using EasyProfileTransfer.UI;
using MegaCrit.Sts2.Core.Modding;

namespace EasyProfileTransfer;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    public const string ModId = "EasyProfileTransfer";
    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree tree)
            {
                Logger.Error("EasyProfileTransfer disabled: main loop is not a SceneTree.");
                return;
            }

            EasyProfileTransferBootRuntime.ScheduleBootAttach(tree);
            string modVer = ModVersionInfo.GetInformationalVersion();
            string compiledAgainst = ModVersionInfo.GetCompiledAgainstVersions();
            Logger.Info($"EasyProfileTransfer initialized (version {modVer}, compiled-against {compiledAgainst}).");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasyProfileTransfer] Initialize failed: {ex}");
        }
    }
}
