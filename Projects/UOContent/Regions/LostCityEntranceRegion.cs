﻿using System.Text.Json.Serialization;

namespace Server.Regions;

// TODO: Implement damage boost regions
public class LostCityEntranceRegion : MondainRegion
{
    [JsonConstructor] // Don't include parent, since it is special
    public LostCityEntranceRegion(string name, Map map, int priority, params Rectangle3D[] area) : base(name, map, priority, area)
    {
    }

    public LostCityEntranceRegion(string name, Map map, Region parent, params Rectangle3D[] area): base(name, map, parent, area)
    {
    }

    public LostCityEntranceRegion(string name, Map map, Region parent, int priority, params Rectangle3D[] area)
        : base(name, map, parent, priority, area)
    {
    }
}
