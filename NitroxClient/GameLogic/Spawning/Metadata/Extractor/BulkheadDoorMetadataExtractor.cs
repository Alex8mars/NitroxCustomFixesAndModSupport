using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class BulkheadDoorMetadataExtractor : EntityMetadataExtractor<BulkheadDoor, BulkheadDoorMetadata>
{
    public override BulkheadDoorMetadata Extract(BulkheadDoor entity)
    {
        return new(entity.opened, entity.GetInitialyOpen());
    }
}
