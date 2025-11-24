using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class BulkheadDoorMetadataProcessor : EntityMetadataProcessor<BulkheadDoorMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, BulkheadDoorMetadata metadata)
    {
        using (PacketSuppressor<EntityMetadataUpdate>.Suppress())
        {
            BulkheadDoor bulkheadDoor = gameObject.GetComponent<BulkheadDoor>();
            bulkheadDoor.SetInitialyOpen(metadata.InitiallyOpen);
            bulkheadDoor.SetState(metadata.Opened);
        }
    }
}
