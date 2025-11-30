using System.Reflection;
using HarmonyLib;
using NitroxClient.MonoBehaviours.CinematicController;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class UseableDiveHatch_OnPlayerCinematicModeEnd_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD =
        Reflect.Method((UseableDiveHatch t) => t.OnPlayerCinematicModeEnd(default));

    // Implement the abstract method from NitroxPatch
    public override void Patch(Harmony harmony)
    {
        harmony.Patch(
            TARGET_METHOD,
            postfix: new HarmonyMethod(typeof(UseableDiveHatch_OnPlayerCinematicModeEnd_Patch).GetMethod(nameof(Postfix)))
        );
    }

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
