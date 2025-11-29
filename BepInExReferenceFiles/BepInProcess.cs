using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace BepInEx
{
	/// <summary>
	/// This attribute specifies which processes this plugin should be run for. Not specifying this attribute will load the plugin under every process.
	/// </summary>
	// Token: 0x0200000D RID: 13
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class BepInProcess : Attribute
	{
		/// <summary>
		/// The name of the process that this plugin will run under.
		/// </summary>
		// Token: 0x17000023 RID: 35
		// (get) Token: 0x06000084 RID: 132 RVA: 0x00003087 File Offset: 0x00001287
		// (set) Token: 0x06000085 RID: 133 RVA: 0x0000308F File Offset: 0x0000128F
		public string ProcessName { get; protected set; }

		/// <param name="ProcessName">The name of the process that this plugin will run under.</param>
		// Token: 0x06000086 RID: 134 RVA: 0x00003098 File Offset: 0x00001298
		public BepInProcess(string ProcessName)
		{
			this.ProcessName = ProcessName;
		}

		// Token: 0x06000087 RID: 135 RVA: 0x000030A7 File Offset: 0x000012A7
		internal static List<BepInProcess> FromCecilType(TypeDefinition td)
		{
			return (from customAttribute in MetadataHelper.GetCustomAttributes<BepInProcess>(td, true)
			select new BepInProcess((string)customAttribute.ConstructorArguments[0].Value)).ToList<BepInProcess>();
		}
	}
}
