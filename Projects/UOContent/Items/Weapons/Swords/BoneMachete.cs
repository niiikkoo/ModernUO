using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0)]
    public partial class BoneMachete : ElvenMachete
    {
        [Constructible]
        public BoneMachete() => ItemID = 0x20E;

        public override WeaponAbility PrimaryAbility => null;
        public override WeaponAbility SecondaryAbility => null;

        public override int PhysicalResistance => 1;
        public override int FireResistance => 1;
        public override int ColdResistance => 1;
        public override int PoisonResistance => 1;
        public override int EnergyResistance => 1;

        public override int InitMinHits => 5;
        public override int InitMaxHits => 5;
    }
}
