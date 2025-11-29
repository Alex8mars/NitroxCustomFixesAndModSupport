using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

namespace BepInEx.Bootstrap
{
	/// <summary>
	/// The manager and loader for all plugins, and the entry point for BepInEx plugin system.
	/// </summary>
	// Token: 0x0200003E RID: 62
	public static class Chainloader
	{
		/// <summary>
		/// The loaded and initialized list of plugins.
		/// </summary>
		// Token: 0x170000AA RID: 170
		// (get) Token: 0x0600026C RID: 620 RVA: 0x0000829F File Offset: 0x0000649F
		public static Dictionary<string, PluginInfo> PluginInfos { get; } = new Dictionary<string, PluginInfo>();

		// Token: 0x170000AB RID: 171
		// (get) Token: 0x0600026D RID: 621 RVA: 0x000082A6 File Offset: 0x000064A6
		private static string UnityVersion
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			get
			{
				return Application.unityVersion;
			}
		}

		// Token: 0x170000AC RID: 172
		// (get) Token: 0x0600026E RID: 622 RVA: 0x000082B0 File Offset: 0x000064B0
		private static bool IsHeadless
		{
			get
			{
				MethodInfo methodInfo = AccessTools.PropertyGetter(typeof(Application), "isBatchMode");
				if (methodInfo != null)
				{
					return (bool)methodInfo.Invoke(null, null);
				}
				return SystemInfo.graphicsDeviceID == 0;
			}
		}

		// Token: 0x170000AD RID: 173
		// (get) Token: 0x0600026F RID: 623 RVA: 0x000082EC File Offset: 0x000064EC
		internal static bool IsEditor
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			get
			{
				bool? flag = Chainloader.isEditor;
				if (flag == null)
				{
					bool? flag2 = Chainloader.isEditor = new bool?(Application.isEditor);
					return flag2.GetValueOrDefault();
				}
				return flag.GetValueOrDefault();
			}
		}

		/// <summary>
		/// List of all <see cref="T:BepInEx.BepInPlugin" /> loaded via the chainloader.
		/// </summary>
		// Token: 0x170000AE RID: 174
		// (get) Token: 0x06000270 RID: 624 RVA: 0x00008328 File Offset: 0x00006528
		[Obsolete("Use PluginInfos instead")]
		public static List<BaseUnityPlugin> Plugins
		{
			get
			{
				List<BaseUnityPlugin> plugins = Chainloader._plugins;
				List<BaseUnityPlugin> result;
				lock (plugins)
				{
					Chainloader._plugins.RemoveAll((BaseUnityPlugin x) => x == null);
					result = Chainloader._plugins.ToList<BaseUnityPlugin>();
				}
				return result;
			}
		}

		/// <summary>
		/// Collection of error chainloader messages that occured during plugin loading.
		/// Contains information about what certain plugins were not loaded.
		/// </summary>
		// Token: 0x170000AF RID: 175
		// (get) Token: 0x06000271 RID: 625 RVA: 0x00008390 File Offset: 0x00006590
		public static List<string> DependencyErrors { get; } = new List<string>();

		/// <summary>
		/// The GameObject that all plugins are attached to as components.
		/// </summary>
		// Token: 0x170000B0 RID: 176
		// (get) Token: 0x06000272 RID: 626 RVA: 0x00008397 File Offset: 0x00006597
		// (set) Token: 0x06000273 RID: 627 RVA: 0x0000839E File Offset: 0x0000659E
		public static GameObject ManagerObject { get; private set; }

		/// <summary>
		/// Initializes BepInEx to be able to start the chainloader.
		/// </summary>
		// Token: 0x06000274 RID: 628 RVA: 0x000083A8 File Offset: 0x000065A8
		public static void Initialize(string gameExePath, bool startConsole = true, ICollection<LogEventArgs> preloaderLogEvents = null)
		{
			if (Chainloader._initialized)
			{
				return;
			}
			ThreadingHelper.Initialize();
			if (gameExePath != null)
			{
				Paths.SetExecutablePath(gameExePath, null, null, null);
			}
			if (ConsoleManager.ConsoleEnabled && startConsole)
			{
				ConsoleManager.CreateConsole();
				Logger.Listeners.Add(new ConsoleLogListener());
			}
			Logger.InitializeInternalLoggers();
			if (Chainloader.ConfigDiskLogging.Value)
			{
				Logger.Listeners.Add(new DiskLogListener("LogOutput.log", Chainloader.ConfigDiskConsoleDisplayedLevel.Value, Chainloader.ConfigDiskAppend.Value, Chainloader.ConfigDiskWriteUnityLog.Value));
			}
			if (!TraceLogSource.IsListening)
			{
				Logger.Sources.Add(TraceLogSource.CreateSource());
			}
			Chainloader.ReplayPreloaderLogs(preloaderLogEvents);
			if (Chainloader.ConfigUnityLogging.Value)
			{
				Logger.Sources.Add(new UnityLogSource());
			}
			if (!Chainloader.IsHeadless)
			{
				Logger.Listeners.Add(new UnityLogListener());
			}
			else
			{
				ConsoleLogListener consoleLogListener = Logger.Listeners.FirstOrDefault((ILogListener l) => l is ConsoleLogListener) as ConsoleLogListener;
				if (consoleLogListener != null)
				{
					consoleLogListener.WriteUnityLogs = false;
				}
			}
			if (PlatformHelper.Is(Platform.Unix))
			{
				Logger.LogInfo("Detected Unity version: v" + Chainloader.UnityVersion);
			}
			Logger.LogMessage("Chainloader ready");
			Chainloader._initialized = true;
		}

		// Token: 0x06000275 RID: 629 RVA: 0x000084E4 File Offset: 0x000066E4
		private static void ReplayPreloaderLogs(ICollection<LogEventArgs> preloaderLogEvents)
		{
			if (preloaderLogEvents == null)
			{
				return;
			}
			UnityLogListener item = new UnityLogListener();
			Logger.Listeners.Add(item);
			ILogListener logListener = Logger.Listeners.FirstOrDefault((ILogListener logger) => logger is ConsoleLogListener);
			if (logListener != null)
			{
				Logger.Listeners.Remove(logListener);
			}
			ManualLogSource manualLogSource = Logger.CreateLogSource("Preloader");
			foreach (LogEventArgs eventArgs in preloaderLogEvents)
			{
				Logger.InternalLogEvent(manualLogSource, eventArgs);
			}
			Logger.Sources.Remove(manualLogSource);
			Logger.Listeners.Remove(item);
			if (logListener != null)
			{
				Logger.Listeners.Add(logListener);
			}
		}

		// Token: 0x170000B1 RID: 177
		// (get) Token: 0x06000276 RID: 630 RVA: 0x000085B0 File Offset: 0x000067B0
		private static Regex allowedGuidRegex { get; } = new Regex("^[a-zA-Z0-9\\._\\-]+$");

		/// <summary>
		/// Analyzes the given type definition and attempts to convert it to a valid <see cref="T:BepInEx.PluginInfo" />
		/// </summary>
		/// <param name="type">Type definition to analyze.</param>
		/// <returns>If the type represent a valid plugin, returns a <see cref="T:BepInEx.PluginInfo" /> instance. Otherwise, return null.</returns>
		// Token: 0x06000277 RID: 631 RVA: 0x000085B8 File Offset: 0x000067B8
		public static PluginInfo ToPluginInfo(TypeDefinition type)
		{
			if (type.IsInterface || type.IsAbstract)
			{
				return null;
			}
			try
			{
				if (!type.IsSubtypeOf(typeof(BaseUnityPlugin)))
				{
					return null;
				}
			}
			catch (AssemblyResolutionException)
			{
				return null;
			}
			BepInPlugin bepInPlugin = BepInPlugin.FromCecilType(type);
			if (bepInPlugin == null)
			{
				Logger.LogWarning("Skipping over type [" + type.FullName + "] as no metadata attribute is specified");
				return null;
			}
			if (string.IsNullOrEmpty(bepInPlugin.GUID) || !Chainloader.allowedGuidRegex.IsMatch(bepInPlugin.GUID))
			{
				Logger.LogWarning(string.Concat(new string[]
				{
					"Skipping type [",
					type.FullName,
					"] because its GUID [",
					bepInPlugin.GUID,
					"] is of an illegal format."
				}));
				return null;
			}
			if (bepInPlugin.Version == null)
			{
				Logger.LogWarning("Skipping type [" + type.FullName + "] because its version is invalid.");
				return null;
			}
			if (bepInPlugin.Name == null)
			{
				Logger.LogWarning("Skipping type [" + type.FullName + "] because its name is null.");
				return null;
			}
			List<BepInProcess> processes = BepInProcess.FromCecilType(type);
			IEnumerable<BepInDependency> dependencies = BepInDependency.FromCecilType(type);
			IEnumerable<BepInIncompatibility> incompatibilities = BepInIncompatibility.FromCecilType(type);
			AssemblyNameReference assemblyNameReference = type.Module.AssemblyReferences.FirstOrDefault((AssemblyNameReference reference) => reference.Name == "BepInEx");
			Version targettedBepInExVersion = ((assemblyNameReference != null) ? assemblyNameReference.Version : null) ?? new Version();
			return new PluginInfo
			{
				Metadata = bepInPlugin,
				Processes = processes,
				Dependencies = dependencies,
				Incompatibilities = incompatibilities,
				TypeName = type.FullName,
				TargettedBepInExVersion = targettedBepInExVersion
			};
		}

		// Token: 0x06000278 RID: 632 RVA: 0x00008770 File Offset: 0x00006970
		private static bool HasBepinPlugins(AssemblyDefinition ass)
		{
			if (ass.MainModule.AssemblyReferences.All((AssemblyNameReference r) => r.Name != Chainloader.CurrentAssemblyName))
			{
				return false;
			}
			return !ass.MainModule.GetTypeReferences().All((TypeReference r) => r.FullName != typeof(BepInPlugin).FullName);
		}

		// Token: 0x06000279 RID: 633 RVA: 0x000087E4 File Offset: 0x000069E4
		private static bool PluginTargetsWrongBepin(PluginInfo pluginInfo)
		{
			Version targettedBepInExVersion = pluginInfo.TargettedBepInExVersion;
			return targettedBepInExVersion.Major != Chainloader.CurrentAssemblyVersion.Major || targettedBepInExVersion.Minor > Chainloader.CurrentAssemblyVersion.Minor || (targettedBepInExVersion.Minor >= Chainloader.CurrentAssemblyVersion.Minor && targettedBepInExVersion.Build > Chainloader.CurrentAssemblyVersion.Build);
		}

		/// <summary>
		/// The entrypoint for the BepInEx plugin system.
		/// </summary>
		// Token: 0x0600027A RID: 634 RVA: 0x00008848 File Offset: 0x00006A48
		public static void Start()
		{
			if (Chainloader._loaded)
			{
				return;
			}
			if (!Chainloader._initialized)
			{
				throw new InvalidOperationException("BepInEx has not been initialized. Please call Chainloader.Initialize prior to starting the chainloader instance.");
			}
			if (!Directory.Exists(Paths.PluginPath))
			{
				Directory.CreateDirectory(Paths.PluginPath);
			}
			if (!Directory.Exists(Paths.PatcherPluginPath))
			{
				Directory.CreateDirectory(Paths.PatcherPluginPath);
			}
			try
			{
				PropertyInfo property = typeof(Application).GetProperty("productName", BindingFlags.Static | BindingFlags.Public);
				if (ConsoleManager.ConsoleActive)
				{
					ConsoleManager.SetConsoleTitle(string.Format("{0} {1} - {2}", Chainloader.CurrentAssemblyName, Chainloader.CurrentAssemblyVersion, ((property != null) ? property.GetValue(null, null) : null) ?? Paths.ProcessName));
				}
				Logger.LogMessage("Chainloader started");
				Chainloader.ManagerObject = new GameObject("BepInEx_Manager");
				if (Chainloader.ConfigHideBepInExGOs.Value)
				{
					Chainloader.ManagerObject.hideFlags = 61;
				}
				Object.DontDestroyOnLoad(Chainloader.ManagerObject);
				Dictionary<string, List<PluginInfo>> dictionary = TypeLoader.FindPluginTypes<PluginInfo>(Paths.PluginPath, new Func<TypeDefinition, PluginInfo>(Chainloader.ToPluginInfo), new Func<AssemblyDefinition, bool>(Chainloader.HasBepinPlugins), "chainloader");
				foreach (KeyValuePair<string, List<PluginInfo>> keyValuePair in dictionary)
				{
					foreach (PluginInfo pluginInfo in keyValuePair.Value)
					{
						pluginInfo.Location = keyValuePair.Key;
					}
				}
				List<PluginInfo> list = dictionary.SelectMany((KeyValuePair<string, List<PluginInfo>> p) => p.Value).ToList<PluginInfo>();
				Dictionary<string, Assembly> dictionary2 = new Dictionary<string, Assembly>();
				Logger.LogInfo(string.Format("{0} plugin{1} to load", list.Count, (list.Count == 1) ? "" : "s"));
				SortedDictionary<string, IEnumerable<string>> dependencyDict = new SortedDictionary<string, IEnumerable<string>>(StringComparer.InvariantCultureIgnoreCase);
				Dictionary<string, PluginInfo> pluginsByGUID = new Dictionary<string, PluginInfo>();
				foreach (IEnumerable<PluginInfo> source in from info in list
				group info by info.Metadata.GUID)
				{
					PluginInfo pluginInfo2 = null;
					foreach (PluginInfo pluginInfo3 in source.OrderByDescending((PluginInfo x) => x.Metadata.Version))
					{
						if (pluginInfo2 != null)
						{
							Logger.LogWarning(string.Format("Skipping [{0}] because a newer version exists ({1})", pluginInfo3, pluginInfo2));
						}
						else
						{
							List<BepInProcess> list2 = pluginInfo3.Processes.ToList<BepInProcess>();
							bool flag;
							if (list2.Count != 0)
							{
								flag = list2.All((BepInProcess x) => !string.Equals(x.ProcessName.Replace(".exe", ""), Paths.ProcessName, StringComparison.InvariantCultureIgnoreCase));
							}
							else
							{
								flag = false;
							}
							if (flag)
							{
								Logger.LogWarning(string.Format("Skipping [{0}] because of process filters ({1})", pluginInfo3, string.Join(", ", pluginInfo3.Processes.Select((BepInProcess p) => p.ProcessName).ToArray<string>())));
							}
							else
							{
								pluginInfo2 = pluginInfo3;
								dependencyDict[pluginInfo3.Metadata.GUID] = pluginInfo3.Dependencies.Select((BepInDependency d) => d.DependencyGUID);
								pluginsByGUID[pluginInfo3.Metadata.GUID] = pluginInfo3;
							}
						}
					}
				}
				Func<BepInIncompatibility, bool> <>9__7;
				Func<string, bool> <>9__9;
				foreach (PluginInfo pluginInfo4 in pluginsByGUID.Values.ToList<PluginInfo>())
				{
					IEnumerable<BepInIncompatibility> incompatibilities = pluginInfo4.Incompatibilities;
					Func<BepInIncompatibility, bool> predicate;
					if ((predicate = <>9__7) == null)
					{
						predicate = (<>9__7 = ((BepInIncompatibility incompatibility) => pluginsByGUID.ContainsKey(incompatibility.IncompatibilityGUID)));
					}
					if (incompatibilities.Any(predicate))
					{
						pluginsByGUID.Remove(pluginInfo4.Metadata.GUID);
						dependencyDict.Remove(pluginInfo4.Metadata.GUID);
						IEnumerable<string> source2 = from x in pluginInfo4.Incompatibilities
						select x.IncompatibilityGUID;
						Func<string, bool> predicate2;
						if ((predicate2 = <>9__9) == null)
						{
							predicate2 = (<>9__9 = ((string x) => pluginsByGUID.ContainsKey(x)));
						}
						string[] value = source2.Where(predicate2).ToArray<string>();
						string text = string.Format("Could not load [{0}] because it is incompatible with: {1}", pluginInfo4, string.Join(", ", value));
						Chainloader.DependencyErrors.Add(text);
						Logger.LogError(text);
					}
					else if (Chainloader.PluginTargetsWrongBepin(pluginInfo4))
					{
						string text2 = string.Format("Plugin [{0}] targets a wrong version of BepInEx ({1}) and might not work until you update", pluginInfo4, pluginInfo4.TargettedBepInExVersion);
						Chainloader.DependencyErrors.Add(text2);
						Logger.LogWarning(text2);
					}
				}
				string[] emptyDependencies = new string[0];
				List<string> list3 = Utility.TopologicalSort<string>(dependencyDict.Keys, delegate(string x)
				{
					IEnumerable<string> result;
					if (!dependencyDict.TryGetValue(x, out result))
					{
						return emptyDependencies;
					}
					return result;
				}).ToList<string>();
				HashSet<string> hashSet = new HashSet<string>();
				Dictionary<string, Version> dictionary3 = new Dictionary<string, Version>();
				foreach (string text3 in list3)
				{
					PluginInfo pluginInfo5;
					if (pluginsByGUID.TryGetValue(text3, out pluginInfo5))
					{
						bool flag2 = false;
						List<BepInDependency> list4 = new List<BepInDependency>();
						foreach (BepInDependency bepInDependency in pluginInfo5.Dependencies)
						{
							Version v;
							if (!dictionary3.TryGetValue(bepInDependency.DependencyGUID, out v) || v < bepInDependency.MinimumVersion)
							{
								if (Chainloader.<Start>g__IsHardDependency|32_10(bepInDependency))
								{
									list4.Add(bepInDependency);
								}
							}
							else if (hashSet.Contains(bepInDependency.DependencyGUID) && Chainloader.<Start>g__IsHardDependency|32_10(bepInDependency))
							{
								flag2 = true;
								break;
							}
						}
						dictionary3.Add(text3, pluginInfo5.Metadata.Version);
						if (flag2)
						{
							string text4 = string.Format("Skipping [{0}] because it has a dependency that was not loaded. See previous errors for details.", pluginInfo5);
							Chainloader.DependencyErrors.Add(text4);
							Logger.LogWarning(text4);
						}
						else if (list4.Count != 0)
						{
							string text5 = string.Format("Could not load [{0}] because it has missing dependencies: {1}", pluginInfo5, string.Join(", ", list4.Select(delegate(BepInDependency s)
							{
								if (!Chainloader.<Start>g__IsEmptyVersion|32_11(s.MinimumVersion))
								{
									return string.Format("{0} (v{1} or newer)", s.DependencyGUID, s.MinimumVersion);
								}
								return s.DependencyGUID;
							}).ToArray<string>()));
							Chainloader.DependencyErrors.Add(text5);
							Logger.LogError(text5);
							hashSet.Add(text3);
						}
						else
						{
							try
							{
								Logger.LogInfo(string.Format("Loading [{0}]", pluginInfo5));
								Assembly assembly;
								if (!dictionary2.TryGetValue(pluginInfo5.Location, out assembly))
								{
									assembly = (dictionary2[pluginInfo5.Location] = Assembly.LoadFile(pluginInfo5.Location));
								}
								Chainloader.PluginInfos[text3] = pluginInfo5;
								pluginInfo5.Instance = (BaseUnityPlugin)Chainloader.ManagerObject.AddComponent(assembly.GetType(pluginInfo5.TypeName));
								Chainloader._plugins.Add(pluginInfo5.Instance);
							}
							catch (Exception ex)
							{
								hashSet.Add(text3);
								Chainloader.PluginInfos.Remove(text3);
								Logger.LogError(string.Format("Error loading [{0}] : {1}", pluginInfo5, ex.Message));
								ReflectionTypeLoadException ex2 = ex as ReflectionTypeLoadException;
								if (ex2 != null)
								{
									Logger.LogDebug(TypeLoader.TypeLoadExceptionToString(ex2));
								}
								else
								{
									Logger.LogDebug(ex);
								}
							}
						}
					}
				}
			}
			catch (Exception ex3)
			{
				try
				{
					ConsoleManager.CreateConsole();
				}
				catch
				{
				}
				Logger.LogFatal("Error occurred starting the game");
				Logger.LogFatal(ex3.ToString());
			}
			Logger.LogMessage("Chainloader startup complete");
			Chainloader._loaded = true;
		}

		// Token: 0x0600027C RID: 636 RVA: 0x00009254 File Offset: 0x00007454
		[CompilerGenerated]
		internal static bool <Start>g__IsHardDependency|32_10(BepInDependency dep)
		{
			return (dep.Flags & BepInDependency.DependencyFlags.HardDependency) > (BepInDependency.DependencyFlags)0;
		}

		// Token: 0x0600027D RID: 637 RVA: 0x00009261 File Offset: 0x00007461
		[CompilerGenerated]
		internal static bool <Start>g__IsEmptyVersion|32_11(Version v)
		{
			return v.Major == 0 && v.Minor == 0 && v.Build <= 0 && v.Revision <= 0;
		}

		// Token: 0x040000C6 RID: 198
		private static readonly List<BaseUnityPlugin> _plugins = new List<BaseUnityPlugin>();

		// Token: 0x040000C7 RID: 199
		private static bool? isEditor;

		// Token: 0x040000CA RID: 202
		private static bool _loaded = false;

		// Token: 0x040000CB RID: 203
		private static bool _initialized = false;

		// Token: 0x040000CD RID: 205
		private static readonly string CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;

		// Token: 0x040000CE RID: 206
		private static readonly Version CurrentAssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;

		// Token: 0x040000CF RID: 207
		internal static readonly ConfigEntry<bool> ConfigHideBepInExGOs = ConfigFile.CoreConfig.Bind<bool>("Chainloader", "HideManagerGameObject", false, new StringBuilder().AppendLine("If enabled, hides BepInEx Manager GameObject from Unity.").AppendLine("This can fix loading issues in some games that attempt to prevent BepInEx from being loaded.").AppendLine("Use this only if you know what this option means, as it can affect functionality of some older plugins.").ToString());

		// Token: 0x040000D0 RID: 208
		private static readonly ConfigEntry<bool> ConfigUnityLogging = ConfigFile.CoreConfig.Bind<bool>("Logging", "UnityLogListening", true, "Enables showing unity log messages in the BepInEx logging system.");

		// Token: 0x040000D1 RID: 209
		private static readonly ConfigEntry<bool> ConfigDiskWriteUnityLog = ConfigFile.CoreConfig.Bind<bool>("Logging.Disk", "WriteUnityLog", false, "Include unity log messages in log file output.");

		// Token: 0x040000D2 RID: 210
		private static readonly ConfigEntry<bool> ConfigDiskAppend = ConfigFile.CoreConfig.Bind<bool>("Logging.Disk", "AppendLog", false, "Appends to the log file instead of overwriting, on game startup.");

		// Token: 0x040000D3 RID: 211
		private static readonly ConfigEntry<bool> ConfigDiskLogging = ConfigFile.CoreConfig.Bind<bool>("Logging.Disk", "Enabled", true, "Enables writing log messages to disk.");

		// Token: 0x040000D4 RID: 212
		private static readonly ConfigEntry<LogLevel> ConfigDiskConsoleDisplayedLevel = ConfigFile.CoreConfig.Bind<LogLevel>("Logging.Disk", "LogLevels", LogLevel.Fatal | LogLevel.Error | LogLevel.Warning | LogLevel.Message | LogLevel.Info, "Which log leves are saved to the disk log output.");
	}
}
