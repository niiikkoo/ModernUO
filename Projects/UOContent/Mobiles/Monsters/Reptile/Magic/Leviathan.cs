using ModernUO.Serialization;
using System;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class Leviathan : BaseCreature
    {
        [Constructible]
        public Leviathan(Mobile fisher = null) : base(AIType.AI_Mage)
        {
            Fisher = fisher;

            // May not be OSI accurate; mostly copied from krakens
            Body = 77;
            BaseSoundID = 353;

            Hue = 0x481;

            SetStr(1000);
            SetDex(501, 520);
            SetInt(501, 515);

            SetHits(1500);

            SetDamage(25, 33);

            SetDamageType(ResistanceType.Physical, 70);
            SetDamageType(ResistanceType.Cold, 30);

            SetResistance(ResistanceType.Physical, 55, 65);
            SetResistance(ResistanceType.Fire, 45, 55);
            SetResistance(ResistanceType.Cold, 45, 55);
            SetResistance(ResistanceType.Poison, 35, 45);
            SetResistance(ResistanceType.Energy, 25, 35);

            SetSkill(SkillName.EvalInt, 97.6, 107.5);
            SetSkill(SkillName.Magery, 97.6, 107.5);
            SetSkill(SkillName.MagicResist, 97.6, 107.5);
            SetSkill(SkillName.Meditation, 97.6, 107.5);
            SetSkill(SkillName.Tactics, 97.6, 107.5);
            SetSkill(SkillName.Wrestling, 97.6, 107.5);

            Fame = 24000;
            Karma = -24000;

            VirtualArmor = 50;

            CanSwim = true;
            CantWalk = true;

            PackItem(new MessageInABottle());
            PackItem(new Rope { ItemID = 0x14F8 });
            PackItem(new Rope { ItemID = 0x14FA });
        }

        public override string CorpseName => "a leviathan corpse";

        public Mobile Fisher { get; set; }

        public override string DefaultName => "a leviathan";

        public override int TreasureMapLevel => 5;

        private static MonsterAbility[] _abilities = { new LeviathanBreath() };
        public override MonsterAbility[] GetMonsterAbilities() => _abilities;

        public override void GenerateLoot()
        {
            AddLoot(LootPack.FilthyRich, 5);
        }

        private class LeviathanBreath : FireBreath
        {
            public override int PhysicalDamage => 70;
            public override int ColdDamage => 30;
            public override int FireDamage => 0;
            public override int BreathEffectHue => 0x1ED;
            public override double BreathDamageScalar => 0.05;
            public override TimeSpan MinTriggerCooldown => TimeSpan.FromSeconds(5.0);
            public override TimeSpan MaxTriggerCooldown => TimeSpan.FromSeconds(7.5);
        }
    }
}
