using NitroxClient.GameLogic;
using NitroxClient.GameLogic.PlayerLogic;
using NitroxClient.MonoBehaviours;
using NitroxClient.Unity.Helper;
using UnityEngine;

namespace NitroxClient.MonoBehaviours.CinematicController;

/// <summary>
/// Watches a local <see cref="PlayerCinematicController"/> and mirrors start/end events to other clients.
/// </summary>
public class PlayerCinematicSync : MonoBehaviour
{
    private PlayerCinematicController controller;
    private MultiplayerCinematicReference reference;
    private PlayerCinematics playerCinematics;
    private LocalPlayer localPlayer;
    private NitroxEntity entity;
    private int controllerIdentifier;
    private bool initialized;
    private bool lastActive;

    public void Initialize(PlayerCinematicController sourceController, MultiplayerCinematicReference cinematicReference, int identifier, PlayerCinematics playerCinematics)
    {
        controller = sourceController;
        reference = cinematicReference;
        controllerIdentifier = identifier;
        this.playerCinematics = playerCinematics;
        localPlayer = this.Resolve<LocalPlayer>();
        entity = reference.GetComponent<NitroxEntity>();
        lastActive = controller.cinematicModeActive;
        initialized = true;
    }

    private void Awake()
    {
        if (initialized)
        {
            return;
        }

        if (controller == null)
        {
            controller = GetComponent<PlayerCinematicController>();
        }

        if (reference == null)
        {
            reference = GetComponentInParent<MultiplayerCinematicReference>();
        }

        if (playerCinematics == null)
        {
            playerCinematics = this.Resolve<PlayerCinematics>();
        }

        if (localPlayer == null)
        {
            localPlayer = this.Resolve<LocalPlayer>();
        }

        entity = reference ? reference.GetComponent<NitroxEntity>() : null;

        if (controller == null || reference == null || entity == null)
        {
            return;
        }

        controllerIdentifier = MultiplayerCinematicReference.GetCinematicControllerIdentifier(controller.gameObject, reference.gameObject);
        lastActive = controller.cinematicModeActive;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        bool isActive = controller.cinematicModeActive;
        if (isActive == lastActive)
        {
            return;
        }

        if (localPlayer?.PlayerId is not ushort playerId)
        {
            lastActive = isActive;
            return;
        }

        if (isActive)
        {
            playerCinematics.StartCinematicMode(playerId, entity.Id, controllerIdentifier, controller.playerViewAnimationName);
        }
        else
        {
            playerCinematics.EndCinematicMode(playerId, entity.Id, controllerIdentifier, controller.playerViewAnimationName);
        }

        lastActive = isActive;
    }

    private void OnDisable()
    {
        if (!initialized || !lastActive || localPlayer?.PlayerId is not ushort playerId)
        {
            return;
        }

        playerCinematics.EndCinematicMode(playerId, entity.Id, controllerIdentifier, controller.playerViewAnimationName);
        lastActive = false;
    }
}
