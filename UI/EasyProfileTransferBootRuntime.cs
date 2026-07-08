using System;
using Godot;

namespace EasyProfileTransfer.UI;

/// <summary>
/// Defers EasyProfileTransfer overlay attach until STS2 boot finishes.
/// </summary>
internal static class EasyProfileTransferBootRuntime
{
    private const double BootAttachDelaySeconds = 4.0;

    private static SceneTree? _tree;
    private static bool _attachScheduled;

    public static void ScheduleBootAttach(SceneTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        _tree = tree;
        if (_attachScheduled)
        {
            return;
        }

        _attachScheduled = true;
        SceneTreeTimer timer = tree.CreateTimer(BootAttachDelaySeconds, processAlways: true);
        timer.Timeout += OnBootAttachTimerElapsed;
    }

    private static void OnBootAttachTimerElapsed()
    {
        if (_tree == null || !GodotObject.IsInstanceValid(_tree.Root))
        {
            GD.PrintErr("[EasyProfileTransfer] Boot attach skipped: scene tree is not valid.");
            return;
        }

        try
        {
            EasyProfileTransferOverlay.Initialize(_tree);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasyProfileTransfer] Boot attach failed: {ex.Message}");
        }
    }
}
