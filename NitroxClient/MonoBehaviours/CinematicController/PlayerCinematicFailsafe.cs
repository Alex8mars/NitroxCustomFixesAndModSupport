using Nitrox.Model.Logger;
using UnityEngine;
using NitroxClient.Extensions;

namespace NitroxClient.MonoBehaviours.CinematicController;

/// <summary>
/// Guards against players getting stuck in cinematics (e.g., lifepod hatches) by force-ending
/// any cinematic that runs longer than a reasonable timeout.
/// </summary>
public class PlayerCinematicFailsafe : MonoBehaviour
{
    private const float MAX_CINEMATIC_DURATION = 12f;

    private PlayerCinematicController controller;
    private float? cinematicStartedAt;
    private bool logged;

    private void Awake()
    {
        controller = GetComponent<PlayerCinematicController>();
    }

    private void OnDisable()
    {
        cinematicStartedAt = null;
        logged = false;
    }

    private void Update()
    {
        if (!controller)
        {
            return;
        }

        if (controller.cinematicModeActive)
        {
            if (cinematicStartedAt == null)
            {
                cinematicStartedAt = Time.time;
            }
            else if (Time.time - cinematicStartedAt.Value > MAX_CINEMATIC_DURATION)
            {
                if (!logged)
                {
                    Log.Warn($"Force ending stuck cinematic on {controller.GetFullHierarchyPath()} after {MAX_CINEMATIC_DURATION} seconds.");
                    logged = true;
                }

                controller.OnPlayerCinematicModeEnd();
                cinematicStartedAt = null;
            }
        }
        else
        {
            cinematicStartedAt = null;
            logged = false;
        }
    }
}
