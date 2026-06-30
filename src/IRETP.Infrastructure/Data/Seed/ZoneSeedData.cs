using IRETP.Domain.Entities;

namespace IRETP.Infrastructure.Data.Seed;

public static class ZoneSeedData
{
    public static List<Zone> GetZones()
    {
        return
        [
            CreateZone("Dubai Marina", "دبي مارينا", "Dubai", "دبي", 25.0805, 55.1403),
            CreateZone("Downtown Dubai", "وسط مدينة دبي", "Dubai", "دبي", 25.1972, 55.2744),
            CreateZone("Palm Jumeirah", "نخلة جميرا", "Dubai", "دبي", 25.1124, 55.1390),
            CreateZone("Business Bay", "الخليج التجاري", "Dubai", "دبي", 25.1852, 55.2627),
            CreateZone("Jumeirah Village Circle", "قرية جميرا الدائرية", "Dubai", "دبي", 25.0650, 55.2094),
            CreateZone("Jumeirah Lake Towers", "أبراج بحيرات الجميرا", "Dubai", "دبي", 25.0770, 55.1480),
            CreateZone("Dubai Hills Estate", "تلال دبي", "Dubai", "دبي", 25.1334, 55.2389),
            CreateZone("Arabian Ranches", "المرابع العربية", "Dubai", "دبي", 25.0622, 55.2589),
            CreateZone("Jumeirah Beach Residence", "جميرا بيتش ريزيدنس", "Dubai", "دبي", 25.0774, 55.1344),
            CreateZone("Dubai Creek Harbour", "ميناء خور دبي", "Dubai", "دبي", 25.2050, 55.3394),
            CreateZone("Mohammed Bin Rashid City", "مدينة محمد بن راشد", "Dubai", "دبي", 25.1686, 55.3056),
            CreateZone("DAMAC Hills", "داماك هيلز", "Dubai", "دبي", 25.0311, 55.2428),
            CreateZone("Town Square", "تاون سكوير", "Dubai", "دبي", 25.0230, 55.2660),
            CreateZone("Dubai South", "دبي الجنوب", "Dubai", "دبي", 24.8960, 55.1616),
            CreateZone("Al Barsha", "البرشاء", "Dubai", "دبي", 25.1000, 55.2000),
            CreateZone("International City", "المدينة العالمية", "Dubai", "دبي", 25.1636, 55.4100),
            CreateZone("Dubai Silicon Oasis", "واحة دبي للسيليكون", "Dubai", "دبي", 25.1200, 55.3800),
            CreateZone("Motor City", "موتور سيتي", "Dubai", "دبي", 25.0469, 55.2356),
            CreateZone("Mirdif", "مردف", "Dubai", "دبي", 25.2268, 55.4175),
            CreateZone("Al Furjan", "الفرجان", "Dubai", "دبي", 25.0390, 55.1450),
            CreateZone("Deira", "ديرة", "Dubai", "دبي", 25.2744, 55.3050),
            CreateZone("Bur Dubai", "بر دبي", "Dubai", "دبي", 25.2532, 55.2946),
            CreateZone("Jumeirah", "جميرا", "Dubai", "دبي", 25.2067, 55.2467),
            CreateZone("Al Quoz", "القوز", "Dubai", "دبي", 25.1444, 55.2444),
            CreateZone("Discovery Gardens", "ديسكفري جاردنز", "Dubai", "دبي", 25.0378, 55.1370),
            CreateZone("Remraam", "رمرام", "Dubai", "دبي", 25.0400, 55.2700),
            CreateZone("Sobha Hartland", "صبحة هارتلاند", "Dubai", "دبي", 25.1750, 55.3100),
            CreateZone("Tilal Al Ghaf", "تلال الغاف", "Dubai", "دبي", 25.0750, 55.2600),
            CreateZone("Emaar Beachfront", "إعمار بيتشفرونت", "Dubai", "دبي", 25.0850, 55.1250),
            CreateZone("City Walk", "سيتي ووك", "Dubai", "دبي", 25.2093, 55.2590),
            CreateZone("Mudon", "مدن", "Dubai", "دبي", 25.0360, 55.2750),
            CreateZone("Arjan", "أرجان", "Dubai", "دبي", 25.0550, 55.2350),
            CreateZone("Al Jaddaf", "الجداف", "Dubai", "دبي", 25.2140, 55.3240),
            CreateZone("Dubai Investment Park", "مجمع دبي للاستثمار", "Dubai", "دبي", 25.0050, 55.1850),
            CreateZone("Sports City", "المدينة الرياضية", "Dubai", "دبي", 25.0400, 55.2250),
            CreateZone("Villanova", "فيلانوفا", "Dubai", "دبي", 25.0200, 55.2600),
            CreateZone("Wasl Gate", "بوابة وصل", "Dubai", "دبي", 25.1200, 55.2300),
            CreateZone("Creek Beach", "شاطئ الخور", "Dubai", "دبي", 25.2020, 55.3350),
            CreateZone("Port De La Mer", "بورت دو لا مير", "Dubai", "دبي", 25.2320, 55.2650),
            CreateZone("Bluewaters Island", "جزيرة بلووترز", "Dubai", "دبي", 25.0800, 55.1200),
        ];
    }

    private static Zone CreateZone(string name, string nameAr, string parentArea, string parentAreaAr, double lat, double lng)
    {
        return new Zone
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameAr = nameAr,
            ParentArea = parentArea,
            ParentAreaAr = parentAreaAr,
            CenterLat = lat,
            CenterLng = lng,
            CreatedAt = DateTime.UtcNow
        };
    }
}
