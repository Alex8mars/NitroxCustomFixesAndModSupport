using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Processors.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

public class ExosuitArmActionProcessor : TransmitIfCanSeePacketProcessor<ExosuitArmActionPacket>
{
    public ExosuitArmActionProcessor(PlayerManager playerManager, EntityRegistry entityRegistry) : base(playerManager, entityRegistry)
    {
    }

    public override void Process(ExosuitArmActionPacket packet, Player sender)
    {
        TransmitIfCanSeeEntities(packet, sender, new List<NitroxId> { packet.ArmId });
    }
}
