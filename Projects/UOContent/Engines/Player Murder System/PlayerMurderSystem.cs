using System;
using System.Collections.Generic;
using Server.Collections;
using Server.Mobiles;

namespace Server.Engines.PlayerMurderSystem;

public static class PlayerMurderSystem
{
    // All of the players with murders
    private static readonly Dictionary<PlayerMobile, MurderContext> _murderContexts = new();

    // Only the players that are online
    private static readonly HashSet<MurderContext> _contextTerms = new(MurderContext.EqualityComparer.Default);

    private static Timer _murdererTimer;

    public static void Configure()
    {
        GenericPersistence.Register("PlayerMurders", Serialize, Deserialize);
    }

    public static void Initialize()
    {
        EventSink.Disconnected += OnDisconnected;
        EventSink.Login += OnLogin;

        _murdererTimer = new MurdererTimer();
        _murdererTimer.Start();
    }

    // Used for migrations only.
    public static void ManuallyAdd(PlayerMobile player, TimeSpan shortTerm, TimeSpan longTerm)
    {
        if (GetContext(player, out var context))
        {
            context.ShortTermElapse = shortTerm;
            context.LongTermElapse = longTerm;
        }
    }

    private static void OnLogin(Mobile m)
    {
        if (m is PlayerMobile pm && GetContext(pm, out var context))
        {
            if (!context.CheckStart())
            {
                // This will probably never happen unless another system clears all of the kills but doesn't clean up.
                _murderContexts.Remove(pm);
            }
        }
    }

    private static void OnDisconnected(Mobile m)
    {
        if (m is PlayerMobile pm && GetContext(pm, out var context))
        {
            _contextTerms.Remove(context);
        }
    }

    private static void Deserialize(IGenericReader reader)
    {
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; ++i)
        {
            var context = new MurderContext(reader.ReadEntity<PlayerMobile>());
            context.Deserialize(reader);

            _murderContexts.Add(context.Player, context);
        }
    }

    private static void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(_murderContexts.Count);
        foreach (var (m, context) in _murderContexts)
        {
            writer.Write(m);
            context.Serialize(writer);
        }
    }

    private static bool GetContext(PlayerMobile player, out MurderContext context)
    {
        if (player.ShortTermMurders <= 0 && player.Kills <= 0)
        {
            _murderContexts.Remove(player);
            context = null;
            return false;
        }

        if (!_murderContexts.TryGetValue(player, out context))
        {
            context = _murderContexts[player] = new MurderContext(player);
        }

        return true;
    }

    public static void OnKillsChange(PlayerMobile player, bool resetKillTime = false)
    {
        if (GetContext(player, out var context))
        {
            // Either we are resetting their decay time, or they got their first kill
            context.ResetKillTime(
                player.ShortTermMurders > 0 && (!resetKillTime || context.ShortTermElapse == TimeSpan.MaxValue),
                player.Kills > 0 && (!resetKillTime || context.LongTermElapse == TimeSpan.MaxValue)
            );

            if (context.CheckStart())
            {
                _contextTerms.Add(context);
            }
            else
            {
                _contextTerms.Remove(context);
            }
        }
    }

    private class MurdererTimer : Timer
    {
        public MurdererTimer() : base(TimeSpan.FromMinutes(10.0), TimeSpan.FromMinutes(10.0))
        {
        }

        protected override void OnTick()
        {
            if (_contextTerms.Count == 0)
            {
                return;
            }

            using var queue = PooledRefQueue<Mobile>.Create();

            foreach (var context in _contextTerms)
            {
                context.DecayKills();
                if (!context.CheckStart())
                {
                    queue.Enqueue(context.Player);
                }
            }

            while (queue.Count > 0)
            {
                if (_murderContexts.Remove((PlayerMobile)queue.Dequeue(), out var context))
                {
                    _contextTerms.Remove(context);
                }
            }
        }
    }
}
