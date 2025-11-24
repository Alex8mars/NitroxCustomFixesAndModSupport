using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

[Serializable]
[DataContract]
public class FireExtinguisherMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public float Fuel { get; }

    [DataMember(Order = 2)]
    public float MaxFuel { get; }

    [IgnoreConstructor]
    protected FireExtinguisherMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public FireExtinguisherMetadata(float fuel, float maxFuel)
    {
        Fuel = fuel;
        MaxFuel = maxFuel;
    }

    public override string ToString()
    {
        return $"[FireExtinguisherMetadata Fuel: {Fuel}, MaxFuel: {MaxFuel}]";
    }
}
