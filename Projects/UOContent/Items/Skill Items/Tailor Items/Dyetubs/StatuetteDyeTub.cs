using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class StatuetteDyeTub : DyeTub
    {
        [Constructible]
        public StatuetteDyeTub() => LootType = LootType.Blessed;

        public override bool AllowDyables => false;
        public override bool AllowStatuettes => true;
        public override int TargetMessage => 1049777; // Target the statuette to dye
        public override int FailMessage => 1049778;   // You can only dye veteran reward statuettes with this tub.
        public override int LabelNumber => 1049741;   // Reward Statuette Dye Tub
        public override CustomHuePicker CustomHuePicker => CustomHuePicker.LeatherDyeTub;
    }
}
