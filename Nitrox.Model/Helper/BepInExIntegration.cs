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
    private const string BEPINEX_DOORSTOP_LIB_DIRECTORY = "doorstop_libs";
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

    public static void ApplyEnvironment(StringDictionary environment, string? gameRoot)
    {
        ApplyEnvironmentForStringDictionary(environment, gameRoot);
    }

    public static void ApplyEnvironmentForStringDictionary(StringDictionary environment, string? gameRoot)
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
            return;
        }

        setValue("DOORSTOP_ENABLED", "TRUE");
        // Preserve compatibility with doorstop builds that still read the legacy flag
        setValue("DOORSTOP_ENABLE", "TRUE");

        string overrides = getValue(WINEDLLOVERRIDES) ?? string.Empty;
        setValue(WINEDLLOVERRIDES, GetWinHttpOverrides(overrides));

        foreach (KeyValuePair<string, string> bepinexVar in GetBepInExDoorstopVariables(gameRoot, getValue))
        {
            setValue(bepinexVar.Key, bepinexVar.Value);
        }
    }

    private static bool ShouldEnableForPlatform(string? gameRoot)
    {
        return (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
               && IsInstalled(gameRoot);
    }

    private static IEnumerable<KeyValuePair<string, string>> GetBepInExDoorstopVariables(
        string? gameRoot,
        Func<string, string?> getValue)
    {
        string? bepInExRoot = ResolveBepInExRoot(gameRoot);
        if (bepInExRoot == null)
        {
            yield break;
        }

        string coreDirectory = Path.Combine(bepInExRoot, "core");
        string preloaderPath = Path.Combine(coreDirectory, BEPINEX_PRELOADER_NAME);
        if (File.Exists(preloaderPath))
        {
            string normalizedPreloader = NormalizePath(preloaderPath);
            yield return new("DOORSTOP_TARGET_ASSEMBLY", normalizedPreloader);
            // Ensure compatibility with older Doorstop versions that still read the legacy variable
            yield return new("DOORSTOP_INVOKE_DLL_PATH", normalizedPreloader);
        }

        string corlibOverride = Path.Combine(coreDirectory, BEPINEX_CORLIB_DIRECTORY);
        if (Directory.Exists(corlibOverride))
        {
            string normalizedCorlib = NormalizePath(corlibOverride);
            yield return new("DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE", normalizedCorlib);
            yield return new("DOORSTOP_CORLIB_OVERRIDE_PATH", normalizedCorlib);
        }

        string? searchDirs = BuildDllSearchDirs(
            getValue(DOORSTOP_DLL_SEARCH_DIRS),
            coreDirectory,
            Path.Combine(bepInExRoot, BEPINEX_DOORSTOP_LIB_DIRECTORY)
        );

        if (!string.IsNullOrEmpty(searchDirs))
        {
            yield return new(DOORSTOP_DLL_SEARCH_DIRS, searchDirs);
        }
    }

    private static string? ResolveBepInExRoot(string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            return null;
        }

        string root = Path.Combine(gameRoot, BEPINEX_DIRECTORY_NAME);
        return Directory.Exists(root) ? root : null;
    }

    private static string NormalizePath(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return path;
        }

        // Doorstop under Proton expects Windows-style absolute paths, so map the POSIX
        // path onto the Z: drive and convert separators for compatibility.
        string fullPath = Path.GetFullPath(path);
        return ToWindowsPath(fullPath);
    }

    private static string ToWindowsPath(string posixPath)
    {
        string fullPath = Path.GetFullPath(posixPath);
        if (!fullPath.StartsWith("/", StringComparison.Ordinal))
        {
            return fullPath.Replace('/', '\\');
        }

        return ("Z:" + fullPath).Replace('/', '\\');
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
                paths.Add(NormalizePath(directory));
            }
        }

        // Use Windows-style separators for Proton compatibility even on Linux to keep Doorstop
        // path parsing consistent with the Windows build.
        char separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.PathSeparator : ';';
        return paths.Count == 0 ? null : string.Join(separator, paths);
    }
}
