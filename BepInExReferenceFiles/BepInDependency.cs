using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;
using Mono.Cecil;

namespace BepInEx
{
	/// <summary>
	/// This attribute specifies any dependencies that this plugin has on other plugins.
	/// </summary>
	// Token: 0x0200000B RID: 11
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class BepInDependency : Attribute, ICacheable
	{
		/// <summary>
		/// The GUID of the referenced plugin.
		/// </summary>
		// Token: 0x1700001F RID: 31
		// (get) Token: 0x06000073 RID: 115 RVA: 0x00002F27 File Offset: 0x00001127
		// (set) Token: 0x06000074 RID: 116 RVA: 0x00002F2F File Offset: 0x0000112F
		public string DependencyGUID { get; protected set; }

		/// <summary>
		/// The flags associated with this dependency definition.
		/// </summary>
		// Token: 0x17000020 RID: 32
		// (get) Token: 0x06000075 RID: 117 RVA: 0x00002F38 File Offset: 0x00001138
		// (set) Token: 0x06000076 RID: 118 RVA: 0x00002F40 File Offset: 0x00001140
		public BepInDependency.DependencyFlags Flags { get; protected set; }

		/// <summary>
		/// The minimum version of the referenced plugin.
		/// </summary>
		// Token: 0x17000021 RID: 33
		// (get) Token: 0x06000077 RID: 119 RVA: 0x00002F49 File Offset: 0x00001149
		// (set) Token: 0x06000078 RID: 120 RVA: 0x00002F51 File Offset: 0x00001151
		public Version MinimumVersion { get; protected set; }

		/// <summary>
		/// Marks this <see cref="T:BepInEx.BaseUnityPlugin" /> as depenant on another plugin. The other plugin will be loaded before this one.
		/// If the other plugin doesn't exist, what happens depends on the <see cref="P:BepInEx.BepInDependency.Flags" /> parameter.
		/// </summary>
		/// <param name="DependencyGUID">The GUID of the referenced plugin.</param>
		/// <param name="Flags">The flags associated with this dependency definition.</param>
		// Token: 0x06000079 RID: 121 RVA: 0x00002F5A File Offset: 0x0000115A
		public BepInDependency(string DependencyGUID, BepInDependency.DependencyFlags Flags = BepInDependency.DependencyFlags.HardDependency)
		{
			this.DependencyGUID = DependencyGUID;
			this.Flags = Flags;
			this.MinimumVersion = new Version();
		}

		/// <summary>
		/// Marks this <see cref="T:BepInEx.BaseUnityPlugin" /> as depenant on another plugin. The other plugin will be loaded before this one.
		/// If the other plugin doesn't exist or is of a version below <see cref="P:BepInEx.BepInDependency.MinimumVersion" />, this plugin will not load and an error will be logged instead.
		/// </summary>
		/// <param name="DependencyGUID">The GUID of the referenced plugin.</param>
		/// <param name="MinimumDependencyVersion">The minimum version of the referenced plugin.</param>
		/// <remarks>When version is supplied the dependency is always treated as HardDependency</remarks>
		// Token: 0x0600007A RID: 122 RVA: 0x00002F7B File Offset: 0x0000117B
		public BepInDependency(string DependencyGUID, string MinimumDependencyVersion) : this(DependencyGUID, BepInDependency.DependencyFlags.HardDependency)
		{
			this.MinimumVersion = new Version(MinimumDependencyVersion);
		}

		// Token: 0x0600007B RID: 123 RVA: 0x00002F91 File Offset: 0x00001191
		internal static IEnumerable<BepInDependency> FromCecilType(TypeDefinition td)
		{
			return MetadataHelper.GetCustomAttributes<BepInDependency>(td, true).Select(delegate(CustomAttribute customAttribute)
			{
				string dependencyGUID = (string)customAttribute.ConstructorArguments[0].Value;
				object value = customAttribute.ConstructorArguments[1].Value;
				string text = value as string;
				if (text != null)
				{
					return new BepInDependency(dependencyGUID, text);
				}
				return new BepInDependency(dependencyGUID, (BepInDependency.DependencyFlags)value);
			}).ToList<BepInDependency>();
		}

		// Token: 0x0600007C RID: 124 RVA: 0x00002FC3 File Offset: 0x000011C3
		void ICacheable.Save(BinaryWriter bw)
		{
			bw.Write(this.DependencyGUID);
			bw.Write((int)this.Flags);
			bw.Write(this.MinimumVersion.ToString());
		}

		// Token: 0x0600007D RID: 125 RVA: 0x00002FEE File Offset: 0x000011EE
		void ICacheable.Load(BinaryReader br)
		{
			this.DependencyGUID = br.ReadString();
			this.Flags = (BepInDependency.DependencyFlags)br.ReadInt32();
			this.MinimumVersion = new Version(br.ReadString());
		}

		/// <summary>
		/// Flags that are applied to a dependency
		/// </summary>
		// Token: 0x0200004E RID: 78
		[Flags]
		public enum DependencyFlags
		{
			/// <summary>
			/// The plugin has a hard dependency on the referenced plugin, and will not run without it.
			/// </summary>
			// Token: 0x040000EB RID: 235
			HardDependency = 1,
			/// <summary>
			/// This plugin has a soft dependency on the referenced plugin, and is able to run without it.
			/// </summary>
			// Token: 0x040000EC RID: 236
			SoftDependency = 2
		}
	}
}
