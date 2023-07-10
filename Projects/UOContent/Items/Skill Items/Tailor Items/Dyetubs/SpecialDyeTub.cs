using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class SpecialDyeTub : DyeTub
    {
        [Constructible]
        public SpecialDyeTub() => LootType = LootType.Blessed;

        public override CustomHuePicker CustomHuePicker => CustomHuePicker.SpecialDyeTub;
        public override int LabelNumber => 1041285; // Special Dye Tub
    }
}
