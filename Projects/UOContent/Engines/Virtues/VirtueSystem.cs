using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Server.Mobiles;

namespace Server;

public enum VirtueLevel
{
    None,
    Seeker,
    Follower,
    Knight
}

[Flags]
public enum VirtueName
{
    Humility,
    Sacrifice,
    Compassion,
    Spirituality,
    Valor,
    Honor,
    Justice,
    Honesty
}

public static class VirtueSystem
{
    private static readonly Dictionary<PlayerMobile, VirtueInfo> _contexts = new();

    public static void Configure()
    {
        GenericPersistence.Register("Virtues", Serialize, Deserialize);
    }

    private static void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version

        writer.WriteEncodedInt(_contexts.Count);
        foreach (var (pm, virtues) in _contexts)
        {
            writer.Write(pm);
            virtues.Serialize(writer);
        }
    }

    private static void Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt(); // version

        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var virtues = _contexts[reader.ReadEntity<PlayerMobile>()] = new VirtueInfo();
            virtues.Deserialize(reader);
        }
    }

    public static VirtueInfo GetVirtues(this PlayerMobile from) =>
        _contexts.TryGetValue(from, out var context) ? context : null;

    public static VirtueInfo GetOrCreateVirtues(this PlayerMobile from)
    {
        ref VirtueInfo info = ref CollectionsMarshal.GetValueRefOrAddDefault(_contexts, from, out bool exists);
        if (!exists)
        {
            info = new VirtueInfo();
        }

        return info;
    }

    public static bool HasAny(PlayerMobile from, VirtueName virtue) => from.Virtues.GetValue((int)virtue) > 0;

    public static bool IsHighestPath(PlayerMobile from, VirtueName virtue) =>
        from.Virtues.GetValue((int)virtue) >= GetMaxAmount(virtue);

    public static VirtueLevel GetLevel(Mobile from, VirtueName virtue)
    {
        var v = (from as PlayerMobile)?.Virtues.GetValue((int)virtue) ?? 0;
        int vl;

        if (v < 4000)
        {
            vl = 0;
        }
        else if (v >= GetMaxAmount(virtue))
        {
            vl = 3;
        }
        else
        {
            vl = (v + 9999) / 10000;
        }

        return (VirtueLevel)vl;
    }

    public static string GetName(this VirtueName virtue) =>
        virtue switch
        {
            VirtueName.Humility     => "Humility",
            VirtueName.Sacrifice    => "Sacrifice",
            VirtueName.Compassion   => "Compassion",
            VirtueName.Spirituality => "Spirituality",
            VirtueName.Valor        => "Valor",
            VirtueName.Honor        => "Honor",
            VirtueName.Justice      => "Justice",
            VirtueName.Honesty      => "Honesty",
            _                       => ""
        };

    public static int GetMaxAmount(VirtueName virtue) =>
        virtue switch
        {
            VirtueName.Honor => 20000,
            VirtueName.Sacrifice => 22000,
            _ => 21000
        };

    public static int GetGainedLocalizedMessage(VirtueName virtue) =>
        virtue switch
        {
            VirtueName.Sacrifice    => 1054160, // You have gained in sacrifice.
            VirtueName.Compassion   => 1053002, // You have gained in compassion.
            VirtueName.Spirituality => 1155832, // You have gained in Spirituality.
            VirtueName.Valor        => 1054030, // You have gained in Valor!
            VirtueName.Honor        => 1063225, // You have gained in Honor.
            VirtueName.Justice      => 1049363, // You have gained in Justice.
            VirtueName.Humility     => 1052070, // You have gained in Humility.
            _                       => 0
        };

    public static int GetGainedAPathLocalizedMessage(VirtueName virtue) =>
        virtue switch
        {
            VirtueName.Sacrifice    => 1052008, // You have gained a path in Sacrifice!
            VirtueName.Spirituality => 1155833, // You have gained a path in Spirituality!
            VirtueName.Valor        => 1054032, // You have gained a path in Valor!
            VirtueName.Honor        => 1063226, // You have gained a path in Honor!
            VirtueName.Justice      => 1049367, // You have gained a path in Justice!
            VirtueName.Humility     => 1155811, // You have gained a path in Humility!
            _                       => 0
        };

    public static int GetHightestPathLocalizedMessage(VirtueName virtue) =>
        virtue switch
        {
            VirtueName.Compassion => 1053003, // You have achieved the highest path of compassion and can no longer gain any further.
            VirtueName.Valor => 1054031, // You have achieved the highest path in Valor and can no longer gain any further.
            VirtueName.Honesty => 1153771, // You have achieved the highest path in Honesty and can no longer gain any further.
            _ => 0
        };

    public static bool Award(PlayerMobile from, VirtueName virtue, int amount, ref bool gainedPath)
    {
        var current = from.Virtues.GetValue((int)virtue);

        var maxAmount = GetMaxAmount(virtue);

        if (current >= maxAmount)
        {
            return false;
        }

        if (current + amount >= maxAmount)
        {
            amount = maxAmount - current;
        }

        var oldLevel = GetLevel(from, virtue);

        from.Virtues.SetValue((int)virtue, current + amount);

        gainedPath = GetLevel(from, virtue) != oldLevel;

        return true;
    }

    public static bool Atrophy(PlayerMobile from, VirtueName virtue) => Atrophy(from, virtue, 1);

    public static bool Atrophy(PlayerMobile from, VirtueName virtue, int amount)
    {
        var current = from.Virtues.GetValue((int)virtue);

        if (current - amount >= 0)
        {
            from.Virtues.SetValue((int)virtue, current - amount);
        }
        else
        {
            from.Virtues.SetValue((int)virtue, 0);
        }

        return current > 0;
    }

    public static bool IsSeeker(PlayerMobile from, VirtueName virtue) => GetLevel(from, virtue) >= VirtueLevel.Seeker;

    public static bool IsFollower(PlayerMobile from, VirtueName virtue) => GetLevel(from, virtue) >= VirtueLevel.Follower;

    public static bool IsKnight(PlayerMobile from, VirtueName virtue) => GetLevel(from, virtue) >= VirtueLevel.Knight;

    public static void AwardVirtue(PlayerMobile pm, VirtueName virtue, int amount)
    {
        var virtues = pm.GetOrCreateVirtues();
        if (virtue == VirtueName.Compassion)
        {
            if (virtues.CompassionGains > 0 && Core.Now > virtues.NextCompassionDay)
            {
                virtues.NextCompassionDay = DateTime.MinValue;
                virtues.CompassionGains = 0;
            }

            if (virtues.CompassionGains >= 5)
            {
                pm.SendLocalizedMessage(1053004); // You must wait about a day before you can gain in compassion again.
                return;
            }
        }

        var gainedPath = false;
        var virtueName = virtue.GetName();

        if (Award(pm, virtue, amount, ref gainedPath))
        {
            if (gainedPath)
            {
                var gainedPathMessage = GetGainedAPathLocalizedMessage(virtue);
                if (gainedPathMessage != 0)
                {
                    pm.SendLocalizedMessage(gainedPathMessage);
                }
                else
                {
                    pm.SendMessage($"You have gained a path in {virtueName}!");
                }
            }
            else
            {
                var gainMessage = GetGainedLocalizedMessage(virtue);
                if (gainMessage != 0)
                {
                    pm.SendLocalizedMessage(gainMessage);
                }
                else
                {
                    pm.SendMessage($"You have gained in {virtueName}.");
                }
            }

            if (virtue == VirtueName.Compassion)
            {
                virtues.NextCompassionDay = Core.Now + TimeSpan.FromDays(1.0);

                if (++virtues.CompassionGains >= 5)
                {
                    // You must wait about a day before you can gain in compassion again.
                    pm.SendLocalizedMessage(1053004);
                }
            }
        }
        else
        {
            var highestPathMessage = GetHightestPathLocalizedMessage(virtue);
            if (highestPathMessage != 0)
            {
                pm.SendLocalizedMessage(highestPathMessage);
            }
            else
            {
                pm.SendLocalizedMessage(1052050); // You have achieved the highest path in this Virtue.
            }
        }
    }
}
