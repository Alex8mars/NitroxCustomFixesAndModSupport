using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MonoMod.Utils;

namespace BepInEx
{
	/// <summary>
	///     Paths used by BepInEx
	/// </summary>
	// Token: 0x02000012 RID: 18
	public static class Paths
	{
		// Token: 0x060000A8 RID: 168 RVA: 0x00003638 File Offset: 0x00001838
		internal static void SetExecutablePath(string executablePath, string bepinRootPath = null, string managedPath = null, string[] dllSearchPath = null)
		{
			Paths.ExecutablePath = executablePath;
			Paths.ProcessName = Path.GetFileNameWithoutExtension(executablePath);
			Paths.GameRootPath = (PlatformHelper.Is(Platform.MacOS) ? Utility.ParentDirectory(executablePath, 4) : Path.GetDirectoryName(executablePath));
			Paths.ManagedPath = (managedPath ?? Utility.CombinePaths(new string[]
			{
				Paths.GameRootPath,
				Paths.ProcessName + "_Data",
				"Managed"
			}));
			Paths.BepInExRootPath = (bepinRootPath ?? Path.Combine(Paths.GameRootPath, "BepInEx"));
			Paths.ConfigPath = Path.Combine(Paths.BepInExRootPath, "config");
			Paths.BepInExConfigPath = Path.Combine(Paths.ConfigPath, "BepInEx.cfg");
			Paths.PluginPath = Path.Combine(Paths.BepInExRootPath, "plugins");
			Paths.PatcherPluginPath = Path.Combine(Paths.BepInExRootPath, "patchers");
			Paths.BepInExAssemblyDirectory = Path.Combine(Paths.BepInExRootPath, "core");
			Paths.BepInExAssemblyPath = Path.Combine(Paths.BepInExAssemblyDirectory, Assembly.GetExecutingAssembly().GetName().Name + ".dll");
			Paths.CachePath = Path.Combine(Paths.BepInExRootPath, "cache");
			Paths.DllSearchPaths = (dllSearchPath ?? new string[0]).Concat(new string[]
			{
				Paths.ManagedPath
			}).Distinct<string>().ToArray<string>();
		}

		// Token: 0x060000A9 RID: 169 RVA: 0x00003790 File Offset: 0x00001990
		internal static void SetManagedPath(string managedPath)
		{
			if (managedPath == null)
			{
				return;
			}
			Paths.ManagedPath = managedPath;
		}

		// Token: 0x060000AA RID: 170 RVA: 0x0000379C File Offset: 0x0000199C
		internal static void SetPluginPath(string pluginPath)
		{
			Paths.PluginPath = Utility.CombinePaths(new string[]
			{
				Paths.BepInExRootPath,
				pluginPath
			});
		}

		/// <summary>
		/// 	List of directories from where Mono will search assemblies before assembly resolving is invoked.
		/// </summary>
		// Token: 0x17000030 RID: 48
		// (get) Token: 0x060000AB RID: 171 RVA: 0x000037BA File Offset: 0x000019BA
		// (set) Token: 0x060000AC RID: 172 RVA: 0x000037C1 File Offset: 0x000019C1
		public static string[] DllSearchPaths { get; private set; }

		/// <summary>
		///     The directory that the core BepInEx DLLs reside in.
		/// </summary>
		// Token: 0x17000031 RID: 49
		// (get) Token: 0x060000AD RID: 173 RVA: 0x000037C9 File Offset: 0x000019C9
		// (set) Token: 0x060000AE RID: 174 RVA: 0x000037D0 File Offset: 0x000019D0
		public static string BepInExAssemblyDirectory { get; private set; }

		/// <summary>
		///     The path to the core BepInEx DLL.
		/// </summary>
		// Token: 0x17000032 RID: 50
		// (get) Token: 0x060000AF RID: 175 RVA: 0x000037D8 File Offset: 0x000019D8
		// (set) Token: 0x060000B0 RID: 176 RVA: 0x000037DF File Offset: 0x000019DF
		public static string BepInExAssemblyPath { get; private set; }

		/// <summary>
		///     The path to the main BepInEx folder.
		/// </summary>
		// Token: 0x17000033 RID: 51
		// (get) Token: 0x060000B1 RID: 177 RVA: 0x000037E7 File Offset: 0x000019E7
		// (set) Token: 0x060000B2 RID: 178 RVA: 0x000037EE File Offset: 0x000019EE
		public static string BepInExRootPath { get; private set; }

		/// <summary>
		///     The path of the currently executing program BepInEx is encapsulated in.
		/// </summary>
		// Token: 0x17000034 RID: 52
		// (get) Token: 0x060000B3 RID: 179 RVA: 0x000037F6 File Offset: 0x000019F6
		// (set) Token: 0x060000B4 RID: 180 RVA: 0x000037FD File Offset: 0x000019FD
		public static string ExecutablePath { get; private set; }

		/// <summary>
		///     The directory that the currently executing process resides in.
		/// 	<para>On OSX however, this is the parent directory of the game.app folder.</para>
		/// </summary>
		// Token: 0x17000035 RID: 53
		// (get) Token: 0x060000B5 RID: 181 RVA: 0x00003805 File Offset: 0x00001A05
		// (set) Token: 0x060000B6 RID: 182 RVA: 0x0000380C File Offset: 0x00001A0C
		public static string GameRootPath { get; private set; }

		/// <summary>
		///     The path to the Managed folder of the currently running Unity game.
		/// </summary>
		// Token: 0x17000036 RID: 54
		// (get) Token: 0x060000B7 RID: 183 RVA: 0x00003814 File Offset: 0x00001A14
		// (set) Token: 0x060000B8 RID: 184 RVA: 0x0000381B File Offset: 0x00001A1B
		public static string ManagedPath { get; private set; }

		/// <summary>
		/// 	The path to the config directory.
		/// </summary>
		// Token: 0x17000037 RID: 55
		// (get) Token: 0x060000B9 RID: 185 RVA: 0x00003823 File Offset: 0x00001A23
		// (set) Token: 0x060000BA RID: 186 RVA: 0x0000382A File Offset: 0x00001A2A
		public static string ConfigPath { get; private set; }

		/// <summary>
		/// 	The path to the global BepInEx configuration file.
		/// </summary>
		// Token: 0x17000038 RID: 56
		// (get) Token: 0x060000BB RID: 187 RVA: 0x00003832 File Offset: 0x00001A32
		// (set) Token: 0x060000BC RID: 188 RVA: 0x00003839 File Offset: 0x00001A39
		public static string BepInExConfigPath { get; private set; }

		/// <summary>
		/// 	The path to temporary cache files.
		/// </summary>
		// Token: 0x17000039 RID: 57
		// (get) Token: 0x060000BD RID: 189 RVA: 0x00003841 File Offset: 0x00001A41
		// (set) Token: 0x060000BE RID: 190 RVA: 0x00003848 File Offset: 0x00001A48
		public static string CachePath { get; private set; }

		/// <summary>
		///     The path to the patcher plugin folder which resides in the BepInEx folder.
		/// </summary>
		// Token: 0x1700003A RID: 58
		// (get) Token: 0x060000BF RID: 191 RVA: 0x00003850 File Offset: 0x00001A50
		// (set) Token: 0x060000C0 RID: 192 RVA: 0x00003857 File Offset: 0x00001A57
		public static string PatcherPluginPath { get; private set; }

		/// <summary>
		///     The path to the plugin folder which resides in the BepInEx folder.
		/// <para>
		/// 	This is ONLY guaranteed to be set correctly when Chainloader has been initialized.
		/// </para>
		/// </summary>
		// Token: 0x1700003B RID: 59
		// (get) Token: 0x060000C1 RID: 193 RVA: 0x0000385F File Offset: 0x00001A5F
		// (set) Token: 0x060000C2 RID: 194 RVA: 0x00003866 File Offset: 0x00001A66
		public static string PluginPath { get; private set; }

		/// <summary>
		///     The name of the currently executing process.
		/// </summary>
		// Token: 0x1700003C RID: 60
		// (get) Token: 0x060000C3 RID: 195 RVA: 0x0000386E File Offset: 0x00001A6E
		// (set) Token: 0x060000C4 RID: 196 RVA: 0x00003875 File Offset: 0x00001A75
		public static string ProcessName { get; private set; }
	}
}
