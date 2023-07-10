using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class MetallicLeatherDyeTub : LeatherDyeTub
    {
        [Constructible]
        public MetallicLeatherDyeTub() => LootType = LootType.Blessed;

        public override CustomHuePicker CustomHuePicker => null;

        public override int LabelNumber => 1153495; // Metallic Leather ...

        public override bool MetallicHues => true;
    }
}
