using System.Reflection;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Simulation;
using NitroxClient.MonoBehaviours;
using NitroxClient.MonoBehaviours.CinematicController;
using NitroxClient.MonoBehaviours.Gui.HUD;
using Nitrox.Model.DataStructures;
using UnityEngine;

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
        global::Player player = context.Player;
        SimulationOwnership ownership = Resolve<SimulationOwnership>();

        if (lockAcquired)
        {
            if (controller == null || player == null || !controller.isActiveAndEnabled || player.cinematicModeActive)
            {
                ownership.RequestSimulationLock(id, SimulationLockType.TRANSIENT);
                return;
            }

            Vector3 targetPosition = controller.animatedTransform != null ? controller.animatedTransform.position : controller.transform.position;

            if (Vector3.Distance(player.transform.position, targetPosition) > 3f)
            {
                ownership.RequestSimulationLock(id, SimulationLockType.TRANSIENT);
                return;
            }

            skipPrefix = true;
            controller.StartCinematicMode(player);
            skipPrefix = false;
        }
        else
        {
            ErrorMessage.AddMessage("Another player is already using this.");
            controller.gameObject.AddComponent<DenyOwnershipHand>();
        }
    }
}
