using System;

namespace Server.Misc
{
    /// <summary>
    ///     This timer spouts some welcome messages to a user at a set interval. It is used on character creation and login.
    /// </summary>
    public class WelcomeTimer : Timer
    {
        private static string[] m_Messages;

        private readonly int m_Count;

        private readonly Mobile m_Mobile;
        private int m_State;

        public static void Initialize()
        {
            m_Messages =  new[]
                {
                    $"Welcome to {ServerList.ServerName}.",
                    "Please enjoy your stay."
                };
        }

        public WelcomeTimer(Mobile m) : this(m, m_Messages.Length)
        {
        }

        public WelcomeTimer(Mobile m, int count) : base(TimeSpan.FromSeconds(5.0), TimeSpan.FromSeconds(10.0))
        {
            m_Mobile = m;
            m_Count = count;
        }

        protected override void OnTick()
        {
            if (m_State < m_Count)
            {
                m_Mobile.SendMessage(0x35, m_Messages[m_State++]);
                return;
            }

            Stop();
        }
    }
}
