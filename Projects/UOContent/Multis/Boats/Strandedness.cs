namespace Server.Misc
{
    public static class Strandedness
    {
        private static readonly Point2D[] m_Gaia =
        {
            new(2528, 3568), new(2376, 3400), new(2528, 3896),
            new(2168, 3904), new(1136, 3416), new(1432, 3648),
            new(1416, 4000), new(4512, 3936), new(4440, 3120),
            new(4192, 3672), new(4720, 3472), new(3744, 2768),
            new(3480, 2432), new(3560, 2136), new(3792, 2112),
            new(2800, 2296), new(2736, 2016), new(4576, 1456),
            new(4680, 1152), new(4304, 1104), new(4496, 984),
            new(4248, 696), new(4040, 616), new(3896, 248),
            new(4176, 384), new(3672, 1104), new(3520, 1152),
            new(3720, 1360), new(2184, 2152), new(1952, 2088),
            new(2056, 1936), new(1720, 1992), new(472, 2064),
            new(656, 2096), new(3008, 3592), new(2784, 3472),
            new(5456, 2400), new(5976, 2424), new(5328, 3112),
            new(5792, 3152), new(2120, 3616), new(2136, 3128),
            new(1632, 3528), new(1328, 3160), new(1072, 3136),
            new(1128, 2976), new(960, 2576), new(752, 1832),
            new(184, 1488), new(592, 1440), new(368, 1216),
            new(232, 752), new(696, 744), new(304, 1000),
            new(840, 376), new(1192, 624), new(1200, 192),
            new(1512, 240), new(1336, 456), new(1536, 648),
            new(1104, 952), new(1864, 264), new(2136, 200),
            new(2160, 528), new(1904, 512), new(2240, 784),
            new(2536, 776), new(2488, 216), new(2336, 72),
            new(2648, 288), new(2680, 576), new(2896, 88),
            new(2840, 344), new(3136, 72), new(2968, 520),
            new(3192, 328), new(3448, 208), new(3432, 608),
            new(3184, 752), new(2800, 704), new(2768, 1016),
            new(2448, 1232), new(2272, 920), new(2072, 1080),
            new(2048, 1264), new(1808, 1528), new(1496, 1880),
            new(1656, 2168), new(2096, 2320), new(1816, 2528),
            new(1840, 2640), new(1928, 2952), new(2120, 2712)
        };

        public static void Initialize()
        {
            EventSink.Login += EventSink_Login;
        }

        private static bool IsStranded(Mobile from)
        {
            var map = from.Map;

            if (map == null)
            {
                return false;
            }

            var surface = map.GetTopSurface(from.Location);

            if (surface is LandTile tile)
            {
                var id = tile.ID;

                return id >= 168 && id <= 171
                       || id >= 310 && id <= 311;
            }

            if (surface is StaticTile staticTile)
            {
                var id = staticTile.ID;

                return id >= 0x1796 && id <= 0x17B2;
            }

            return false;
        }

        public static void EventSink_Login(Mobile from)
        {
            if (!IsStranded(from))
            {
                return;
            }

            var map = from.Map;

            Point2D[] list;

            if (map == Map.Gaia)
            {
                list = m_Gaia;
            }
            else
            {
                return;
            }

            var p = Point2D.Zero;
            var pdist = double.MaxValue;

            for (var i = 0; i < list.Length; ++i)
            {
                var dist = from.GetDistanceToSqrt(list[i]);

                if (dist < pdist)
                {
                    p = list[i];
                    pdist = dist;
                }
            }

            int x = p.X, y = p.Y;
            int z;
            bool canFit;

            z = map.GetAverageZ(x, y);
            canFit = map.CanSpawnMobile(x, y, z);

            for (var i = 1; !canFit && i <= 40; i += 2)
            {
                for (var xo = -1; !canFit && xo <= 1; ++xo)
                {
                    for (var yo = -1; !canFit && yo <= 1; ++yo)
                    {
                        if (xo == 0 && yo == 0)
                        {
                            continue;
                        }

                        x = p.X + xo * i;
                        y = p.Y + yo * i;
                        z = map.GetAverageZ(x, y);
                        canFit = map.CanSpawnMobile(x, y, z);
                    }
                }
            }

            if (canFit)
            {
                from.Location = new Point3D(x, y, z);
            }
        }
    }
}
