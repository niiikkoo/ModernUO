using System;
using Server.Engines.PartySystem;
using Server.Guilds;
using Server.Items;
using Server.Regions;

namespace Server.Spells.Necromancy
{
    public class ExorcismSpell : NecromancerSpell
    {
        private static readonly SpellInfo _info = new(
            "Exorcism",
            "Ort Corp Grav",
            203,
            9031,
            Reagent.NoxCrystal,
            Reagent.GraveDust
        );

        private static readonly int Range = Core.ML ? 48 : 18;

        private static readonly Point3D[] m_GaiaLocs =
        {
            new(1470, 843, 0),
            new(1857, 865, -1),
            new(4220, 563, 36),
            new(1732, 3528, 0),
            new(1300, 644, 8),
            new(3355, 302, 9),
            new(1606, 2490, 5),
            new(2500, 3931, 3),
            new(4264, 3707, 0)
        };

        public ExorcismSpell(Mobile caster, Item scroll = null) : base(caster, scroll, _info)
        {
        }

        public override TimeSpan CastDelayBase => TimeSpan.FromSeconds(2.0);

        public override double RequiredSkill => 80.0;
        public override int RequiredMana => 40;

        public override bool DelayedDamage => false;

        public override bool CheckCast()
        {
            if (Caster.Skills.SpiritSpeak.Value < 100.0)
            {
                Caster.SendLocalizedMessage(1072112); // You must have GM Spirit Speak to use this spell
                return false;
            }

            return base.CheckCast();
        }

        public override int ComputeKarmaAward() => 0;

        public override void OnCast()
        {
            if (CheckSequence())
            {
                var map = Caster.Map;

                if (map != null)
                {
                    // Surprisingly, no sparkle type effects
                    foreach (var m in Caster.GetMobilesInRange(Range))
                    {
                        if (IsValidTarget(m))
                        {
                            m.Location = GetNearestShrine(m);
                        }
                    }
                }
            }

            FinishSequence();
        }

        private bool IsValidTarget(Mobile m)
        {
            if (!m.Player || m.Alive)
            {
                return false;
            }

            var c = m.Corpse as Corpse;
            var map = m.Map;

            if (c?.Deleted == false && map != null && c.Map == map)
            {
                if (m.Region.IsPartOf<DungeonRegion>() == Region.Find(c.Location, map).IsPartOf<DungeonRegion>())
                {
                    return false; // Same Map, both in Dungeon region OR They're both NOT in a dungeon region.
                }

                // Just an approximation cause RunUO doesn't divide up the world the same way OSI does ;p
            }

            if (Party.Get(m)?.Contains(Caster) == true)
            {
                return false;
            }

            if (m.Guild != null && Caster.Guild != null)
            {
                var mGuild = m.Guild as Guild;
                var cGuild = Caster.Guild as Guild;

                if (mGuild?.IsAlly(cGuild) == true || mGuild == cGuild)
                {
                    return false;
                }
            }

            return true;
        }

        private static Point3D GetNearestShrine(Mobile m)
        {
            var map = m.Map;

            Point3D[] locList;

            if (map == Map.Gaia)
            {
                locList = m_GaiaLocs;
            }
            else
            {
                locList = Array.Empty<Point3D>();
            }

            var closest = Point3D.Zero;
            var minDist = double.MaxValue;

            for (var i = 0; i < locList.Length; i++)
            {
                var p = locList[i];

                var dist = m.GetDistanceToSqrt(p);
                if (minDist > dist)
                {
                    closest = p;
                    minDist = dist;
                }
            }

            return closest;
        }
    }
}
