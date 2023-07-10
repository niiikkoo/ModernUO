using ModernUO.Serialization;

namespace Server.Items;

[Furniture]
[SerializationGenerator(0)]
public partial class CommodityDeedBox : BaseContainer
{
    [Constructible]
    public CommodityDeedBox() : base(0x9AA)
    {
        Hue = 0x47;
        Weight = 4.0;
    }

    public override int LabelNumber => 1080523; // Commodity Deed Box
    public override int DefaultGumpID => 0x43;

    public static CommodityDeedBox Find(Item deed)
    {
        var parent = deed;

        while (parent != null && parent is not CommodityDeedBox)
        {
            parent = parent.Parent as Item;
        }

        return parent as CommodityDeedBox;
    }
}
