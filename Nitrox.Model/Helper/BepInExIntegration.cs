using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;

namespace Nitrox.Model.Helper;

public static class BepInExIntegration
{
    private const string BEPINEX_DIRECTORY_NAME = "BepInEx";
    private const string BEPINEX_PRELOADER = "BepInEx.Preloader.dll";
    private const string WINHTTP_DLL_NAME = "winhttp.dll";
    private const string WINEDLLOVERRIDES = "WINEDLLOVERRIDES";

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
        ApplyEnvironmentInternal(environment, gameRoot);
    }

    public static void ApplyEnvironment(StringDictionary environment, string? gameRoot)
    {
        ApplyEnvironmentInternal(environment, gameRoot);
    }

    private static void ApplyEnvironmentInternal(IDictionary<string, string> environment, string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !IsInstalled(gameRoot))
        {
            return;
        }

        bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        if (!isLinux && !isWindows)
        {
            return;
        }

        environment["DOORSTOP_ENABLE"] = "TRUE";

        if (isWindows)
        {
            string? preloaderPath = GetPreloaderPath(gameRoot);
            if (!string.IsNullOrWhiteSpace(preloaderPath))
            {
                environment["DOORSTOP_INVOKE_DLL_PATH"] = preloaderPath;
            }
            return;
        }

        // Linux/Proton-specific settings
        _ = environment.TryGetValue(WINEDLLOVERRIDES, out string? overrides);
        environment[WINEDLLOVERRIDES] = GetWinHttpOverrides(overrides);
    }

    private static void ApplyEnvironmentInternal(StringDictionary environment, string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !IsInstalled(gameRoot))
        {
            return;
        }

        bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        if (!isLinux && !isWindows)
        {
            return;
        }

        environment["DOORSTOP_ENABLE"] = "TRUE";

        if (isWindows)
        {
            string? preloaderPath = GetPreloaderPath(gameRoot);
            if (!string.IsNullOrWhiteSpace(preloaderPath))
            {
                environment["DOORSTOP_INVOKE_DLL_PATH"] = preloaderPath;
            }
            return;
        }

        string? overrides = environment.ContainsKey(WINEDLLOVERRIDES) ? environment[WINEDLLOVERRIDES] : null;
        environment[WINEDLLOVERRIDES] = GetWinHttpOverrides(overrides);
    }

    private static string? GetPreloaderPath(string gameRoot)
    {
        string preloaderPath = Path.Combine(gameRoot, BEPINEX_DIRECTORY_NAME, "core", BEPINEX_PRELOADER);
        return File.Exists(preloaderPath) ? preloaderPath : null;
    }

    private static string GetWinHttpOverrides(string? existingOverrides)
    {
        const string winHttpOverride = "winhttp=n,b";
        if (string.IsNullOrWhiteSpace(existingOverrides))
        {
            return winHttpOverride;
        }

        if (existingOverrides.Contains("winhttp", StringComparison.OrdinalIgnoreCase))
        {
            return existingOverrides;
        }

        return string.Join(';', existingOverrides, winHttpOverride);
    }
}
