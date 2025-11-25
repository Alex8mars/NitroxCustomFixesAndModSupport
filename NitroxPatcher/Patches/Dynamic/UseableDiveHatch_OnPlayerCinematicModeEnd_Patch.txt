using System.Reflection;
using NitroxClient.MonoBehaviours.CinematicController;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class UseableDiveHatch_OnPlayerCinematicModeEnd_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((UseableDiveHatch t) => t.OnPlayerCinematicModeEnd(default));

    public static void Postfix(UseableDiveHatch __instance)
    {
        DiveHatchLock hatchLock = __instance.GetComponent<DiveHatchLock>();

        if (hatchLock == null)
        {
            return;
        }

        hatchLock.Unlock();
    }
}
