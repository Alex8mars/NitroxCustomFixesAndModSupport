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
    private const string DOORSTOP_ENABLED = "DOORSTOP_ENABLED";

    /// <summary>
    /// Check if BepInEx appears to be installed in the given game root.
    /// </summary>
    public static bool IsInstalled(string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            return false;
        }

        return Directory.Exists(Path.Combine(gameRoot, BEPINEX_DIRECTORY_NAME))
               || File.Exists(Path.Combine(gameRoot, WINHTTP_DLL_NAME));
    }

    /// <summary>
    /// Apply BepInEx / Doorstop environment variables to a generic dictionary.
    /// This matches your original signature.
    /// </summary>
    public static void ApplyEnvironment(IDictionary<string, string> environment, string? gameRoot)
    {
        if (environment == null)
        {
            return;
        }

        ApplyEnvironmentInternal(
            gameRoot,
            key => environment.TryGetValue(key, out string value) ? value : null,
            (key, value) => environment[key] = value
        );
    }

    /// <summary>
    /// Apply BepInEx / Doorstop env vars to a StringDictionary (used by ProcessStartInfo.EnvironmentVariables).
    /// This is the method your Steam launcher calls.
    /// </summary>
    public static void ApplyEnvironmentForStringDictionary(StringDictionary environment, string? gameRoot)
    {
        if (environment == null)
        {
            return;
        }

        ApplyEnvironmentInternal(
            gameRoot,
            key => environment.ContainsKey(key) ? environment[key] : null,
            (key, value) => environment[key] = value
        );
    }

    /// <summary>
    /// Decide if BepInEx should be enabled at all for this platform/game root.
    /// For now, simply require that BepInEx is installed.
    /// </summary>
    private static bool ShouldEnableForPlatform(string? gameRoot)
    {
        return IsInstalled(gameRoot);
    }

    /// <summary>
    /// Core logic that actually sets the environment variables using provided
    /// get/set delegates so it works with both IDictionary and StringDictionary.
    /// </summary>
    private static void ApplyEnvironmentInternal(
        string? gameRoot,
        Func<string, string?> getValue,
        Action<string, string> setValue)
    {
        if (!ShouldEnableForPlatform(gameRoot) || string.IsNullOrWhiteSpace(gameRoot))
        {
            return;
        }

        // Ensure BepInEx root exists.
        string? bepInExRoot = GetBepInExRoot(gameRoot);
        if (bepInExRoot == null)
        {
            return;
        }

        // Enable Doorstop.
        setValue(DOORSTOP_ENABLED, "TRUE");

        // Make sure winhttp override is set correctly (for Wine/Proton).
        string overrides = getValue(WINEDLLOVERRIDES) ?? string.Empty;
        setValue(WINEDLLOVERRIDES, GetWinHttpOverrides(overrides));

        // Apply BepInEx-specific Doorstop variables (preloader, corlib override, etc.).
        foreach (KeyValuePair<string, string> bepinexVar in GetBepInExDoorstopVariables(gameRoot))
        {
            setValue(bepinexVar.Key, bepinexVar.Value);
        }

        // Optionally extend DOORSTOP_DLL_SEARCH_DIRS to include BepInEx paths.
        string coreDirectory = Path.Combine(bepInExRoot, "core");
        string? dllSearchDirs = BuildDllSearchDirs(
            getValue(DOORSTOP_DLL_SEARCH_DIRS),
            coreDirectory,
            bepInExRoot
        );

        if (!string.IsNullOrWhiteSpace(dllSearchDirs))
        {
            setValue(DOORSTOP_DLL_SEARCH_DIRS, dllSearchDirs);
        }

        ApplyPlatformInjectionVariables(getValue, setValue, bepInExRoot);
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

    private static string ToPlatformPath(string path)
    {
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Builds BepInEx-related Doorstop variables (preloader path, corlib override).
    /// </summary>
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
            string normalizedPreloaderPath = ToPlatformPath(preloaderPath);
            yield return new("DOORSTOP_INVOKE_DLL_PATH", normalizedPreloaderPath);
            yield return new("DOORSTOP_TARGET_ASSEMBLY", normalizedPreloaderPath);
        }

        string corlibOverride = Path.Combine(bepInExRoot, "core", BEPINEX_CORLIB_DIRECTORY);
        if (Directory.Exists(corlibOverride))
        {
            yield return new("DOORSTOP_CORLIB_OVERRIDE_PATH", ToPlatformPath(corlibOverride));
        }
    }

    private static string GetWinHttpOverrides(string? existingOverrides)
    {
        const string winHttpOverride = "winhttp=n,b";

        if (string.IsNullOrWhiteSpace(existingOverrides))
        {
            return winHttpOverride;
        }

        // Avoid duplicate winhttp entries (case-insensitive).
        if (existingOverrides.IndexOf("winhttp", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return existingOverrides;
        }

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
                paths.Add(ToPlatformPath(directory));
            }
        }

        // Use string separator and IEnumerable<string> overload for compatibility.
        return paths.Count == 0 ? null : string.Join(Path.PathSeparator.ToString(), paths);
    }

    private static void ApplyPlatformInjectionVariables(
        Func<string, string?> getValue,
        Action<string, string> setValue,
        string bepInExRoot)
    {
        string doorstopLibrary = Path.Combine(bepInExRoot, "core", GetDoorstopLibraryName());
        if (!File.Exists(doorstopLibrary))
        {
            return;
        }

        string coreDirectory = Path.Combine(bepInExRoot, "core");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            setValue("LD_LIBRARY_PATH", BuildPathList(getValue("LD_LIBRARY_PATH"), bepInExRoot, coreDirectory));
            setValue("LD_PRELOAD", BuildPathList(getValue("LD_PRELOAD"), doorstopLibrary));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            setValue("DYLD_LIBRARY_PATH", BuildPathList(getValue("DYLD_LIBRARY_PATH"), bepInExRoot, coreDirectory));
            setValue("DYLD_INSERT_LIBRARIES", BuildPathList(getValue("DYLD_INSERT_LIBRARIES"), doorstopLibrary));
        }
    }

    private static string BuildPathList(string? existingValue, params string[] additions)
    {
        List<string> paths = new();

        if (!string.IsNullOrWhiteSpace(existingValue))
        {
            // Use char[] separator overload for older frameworks.
            string[] split = existingValue.Split(
                new[] { Path.PathSeparator },
                StringSplitOptions.RemoveEmptyEntries
            );

            paths.AddRange(split);
        }

        foreach (string addition in additions)
        {
            string normalized = ToPlatformPath(addition);

            // Case-insensitive contains using List.Exists, since List<T>.Contains
            // does not take an IEqualityComparer in older frameworks.
            bool alreadyPresent = paths.Exists(
                p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase)
            );

            if (!alreadyPresent)
            {
                paths.Add(normalized);
            }
        }

        // Use string separator and IEnumerable<string> overload for compatibility.
        return string.Join(Path.PathSeparator.ToString(), paths);
    }

    private static string GetDoorstopLibraryName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "libdoorstop.so";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "libdoorstop.dylib";
        }

        return "doorstop.dll";
    }
}
