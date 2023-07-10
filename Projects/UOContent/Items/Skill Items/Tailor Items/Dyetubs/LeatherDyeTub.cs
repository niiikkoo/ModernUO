using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class LeatherDyeTub : DyeTub
    {
        [Constructible]
        public LeatherDyeTub() => LootType = LootType.Blessed;

        public override bool AllowDyables => false;
        public override bool AllowLeather => true;
        public override int TargetMessage => 1042416; // Select the leather item to dye.
        public override int FailMessage => 1042418;   // You can only dye leather with this tub.
        public override int LabelNumber => 1041284;   // Leather Dye Tub
        public override CustomHuePicker CustomHuePicker => CustomHuePicker.LeatherDyeTub;
    }
}
