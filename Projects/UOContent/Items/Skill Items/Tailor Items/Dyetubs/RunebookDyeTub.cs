using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class RunebookDyeTub : DyeTub
    {
        [Constructible]
        public RunebookDyeTub() => LootType = LootType.Blessed;

        public override bool AllowDyables => false;
        public override bool AllowRunebooks => true;
        public override int TargetMessage => 1049774; // Target the runebook or runestone to dye
        public override int FailMessage => 1049775;   // You can only dye runestones or runebooks with this tub.
        public override int LabelNumber => 1049740;   // Runebook Dye Tub
        public override CustomHuePicker CustomHuePicker => CustomHuePicker.LeatherDyeTub;
    }
}
