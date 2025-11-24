using System.Reflection;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class PlayerCinematicController_EndCinematicMode_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PlayerCinematicController t) => t.EndCinematicMode());

    public static void Postfix(PlayerCinematicController __instance)
    {
        if (!CinematicSyncToggle.Enabled)
        {
            return;
        }

        if (!__instance.TryGetComponentInParent(out NitroxEntity entity, true))
        {
            return;
        }

        if (!entity.TryGetIdOrWarn(out NitroxId id))
        {
            return;
        }

        SimulationOwnership ownership = Resolve<SimulationOwnership>();

        if (!ownership.HasExclusiveLock(id))
        {
            return;
        }

        ownership.RequestSimulationLock(id, SimulationLockType.TRANSIENT);
    }
}
