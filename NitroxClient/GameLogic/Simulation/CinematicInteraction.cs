using NitroxClient.MonoBehaviours.CinematicController;
using UnityEngine;

namespace NitroxClient.GameLogic.Simulation;

public class CinematicInteraction : LockRequestContext
{
    public PlayerCinematicController Controller { get; }
    public global::Player Player { get; }
    public float RequestTime { get; }

    public CinematicInteraction(PlayerCinematicController controller, global::Player player)
    {
        Controller = controller;
        Player = player;
        RequestTime = Time.time;
    }
}
