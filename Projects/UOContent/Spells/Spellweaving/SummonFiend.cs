using System;
using Server.Mobiles;

namespace Server.Spells.Spellweaving
{
    public class SummonFiendSpell : ArcaneSummon<ArcaneFiend>
    {
        private static readonly SpellInfo _info = new(
            "Summon Fiend",
            "Nylisstra",
            -1
        );

        public SummonFiendSpell(Mobile caster, Item scroll = null) : base(caster, scroll, _info)
        {
        }

        public override TimeSpan CastDelayBase => TimeSpan.FromSeconds(2.0);

        public override double RequiredSkill => 38.0;
        public override int RequiredMana => 10;

        public override int Sound => 0x216;
    }
}
