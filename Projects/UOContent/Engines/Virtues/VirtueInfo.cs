using System;
using ModernUO.Serialization;

namespace Server;

[PropertyObject]
[SerializationGenerator(0)]
public partial class VirtueInfo
{
    [DeltaDateTime]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private DateTime _lastSacrificeGain;

    [SerializableFieldSaveFlag(0)]
    private bool ShouldSerializeLastSacrificeGain() => !SacrificeVirtue.CanGain(this);

    [SerializableFieldDefault(0)]
    private static DateTime LastSacrificeGainDefaultValue() => DateTime.MinValue;

    [DeltaDateTime]
    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private DateTime _lastSacrificeLoss;

    [SerializableFieldSaveFlag(1)]
    private bool ShouldSerializeLastSacrificeLoss() => !SacrificeVirtue.CanAtrophy(this);

    [SerializableFieldDefault(1)]
    private static DateTime LastSacrificeLossDefaultValue() => DateTime.MinValue;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _availableResurrects;

    [SerializableFieldSaveFlag(2)]
    private bool ShouldSerializeAvailableResurrects() => _availableResurrects > 0;

    [DeltaDateTime]
    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private DateTime _lastJusticeLoss;

    [SerializableFieldSaveFlag(3)]
    private bool ShouldSerializeLastJusticeLoss() => !JusticeVirtue.CanAtrophy(this);

    [SerializableFieldDefault(3)]
    private static DateTime LastJusticeLossDefaultValue() => DateTime.MinValue;

    [DeltaDateTime]
    [SerializableField(4)]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private DateTime _lastCompassionLoss;

    [SerializableFieldSaveFlag(4)]
    private bool ShouldSerializeLastCompassionLoss() => !CompassionVirtue.CanAtrophy(this);

    [SerializableFieldDefault(4)]
    private static DateTime LastSacrificeCompassionLossDefaultValue() => DateTime.MinValue;

    [DeltaDateTime]
    [SerializableField(5)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _nextCompassionDay;

    [SerializableFieldSaveFlag(5)]
    private bool ShouldSerializeNestCompassionDay() => _compassionGains > 0;

    [SerializableFieldDefault(5)]
    private static DateTime NextCompassionDayDefaultValue() => DateTime.MinValue;

    [SerializableField(6)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _compassionGains;

    [SerializableFieldSaveFlag(6)]
    private bool ShouldSerializeCompassionGains() => _compassionGains > 0;

    [DeltaDateTime]
    [SerializableField(7)]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private DateTime _lastValorLoss;

    [SerializableFieldSaveFlag(7)]
    private bool ShouldSerializeValorLoss() => !ValorVirtue.CanAtrophy(this);

    [SerializableFieldDefault(7)]
    private static DateTime LastValorLossDefaultValue() => DateTime.MinValue;

    [DeltaDateTime]
    [SerializableField(8)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _lastHonorUse;

    [SerializableFieldSaveFlag(8)]
    private bool ShouldSerializeLastHonorUse() => !HonorVirtue.CanUse(this);

    [SerializableFieldDefault(8)]
    private static DateTime LastHonorUseDefaultValue() => DateTime.MinValue;

    [SerializableField(9)]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private bool _honorActive;

    [SerializableFieldSaveFlag(9)]
    private bool ShouldSerializeHonorActive() => _honorActive;

    [SerializableField(10, setter: "private")]
    private int[] _values;

    [SerializableFieldSaveFlag(10)]
    private bool ShouldSerializeValues()
    {
        if (_values == null)
        {
            return false;
        }

        for (var i = 0; i < _values.Length; i++)
        {
            if (_values[i] > 0)
            {
                return true;
            }
        }

        return false;
    }

    [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
    public int Humility
    {
        get => GetValue(0);
        set => SetValue(0, value);
    }

    [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
    public int Sacrifice
    {
        get => GetValue(1);
        set => SetValue(1, value);
    }

    [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
    public int Compassion
    {
        get => GetValue(2);
        set => SetValue(2, value);
    }

    [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
    public int Spirituality
    {
        get => GetValue(3);
        set => SetValue(3, value);
    }

    [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
    public int Valor
    {
        get => GetValue(4);
        set => SetValue(4, value);
    }

    [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
    public int Honor
    {
        get => GetValue(5);
        set => SetValue(5, value);
    }

    [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
    public int Justice
    {
        get => GetValue(6);
        set => SetValue(6, value);
    }

    [CommandProperty(AccessLevel.Counselor, AccessLevel.GameMaster)]
    public int Honesty
    {
        get => GetValue(7);
        set => SetValue(7, value);
    }

    public int GetValue(int index) => _values?[index] ?? 0;

    public void SetValue(int index, int value)
    {
        _values ??= new int[8];
        _values[index] = value;
    }

    public override string ToString() => "...";

    // Used to invalidate and delete the VirtueContext, usually during world load
    public bool IsUnused()
    {
        if (Values == null)
        {
            return true;
        }

        if (AvailableResurrects > 0)
        {
            return false;
        }

        if (!SacrificeVirtue.CanGain(this))
        {
            return false;
        }

        return true;
    }
}
