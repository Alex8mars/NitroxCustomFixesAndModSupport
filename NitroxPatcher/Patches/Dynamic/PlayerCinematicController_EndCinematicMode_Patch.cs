using System.Reflection;
using HarmonyLib;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;   // 👈 ADD THIS
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic
{
    public sealed class PlayerCinematicController_EndCinematicMode_Patch : NitroxPatch, IDynamicPatch
    {
        // Target: PlayerCinematicController.EndCinematicMode()
        private static readonly MethodInfo TARGET_METHOD =
            Reflect.Method((PlayerCinematicController t) => t.EndCinematicMode());

        // This gets called as a postfix after EndCinematicMode runs
        public static void Postfix(PlayerCinematicController __instance)
        {
            // Find the NitroxEntity this cinematic controller belongs to
            if (!__instance.TryGetComponentInParent(out NitroxEntity entity, true))
            {
                return;
            }

            // Get its NitroxId (with warning if missing)
            if (!entity.TryGetIdOrWarn(out NitroxId id))
            {
                return;
            }

            SimulationOwnership ownership = Resolve<SimulationOwnership>();

            // Only act if we have the lock for this entity
            if (!ownership.HasExclusiveLock(id))
            {
                return;
            }

            // Request a transient simulation lock when the cinematic ends
            ownership.RequestSimulationLock(id, SimulationLockType.TRANSIENT);
        }

        // Required by NitroxPatch – wire our postfix into Harmony
        public override void Patch(Harmony harmony)
        {
            PatchPostfix(
                harmony,
                TARGET_METHOD,
                typeof(PlayerCinematicController_EndCinematicMode_Patch)
                    .GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.Public)
            );
        }
    }
}
