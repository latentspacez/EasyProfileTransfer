using System;
using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Saves;

namespace EasyProfileTransfer.Data;

/// <summary>
/// Copies the official vanilla-to-modded save file set, overwriting modded targets.
/// </summary>
public static class ProfileTransferService
{
    public static ProfileTransferResult TransferAll()
    {
        var result = new ProfileTransferResult();
        string accountBase = ProfileTransferPaths.GetAccountBaseGodotPath();

        try
        {
            string moddedRoot = accountBase.PathJoin("modded");
            DirAccess.MakeDirRecursiveAbsolute(moddedRoot);

            MirrorFile(accountBase,
                ProfileTransferPaths.GetRelativeProfileSavePath(modded: false),
                ProfileTransferPaths.GetRelativeProfileSavePath(modded: true),
                result);

            for (int profileId = 1; profileId <= ProfileTransferPaths.MaxProfileCount; profileId++)
            {
                MirrorFile(accountBase,
                    ProfileTransferPaths.GetRelativeProgressPath(profileId, modded: false),
                    ProfileTransferPaths.GetRelativeProgressPath(profileId, modded: true),
                    result);
                MirrorFile(accountBase,
                    ProfileTransferPaths.GetRelativeRunSavePath(profileId, ProfileTransferPaths.CurrentRunSaveFileName, modded: false),
                    ProfileTransferPaths.GetRelativeRunSavePath(profileId, ProfileTransferPaths.CurrentRunSaveFileName, modded: true),
                    result);
                MirrorFile(accountBase,
                    ProfileTransferPaths.GetRelativeRunSavePath(profileId, ProfileTransferPaths.MultiplayerRunSaveFileName, modded: false),
                    ProfileTransferPaths.GetRelativeRunSavePath(profileId, ProfileTransferPaths.MultiplayerRunSaveFileName, modded: true),
                    result);
                MirrorFile(accountBase,
                    ProfileTransferPaths.GetRelativePrefsPath(profileId, modded: false),
                    ProfileTransferPaths.GetRelativePrefsPath(profileId, modded: true),
                    result);
                MirrorHistoryDirectory(accountBase, profileId, result);
            }

            result.Success = result.FilesFailed == 0;
            EasyProfileTransferLog.Logger.Info(
                $"Profile transfer finished: copied={result.FilesCopied} removed={result.FilesRemoved} skipped={result.FilesSkipped} failed={result.FilesFailed}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.FilesFailed++;
            result.Errors.Add(ex.Message);
            EasyProfileTransferLog.Logger.Error($"Profile transfer failed: {ex.Message}");
        }

        return result;
    }

    public static async System.Threading.Tasks.Task TryOverwriteCloudWithLocalAsync()
    {
        try
        {
            SaveManager? saveManager = SaveManager.Instance;
            if (saveManager == null)
            {
                EasyProfileTransferLog.Logger.Warn("Cloud overwrite skipped: SaveManager.Instance is null.");
                return;
            }

            await saveManager.OverwriteCloudWithLocal();
            EasyProfileTransferLog.Logger.Info("Triggered cloud overwrite with local modded saves.");
        }
        catch (Exception ex)
        {
            EasyProfileTransferLog.Logger.Warn($"Cloud overwrite failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Reloads prefs/progress for the active profile from disk after files were overwritten.
    /// Mirrors the in-memory refresh done when switching profiles in <c>NProfileButton</c>.
    /// </summary>
    public static bool TryReloadActiveProfileSaveData(
        out ReadSaveResult<PrefsSave> prefsReadResult,
        out ReadSaveResult<SerializableProgress> progressReadResult)
    {
        prefsReadResult = default!;
        progressReadResult = default!;

        try
        {
            SaveManager? saveManager = SaveManager.Instance;
            if (saveManager == null || !saveManager.IsProfileInitialized)
            {
                EasyProfileTransferLog.Logger.Warn("Profile reload skipped: SaveManager is not ready.");
                return false;
            }

            prefsReadResult = saveManager.InitPrefsData();
            progressReadResult = saveManager.InitProgressData();
            EasyProfileTransferLog.Logger.Info(
                $"Reloaded active profile {saveManager.CurrentProfileId} save data after transfer.");
            return true;
        }
        catch (Exception ex)
        {
            EasyProfileTransferLog.Logger.Warn($"Profile reload failed: {ex.Message}");
            return false;
        }
    }

    private static void MirrorHistoryDirectory(string accountBase, int profileId, ProfileTransferResult result)
    {
        string sourceRelative = ProfileTransferPaths.GetRelativeHistoryDir(profileId, modded: false);
        string targetRelative = ProfileTransferPaths.GetRelativeHistoryDir(profileId, modded: true);
        string sourceDir = accountBase.PathJoin(NormalizeRelativePath(sourceRelative));
        string targetDir = accountBase.PathJoin(NormalizeRelativePath(targetRelative));
        DirAccess.MakeDirRecursiveAbsolute(targetDir);

        var sourceFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DirAccess? sourceAccess = DirAccess.Open(sourceDir);
        if (sourceAccess != null)
        {
            foreach (string file in sourceAccess.GetFiles())
            {
                if (ShouldMirrorHistoryFile(file))
                {
                    sourceFiles.Add(file);
                }
            }
        }

        foreach (string file in sourceFiles)
        {
            string sourceFile = NormalizeRelativePath(sourceRelative).PathJoin(file);
            string targetFile = NormalizeRelativePath(targetRelative).PathJoin(file);
            MirrorFile(accountBase, sourceFile, targetFile, result);
        }
    }

    private static bool ShouldMirrorHistoryFile(string file)
    {
        return !string.IsNullOrWhiteSpace(file)
               && file.EndsWith(".run", StringComparison.OrdinalIgnoreCase)
               && !file.EndsWith(".corrupt", StringComparison.OrdinalIgnoreCase)
               && !file.EndsWith(".backup", StringComparison.OrdinalIgnoreCase);
    }

    private static void MirrorFile(string accountBase, string sourceRelative, string targetRelative, ProfileTransferResult result)
    {
        string sourcePath = accountBase.PathJoin(NormalizeRelativePath(sourceRelative));
        string targetPath = accountBase.PathJoin(NormalizeRelativePath(targetRelative));
        if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            result.FilesFailed++;
            string samePathMessage = $"Source and target resolve to the same path: {sourceRelative}";
            result.Errors.Add(samePathMessage);
            EasyProfileTransferLog.Logger.Error($"Profile transfer copy failed: {samePathMessage}");
            return;
        }

        if (!Godot.FileAccess.FileExists(sourcePath))
        {
            if (!Godot.FileAccess.FileExists(targetPath)
                && !Godot.FileAccess.FileExists(accountBase.PathJoin(NormalizeRelativePath(targetRelative + ".backup"))))
            {
                result.FilesSkipped++;
            }

            return;
        }

        DirAccess.MakeDirRecursiveAbsolute(targetPath.GetBaseDir());
        Error error = DirAccess.CopyAbsolute(sourcePath, targetPath);
        if (error == Error.Ok)
        {
            result.FilesCopied++;
            Log.Info($"EasyProfileTransfer copied {sourceRelative} -> {targetRelative}");
            return;
        }

        result.FilesFailed++;
        string message = $"{sourceRelative} -> {targetRelative}: {error}";
        result.Errors.Add(message);
        EasyProfileTransferLog.Logger.Error($"Profile transfer copy failed: {message}");
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }
}
