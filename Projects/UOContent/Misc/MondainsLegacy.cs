using System;
using Server.Items;
using Server.Mobiles;
using Server.Utilities;

namespace Server
{
    public static class MondainsLegacy
    {
        public static bool CheckML(Mobile from, bool message = true)
        {
            if (from?.NetState == null)
            {
                return false;
            }

            if (from.NetState.SupportsExpansion(Expansion.ML))
            {
                return true;
            }

            if (message)
            {
                from.SendLocalizedMessage(1072791); // You must upgrade to Mondain's Legacy in order to use that item.
            }

            return false;
        }
    }
}
