using System;
using System.Collections.Generic;
using Server.Items;
using Server.Spells;

namespace Server.Mobiles
{
    public class Harrower : BaseCreature
    {
        private static readonly SpawnEntry[] m_Entries =
        {
        };

        private static readonly double[] m_Offsets =
        {
            Math.Cos(000.0 / 180.0 * Math.PI), Math.Sin(000.0 / 180.0 * Math.PI),
            Math.Cos(040.0 / 180.0 * Math.PI), Math.Sin(040.0 / 180.0 * Math.PI),
            Math.Cos(080.0 / 180.0 * Math.PI), Math.Sin(080.0 / 180.0 * Math.PI),
            Math.Cos(120.0 / 180.0 * Math.PI), Math.Sin(120.0 / 180.0 * Math.PI),
            Math.Cos(160.0 / 180.0 * Math.PI), Math.Sin(160.0 / 180.0 * Math.PI),
            Math.Cos(200.0 / 180.0 * Math.PI), Math.Sin(200.0 / 180.0 * Math.PI),
            Math.Cos(240.0 / 180.0 * Math.PI), Math.Sin(240.0 / 180.0 * Math.PI),
            Math.Cos(280.0 / 180.0 * Math.PI), Math.Sin(280.0 / 180.0 * Math.PI),
            Math.Cos(320.0 / 180.0 * Math.PI), Math.Sin(320.0 / 180.0 * Math.PI)
        };

        private List<HarrowerTentacles> m_Tentacles;
        private Timer m_Timer;

        private bool m_TrueForm;

        [Constructible]
        public Harrower() : base(AIType.AI_Mage, FightMode.Closest, 18)
        {
            Instances.Add(this);
            Body = 146;

            SetStr(900, 1000);
            SetDex(125, 135);
            SetInt(1000, 1200);

            Fame = 22500;
            Karma = -22500;

            VirtualArmor = 60;

            SetDamageType(ResistanceType.Physical, 50);
            SetDamageType(ResistanceType.Energy, 50);

            SetResistance(ResistanceType.Physical, 55, 65);
            SetResistance(ResistanceType.Fire, 60, 80);
            SetResistance(ResistanceType.Cold, 60, 80);
            SetResistance(ResistanceType.Poison, 60, 80);
            SetResistance(ResistanceType.Energy, 60, 80);

            SetSkill(SkillName.Wrestling, 90.1, 100.0);
            SetSkill(SkillName.Tactics, 90.2, 110.0);
            SetSkill(SkillName.MagicResist, 120.2, 160.0);
            SetSkill(SkillName.Magery, 120.0);
            SetSkill(SkillName.EvalInt, 120.0);
            SetSkill(SkillName.Meditation, 120.0);

            m_Tentacles = new List<HarrowerTentacles>();

            m_Timer = new TeleportTimer(this);
            m_Timer.Start();
        }

        public Harrower(Serial serial) : base(serial)
        {
            Instances.Add(this);
        }

        public static List<Harrower> Instances { get; } = new();

        public static bool CanSpawn => Instances.Count == 0;

        public override string DefaultName => "the harrower";

        public override bool AutoDispel => true;
        public override bool Unprovokable => true;
        public override Poison PoisonImmune => Poison.Lethal;

        [CommandProperty(AccessLevel.GameMaster)]
        public override int HitsMax => m_TrueForm ? 65000 : 30000;

        [CommandProperty(AccessLevel.GameMaster)]
        public override int ManaMax => 5000;

        public override bool DisallowAllMoves => m_TrueForm;

        public static Harrower Spawn(Point3D platLoc, Map platMap)
        {
            if (Instances.Count > 0)
            {
                return null;
            }

            var entry = m_Entries.RandomElement();

            var harrower = new Harrower();

            harrower.MoveToWorld(entry.m_Location, Map.Gaia);

            //harrower.m_GateItem = new HarrowerGate(harrower, platLoc, platMap, entry.m_Entrance, Map.Gaia);

            return harrower;
        }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.SuperBoss, 2);
            AddLoot(LootPack.Meager);
        }

        public void Morph()
        {
            if (m_TrueForm)
            {
                return;
            }

            m_TrueForm = true;

            Name = "the true harrower";
            Body = 780;
            Hue = 0x497;

            Hits = HitsMax;
            Stam = StamMax;
            Mana = ManaMax;

            ProcessDelta();

            Say(1049499); // Behold my true form!

            var map = Map;

            if (map != null)
            {
                for (var i = 0; i < m_Offsets.Length; i += 2)
                {
                    var rx = m_Offsets[i];
                    var ry = m_Offsets[i + 1];

                    var dist = 0;
                    var ok = false;
                    int x = 0, y = 0, z = 0;

                    while (!ok && dist < 10)
                    {
                        var rdist = 10 + dist;

                        x = X + (int)(rx * rdist);
                        y = Y + (int)(ry * rdist);
                        z = map.GetAverageZ(x, y);

                        if (!(ok = map.CanFit(x, y, Z, 16, false, false)))
                        {
                            ok = map.CanFit(x, y, z, 16, false, false);
                        }

                        if (dist >= 0)
                        {
                            dist = -(dist + 1);
                        }
                        else
                        {
                            dist = -(dist - 1);
                        }
                    }

                    if (!ok)
                    {
                        continue;
                    }

                    var spawn = new HarrowerTentacles(this) { Team = Team };

                    spawn.MoveToWorld(new Point3D(x, y, z), map);

                    m_Tentacles.Add(spawn);
                }
            }
        }

        public override void OnAfterDelete()
        {
            Instances.Remove(this);

            base.OnAfterDelete();
        }

        public override void Serialize(IGenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write(0); // version

            writer.Write(m_TrueForm);
            writer.Write(m_Tentacles);
        }

        public override void Deserialize(IGenericReader reader)
        {
            base.Deserialize(reader);

            var version = reader.ReadInt();

            switch (version)
            {
                case 0:
                    {
                        m_TrueForm = reader.ReadBool();
                        m_Tentacles = reader.ReadEntityList<HarrowerTentacles>();

                        m_Timer = new TeleportTimer(this);
                        m_Timer.Start();

                        break;
                    }
            }
        }

        public override bool OnBeforeDeath()
        {
            if (m_TrueForm)
            {
                return base.OnBeforeDeath();
            }

            Morph();
            return false;
        }

        private class SpawnEntry
        {
            public readonly Point3D m_Entrance;
            public readonly Point3D m_Location;

            public SpawnEntry(Point3D loc, Point3D ent)
            {
                m_Location = loc;
                m_Entrance = ent;
            }
        }

        private class TeleportTimer : Timer
        {
            private static readonly int[] m_Offsets =
            {
                -1, -1,
                -1, 0,
                -1, 1,
                0, -1,
                0, 1,
                1, -1,
                1, 0,
                1, 1
            };

            private readonly Mobile m_Owner;

            public TeleportTimer(Mobile owner) : base(TimeSpan.FromSeconds(5.0), TimeSpan.FromSeconds(5.0))
            {

                m_Owner = owner;
            }

            protected override void OnTick()
            {
                if (m_Owner.Deleted)
                {
                    Stop();
                    return;
                }

                var map = m_Owner.Map;

                if (map == null)
                {
                    return;
                }

                if (Utility.RandomDouble() < 0.75)
                {
                    return;
                }

                var eable = m_Owner.GetMobilesInRange(16);
                Mobile toTeleport = null;
                foreach (var m in eable)
                {
                    if (m != m_Owner && m.Player && m_Owner.CanBeHarmful(m) && m_Owner.CanSee(m))
                    {
                        toTeleport = m;
                        break;
                    }
                }
                eable.Free();

                if (toTeleport == null)
                {
                    return;
                }

                var offset = Utility.Random(8) * 2;

                var to = m_Owner.Location;

                for (var i = 0; i < m_Offsets.Length; i += 2)
                {
                    var x = m_Owner.X + m_Offsets[(offset + i) % m_Offsets.Length];
                    var y = m_Owner.Y + m_Offsets[(offset + i + 1) % m_Offsets.Length];

                    if (map.CanSpawnMobile(x, y, m_Owner.Z))
                    {
                        to = new Point3D(x, y, m_Owner.Z);
                        break;
                    }

                    var z = map.GetAverageZ(x, y);

                    if (map.CanSpawnMobile(x, y, z))
                    {
                        to = new Point3D(x, y, z);
                        break;
                    }
                }

                var from = toTeleport.Location;

                toTeleport.Location = to;

                SpellHelper.Turn(m_Owner, toTeleport);
                SpellHelper.Turn(toTeleport, m_Owner);

                toTeleport.ProcessDelta();

                Effects.SendLocationParticles(
                    EffectItem.Create(from, toTeleport.Map, EffectItem.DefaultDuration),
                    0x3728,
                    10,
                    10,
                    2023
                );
                Effects.SendLocationParticles(
                    EffectItem.Create(to, toTeleport.Map, EffectItem.DefaultDuration),
                    0x3728,
                    10,
                    10,
                    5023
                );

                toTeleport.PlaySound(0x1FE);

                m_Owner.Combatant = toTeleport;
            }
        }

        private class GoodiesTimer : Timer
        {
            private readonly Map m_Map;
            private readonly int m_X;
            private readonly int m_Y;

            public GoodiesTimer(Map map, int x, int y) : base(TimeSpan.FromSeconds(Utility.RandomDouble() * 10.0))
            {

                m_Map = map;
                m_X = x;
                m_Y = y;
            }

            protected override void OnTick()
            {
                var z = m_Map.GetAverageZ(m_X, m_Y);
                var canFit = m_Map.CanFit(m_X, m_Y, z, 6, false, false);

                for (var i = -3; !canFit && i <= 3; ++i)
                {
                    canFit = m_Map.CanFit(m_X, m_Y, z + i, 6, false, false);

                    if (canFit)
                    {
                        z += i;
                    }
                }

                if (!canFit)
                {
                    return;
                }

                var g = new Gold(750, 1250);

                g.MoveToWorld(new Point3D(m_X, m_Y, z), m_Map);

                if (Utility.RandomDouble() < 0.05)
                {
                    return;
                }

                switch (Utility.Random(3))
                {
                    case 0: // Fire column
                        {
                            Effects.SendLocationParticles(
                                EffectItem.Create(g.Location, g.Map, EffectItem.DefaultDuration),
                                0x3709,
                                10,
                                30,
                                5052
                            );
                            Effects.PlaySound(g, 0x208);

                            break;
                        }
                    case 1: // Explosion
                        {
                            Effects.SendLocationParticles(
                                EffectItem.Create(g.Location, g.Map, EffectItem.DefaultDuration),
                                0x36BD,
                                20,
                                10,
                                5044
                            );
                            Effects.PlaySound(g, 0x307);

                            break;
                        }
                    case 2: // Ball of fire
                        {
                            Effects.SendLocationParticles(
                                EffectItem.Create(g.Location, g.Map, EffectItem.DefaultDuration),
                                0x36FE,
                                10,
                                10,
                                5052
                            );

                            break;
                        }
                }
            }
        }
    }
}
