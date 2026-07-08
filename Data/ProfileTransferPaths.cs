using System;
using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Saves;

namespace EasyProfileTransfer.Data;

/// <summary>
/// Resolves vanilla and modded profile save paths without v0.108-only forceModState helpers.
/// </summary>
public static class ProfileTransferPaths
{
    public const int MaxProfileCount = 3;
    public const string ProfileSaveFileName = "profile.save";
    public const string ProgressSaveFileName = "progress.save";
    public const string PrefsSaveFileName = "prefs.save";
    public const string CurrentRunSaveFileName = "current_run.save";
    public const string MultiplayerRunSaveFileName = "current_run_mp.save";
    public const string HistoryDirName = "history";

    public static string GetAccountBaseGodotPath()
    {
        return UserDataPathProvider.GetAccountScopedBasePath(null);
    }

    public static string GetAccountBaseAbsolutePath()
    {
        return GlobalizePath(GetAccountBaseGodotPath());
    }

    public static string GetRelativeAccountDir(bool modded)
    {
        return modded ? "modded" : string.Empty;
    }

    public static string GetRelativeProfileDir(int profileId, bool modded)
    {
        string profileDir = $"profile{profileId}";
        string accountDir = GetRelativeAccountDir(modded);
        return string.IsNullOrEmpty(accountDir) ? profileDir : Path.Combine(accountDir, profileDir);
    }

    public static string GetRelativeProfileSavePath(bool modded)
    {
        return ProfileTransferOfficialPaths.TryGetProfileSavePath(modded)
            ?? BuildRelativeProfileSavePath(modded);
    }

    private static string BuildRelativeProfileSavePath(bool modded)
    {
        string accountDir = GetRelativeAccountDir(modded);
        return string.IsNullOrEmpty(accountDir) ? ProfileSaveFileName : Path.Combine(accountDir, ProfileSaveFileName);
    }

    public static string GetRelativeProgressPath(int profileId, bool modded)
    {
        return ProfileTransferOfficialPaths.TryGetProgressPath(profileId, modded)
            ?? Path.Combine(GetRelativeProfileDir(profileId, modded), UserDataPathProvider.SavesDir, ProgressSaveFileName);
    }

    public static string GetRelativePrefsPath(int profileId, bool modded)
    {
        return ProfileTransferOfficialPaths.TryGetPrefsPath(profileId, modded)
            ?? Path.Combine(GetRelativeProfileDir(profileId, modded), UserDataPathProvider.SavesDir, PrefsSaveFileName);
    }

    public static string GetRelativeRunSavePath(int profileId, string fileName, bool modded)
    {
        return ProfileTransferOfficialPaths.TryGetRunSavePath(profileId, fileName, modded)
            ?? Path.Combine(GetRelativeProfileDir(profileId, modded), UserDataPathProvider.SavesDir, fileName);
    }

    public static string GetRelativeHistoryDir(int profileId, bool modded)
    {
        return ProfileTransferOfficialPaths.TryGetHistoryDir(profileId, modded)
            ?? Path.Combine(GetRelativeProfileDir(profileId, modded), UserDataPathProvider.SavesDir, HistoryDirName);
    }

    public static string JoinAccountGodotPath(string relativePath)
    {
        string basePath = GetAccountBaseGodotPath();
        if (string.IsNullOrEmpty(relativePath))
        {
            return basePath;
        }

        return basePath.PathJoin(NormalizeRelativePath(relativePath));
    }

    public static string JoinAccountAbsolutePath(string relativePath)
    {
        return GlobalizePath(JoinAccountGodotPath(relativePath));
    }

    public static bool SaveFileExists(string relativePath)
    {
        return Godot.FileAccess.FileExists(JoinAccountGodotPath(relativePath));
    }

    public static string GlobalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string normalized = path;
        if (normalized.Contains("://", StringComparison.Ordinal))
        {
            normalized = ProjectSettings.GlobalizePath(normalized);
        }

        normalized = normalized.Replace('/', Path.DirectorySeparatorChar);
        try
        {
            return Path.GetFullPath(normalized);
        }
        catch
        {
            return normalized;
        }
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }
}
