using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class FurnitureDyeTub : DyeTub
    {
        [Constructible]
        public FurnitureDyeTub() => LootType = LootType.Blessed;

        public override bool AllowDyables => false;
        public override bool AllowFurniture => true;
        public override int TargetMessage => 501019; // Select the furniture to dye.
        public override int FailMessage => 501021;   // That is not a piece of furniture.
        public override int LabelNumber => 1041246;  // Furniture Dye Tubz
    }
}
