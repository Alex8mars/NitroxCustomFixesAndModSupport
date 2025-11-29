using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace BepInEx
{
    /// <summary>
    /// Attribute describing a BepInEx plugin class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BepInPlugin : Attribute
    {
        public BepInPlugin(string guid, string name, string version)
        {
            GUID = guid;
            Name = name;
            Version = version;
        }

        public string GUID { get; }

        public string Name { get; }

        public string Version { get; }
    }

    /// <summary>
    /// Optional dependency declaration. Only used for ordering in the shim loader today.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class BepInDependency : Attribute
    {
        public enum DependencyFlags
        {
            HardDependency = 1,
            SoftDependency = 2
        }

        public BepInDependency(string dependencyGuid, DependencyFlags flags = DependencyFlags.HardDependency)
        {
            DependencyGUID = dependencyGuid;
            Flags = flags;
        }

        public string DependencyGUID { get; }

        public DependencyFlags Flags { get; }
    }

    /// <summary>
    /// Marks a plugin as incompatible with another one.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class BepInIncompatibility : Attribute
    {
        public BepInIncompatibility(string incompatibilityGuid)
        {
            IncompatibilityGUID = incompatibilityGuid;
        }

        public string IncompatibilityGUID { get; }
    }

    /// <summary>
    /// Restricts plugin to a specific process.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class BepInProcess : Attribute
    {
        public BepInProcess(string processName)
        {
            ProcessName = processName;
        }

        public string ProcessName { get; }
    }

    /// <summary>
    /// Lightweight BaseUnityPlugin replacement that keeps the public surface expected by most plugins.
    /// </summary>
    public abstract class BaseUnityPlugin : MonoBehaviour
    {
        private Logging.ManualLogSource? logger;

        protected virtual void Awake()
        {
            // Ensure a logger exists even if the shim is asked to create one before initialization.
            logger ??= Logging.Logger.CreateLogSource(GetType().FullName ?? "BepInExPlugin");
        }

        public BepInPlugin? Info { get; internal set; }

        public Logging.ManualLogSource Logger
        {
            get => logger ??= Logging.Logger.CreateLogSource(GetType().FullName ?? "BepInExPlugin");
            internal set => logger = value;
        }

        internal void InitializeShimContext(BepInPlugin info, Logging.ManualLogSource logSource, Configuration.ConfigFile config)
        {
            Info = info;
            Logger = logSource;
            Config = config;
        }

        public Configuration.ConfigFile Config { get; private set; } = Configuration.ConfigFile.Empty;
    }
}

namespace BepInEx.Bootstrap
{
    /// <summary>
    /// Simplified plugin info record to track metadata and type.
    /// </summary>
    public sealed class PluginInfo
    {
        public required BepInPlugin Metadata { get; init; }

        public required Type PluginType { get; init; }

        public BepInEx.Logging.ManualLogSource LogSource { get; init; } = Logging.Logger.CreateLogSource("BepInExPlugin");

        public BepInEx.Configuration.ConfigFile Config { get; init; } = Configuration.ConfigFile.Empty;
    }
}

namespace BepInEx.Logging
{
    public enum LogLevel
    {
        Fatal,
        Error,
        Warning,
        Message,
        Info,
        Debug,
        All
    }

    /// <summary>
    /// Basic ManualLogSource that forwards to Nitrox logging.
    /// </summary>
    public sealed class ManualLogSource
    {
        private readonly string sourceName;

        internal ManualLogSource(string sourceName)
        {
            this.sourceName = string.IsNullOrWhiteSpace(sourceName) ? "BepInEx" : sourceName;
        }

        public void Log(LogLevel level, object data)
        {
            string message = data?.ToString() ?? string.Empty;
            switch (level)
            {
                case LogLevel.Fatal:
                case LogLevel.Error:
                    Nitrox.Model.Logger.Log.Error($"[{sourceName}] {message}");
                    break;
                case LogLevel.Warning:
                    Nitrox.Model.Logger.Log.Warn($"[{sourceName}] {message}");
                    break;
                case LogLevel.Message:
                case LogLevel.Info:
                    Nitrox.Model.Logger.Log.Info($"[{sourceName}] {message}");
                    break;
                default:
                    Nitrox.Model.Logger.Log.Debug($"[{sourceName}] {message}");
                    break;
            }
        }

        public void LogInfo(object data) => Log(LogLevel.Info, data);

        public void LogWarning(object data) => Log(LogLevel.Warning, data);

        public void LogError(object data) => Log(LogLevel.Error, data);

        [Conditional("DEBUG")]
        public void LogDebug(object data) => Log(LogLevel.Debug, data);
    }

    public static class Logger
    {
        private static readonly Dictionary<string, ManualLogSource> loggers = new(StringComparer.OrdinalIgnoreCase);

        public static ManualLogSource CreateLogSource(string sourceName)
        {
            if (!loggers.TryGetValue(sourceName, out ManualLogSource logSource))
            {
                logSource = new ManualLogSource(sourceName);
                loggers[sourceName] = logSource;
            }

            return logSource;
        }
    }
}

namespace BepInEx.Configuration
{
    /// <summary>
    /// Minimal configuration representation for compatibility.
    /// </summary>
    public sealed class ConfigFile
    {
        public static ConfigFile Empty { get; } = new(string.Empty, false);

        private readonly Dictionary<ConfigDefinition, object?> entries;
        private readonly string filePath;
        private readonly bool persist;

        public ConfigFile(string filePath, bool persist = true)
        {
            this.filePath = filePath;
            this.persist = persist;
            entries = new Dictionary<ConfigDefinition, object?>();
        }

        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string? description = null)
        {
            ConfigDefinition def = new(section, key);
            if (!entries.TryGetValue(def, out object? value))
            {
                value = defaultValue;
                entries[def] = value;
                Save();
            }

            return new ConfigEntry<T>(def, description, () => (T)(entries[def] ?? defaultValue), v =>
            {
                entries[def] = v;
                Save();
            });
        }

        private void Save()
        {
            if (!persist || string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Simple persistence format: key=value lines.
                List<string> lines = new();
                foreach (KeyValuePair<ConfigDefinition, object?> entry in entries)
                {
                    lines.Add($"{entry.Key.Section}:{entry.Key.Key}={entry.Value}");
                }

                File.WriteAllLines(filePath, lines);
            }
            catch (Exception ex)
            {
                Nitrox.Model.Logger.Log.Warn($"[BepInExShim] Failed to persist config to {filePath}: {ex}");
            }
        }
    }

    public readonly struct ConfigDefinition : IEquatable<ConfigDefinition>
    {
        public ConfigDefinition(string section, string key)
        {
            Section = section;
            Key = key;
        }

        public string Section { get; }

        public string Key { get; }

        public bool Equals(ConfigDefinition other) => string.Equals(Section, other.Section, StringComparison.OrdinalIgnoreCase)
                                                    && string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is ConfigDefinition def && Equals(def);

        public override int GetHashCode() => HashCode.Combine(Section.ToLowerInvariant(), Key.ToLowerInvariant());
    }

    public sealed class ConfigEntry<T>
    {
        private readonly Func<T> getter;
        private readonly Action<T> setter;

        internal ConfigEntry(ConfigDefinition definition, string? description, Func<T> getter, Action<T> setter)
        {
            Definition = definition;
            Description = description;
            this.getter = getter;
            this.setter = setter;
        }

        public ConfigDefinition Definition { get; }

        public string? Description { get; }

        public T Value
        {
            get => getter();
            set => setter(value);
        }
    }
}
