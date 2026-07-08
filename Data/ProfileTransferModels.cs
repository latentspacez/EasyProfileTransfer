namespace EasyProfileTransfer.Data;

public sealed class ProfileTransferRow
{
    public int ProfileId { get; init; }
    public bool HasProgressSave { get; init; }
    public int CurrentScore { get; init; } = -1;
    public int BestAscension { get; init; } = -1;
    public int RunHistoryCount { get; init; }
    public bool HasSinglePlayerRun { get; init; }
    public bool HasMultiplayerRun { get; init; }

    /// <summary>
    /// Matches <c>NProfileButton</c>: a profile slot is unused when vanilla <c>progress.save</c> is missing.
    /// </summary>
    public bool IsEmptyProfileSlot => !HasProgressSave;

    public bool HasAnyData =>
        HasProgressSave || HasSinglePlayerRun || HasMultiplayerRun || RunHistoryCount > 0;
}

public sealed class ProfileTransferComparison
{
    public ProfileTransferRow[] Vanilla { get; init; } = [];
    public ProfileTransferRow[] Modded { get; init; } = [];
}

public sealed class ProfileTransferResult
{
    public bool Success { get; set; }
    public int FilesCopied { get; set; }
    public int FilesRemoved { get; set; }
    public int FilesSkipped { get; set; }
    public int FilesFailed { get; set; }
    public List<string> Errors { get; } = new();
}
