using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;

namespace Nitrox.Model.Helper;

public static class BepInExIntegration
{
    private const string BEPINEX_DIRECTORY_NAME = "BepInEx";
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
        if (!ShouldEnableForPlatform(gameRoot))
        {
            return;
        }

        environment["DOORSTOP_ENABLE"] = "TRUE";
        environment[WINEDLLOVERRIDES] = GetWinHttpOverrides(environment.TryGetValue(WINEDLLOVERRIDES, out string overrides) ? overrides : null);
    }

    public static void ApplyEnvironment(StringDictionary environment, string? gameRoot)
    {
        if (!ShouldEnableForPlatform(gameRoot))
        {
            return;
        }

        environment["DOORSTOP_ENABLE"] = "TRUE";
        environment[WINEDLLOVERRIDES] = GetWinHttpOverrides(environment.ContainsKey(WINEDLLOVERRIDES) ? environment[WINEDLLOVERRIDES] : null);
    }

    private static bool ShouldEnableForPlatform(string? gameRoot)
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && IsInstalled(gameRoot);
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
