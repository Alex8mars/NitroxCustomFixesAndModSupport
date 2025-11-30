using System.Reflection;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Simulation;
using NitroxClient.MonoBehaviours;
using NitroxClient.MonoBehaviours.CinematicController;
using NitroxClient.MonoBehaviours.Gui.HUD;
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class PlayerCinematicController_StartCinematicMode_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PlayerCinematicController t) => t.StartCinematicMode(default(global::Player)));

    private static bool skipPrefix;

    public static bool Prefix(PlayerCinematicController __instance, global::Player setplayer)
    {
        if (skipPrefix || setplayer == null)
        {
            return true;
        }

        if (!__instance.TryGetComponentInParent(out NitroxEntity entity, true))
        {
            return true;
        }

        if (!entity.TryGetIdOrWarn(out NitroxId id))
        {
            return true;
        }

        SimulationOwnership ownership = Resolve<SimulationOwnership>();
        if (ownership.HasExclusiveLock(id))
        {
            return true;
        }

        CinematicInteraction context = new(__instance, setplayer);
        LockRequest<CinematicInteraction> lockRequest = new(id, SimulationLockType.EXCLUSIVE, ReceivedSimulationLockResponse, context);

        ownership.RequestSimulationLock(lockRequest);

        return false;
    }

    private static void ReceivedSimulationLockResponse(NitroxId id, bool lockAcquired, CinematicInteraction context)
    {
        PlayerCinematicController controller = context.Controller;

        if (lockAcquired)
        {
            skipPrefix = true;
            controller.StartCinematicMode(context.Player);
            skipPrefix = false;
        }
        else
        {
            ErrorMessage.AddMessage("Another player is already using this.");
            controller.gameObject.AddComponent<DenyOwnershipHand>();
        }
    }
}
