using ModernUO.Serialization;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class Ferret : BaseCreature
    {

        [Constructible]
        public Ferret() : base(AIType.AI_Animal, FightMode.Aggressor)
        {
            Body = 0x117;

            SetStr(41, 48);
            SetDex(55);
            SetInt(75);

            SetHits(45, 50);

            SetDamage(7, 9);

            SetDamageType(ResistanceType.Physical, 100);

            SetResistance(ResistanceType.Physical, 45, 50);
            SetResistance(ResistanceType.Fire, 10, 14);
            SetResistance(ResistanceType.Cold, 30, 40);
            SetResistance(ResistanceType.Poison, 21, 25);
            SetResistance(ResistanceType.Energy, 20, 25);

            SetSkill(SkillName.MagicResist, 4.0);
            SetSkill(SkillName.Tactics, 4.0);
            SetSkill(SkillName.Wrestling, 4.0);

            Tamable = true;
            ControlSlots = 1;
            MinTameSkill = -21.3;
        }

        public override string CorpseName => "a ferret corpse";
        public override string DefaultName => "a ferret";

        public override int Meat => 1;
        public override FoodType FavoriteFood => FoodType.Fish;
    }
}
