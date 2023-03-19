using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Engines.PlayerMurderSystem;

[SerializationGenerator(0)]
public partial class MurderContext
{
    [SerializableField(0)]
    private TimeSpan _shortTermElapse = TimeSpan.MaxValue;

    [SerializableField(1)]
    private TimeSpan _longTermElapse = TimeSpan.MaxValue;

    public PlayerMobile Player { get; }

    // Wall clock time for next short or long term expiration
    internal DateTime _nextElapse;

    public MurderContext(PlayerMobile player) => Player = player;

    public void ResetKillTime(bool isShort = true, bool isLong = true)
    {
        var gameTime = Player.GameTime;

        if (isShort)
        {
            ShortTermElapse = gameTime + TimeSpan.FromHours(8);
        }

        if (isLong)
        {
            LongTermElapse = gameTime + TimeSpan.FromHours(40);
        }
    }

    public void DecayKills()
    {
        var gameTime = Player.GameTime;

        if (ShortTermElapse < gameTime)
        {
            ShortTermElapse += TimeSpan.FromHours(8);
            if (Player.ShortTermMurders > 0)
            {
                --Player.ShortTermMurders;
            }
        }

        if (LongTermElapse < gameTime)
        {
            LongTermElapse += TimeSpan.FromHours(40);
            if (Player.Kills > 0)
            {
                --Player.Kills;
            }
        }
    }

    public bool CheckStart()
    {
        if (Player.NetState == null)
        {
            return false;
        }

        _nextElapse = DateTime.MaxValue;

        var now = Core.Now;
        var gameTime = Player.GameTime;

        if (Player.ShortTermMurders > 0)
        {
            _nextElapse = now + (ShortTermElapse - gameTime);
        }

        if (Player.Kills > 0)
        {
            var timeUntilLong = now + (LongTermElapse - gameTime);
            if (_nextElapse > timeUntilLong)
            {
                _nextElapse = timeUntilLong;
            }
        }

        return _nextElapse != DateTime.MaxValue;
    }

    public class EqualityComparer : IEqualityComparer<MurderContext>
    {
        public static EqualityComparer Default { get; } = new ();

        public bool Equals(MurderContext x, MurderContext y) => x?.Player == y?.Player;

        public int GetHashCode(MurderContext context) => context.Player?.GetHashCode() ?? 0;
    }
}
