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
        return (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                || RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
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

        bool useWindowsPaths = ShouldUseWindowsPaths(bepInExRoot);
        char dllSearchSeparator = useWindowsPaths ? ';' : Path.PathSeparator;

        string coreDirectory = Path.Combine(bepInExRoot, "core");
        string preloaderPath = Path.Combine(coreDirectory, BEPINEX_PRELOADER_NAME);
        if (File.Exists(preloaderPath))
        {
            string normalizedPreloader = NormalizePath(preloaderPath, useWindowsPaths);
            yield return new("DOORSTOP_TARGET_ASSEMBLY", normalizedPreloader);
            // Ensure compatibility with older Doorstop versions that still read the legacy variable
            yield return new("DOORSTOP_INVOKE_DLL_PATH", normalizedPreloader);
        }

        string corlibOverride = Path.Combine(coreDirectory, BEPINEX_CORLIB_DIRECTORY);
        if (Directory.Exists(corlibOverride))
        {
            string normalizedCorlib = NormalizePath(corlibOverride, useWindowsPaths);
            yield return new("DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE", normalizedCorlib);
            yield return new("DOORSTOP_CORLIB_OVERRIDE_PATH", normalizedCorlib);
        }

        string? searchDirs = BuildDllSearchDirs(
            getValue(DOORSTOP_DLL_SEARCH_DIRS),
            useWindowsPaths,
            dllSearchSeparator,
            coreDirectory,
            Path.Combine(bepInExRoot, BEPINEX_DOORSTOP_LIB_DIRECTORY)
        );

        if (!string.IsNullOrEmpty(searchDirs))
        {
            yield return new(DOORSTOP_DLL_SEARCH_DIRS, searchDirs);
        }

        foreach (KeyValuePair<string, string> nativeVar in GetNativeDoorstopBootstrapVariables(
                     bepInExRoot,
                     corlibOverride,
                     getValue))
        {
            yield return nativeVar;
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

    private static string NormalizePath(string path, bool toWindows)
    {
        if (!toWindows)
        {
            return Path.GetFullPath(path);
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

    private static string? BuildDllSearchDirs(string? existingValue, bool useWindowsPaths, char separator, params string[] directories)
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
                paths.Add(NormalizePath(directory, useWindowsPaths));
            }
        }

        return paths.Count == 0 ? null : string.Join(separator, paths);
    }

    private static bool ShouldUseWindowsPaths(string bepInExRoot)
    {
        // Native Linux/macOS packs ship libdoorstop.* alongside BepInEx. If present, prefer native
        // path formatting so LD_PRELOAD and Doorstop can resolve the libraries correctly. Proton
        // builds rely on the Windows-style path translation instead.
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !HasNativeDoorstop(bepInExRoot);
    }

    private static bool HasNativeDoorstop(string bepInExRoot)
    {
        return File.Exists(Path.Combine(bepInExRoot, "libdoorstop.so"))
               || File.Exists(Path.Combine(bepInExRoot, "libdoorstop.dylib"));
    }

    private static IEnumerable<KeyValuePair<string, string>> GetNativeDoorstopBootstrapVariables(
        string bepInExRoot,
        string corlibOverride,
        Func<string, string?> getValue)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !HasNativeDoorstop(bepInExRoot))
        {
            yield break;
        }

        string? doorstopLibrary = GetNativeDoorstopLibraryPath(bepInExRoot);
        if (doorstopLibrary != null)
        {
            string ldPreload = PrependPath(doorstopLibrary, getValue("LD_PRELOAD"), ':');
            yield return new("LD_PRELOAD", ldPreload);

            // macOS uses DYLD_* variables instead of LD_*.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string dyldInsert = PrependPath(doorstopLibrary, getValue("DYLD_INSERT_LIBRARIES"), ':');
                yield return new("DYLD_INSERT_LIBRARIES", dyldInsert);
            }
        }

        string libraryPath = BuildLibraryPath(bepInExRoot, corlibOverride, getValue);
        if (!string.IsNullOrEmpty(libraryPath))
        {
            yield return new("LD_LIBRARY_PATH", libraryPath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                yield return new("DYLD_LIBRARY_PATH", libraryPath);
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

    private static string? BuildLibraryPath(string bepInExRoot, string corlibOverride, Func<string, string?> getValue)
    {
        List<string> paths = new()
        {
            Path.GetFullPath(bepInExRoot)
        };

        if (Directory.Exists(corlibOverride))
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
}
