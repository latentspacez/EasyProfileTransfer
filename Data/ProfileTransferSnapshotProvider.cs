using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Godot;

namespace EasyProfileTransfer.Data;

public static class ProfileTransferSnapshotProvider
{
    private static readonly JsonDocumentOptions JsonReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static ProfileTransferComparison LoadComparison()
    {
        var vanilla = new ProfileTransferRow[ProfileTransferPaths.MaxProfileCount];
        var modded = new ProfileTransferRow[ProfileTransferPaths.MaxProfileCount];

        for (int profileId = 1; profileId <= ProfileTransferPaths.MaxProfileCount; profileId++)
        {
            vanilla[profileId - 1] = LoadProfileRow(profileId, modded: false);
            modded[profileId - 1] = LoadProfileRow(profileId, modded: true);
        }

        return new ProfileTransferComparison
        {
            Vanilla = vanilla,
            Modded = modded,
        };
    }

    private static ProfileTransferRow LoadProfileRow(int profileId, bool modded)
    {
        string progressRelative = ProfileTransferPaths.GetRelativeProgressPath(profileId, modded);
        string singleRunRelative = ProfileTransferPaths.GetRelativeRunSavePath(
            profileId, ProfileTransferPaths.CurrentRunSaveFileName, modded);
        string mpRunRelative = ProfileTransferPaths.GetRelativeRunSavePath(
            profileId, ProfileTransferPaths.MultiplayerRunSaveFileName, modded);
        string historyRelative = ProfileTransferPaths.GetRelativeHistoryDir(profileId, modded);

        bool hasProgress = ProfileTransferPaths.SaveFileExists(progressRelative);
        int score = -1;
        int bestAscension = -1;
        if (hasProgress)
        {
            string progressPath = ProfileTransferPaths.JoinAccountAbsolutePath(progressRelative);
            TryReadProgressMeta(progressPath, out score, out bestAscension);
        }

        return new ProfileTransferRow
        {
            ProfileId = profileId,
            HasProgressSave = hasProgress,
            CurrentScore = score,
            BestAscension = bestAscension,
            RunHistoryCount = CountRunHistoryFiles(historyRelative),
            HasSinglePlayerRun = ProfileTransferPaths.SaveFileExists(singleRunRelative),
            HasMultiplayerRun = ProfileTransferPaths.SaveFileExists(mpRunRelative),
        };
    }

    private static void TryReadProgressMeta(string progressPath, out int score, out int bestAscension)
    {
        score = -1;
        bestAscension = -1;
        try
        {
            string json = File.ReadAllText(progressPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            using JsonDocument doc = JsonDocument.Parse(json, JsonReadOptions);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            score = ReadInt(root, "current_score", "currentScore", "score");
            int maxMultiplayer = ReadInt(root, "max_multiplayer_ascension", "maxMultiplayerAscension");
            int bestCharacterAscension = ReadBestCharacterAscension(root);
            bestAscension = Math.Max(maxMultiplayer, bestCharacterAscension);
        }
        catch (Exception ex)
        {
            EasyProfileTransferLog.Logger.Warn($"Failed to read progress metadata from {progressPath}: {ex.Message}");
        }
    }

    private static int ReadBestCharacterAscension(JsonElement root)
    {
        if (!TryGetPropertyIgnoreCase(root, "character_stats", out JsonElement characterStats)
            || characterStats.ValueKind != JsonValueKind.Array)
        {
            return -1;
        }

        int best = -1;
        foreach (JsonElement row in characterStats.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            int maxAscension = ReadInt(row, "max_ascension", "maxAscension");
            if (maxAscension > best)
            {
                best = maxAscension;
            }
        }

        return best;
    }

    private static int CountRunHistoryFiles(string historyRelative)
    {
        string historyDir = ProfileTransferPaths.JoinAccountGodotPath(historyRelative);
        DirAccess? dirAccess = DirAccess.Open(historyDir);
        if (dirAccess == null)
        {
            return 0;
        }

        try
        {
            int count = 0;
            foreach (string file in dirAccess.GetFiles())
            {
                if (file.EndsWith(".run", StringComparison.OrdinalIgnoreCase)
                    && !file.EndsWith(".corrupt", StringComparison.OrdinalIgnoreCase)
                    && !file.EndsWith(".backup", StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }
        catch (Exception ex)
        {
            EasyProfileTransferLog.Logger.Warn($"Failed to enumerate run history in {historyDir}: {ex.Message}");
            return 0;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement parent, string name, out JsonElement value)
    {
        foreach (JsonProperty property in parent.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static int ReadInt(JsonElement root, params string[] names)
    {
        foreach (string name in names)
        {
            if (!TryGetPropertyIgnoreCase(root, name, out JsonElement found))
            {
                continue;
            }

            if (found.ValueKind == JsonValueKind.Number)
            {
                if (found.TryGetInt32(out int iv))
                {
                    return iv;
                }

                if (found.TryGetDouble(out double dv))
                {
                    return (int)Math.Round(dv, MidpointRounding.AwayFromZero);
                }
            }

            if (found.ValueKind == JsonValueKind.String
                && int.TryParse(found.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }
        }

        return -1;
    }
}
