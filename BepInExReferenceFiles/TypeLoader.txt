using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil;

namespace BepInEx.Bootstrap
{
	/// <summary>
	///     Provides methods for loading specified types from an assembly.
	/// </summary>
	// Token: 0x02000041 RID: 65
	public static class TypeLoader
	{
		// Token: 0x06000285 RID: 645 RVA: 0x000092B4 File Offset: 0x000074B4
		static TypeLoader()
		{
			TypeLoader.Resolver = new DefaultAssemblyResolver();
			TypeLoader.ReaderParameters = new ReaderParameters
			{
				AssemblyResolver = TypeLoader.Resolver
			};
			TypeLoader.Resolver.ResolveFailure += delegate(object sender, AssemblyNameReference reference)
			{
				AssemblyName assemblyName;
				if (Utility.TryParseAssemblyName(reference.FullName, out assemblyName))
				{
					foreach (string directory in new string[]
					{
						Paths.BepInExAssemblyDirectory,
						Paths.PluginPath,
						Paths.PatcherPluginPath
					}.Concat(Paths.DllSearchPaths))
					{
						AssemblyDefinition result;
						if (Utility.TryResolveDllAssembly(assemblyName, directory, TypeLoader.ReaderParameters, out result))
						{
							return result;
						}
					}
					AssemblyResolveEventHandler assemblyResolve = TypeLoader.AssemblyResolve;
					if (assemblyResolve == null)
					{
						return null;
					}
					return assemblyResolve(sender, reference);
				}
				AssemblyResolveEventHandler assemblyResolve2 = TypeLoader.AssemblyResolve;
				if (assemblyResolve2 == null)
				{
					return null;
				}
				return assemblyResolve2(sender, reference);
			};
		}

		/// <summary>
		/// Event fired when <see cref="T:BepInEx.Bootstrap.TypeLoader" /> fails to resolve a type during type loading.
		/// </summary>
		// Token: 0x1400000A RID: 10
		// (add) Token: 0x06000286 RID: 646 RVA: 0x0000931C File Offset: 0x0000751C
		// (remove) Token: 0x06000287 RID: 647 RVA: 0x00009350 File Offset: 0x00007550
		public static event AssemblyResolveEventHandler AssemblyResolve;

		/// <summary>
		///     Looks up assemblies in the given directory and locates all types that can be loaded and collects their metadata.
		/// </summary>
		/// <typeparam name="T">The specific base type to search for.</typeparam>
		/// <param name="directory">The directory to search for assemblies.</param>
		/// <param name="typeSelector">A function to check if a type should be selected and to build the type metadata.</param>
		/// <param name="assemblyFilter">A filter function to quickly determine if the assembly can be loaded.</param>
		/// <param name="cacheName">The name of the cache to get cached types from.</param>
		/// <returns>A dictionary of all assemblies in the directory and the list of type metadatas of types that match the selector.</returns>
		// Token: 0x06000288 RID: 648 RVA: 0x00009384 File Offset: 0x00007584
		public static Dictionary<string, List<T>> FindPluginTypes<T>(string directory, Func<TypeDefinition, T> typeSelector, Func<AssemblyDefinition, bool> assemblyFilter = null, string cacheName = null) where T : ICacheable, new()
		{
			Dictionary<string, List<T>> dictionary = new Dictionary<string, List<T>>();
			Dictionary<string, CachedAssembly<T>> dictionary2 = null;
			if (cacheName != null)
			{
				dictionary2 = TypeLoader.LoadAssemblyCache<T>(cacheName);
			}
			foreach (string text in Directory.GetFiles(Path.GetFullPath(directory), "*.dll", SearchOption.AllDirectories))
			{
				try
				{
					CachedAssembly<T> cachedAssembly;
					if (dictionary2 != null && dictionary2.TryGetValue(text, out cachedAssembly) && File.GetLastWriteTimeUtc(text).Ticks == cachedAssembly.Timestamp)
					{
						dictionary[text] = cachedAssembly.CacheItems;
					}
					else
					{
						AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(text, TypeLoader.ReaderParameters);
						if (assemblyFilter != null && !assemblyFilter(assemblyDefinition))
						{
							dictionary[text] = new List<T>();
							assemblyDefinition.Dispose();
						}
						else
						{
							List<T> value = (from t in assemblyDefinition.MainModule.Types.Select(typeSelector)
							where t != null
							select t).ToList<T>();
							dictionary[text] = value;
							assemblyDefinition.Dispose();
						}
					}
				}
				catch (BadImageFormatException ex)
				{
					Logger.LogDebug("Skipping loading " + text + " because it's not a valid .NET assembly. Full error: " + ex.Message);
				}
				catch (Exception ex2)
				{
					Logger.LogError(ex2.ToString());
				}
			}
			if (cacheName != null)
			{
				TypeLoader.SaveAssemblyCache<T>(cacheName, dictionary);
			}
			return dictionary;
		}

		/// <summary>
		///     Loads an index of type metadatas from a cache.
		/// </summary>
		/// <param name="cacheName">Name of the cache</param>
		/// <typeparam name="T">Cacheable item</typeparam>
		/// <returns>Cached type metadatas indexed by the path of the assembly that defines the type. If no cache is defined, return null.</returns>
		// Token: 0x06000289 RID: 649 RVA: 0x000094E0 File Offset: 0x000076E0
		public static Dictionary<string, CachedAssembly<T>> LoadAssemblyCache<T>(string cacheName) where T : ICacheable, new()
		{
			if (!TypeLoader.EnableAssemblyCache.Value)
			{
				return null;
			}
			Dictionary<string, CachedAssembly<T>> dictionary = new Dictionary<string, CachedAssembly<T>>();
			try
			{
				string path = Path.Combine(Paths.CachePath, cacheName + "_typeloader.dat");
				if (!File.Exists(path))
				{
					return null;
				}
				using (BinaryReader binaryReader = new BinaryReader(File.OpenRead(path)))
				{
					int num = binaryReader.ReadInt32();
					for (int i = 0; i < num; i++)
					{
						string key = binaryReader.ReadString();
						long timestamp = binaryReader.ReadInt64();
						int num2 = binaryReader.ReadInt32();
						List<T> list = new List<T>();
						for (int j = 0; j < num2; j++)
						{
							T item = Activator.CreateInstance<T>();
							item.Load(binaryReader);
							list.Add(item);
						}
						dictionary[key] = new CachedAssembly<T>
						{
							Timestamp = timestamp,
							CacheItems = list
						};
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(string.Concat(new string[]
				{
					"Failed to load cache \"",
					cacheName,
					"\"; skipping loading cache. Reason: ",
					ex.Message,
					"."
				}));
			}
			return dictionary;
		}

		/// <summary>
		///     Saves indexed type metadata into a cache.
		/// </summary>
		/// <param name="cacheName">Name of the cache</param>
		/// <param name="entries">List of plugin metadatas indexed by the path to the assembly that contains the types</param>
		/// <typeparam name="T">Cacheable item</typeparam>
		// Token: 0x0600028A RID: 650 RVA: 0x00009620 File Offset: 0x00007820
		public static void SaveAssemblyCache<T>(string cacheName, Dictionary<string, List<T>> entries) where T : ICacheable
		{
			if (!TypeLoader.EnableAssemblyCache.Value)
			{
				return;
			}
			try
			{
				if (!Directory.Exists(Paths.CachePath))
				{
					Directory.CreateDirectory(Paths.CachePath);
				}
				using (BinaryWriter binaryWriter = new BinaryWriter(File.OpenWrite(Path.Combine(Paths.CachePath, cacheName + "_typeloader.dat"))))
				{
					binaryWriter.Write(entries.Count);
					foreach (KeyValuePair<string, List<T>> keyValuePair in entries)
					{
						binaryWriter.Write(keyValuePair.Key);
						binaryWriter.Write(File.GetLastWriteTimeUtc(keyValuePair.Key).Ticks);
						binaryWriter.Write(keyValuePair.Value.Count);
						foreach (T t in keyValuePair.Value)
						{
							t.Save(binaryWriter);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(string.Concat(new string[]
				{
					"Failed to save cache \"",
					cacheName,
					"\"; skipping saving cache. Reason: ",
					ex.Message,
					"."
				}));
			}
		}

		/// <summary>
		///     Converts TypeLoadException to a readable string.
		/// </summary>
		/// <param name="ex">TypeLoadException</param>
		/// <returns>Readable representation of the exception</returns>
		// Token: 0x0600028B RID: 651 RVA: 0x000097A4 File Offset: 0x000079A4
		public static string TypeLoadExceptionToString(ReflectionTypeLoadException ex)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (Exception ex2 in ex.LoaderExceptions)
			{
				stringBuilder.AppendLine(ex2.Message);
				FileNotFoundException ex3 = ex2 as FileNotFoundException;
				if (ex3 != null)
				{
					if (!string.IsNullOrEmpty(ex3.FusionLog))
					{
						stringBuilder.AppendLine("Fusion Log:");
						stringBuilder.AppendLine(ex3.FusionLog);
					}
				}
				else
				{
					FileLoadException ex4 = ex2 as FileLoadException;
					if (ex4 != null && !string.IsNullOrEmpty(ex4.FusionLog))
					{
						stringBuilder.AppendLine("Fusion Log:");
						stringBuilder.AppendLine(ex4.FusionLog);
					}
				}
				stringBuilder.AppendLine();
			}
			return stringBuilder.ToString();
		}

		/// <summary>
		/// Default assembly resolved used by the <see cref="T:BepInEx.Bootstrap.TypeLoader" />
		/// </summary>
		// Token: 0x040000D7 RID: 215
		public static readonly DefaultAssemblyResolver Resolver;

		/// <summary>
		/// Default reader parameters used by <see cref="T:BepInEx.Bootstrap.TypeLoader" />
		/// </summary>
		// Token: 0x040000D8 RID: 216
		public static readonly ReaderParameters ReaderParameters;

		// Token: 0x040000DA RID: 218
		private static readonly ConfigEntry<bool> EnableAssemblyCache = ConfigFile.CoreConfig.Bind<bool>("Caching", "EnableAssemblyCache", true, "Enable/disable assembly metadata cache\nEnabling this will speed up discovery of plugins and patchers by caching the metadata of all types BepInEx discovers.");
	}
}
