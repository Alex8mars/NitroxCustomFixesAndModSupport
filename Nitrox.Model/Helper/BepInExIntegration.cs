using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;

namespace Nitrox.Model.Helper;

public static class BepInExIntegration
{
    private const string BEPINEX_DIRECTORY_NAME = "BepInEx";
    private const string BEPINEX_PRELOADER_NAME = "BepInEx.Preloader.dll";
    private const string BEPINEX_CORLIB_DIRECTORY = "unstripped_corlib";
    private const string WINHTTP_DLL_NAME = "winhttp.dll";
    private const string WINEDLLOVERRIDES = "WINEDLLOVERRIDES";
    private const string DOORSTOP_DLL_SEARCH_DIRS = "DOORSTOP_DLL_SEARCH_DIRS";

    public static bool IsInstalled(string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            return false;
        }

        return Directory.Exists(Path.Combine(gameRoot, BEPINEX_DIRECTORY_NAME))
               || File.Exists(Path.Combine(gameRoot, WINHTTP_DLL_NAME));
    }

    public static void ApplyEnvironment(IDictionary<string, string> environment, string? gameRoot)
    {
        ApplyEnvironmentInternal(
            gameRoot,
            key => environment.TryGetValue(key, out string overrides) ? overrides : null,
            (key, value) => environment[key] = value
        );
    }

    private static bool ShouldEnableForPlatform(string? gameRoot)
    {
        ApplyEnvironmentInternal(
            gameRoot,
            key => environment.ContainsKey(key) ? environment[key] : null,
            (key, value) => environment[key] = value
        );
    }

    private static void ApplyEnvironmentInternal(
        string? gameRoot,
        Func<string, string?> getValue,
        Action<string, string> setValue)
    {
        if (!ShouldEnableForPlatform(gameRoot))
        {
            yield break;
        }

        string coreDirectory = Path.Combine(bepInExRoot, "core");
        string preloaderPath = Path.Combine(coreDirectory, BEPINEX_PRELOADER_NAME);
        if (File.Exists(preloaderPath))
        {
            yield return new("DOORSTOP_INVOKE_DLL_PATH", ToWindowsPath(preloaderPath));
        }

        setValue("DOORSTOP_ENABLE", "TRUE");

        string overrides = getValue(WINEDLLOVERRIDES) ?? string.Empty;
        setValue(WINEDLLOVERRIDES, GetWinHttpOverrides(overrides));

        foreach (KeyValuePair<string, string> bepinexVar in GetBepInExDoorstopVariables(gameRoot))
        {
            setValue(bepinexVar.Key, bepinexVar.Value);
        }
    }

    private static string? GetBepInExRoot(string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            return null;
        }

        string root = Path.Combine(gameRoot, BEPINEX_DIRECTORY_NAME);
        return Directory.Exists(root) ? root : null;
    }

    private static string ToWindowsPath(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return path;
        }

        string fullPath = Path.GetFullPath(path);
        return Path.DirectorySeparatorChar == '\\'
            ? fullPath
            : $"Z:{fullPath.Replace(Path.DirectorySeparatorChar, '\\')}";
    }

    private static IEnumerable<KeyValuePair<string, string>> GetBepInExDoorstopVariables(string? gameRoot)
    {
        string? bepInExRoot = GetBepInExRoot(gameRoot);
        if (bepInExRoot == null)
        {
            yield break;
        }

        string preloaderPath = Path.Combine(bepInExRoot, "core", BEPINEX_PRELOADER_NAME);
        if (File.Exists(preloaderPath))
        {
            yield return new("DOORSTOP_INVOKE_DLL_PATH", ToWindowsPath(preloaderPath));
        }

        string corlibOverride = Path.Combine(bepInExRoot, "core", BEPINEX_CORLIB_DIRECTORY);
        if (Directory.Exists(corlibOverride))
        {
            yield return new("DOORSTOP_CORLIB_OVERRIDE_PATH", ToWindowsPath(corlibOverride));
        }
    }

    private static string? GetBepInExRoot(string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            return null;
        }

        string root = Path.Combine(gameRoot, BEPINEX_DIRECTORY_NAME);
        return Directory.Exists(root) ? root : null;
    }

    private static string ToWindowsPath(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return path;
        }

        string fullPath = Path.GetFullPath(path);
        return Path.DirectorySeparatorChar == '\\'
            ? fullPath
            : $"Z:{fullPath.Replace(Path.DirectorySeparatorChar, '\\')}";
    }

    private static string GetWinHttpOverrides(string? existingOverrides)
    {
        const string winHttpOverride = "winhttp=n,b";

        if (string.IsNullOrWhiteSpace(existingOverrides))
        {
            return winHttpOverride;
        }

        // Replace Contains() with IndexOf() for .NET Framework compatibility
        if (existingOverrides.IndexOf("winhttp", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return existingOverrides;
        }

        // Replace char separator with string separator
        return string.Join(";", existingOverrides, winHttpOverride);
    }

    private static string? BuildDllSearchDirs(string? existingValue, params string[] directories)
    {
        List<string> paths = new();

        if (!string.IsNullOrWhiteSpace(existingValue))
        {
            paths.Add(existingValue);
        }

        foreach (string directory in directories)
        {
            if (Directory.Exists(directory))
            {
                paths.Add(ToWindowsPath(directory));
            }
        }

        return paths.Count == 0 ? null : string.Join(";", paths);
    }
}
