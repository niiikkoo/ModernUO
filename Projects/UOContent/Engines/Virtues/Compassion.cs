using System;
using System.Runtime.CompilerServices;
using Server.Mobiles;

namespace Server
{
    public static class CompassionVirtue
    {
        private const int LossAmount = 500;
        private static readonly TimeSpan LossDelay = TimeSpan.FromDays(7.0);

        public static void Initialize()
        {
            VirtueGump.Register(105, OnVirtueUsed);
        }

        public static void OnVirtueUsed(Mobile from)
        {
            from.SendLocalizedMessage(1053001); // This virtue is not activated through the virtue menu.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanAtrophy(VirtueInfo info) => info?.LastCompassionLoss + LossDelay < Core.Now;

        public static void CheckAtrophy(PlayerMobile pm)
        {
            var virtues = pm.Virtues;
            if (CanAtrophy(virtues))
            {
                if (VirtueSystem.Atrophy(pm, VirtueName.Compassion, LossAmount))
                {
                    pm.SendLocalizedMessage(1114420); // You have lost some Compassion.
                }

                virtues.LastCompassionLoss = Core.Now;
            }
        }
    }
}
