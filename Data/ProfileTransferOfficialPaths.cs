using System;
using System.Linq;
using System.Reflection;

namespace EasyProfileTransfer.Data;

/// <summary>
/// Optional reflection bridge to official STS2 save path helpers when present (v0.108+).
/// Falls back to manual relative paths when unavailable.
/// </summary>
internal static class ProfileTransferOfficialPaths
{
    public static string? TryGetProfileSavePath(bool modded)
    {
        return InvokeBoolPath("MegaCrit.Sts2.Core.Saves.Managers.ProfileSaveManager", "GetProfileSavePath", modded);
    }

    public static string? TryGetProgressPath(int profileId, bool modded)
    {
        return InvokeIntBoolPath("MegaCrit.Sts2.Core.Saves.Managers.ProgressSaveManager", "GetProgressPathForProfile", profileId, modded);
    }

    public static string? TryGetPrefsPath(int profileId, bool modded)
    {
        return InvokeIntBoolPath("MegaCrit.Sts2.Core.Saves.Managers.PrefsSaveManager", "GetPrefsPath", profileId, modded);
    }

    public static string? TryGetRunSavePath(int profileId, string fileName, bool modded)
    {
        return InvokeIntStringBoolPath("MegaCrit.Sts2.Core.Saves.Managers.RunSaveManager", "GetRunSavePath", profileId, fileName, modded);
    }

    public static string? TryGetHistoryDir(int profileId, bool modded)
    {
        return InvokeIntBoolPath("MegaCrit.Sts2.Core.Saves.Managers.RunHistorySaveManager", "GetHistoryPath", profileId, modded);
    }

    private static string? InvokeBoolPath(string typeName, string methodName, bool modded)
    {
        MethodInfo? method = GetStaticMethod(typeName, methodName, typeof(bool))
            ?? GetStaticMethod(typeName, methodName, typeof(bool?));
        if (method == null)
        {
            return null;
        }

        try
        {
            object?[] args = method.GetParameters().Length == 0
                ? Array.Empty<object?>()
                : new object?[] { (bool?)modded };
            return method.Invoke(null, args) as string;
        }
        catch
        {
            return null;
        }
    }

    private static string? InvokeIntBoolPath(string typeName, string methodName, int profileId, bool modded)
    {
        MethodInfo? method = GetStaticMethod(typeName, methodName, typeof(int), typeof(bool))
            ?? GetStaticMethod(typeName, methodName, typeof(int), typeof(bool?));
        if (method == null)
        {
            return null;
        }

        try
        {
            return method.Invoke(null, new object?[] { profileId, (bool?)modded }) as string;
        }
        catch
        {
            return null;
        }
    }

    private static string? InvokeIntStringBoolPath(string typeName, string methodName, int profileId, string fileName, bool modded)
    {
        MethodInfo? method = GetStaticMethod(typeName, methodName, typeof(int), typeof(string), typeof(bool))
            ?? GetStaticMethod(typeName, methodName, typeof(int), typeof(string), typeof(bool?));
        if (method == null)
        {
            return null;
        }

        try
        {
            return method.Invoke(null, new object?[] { profileId, fileName, (bool?)modded }) as string;
        }
        catch
        {
            return null;
        }
    }

    private static MethodInfo? GetStaticMethod(string typeName, string methodName, params Type[] parameterTypes)
    {
        Type? type = Type.GetType($"{typeName}, sts2");
        if (type == null)
        {
            return null;
        }

        return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method => method.Name == methodName && ParametersMatch(method, parameterTypes));
    }

    private static bool ParametersMatch(MethodInfo method, Type[] expectedTypes)
    {
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length != expectedTypes.Length)
        {
            return false;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            if (!TypesCompatible(expectedTypes[i], parameters[i].ParameterType))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TypesCompatible(Type expected, Type actual)
    {
        if (expected == actual)
        {
            return true;
        }

        if (expected == typeof(bool) && actual == typeof(bool?))
        {
            return true;
        }

        return false;
    }
}
