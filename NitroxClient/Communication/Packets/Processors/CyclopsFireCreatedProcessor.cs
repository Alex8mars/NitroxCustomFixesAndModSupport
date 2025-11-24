using NitroxClient.Communication.Abstract;
using NitroxClient.Communication.Packets.Processors.Abstract;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxClient.Communication.Packets.Processors
{
    public class CyclopsFireCreatedProcessor : ClientPacketProcessor<CyclopsFireCreated>
    {
        private readonly IPacketSender packetSender;
        private readonly Fires fires;

        public CyclopsFireCreatedProcessor(IPacketSender packetSender, Fires fires)
        {
            this.packetSender = packetSender;
            this.fires = fires;
        }

        public override void Process(CyclopsFireCreated packet)
        {
            fires.Create(packet.FireCreatedData);
            Log.Info($"Synced Cyclops fire create packet for {packet.FireCreatedData.CyclopsId} at {packet.FireCreatedData.Room} node {packet.FireCreatedData.NodeIndex}");
        }
    }
}
