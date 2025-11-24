using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class BulkheadDoor_SetState_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BulkheadDoor t) => t.SetState(default));

    public static void Postfix(BulkheadDoor __instance)
    {
        if (__instance.TryGetIdOrWarn(out NitroxId id))
        {
            BulkheadDoorMetadata metadata = new(__instance.opened, __instance.GetInitialyOpen());
            Resolve<Entities>().BroadcastMetadataUpdate(id, metadata);
        }
    }
}
