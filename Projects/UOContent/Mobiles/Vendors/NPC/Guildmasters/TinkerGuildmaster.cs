using ModernUO.Serialization;
using System.Collections.Generic;
using Server.ContextMenus;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class TinkerGuildmaster : BaseGuildmaster
    {
        [Constructible]
        public TinkerGuildmaster() : base("tinker")
        {
            SetSkill(SkillName.Lockpicking, 65.0, 88.0);
            SetSkill(SkillName.Tinkering, 90.0, 100.0);
            SetSkill(SkillName.RemoveTrap, 85.0, 100.0);
        }

        public override NpcGuild NpcGuild => NpcGuild.TinkersGuild;

        public override void AddCustomContextEntries(Mobile from, List<ContextMenuEntry> list)
        {
            if (Core.ML && from.Alive)
            {
                var entry = new RechargeEntry(from, this);

                entry.Enabled = false;

                list.Add(entry);
            }

            base.AddCustomContextEntries(from, list);
        }

        private class RechargeEntry : ContextMenuEntry
        {
            private readonly Mobile m_From;
            private readonly Mobile m_Vendor;

            public RechargeEntry(Mobile from, Mobile vendor) : base(6271, 6)
            {
                m_From = from;
                m_Vendor = vendor;
            }

            public override void OnClick()
            {
                if (!Core.ML || m_Vendor?.Deleted != false)
                {
                    return;
                }
            }
        }
    }
}
