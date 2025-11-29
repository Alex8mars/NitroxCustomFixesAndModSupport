using System;
using System.Linq;
using Mono.Cecil;

namespace BepInEx
{
	/// <summary>
	/// This attribute denotes that a class is a plugin, and specifies the required metadata.
	/// </summary>
	// Token: 0x0200000A RID: 10
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class BepInPlugin : Attribute
	{
		/// <summary>
		/// The unique identifier of the plugin. Should not change between plugin versions.
		/// </summary>
		// Token: 0x1700001C RID: 28
		// (get) Token: 0x0600006B RID: 107 RVA: 0x00002E37 File Offset: 0x00001037
		// (set) Token: 0x0600006C RID: 108 RVA: 0x00002E3F File Offset: 0x0000103F
		public string GUID { get; protected set; }

		/// <summary>
		/// The user friendly name of the plugin. Is able to be changed between versions.
		/// </summary>
		// Token: 0x1700001D RID: 29
		// (get) Token: 0x0600006D RID: 109 RVA: 0x00002E48 File Offset: 0x00001048
		// (set) Token: 0x0600006E RID: 110 RVA: 0x00002E50 File Offset: 0x00001050
		public string Name { get; protected set; }

		/// <summary>
		/// The specfic version of the plugin.
		/// </summary>
		// Token: 0x1700001E RID: 30
		// (get) Token: 0x0600006F RID: 111 RVA: 0x00002E59 File Offset: 0x00001059
		// (set) Token: 0x06000070 RID: 112 RVA: 0x00002E61 File Offset: 0x00001061
		public Version Version { get; protected set; }

		/// <param name="GUID">The unique identifier of the plugin. Should not change between plugin versions.</param>
		/// <param name="Name">The user friendly name of the plugin. Is able to be changed between versions.</param>
		/// <param name="Version">The specfic version of the plugin.</param>
		// Token: 0x06000071 RID: 113 RVA: 0x00002E6C File Offset: 0x0000106C
		public BepInPlugin(string GUID, string Name, string Version)
		{
			this.GUID = GUID;
			this.Name = Name;
			try
			{
				this.Version = new Version(Version);
			}
			catch
			{
				this.Version = null;
			}
		}

		// Token: 0x06000072 RID: 114 RVA: 0x00002EB8 File Offset: 0x000010B8
		internal static BepInPlugin FromCecilType(TypeDefinition td)
		{
			CustomAttribute customAttribute = MetadataHelper.GetCustomAttributes<BepInPlugin>(td, false).FirstOrDefault<CustomAttribute>();
			if (customAttribute == null)
			{
				return null;
			}
			return new BepInPlugin((string)customAttribute.ConstructorArguments[0].Value, (string)customAttribute.ConstructorArguments[1].Value, (string)customAttribute.ConstructorArguments[2].Value);
		}
	}
}
