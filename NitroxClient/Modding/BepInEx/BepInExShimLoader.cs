using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

// Explicit aliases so we never rely on a bare "Logging" namespace or ambiguous "Logger" symbol
using BepLogger = BepInEx.Logging.Logger;
using ManualLogSource = BepInEx.Logging.ManualLogSource;

namespace NitroxClient.Modding.BepInEx;

/// <summary>
/// Simplified BepInEx-like loader that can bootstrap BepInEx plugins without the full framework.
/// </summary>
public static class BepInExShimLoader
{
    private static readonly List<PluginInfo> plugins = new();
    private static bool initialized;
    private static bool attemptedScan;
    private static bool startedRealBepInEx;
    private static readonly string processName = Process.GetCurrentProcess().ProcessName;
    private static readonly ConcurrentDictionary<string, Assembly> loadedBepInExAssemblies = new(StringComparer.OrdinalIgnoreCase);

    static BepInExShimLoader()
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveBepInExAssembly;
    }

    public static string BepInExRoot => Path.Combine(Environment.CurrentDirectory, "BepInEx");

    public static string PluginPath => Path.Combine(BepInExRoot, "plugins");

    public static string ConfigPath => Path.Combine(BepInExRoot, "config");

    /// <summary>
    /// Initialize the shim, create directories, and scan for plugins once.
    /// </summary>
    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        TryEnsureFolders();
        if (TryStartRealBepInEx())
        {
            return;
        }
        ScanForPlugins();
    }

    /// <summary>
    /// Instantiate and attach all discovered plugins under the given host GameObject.
    /// </summary>
    public static void Bootstrap(GameObject host)
    {
        if (startedRealBepInEx)
        {
            return;
        }

        if (host == null)
        {
            return;
        }

        Initialize();

        foreach (PluginInfo plugin in plugins)
        {
            try
            {
                Component component = host.AddComponent(plugin.PluginType);

                if (component is BaseUnityPlugin shimInstance)
                {
                    shimInstance.InitializeShimContext(plugin.Metadata, plugin.LogSource, plugin.Config);
                }

                plugin.LogSource.LogInfo($"Loaded BepInEx plugin {plugin.Metadata.GUID} ({plugin.Metadata.Name} {plugin.Metadata.Version})");
            }
            catch (Exception ex)
            {
                Nitrox.Model.Logger.Log.Error($"[BepInExShim] Failed to start {plugin.Metadata.GUID}: {ex}");
            }
        }
    }

    private static void TryEnsureFolders()
    {
        try
        {
            if (!Directory.Exists(PluginPath))
            {
                Directory.CreateDirectory(PluginPath);
            }

            if (!Directory.Exists(ConfigPath))
            {
                Directory.CreateDirectory(ConfigPath);
            }
        }
        catch (Exception ex)
        {
            Nitrox.Model.Logger.Log.Warn($"[BepInExShim] Unable to create BepInEx folders: {ex}");
        }
    }

    private static bool TryStartRealBepInEx()
    {
        if (startedRealBepInEx)
        {
            return true;
        }

        string[] candidates =
        {
            Path.Combine(BepInExRoot, "core", "BepInEx.dll"),
            Path.Combine(BepInExRoot, "BepInEx.dll")
        };

        string? candidatePath = candidates.FirstOrDefault(File.Exists);
        if (candidatePath == null)
        {
            return false;
        }

        try
        {
            realBepInExAssembly = Assembly.LoadFrom(candidatePath);
            CacheAssemblyBySimpleName(realBepInExAssembly);
            TrySetPaths(realBepInExAssembly);
            if (TryInvokeChainloader(realBepInExAssembly))
            {
                startedRealBepInEx = true;
                attemptedScan = true;
                Nitrox.Model.Logger.Log.Info("[BepInExShim] Detected external BepInEx installation; deferring plugin load to it.");
                return true;
            }
        }
        catch (Exception ex)
        {
            Nitrox.Model.Logger.Log.Warn($"[BepInExShim] Failed to start external BepInEx: {ex}");
        }

        return false;
    }

    private static void ScanForPlugins()
    {
        if (attemptedScan)
        {
            return;
        }

        attemptedScan = true;

        if (!Directory.Exists(PluginPath))
        {
            Nitrox.Model.Logger.Log.Info("[BepInExShim] No BepInEx plugin directory found; skipping load.");
            return;
        }

        foreach (string dllPath in Directory.EnumerateFiles(PluginPath, "*.dll", SearchOption.AllDirectories))
        {
            TryLoadAssembly(dllPath);
        }

        plugins.Sort((a, b) => string.Compare(a.Metadata.GUID, b.Metadata.GUID, StringComparison.OrdinalIgnoreCase));
    }

    private static void TryLoadAssembly(string dllPath)
    {
        try
        {
            byte[] raw = File.ReadAllBytes(dllPath);
            Assembly pluginAssembly = Assembly.Load(raw);
            foreach (Type type in pluginAssembly.GetTypes())
            {
                if (!IsBaseUnityPluginType(type))
                {
                    continue;
                }

                BepInPlugin? metadata = TryReadMetadata(type);
                if (metadata == null)
                {
                    continue;
                }

                if (!IsProcessAllowed(type))
                {
                    Nitrox.Model.Logger.Log.Debug($"[BepInExShim] Skipping {metadata.GUID} because it does not target {processName}.");
                    continue;
                }

                // Use explicit alias so there's no confusion with UnityEngine.Logger
                ManualLogSource logSource = BepLogger.CreateLogSource(metadata.GUID);

                ConfigFile config = new(Path.Combine(ConfigPath, $"{metadata.GUID}.cfg"));
                plugins.Add(new PluginInfo
                {
                    Metadata = metadata,
                    PluginType = type,
                    LogSource = logSource,
                    Config = config
                });
            }
        }
        catch (Exception ex)
        {
            Nitrox.Model.Logger.Log.Warn($"[BepInExShim] Failed to load plugin assembly {dllPath}: {ex}");
        }
    }

    private static bool IsProcessAllowed(MemberInfo type)
    {
        IEnumerable<Attribute> restrictions = type
            .GetCustomAttributes(true)
            .OfType<Attribute>()
            .Where(attr => attr.GetType().FullName?.EndsWith("BepInProcess", StringComparison.OrdinalIgnoreCase) == true);

        if (!restrictions.Any())
        {
            return true;
        }

        return restrictions
            .Select(r => r.GetType().GetProperty("ProcessName")?.GetValue(r) as string)
            .Any(name => name != null && name.Equals(processName, StringComparison.OrdinalIgnoreCase));
    }

    private static Assembly? ResolveBepInExAssembly(object? sender, ResolveEventArgs args)
    {
        string? name = new AssemblyName(args.Name).Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (name.Equals("BepInEx", StringComparison.OrdinalIgnoreCase) || name.StartsWith("BepInEx.", StringComparison.OrdinalIgnoreCase))
        {
            return loadedBepInExAssemblies.GetOrAdd(name, LoadBepInExAssemblyFromDisk);
        }

        return null;
    }

    private static Assembly LoadBepInExAssemblyFromDisk(string simpleName)
    {
        static string BuildCandidatePath(params string[] parts) => Path.Combine(parts);

        string[] candidates =
        {
            BuildCandidatePath(BepInExRoot, "core", simpleName + ".dll"),
            BuildCandidatePath(BepInExRoot, simpleName + ".dll")
        };

        foreach (string candidate in candidates)
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                return Assembly.LoadFrom(candidate);
            }
            catch (Exception ex)
            {
                Nitrox.Model.Logger.Log.Warn($"[BepInExShim] Failed to load BepInEx assembly {candidate}: {ex}");
            }
        }

        return typeof(BepInPlugin).Assembly;
    }

    private static bool IsBaseUnityPluginType(Type type)
    {
        if (type.IsAbstract || !typeof(MonoBehaviour).IsAssignableFrom(type))
        {
            return false;
        }

        Type? current = type;
        while (current != null)
        {
            if (string.Equals(current.FullName, "BepInEx.BaseUnityPlugin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static BepInPlugin? TryReadMetadata(MemberInfo type)
    {
        Attribute? metadata = type
            .GetCustomAttributes(true)
            .OfType<Attribute>()
            .FirstOrDefault(attr => attr.GetType().FullName is string fullName && fullName.EndsWith("BepInPlugin", StringComparison.OrdinalIgnoreCase));

        if (metadata == null)
        {
            return null;
        }

        string? guid = metadata.GetType().GetProperty("GUID")?.GetValue(metadata) as string;
        string? name = metadata.GetType().GetProperty("Name")?.GetValue(metadata) as string;
        string? version = metadata.GetType().GetProperty("Version")?.GetValue(metadata) as string;

        if (string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        return new BepInPlugin(guid, name, version);
    }
}

/// <summary>
/// MonoBehaviour wrapper so initialization happens within the Unity lifecycle.
/// </summary>
public sealed class BepInExShimBehaviour : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        name = "BepInExShim";
        BepInExShimLoader.Initialize();
        BepInExShimLoader.Bootstrap(gameObject);
    }
}
