using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Craft;
using Server.Engines.Help;
using Server.Engines.PartySystem;
using Server.Guilds;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Movement;
using Server.Multis;
using Server.Network;
using Server.Regions;
using Server.SkillHandlers;
using Server.Spells;
using Server.Spells.Bushido;
using Server.Spells.Fifth;
using Server.Spells.Fourth;
using Server.Spells.Necromancy;
using Server.Spells.Ninjitsu;
using Server.Spells.Seventh;
using Server.Spells.Sixth;
using Server.Spells.Spellweaving;
using Server.Targeting;
using Server.Utilities;
using CalcMoves = Server.Movement.Movement;
using RankDefinition = Server.Guilds.RankDefinition;

namespace Server.Mobiles
{
    [Flags]
    public enum PlayerFlag // First 16 bits are reserved for default-distro use, start custom flags at 0x00010000
    {
        None = 0x00000000,
        Glassblowing = 0x00000001,
        Masonry = 0x00000002,
        SandMining = 0x00000004,
        StoneMining = 0x00000008,
        ToggleMiningStone = 0x00000010,
        KarmaLocked = 0x00000020,
        UseOwnFilter = 0x00000040,
        PagingSquelched = 0x00000080,
        AcceptGuildInvites = 0x00000100,
        HasStatReward = 0x00000200,
        RefuseTrades = 0x00000400
    }

    public enum NpcGuild
    {
        None,
        MagesGuild,
        WarriorsGuild,
        ThievesGuild,
        RangersGuild,
        HealersGuild,
        MinersGuild,
        MerchantsGuild,
        TinkersGuild,
        TailorsGuild,
        FishermensGuild,
        BardsGuild,
        BlacksmithsGuild
    }

    public enum BlockMountType
    {
        None = -1,
        Dazed = 1040024,
        BolaRecovery = 1062910,
        DismountRecovery = 1070859
    }

    public class PlayerMobile : Mobile
    {
        private static bool m_NoRecursion;

        private static readonly Point3D[] m_GaiaDeathDestinations =
        {
            new(1481, 1612, 20),
            new(2708, 2153, 0),
            new(2249, 1230, 0),
            new(5197, 3994, 37),
            new(1412, 3793, 0),
            new(3688, 2232, 20),
            new(2578, 604, 0),
            new(4397, 1089, 0),
            new(5741, 3218, -2),
            new(2996, 3441, 15),
            new(624, 2225, 0),
            new(1916, 2814, 0),
            new(2929, 854, 0),
            new(545, 967, 0),
            new(3665, 2587, 0)
        };

        private Dictionary<int, bool> m_AcquiredRecipes;

        private List<Mobile> m_AllFollowers;
        private int m_BeardModID = -1, m_BeardModHue;

        // TODO: Pool BuffInfo objects
        private Dictionary<BuffIcon, BuffInfo> m_BuffTable;

        private Type m_EnemyOfOneType;
        private TimeSpan m_GameTime;

        /*
         * a value of zero means, that the mobile is not executing the spell. Otherwise,
         * the value should match the BaseMana required
        */

        private RankDefinition m_GuildRank;

        private int m_HairModID = -1, m_HairModHue;

        private int m_LastGlobalLight = -1, m_LastPersonalLight = -1;

        private TimeSpan m_LongTermElapse;

        private bool m_LastProtectedMessage;

        private MountBlock _mountBlock;

        private int m_NextMovementTime;
        private int m_NextProtectionCheck = 10;

        private bool m_NoDeltaRecursion;

        private DateTime m_SavagePaintExpiration;
        private TimeSpan m_ShortTermElapse;

        private DateTime[] m_StuckMenuUses;

        private QuestArrow m_QuestArrow;

        public PlayerMobile()
        {
            AutoStabled = new List<Mobile>();

            VisibilityList = new List<Mobile>();
            PermaFlags = new List<Mobile>();
            RecentlyReported = new List<Mobile>();

            m_GameTime = TimeSpan.Zero;
            m_ShortTermElapse = TimeSpan.FromHours(8.0);
            m_LongTermElapse = TimeSpan.FromHours(40.0);

            m_GuildRank = RankDefinition.Lowest;
        }

        public PlayerMobile(Serial s) : base(s)
        {
            VisibilityList = new List<Mobile>();
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime AnkhNextUse { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan DisguiseTimeLeft => DisguisePersistence.TimeRemaining(this);

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime PeacedUntil { get; set; }

        public DesignContext DesignContext { get; set; }

        public BlockMountType MountBlockReason => _mountBlock?.MountBlockReason ?? BlockMountType.None;

        public override int MaxWeight => (Core.ML && Race == Race.Human ? 100 : 40) + (int)(3.5 * Str);

        public override double ArmorRating
        {
            get
            {
                // BaseArmor ar;
                var rating = 0.0;

                AddArmorRating(ref rating, NeckArmor);
                AddArmorRating(ref rating, HandArmor);
                AddArmorRating(ref rating, HeadArmor);
                AddArmorRating(ref rating, ArmsArmor);
                AddArmorRating(ref rating, LegsArmor);
                AddArmorRating(ref rating, ChestArmor);
                AddArmorRating(ref rating, ShieldArmor);

                return VirtualArmor + VirtualArmorMod + rating;
            }
        }

        public SkillName[] AnimalFormRestrictedSkills { get; } =
        {
            SkillName.ArmsLore, SkillName.Begging, SkillName.Discordance, SkillName.Forensics,
            SkillName.Inscribe, SkillName.ItemID, SkillName.Meditation, SkillName.Peacemaking,
            SkillName.Provocation, SkillName.RemoveTrap, SkillName.SpiritSpeak, SkillName.Stealing,
            SkillName.TasteID
        };

        public override double RacialSkillBonus
        {
            get
            {
                if (Core.ML && Race == Race.Human)
                {
                    return 20.0;
                }

                return 0;
            }
        }

        public List<Item> EquipSnapshot { get; private set; }

        public SkillName Learning { get; set; } = (SkillName)(-1);

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan SavagePaintExpiration
        {
            get => Utility.Max(m_SavagePaintExpiration - Core.Now, TimeSpan.Zero);
            set => m_SavagePaintExpiration = Core.Now + value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime LastEscortTime { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime LastPetBallTime { get; set; }

        public List<Mobile> VisibilityList { get; }

        public List<Mobile> PermaFlags { get; private set; }

        public override int Luck => AosAttributes.GetValue(this, AosAttribute.Luck);

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime SessionStart { get; private set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan GameTime
        {
            get
            {
                if (NetState != null)
                {
                    return m_GameTime + (Core.Now - SessionStart);
                }

                return m_GameTime;
            }
        }

        public override bool NewGuildDisplay => Guilds.Guild.NewGuildSystem;

        public bool BedrollLogout { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public override bool Paralyzed
        {
            get => base.Paralyzed;
            set
            {
                base.Paralyzed = value;

                if (value)
                {
                    AddBuff(new BuffInfo(BuffIcon.Paralyze, 1075827)); // Paralyze/You are frozen and can not move
                }
                else
                {
                    RemoveBuff(BuffIcon.Paralyze);
                }
            }
        }

        public List<Mobile> RecentlyReported { get; set; }

        public List<Mobile> AutoStabled { get; private set; }

        public bool NinjaWepCooldown { get; set; }

        public List<Mobile> AllFollowers => m_AllFollowers ??= new List<Mobile>();

        public RankDefinition GuildRank
        {
            get => AccessLevel >= AccessLevel.GameMaster ? RankDefinition.Leader : m_GuildRank;
            set => m_GuildRank = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int GuildMessageHue { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public int AllianceMessageHue { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public int Profession { get; set; }

        public int StepsTaken { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool IsStealthing // IsStealthing should be moved to Server.Mobiles
        {
            get;
            set;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public NpcGuild NpcGuild { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NpcGuildJoinTime { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime LastOnline { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public long LastMoved => LastMoveTime;

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan NpcGuildGameTime { get; set; }

        public int ExecutesLightningStrike { get; set; }

        public PlayerFlag Flags { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool PagingSquelched
        {
            get => GetFlag(PlayerFlag.PagingSquelched);
            set => SetFlag(PlayerFlag.PagingSquelched, value);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Glassblowing
        {
            get => GetFlag(PlayerFlag.Glassblowing);
            set => SetFlag(PlayerFlag.Glassblowing, value);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Masonry
        {
            get => GetFlag(PlayerFlag.Masonry);
            set => SetFlag(PlayerFlag.Masonry, value);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool SandMining
        {
            get => GetFlag(PlayerFlag.SandMining);
            set => SetFlag(PlayerFlag.SandMining, value);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool StoneMining
        {
            get => GetFlag(PlayerFlag.StoneMining);
            set => SetFlag(PlayerFlag.StoneMining, value);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool ToggleMiningStone
        {
            get => GetFlag(PlayerFlag.ToggleMiningStone);
            set => SetFlag(PlayerFlag.ToggleMiningStone, value);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool KarmaLocked
        {
            get => GetFlag(PlayerFlag.KarmaLocked);
            set => SetFlag(PlayerFlag.KarmaLocked, value);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool UseOwnFilter
        {
            get => GetFlag(PlayerFlag.UseOwnFilter);
            set => SetFlag(PlayerFlag.UseOwnFilter, value);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool AcceptGuildInvites
        {
            get => GetFlag(PlayerFlag.AcceptGuildInvites);
            set => SetFlag(PlayerFlag.AcceptGuildInvites, value);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool HasStatReward
        {
            get => GetFlag(PlayerFlag.HasStatReward);
            set => SetFlag(PlayerFlag.HasStatReward, value);
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool RefuseTrades
        {
            get => GetFlag(PlayerFlag.RefuseTrades);
            set => SetFlag(PlayerFlag.RefuseTrades, value);
        }

        public Dictionary<Type, int> RecoverableAmmo { get; } = new();

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime AcceleratedStart { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public SkillName AcceleratedSkill { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public override int HitsMax
        {
            get
            {
                int strBase;
                var strOffs = GetStatOffset(StatType.Str);

                if (Core.AOS)
                {
                    strBase = Str; // this.Str already includes GetStatOffset/str
                    strOffs = AosAttributes.GetValue(this, AosAttribute.BonusHits);

                    if (Core.ML && strOffs > 25 && AccessLevel <= AccessLevel.Player)
                    {
                        strOffs = 25;
                    }

                    if (/*AnimalForm.UnderTransformation(this, typeof(BakeKitsune)) ||*/
                        AnimalForm.UnderTransformation(this, typeof(GreyWolf)))
                    {
                        strOffs += 20;
                    }
                }
                else
                {
                    strBase = RawStr;
                }

                return strBase / 2 + 50 + strOffs;
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override int StamMax => base.StamMax + AosAttributes.GetValue(this, AosAttribute.BonusStam);

        [CommandProperty(AccessLevel.GameMaster)]
        public override int ManaMax => base.ManaMax + AosAttributes.GetValue(this, AosAttribute.BonusMana) +
                                       (Core.ML && Race == Race.Elf ? 20 : 0);

        [CommandProperty(AccessLevel.GameMaster)]
        public override int Str
        {
            get
            {
                if (Core.ML && AccessLevel == AccessLevel.Player)
                {
                    return Math.Min(base.Str, 150);
                }

                return base.Str;
            }
            set => base.Str = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override int Int
        {
            get
            {
                if (Core.ML && AccessLevel == AccessLevel.Player)
                {
                    return Math.Min(base.Int, 150);
                }

                return base.Int;
            }
            set => base.Int = value;
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public override int Dex
        {
            get
            {
                if (Core.ML && AccessLevel == AccessLevel.Player)
                {
                    return Math.Min(base.Dex, 150);
                }

                return base.Dex;
            }
            set => base.Dex = value;
        }

        public Type EnemyOfOneType
        {
            get => m_EnemyOfOneType;
            set
            {
                var oldType = m_EnemyOfOneType;
                var newType = value;

                if (oldType == newType)
                {
                    return;
                }

                m_EnemyOfOneType = value;

                DeltaEnemies(oldType, newType);
            }
        }

        public bool WaitingForEnemy { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public int AvailableResurrects { get; set; }

        public SpeechLog SpeechLog { get; private set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public int KnownRecipes => m_AcquiredRecipes?.Count ?? 0;

        public QuestArrow QuestArrow
        {
            get => m_QuestArrow;
            set
            {
                if (m_QuestArrow != value)
                {
                    m_QuestArrow?.Stop();

                    m_QuestArrow = value;
                }
            }
        }

        public void ClearQuestArrow() => m_QuestArrow = null;

        public override void ToggleFlying()
        {
            if (Race != Race.Gargoyle)
            {
                return;
            }

            if (Flying)
            {
                Freeze(TimeSpan.FromSeconds(1));
                Animate(61, 10, 1, true, false, 0);
                Flying = false;
                BuffInfo.RemoveBuff(this, BuffIcon.Fly);
                SendMessage("You have landed.");

                BaseMount.Dismount(this);
                return;
            }

            var type = MountBlockReason;

            if (!Alive)
            {
                SendLocalizedMessage(1113082); // You may not fly while dead.
            }
            else if (IsBodyMod && !(BodyMod == 666 || BodyMod == 667))
            {
                SendLocalizedMessage(1112453); // You can't fly in your current form!
            }
            else if (type != BlockMountType.None)
            {
                switch (type)
                {
                    case BlockMountType.Dazed:
                        {
                            SendLocalizedMessage(1112457); // You are still too dazed to fly.
                            break;
                        }
                    case BlockMountType.BolaRecovery:
                        {
                            SendLocalizedMessage(1112455); // You cannot fly while recovering from a bola throw.
                            break;
                        }
                    case BlockMountType.DismountRecovery:
                        {
                            // You cannot fly while recovering from a dismount maneuver.
                            SendLocalizedMessage(1112456);
                            break;
                        }
                }
            }
            else if (Hits < 25) // TODO confirm
            {
                SendLocalizedMessage(1112454); // You must heal before flying.
            }
            else
            {
                if (!Flying)
                {
                    // No message?
                    if (Spell is FlySpell spell)
                    {
                        spell.Stop();
                    }

                    new FlySpell(this).Cast();
                }
                else
                {
                    Flying = false;
                    BuffInfo.RemoveBuff(this, BuffIcon.Fly);
                }
            }
        }

        public static Direction GetDirection4(Point3D from, Point3D to)
        {
            var dx = from.X - to.X;
            var dy = from.Y - to.Y;

            var rx = dx - dy;
            var ry = dx + dy;

            Direction ret;

            if (rx >= 0 && ry >= 0)
            {
                ret = Direction.West;
            }
            else if (rx >= 0 && ry < 0)
            {
                ret = Direction.South;
            }
            else if (rx < 0 && ry < 0)
            {
                ret = Direction.East;
            }
            else
            {
                ret = Direction.North;
            }

            return ret;
        }

        public override bool OnDroppedItemToWorld(Item item, Point3D location)
        {
            if (!base.OnDroppedItemToWorld(item, location))
            {
                return false;
            }

            if (Core.AOS)
            {
                var mobiles = Map.GetMobilesInRange(location, 0);

                foreach (Mobile m in mobiles)
                {
                    if (m.Z >= location.Z && m.Z < location.Z + 16 && (!m.Hidden || m.AccessLevel == AccessLevel.Player))
                    {
                        mobiles.Free();
                        return false;
                    }
                }

                mobiles.Free();
            }

            var bi = item.GetBounce();

            if (bi == null)
            {
                return true;
            }

            var type = item.GetType();

            if (type.IsDefined(typeof(FurnitureAttribute), true) ||
                type.IsDefined(typeof(DynamicFlippingAttribute), true))
            {
                var objs = type.GetCustomAttributes(typeof(FlippableAttribute), true);

                if (objs.Length > 0)
                {
                    if (objs[0] is FlippableAttribute fp)
                    {
                        var itemIDs = fp.ItemIDs;

                        var oldWorldLoc = bi.WorldLoc;
                        var newWorldLoc = location;

                        if (oldWorldLoc.X != newWorldLoc.X || oldWorldLoc.Y != newWorldLoc.Y)
                        {
                            var dir = GetDirection4(oldWorldLoc, newWorldLoc);

                            item.ItemID = itemIDs.Length switch
                            {
                                2 => dir switch
                                {
                                    Direction.North => itemIDs[0],
                                    Direction.South => itemIDs[0],
                                    Direction.East  => itemIDs[1],
                                    Direction.West  => itemIDs[1],
                                    _               => item.ItemID
                                },
                                4 => dir switch
                                {
                                    Direction.South => itemIDs[0],
                                    Direction.East  => itemIDs[1],
                                    Direction.North => itemIDs[2],
                                    Direction.West  => itemIDs[3],
                                    _               => item.ItemID
                                },
                                _ => item.ItemID
                            };
                        }
                    }
                }
            }

            return true;
        }

        public bool GetFlag(PlayerFlag flag) => (Flags & flag) != 0;

        public void SetFlag(PlayerFlag flag, bool value)
        {
            if (value)
            {
                Flags |= flag;
            }
            else
            {
                Flags &= ~flag;
            }
        }

        public static void Initialize()
        {
            EventSink.Login += OnLogin;
            EventSink.Logout += OnLogout;
            EventSink.Connected += EventSink_Connected;
            EventSink.Disconnected += EventSink_Disconnected;

            EventSink.TargetedSkillUse += TargetedSkillUse;
            EventSink.EquipMacro += EquipMacro;
            EventSink.UnequipMacro += UnequipMacro;

            if (Core.SE)
            {
                Timer.StartTimer(CheckPets);
            }
        }

        private static void TargetedSkillUse(Mobile from, IEntity target, int skillId)
        {
            if (from == null || target == null)
            {
                return;
            }

            from.TargetLocked = true;

            if (skillId == 35)
            {
                AnimalTaming.DisableMessage = true;
            }
            // AnimalTaming.DeferredTarget = false;

            if (from.UseSkill(skillId))
            {
                from.Target?.Invoke(from, target);
            }

            if (skillId == 35)
                // AnimalTaming.DeferredTarget = true;
            {
                AnimalTaming.DisableMessage = false;
            }

            from.TargetLocked = false;
        }

        public static void EquipMacro(Mobile m, List<Serial> list)
        {
            if (m is PlayerMobile { Alive: true } pm && pm.Backpack != null)
            {
                var pack = pm.Backpack;

                foreach (var serial in list)
                {
                    Item item = null;
                    foreach (var i in pack.Items)
                    {
                        if (i.Serial == serial)
                        {
                            item = i;
                            break;
                        }
                    }

                    if (item == null)
                    {
                        continue;
                    }

                    var toMove = pm.FindItemOnLayer(item.Layer);

                    if (toMove != null)
                    {
                        // pack.DropItem(toMove);
                        toMove.Internalize();

                        if (!pm.EquipItem(item))
                        {
                            pm.EquipItem(toMove);
                        }
                        else
                        {
                            pack.DropItem(toMove);
                        }
                    }
                    else
                    {
                        pm.EquipItem(item);
                    }
                }
            }
        }

        public static void UnequipMacro(Mobile m, List<Layer> layers)
        {
            if (m is PlayerMobile { Alive: true } pm && pm.Backpack != null)
            {
                var pack = pm.Backpack;
                var eq = m.Items;

                for (var i = eq.Count - 1; i >= 0; i--)
                {
                    var item = eq[i];
                    if (layers.Contains(item.Layer))
                    {
                        pack.TryDropItem(pm, item, false);
                    }
                }
            }
        }

        private static void CheckPets()
        {
            foreach (var m in World.Mobiles.Values)
            {
                if (m is PlayerMobile pm &&
                    ((!pm.Mounted /*|| pm.Mount is EtherealMount*/) && pm.AllFollowers.Count > pm.AutoStabled.Count ||
                     pm.Mounted && pm.AllFollowers.Count > pm.AutoStabled.Count + 1))
                {
                    pm.AutoStablePets(); /* autostable checks summons, et al: no need here */
                }
            }
        }

        public void SetMountBlock(BlockMountType type, bool dismount) =>
            SetMountBlock(type, TimeSpan.MaxValue, dismount);

        public void SetMountBlock(BlockMountType type, TimeSpan duration, bool dismount)
        {
            if (dismount)
            {
                if (Mount != null)
                {
                    Mount.Rider = null;
                }
                else if (AnimalForm.UnderTransformation(this))
                {
                    AnimalForm.RemoveContext(this, true);
                }
            }

            if (_mountBlock == null || !_mountBlock.CheckBlock() || _mountBlock.Expiration < Core.Now + duration)
            {
                _mountBlock?.RemoveBlock(this);
                _mountBlock = new MountBlock(duration, type, this);
            }
        }

        public override void OnSkillInvalidated(Skill skill)
        {
            if (Core.AOS && skill.SkillName == SkillName.MagicResist)
            {
                UpdateResistances();
            }
        }

        public override int GetMaxResistance(ResistanceType type)
        {
            if (AccessLevel > AccessLevel.Player)
            {
                return 100;
            }

            var max = base.GetMaxResistance(type);

            if (type != ResistanceType.Physical && CurseSpell.UnderEffect(this))
            {
                max -= 10;
            }

            if (Core.ML && Race == Race.Elf && type == ResistanceType.Energy)
            {
                max += 5; // Intended to go after the 60 max from curse
            }

            return max;
        }

        protected override void OnRaceChange(Race oldRace)
        {
            ValidateEquipment();
            UpdateResistances();
        }

        public override void OnNetStateChanged()
        {
            m_LastGlobalLight = -1;
            m_LastPersonalLight = -1;
        }

        public override void ComputeBaseLightLevels(out int global, out int personal)
        {
            global = LightCycle.ComputeLevelFor(this);

            var racialNightSight = Core.ML && Race == Race.Elf;

            if (LightLevel < 21 && (AosAttributes.GetValue(this, AosAttribute.NightSight) > 0 || racialNightSight))
            {
                personal = 21;
            }
            else
            {
                personal = LightLevel;
            }
        }

        public override void CheckLightLevels(bool forceResend)
        {
            var ns = NetState;

            if (ns == null)
            {
                return;
            }

            ComputeLightLevels(out var global, out var personal);

            if (!forceResend)
            {
                forceResend = global != m_LastGlobalLight || personal != m_LastPersonalLight;
            }

            if (!forceResend)
            {
                return;
            }

            m_LastGlobalLight = global;
            m_LastPersonalLight = personal;

            ns.SendGlobalLightLevel(global);
            ns.SendPersonalLightLevel(Serial, personal);
        }

        public override int GetMinResistance(ResistanceType type)
        {
            var magicResist = (int)(Skills.MagicResist.Value * 10);
            int min;

            if (magicResist >= 1000)
            {
                min = 40 + (magicResist - 1000) / 50;
            }
            else if (magicResist >= 400)
            {
                min = (magicResist - 400) / 15;
            }
            else
            {
                min = int.MinValue;
            }

            return Math.Clamp(min, base.GetMinResistance(type), MaxPlayerResistance);
        }

        public override void OnManaChange(int oldValue)
        {
            base.OnManaChange(oldValue);
            if (ExecutesLightningStrike > 0)
            {
                if (Mana < ExecutesLightningStrike)
                {
                    SpecialMove.ClearCurrentMove(this);
                }
            }
        }

        private static void OnLogin(Mobile from)
        {
            if (AccountHandler.LockdownLevel > AccessLevel.Player)
            {
                string notice;

                if (from.Account is not Account acct || !acct.HasAccess(from.NetState))
                {
                    if (from.AccessLevel == AccessLevel.Player)
                    {
                        notice = "The server is currently under lockdown. No players are allowed to log in at this time.";
                    }
                    else
                    {
                        notice =
                            "The server is currently under lockdown. You do not have sufficient access level to connect.";
                    }

                    if (from.NetState != null)
                    {
                        Timer.StartTimer(TimeSpan.FromSeconds(1.0), () => from.NetState.Disconnect("Server is locked down"));
                    }
                }
                else if (from.AccessLevel >= AccessLevel.Administrator)
                {
                    notice =
                        "The server is currently under lockdown. As you are an administrator, you may change this from the [Admin gump.";
                }
                else
                {
                    notice = "The server is currently under lockdown. You have sufficient access level to connect.";
                }

                from.SendGump(new NoticeGump(1060637, 30720, notice, 0xFFC000, 300, 140));
                return;
            }

            if (from is PlayerMobile mobile)
            {
                mobile.ClaimAutoStabledPets();
            }
        }

        public void ValidateEquipment()
        {
            if (m_NoDeltaRecursion || Map == null || Map == Map.Internal)
            {
                return;
            }

            if (Items == null)
            {
                return;
            }

            m_NoDeltaRecursion = true;
            Timer.StartTimer(ValidateEquipment_Sandbox);
        }

        private void ValidateEquipment_Sandbox()
        {
            try
            {
                if (Map == null || Map == Map.Internal)
                {
                    return;
                }

                var items = Items;

                if (items == null)
                {
                    return;
                }

                var moved = false;

                var str = Str;
                var dex = Dex;
                var intel = Int;

                Mobile from = this;

                for (var i = items.Count - 1; i >= 0; --i)
                {
                    if (i >= items.Count)
                    {
                        continue;
                    }

                    var item = items[i];

                    if (item is BaseWeapon weapon)
                    {
                        var drop = false;

                        if (dex < weapon.DexRequirement)
                        {
                            drop = true;
                        }
                        else if (str < AOS.Scale(weapon.StrRequirement, 100 - weapon.GetLowerStatReq()))
                        {
                            drop = true;
                        }
                        else if (intel < weapon.IntRequirement)
                        {
                            drop = true;
                        }
                        else if (!weapon.CheckRace(Race))
                        {
                            drop = true;
                        }

                        if (drop)
                        {
                            // You can no longer wield your ~1_WEAPON~
                            from.SendLocalizedMessage(1062001, weapon.Name ?? $"#{weapon.LabelNumber}");
                            from.AddToBackpack(weapon);
                            moved = true;
                        }
                    }
                    else if (item is BaseArmor armor)
                    {
                        var drop = false;

                        if (!armor.AllowMaleWearer && !from.Female && from.AccessLevel < AccessLevel.GameMaster)
                        {
                            drop = true;
                        }
                        else if (!armor.AllowFemaleWearer && from.Female && from.AccessLevel < AccessLevel.GameMaster)
                        {
                            drop = true;
                        }
                        else if (!armor.CheckRace(Race))
                        {
                            drop = true;
                        }
                        else
                        {
                            int strBonus = armor.ComputeStatBonus(StatType.Str), strReq = armor.ComputeStatReq(StatType.Str);
                            int dexBonus = armor.ComputeStatBonus(StatType.Dex), dexReq = armor.ComputeStatReq(StatType.Dex);
                            int intBonus = armor.ComputeStatBonus(StatType.Int), intReq = armor.ComputeStatReq(StatType.Int);

                            if (dex < dexReq || dex + dexBonus < 1)
                            {
                                drop = true;
                            }
                            else if (str < strReq || str + strBonus < 1)
                            {
                                drop = true;
                            }
                            else if (intel < intReq || intel + intBonus < 1)
                            {
                                drop = true;
                            }
                        }

                        if (drop)
                        {
                            var name = armor.Name ?? $"#{armor.LabelNumber}";

                            if (armor is BaseShield)
                            {
                                from.SendLocalizedMessage(1062003, name); // You can no longer equip your ~1_SHIELD~
                            }
                            else
                            {
                                from.SendLocalizedMessage(1062002, name); // You can no longer wear your ~1_ARMOR~
                            }

                            from.AddToBackpack(armor);
                            moved = true;
                        }
                    }
                    else if (item is BaseClothing clothing)
                    {
                        var drop = false;

                        if (!clothing.AllowMaleWearer && !from.Female && from.AccessLevel < AccessLevel.GameMaster)
                        {
                            drop = true;
                        }
                        else if (!clothing.AllowFemaleWearer && from.Female && from.AccessLevel < AccessLevel.GameMaster)
                        {
                            drop = true;
                        }
                        else if (clothing.RequiredRace != null && clothing.RequiredRace != Race)
                        {
                            drop = true;
                        }
                        else
                        {
                            var strBonus = clothing.ComputeStatBonus(StatType.Str);
                            var strReq = clothing.ComputeStatReq(StatType.Str);

                            if (str < strReq || str + strBonus < 1)
                            {
                                drop = true;
                            }
                        }

                        if (drop)
                        {
                            // You can no longer wear your ~1_ARMOR~
                            from.SendLocalizedMessage(1062002, clothing.Name ?? $"#{clothing.LabelNumber}");

                            from.AddToBackpack(clothing);
                            moved = true;
                        }
                    }
                }

                if (moved)
                {
                    from.SendLocalizedMessage(500647); // Some equipment has been moved to your backpack.
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                m_NoDeltaRecursion = false;
            }
        }

        public override void Delta(MobileDelta flag)
        {
            base.Delta(flag);

            if ((flag & MobileDelta.Stat) != 0)
            {
                ValidateEquipment();
            }
        }

        private static void OnLogout(Mobile m)
        {
            (m as PlayerMobile)?.AutoStablePets();
        }

        private static void EventSink_Connected(Mobile m)
        {
            if (m is PlayerMobile pm)
            {
                pm.SessionStart = Core.Now;

                pm.BedrollLogout = false;
                pm.LastOnline = Core.Now;
            }

            DisguisePersistence.StartTimer(m);

            Timer.StartTimer(() => SpecialMove.ClearAllMoves(m));
        }

        private static void EventSink_Disconnected(Mobile from)
        {
            var context = DesignContext.Find(from);

            if (context != null)
            {
                /* Client disconnected
                 *  - Remove design context
                 *  - Eject all from house
                 *  - Restore relocated entities
                 */

                // Remove design context
                DesignContext.Remove(from);

                // Eject all from house
                from.RevealingAction();

                foreach (var item in context.Foundation.GetItems())
                {
                    item.Location = context.Foundation.BanLocation;
                }

                foreach (var mobile in context.Foundation.GetMobiles())
                {
                    mobile.Location = context.Foundation.BanLocation;
                }

                // Restore relocated entities
                context.Foundation.RestoreRelocatedEntities();
            }

            if (from is PlayerMobile pm)
            {
                pm.m_GameTime += Core.Now - pm.SessionStart;

                pm.SpeechLog = null;
                pm.ClearQuestArrow();
                pm.LastOnline = Core.Now;
            }

            DisguisePersistence.StopTimer(from);
        }

        public override void RevealingAction()
        {
            if (DesignContext != null)
            {
                return;
            }

            InvisibilitySpell.StopTimer(this);

            base.RevealingAction();

            IsStealthing = false; // IsStealthing should be moved to Server.Mobiles
        }

        public override void OnHiddenChanged()
        {
            base.OnHiddenChanged();

            RemoveBuff(
                BuffIcon
                    .Invisibility
            ); // Always remove, default to the hiding icon EXCEPT in the invis spell where it's explicitly set

            if (!Hidden)
            {
                RemoveBuff(BuffIcon.HidingAndOrStealth);
            }
            else // if (!InvisibilitySpell.HasTimer( this ))
            {
                BuffInfo.AddBuff(
                    this,
                    new BuffInfo(BuffIcon.HidingAndOrStealth, 1075655)
                ); // Hidden/Stealthing & You Are Hidden
            }
        }

        public override void OnSubItemAdded(Item item)
        {
            if (AccessLevel < AccessLevel.GameMaster && item.IsChildOf(Backpack))
            {
                var maxWeight = WeightOverloading.GetMaxWeight(this);
                var curWeight = BodyWeight + TotalWeight;

                if (curWeight > maxWeight)
                {
                    SendLocalizedMessage(1019035, true, $" : {curWeight} / {maxWeight}");
                }
            }

            base.OnSubItemAdded(item);
        }

        public override bool CanBeHarmful(Mobile target, bool message, bool ignoreOurBlessedness)
        {
            if (DesignContext != null || target is PlayerMobile mobile && mobile.DesignContext != null)
            {
                return false;
            }

            if (target is BaseCreature creature && creature.IsInvulnerable || target is PlayerVendor or TownCrier)
            {
                if (message)
                {
                    if (target.Title == null)
                    {
                        SendMessage($"{target.Name} cannot be harmed.");
                    }
                    else
                    {
                        SendMessage($"{target.Name} {target.Title} cannot be harmed.");
                    }
                }

                return false;
            }

            return base.CanBeHarmful(target, message, ignoreOurBlessedness);
        }

        public override bool CanBeBeneficial(Mobile target, bool message, bool allowDead)
        {
            if (DesignContext != null || target is PlayerMobile mobile && mobile.DesignContext != null)
            {
                return false;
            }

            return base.CanBeBeneficial(target, message, allowDead);
        }

        public override bool CheckContextMenuDisplay(IEntity target) => DesignContext == null;

        public override void OnItemAdded(Item item)
        {
            base.OnItemAdded(item);

            if (item is BaseArmor or BaseWeapon)
            {
                CheckStatTimers();
            }

            if (NetState != null)
            {
                CheckLightLevels(false);
            }
        }

        public override void OnItemRemoved(Item item)
        {
            base.OnItemRemoved(item);

            if (item is BaseArmor or BaseWeapon)
            {
                CheckStatTimers();
            }

            if (NetState != null)
            {
                CheckLightLevels(false);
            }
        }

        private void AddArmorRating(ref double rating, Item armor)
        {
            if (armor is BaseArmor ar && (!Core.AOS || ar.ArmorAttributes.MageArmor == 0))
            {
                rating += ar.ArmorRatingScaled;
            }
        }

        public override bool Move(Direction d)
        {
            var ns = NetState;

            if (ns != null)
            {
                if (HasGump<ResurrectGump>())
                {
                    if (Alive)
                    {
                        CloseGump<ResurrectGump>();
                    }
                    else
                    {
                        SendLocalizedMessage(500111); // You are frozen and cannot move.
                        return false;
                    }
                }
            }

            var speed = ComputeMovementSpeed(d);

            if (!Alive)
            {
                MovementImpl.IgnoreMovableImpassables = true;
            }

            var res = base.Move(d);

            MovementImpl.IgnoreMovableImpassables = false;

            if (!res)
            {
                return false;
            }

            m_NextMovementTime += speed;

            return true;
        }

        public override bool CheckMovement(Direction d, out int newZ)
        {
            var context = DesignContext;

            if (context == null)
            {
                return base.CheckMovement(d, out newZ);
            }

            var foundation = context.Foundation;

            newZ = foundation.Z + HouseFoundation.GetLevelZ(context.Level, context.Foundation);

            int newX = X, newY = Y;
            Movement.Movement.Offset(d, ref newX, ref newY);

            var startX = foundation.X + foundation.Components.Min.X + 1;
            var startY = foundation.Y + foundation.Components.Min.Y + 1;
            var endX = startX + foundation.Components.Width - 1;
            var endY = startY + foundation.Components.Height - 2;

            return newX >= startX && newY >= startY && newX < endX && newY < endY && Map == foundation.Map;
        }

        public override bool AllowItemUse(Item item)
        {
            return DesignContext.Check(this);
        }

        public override bool AllowSkillUse(SkillName skill)
        {
            if (AnimalForm.UnderTransformation(this))
            {
                for (var i = 0; i < AnimalFormRestrictedSkills.Length; i++)
                {
                    if (AnimalFormRestrictedSkills[i] == skill)
                    {
                        SendLocalizedMessage(1070771); // You cannot use that skill in this form.
                        return false;
                    }
                }
            }

            return DesignContext.Check(this);
        }

        public virtual void RecheckTownProtection()
        {
            m_NextProtectionCheck = 10;

            var reg = Region.GetRegion<GuardedRegion>();
            var isProtected = reg?.IsDisabled() == false;

            if (isProtected != m_LastProtectedMessage)
            {
                if (isProtected)
                {
                    SendLocalizedMessage(500112); // You are now under the protection of the town guards.
                }
                else
                {
                    SendLocalizedMessage(500113); // You have left the protection of the town guards.
                }

                m_LastProtectedMessage = isProtected;
            }
        }

        public override void MoveToWorld(Point3D loc, Map map)
        {
            base.MoveToWorld(loc, map);

            RecheckTownProtection();
        }

        public override void SetLocation(Point3D loc, bool isTeleport)
        {
            if (!isTeleport && AccessLevel == AccessLevel.Player)
            {
                // moving, not teleporting
                var zDrop = Location.Z - loc.Z;

                if (zDrop > 20)                  // we fell more than one story
                {
                    Hits -= zDrop / 20 * 10 - 5; // deal some damage; does not kill, disrupt, etc
                }
            }

            base.SetLocation(loc, isTeleport);

            if (isTeleport || --m_NextProtectionCheck == 0)
            {
                RecheckTownProtection();
            }
        }

        public override void GetContextMenuEntries(Mobile from, List<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, list);

            if (from == this)
            {
                var house = BaseHouse.FindHouseAt(this);

                if (house != null)
                {
                    if (Alive && house.InternalizedVendors.Count > 0 && house.IsOwner(this))
                    {
                        list.Add(new CallbackEntry(6204, GetVendor));
                    }
                }

                if (Core.HS)
                {
                    var ns = from.NetState;

                    if (ns?.ExtendedStatus == true)
                    {
                        list.Add(
                            new CallbackEntry(
                                RefuseTrades ? 1154112 : 1154113,
                                ToggleTrades
                            )
                        ); // Allow Trades / Refuse Trades
                    }
                }
            }
            else
            {
                if (Core.TOL && from.InRange(this, 2))
                {
                    list.Add(new CallbackEntry(1077728, () => OpenTrade(from))); // Trade
                }

                if (Alive && Core.Expansion >= Expansion.AOS)
                {
                    var theirParty = from.Party as Party;
                    var ourParty = Party as Party;

                    if (theirParty == null && ourParty == null)
                    {
                        list.Add(new AddToPartyEntry(from, this));
                    }
                    else if (theirParty != null && theirParty.Leader == from)
                    {
                        if (ourParty == null)
                        {
                            list.Add(new AddToPartyEntry(from, this));
                        }
                        else if (ourParty == theirParty)
                        {
                            list.Add(new RemoveFromPartyEntry(from, this));
                        }
                    }
                }

                var curhouse = BaseHouse.FindHouseAt(this);

                if (curhouse != null && Alive && Core.Expansion >= Expansion.AOS && curhouse.IsAosRules &&
                    curhouse.IsFriend(from))
                {
                    list.Add(new EjectPlayerEntry(from, this));
                }
            }
        }

        private void ToggleTrades()
        {
            RefuseTrades = !RefuseTrades;
        }

        private void GetVendor()
        {
            var house = BaseHouse.FindHouseAt(this);

            if (CheckAlive() && house?.IsOwner(this) == true && house.InternalizedVendors.Count > 0)
            {
                CloseGump<ReclaimVendorGump>();
                SendGump(new ReclaimVendorGump(house));
            }
        }

        private void LeaveHouse()
        {
            var house = BaseHouse.FindHouseAt(this);

            if (house != null)
            {
                Location = house.BanLocation;
            }
        }

        public override void DisruptiveAction()
        {
            if (Meditating)
            {
                RemoveBuff(BuffIcon.ActiveMeditation);
            }

            base.DisruptiveAction();
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (this == from && !Warmode)
            {
                var mount = Mount;

                if (mount != null && !DesignContext.Check(this))
                {
                    return;
                }
            }

            base.OnDoubleClick(from);
        }

        public override void DisplayPaperdollTo(Mobile to)
        {
            if (DesignContext.Check(this))
            {
                base.DisplayPaperdollTo(to);
            }
        }

        public override bool CheckEquip(Item item)
        {
            if (!base.CheckEquip(item))
            {
                return false;
            }

            if (AccessLevel < AccessLevel.GameMaster && item.Layer != Layer.Mount && HasTrade)
            {
                var bounce = item.GetBounce();

                if (bounce != null)
                {
                    if (bounce.Parent is Item parent)
                    {
                        if (parent == Backpack || parent.IsChildOf(Backpack))
                        {
                            return true;
                        }
                    }
                    else if (bounce.Parent == this)
                    {
                        return true;
                    }
                }

                SendLocalizedMessage(
                    1004042
                ); // You can only equip what you are already carrying while you have a trade pending.
                return false;
            }

            return true;
        }

        public override bool CheckTrade(
            Mobile to, Item item, SecureTradeContainer cont, bool message, bool checkItems,
            int plusItems, int plusWeight
        )
        {
            var msgNum = 0;

            if (cont == null)
            {
                if (to.Holding != null)
                {
                    msgNum = 1062727; // You cannot trade with someone who is dragging something.
                }
                else if (HasTrade)
                {
                    msgNum = 1062781; // You are already trading with someone else!
                }
                else if (to.HasTrade)
                {
                    msgNum = 1062779; // That person is already involved in a trade
                }
                else if (to is PlayerMobile mobile && mobile.RefuseTrades)
                {
                    msgNum = 1154111; // ~1_NAME~ is refusing all trades.
                }
            }

            if (msgNum == 0 && item != null)
            {
                if (cont != null)
                {
                    plusItems += cont.TotalItems;
                    plusWeight += cont.TotalWeight;
                }

                if (Backpack?.CheckHold(this, item, false, checkItems, plusItems, plusWeight) != true)
                {
                    msgNum = 1004040; // You would not be able to hold this if the trade failed.
                }
                else if (to.Backpack?.CheckHold(to, item, false, checkItems, plusItems, plusWeight) != true)
                {
                    msgNum = 1004039; // The recipient of this trade would not be able to carry this.
                }
                else
                {
                    msgNum = CheckContentForTrade(item);
                }
            }

            if (msgNum != 0)
            {
                if (message)
                {
                    if (msgNum == 1154111)
                    {
                        SendLocalizedMessage(msgNum, to.Name);
                    }
                    else
                    {
                        SendLocalizedMessage(msgNum);
                    }
                }

                return false;
            }

            return true;
        }

        private static int CheckContentForTrade(Item item)
        {
            if (item is TrappableContainer container && container.TrapType != TrapType.None)
            {
                return 1004044; // You may not trade trapped items.
            }

            if (StolenItem.IsStolen(item))
            {
                return 1004043; // You may not trade recently stolen items.
            }

            if (item is Container)
            {
                foreach (var subItem in item.Items)
                {
                    var msg = CheckContentForTrade(subItem);

                    if (msg != 0)
                    {
                        return msg;
                    }
                }
            }

            return 0;
        }

        public override bool CheckNonlocalDrop(Mobile from, Item item, Item target)
        {
            if (!base.CheckNonlocalDrop(from, item, target))
            {
                return false;
            }

            if (from.AccessLevel >= AccessLevel.GameMaster)
            {
                return true;
            }

            var pack = Backpack;
            if (from == this && HasTrade && (target == pack || target.IsChildOf(pack)))
            {
                var bounce = item.GetBounce();

                if (bounce?.Parent is Item parent && (parent == pack || parent.IsChildOf(pack)))
                {
                    return true;
                }

                SendLocalizedMessage(1004041); // You can't do that while you have a trade pending.
                return false;
            }

            return true;
        }

        protected override void OnLocationChange(Point3D oldLocation)
        {
            CheckLightLevels(false);

            var context = DesignContext;

            if (context == null || m_NoRecursion)
            {
                return;
            }

            m_NoRecursion = true;

            var foundation = context.Foundation;

            int newX = X, newY = Y;
            var newZ = foundation.Z + HouseFoundation.GetLevelZ(context.Level, context.Foundation);

            var startX = foundation.X + foundation.Components.Min.X + 1;
            var startY = foundation.Y + foundation.Components.Min.Y + 1;
            var endX = startX + foundation.Components.Width - 1;
            var endY = startY + foundation.Components.Height - 2;

            if (newX >= startX && newY >= startY && newX < endX && newY < endY && Map == foundation.Map)
            {
                if (Z != newZ)
                {
                    Location = new Point3D(X, Y, newZ);
                }

                m_NoRecursion = false;
                return;
            }

            Location = new Point3D(foundation.X, foundation.Y, newZ);
            Map = foundation.Map;

            m_NoRecursion = false;
        }

        public override bool OnMoveOver(Mobile m) =>
            m is BaseCreature creature && !creature.Controlled
                ? !Alive || !creature.Alive || IsDeadBondedPet || creature.IsDeadBondedPet ||
                  Hidden && AccessLevel > AccessLevel.Player
                : base.OnMoveOver(m);

        public override bool CheckShove(Mobile shoved) =>
            IgnoreMobiles || shoved.IgnoreMobiles || TransformationSpellHelper.UnderTransformation(shoved, typeof(WraithFormSpell)) ||
            base.CheckShove(shoved);

        protected override void OnMapChange(Map oldMap)
        {
            var context = DesignContext;

            if (context == null || m_NoRecursion)
            {
                return;
            }

            m_NoRecursion = true;

            var foundation = context.Foundation;

            if (Map != foundation.Map)
            {
                Map = foundation.Map;
            }

            m_NoRecursion = false;
        }

        public override void OnDamage(int amount, Mobile from, bool willKill)
        {
            int disruptThreshold;

            if (!Core.AOS)
            {
                disruptThreshold = 0;
            }
            else if (from?.Player == true)
            {
                disruptThreshold = 18;
            }
            else
            {
                disruptThreshold = 25;
            }

            if (amount > disruptThreshold)
            {
                var c = BandageContext.GetContext(this);

                c?.Slip();
            }

            Confidence.StopRegenerating(this);

            WeightOverloading.FatigueOnDamage(this, amount);

            if (willKill && from is PlayerMobile mobile)
            {
                Timer.StartTimer(TimeSpan.FromSeconds(10), mobile.RecoverAmmo);
            }

            base.OnDamage(amount, from, willKill);
        }

        public override void Resurrect()
        {
            var wasAlive = Alive;

            base.Resurrect();

            if (Alive && !wasAlive)
            {
                Item deathRobe = new DeathRobe();

                if (!EquipItem(deathRobe))
                {
                    deathRobe.Delete();
                }
            }
        }

        public override void OnWarmodeChanged()
        {
            if (!Warmode)
            {
                Timer.StartTimer(TimeSpan.FromSeconds(10), RecoverAmmo);
            }
        }

        private bool FindItems_Callback(Item item) =>
            !item.Deleted && (item.LootType == LootType.Blessed) &&
            Backpack != item.Parent;

        public override bool OnBeforeDeath()
        {
            var state = NetState;

            state?.CancelAllTrades();

            DropHolding();

            RecoverAmmo();

            return base.OnBeforeDeath();
        }

        public override DeathMoveResult GetParentMoveResultFor(Item item)
        {
            // It seems all items are unmarked on death, even blessed
            if (item.QuestItem)
            {
                item.QuestItem = false;
            }

            return base.GetParentMoveResultFor(item);
        }

        public override DeathMoveResult GetInventoryMoveResultFor(Item item)
        {
            // It seems all items are unmarked on death, even blessed
            if (item.QuestItem)
            {
                item.QuestItem = false;
            }

            return base.GetInventoryMoveResultFor(item);
        }

        public override void OnDeath(Container c)
        {
            base.OnDeath(c);

            EquipSnapshot = null;

            HueMod = -1;
            NameMod = null;
            SavagePaintExpiration = TimeSpan.Zero;

            SetHairMods(-1, -1);

            PolymorphSpell.StopTimer(this);
            IncognitoSpell.StopTimer(this);
            DisguisePersistence.RemoveTimer(this);

            EndAction<PolymorphSpell>();
            EndAction<IncognitoSpell>();

            if (Flying)
            {
                Flying = false;
                BuffInfo.RemoveBuff(this, BuffIcon.Fly);
            }

            StolenItem.ReturnOnDeath(this, c);

            if (PermaFlags.Count > 0)
            {
                PermaFlags.Clear();

                if (c is Corpse corpse)
                {
                    corpse.Criminal = true;
                }

                if (Stealing.ClassicMode)
                {
                    Criminal = true;
                }
            }

            var killer = FindMostRecentDamager(true);

            if (killer is BaseCreature bcKiller)
            {
                var master = bcKiller.GetMaster();
                if (master != null)
                {
                    killer = master;
                }
            }

            Guilds.Guild.HandleDeath(this, killer);

            if (m_BuffTable != null)
            {
                using var queue = PooledRefQueue<BuffInfo>.Create();

                foreach (var buff in m_BuffTable.Values)
                {
                    if (!buff.RetainThroughDeath)
                    {
                        queue.Enqueue(buff);
                    }
                }

                while (queue.Count > 0)
                {
                    RemoveBuff(queue.Dequeue());
                }
            }
        }

        public override bool MutateSpeech(List<Mobile> hears, ref string text, ref object context)
        {
            if (Alive)
            {
                return false;
            }

            if (Core.ML && Skills.SpiritSpeak.Value >= 100.0)
            {
                return false;
            }

            if (Core.AOS)
            {
                for (var i = 0; i < hears.Count; ++i)
                {
                    var m = hears[i];

                    if (m != this && m.Skills.SpiritSpeak.Value >= 100.0)
                    {
                        return false;
                    }
                }
            }

            return base.MutateSpeech(hears, ref text, ref context);
        }

        public override void DoSpeech(string text, int[] keywords, MessageType type, int hue)
        {
            if (Guilds.Guild.NewGuildSystem && type is MessageType.Guild or MessageType.Alliance)
            {
                if (Guild is not Guild g)
                {
                    SendLocalizedMessage(1063142); // You are not in a guild!
                }
                else if (type == MessageType.Alliance)
                {
                    if (g.Alliance?.IsMember(g) == true)
                    {
                        // g.Alliance.AllianceTextMessage( hue, "[Alliance][{0}]: {1}", this.Name, text );
                        g.Alliance.AllianceChat(this, text);
                        SendToStaffMessage(this, $"[Alliance]: {text}");

                        AllianceMessageHue = hue;
                    }
                    else
                    {
                        SendLocalizedMessage(1071020); // You are not in an alliance!
                    }
                }
                else // Type == MessageType.Guild
                {
                    GuildMessageHue = hue;

                    g.GuildChat(this, text);
                    SendToStaffMessage(this, $"[Guild]: {text}");
                }
            }
            else
            {
                base.DoSpeech(text, keywords, type, hue);
            }
        }

        private static void SendToStaffMessage(Mobile from, string text)
        {
            Span<byte> buffer = stackalloc byte[OutgoingMessagePackets.GetMaxMessageLength(text)].InitializePacket();

            foreach (var ns in from.GetClientsInRange(8))
            {
                var mob = ns.Mobile;

                if (mob?.AccessLevel >= AccessLevel.GameMaster && mob.AccessLevel > from.AccessLevel)
                {
                    var length = OutgoingMessagePackets.CreateMessage(
                        buffer,
                        from.Serial,
                        from.Body,
                        MessageType.Regular,
                        from.SpeechHue,
                        3,
                        false,
                        from.Language,
                        from.Name,
                        text
                    );

                    if (length != buffer.Length)
                    {
                        buffer = buffer[..length]; // Adjust to the actual size
                    }

                    ns.Send(buffer);
                }
            }
        }

        public override void Damage(int amount, Mobile from = null, bool informMount = true)
        {
            var damageBonus = 1.0;

            if (EvilOmenSpell.EndEffect(this) && !PainSpikeSpell.UnderEffect(this))
            {
                damageBonus += 0.25;
            }

            var hasBloodOath = false;

            if (from != null)
            {
                if (Talisman is BaseTalisman talisman &&
                    talisman.Protection?.Type?.IsInstanceOfType(from) == true)
                {
                    damageBonus -= talisman.Protection.Amount / 100.0;
                }

                // Is the attacker attacking the blood oath caster?
                if (BloodOathSpell.GetBloodOath(from) == this)
                {
                    hasBloodOath = true;
                    damageBonus += 0.2;
                }
            }

            base.Damage((int)(amount * damageBonus), from, informMount);

            // If the blood oath caster will die then damage is not reflected back to the attacker
            if (hasBloodOath && Alive && !Deleted && !IsDeadBondedPet)
            {
                // In some expansions resisting spells reduces reflect dmg from monster blood oath
                var resistReflectedDamage = !from.Player && Core.ML && !Core.HS
                    ? (from.Skills.MagicResist.Value * 0.5 + 10) / 100
                    : 0;

                // Reflect damage to the attacker
                from.Damage((int)(amount * (1.0 - resistReflectedDamage)), this);
            }
        }

        public override bool IsHarmfulCriminal(Mobile target)
        {
            if (Stealing.ClassicMode && target is PlayerMobile mobile && mobile.PermaFlags.Count > 0)
            {
                if (Notoriety.Compute(this, mobile) == Notoriety.Innocent)
                {
                    mobile.Delta(MobileDelta.Noto);
                }

                return false;
            }

            var bc = target as BaseCreature;

            if (bc?.InitialInnocent == true && !bc.Controlled)
            {
                return false;
            }

            if (Core.ML && bc?.Controlled == true && this == bc.ControlMaster)
            {
                return false;
            }

            return base.IsHarmfulCriminal(target);
        }

        private void RevertHair()
        {
            SetHairMods(-1, -1);
        }

        public override void Deserialize(IGenericReader reader)
        {
            base.Deserialize(reader);
            var version = reader.ReadInt();

            switch (version)
            {
                case 29:
                    {
                        if (reader.ReadBool())
                        {
                            m_StuckMenuUses = new DateTime[reader.ReadInt()];

                            for (var i = 0; i < m_StuckMenuUses.Length; ++i)
                            {
                                m_StuckMenuUses[i] = reader.ReadDateTime();
                            }
                        }
                        else
                        {
                            m_StuckMenuUses = null;
                        }

                        goto case 28;
                    }
                case 28:
                    {
                        PeacedUntil = reader.ReadDateTime();

                        goto case 27;
                    }
                case 27:
                    {
                        AnkhNextUse = reader.ReadDateTime();

                        goto case 26;
                    }
                case 26:
                    {
                        AutoStabled = reader.ReadEntityList<Mobile>();

                        goto case 25;
                    }
                case 25:
                    {
                        var recipeCount = reader.ReadInt();

                        if (recipeCount > 0)
                        {
                            m_AcquiredRecipes = new Dictionary<int, bool>();

                            for (var i = 0; i < recipeCount; i++)
                            {
                                var r = reader.ReadInt();
                                if (reader.ReadBool()) // Don't add in recipes which we haven't gotten or have been removed
                                {
                                    m_AcquiredRecipes.Add(r, true);
                                }
                            }
                        }

                        goto case 24;
                    }
                case 24:
                case 23:
                case 22:
                case 21:
                case 20:
                    {
                        AllianceMessageHue = reader.ReadEncodedInt();
                        GuildMessageHue = reader.ReadEncodedInt();

                        goto case 19;
                    }
                case 19:
                    {
                        var rank = reader.ReadEncodedInt();
                        var maxRank = RankDefinition.Ranks.Length - 1;
                        if (rank > maxRank)
                        {
                            rank = maxRank;
                        }

                        m_GuildRank = RankDefinition.Ranks[rank];
                        LastOnline = reader.ReadDateTime();
                        goto case 18;
                    }
                case 18:
                case 17:
                case 16:
                    {
                        Profession = reader.ReadEncodedInt();
                        goto case 15;
                    }
                case 15:
                case 14:
                case 13:
                case 12:
                case 11:
                case 10:
                    {
                        if (reader.ReadBool())
                        {
                            m_HairModID = reader.ReadInt();
                            m_HairModHue = reader.ReadInt();
                            m_BeardModID = reader.ReadInt();
                            m_BeardModHue = reader.ReadInt();
                        }

                        goto case 9;
                    }
                case 9:
                    {
                        SavagePaintExpiration = reader.ReadTimeSpan();

                        if (SavagePaintExpiration > TimeSpan.Zero)
                        {
                            BodyMod = Female ? 184 : 183;
                            HueMod = 0;
                        }

                        goto case 8;
                    }
                case 8:
                    {
                        NpcGuild = (NpcGuild)reader.ReadInt();
                        NpcGuildJoinTime = reader.ReadDateTime();
                        NpcGuildGameTime = reader.ReadTimeSpan();
                        goto case 7;
                    }
                case 7:
                    {
                        PermaFlags = reader.ReadEntityList<Mobile>();
                        goto case 6;
                    }
                case 6:
                case 5:
                case 4:
                case 3:
                case 2:
                    {
                        Flags = (PlayerFlag)reader.ReadInt();
                        goto case 1;
                    }
                case 1:
                    {
                        m_LongTermElapse = reader.ReadTimeSpan();
                        m_ShortTermElapse = reader.ReadTimeSpan();
                        m_GameTime = reader.ReadTimeSpan();
                        goto case 0;
                    }
                case 0:
                    {
                        if (version < 26)
                        {
                            AutoStabled = new List<Mobile>();
                        }

                        break;
                    }
            }

            RecentlyReported ??= new List<Mobile>();

            if (!CharacterCreation.VerifyProfession(Profession))
            {
                Profession = 0;
            }

            PermaFlags ??= new List<Mobile>();

            // Default to member if going from older version to new version (only time it should be null)
            m_GuildRank ??= RankDefinition.Member;

            if (LastOnline == DateTime.MinValue && Account != null)
            {
                LastOnline = ((Account)Account).LastLogin;
            }

            if (AccessLevel > AccessLevel.Player)
            {
                IgnoreMobiles = true;
            }

            var list = Stabled;

            for (var i = 0; i < list.Count; ++i)
            {
                if (list[i] is BaseCreature bc)
                {
                    bc.IsStabled = true;
                    bc.StabledBy = this;
                }
            }

            CheckKillDecay();

            if (Hidden) // Hiding is the only buff where it has an effect that's serialized.
            {
                AddBuff(new BuffInfo(BuffIcon.HidingAndOrStealth, 1075655));
            }
        }

        public override void Serialize(IGenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write(29); // version

            if (m_StuckMenuUses != null)
            {
                writer.Write(true);

                writer.Write(m_StuckMenuUses.Length);

                for (var i = 0; i < m_StuckMenuUses.Length; ++i)
                {
                    writer.Write(m_StuckMenuUses[i]);
                }
            }
            else
            {
                writer.Write(false);
            }

            writer.Write(PeacedUntil);
            writer.Write(AnkhNextUse);
            AutoStabled.Tidy();
            writer.Write(AutoStabled);

            if (m_AcquiredRecipes == null)
            {
                writer.Write(0);
            }
            else
            {
                writer.Write(m_AcquiredRecipes.Count);

                foreach (var kvp in m_AcquiredRecipes)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value);
                }
            }

            writer.WriteEncodedInt(AllianceMessageHue);
            writer.WriteEncodedInt(GuildMessageHue);

            writer.WriteEncodedInt(m_GuildRank.Rank);
            writer.Write(LastOnline);

            writer.WriteEncodedInt(Profession);

            var useMods = m_HairModID != -1 || m_BeardModID != -1;

            writer.Write(useMods);

            if (useMods)
            {
                writer.Write(m_HairModID);
                writer.Write(m_HairModHue);
                writer.Write(m_BeardModID);
                writer.Write(m_BeardModHue);
            }

            writer.Write(SavagePaintExpiration);

            writer.Write((int)NpcGuild);
            writer.Write(NpcGuildJoinTime);
            writer.Write(NpcGuildGameTime);

            PermaFlags.Tidy();
            writer.Write(PermaFlags);

            writer.Write((int)Flags);

            writer.Write(m_LongTermElapse);
            writer.Write(m_ShortTermElapse);
            writer.Write(GameTime);
        }

        // Do we need to run an after serialize?
        public override bool ShouldExecuteAfterSerialize => ShouldKillDecay();

        public override void AfterSerialize()
        {
            base.AfterSerialize();

            CheckKillDecay();
        }

        public bool ShouldKillDecay() => m_ShortTermElapse < GameTime || m_LongTermElapse < GameTime;

        public void CheckKillDecay()
        {
            if (m_ShortTermElapse < GameTime)
            {
                m_ShortTermElapse += TimeSpan.FromHours(8);
                if (ShortTermMurders > 0)
                {
                    --ShortTermMurders;
                }
            }

            if (m_LongTermElapse < GameTime)
            {
                m_LongTermElapse += TimeSpan.FromHours(40);
                if (Kills > 0)
                {
                    --Kills;
                }
            }
        }

        public void ResetKillTime()
        {
            m_ShortTermElapse = GameTime + TimeSpan.FromHours(8);
            m_LongTermElapse = GameTime + TimeSpan.FromHours(40);
        }

        public override bool CanSee(Mobile m)
        {
            if (m is PlayerMobile mobile && mobile.VisibilityList.Contains(this))
            {
                return true;
            }

            return base.CanSee(m);
        }

        public virtual void CheckedAnimate(int action, int frameCount, int repeatCount, bool forward, bool repeat, int delay)
        {
            if (!Mounted)
            {
                Animate(action, frameCount, repeatCount, forward, repeat, delay);
            }
        }

        public override bool CanSee(Item item) =>
            DesignContext?.Foundation.IsHiddenToCustomizer(item) != true && base.CanSee(item);

        public override void OnAfterDelete()
        {
            base.OnAfterDelete();

            BaseHouse.HandleDeletion(this);

            DisguisePersistence.RemoveTimer(this);
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            if (Core.ML)
            {
                for (var i = AllFollowers.Count - 1; i >= 0; i--)
                {
                    if (AllFollowers[i] is BaseCreature c && c.ControlOrder == OrderType.Guard)
                    {
                        list.Add(501129); // guarded
                        break;
                    }
                }
            }
        }

        protected override bool OnMove(Direction d)
        {
            if (!Core.SE)
            {
                return base.OnMove(d);
            }

            if (AccessLevel != AccessLevel.Player)
            {
                return true;
            }

            if (Hidden && DesignContext.Find(this) == null) // Hidden & NOT customizing a house
            {
                if (!Mounted && Skills.Stealth.Value >= 25.0)
                {
                    var running = (d & Direction.Running) != 0;

                    if (running)
                    {
                        if ((AllowedStealthSteps -= 2) <= 0)
                        {
                            RevealingAction();
                        }
                    }
                    else if (AllowedStealthSteps-- <= 0)
                    {
                        Stealth.OnUse(this);
                    }
                }
                else
                {
                    RevealingAction();
                }
            }

            return true;
        }

        public void AutoStablePets()
        {
            if (Core.SE && AllFollowers.Count > 0)
            {
                for (var i = m_AllFollowers.Count - 1; i >= 0; --i)
                {
                    if (AllFollowers[i] is not BaseCreature pet || pet.ControlMaster == null)
                    {
                        continue;
                    }

                    if (pet.Summoned)
                    {
                        if (pet.Map != Map)
                        {
                            pet.PlaySound(pet.GetAngerSound());
                            Timer.StartTimer(pet.Delete);
                        }

                        continue;
                    }

                    if ((pet as IMount)?.Rider != null)
                    {
                        continue;
                    }

                    if (pet is PackLlama or PackHorse or Beetle && pet.Backpack?.Items.Count > 0)
                    {
                        continue;
                    }

                    pet.ControlTarget = null;
                    pet.ControlOrder = OrderType.Stay;
                    pet.Internalize();

                    pet.SetControlMaster(null);
                    pet.SummonMaster = null;

                    pet.IsStabled = true;
                    pet.StabledBy = this;

                    pet.Loyalty = BaseCreature.MaxLoyalty; // Wonderfully happy

                    Stabled.Add(pet);
                    AutoStabled.Add(pet);
                }
            }
        }

        public void ClaimAutoStabledPets()
        {
            if (!Core.SE || AutoStabled.Count <= 0)
            {
                return;
            }

            if (!Alive)
            {
                SendLocalizedMessage(
                    1076251
                ); // Your pet was unable to join you while you are a ghost.  Please re-login once you have ressurected to claim your pets.
                return;
            }

            for (var i = AutoStabled.Count - 1; i >= 0; --i)
            {
                if (AutoStabled[i] is not BaseCreature pet)
                {
                    continue;
                }

                if (pet.Deleted)
                {
                    pet.IsStabled = false;
                    pet.StabledBy = null;

                    if (Stabled.Contains(pet))
                    {
                        Stabled.Remove(pet);
                    }

                    continue;
                }

                if (Followers + pet.ControlSlots <= FollowersMax)
                {
                    pet.SetControlMaster(this);

                    if (pet.Summoned)
                    {
                        pet.SummonMaster = this;
                    }

                    pet.ControlTarget = this;
                    pet.ControlOrder = OrderType.Follow;

                    pet.MoveToWorld(Location, Map);

                    pet.IsStabled = false;
                    pet.StabledBy = null;

                    pet.Loyalty = BaseCreature.MaxLoyalty; // Wonderfully Happy

                    if (Stabled.Contains(pet))
                    {
                        Stabled.Remove(pet);
                    }
                }
                else
                {
                    SendLocalizedMessage(
                        1049612,
                        pet.Name
                    ); // ~1_NAME~ remained in the stables because you have too many followers.
                }
            }

            AutoStabled.Clear();
        }

        public void RecoverAmmo()
        {
            if (!Core.SE || !Alive)
            {
                return;
            }

            foreach (var kvp in RecoverableAmmo)
            {
                if (kvp.Value > 0)
                {
                    Item ammo = null;

                    try
                    {
                        ammo = kvp.Key.CreateInstance<Item>();
                    }
                    catch
                    {
                        // ignored
                    }

                    if (ammo == null)
                    {
                        continue;
                    }

                    ammo.Amount = kvp.Value;

                    var name = ammo.Name ?? ammo switch
                    {
                        Arrow _ => $"arrow{(ammo.Amount != 1 ? "s" : "")}",
                        Bolt _  => $"bolt{(ammo.Amount != 1 ? "s" : "")}",
                        _       => $"#{ammo.LabelNumber}"
                    };

                    PlaceInBackpack(ammo);
                    SendLocalizedMessage(1073504, $"{ammo.Amount}\t{name}"); // You recover ~1_NUM~ ~2_AMMO~.
                }
            }

            RecoverableAmmo.Clear();
        }

        public bool CanUseStuckMenu()
        {
            if (m_StuckMenuUses == null)
            {
                return true;
            }

            for (var i = 0; i < m_StuckMenuUses.Length; ++i)
            {
                if (Core.Now - m_StuckMenuUses[i] > TimeSpan.FromDays(1.0))
                {
                    return true;
                }
            }

            return false;
        }

        public void UsedStuckMenu()
        {
            if (m_StuckMenuUses == null)
            {
                m_StuckMenuUses = new DateTime[2];
            }

            for (var i = 0; i < m_StuckMenuUses.Length; ++i)
            {
                if (Core.Now - m_StuckMenuUses[i] > TimeSpan.FromDays(1.0))
                {
                    m_StuckMenuUses[i] = Core.Now;
                    return;
                }
            }
        }

        public override ApplyPoisonResult ApplyPoison(Mobile from, Poison poison)
        {
            if (!Alive)
            {
                return ApplyPoisonResult.Immune;
            }

            if (EvilOmenSpell.EndEffect(this))
            {
                poison = PoisonImpl.IncreaseLevel(poison);
            }

            var result = base.ApplyPoison(from, poison);

            if (from != null && result == ApplyPoisonResult.Poisoned && PoisonTimer is PoisonImpl.PoisonTimer timer)
            {
                timer.From = from;
            }

            return result;
        }

        public override void OnGenderChanged(bool oldFemale)
        {
        }

        public override void OnGuildChange(BaseGuild oldGuild)
        {
        }

        public override void OnGuildTitleChange(string oldTitle)
        {
        }

        public override void OnKarmaChange(int oldValue)
        {
        }

        public override void OnFameChange(int oldValue)
        {
        }

        public override void OnAccessLevelChanged(AccessLevel oldLevel)
        {
            IgnoreMobiles = AccessLevel != AccessLevel.Player;
        }

        public override void OnRawStatChange(StatType stat, int oldValue)
        {
        }

        public override int ComputeMovementSpeed(Direction dir, bool checkTurning = true)
        {
            if (checkTurning && (dir & Direction.Mask) != (Direction & Direction.Mask))
            {
                return CalcMoves.RunMountDelay; // We are NOT actually moving (just a direction change)
            }

            var context = TransformationSpellHelper.GetContext(this);

            if (context?.Type == typeof(ReaperFormSpell))
            {
                return CalcMoves.WalkFootDelay;
            }

            var running = (dir & Direction.Running) != 0;

            var onHorse = Mount != null;

            if (onHorse || AnimalForm.GetContext(this)?.SpeedBoost == true)
            {
                return running ? CalcMoves.RunMountDelay : CalcMoves.WalkMountDelay;
            }

            return running ? CalcMoves.RunFootDelay : CalcMoves.WalkFootDelay;
        }

        private void DeltaEnemies(Type oldType, Type newType)
        {
            foreach (var m in GetMobilesInRange(18))
            {
                var t = m.GetType();

                if (t == oldType || t == newType)
                {
                    m.NetState.SendMobileMoving(this, m);
                }
            }
        }

        public void SetHairMods(int hairID, int beardID)
        {
            if (hairID == -1)
            {
                InternalRestoreHair(true, ref m_HairModID, ref m_HairModHue);
            }
            else if (hairID != -2)
            {
                InternalChangeHair(true, hairID, ref m_HairModID, ref m_HairModHue);
            }

            if (beardID == -1)
            {
                InternalRestoreHair(false, ref m_BeardModID, ref m_BeardModHue);
            }
            else if (beardID != -2)
            {
                InternalChangeHair(false, beardID, ref m_BeardModID, ref m_BeardModHue);
            }
        }

        private void CreateHair(bool hair, int id, int hue)
        {
            if (hair)
            {
                // TODO Verification?
                HairItemID = id;
                HairHue = hue;
            }
            else
            {
                FacialHairItemID = id;
                FacialHairHue = hue;
            }
        }

        private void InternalRestoreHair(bool hair, ref int id, ref int hue)
        {
            if (id == -1)
            {
                return;
            }

            if (hair)
            {
                HairItemID = 0;
            }
            else
            {
                FacialHairItemID = 0;
            }

            // if (id != 0)
            CreateHair(hair, id, hue);

            id = -1;
            hue = 0;
        }

        private void InternalChangeHair(bool hair, int id, ref int storeID, ref int storeHue)
        {
            if (storeID == -1)
            {
                storeID = hair ? HairItemID : FacialHairItemID;
                storeHue = hair ? HairHue : FacialHairHue;
            }

            CreateHair(hair, id, 0);
        }

        public override TimeSpan GetLogoutDelay()
        {
            if (BedrollLogout)
            {
                return TimeSpan.Zero;
            }

            return base.GetLogoutDelay();
        }

        public override void OnSpeech(SpeechEventArgs e)
        {
            if (SpeechLog.Enabled && NetState != null)
            {
                if (SpeechLog == null)
                {
                    SpeechLog = new SpeechLog();
                }

                SpeechLog.Add(e.Mobile, e.Speech);
            }
        }

        public virtual bool HasRecipe(Recipe r) => r != null && HasRecipe(r.ID);

        public virtual bool HasRecipe(int recipeID) =>
            m_AcquiredRecipes != null && m_AcquiredRecipes.TryGetValue(recipeID, out var value) && value;

        public virtual void AcquireRecipe(Recipe r)
        {
            if (r != null)
            {
                AcquireRecipe(r.ID);
            }
        }

        public virtual void AcquireRecipe(int recipeID)
        {
            m_AcquiredRecipes ??= new Dictionary<int, bool>();
            m_AcquiredRecipes[recipeID] = true;
        }

        public virtual void ResetRecipes()
        {
            m_AcquiredRecipes = null;
        }

        public void ResendBuffs()
        {
            if (BuffInfo.Enabled && m_BuffTable != null && NetState?.BuffIcon == true)
            {
                foreach (var info in m_BuffTable.Values)
                {
                    info.SendAddBuffPacket(NetState, Serial);
                }
            }
        }

        public void AddBuff(BuffInfo b)
        {
            if (!BuffInfo.Enabled || b == null)
            {
                return;
            }

            RemoveBuff(b); // Check & subsequently remove the old one.

            m_BuffTable ??= new Dictionary<BuffIcon, BuffInfo>();

            m_BuffTable.Add(b.ID, b);

            if (NetState?.BuffIcon == true)
            {
                b.SendAddBuffPacket(NetState, Serial);
            }
        }

        public void RemoveBuff(BuffInfo b)
        {
            if (b == null)
            {
                return;
            }

            RemoveBuff(b.ID);
        }

        public void RemoveBuff(BuffIcon b)
        {
            if (m_BuffTable?.Remove(b) != true)
            {
                return;
            }

            if (NetState?.BuffIcon == true)
            {
                BuffInfo.SendRemoveBuffPacket(NetState, Serial, b);
            }

            if (m_BuffTable.Count <= 0)
            {
                m_BuffTable = null;
            }
        }

        private class MountBlock
        {
            private TimerExecutionToken _timerToken;
            private BlockMountType _type;

            public MountBlock(TimeSpan duration, BlockMountType type, Mobile mobile)
            {
                _type = type;

                if (duration < TimeSpan.MaxValue)
                {
                    Timer.StartTimer(duration, () => RemoveBlock(mobile), out _timerToken);
                }
            }

            public DateTime Expiration => _timerToken.Next;

            public BlockMountType MountBlockReason => CheckBlock() ? _type : BlockMountType.None;

            public bool CheckBlock() => _timerToken.Next == DateTime.MinValue || _timerToken.Running;

            public void RemoveBlock(Mobile mobile)
            {
                if (mobile is PlayerMobile pm)
                {
                    pm._mountBlock = null;
                }

                _timerToken.Cancel();
            }
        }

        private delegate void ContextCallback();

        private class CallbackEntry : ContextMenuEntry
        {
            private readonly ContextCallback m_Callback;

            public CallbackEntry(int number, ContextCallback callback) : this(number, -1, callback)
            {
            }

            public CallbackEntry(int number, int range, ContextCallback callback) : base(number, range) =>
                m_Callback = callback;

            public override void OnClick()
            {
                m_Callback?.Invoke();
            }
        }
    }
}
