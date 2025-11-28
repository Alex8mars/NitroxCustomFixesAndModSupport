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
    /// Kept close to the original intent: it mutates the given environment in-place.
    /// </summary>
    public static void ApplyEnvironmentForStringDictionary(StringDictionary environment, string? gameRoot)
    {
        if (environment == null)
        {
            return;
        }

        // Reuse the shared logic so behavior matches the IDictionary-based path.
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
        setValue("DOORSTOP_ENABLE", "TRUE");

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

    /// <summary>
    /// Normalizes a file system path to a Windows-style path usable by Doorstop.
    /// Kept as a separate method so it matches your original style.
    /// </summary>
    private static string NormalizePath(string path)
    {
        // You can tweak this if you need a different normalization, but this
        // is effectively what you want for Proton/Wine setups.
        return ToWindowsPath(path);
    }

    /// <summary>
    /// Builds BepInEx-related Doorstop variables (preloader path, corlib override).
    /// Now matches the variables your original iterator was trying to produce.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string>> GetBepInExDoorstopVariables(string? gameRoot)
    {
        string? bepInExRoot = GetBepInExRoot(gameRoot);
        if (bepInExRoot == null)
        {
            yield break;
        }

        string coreDirectory = Path.Combine(bepInExRoot, "core");

        string preloaderPath = Path.Combine(coreDirectory, BEPINEX_PRELOADER_NAME);
        if (File.Exists(preloaderPath))
        {
            string normalizedPreloader = NormalizePath(preloaderPath);

            // New-style Doorstop variable
            yield return new("DOORSTOP_TARGET_ASSEMBLY", normalizedPreloader);

            // Legacy/compatibility variable
            yield return new("DOORSTOP_INVOKE_DLL_PATH", normalizedPreloader);
        }

        string corlibOverride = Path.Combine(coreDirectory, BEPINEX_CORLIB_DIRECTORY);
        if (Directory.Exists(corlibOverride))
        {
            string normalizedCorlib = NormalizePath(corlibOverride);

            // Mono search path override
            yield return new("DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE", normalizedCorlib);

            // Corlib override path (what you had before)
            yield return new("DOORSTOP_CORLIB_OVERRIDE_PATH", normalizedCorlib);
        }
    }

    private static string GetWinHttpOverrides(string? existingOverrides)
    {
        const string WINHTTP_OVERRIDE = "winhttp=n,b";

        if (string.IsNullOrWhiteSpace(existingOverrides))
        {
            return WINHTTP_OVERRIDE;
        }

        // Avoid duplicate winhttp entries (case-insensitive).
        if (existingOverrides.IndexOf("winhttp", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return existingOverrides;
        }

        return string.Join(";", existingOverrides, WINHTTP_OVERRIDE);
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
