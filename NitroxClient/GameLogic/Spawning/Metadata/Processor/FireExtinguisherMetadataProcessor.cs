using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class FireExtinguisherMetadataProcessor : EntityMetadataProcessor<FireExtinguisherMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, FireExtinguisherMetadata metadata)
    {
        FireExtinguisher extinguisher = gameObject.GetComponent<FireExtinguisher>();

        if (extinguisher)
        {
            extinguisher.maxFuel = metadata.MaxFuel;
            extinguisher.fuel = Mathf.Clamp(metadata.Fuel, 0f, metadata.MaxFuel);
        }
        else
        {
            Log.Error($"Could not find FireExtinguisher on {gameObject.name}");
        }
    }
}
