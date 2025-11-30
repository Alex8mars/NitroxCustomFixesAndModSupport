using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Nitrox.Model.Platforms.OS.Shared;

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
    private const string DOORSTOP_IGNORE_DISABLED_ENV = "DOORSTOP_IGNORE_DISABLED_ENV";
    private const string DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE = "DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE";
    private const string DOORSTOP_MONO_DEBUG_ENABLED = "DOORSTOP_MONO_DEBUG_ENABLED";
    private const string DOORSTOP_MONO_DEBUG_ADDRESS = "DOORSTOP_MONO_DEBUG_ADDRESS";
    private const string DOORSTOP_MONO_DEBUG_SUSPEND = "DOORSTOP_MONO_DEBUG_SUSPEND";
    private const string DOORSTOP_TARGET_ASSEMBLY = "DOORSTOP_TARGET_ASSEMBLY";
    private const string DOORSTOP_INVOKE_DLL_PATH = "DOORSTOP_INVOKE_DLL_PATH";
    private const string DOORSTOP_CORLIB_OVERRIDE_PATH = "DOORSTOP_CORLIB_OVERRIDE_PATH";
    private const string DOORSTOP_BOOT_CONFIG_OVERRIDE = "DOORSTOP_BOOT_CONFIG_OVERRIDE";
    private const string DOORSTOP_CLR_RUNTIME_CORECLR_PATH = "DOORSTOP_CLR_RUNTIME_CORECLR_PATH";
    private const string DOORSTOP_CLR_CORLIB_DIR = "DOORSTOP_CLR_CORLIB_DIR";
    private const string DEFAULT_MONO_DEBUG_ADDRESS = "127.0.0.1:10000";

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

    public static ProcessEx? StartWithBepInEx(string gameFilePath, string launchArguments)
    {
        if (string.IsNullOrWhiteSpace(gameFilePath))
        {
            return null;
        }

        string? gameRoot = Path.GetDirectoryName(gameFilePath);
        string? bepInExRoot = GetBepInExRoot(gameRoot);
        if (bepInExRoot == null)
        {
            return null;
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = gameFilePath,
            Arguments = launchArguments,
            WorkingDirectory = gameRoot,
            UseShellExecute = false
        };

        ApplyEnvironmentInternal(
            gameRoot,
            key => startInfo.EnvironmentVariables.ContainsKey(key) ? startInfo.EnvironmentVariables[key] : null,
            (key, value) => startInfo.EnvironmentVariables[key] = value,
            preferDoorstopLibs: true
        );

        return ProcessEx.From(startInfo);
    }

    private static bool ShouldEnableForPlatform(string? gameRoot)
    {
        return IsInstalled(gameRoot);
    }

    private static void ApplyEnvironmentInternal(
        string? gameRoot,
        Func<string, string?> getValue,
        Action<string, string> setValue,
        bool preferDoorstopLibs = false)
    {
        if (!ShouldEnableForPlatform(gameRoot) || string.IsNullOrWhiteSpace(gameRoot))
        {
            return;
        }

        string? bepInExRoot = GetBepInExRoot(gameRoot);
        if (bepInExRoot == null)
        {
            return;
        }

        setValue(DOORSTOP_ENABLED, "1");
        setValue(DOORSTOP_IGNORE_DISABLED_ENV, "0");
        setValue(DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE, string.Empty);
        setValue(DOORSTOP_MONO_DEBUG_ENABLED, "0");
        setValue(DOORSTOP_MONO_DEBUG_ADDRESS, DEFAULT_MONO_DEBUG_ADDRESS);
        setValue(DOORSTOP_MONO_DEBUG_SUSPEND, "0");
        setValue(DOORSTOP_BOOT_CONFIG_OVERRIDE, string.Empty);
        setValue(DOORSTOP_CLR_RUNTIME_CORECLR_PATH, string.Empty);
        setValue(DOORSTOP_CLR_CORLIB_DIR, string.Empty);

        string overrides = getValue(WINEDLLOVERRIDES) ?? string.Empty;
        setValue(WINEDLLOVERRIDES, GetWinHttpOverrides(overrides));

        foreach (KeyValuePair<string, string> bepinexVar in GetBepInExDoorstopVariables(gameRoot))
        {
            setValue(bepinexVar.Key, bepinexVar.Value);
        }

        string coreDirectory = Path.Combine(bepInExRoot, "core");
        string? doorstopLibsDirectory = Path.Combine(bepInExRoot, "doorstop_libs");

        string? dllSearchDirs = BuildDllSearchDirs(
            getValue(DOORSTOP_DLL_SEARCH_DIRS),
            coreDirectory,
            bepInExRoot,
            doorstopLibsDirectory
        );

        if (!string.IsNullOrWhiteSpace(dllSearchDirs))
        {
            setValue(DOORSTOP_DLL_SEARCH_DIRS, dllSearchDirs);
        }

        ApplyPlatformInjectionVariables(getValue, setValue, bepInExRoot, coreDirectory, doorstopLibsDirectory, preferDoorstopLibs);
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
            yield return new(DOORSTOP_INVOKE_DLL_PATH, normalizedPreloaderPath);
            yield return new(DOORSTOP_TARGET_ASSEMBLY, normalizedPreloaderPath);
        }

        string corlibOverride = Path.Combine(bepInExRoot, "core", BEPINEX_CORLIB_DIRECTORY);
        if (Directory.Exists(corlibOverride))
        {
            yield return new(DOORSTOP_CORLIB_OVERRIDE_PATH, ToPlatformPath(corlibOverride));
            yield return new(DOORSTOP_CLR_CORLIB_DIR, ToPlatformPath(corlibOverride));
        }
    }

    private static string GetWinHttpOverrides(string? existingOverrides)
    {
        const string winHttpOverride = "winhttp=n,b";

        if (string.IsNullOrWhiteSpace(existingOverrides))
        {
            return winHttpOverride;
        }

        if (existingOverrides.IndexOf("winhttp", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return existingOverrides;
        }

        return string.Join(";", existingOverrides, winHttpOverride);
    }

    private static string? BuildDllSearchDirs(string? existingValue, params string?[] directories)
    {
        List<string> paths = new();

        if (!string.IsNullOrWhiteSpace(existingValue))
        {
            paths.Add(existingValue);
        }

        foreach (string? directory in directories)
        {
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                paths.Add(ToPlatformPath(directory));
            }
        }

        return paths.Count == 0 ? null : string.Join(Path.PathSeparator, paths);
    }

    private static void ApplyPlatformInjectionVariables(
        Func<string, string?> getValue,
        Action<string, string> setValue,
        string bepInExRoot,
        string bepInExCore,
        string? doorstopLibsDirectory,
        bool preferDoorstopLibs)
    {
        string doorstopLibrary = ResolveDoorstopLibrary(bepInExRoot, doorstopLibsDirectory, preferDoorstopLibs);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            setValue("LD_LIBRARY_PATH", BuildPathList(getValue("LD_LIBRARY_PATH"), bepInExRoot, bepInExCore, Path.GetDirectoryName(doorstopLibrary)));
            setValue("LD_PRELOAD", BuildPathList(getValue("LD_PRELOAD"), doorstopLibrary));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            setValue("DYLD_LIBRARY_PATH", BuildPathList(getValue("DYLD_LIBRARY_PATH"), bepInExRoot, bepInExCore, Path.GetDirectoryName(doorstopLibrary)));
            setValue("DYLD_INSERT_LIBRARIES", BuildPathList(getValue("DYLD_INSERT_LIBRARIES"), doorstopLibrary));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            setValue("PATH", BuildPathList(getValue("PATH"), bepInExRoot, bepInExCore, Path.GetDirectoryName(doorstopLibrary)));
        }
    }

    private static string BuildPathList(string? existingValue, params string?[] additions)
    {
        List<string> paths = new();

        if (!string.IsNullOrWhiteSpace(existingValue))
        {
            paths.AddRange(existingValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
        }

        foreach (string? addition in additions)
        {
            if (string.IsNullOrWhiteSpace(addition))
            {
                continue;
            }

            string normalized = ToPlatformPath(addition);
            if (!paths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                paths.Add(normalized);
            }
        }

        return string.Join(Path.PathSeparator, paths);
    }

    private static string ResolveDoorstopLibrary(string bepInExRoot, string? doorstopLibsDirectory, bool preferDoorstopLibs)
    {
        string libraryName = GetDoorstopLibraryName();
        List<string> candidates = new();

        if (preferDoorstopLibs && !string.IsNullOrWhiteSpace(doorstopLibsDirectory))
        {
            candidates.Add(Path.Combine(doorstopLibsDirectory, libraryName));
        }

        if (!string.IsNullOrWhiteSpace(doorstopLibsDirectory))
        {
            candidates.Add(Path.Combine(doorstopLibsDirectory, libraryName));
        }

        candidates.Add(Path.Combine(bepInExRoot, libraryName));

        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(bepInExRoot, libraryName);
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
