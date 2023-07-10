using System;
using System.Collections.Generic;
using Server.Engines.PartySystem;
using Server.Guilds;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Multis;
using Server.Regions;
using Server.Spells.Fifth;
using Server.Spells.Necromancy;
using Server.Spells.Ninjitsu;
using Server.Spells.Seventh;
using Server.Targeting;

namespace Server
{
#pragma warning disable CA1052
    public class DefensiveSpell
    {
        public static void Nullify(Mobile from)
        {
            if (!from.CanBeginAction<DefensiveSpell>())
            {
                new InternalTimer(from).Start();
            }
        }

        private class InternalTimer : Timer
        {
            private readonly Mobile m_Mobile;

            public InternalTimer(Mobile m) : base(TimeSpan.FromMinutes(1.0))
            {
                m_Mobile = m;
            }

            protected override void OnTick()
            {
                m_Mobile.EndAction<DefensiveSpell>();
            }
        }
    }
#pragma warning restore CA1052
}

namespace Server.Spells
{
    public enum TravelCheckType
    {
        RecallFrom,
        RecallTo,
        GateFrom,
        GateTo,
        Mark,
        TeleportFrom,
        TeleportTo
    }

    public static class SpellHelper
    {
        private static readonly TimeSpan AosDamageDelay = TimeSpan.FromSeconds(1.25);
        private static readonly TimeSpan OldDamageDelay = TimeSpan.FromSeconds(0.5);

        private static readonly TimeSpan CombatHeatDelay = TimeSpan.FromSeconds(30.0);
        private static readonly bool RestrictTravelCombat = true;

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

        private static readonly TravelValidator[] m_Validators =
        {
            IsDungeon
        };

        // TODO: Move to configuration
        private static readonly bool[,] m_Rules =
        {
            /* T2A(Fel), Khaldun, Ilshenar, Wind(Tram), Wind(Fel), Dungeons(Fel), Solen(Tram), Solen(Fel), CrystalCave(Malas), Gauntlet(Malas), Gauntlet(Ferry), SafeZone, Stronghold, ChampionSpawn, Dungeons(Tokuno[Malas]), LampRoom(Doom), GuardianRoom(Doom), Heartwood, MLDungeons */
            /* Recall From */
            {
                false
            },
            /* Recall To */
            {
                false
            },
            /* Gate From */
            {
                false
            },
            /* Gate To */
            {
                false
            },
            /* Mark In */
            {
                false
            },
            /* Tele From */
            {
                true
            },
            /* Tele To */
            {
                true
            }
        };

        private static Mobile m_TravelCaster;
        private static TravelCheckType m_TravelType;

        public static TimeSpan GetDamageDelayForSpell(Spell sp) =>
            !sp.DelayedDamage ? TimeSpan.Zero :
            Core.AOS ? AosDamageDelay : OldDamageDelay;

        public static bool CheckMulti(Point3D p, Map map, bool houses = true, int housingrange = 0)
        {
            if (map == null || map == Map.Internal)
            {
                return false;
            }

            var sector = map.GetSector(p.X, p.Y);

            for (var i = 0; i < sector.Multis.Count; ++i)
            {
                var multi = sector.Multis[i];

                if (multi is BaseHouse bh)
                {
                    if (houses && bh.IsInside(p, 16) || housingrange > 0 && bh.InRange(p, housingrange))
                    {
                        return true;
                    }
                }
                else if (multi.Contains(p))
                {
                    return true;
                }
            }

            return false;
        }

        public static void Turn(Mobile from, object to)
        {
            if (to is not IPoint3D target)
            {
                return;
            }

            if (target is Item item)
            {
                if (item.RootParent != from)
                {
                    from.Direction = from.GetDirectionTo(item.GetWorldLocation());
                }
            }
            else if (!from.Equals(target))
            {
                from.Direction = from.GetDirectionTo(target);
            }
        }

        public static bool CheckCombat(Mobile m)
        {
            if (!RestrictTravelCombat)
            {
                return false;
            }

            for (var i = 0; i < m.Aggressed.Count; ++i)
            {
                var info = m.Aggressed[i];

                if (info.Defender.Player && Core.Now - info.LastCombatTime < CombatHeatDelay)
                {
                    return true;
                }
            }

            if (Core.AOS)
            {
                for (var i = 0; i < m.Aggressors.Count; ++i)
                {
                    var info = m.Aggressors[i];

                    if (info.Attacker.Player && info.Attacker == m && Core.Now - info.LastCombatTime < CombatHeatDelay)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool AdjustField(ref Point3D p, Map map, int height, bool mobsBlock)
        {
            if (map == null)
            {
                return false;
            }

            for (var offset = 0; offset < 10; ++offset)
            {
                var loc = new Point3D(p.X, p.Y, p.Z - offset);

                if (map.CanFit(loc, height, true, mobsBlock))
                {
                    p = loc;
                    return true;
                }
            }

            return false;
        }

        public static bool GetEastToWest(Point3D from, Point3D target)
        {
            var dx = from.X - target.X;
            var dy = from.Y - target.Y;
            var rx = (dx - dy) * 44;
            var ry = (dx + dy) * 44;

            return rx >= 0 && ry < 0 || ry >= 0 && rx < 0;
        }

        public static bool CanRevealCaster(Mobile m) => m is BaseCreature { Controlled: false };

        public static void GetSurfaceTop(ref IPoint3D p)
        {
            if (p is Item item)
            {
                p = item.GetSurfaceTop();
            }
            else if (p is StaticTarget t)
            {
                var z = t.Z;

                if ((t.Flags & TileFlag.Surface) == 0)
                {
                    z -= TileData.ItemTable[t.ItemID & TileData.MaxItemValue].CalcHeight;
                }

                p = new Point3D(t.X, t.Y, z);
            }
        }

        public static bool AddStatOffset(Mobile m, StatType type, int offset, TimeSpan duration) =>
            offset > 0
                ? AddStatBonus(m, m, type, offset, duration)
                : offset >= 0 || AddStatCurse(m, m, type, -offset, duration);

        public static bool AddStatBonus(Mobile caster, Mobile target, StatType type, TimeSpan duration, bool skillCheck = true) =>
            AddStatBonus(
                caster,
                target,
                type,
                GetOffset(caster, target, type, false, skillCheck),
                duration
            );

        public static bool AddStatBonus(Mobile caster, Mobile target, StatType type, int bonus, TimeSpan duration)
        {
            var name = $"[Magic] {type} Buff";

            var mod = target.GetStatMod(name);

            if (mod != null)
            {
                if (mod.Offset >= bonus)
                {
                    return false;
                }

                target.RemoveStatMod(mod);
            }

            target.AddStatMod(new StatMod(type, name, bonus, duration));
            return true;
        }

        public static bool AddStatCurse(Mobile caster, Mobile target, StatType type, TimeSpan duration, bool skillCheck = true) =>
            AddStatCurse(
                caster,
                target,
                type,
                GetOffset(caster, target, type, true, skillCheck),
                duration
            );

        public static bool AddStatCurse(Mobile caster, Mobile target, StatType type, int curse, TimeSpan duration)
        {
            var malus = -curse;
            var name = $"[Magic] {type} Curse";

            var mod = target.GetStatMod(name);

            if (mod != null)
            {
                if (mod.Offset <= malus)
                {
                    return false;
                }

                target.RemoveStatMod(mod);
            }

            target.AddStatMod(new StatMod(type, name, malus, duration));
            return true;
        }

        public static TimeSpan GetDuration(Mobile caster, Mobile target) =>
            // TODO: Is this accurate for Curse? Sources say it is magery. Should confirm at least for newest era.
            TimeSpan.FromSeconds(6 * (Core.AOS ? caster.Skills.EvalInt.Value : caster.Skills.Magery.Value) / 5.0);

        public static double GetOffsetScalar(Mobile caster, Mobile target, bool curse)
        {
            double percent;

            if (curse)
            {
                percent = 8 + caster.Skills.EvalInt.Fixed / 100 - target.Skills.MagicResist.Fixed / 100;
            }
            else
            {
                percent = 1 + caster.Skills.EvalInt.Fixed / 100;
            }

            percent *= 0.01;

            return Math.Max(percent, 0);
        }

        public static int GetOffset(Mobile caster, Mobile target, StatType type, bool curse, bool skillCheck)
        {
            if (!Core.AOS)
            {
                return 1 + (int)(caster.Skills.Magery.Value * 0.1);
            }

            if (skillCheck)
            {
                caster.CheckSkill(SkillName.EvalInt, 0.0, 120.0);

                if (curse)
                {
                    target.CheckSkill(SkillName.MagicResist, 0.0, 120.0);
                }
            }

            var percent = GetOffsetScalar(caster, target, curse);

            return type switch
            {
                StatType.Str => (int)(target.RawStr * percent),
                StatType.Dex => (int)(target.RawDex * percent),
                StatType.Int => (int)(target.RawInt * percent),
                _            => 1 + (int)(caster.Skills.Magery.Value * 0.1)
            };
        }

        public static Guild GetGuildFor(Mobile m)
        {
            var g = m.Guild as Guild;

            if (g == null && m is BaseCreature c)
            {
                m = c.ControlMaster;

                if (m != null)
                {
                    g = m.Guild as Guild;
                }

                if (g == null)
                {
                    m = c.SummonMaster;

                    if (m != null)
                    {
                        g = m.Guild as Guild;
                    }
                }
            }

            return g;
        }

        public static bool ValidIndirectTarget(Mobile from, Mobile to)
        {
            if (from == to)
            {
                return true;
            }

            if (to.Hidden && to.AccessLevel > from.AccessLevel)
            {
                return false;
            }

            var bcFrom = from as BaseCreature;
            var bcTarg = to as BaseCreature;

            var fromGuild = GetGuildFor(from);
            var toGuild = GetGuildFor(to);

            if (fromGuild != null && toGuild != null && (fromGuild == toGuild || fromGuild.IsAlly(toGuild)))
            {
                return false;
            }

            var p = Party.Get(from);

            if (p?.Contains(to) == true)
            {
                return false;
            }

            if (bcTarg != null && (bcTarg.Controlled || bcTarg.Summoned))
            {
                if (bcTarg.ControlMaster == from || bcTarg.SummonMaster == from)
                {
                    return false;
                }

                if (p != null && (p.Contains(bcTarg.ControlMaster) || p.Contains(bcTarg.SummonMaster)))
                {
                    return false;
                }
            }

            if (bcFrom != null && (bcFrom.Controlled || bcFrom.Summoned))
            {
                if (bcFrom.ControlMaster == to || bcFrom.SummonMaster == to)
                {
                    return false;
                }

                p = Party.Get(to);

                if (p != null && (p.Contains(bcFrom.ControlMaster) || p.Contains(bcFrom.SummonMaster)))
                {
                    return false;
                }
            }

            return bcTarg?.Controlled == false && bcTarg.InitialInnocent ||
                   Notoriety.Compute(from, to) != Notoriety.Innocent || from.Kills >= 5;
        }

        public static void Summon(
            BaseCreature creature, Mobile caster, int sound, TimeSpan duration, bool scaleDuration,
            bool scaleStats
        )
        {
            var map = caster.Map;

            if (map == null)
            {
                return;
            }

            var scale = 1.0 + (caster.Skills.Magery.Value - 100.0) / 200.0;

            if (scaleDuration)
            {
                duration = TimeSpan.FromSeconds(duration.TotalSeconds * scale);
            }

            if (scaleStats)
            {
                creature.RawStr = (int)(creature.RawStr * scale);
                creature.Hits = creature.HitsMax;

                creature.RawDex = (int)(creature.RawDex * scale);
                creature.Stam = creature.StamMax;

                creature.RawInt = (int)(creature.RawInt * scale);
                creature.Mana = creature.ManaMax;
            }

            var p = new Point3D(caster);

            if (FindValidSpawnLocation(map, ref p, true))
            {
                BaseCreature.Summon(creature, caster, p, sound, duration);
                return;
            }

            /*
            int offset = Utility.Random( 8 ) * 2;

            for ( int i = 0; i < m_Offsets.Length; i += 2 )
            {
              int x = caster.X + m_Offsets[(offset + i) % m_Offsets.Length];
              int y = caster.Y + m_Offsets[(offset + i + 1) % m_Offsets.Length];

              if (map.CanSpawnMobile( x, y, caster.Z ))
              {
                BaseCreature.Summon( creature, caster, new Point3D( x, y, caster.Z ), sound, duration );
                return;
              }
              else
              {
                int z = map.GetAverageZ( x, y );

                if (map.CanSpawnMobile( x, y, z ))
                {
                  BaseCreature.Summon( creature, caster, new Point3D( x, y, z ), sound, duration );
                  return;
                }
              }
            }
             * */

            creature.Delete();
            caster.SendLocalizedMessage(501942); // That location is blocked.
        }

        public static bool FindValidSpawnLocation(Map map, ref Point3D p, bool surroundingsOnly)
        {
            if (map == null) // sanity
            {
                return false;
            }

            if (!surroundingsOnly)
            {
                if (map.CanSpawnMobile(p)) // p's fine.
                {
                    p = new Point3D(p);
                    return true;
                }

                var z = map.GetAverageZ(p.X, p.Y);

                if (map.CanSpawnMobile(p.X, p.Y, z))
                {
                    p = new Point3D(p.X, p.Y, z);
                    return true;
                }
            }

            var offset = Utility.Random(8) * 2;

            for (var i = 0; i < m_Offsets.Length; i += 2)
            {
                var x = p.X + m_Offsets[(offset + i) % m_Offsets.Length];
                var y = p.Y + m_Offsets[(offset + i + 1) % m_Offsets.Length];

                if (map.CanSpawnMobile(x, y, p.Z))
                {
                    p = new Point3D(x, y, p.Z);
                    return true;
                }

                var z = map.GetAverageZ(x, y);

                if (map.CanSpawnMobile(x, y, z))
                {
                    p = new Point3D(x, y, z);
                    return true;
                }
            }

            return false;
        }

        private static TextDefinition _travelTo = 1019004;    // You are not allowed to travel there.
        private static TextDefinition _travelFrom = 502361;   // You cannot teleport into that area from here.
        private static TextDefinition _teleportTo = 501035;   // You cannot teleport from here to the destination.
        private static TextDefinition _genericSpell = 501802; // Thy spell doth not appear to work...

        public static TextDefinition InvalidTravelMessage(TravelCheckType type)
        {
            return type switch
            {
                TravelCheckType.RecallTo or TravelCheckType.GateTo         => _travelTo,
                TravelCheckType.RecallFrom or TravelCheckType.TeleportFrom => _travelFrom,
                TravelCheckType.TeleportTo                                 => _teleportTo,
                _                                                          => _genericSpell
            };
        }

        public static bool CheckTravel(Mobile caster, TravelCheckType type, out TextDefinition message) =>
            CheckTravel(caster, caster.Map, caster.Location, type, out message);

        public static bool CheckTravel(Map map, Point3D loc, TravelCheckType type, out TextDefinition message) =>
            CheckTravel(null, map, loc, type, out message);

        public static bool CheckTravel(Mobile caster, Map map, Point3D loc, TravelCheckType type, out TextDefinition message)
        {
            message = null;

            if (IsInvalid(map, loc)) // null, internal, out of bounds
            {
                message = InvalidTravelMessage(type);
                return false;
            }

            // Always allow monsters to teleport
            if (type is TravelCheckType.TeleportTo or TravelCheckType.TeleportFrom &&
                caster is BaseCreature { Controlled: false, Summoned: false })
            {
                return true;
            }

            m_TravelCaster = caster;
            m_TravelType = type;

            var v = (int)type;

            if (caster != null)
            {
                var destination = Region.Find(loc, map) as BaseRegion;
                var current = Region.Find(caster.Location, caster.Map) as BaseRegion;

                if (destination?.CheckTravel(caster, loc, type, out message) == false || current?.CheckTravel(caster, loc, type, out message) == false)
                {
                    message ??= InvalidTravelMessage(type);
                    return false;
                }
            }

            for (var i = 0; i < m_Validators.Length; ++i)
            {
                var isValid = m_Rules[v, i] || !m_Validators[i](map, loc);
                if (!isValid)
                {
                    message = InvalidTravelMessage(type);
                    return false;
                }
            }

            return true;
        }

        public static bool IsDungeon(Map map, Point3D loc)
        {
            var region = Region.Find(loc, map);
            return region.IsPartOf<DungeonRegion>();
        }

        public static bool IsInvalid(Map map, Point3D loc)
        {
            if (map == null || map == Map.Internal)
            {
                return true;
            }

            int x = loc.X, y = loc.Y;

            return x < 0 || y < 0 || x >= map.Width || y >= map.Height;
        }

        public static bool IsTown(Point3D loc, Mobile caster)
        {
            var map = caster.Map;

            if (map == null)
            {
                return false;
            }

            var reg = Region.Find(loc, map).GetRegion<GuardedRegion>();

            return reg?.IsDisabled() == false;
        }

        public static bool CheckTown(IPoint3D ip, Mobile caster) =>
            CheckTown((ip as Item)?.GetWorldLocation() ?? new Point3D(ip), caster);

        public static bool CheckTown(Point3D loc, Mobile caster)
        {
            if (IsTown(loc, caster))
            {
                caster.SendLocalizedMessage(500946); // You cannot cast this in town!
                return false;
            }

            return true;
        }

        // magic reflection
        public static bool CheckReflect(int circle, Mobile caster, ref Mobile target) =>
            CheckReflect(circle, ref caster, ref target);

        public static bool CheckReflect(int circle, ref Mobile caster, ref Mobile target)
        {
            var reflect = false;

            if (target.MagicDamageAbsorb > 0)
            {
                ++circle;

                target.MagicDamageAbsorb -= circle;

                // This order isn't very intuitive, but you have to nullify reflect before target gets switched
                reflect = target.MagicDamageAbsorb >= 0;
                if (target.MagicDamageAbsorb <= 0)
                {
                    target.MagicDamageAbsorb = 0;
                    DefensiveSpell.Nullify(target);
                }
            }

            if (target is BaseCreature creature)
            {
                creature.CheckReflect(caster, ref reflect);
            }

            if (reflect)
            {
                target.FixedEffect(0x37B9, 10, 5);
                (caster, target) = (target, caster);
            }

            return reflect;
        }

        public static void Damage(Spell spell, Mobile target, double damage)
        {
            var ts = GetDamageDelayForSpell(spell);

            Damage(spell, ts, target, spell.Caster, damage);
        }

        public static void Damage(TimeSpan delay, Mobile target, double damage)
        {
            Damage(delay, target, null, damage);
        }

        public static void Damage(TimeSpan delay, Mobile target, Mobile from, double damage)
        {
            Damage(null, delay, target, from, damage);
        }

        public static void Damage(Spell spell, TimeSpan delay, Mobile target, Mobile from, double damage)
        {
            var damageGiven = (int)damage;

            if (delay == TimeSpan.Zero)
            {
                var bcFrom = from as BaseCreature;
                var bcTarget = target as BaseCreature;

                bcFrom?.AlterSpellDamageTo(target, ref damageGiven);
                bcTarget?.AlterSpellDamageFrom(from, ref damageGiven);

                target.Damage(damageGiven, from);

                bcFrom?.OnDamageSpell(target, damageGiven);

                if (from != null)
                {
                    bcTarget?.OnHarmfulSpell(from);
                    bcTarget?.OnDamagedBySpell(from, damageGiven);
                }
            }
            else
            {
                new SpellDamageTimer(spell, target, from, damageGiven, delay).Start();
            }
        }

        public static void Damage(
            Spell spell, Mobile target, double damage, int phys, int fire, int cold, int pois,
            int nrgy, int chaos = 0, DFAlgorithm dfa = DFAlgorithm.Standard
        )
        {
            Damage(
                spell,
                GetDamageDelayForSpell(spell),
                target,
                spell.Caster,
                damage,
                phys,
                fire,
                cold,
                pois,
                nrgy,
                chaos,
                dfa
            );
        }

        public static void Damage(
            TimeSpan delay, Mobile target, double damage, int phys, int fire, int cold, int pois,
            int nrgy, int chaos = 0, DFAlgorithm dfa = DFAlgorithm.Standard
        )
        {
            Damage(delay, target, null, damage, phys, fire, cold, pois, nrgy, chaos, dfa);
        }

        public static void Damage(
            TimeSpan delay, Mobile target, Mobile from, double damage, int phys, int fire, int cold,
            int pois, int nrgy, int chaos = 0, DFAlgorithm dfa = DFAlgorithm.Standard
        )
        {
            Damage(null, delay, target, from, damage, phys, fire, cold, pois, nrgy, chaos, dfa);
        }

        public static void Damage(
            Spell spell, TimeSpan delay, Mobile target, Mobile from, double damage, int phys, int fire,
            int cold, int pois, int nrgy, int chaos = 0, DFAlgorithm dfa = DFAlgorithm.Standard
        )
        {
            var dmg = (int)damage;

            if (delay == TimeSpan.Zero)
            {
                var bcFrom = from as BaseCreature;
                var bcTarget = target as BaseCreature;
                bcFrom?.AlterSpellDamageTo(target, ref dmg);

                bcTarget?.AlterSpellDamageFrom(from, ref dmg);

                WeightOverloading.DFA = dfa;

                var damageGiven = AOS.Damage(target, from, dmg, phys, fire, cold, pois, nrgy, chaos);

                WeightOverloading.DFA = DFAlgorithm.Standard;

                bcFrom?.OnDamageSpell(target, damageGiven);

                if (bcTarget != null && from != null && delay == TimeSpan.Zero)
                {
                    bcTarget.OnHarmfulSpell(from);
                    bcTarget.OnDamagedBySpell(from, damageGiven);
                }
            }
            else
            {
                new SpellDamageTimerAOS(spell, delay, target, from, dmg, phys, fire, cold, pois, nrgy, chaos, dfa).Start();
            }
        }

        public static void DoLeech(int damageGiven, Mobile from, Mobile target)
        {
            if (target == null)
            {
                return;
            }

            var context = TransformationSpellHelper.GetContext(from);

            if (context == null) /* cleanup */
            {
                return;
            }

            if (context.Type == typeof(WraithFormSpell))
            {
                WraithFormSpell.DoWraithLeech(from, target, damageGiven);
            }
            else if (context.Type == typeof(VampiricEmbraceSpell))
            {
                from.Hits += Math.Min(target.Hits, AOS.Scale(damageGiven, 20));
                from.PlaySound(0x44D);
            }
        }
        public static void Heal(int amount, Mobile target, Mobile from, bool message = true)
        {
            // TODO: All Healing *spells* go through ArcaneEmpowerment
            target.Heal(amount, from, message);
        }

        private delegate bool TravelValidator(Map map, Point3D loc);

        private class SpellDamageTimer : Timer
        {
            private readonly Mobile m_From;
            private readonly Spell m_Spell;
            private readonly Mobile m_Target;
            private int m_Damage;

            public SpellDamageTimer(Spell s, Mobile target, Mobile from, int damage, TimeSpan delay) : base(delay)
            {
                m_Target = target;
                m_From = from;
                m_Damage = damage;
                m_Spell = s;

                if (m_Spell?.DelayedDamage == true)
                {
                    m_Spell.StartDelayedDamageContext(target, this);
                }
            }

            protected override void OnTick()
            {
                (m_From as BaseCreature)?.AlterSpellDamageTo(m_Target, ref m_Damage);
                (m_Target as BaseCreature)?.AlterSpellDamageFrom(m_From, ref m_Damage);

                m_Target.Damage(m_Damage);
                m_Spell?.RemoveDelayedDamageContext(m_Target);
            }
        }

        private class SpellDamageTimerAOS : Timer
        {
            private readonly int m_Chaos;
            private readonly int m_Cold;
            private readonly DFAlgorithm m_DFA;
            private readonly int m_Fire;
            private readonly Mobile m_From;
            private readonly int m_Nrgy;
            private readonly int m_Phys;
            private readonly int m_Pois;
            private readonly Spell m_Spell;
            private readonly Mobile m_Target;
            private int m_Damage;

            public SpellDamageTimerAOS(
                Spell s, TimeSpan delay, Mobile target, Mobile from, int damage, int phys, int fire, int cold,
                int pois, int nrgy, int chaos, DFAlgorithm dfa
            ) : base(delay)
            {
                m_Target = target;
                m_From = from;
                m_Damage = damage;
                m_Phys = phys;
                m_Fire = fire;
                m_Cold = cold;
                m_Pois = pois;
                m_Nrgy = nrgy;
                m_Chaos = chaos;
                m_DFA = dfa;
                m_Spell = s;

                if (m_Spell?.DelayedDamage == true)
                {
                    m_Spell.StartDelayedDamageContext(target, this);
                }
            }

            protected override void OnTick()
            {
                var bcFrom = m_From as BaseCreature;
                var bcTarg = m_Target as BaseCreature;

                if (m_Target != null)
                {
                    bcFrom?.AlterSpellDamageTo(m_Target, ref m_Damage);
                }

                if (m_From != null)
                {
                    bcTarg?.AlterSpellDamageFrom(m_From, ref m_Damage);
                }

                WeightOverloading.DFA = m_DFA;

                var damageGiven = AOS.Damage(m_Target, m_From, m_Damage, m_Phys, m_Fire, m_Cold, m_Pois, m_Nrgy, m_Chaos);

                WeightOverloading.DFA = DFAlgorithm.Standard;

                bcFrom?.OnDamageSpell(m_Target, damageGiven);

                if (m_From != null)
                {
                    bcTarg?.OnHarmfulSpell(m_From);
                    bcTarg?.OnDamagedBySpell(m_From, damageGiven);
                }

                m_Spell?.RemoveDelayedDamageContext(m_Target);
            }
        }
    }

    public static class TransformationSpellHelper
    {
        private static readonly Dictionary<Mobile, TransformContext> _table = new();

        public static bool CheckCast(Mobile caster, Spell spell)
        {
            if (!caster.CanBeginAction<PolymorphSpell>())
            {
                caster.SendLocalizedMessage(1061628); // You can't do that while polymorphed.
                return false;
            }

            if (AnimalForm.UnderTransformation(caster))
            {
                caster.SendLocalizedMessage(1061091); // You cannot cast that spell in this form.
                return false;
            }

            return true;
        }

        public static bool OnCast(Mobile caster, Spell spell)
        {
            if (spell is not ITransformationSpell transformSpell)
            {
                return false;
            }

            if (!caster.CanBeginAction<PolymorphSpell>())
            {
                caster.SendLocalizedMessage(1061628); // You can't do that while polymorphed.
            }
            else if (DisguisePersistence.IsDisguised(caster))
            {
                caster.SendLocalizedMessage(1061631); // You can't do that while disguised.
                return false;
            }
            else if (AnimalForm.UnderTransformation(caster))
            {
                caster.SendLocalizedMessage(1061091); // You cannot cast that spell in this form.
            }
            else if (!caster.CanBeginAction<IncognitoSpell>() || caster.IsBodyMod && GetContext(caster) == null)
            {
                spell.DoFizzle();
            }
            else if (spell.CheckSequence())
            {
                var context = GetContext(caster);
                var ourType = spell.GetType();

                var wasTransformed = context != null;
                var ourTransform = wasTransformed && context.Type == ourType;

                if (wasTransformed)
                {
                    RemoveContext(caster, context, ourTransform);

                    if (ourTransform)
                    {
                        caster.PlaySound(0xFA);
                        caster.FixedParticles(0x3728, 1, 13, 5042, EffectLayer.Waist);
                    }
                }

                if (!ourTransform)
                {
                    if (transformSpell.PhysResistOffset != 0)
                    {
                        caster.AddResistanceMod(new ResistanceMod(ResistanceType.Physical, "TransformSpell", transformSpell.PhysResistOffset));
                    }

                    if (transformSpell.FireResistOffset != 0)
                    {
                        caster.AddResistanceMod(new ResistanceMod(ResistanceType.Fire, "TransformSpell", transformSpell.FireResistOffset));
                    }

                    if (transformSpell.ColdResistOffset != 0)
                    {
                        caster.AddResistanceMod(new ResistanceMod(ResistanceType.Cold, "TransformSpell", transformSpell.ColdResistOffset));
                    }

                    if (transformSpell.PoisResistOffset != 0)
                    {
                        caster.AddResistanceMod(new ResistanceMod(ResistanceType.Poison, "TransformSpell", transformSpell.PoisResistOffset));
                    }

                    if (transformSpell.NrgyResistOffset != 0)
                    {
                        caster.AddResistanceMod(new ResistanceMod(ResistanceType.Energy, "TransformSpell", transformSpell.NrgyResistOffset));
                    }

                    if (!((Body)transformSpell.Body).IsHuman)
                    {
                        var mt = caster.Mount;

                        if (mt != null)
                        {
                            mt.Rider = null;
                        }
                    }

                    caster.BodyMod = transformSpell.Body;
                    caster.HueMod = transformSpell.Hue;

                    transformSpell.DoEffect(caster);

                    Timer timer = new TransformTimer(caster, transformSpell);
                    timer.Start();

                    AddContext(caster, new TransformContext(timer, ourType, transformSpell));
                    return true;
                }
            }

            return false;
        }

        public static void AddContext(Mobile m, TransformContext context)
        {
            _table[m] = context;
        }

        public static void RemoveContext(Mobile m, bool resetGraphics)
        {
            var context = GetContext(m);

            if (context != null)
            {
                RemoveContext(m, context, resetGraphics);
            }
        }

        public static void RemoveContext(Mobile m, TransformContext context, bool resetGraphics)
        {
            if (!_table.Remove(m))
            {
                return;
            }

            m.RemoveResistanceMod("TransformSpell");

            if (resetGraphics)
            {
                m.HueMod = -1;
                m.BodyMod = 0;
            }

            context.Timer.Stop();
            context.Spell.RemoveEffect(m);
        }

        public static TransformContext GetContext(Mobile m)
        {
            _table.TryGetValue(m, out var context);

            return context;
        }

        public static bool UnderTransformation(Mobile m) => GetContext(m) != null;

        public static bool UnderTransformation(Mobile m, Type type) => GetContext(m)?.Type == type;
    }

    public interface ITransformationSpell
    {
        int Body { get; }
        int Hue { get; }

        int PhysResistOffset { get; }
        int FireResistOffset { get; }
        int ColdResistOffset { get; }
        int PoisResistOffset { get; }
        int NrgyResistOffset { get; }

        double TickRate { get; }
        void OnTick(Mobile m);

        void DoEffect(Mobile m);
        void RemoveEffect(Mobile m);
    }

    public class TransformContext
    {
        public TransformContext(Timer timer, Type type, ITransformationSpell spell)
        {
            Timer = timer;
            Type = type;
            Spell = spell;
        }

        public Timer Timer { get; }

        public Type Type { get; }

        public ITransformationSpell Spell { get; }
    }

    public class TransformTimer : Timer
    {
        private readonly Mobile m_Mobile;
        private readonly ITransformationSpell m_Spell;

        public TransformTimer(Mobile from, ITransformationSpell spell)
            : base(TimeSpan.FromSeconds(spell.TickRate), TimeSpan.FromSeconds(spell.TickRate))
        {
            m_Mobile = from;
            m_Spell = spell;
        }

        protected override void OnTick()
        {
            if (m_Mobile.Deleted || !m_Mobile.Alive || m_Mobile.Body != m_Spell.Body || m_Mobile.Hue != m_Spell.Hue)
            {
                TransformationSpellHelper.RemoveContext(m_Mobile, true);
                Stop();
            }
            else
            {
                m_Spell.OnTick(m_Mobile);
            }
        }
    }
}
