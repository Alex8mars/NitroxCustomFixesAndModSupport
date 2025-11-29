using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using Mono.Cecil;

namespace BepInEx
{
	/// <summary>
	/// Generic helper properties and methods.
	/// </summary>
	// Token: 0x0200001A RID: 26
	public static class Utility
	{
		/// <summary>
		/// Whether current Common Language Runtime supports dynamic method generation using <see cref="N:System.Reflection.Emit" /> namespace.
		/// </summary>
		// Token: 0x1700005A RID: 90
		// (get) Token: 0x0600011E RID: 286 RVA: 0x0000429D File Offset: 0x0000249D
		public static bool CLRSupportsDynamicAssemblies
		{
			get
			{
				return Utility.CheckSRE();
			}
		}

		/// <summary>
		/// An encoding for UTF-8 which does not emit a byte order mark (BOM). 
		/// </summary>
		// Token: 0x1700005B RID: 91
		// (get) Token: 0x0600011F RID: 287 RVA: 0x000042A4 File Offset: 0x000024A4
		public static Encoding UTF8NoBom { get; } = new UTF8Encoding(false);

		// Token: 0x06000120 RID: 288 RVA: 0x000042AC File Offset: 0x000024AC
		private static bool CheckSRE()
		{
			if (Utility.sreEnabled != null)
			{
				return Utility.sreEnabled.Value;
			}
			try
			{
				new CustomAttributeBuilder(null, new object[0]);
			}
			catch (PlatformNotSupportedException)
			{
				Utility.sreEnabled = new bool?(false);
				return Utility.sreEnabled.Value;
			}
			catch (ArgumentNullException)
			{
			}
			Utility.sreEnabled = new bool?(true);
			return Utility.sreEnabled.Value;
		}

		/// <summary>
		/// Try to perform an action.
		/// </summary>
		/// <param name="action">Action to perform.</param>
		/// <param name="exception">Possible exception that gets returned.</param>
		/// <returns>True, if action succeeded, false if an exception occured.</returns>
		// Token: 0x06000121 RID: 289 RVA: 0x00004330 File Offset: 0x00002530
		public static bool TryDo(Action action, out Exception exception)
		{
			exception = null;
			bool result;
			try
			{
				action();
				result = true;
			}
			catch (Exception ex)
			{
				exception = ex;
				result = false;
			}
			return result;
		}

		/// <summary>
		/// Combines multiple paths together, as the specific method is not available in .NET 3.5.
		/// </summary>
		/// <param name="parts">The multiple paths to combine together.</param>
		/// <returns>A combined path.</returns>
		// Token: 0x06000122 RID: 290 RVA: 0x00004364 File Offset: 0x00002564
		public static string CombinePaths(params string[] parts)
		{
			return parts.Aggregate(new Func<string, string, string>(Path.Combine));
		}

		/// <summary>
		/// Returns the parent directory of a path, optionally specifying the amount of levels.
		/// </summary>
		/// <param name="path">The path to get the parent directory of.</param>
		/// <param name="levels">The amount of levels to traverse. Defaults to 1</param>
		/// <returns>The parent directory.</returns>
		// Token: 0x06000123 RID: 291 RVA: 0x00004378 File Offset: 0x00002578
		public static string ParentDirectory(string path, int levels = 1)
		{
			for (int i = 0; i < levels; i++)
			{
				path = Path.GetDirectoryName(path);
			}
			return path;
		}

		/// <summary>
		/// Tries to parse a bool, with a default value if unable to parse.
		/// </summary>
		/// <param name="input">The string to parse</param>
		/// <param name="defaultValue">The value to return if parsing is unsuccessful.</param>
		/// <returns>Boolean value of input if able to be parsed, otherwise default value.</returns>
		// Token: 0x06000124 RID: 292 RVA: 0x0000439C File Offset: 0x0000259C
		public static bool SafeParseBool(string input, bool defaultValue = false)
		{
			bool result;
			if (!bool.TryParse(input, out result))
			{
				return defaultValue;
			}
			return result;
		}

		/// <summary>
		/// Converts a file path into a UnityEngine.WWW format.
		/// </summary>
		/// <param name="path">The file path to convert.</param>
		/// <returns>A converted file path.</returns>
		// Token: 0x06000125 RID: 293 RVA: 0x000043B6 File Offset: 0x000025B6
		public static string ConvertToWWWFormat(string path)
		{
			return "file://" + path.Replace('\\', '/');
		}

		/// <summary>
		/// Indicates whether a specified string is null, empty, or consists only of white-space characters.
		/// </summary>
		/// <param name="self">The string to test.</param>
		/// <returns>True if the value parameter is null or empty, or if value consists exclusively of white-space characters.</returns>
		// Token: 0x06000126 RID: 294 RVA: 0x000043CC File Offset: 0x000025CC
		public static bool IsNullOrWhiteSpace(this string self)
		{
			return self == null || self.All(new Func<char, bool>(char.IsWhiteSpace));
		}

		/// <summary>
		/// Sorts a given dependency graph using a direct toposort, reporting possible cyclic dependencies.
		/// </summary>
		/// <param name="nodes">Nodes to sort</param>
		/// <param name="dependencySelector">Function that maps a node to a collection of its dependencies.</param>
		/// <typeparam name="TNode">Type of the node in a dependency graph.</typeparam>
		/// <returns>Collection of nodes sorted in the order of least dependencies to the most.</returns>
		/// <exception cref="T:System.Exception">Thrown when a cyclic dependency occurs.</exception>
		// Token: 0x06000127 RID: 295 RVA: 0x000043E8 File Offset: 0x000025E8
		public static IEnumerable<TNode> TopologicalSort<TNode>(IEnumerable<TNode> nodes, Func<TNode, IEnumerable<TNode>> dependencySelector)
		{
			Utility.<>c__DisplayClass13_0<TNode> CS$<>8__locals1;
			CS$<>8__locals1.dependencySelector = dependencySelector;
			CS$<>8__locals1.sorted_list = new List<TNode>();
			CS$<>8__locals1.visited = new HashSet<TNode>();
			CS$<>8__locals1.sorted = new HashSet<TNode>();
			foreach (TNode node in nodes)
			{
				Stack<TNode> stack = new Stack<TNode>();
				if (!Utility.<TopologicalSort>g__Visit|13_0<TNode>(node, stack, ref CS$<>8__locals1))
				{
					throw new Exception("Cyclic Dependency:\r\n" + (from x in stack
					select string.Format(" - {0}", x)).Aggregate((string a, string b) => a + "\r\n" + b));
				}
			}
			return CS$<>8__locals1.sorted_list;
		}

		// Token: 0x06000128 RID: 296 RVA: 0x000044C4 File Offset: 0x000026C4
		private static bool TryResolveDllAssembly<T>(AssemblyName assemblyName, string directory, Func<string, T> loader, out T assembly) where T : class
		{
			assembly = default(T);
			List<string> list = new List<string>();
			list.Add(directory);
			list.AddRange(Directory.GetDirectories(directory, "*", SearchOption.AllDirectories));
			foreach (string path in list)
			{
				string text = Path.Combine(path, assemblyName.Name + ".dll");
				if (File.Exists(text))
				{
					try
					{
						assembly = loader(text);
					}
					catch (Exception)
					{
						continue;
					}
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Checks whether a given cecil type definition is a subtype of a provided type.
		/// </summary>
		/// <param name="self">Cecil type definition</param>
		/// <param name="td">Type to check against</param>
		/// <returns>Whether the given cecil type is a subtype of the type.</returns>
		// Token: 0x06000129 RID: 297 RVA: 0x00004574 File Offset: 0x00002774
		public static bool IsSubtypeOf(this TypeDefinition self, Type td)
		{
			if (self.FullName == td.FullName)
			{
				return true;
			}
			if (self.FullName != "System.Object")
			{
				TypeReference baseType = self.BaseType;
				bool? flag;
				if (baseType == null)
				{
					flag = null;
				}
				else
				{
					TypeDefinition typeDefinition = baseType.Resolve();
					flag = ((typeDefinition != null) ? new bool?(typeDefinition.IsSubtypeOf(td)) : null);
				}
				bool? flag2 = flag;
				return flag2.GetValueOrDefault();
			}
			return false;
		}

		/// <summary>
		/// Try to resolve and load the given assembly DLL.
		/// </summary>
		/// <param name="assemblyName">Name of the assembly, of the type <see cref="T:System.Reflection.AssemblyName" />.</param>
		/// <param name="directory">Directory to search the assembly from.</param>
		/// <param name="assembly">The loaded assembly.</param>
		/// <returns>True, if the assembly was found and loaded. Otherwise, false.</returns>
		// Token: 0x0600012A RID: 298 RVA: 0x000045E6 File Offset: 0x000027E6
		public static bool TryResolveDllAssembly(AssemblyName assemblyName, string directory, out Assembly assembly)
		{
			return Utility.TryResolveDllAssembly<Assembly>(assemblyName, directory, new Func<string, Assembly>(Assembly.LoadFile), out assembly);
		}

		/// <summary>
		/// Try to resolve and load the given assembly DLL.
		/// </summary>
		/// <param name="assemblyName">Name of the assembly, of the type <see cref="T:System.Reflection.AssemblyName" />.</param>
		/// <param name="directory">Directory to search the assembly from.</param>
		/// <param name="readerParameters">Reader parameters that contain possible custom assembly resolver.</param>
		/// <param name="assembly">The loaded assembly.</param>
		/// <returns>True, if the assembly was found and loaded. Otherwise, false.</returns>
		// Token: 0x0600012B RID: 299 RVA: 0x000045FC File Offset: 0x000027FC
		public static bool TryResolveDllAssembly(AssemblyName assemblyName, string directory, ReaderParameters readerParameters, out AssemblyDefinition assembly)
		{
			return Utility.TryResolveDllAssembly<AssemblyDefinition>(assemblyName, directory, (string s) => AssemblyDefinition.ReadAssembly(s, readerParameters), out assembly);
		}

		/// <summary>
		/// Tries to create a file with the given name
		/// </summary>
		/// <param name="path">Path of the file to create</param>
		/// <param name="mode">File open mode</param>
		/// <param name="fileStream">Resulting filestream</param>
		/// <param name="access">File access options</param>
		/// <param name="share">File share options</param>
		/// <returns></returns>
		// Token: 0x0600012C RID: 300 RVA: 0x0000462C File Offset: 0x0000282C
		public static bool TryOpenFileStream(string path, FileMode mode, out FileStream fileStream, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read)
		{
			bool result;
			try
			{
				fileStream = new FileStream(path, mode, access, share);
				result = true;
			}
			catch (IOException)
			{
				fileStream = null;
				result = false;
			}
			return result;
		}

		/// <summary>
		/// Try to parse given string as an assembly name
		/// </summary>
		/// <param name="fullName">Fully qualified assembly name</param>
		/// <param name="assemblyName">Resulting <see cref="T:System.Reflection.AssemblyName" /> instance</param>
		/// <returns><c>true</c>, if parsing was successful, otherwise <c>false</c></returns>
		/// <remarks>
		/// On some versions of mono, using <see cref="M:System.Reflection.Assembly.GetName" /> fails because it runs on unmanaged side
		/// which has problems with encoding.
		/// Using <see cref="T:System.Reflection.AssemblyName" /> solves this by doing parsing on managed side instead.
		/// </remarks>
		// Token: 0x0600012D RID: 301 RVA: 0x00004664 File Offset: 0x00002864
		public static bool TryParseAssemblyName(string fullName, out AssemblyName assemblyName)
		{
			bool result;
			try
			{
				assemblyName = new AssemblyName(fullName);
				result = true;
			}
			catch (Exception)
			{
				assemblyName = null;
				result = false;
			}
			return result;
		}

		/// <summary>
		/// Gets unique files in all given directories. If the file with the same name exists in multiple directories,
		/// only the first occurrence is returned.
		/// </summary>
		/// <param name="directories">Directories to search from.</param>
		/// <param name="pattern">File pattern to search.</param>
		/// <returns>Collection of all files in the directories.</returns>
		// Token: 0x0600012E RID: 302 RVA: 0x00004698 File Offset: 0x00002898
		public static IEnumerable<string> GetUniqueFilesInDirectories(IEnumerable<string> directories, string pattern = "*")
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
			foreach (string path in directories)
			{
				foreach (string text in Directory.GetFiles(path, pattern))
				{
					string fileName = Path.GetFileName(text);
					if (!dictionary.ContainsKey(fileName))
					{
						dictionary[fileName] = text;
					}
				}
			}
			return dictionary.Values;
		}

		// Token: 0x06000130 RID: 304 RVA: 0x00004730 File Offset: 0x00002930
		[CompilerGenerated]
		internal static bool <TopologicalSort>g__Visit|13_0<TNode>(TNode node, Stack<TNode> stack, ref Utility.<>c__DisplayClass13_0<TNode> A_2)
		{
			if (!A_2.visited.Contains(node))
			{
				A_2.visited.Add(node);
				stack.Push(node);
				using (IEnumerator<TNode> enumerator = A_2.dependencySelector(node).GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						if (!Utility.<TopologicalSort>g__Visit|13_0<TNode>(enumerator.Current, stack, ref A_2))
						{
							return false;
						}
					}
				}
				A_2.sorted.Add(node);
				A_2.sorted_list.Add(node);
				stack.Pop();
				return true;
			}
			if (!A_2.sorted.Contains(node))
			{
				return false;
			}
			return true;
		}

		// Token: 0x0400005F RID: 95
		private static bool? sreEnabled;
	}
}
