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
    private const string DOORSTOP_CONFIG_NAME = "doorstop_config.ini";
    private const string WINHTTP_DLL_NAME = "winhttp.dll";
    private const string WINEDLLOVERRIDES = "WINEDLLOVERRIDES";
    private const string DOORSTOP_DLL_SEARCH_DIRS = "DOORSTOP_DLL_SEARCH_DIRS";

    private enum InstallKind
    {
        None,
        NativeDoorstop,
        WinHttp
    }

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

    public static bool RequiresDirectLaunch(string? gameRoot)
    {
        InstallKind installKind = GetInstallKind(gameRoot, out string? _);

        // Native Doorstop packs require the environment to be applied directly to the
        // game process. Proton/Wine installs also need direct control of the process
        // environment so WINEDLLOVERRIDES and related overrides are respected.
        return installKind == InstallKind.NativeDoorstop
               || (installKind == InstallKind.WinHttp
                   && (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                       || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)));
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
        InstallKind installKind = GetInstallKind(gameRoot, out string? bepInExRoot);
        if (installKind == InstallKind.None || bepInExRoot == null)
        {
            return;
        }

        switch (installKind)
        {
            case InstallKind.NativeDoorstop:
                ApplyNativeDoorstopEnvironment(bepInExRoot, getValue, setValue);
                break;
            case InstallKind.WinHttp:
                ApplyWinHttpEnvironment(gameRoot, bepInExRoot, getValue, setValue);
                break;
        }
    }

    private static string? GetDoorstopConfigPath(string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            return null;
        }

        string configPath = Path.Combine(gameRoot, DOORSTOP_CONFIG_NAME);
        return File.Exists(configPath) ? Path.GetFullPath(configPath) : null;
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

    private static string? BuildDllSearchDirs(string? existingValue, char separator, params string[] directories)
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
                paths.Add(Path.GetFullPath(directory));
            }
        }

        return paths.Count == 0 ? null : string.Join(separator, paths);
    }

    private static InstallKind GetInstallKind(string? gameRoot, out string? bepInExRoot)
    {
        bepInExRoot = null;
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            return InstallKind.None;
        }

        string candidateRoot = Path.Combine(gameRoot, BEPINEX_DIRECTORY_NAME);
        if (!Directory.Exists(candidateRoot))
        {
            return File.Exists(Path.Combine(gameRoot, WINHTTP_DLL_NAME)) ? InstallKind.WinHttp : InstallKind.None;
        }

        bepInExRoot = candidateRoot;

        if (HasNativeDoorstop(bepInExRoot))
        {
            return InstallKind.NativeDoorstop;
        }

        // If a BepInEx folder is present but the native pack is not, fall back to the Windows-style
        // loader so winhttp overrides are still applied under Proton/Wine.
        return InstallKind.WinHttp;
    }

    private static void ApplyNativeDoorstopEnvironment(
        string bepInExRoot,
        Func<string, string?> getValue,
        Action<string, string> setValue)
    {
        string coreDirectory = Path.Combine(bepInExRoot, "core");
        string preloaderPath = Path.Combine(coreDirectory, BEPINEX_PRELOADER_NAME);
        string corlibOverride = Path.Combine(coreDirectory, BEPINEX_CORLIB_DIRECTORY);

        DoorstopConfig config = DoorstopConfig.Load(GetDoorstopConfigPath(Path.GetDirectoryName(bepInExRoot)));

        setValue("DOORSTOP_ENABLED", config.Enabled ?? "1");
        setValue("DOORSTOP_ENABLE", config.Enabled ?? "1");
        if (config.IgnoreDisable != null)
        {
            setValue("DOORSTOP_IGNORE_DISABLED_ENV", config.IgnoreDisable);
        }

        string? targetAssembly = config.TargetAssembly ?? (File.Exists(preloaderPath) ? Path.GetFullPath(preloaderPath) : null);
        if (!string.IsNullOrWhiteSpace(targetAssembly))
        {
            setValue("DOORSTOP_TARGET_ASSEMBLY", targetAssembly);
            setValue("DOORSTOP_INVOKE_DLL_PATH", targetAssembly);
        }

        string? corlibOverridePath = config.CorlibOverride ?? (Directory.Exists(corlibOverride) ? Path.GetFullPath(corlibOverride) : null);
        if (!string.IsNullOrWhiteSpace(corlibOverridePath))
        {
            setValue("DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE", corlibOverridePath);
            setValue("DOORSTOP_CORLIB_OVERRIDE_PATH", corlibOverridePath);
        }

        string? searchDirs = BuildDllSearchDirs(
            config.DllSearchDirs ?? getValue(DOORSTOP_DLL_SEARCH_DIRS),
            Path.PathSeparator,
            coreDirectory,
            Path.Combine(bepInExRoot, BEPINEX_DOORSTOP_LIB_DIRECTORY)
        );

        if (!string.IsNullOrEmpty(searchDirs))
        {
            setValue(DOORSTOP_DLL_SEARCH_DIRS, searchDirs);
        }

        ApplyNativeBootstrap(bepInExRoot, corlibOverridePath, getValue, setValue);

        if (config.ConfigPath != null)
        {
            setValue("DOORSTOP_CONFIG_FILE", config.ConfigPath);
        }
    }

    private static void ApplyWinHttpEnvironment(
        string? gameRoot,
        string? bepInExRoot,
        Func<string, string?> getValue,
        Action<string, string> setValue)
    {
        string overrides = getValue(WINEDLLOVERRIDES) ?? string.Empty;
        setValue(WINEDLLOVERRIDES, GetWinHttpOverrides(overrides));

        string? configPath = GetDoorstopConfigPath(gameRoot);
        DoorstopConfig config = DoorstopConfig.Load(configPath);

        string? coreDirectory = string.IsNullOrWhiteSpace(bepInExRoot) ? null : Path.Combine(bepInExRoot, "core");
        string? targetAssembly = config.TargetAssembly;
        if (string.IsNullOrWhiteSpace(targetAssembly) && coreDirectory != null)
        {
            string preloaderPath = Path.Combine(coreDirectory, BEPINEX_PRELOADER_NAME);
            targetAssembly = File.Exists(preloaderPath) ? preloaderPath : null;
        }

        string? corlibOverridePath = config.CorlibOverride;
        if (string.IsNullOrWhiteSpace(corlibOverridePath) && coreDirectory != null)
        {
            string corlibOverride = Path.Combine(coreDirectory, BEPINEX_CORLIB_DIRECTORY);
            corlibOverridePath = Directory.Exists(corlibOverride) ? corlibOverride : null;
        }

        string? dllSearchDirs = BuildDllSearchDirs(
            config.DllSearchDirs ?? getValue(DOORSTOP_DLL_SEARCH_DIRS),
            ';',
            coreDirectory ?? string.Empty,
            Path.Combine(bepInExRoot ?? gameRoot ?? string.Empty, BEPINEX_DOORSTOP_LIB_DIRECTORY)
        );

        // BepInEx running under Proton expects Windows-style path separators and slashes
        targetAssembly = NormalizeToWindowsPath(targetAssembly);
        corlibOverridePath = NormalizeToWindowsPath(corlibOverridePath);
        dllSearchDirs = NormalizeToWindowsPathList(dllSearchDirs, ';');
        if (configPath != null)
        {
            setValue("DOORSTOP_CONFIG_FILE", NormalizeToWindowsPath(configPath));
        }

        setValue("DOORSTOP_ENABLE", config.Enabled ?? "1");
        setValue("DOORSTOP_ENABLED", config.Enabled ?? "1");

        if (!string.IsNullOrWhiteSpace(targetAssembly))
        {
            setValue("DOORSTOP_TARGET_ASSEMBLY", targetAssembly);
            setValue("DOORSTOP_INVOKE_DLL_PATH", targetAssembly);
        }

        if (!string.IsNullOrWhiteSpace(corlibOverridePath))
        {
            setValue("DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE", corlibOverridePath);
            setValue("DOORSTOP_CORLIB_OVERRIDE_PATH", corlibOverridePath);
        }

        if (!string.IsNullOrWhiteSpace(dllSearchDirs))
        {
            setValue(DOORSTOP_DLL_SEARCH_DIRS, dllSearchDirs);
        }
    }

    private static bool HasNativeDoorstop(string bepInExRoot)
    {
        return File.Exists(Path.Combine(bepInExRoot, "libdoorstop.so"))
               || File.Exists(Path.Combine(bepInExRoot, "libdoorstop.dylib"));
    }

    private static void ApplyNativeBootstrap(
        string bepInExRoot,
        string? corlibOverride,
        Func<string, string?> getValue,
        Action<string, string> setValue)
    {
        string? doorstopLibrary = GetNativeDoorstopLibraryPath(bepInExRoot);
        if (doorstopLibrary != null)
        {
            string ldPreload = PrependPath(doorstopLibrary, getValue("LD_PRELOAD"), ':');
            setValue("LD_PRELOAD", ldPreload);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string dyldInsert = PrependPath(doorstopLibrary, getValue("DYLD_INSERT_LIBRARIES"), ':');
                setValue("DYLD_INSERT_LIBRARIES", dyldInsert);
            }
        }

        string libraryPath = BuildLibraryPath(bepInExRoot, corlibOverride, getValue);
        if (!string.IsNullOrEmpty(libraryPath))
        {
            setValue("LD_LIBRARY_PATH", libraryPath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                setValue("DYLD_LIBRARY_PATH", libraryPath);
            }
        }
    }

    private static string? GetNativeDoorstopLibraryPath(string bepInExRoot)
    {
        string soPath = Path.Combine(bepInExRoot, "libdoorstop.so");
        if (File.Exists(soPath))
        {
            return Path.GetFullPath(soPath);
        }

        string dylibPath = Path.Combine(bepInExRoot, "libdoorstop.dylib");
        if (File.Exists(dylibPath))
        {
            return Path.GetFullPath(dylibPath);
        }

        return null;
    }

    private static string? BuildLibraryPath(string bepInExRoot, string? corlibOverride, Func<string, string?> getValue)
    {
        List<string> paths = new()
        {
            Path.GetFullPath(bepInExRoot)
        };

        if (!string.IsNullOrWhiteSpace(corlibOverride) && Directory.Exists(corlibOverride))
        {
            paths.Add(Path.GetFullPath(corlibOverride));
        }

        string? existing = getValue("LD_LIBRARY_PATH");
        if (!string.IsNullOrWhiteSpace(existing))
        {
            paths.Add(existing);
        }

        return paths.Count == 0 ? null : string.Join(':', paths);
    }

    private static string PrependPath(string pathToAdd, string? existingValue, char separator)
    {
        if (string.IsNullOrWhiteSpace(existingValue))
        {
            return pathToAdd;
        }

        return string.Join(separator, pathToAdd, existingValue);
    }

    private static string? NormalizeToWindowsPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return path.Replace(Path.DirectorySeparatorChar, '\\')
                   .Replace(Path.AltDirectorySeparatorChar, '\\');
    }

    private static string? NormalizeToWindowsPathList(string? pathList, char separator)
    {
        if (string.IsNullOrWhiteSpace(pathList))
        {
            return pathList;
        }

        string[] parts = pathList.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = NormalizeToWindowsPath(parts[i])!;
        }

        return string.Join(separator, parts);
    }

    private sealed class DoorstopConfig
    {
        public string? ConfigPath { get; private set; }
        public string? Enabled { get; private set; }
        public string? IgnoreDisable { get; private set; }
        public string? TargetAssembly { get; private set; }
        public string? CorlibOverride { get; private set; }
        public string? DllSearchDirs { get; private set; }

        public static DoorstopConfig Load(string? configPath)
        {
            DoorstopConfig config = new();

            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            {
                return config;
            }

            config.ConfigPath = configPath;

            foreach (string rawLine in File.ReadAllLines(configPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
                {
                    continue;
                }

                string key = line[..separatorIndex].Trim();
                string value = line[(separatorIndex + 1)..].Trim();

                switch (key.ToUpperInvariant())
                {
                    case "DOORSTOP_ENABLE":
                    case "DOORSTOP_ENABLED":
                        config.Enabled = value;
                        break;
                    case "DOORSTOP_IGNORE_DISABLED_ENV":
                        config.IgnoreDisable = value;
                        break;
                    case "DOORSTOP_TARGET_ASSEMBLY":
                    case "DOORSTOP_INVOKE_DLL_PATH":
                        config.TargetAssembly = value;
                        break;
                    case "DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE":
                    case "DOORSTOP_CORLIB_OVERRIDE_PATH":
                        config.CorlibOverride = value;
                        break;
                    case DOORSTOP_DLL_SEARCH_DIRS:
                        config.DllSearchDirs = value;
                        break;
                }
            }

            return config;
        }
    }
}
