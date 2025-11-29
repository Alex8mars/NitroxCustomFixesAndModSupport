using System;
using System.Collections.Generic;
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
    private static readonly string processName = Process.GetCurrentProcess().ProcessName;

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
        ScanForPlugins();
    }

    /// <summary>
    /// Instantiate and attach all discovered plugins under the given host GameObject.
    /// </summary>
    public static void Bootstrap(GameObject host)
    {
        if (host == null)
        {
            return;
        }

        Initialize();

        foreach (PluginInfo plugin in plugins)
        {
            try
            {
                BaseUnityPlugin instance = (BaseUnityPlugin)host.AddComponent(plugin.PluginType);
                instance.InitializeShimContext(plugin.Metadata, plugin.LogSource, plugin.Config);
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
                if (!typeof(BaseUnityPlugin).IsAssignableFrom(type) || type.IsAbstract)
                {
                    continue;
                }

                BepInPlugin? metadata = type.GetCustomAttribute<BepInPlugin>();
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
        BepInProcess[] restrictions = type.GetCustomAttributes<BepInProcess>(true).ToArray();
        if (restrictions.Length == 0)
        {
            return true;
        }

        return restrictions.Any(r => r.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
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
            return typeof(BepInPlugin).Assembly;
        }

        return null;
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
