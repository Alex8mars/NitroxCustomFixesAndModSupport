using System.Reflection;
using NitroxClient.MonoBehaviours.CinematicController;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class UseableDiveHatch_OnHandClick_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((UseableDiveHatch t) => t.OnHandClick(default));

    public static bool Prefix(UseableDiveHatch __instance)
    {
        DiveHatchLock hatchLock = __instance.GetComponent<DiveHatchLock>();

        if (hatchLock != null && hatchLock.IsLocked)
        {
            ErrorMessage.AddMessage("Hatch is cycling, please wait.");
            return false;
        }

        return true;
    }

    public static void Postfix(UseableDiveHatch __instance)
    {
        bool cinematicRunning = (__instance.enterCinematicController != null && __instance.enterCinematicController.cinematicModeActive) ||
                                (__instance.exitCinematicController != null && __instance.exitCinematicController.cinematicModeActive);

        if (!cinematicRunning)
        {
            return;
        }

        DiveHatchLock hatchLock = __instance.GetComponent<DiveHatchLock>();

        if (hatchLock == null)
        {
            hatchLock = __instance.gameObject.AddComponent<DiveHatchLock>();
        }

        hatchLock.Lock();
    }
}
