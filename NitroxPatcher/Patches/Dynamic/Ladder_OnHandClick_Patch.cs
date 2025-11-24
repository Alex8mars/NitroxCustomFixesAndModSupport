using System.Reflection;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Simulation;
using NitroxClient.MonoBehaviours.Gui.HUD;
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Ladder_OnHandClick_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Ladder t) => t.OnHandClick(default(GUIHand)));

    private static bool skipPrefix;

    public static bool Prefix(Ladder __instance, GUIHand hand)
    {
        if (skipPrefix)
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

        if (Resolve<SimulationOwnership>().HasExclusiveLock(id))
        {
            return true;
        }

        HandInteraction<Ladder> context = new(__instance, hand);
        LockRequest<HandInteraction<Ladder>> lockRequest = new(id, SimulationLockType.EXCLUSIVE, ReceivedSimulationLockResponse, context);

        Resolve<SimulationOwnership>().RequestSimulationLock(lockRequest);

        return false;
    }

    private static void ReceivedSimulationLockResponse(NitroxId id, bool lockAcquired, HandInteraction<Ladder> context)
    {
        Ladder ladder = context.Target;

        if (lockAcquired)
        {
            skipPrefix = true;
            ladder.OnHandClick(context.GuiHand);
            skipPrefix = false;
        }
        else
        {
            ladder.gameObject.AddComponent<DenyOwnershipHand>();
        }
    }
}
