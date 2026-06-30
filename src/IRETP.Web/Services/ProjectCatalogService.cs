namespace IRETP.Web.Services;

/// <summary>
/// Demo catalogue for the /projects database page (Phase 1 deliverable #18).
/// Fixture data modelled on DLD project registrations. Production swaps this
/// for the /api/map/projects + /api/open-data/projects endpoints.
/// </summary>
public class ProjectCatalogService
{
    public sealed record Project(
        Guid Id,
        string Name,
        string NameAr,
        string Developer,
        string Zone,
        string Status,              // Completed | UnderConstruction | Announced | Stalled
        decimal CompletionPercent,
        int TotalUnits,
        int SoldUnits,
        DateTime ExpectedDelivery,
        string PropertyType,
        double Latitude,
        double Longitude,
        string LicenseNumber);

    private readonly List<Project> _projects;

    public ProjectCatalogService()
    {
        _projects =
        [
            new(Guid.NewGuid(), "Emaar Beachfront",        "إعمار بيتشفرونت",     "Emaar Properties",     "Dubai Harbour",       "UnderConstruction", 62m,  3200, 1920, new DateTime(2027,  6, 30), "Apartment", 25.0905, 55.1458, "DLD-RERA-2461"),
            new(Guid.NewGuid(), "Creek Horizon",           "كريك هورايزن",         "Emaar Properties",     "Dubai Creek Harbour", "UnderConstruction", 78m,  1800, 1500, new DateTime(2026, 12, 15), "Apartment", 25.1971, 55.3555, "DLD-RERA-1890"),
            new(Guid.NewGuid(), "Burj Vista",              "برج فيستا",            "Emaar Properties",     "Downtown Dubai",      "Completed",        100m,  1210, 1210, new DateTime(2023,  9,  1), "Apartment", 25.1946, 55.2788, "DLD-RERA-0932"),
            new(Guid.NewGuid(), "Damac Lagoons",           "لاجون داماك",          "DAMAC",                "Dubailand",           "UnderConstruction", 54m,  4500, 3200, new DateTime(2027,  3, 31), "Townhouse",25.0561, 55.2876, "DLD-RERA-2705"),
            new(Guid.NewGuid(), "Palm Jebel Ali Villas",   "فلل نخلة جبل علي",     "Nakheel",              "Palm Jebel Ali",      "Announced",          5m,   2000,   60, new DateTime(2029,  6, 30), "Villa",    24.9772, 54.9920, "DLD-RERA-3020"),
            new(Guid.NewGuid(), "Sobha Hartland Greens",   "سوبها هارتلاند غرينز", "Sobha Realty",         "Mohammed Bin Rashid City","UnderConstruction", 41m, 960,  620, new DateTime(2027,  9, 30), "Apartment", 25.1715, 55.2950, "DLD-RERA-2588"),
            new(Guid.NewGuid(), "Meraas Boulevard Heights","ميراس بوليفارد هايتس", "Meraas",               "Downtown Dubai",      "Completed",        100m,   540,  540, new DateTime(2022,  5, 20), "Apartment", 25.1975, 55.2696, "DLD-RERA-0775"),
            new(Guid.NewGuid(), "Azizi Riviera",           "عزيزي ريفييرا",        "Azizi Developments",   "Meydan One",          "UnderConstruction", 72m,  4000, 3400, new DateTime(2026, 10, 31), "Apartment", 25.1512, 55.3022, "DLD-RERA-1703"),
            new(Guid.NewGuid(), "Binghatti Canal",         "بنغاتي كانال",         "Binghatti",            "Business Bay",        "UnderConstruction", 35m,   520,  430, new DateTime(2027, 12, 31), "Apartment", 25.1858, 55.2600, "DLD-RERA-2810"),
            new(Guid.NewGuid(), "Ellington Belgravia Heights","إلينغتون بلغرافيا","Ellington Properties",  "Jumeirah Village Circle","Completed",      100m,   310,  310, new DateTime(2023, 11, 10), "Apartment", 25.0516, 55.2097, "DLD-RERA-1120"),
            new(Guid.NewGuid(), "Omniyat Orla",            "أومنيات أورلا",        "Omniyat",              "Palm Jumeirah",       "UnderConstruction", 48m,    80,   74, new DateTime(2026,  9, 30), "Villa",    25.1107, 55.1382, "DLD-RERA-2450"),
            new(Guid.NewGuid(), "Deyaar Mayan",            "ديار مايان",           "Deyaar",               "Al Barari",           "Stalled",           22m,   230,  90, new DateTime(2026,  6, 30), "Apartment", 25.1094, 55.2972, "DLD-RERA-1855"),
        ];
    }

    public IReadOnlyList<Project> All() => _projects;

    public IEnumerable<Project> Search(
        string? zone = null, string? status = null, string? propertyType = null, string? developer = null, string? term = null)
    {
        IEnumerable<Project> q = _projects;
        if (!string.IsNullOrWhiteSpace(zone))         q = q.Where(p => p.Zone == zone);
        if (!string.IsNullOrWhiteSpace(status))       q = q.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(propertyType)) q = q.Where(p => p.PropertyType == propertyType);
        if (!string.IsNullOrWhiteSpace(developer))    q = q.Where(p => p.Developer == developer);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            q = q.Where(p =>
                p.Name.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                p.Developer.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                p.Zone.Contains(t, StringComparison.OrdinalIgnoreCase));
        }
        return q;
    }

    public Project? Find(Guid id) => _projects.FirstOrDefault(p => p.Id == id);

    public IReadOnlyList<string> Zones()        => _projects.Select(p => p.Zone).Distinct().OrderBy(z => z).ToList();
    public IReadOnlyList<string> Developers()   => _projects.Select(p => p.Developer).Distinct().OrderBy(d => d).ToList();
    public IReadOnlyList<string> Statuses      => new[] { "Completed", "UnderConstruction", "Announced", "Stalled" };
    public IReadOnlyList<string> PropertyTypes => new[] { "Apartment", "Villa", "Townhouse", "Office", "Retail", "Land" };
}
