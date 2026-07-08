namespace EasyProfileTransfer.Data;

internal static class EasyProfileTransferLog
{
    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new("EasyProfileTransfer", MegaCrit.Sts2.Core.Logging.LogType.Generic);
}
