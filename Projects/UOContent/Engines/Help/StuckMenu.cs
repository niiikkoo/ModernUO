using System;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Menus.Questions
{
    public class StuckMenuEntry
    {
        public StuckMenuEntry(int name, Point3D[] locations)
        {
            Name = name;
            Locations = locations;
        }

        public int Name { get; }

        public Point3D[] Locations { get; }
    }

    public class StuckMenu : Gump
    {
        private static readonly StuckMenuEntry[] m_Entries =
        {
            // Britain
            new(
                1011028,
                new[]
                {
                    new Point3D(1522, 1757, 28),
                    new Point3D(1519, 1619, 10),
                    new Point3D(1457, 1538, 30),
                    new Point3D(1607, 1568, 20),
                    new Point3D(1643, 1680, 18)
                }
            ),

            // Trinsic
            new(
                1011029,
                new[]
                {
                    new Point3D(2005, 2754, 30),
                    new Point3D(1993, 2827, 0),
                    new Point3D(2044, 2883, 0),
                    new Point3D(1876, 2859, 20),
                    new Point3D(1865, 2687, 0)
                }
            ),

            // Vesper
            new(
                1011030,
                new[]
                {
                    new Point3D(2973, 891, 0),
                    new Point3D(3003, 776, 0),
                    new Point3D(2910, 727, 0),
                    new Point3D(2865, 804, 0),
                    new Point3D(2832, 927, 0)
                }
            ),

            // Minoc
            new(
                1011031,
                new[]
                {
                    new Point3D(2498, 392, 0),
                    new Point3D(2433, 541, 0),
                    new Point3D(2445, 501, 15),
                    new Point3D(2501, 469, 15),
                    new Point3D(2444, 420, 15)
                }
            ),

            // Yew
            new(
                1011032,
                new[]
                {
                    new Point3D(490, 1166, 0),
                    new Point3D(652, 1098, 0),
                    new Point3D(650, 1013, 0),
                    new Point3D(536, 979, 0),
                    new Point3D(464, 970, 0)
                }
            ),

            // Cove
            new(
                1011033,
                new[]
                {
                    new Point3D(2230, 1159, 0),
                    new Point3D(2218, 1203, 0),
                    new Point3D(2247, 1194, 0),
                    new Point3D(2236, 1224, 0),
                    new Point3D(2273, 1231, 0)
                }
            )
        };

        private readonly bool m_MarkUse;

        private readonly Mobile m_Mobile;
        private readonly Mobile m_Sender;

        private Timer m_Timer;

        public StuckMenu(Mobile beholder, Mobile beheld, bool markUse) : base(150, 50)
        {
            m_Sender = beholder;
            m_Mobile = beheld;
            m_MarkUse = markUse;

            Closable = false;
            Draggable = false;
            Disposable = false;

            AddBackground(0, 0, 270, 320, 2600);

            AddHtmlLocalized(50, 20, 250, 35, 1011027); // Chose a town:

            for (var i = 0; i < m_Entries.Length; i++)
            {
                var entry = m_Entries[i];

                AddButton(50, 55 + 35 * i, 208, 209, i + 1);
                AddHtmlLocalized(75, 55 + 35 * i, 335, 40, entry.Name);
            }

            AddButton(55, 263, 4005, 4007, 0);
            AddHtmlLocalized(90, 265, 200, 35, 1011012); // CANCEL
        }

        public void BeginClose()
        {
            StopClose();

            m_Timer = new CloseTimer(m_Mobile);
            m_Timer.Start();

            m_Mobile.Frozen = true;
        }

        public void StopClose()
        {
            m_Timer?.Stop();

            m_Mobile.Frozen = false;
        }

        public override void OnResponse(NetState state, RelayInfo info)
        {
            StopClose();

            if (info.ButtonID == 0)
            {
                if (m_Mobile == m_Sender)
                {
                    m_Mobile.SendLocalizedMessage(1010588); // You choose not to go to any city.
                }
            }
            else
            {
                var index = info.ButtonID - 1;

                if (index >= 0 && index < m_Entries.Length)
                {
                    Teleport(m_Entries[index]);
                }
            }
        }

        private void Teleport(StuckMenuEntry entry)
        {
            if (m_MarkUse)
            {
                m_Mobile.SendLocalizedMessage(1010589); // You will be teleported within the next two minutes.

                new TeleportTimer(m_Mobile, entry, TimeSpan.FromSeconds(10.0 + Utility.RandomDouble() * 110.0)).Start();

                if (m_Mobile is PlayerMobile mobile)
                {
                    mobile.UsedStuckMenu();
                }
            }
            else
            {
                new TeleportTimer(m_Mobile, entry, TimeSpan.Zero).Start();
            }
        }

        private class CloseTimer : Timer
        {
            private readonly DateTime m_End;
            private readonly Mobile m_Mobile;

            public CloseTimer(Mobile m) : base(TimeSpan.Zero, TimeSpan.FromSeconds(1.0))
            {
                m_Mobile = m;
                m_End = Core.Now + TimeSpan.FromMinutes(3.0);
            }

            protected override void OnTick()
            {
                if (m_Mobile.NetState == null || Core.Now > m_End)
                {
                    m_Mobile.Frozen = false;
                    m_Mobile.CloseGump<StuckMenu>();

                    Stop();
                }
                else
                {
                    m_Mobile.Frozen = true;
                }
            }
        }

        private class TeleportTimer : Timer
        {
            private readonly StuckMenuEntry m_Destination;
            private readonly DateTime m_End;
            private readonly Mobile m_Mobile;

            public TeleportTimer(Mobile mobile, StuckMenuEntry destination, TimeSpan delay) : base(
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1.0)
            )
            {

                m_Mobile = mobile;
                m_Destination = destination;
                m_End = Core.Now + delay;
            }

            protected override void OnTick()
            {
                if (Core.Now < m_End)
                {
                    m_Mobile.Frozen = true;
                }
                else
                {
                    m_Mobile.Frozen = false;
                    Stop();

                    var dest = m_Destination.Locations.RandomElement();

                    BaseCreature.TeleportPets(m_Mobile, dest, Map.Gaia);
                    m_Mobile.MoveToWorld(dest, Map.Gaia);
                }
            }
        }
    }
}
