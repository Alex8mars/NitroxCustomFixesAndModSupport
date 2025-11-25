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

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ApplyWindowsEnvironment(environment, gameRoot);
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            ApplyLinuxEnvironment(environment, gameRoot, GetValue);
        }
    }

    private static void ApplyEnvironmentInternal(StringDictionary environment, string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !IsInstalled(gameRoot))
        {
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ApplyWindowsEnvironment(environment, gameRoot);
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            ApplyLinuxEnvironment(environment, gameRoot, GetValue);
        }
    }

    private static void ApplyWindowsEnvironment(IDictionary<string, string> environment, string gameRoot)
    {
        string? preloaderPath = GetPreloaderPath(gameRoot);
        if (string.IsNullOrWhiteSpace(preloaderPath))
        {
            return;
        }

        ApplyDoorstopBase(environment, preloaderPath, gameRoot);
    }

    private static void ApplyWindowsEnvironment(StringDictionary environment, string gameRoot)
    {
        string? preloaderPath = GetPreloaderPath(gameRoot);
        if (string.IsNullOrWhiteSpace(preloaderPath))
        {
            return;
        }

        ApplyDoorstopBase(environment, preloaderPath, gameRoot);
    }

    private static void ApplyLinuxEnvironment<T>(T environment, string gameRoot, Func<T, string, string?> valueGetter)
        where T : class
    {
        string? preloaderPath = GetPreloaderPath(gameRoot);
        string doorstopLibsPath = Path.Combine(gameRoot, BEPINEX_DIRECTORY_NAME, "doorstop_libs");
        string? doorstopLibrary = GetDoorstopLibraryPath(doorstopLibsPath);

        if (!string.IsNullOrEmpty(doorstopLibrary))
        {
            if (!string.IsNullOrWhiteSpace(preloaderPath))
            {
                SetValue(environment, "DOORSTOP_TARGET_ASSEMBLY", preloaderPath);
                SetValue(environment, "DOORSTOP_DLL_SEARCH_PATH", GetDoorstopSearchPath(gameRoot));
            }

            SetValue(environment, "DOORSTOP_ENABLE", "TRUE");
            SetValue(environment, "DOORSTOP_CORLIB_OVERRIDE_PATH", string.Empty);

            string? existingLdLibraryPath = valueGetter(environment, "LD_LIBRARY_PATH");
            SetValue(environment, "LD_LIBRARY_PATH", PrependDelimitedValue(doorstopLibsPath, existingLdLibraryPath, ':'));

            string? existingLdPreload = valueGetter(environment, "LD_PRELOAD");
            SetValue(environment, "LD_PRELOAD", PrependDelimitedValue(doorstopLibrary, existingLdPreload, ':'));
            return;
        }

        string? overrides = valueGetter(environment, WINEDLLOVERRIDES);
        SetValue(environment, WINEDLLOVERRIDES, GetWinHttpOverrides(overrides));
    }

    private static void ApplyDoorstopBase(object environment, string preloaderPath, string gameRoot)
    {
        SetValue(environment, "DOORSTOP_ENABLE", "TRUE");
        SetValue(environment, "DOORSTOP_TARGET_ASSEMBLY", preloaderPath);
        SetValue(environment, "DOORSTOP_INVOKE_DLL_PATH", preloaderPath);
        SetValue(environment, "DOORSTOP_DLL_SEARCH_PATH", GetDoorstopSearchPath(gameRoot));
        SetValue(environment, "DOORSTOP_CORLIB_OVERRIDE_PATH", string.Empty);
    }

    private static string? GetPreloaderPath(string gameRoot)
    {
        string preloaderPath = Path.Combine(gameRoot, BEPINEX_DIRECTORY_NAME, "core", BEPINEX_PRELOADER);
        return File.Exists(preloaderPath) ? preloaderPath : null;
    }

    private static string GetDoorstopSearchPath(string gameRoot)
    {
        return Path.Combine(gameRoot, BEPINEX_DIRECTORY_NAME, "core");
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

    private static string? GetDoorstopLibraryPath(string doorstopLibsPath)
    {
        string[] candidateFiles =
        [
            Path.Combine(doorstopLibsPath, "libdoorstop_x64.so"),
            Path.Combine(doorstopLibsPath, "libdoorstop.so")
        ];

        foreach (string candidate in candidateFiles)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? GetValue(IDictionary<string, string> environment, string key)
    {
        environment.TryGetValue(key, out string? value);
        return value;
    }

    private static string? GetValue(StringDictionary environment, string key)
    {
        return environment.ContainsKey(key) ? environment[key] : null;
    }

    private static void SetValue(object environment, string key, string value)
    {
        switch (environment)
        {
            case IDictionary<string, string> dictionary:
                dictionary[key] = value;
                break;
            case StringDictionary stringDictionary:
                stringDictionary[key] = value;
                break;
            default:
                throw new ArgumentException("Unsupported environment type", nameof(environment));
        }
    }

    private static string PrependDelimitedValue(string value, string? existing, char delimiter)
    {
        if (string.IsNullOrWhiteSpace(existing))
        {
            return value;
        }

        if (existing!.Contains(value, StringComparison.Ordinal))
        {
            return existing;
        }

        return string.Join(delimiter, value, existing);
    }
}
