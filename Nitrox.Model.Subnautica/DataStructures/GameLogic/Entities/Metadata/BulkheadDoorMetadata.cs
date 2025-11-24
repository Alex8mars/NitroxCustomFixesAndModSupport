using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata
{
    [Serializable]
    [DataContract]
    public class BulkheadDoorMetadata : EntityMetadata
    {
        [DataMember(Order = 1)]
        public bool Opened { get; }

        [DataMember(Order = 2)]
        public bool InitiallyOpen { get; }

        [IgnoreConstructor]
        protected BulkheadDoorMetadata()
        {
            //Constructor for serialization. Has to be "protected" for json serialization.
        }

        public BulkheadDoorMetadata(bool opened, bool initiallyOpen)
        {
            Opened = opened;
            InitiallyOpen = initiallyOpen;
        }

        public override string ToString()
        {
            return $"[BulkheadDoorMetadata Opened: {Opened} InitiallyOpen: {InitiallyOpen}]";
        }
    }
}
