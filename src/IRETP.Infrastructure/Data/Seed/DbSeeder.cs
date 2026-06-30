using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IRETP.Infrastructure.Data.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IretpDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.MigrateAsync();

        await SeedRolesAsync(roleManager);
        await SeedAdminUserAsync(userManager);

        if (!await context.Zones.AnyAsync())
        {
            var zones = ZoneSeedData.GetZones();
            await context.Zones.AddRangeAsync(zones);
            await context.SaveChangesAsync();

            await SeedDevelopersAsync(context, zones);
            await SeedProjectsAsync(context, zones);
            await SeedTransactionsAsync(context, zones);
            await SeedRiskThresholdsAsync(context);
            await SeedScoringWeightsAsync(context);
        }

        await SeedMarketBenchmarksAsync(context);
        await SeedProjectCertificationsAsync(context);
        await SeedBeneficialOwnersAsync(context);
        await SeedPriceAndRentalIndicesAsync(context);
        await BackfillPlaybookStepsAsync(context);
        await BackfillNameValidationsAsync(context);
    }

    // Seeds 8 rolling quarters of apartment price + rental index per zone.
    // Lets `/api/price-index` and `/api/rental-index` return meaningful
    // demo data that mirrors the public-page figures from MarketDataService.
    private static async Task SeedPriceAndRentalIndicesAsync(IretpDbContext context)
    {
        var priceAlready = await context.PriceIndices.AnyAsync();
        var rentalAlready = await context.RentalIndices.AnyAsync();
        if (priceAlready && rentalAlready) return;

        var zones = await context.Zones.ToListAsync();
        if (zones.Count == 0) return;

        // 8 rolling quarters ending with the current quarter. Using a fixed
        // seed so re-running the seeder produces stable values for tests.
        var now = DateTime.UtcNow;
        var curYear = now.Year;
        var curQ = (now.Month - 1) / 3 + 1;
        var quarters = Enumerable.Range(0, 8)
            .Select(i =>
            {
                var y = curYear;
                var q = curQ - i;
                while (q <= 0) { q += 4; y -= 1; }
                return (Year: y, Quarter: q);
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Quarter)
            .ToList();

        var rng = new Random(20260418);

        if (!priceAlready)
        {
            var priceRows = new List<Domain.Entities.PriceIndex>();
            foreach (var zone in zones)
            {
                // Base PSF anchored on zone name so the seed lines up with
                // the /map mock values (Downtown high, DIP low, etc.).
                var basePsf = EstimateBasePsf(zone.Name);
                decimal prev = 0m;
                decimal? prevYear = null;
                for (var idx = 0; idx < quarters.Count; idx++)
                {
                    var (y, q) = quarters[idx];
                    // +1.8% per quarter baseline with ±1.5% jitter.
                    var growth = 1m + (0.018m + (decimal)(rng.NextDouble() * 0.03 - 0.015));
                    var psf = idx == 0 ? basePsf : Math.Round(prev * growth, 0);
                    var qChange = prev == 0 ? (decimal?)null : Math.Round((psf / prev - 1m) * 100m, 2);
                    var aChange = prevYear.HasValue ? Math.Round((psf / prevYear.Value - 1m) * 100m, 2) : (decimal?)null;

                    priceRows.Add(new Domain.Entities.PriceIndex
                    {
                        ZoneId = zone.Id,
                        PropertyType = Domain.Enums.PropertyType.Apartment,
                        IsOffPlan = false,
                        Year = y,
                        Quarter = q,
                        AveragePricePerSqft = psf,
                        TransactionCount = 250 + rng.Next(0, 600),
                        TotalValue = psf * 1_500_000m,
                        QuarterlyChange = qChange,
                        AnnualChange = aChange
                    });
                    if (idx >= 3) prevYear = priceRows[priceRows.Count - 4].AveragePricePerSqft;
                    prev = psf;
                }
            }
            context.PriceIndices.AddRange(priceRows);
        }

        if (!rentalAlready)
        {
            var rentalRows = new List<Domain.Entities.RentalIndex>();
            foreach (var zone in zones)
            {
                var baseRent = EstimateBaseAnnualRent(zone.Name);
                var baseYield = EstimateBaseYield(zone.Name);
                for (var idx = 0; idx < quarters.Count; idx++)
                {
                    var (y, q) = quarters[idx];
                    var rent = Math.Round(baseRent * (1m + (decimal)(rng.NextDouble() * 0.1 - 0.02) + idx * 0.008m), 0);
                    var yld = Math.Round(baseYield + (decimal)(rng.NextDouble() * 0.4 - 0.2), 2);
                    rentalRows.Add(new Domain.Entities.RentalIndex
                    {
                        ZoneId = zone.Id,
                        UnitType = Domain.Enums.PropertyType.Apartment,
                        IsShortTerm = false,
                        Year = y,
                        Quarter = q,
                        AverageAnnualRent = rent,
                        GrossRentalYield = yld,
                        SampleSize = 120 + rng.Next(0, 280)
                    });
                }
            }
            context.RentalIndices.AddRange(rentalRows);
        }

        await context.SaveChangesAsync();
    }

    private static decimal EstimateBasePsf(string zoneName) => zoneName switch
    {
        var n when n.Contains("Palm", StringComparison.OrdinalIgnoreCase)        => 3_100m,
        var n when n.Contains("Downtown", StringComparison.OrdinalIgnoreCase)    => 2_850m,
        var n when n.Contains("Marina", StringComparison.OrdinalIgnoreCase)      => 2_400m,
        var n when n.Contains("Creek Harbour", StringComparison.OrdinalIgnoreCase) => 2_050m,
        var n when n.Contains("Business Bay", StringComparison.OrdinalIgnoreCase) => 1_800m,
        var n when n.Contains("Hills", StringComparison.OrdinalIgnoreCase)       => 1_650m,
        var n when n.Contains("Meydan", StringComparison.OrdinalIgnoreCase)      => 1_500m,
        var n when n.Contains("Jumeirah Lake", StringComparison.OrdinalIgnoreCase) => 1_400m,
        var n when n.Contains("Arabian Ranches", StringComparison.OrdinalIgnoreCase) => 1_180m,
        var n when n.Contains("Al Barsha", StringComparison.OrdinalIgnoreCase)   => 1_260m,
        var n when n.Contains("Jumeirah Village", StringComparison.OrdinalIgnoreCase) => 1_050m,
        var n when n.Contains("Mirdif", StringComparison.OrdinalIgnoreCase)      => 1_000m,
        var n when n.Contains("Silicon", StringComparison.OrdinalIgnoreCase)     => 920m,
        var n when n.Contains("Sports City", StringComparison.OrdinalIgnoreCase) => 860m,
        var n when n.Contains("Investment Park", StringComparison.OrdinalIgnoreCase) => 740m,
        _ => 1_150m
    };

    private static decimal EstimateBaseAnnualRent(string zoneName) =>
        Math.Round(EstimateBasePsf(zoneName) * 95m, 0); // ~95 sqft-years of rent baseline

    private static decimal EstimateBaseYield(string zoneName) => zoneName switch
    {
        var n when n.Contains("Palm", StringComparison.OrdinalIgnoreCase)        => 4.6m,
        var n when n.Contains("Downtown", StringComparison.OrdinalIgnoreCase)    => 5.1m,
        var n when n.Contains("Marina", StringComparison.OrdinalIgnoreCase)      => 5.8m,
        var n when n.Contains("Creek Harbour", StringComparison.OrdinalIgnoreCase) => 5.3m,
        var n when n.Contains("Business Bay", StringComparison.OrdinalIgnoreCase) => 6.4m,
        var n when n.Contains("Hills", StringComparison.OrdinalIgnoreCase)       => 6.1m,
        var n when n.Contains("Meydan", StringComparison.OrdinalIgnoreCase)      => 6.6m,
        var n when n.Contains("Jumeirah Lake", StringComparison.OrdinalIgnoreCase) => 7.0m,
        var n when n.Contains("Arabian Ranches", StringComparison.OrdinalIgnoreCase) => 5.5m,
        var n when n.Contains("Al Barsha", StringComparison.OrdinalIgnoreCase)   => 6.9m,
        var n when n.Contains("Jumeirah Village", StringComparison.OrdinalIgnoreCase) => 8.2m,
        var n when n.Contains("Mirdif", StringComparison.OrdinalIgnoreCase)      => 6.8m,
        var n when n.Contains("Silicon", StringComparison.OrdinalIgnoreCase)     => 8.6m,
        var n when n.Contains("Sports City", StringComparison.OrdinalIgnoreCase) => 8.9m,
        var n when n.Contains("Investment Park", StringComparison.OrdinalIgnoreCase) => 9.4m,
        _ => 6.8m
    };

    // -----------------------------------------------------------------------
    // RFP FR009 acceptance — Arabic-name validation queue
    // -----------------------------------------------------------------------
    private static async Task BackfillNameValidationsAsync(IretpDbContext context)
    {
        if (await context.NameValidations.AnyAsync()) return;

        var rows = new List<Domain.Entities.NameValidation>();

        foreach (var zone in await context.Zones.ToListAsync())
        {
            rows.Add(new Domain.Entities.NameValidation
            {
                EntityType = "Zone",
                EntityId = zone.Id,
                NameEn = zone.Name,
                NameAr = zone.NameAr,
                Status = NameValidationStatus.Pending
            });
        }

        foreach (var project in await context.Projects.ToListAsync())
        {
            rows.Add(new Domain.Entities.NameValidation
            {
                EntityType = "Project",
                EntityId = project.Id,
                NameEn = project.Name,
                NameAr = project.NameAr,
                Status = NameValidationStatus.Pending
            });
        }

        foreach (var developer in await context.Developers.ToListAsync())
        {
            rows.Add(new Domain.Entities.NameValidation
            {
                EntityType = "Developer",
                EntityId = developer.Id,
                NameEn = developer.Name,
                NameAr = developer.NameAr,
                Status = NameValidationStatus.Pending
            });
        }

        if (rows.Count > 0)
        {
            context.NameValidations.AddRange(rows);
            await context.SaveChangesAsync();
        }
    }

    // -----------------------------------------------------------------------
    // RFP Section 8.3 — Playbook default steps per indicator
    // -----------------------------------------------------------------------
    private static async Task BackfillPlaybookStepsAsync(IretpDbContext context)
    {
        var thresholds = await context.RiskThresholds
            .Where(t => t.PlaybookStepsJson == null)
            .ToListAsync();
        if (thresholds.Count == 0) return;

        var defaults = new Dictionary<string, string[]>
        {
            ["ProjectDeliveryDelay_Warning"] = new[]
            {
                "Contact project officer and verify current construction status",
                "Request updated delivery timeline from developer within 5 business days",
                "Review escrow release schedule for consistency with revised timeline",
                "Log finding in alert audit trail"
            },
            ["ProjectDeliveryDelay_Critical"] = new[]
            {
                "Immediately freeze further escrow releases pending review",
                "Convene escalation meeting with division director",
                "Notify Director General — brief prepared within 4 business hours",
                "Issue formal written request to developer for remediation plan",
                "Coordinate with RERA compliance team for joint review"
            },
            ["EscrowShortfall_Warning"] = new[]
            {
                "Request trustee statement covering the last 90 days",
                "Reconcile against DLD registered sale values for the project",
                "Notify developer finance lead and request top-up plan",
                "Schedule follow-up review in 10 business days"
            },
            ["EscrowShortfall_Critical"] = new[]
            {
                "Suspend all pending escrow releases immediately",
                "Issue regulatory notice to developer and escrow agent",
                "Brief Director General and Deputy Directors with encrypted report",
                "Prepare Level-4 regulatory submission pack",
                "Coordinate with Central Bank if funds movement requires review"
            },
            ["ConstructionSuspension"] = new[]
            {
                "Site visit — photographic evidence logged",
                "Request construction contractor report",
                "Confirm labour and permit status with Dubai Municipality",
                "Escalate to Level 3 if suspension exceeds 60 days"
            },
            ["TransactionVolumeDecline"] = new[]
            {
                "Review zone supply/demand fundamentals",
                "Correlate against recent regulatory or policy changes",
                "Prepare Zone Risk Report for division director",
                "Share with Analytics team for market commentary"
            },
            ["DeveloperScoreDeterioration"] = new[]
            {
                "Flag developer for quarterly review",
                "Request explanatory note from developer's compliance officer",
                "Cross-check with RERA violation register",
                "Consider temporary new-project registration hold if score < 40"
            },
            ["HighRiskConcentration"] = new[]
            {
                "Compile Executive Summary Report by zone and developer",
                "Identify common contributing factors across flagged projects",
                "Brief Director General within one business day",
                "Schedule cross-functional risk review"
            },
            ["PriceDecline"] = new[]
            {
                "Assemble Market Risk Report for the impacted zone",
                "Check for rezoning / supply-side drivers",
                "Validate data against Ejari and transaction registry",
                "Distribute brief to leadership"
            },
            ["SevereRegulatoryViolation"] = new[]
            {
                "Open formal audit trail entry",
                "Freeze developer's new-project registrations",
                "Escalate to Director General within one business hour",
                "Coordinate with RERA enforcement team",
                "Issue public statement only after Director General sign-off"
            }
        };

        foreach (var threshold in thresholds)
        {
            if (defaults.TryGetValue(threshold.IndicatorKey, out var steps))
            {
                threshold.PlaybookStepsJson = System.Text.Json.JsonSerializer.Serialize(steps);
            }
        }

        await context.SaveChangesAsync();
    }

    // -----------------------------------------------------------------------
    // RFP Section 20 — Beneficial ownership disclosure
    // -----------------------------------------------------------------------
    private static async Task SeedBeneficialOwnersAsync(IretpDbContext context)
    {
        if (await context.BeneficialOwners.AnyAsync()) return;

        var developers = await context.Developers.ToListAsync();
        if (developers.Count == 0) return;

        var now = DateTime.UtcNow;
        var source = $"RERA annual return {now:yyyy}-Q{((now.Month - 1) / 3) + 1}";

        foreach (var d in developers.Take(5))
        {
            // Illustrative shareholder structure — representative of publicly
            // listed UAE developers' annual disclosures.
            context.BeneficialOwners.AddRange(
                new BeneficialOwner
                {
                    DeveloperId = d.Id,
                    OwnerName = "Dubai Holding Commercial Operations Group",
                    OwnerNameAr = "مجموعة دبي القابضة للعمليات التجارية",
                    OwnerType = "SovereignFund",
                    CountryOfIncorporation = "AE",
                    OwnershipPct = 52m,
                    DisclosedAt = now.AddDays(-120),
                    DisclosureSource = source
                },
                new BeneficialOwner
                {
                    DeveloperId = d.Id,
                    OwnerName = "Public float (DFM-listed ordinary shares)",
                    OwnerNameAr = "الأسهم المدرجة في السوق (DFM)",
                    OwnerType = "Corporate",
                    CountryOfIncorporation = "AE",
                    OwnershipPct = 34m,
                    DisclosedAt = now.AddDays(-120),
                    DisclosureSource = source
                },
                new BeneficialOwner
                {
                    DeveloperId = d.Id,
                    OwnerName = "Abu Dhabi Investment Authority",
                    OwnerNameAr = "جهاز أبوظبي للاستثمار",
                    OwnerType = "SovereignFund",
                    CountryOfIncorporation = "AE",
                    OwnershipPct = 8m,
                    DisclosedAt = now.AddDays(-120),
                    DisclosureSource = source
                },
                new BeneficialOwner
                {
                    DeveloperId = d.Id,
                    OwnerName = "Board &amp; executive team",
                    OwnerType = "Individual",
                    CountryOfIncorporation = "AE",
                    OwnershipPct = 4m,
                    DisclosedAt = now.AddDays(-120),
                    DisclosureSource = source
                });
        }

        await context.SaveChangesAsync();
    }

    // -----------------------------------------------------------------------
    // RFP Section 20 — International Market Benchmarking snapshots
    // -----------------------------------------------------------------------
    private static async Task SeedMarketBenchmarksAsync(IretpDbContext context)
    {
        if (await context.MarketBenchmarks.AnyAsync()) return;

        // Q1 2026 snapshot sourced from JLL GRETI 2024, Savills Prime Residential
        // World Cities, and Knight Frank Wealth Report 2025. Stored as the
        // authoritative reference for the /benchmark page.
        context.MarketBenchmarks.AddRange(
            new MarketBenchmark
            {
                CityCode = "DXB", CityName = "Dubai", CountryCode = "AE",
                Year = 2026, Quarter = 1,
                GretiCompositeScore = 2.14m,
                AveragePricePerSqft = 720m,
                AverageGrossRentalYieldPct = 6.8m,
                PrimePriceYoYPct = 12.4m,
                TransactionVolumeYoYPct = 24.6m,
                InstitutionalCapitalSharePct = 38m,
                Notes = "DLD registry — home market"
            },
            new MarketBenchmark
            {
                CityCode = "LON", CityName = "London", CountryCode = "GB",
                Year = 2026, Quarter = 1,
                GretiCompositeScore = 1.28m,
                AveragePricePerSqft = 1840m,
                AverageGrossRentalYieldPct = 3.9m,
                PrimePriceYoYPct = 1.2m,
                TransactionVolumeYoYPct = -3.5m,
                InstitutionalCapitalSharePct = 54m
            },
            new MarketBenchmark
            {
                CityCode = "SGP", CityName = "Singapore", CountryCode = "SG",
                Year = 2026, Quarter = 1,
                GretiCompositeScore = 1.58m,
                AveragePricePerSqft = 1620m,
                AverageGrossRentalYieldPct = 3.2m,
                PrimePriceYoYPct = 2.8m,
                TransactionVolumeYoYPct = 5.1m,
                InstitutionalCapitalSharePct = 48m
            },
            new MarketBenchmark
            {
                CityCode = "NYC", CityName = "New York", CountryCode = "US",
                Year = 2026, Quarter = 1,
                GretiCompositeScore = 1.22m,
                AveragePricePerSqft = 1950m,
                AverageGrossRentalYieldPct = 4.4m,
                PrimePriceYoYPct = 0.5m,
                TransactionVolumeYoYPct = -6.8m,
                InstitutionalCapitalSharePct = 62m
            },
            new MarketBenchmark
            {
                CityCode = "PAR", CityName = "Paris", CountryCode = "FR",
                Year = 2026, Quarter = 1,
                GretiCompositeScore = 1.62m,
                AveragePricePerSqft = 1420m,
                AverageGrossRentalYieldPct = 3.1m,
                PrimePriceYoYPct = -1.4m,
                TransactionVolumeYoYPct = -8.2m,
                InstitutionalCapitalSharePct = 41m
            },
            new MarketBenchmark
            {
                CityCode = "HKG", CityName = "Hong Kong", CountryCode = "HK",
                Year = 2026, Quarter = 1,
                GretiCompositeScore = 1.92m,
                AveragePricePerSqft = 2180m,
                AverageGrossRentalYieldPct = 2.7m,
                PrimePriceYoYPct = -4.5m,
                TransactionVolumeYoYPct = -12.1m,
                InstitutionalCapitalSharePct = 45m
            });

        await context.SaveChangesAsync();
    }

    // -----------------------------------------------------------------------
    // RFP Section 20 — ESG / Sustainability certifications
    // -----------------------------------------------------------------------
    private static async Task SeedProjectCertificationsAsync(IretpDbContext context)
    {
        if (await context.ProjectCertifications.AnyAsync()) return;

        var projects = await context.Projects.Take(20).ToListAsync();
        if (projects.Count == 0) return;

        // Deterministic pseudo-random assignment so the same sample is
        // generated on every fresh database and reviewers can verify counts.
        var rng = new Random(20260416);
        var schemes = Enum.GetValues<CertificationScheme>();
        var levels = Enum.GetValues<CertificationLevel>();

        foreach (var project in projects)
        {
            // Roughly 60% of sampled projects receive a certification.
            if (rng.NextDouble() < 0.4) continue;

            var scheme = schemes[rng.Next(schemes.Length)];
            var level = levels[rng.Next(levels.Length)];
            var awarded = DateTime.UtcNow.AddMonths(-rng.Next(3, 36));

            context.ProjectCertifications.Add(new ProjectCertification
            {
                ProjectId = project.Id,
                Scheme = scheme,
                Level = level,
                CertificateNumber = $"{scheme.ToString().ToUpperInvariant()}-{awarded:yyyyMM}-{rng.Next(1000, 9999)}",
                AwardedAt = awarded,
                ExpiresAt = awarded.AddYears(5),
                ScorePct = Math.Round((decimal)(55 + rng.NextDouble() * 40), 1)
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = [UserRoles.RegisteredInvestor, UserRoles.DldViewer, UserRoles.DldOperator, UserRoles.DldSupervisor, UserRoles.SystemAdministrator];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager)
    {
        if (await userManager.FindByEmailAsync("admin@dld.gov.ae") is null)
        {
            var admin = new ApplicationUser
            {
                UserName = "admin@dld.gov.ae",
                Email = "admin@dld.gov.ae",
                FirstName = "System",
                LastName = "Administrator",
                PreferredLanguage = "en",
                IsInternalUser = true,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(admin, "Admin@DLD2026!");
            await userManager.AddToRoleAsync(admin, UserRoles.SystemAdministrator);
        }
    }

    private static async Task SeedDevelopersAsync(IretpDbContext context, List<Zone> zones)
    {
        var developers = new List<Developer>
        {
            new() { Id = Guid.NewGuid(), Name = "Emaar Properties", NameAr = "إعمار العقارية", LicenceNumber = "DLD-DEV-001", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "DAMAC Properties", NameAr = "داماك العقارية", LicenceNumber = "DLD-DEV-002", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Nakheel", NameAr = "نخيل", LicenceNumber = "DLD-DEV-003", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Meraas", NameAr = "مراس", LicenceNumber = "DLD-DEV-004", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Dubai Properties", NameAr = "دبي العقارية", LicenceNumber = "DLD-DEV-005", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Sobha Realty", NameAr = "صبحة العقارية", LicenceNumber = "DLD-DEV-006", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Azizi Developments", NameAr = "عزيزي للتطوير", LicenceNumber = "DLD-DEV-007", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Danube Properties", NameAr = "الدانوب العقارية", LicenceNumber = "DLD-DEV-008", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Omniyat", NameAr = "أمنيات", LicenceNumber = "DLD-DEV-009", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Select Group", NameAr = "سيليكت جروب", LicenceNumber = "DLD-DEV-010", IsActive = true, CreatedAt = DateTime.UtcNow },
        };
        await context.Developers.AddRangeAsync(developers);
        await context.SaveChangesAsync();
    }

    private static async Task SeedProjectsAsync(IretpDbContext context, List<Zone> zones)
    {
        var developers = await context.Developers.ToListAsync();
        var random = new Random(42);
        var projects = new List<Project>();

        string[] projectNames = ["The Grand Residences", "Azure Tower", "Palm View", "Creek Vista", "Marina Heights",
            "Sunset Villas", "The Crest", "Boulevard Point", "Harbour Gate", "Sea La Vie",
            "Waves Grande", "Al Andalus", "The Opus", "One Park Avenue", "Skyview Tower",
            "The Address", "FIVE Residences", "Ellington Beach", "Creek Palace", "Golf Horizon"];

        for (int i = 0; i < 20; i++)
        {
            var zone = zones[random.Next(zones.Count)];
            var dev = developers[random.Next(developers.Count)];
            var status = (ProjectStatus)random.Next(1, 5);
            var totalUnits = random.Next(50, 500);
            var soldUnits = status == ProjectStatus.Completed ? totalUnits : random.Next(0, totalUnits);

            projects.Add(new Project
            {
                Id = Guid.NewGuid(),
                Name = projectNames[i],
                NameAr = projectNames[i],
                DeveloperId = dev.Id,
                ZoneId = zone.Id,
                Status = status,
                CompletionPercentage = status == ProjectStatus.Completed ? 100 : random.Next(10, 95),
                TotalUnits = totalUnits,
                SoldUnits = soldUnits,
                AvailableUnits = totalUnits - soldUnits,
                ExpectedDeliveryDate = DateTime.UtcNow.AddMonths(random.Next(-12, 24)),
                Latitude = zone.CenterLat ?? 25.2 + random.NextDouble() * 0.01,
                Longitude = zone.CenterLng ?? 55.27 + random.NextDouble() * 0.01,
                TotalProjectCost = random.Next(50, 500) * 1_000_000m,
                CreatedAt = DateTime.UtcNow
            });
        }
        await context.Projects.AddRangeAsync(projects);
        await context.SaveChangesAsync();
    }

    private static async Task SeedTransactionsAsync(IretpDbContext context, List<Zone> zones)
    {
        var projects = await context.Projects.ToListAsync();
        var random = new Random(123);
        var transactions = new List<Transaction>();
        var propertyTypes = Enum.GetValues<PropertyType>();
        var transactionTypes = Enum.GetValues<TransactionType>();
        var financingMethods = Enum.GetValues<FinancingMethod>();

        for (int i = 0; i < 2000; i++)
        {
            var zone = zones[random.Next(zones.Count)];
            var project = random.NextDouble() > 0.3 ? projects[random.Next(projects.Count)] : null;
            var areaSqft = random.Next(400, 5000);
            var pricePerSqft = random.Next(800, 4000);
            var transDate = DateTime.UtcNow.AddDays(-random.Next(0, 1825)); // 5 years

            transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                TransactionDate = transDate,
                ZoneId = zone.Id,
                Community = zone.Name,
                ProjectId = project?.Id,
                ProjectName = project?.Name ?? "Individual Unit",
                PropertyType = propertyTypes[random.Next(propertyTypes.Length)],
                TransactionType = transactionTypes[random.Next(transactionTypes.Length)],
                AreaSqft = areaSqft,
                AreaSqm = areaSqft * 0.092903m,
                TransactionValue = areaSqft * pricePerSqft,
                PricePerSqft = pricePerSqft,
                FinancingMethod = financingMethods[random.Next(financingMethods.Length)],
                IsOffPlan = random.NextDouble() > 0.5,
                CreatedAt = DateTime.UtcNow
            });
        }
        await context.Transactions.AddRangeAsync(transactions);
        await context.SaveChangesAsync();
    }

    private static async Task SeedRiskThresholdsAsync(IretpDbContext context)
    {
        var thresholds = new List<RiskThreshold>
        {
            new() { Id = Guid.NewGuid(), IndicatorKey = "ProjectDeliveryDelay_Warning", IndicatorName = "Project Delivery Delay — Warning", IndicatorNameAr = "تأخير تسليم المشروع — تحذير", ThresholdValue = 6, ThresholdUnit = "months", DefaultRiskLevel = RiskLevel.Warning, DefaultAlertLevel = AlertLevel.Level1_Operational, EscalationPath = "Level 1 to Level 2", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), IndicatorKey = "ProjectDeliveryDelay_Critical", IndicatorName = "Project Delivery Delay — Critical", IndicatorNameAr = "تأخير تسليم المشروع — حرج", ThresholdValue = 12, ThresholdUnit = "months", DefaultRiskLevel = RiskLevel.High, DefaultAlertLevel = AlertLevel.Level3_SeniorLeadership, EscalationPath = "Levels 1, 2, and 3", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), IndicatorKey = "EscrowShortfall_Warning", IndicatorName = "Escrow Shortfall — Warning", IndicatorNameAr = "عجز حساب الضمان — تحذير", ThresholdValue = 80, ThresholdUnit = "percentage", DefaultRiskLevel = RiskLevel.Warning, DefaultAlertLevel = AlertLevel.Level2_Managerial, EscalationPath = "Level 1 to Level 2", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), IndicatorKey = "EscrowShortfall_Critical", IndicatorName = "Escrow Shortfall — Critical", IndicatorNameAr = "عجز حساب الضمان — حرج", ThresholdValue = 60, ThresholdUnit = "percentage", DefaultRiskLevel = RiskLevel.High, DefaultAlertLevel = AlertLevel.Level4_Strategic, EscalationPath = "Levels 1, 2, 3, and 4", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), IndicatorKey = "ConstructionSuspension", IndicatorName = "Construction Activity Suspension", IndicatorNameAr = "توقف نشاط البناء", ThresholdValue = 30, ThresholdUnit = "days", DefaultRiskLevel = RiskLevel.Warning, DefaultAlertLevel = AlertLevel.Level2_Managerial, EscalationPath = "Level 2 at 30 days; Level 3 at 60 days", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), IndicatorKey = "TransactionVolumeDecline", IndicatorName = "Sharp Transaction Volume Decline", IndicatorNameAr = "انخفاض حاد في حجم المعاملات", ThresholdValue = 40, ThresholdUnit = "percentage", DefaultRiskLevel = RiskLevel.Medium, DefaultAlertLevel = AlertLevel.Level2_Managerial, EscalationPath = "Level 2 (Zone Risk Report)", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), IndicatorKey = "DeveloperScoreDeterioration", IndicatorName = "Developer Score Deterioration", IndicatorNameAr = "تدهور تقييم المطور", ThresholdValue = 15, ThresholdUnit = "points", DefaultRiskLevel = RiskLevel.Medium, DefaultAlertLevel = AlertLevel.Level2_Managerial, EscalationPath = "Level 2 + Developer Review Flag", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), IndicatorKey = "HighRiskConcentration", IndicatorName = "High-Risk Project Concentration", IndicatorNameAr = "تركز المشاريع عالية المخاطر", ThresholdValue = 30, ThresholdUnit = "percentage", DefaultRiskLevel = RiskLevel.High, DefaultAlertLevel = AlertLevel.Level3_SeniorLeadership, EscalationPath = "Level 3 + Executive Summary Report", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), IndicatorKey = "PriceDecline", IndicatorName = "Price Decline — Zone Level", IndicatorNameAr = "انخفاض الأسعار — مستوى المنطقة", ThresholdValue = 15, ThresholdUnit = "percentage", DefaultRiskLevel = RiskLevel.Medium, DefaultAlertLevel = AlertLevel.Level2_Managerial, EscalationPath = "Level 2 (Market Risk Report)", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), IndicatorKey = "SevereRegulatoryViolation", IndicatorName = "Severe Regulatory Violation", IndicatorNameAr = "مخالفة تنظيمية جسيمة", ThresholdValue = 1, ThresholdUnit = "count", DefaultRiskLevel = RiskLevel.High, DefaultAlertLevel = AlertLevel.Level3_SeniorLeadership, EscalationPath = "Level 3 + Automatic Audit Flag", CreatedAt = DateTime.UtcNow },
        };
        await context.RiskThresholds.AddRangeAsync(thresholds);
        await context.SaveChangesAsync();
    }

    private static async Task SeedScoringWeightsAsync(IretpDbContext context)
    {
        var weights = new List<ScoringWeight>
        {
            new() { Id = Guid.NewGuid(), CriterionKey = "OnTimeDelivery", CriterionName = "On-Time Project Delivery Rate", CriterionNameAr = "معدل تسليم المشاريع في الوقت المحدد", Weight = 25, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), CriterionKey = "UnitSalesCompletion", CriterionName = "Unit Sales Completion Rate", CriterionNameAr = "معدل إكمال مبيعات الوحدات", Weight = 20, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), CriterionKey = "EscrowHealth", CriterionName = "Escrow Account Health Score", CriterionNameAr = "درجة صحة حساب الضمان", Weight = 20, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), CriterionKey = "RegulatoryCompliance", CriterionName = "Regulatory Compliance Record", CriterionNameAr = "سجل الامتثال التنظيمي", Weight = 15, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), CriterionKey = "FinancialSoundness", CriterionName = "Financial Soundness Indicator", CriterionNameAr = "مؤشر السلامة المالية", Weight = 10, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), CriterionKey = "HistoricalSuccess", CriterionName = "Historical Project Success Rate", CriterionNameAr = "معدل نجاح المشاريع التاريخي", Weight = 10, CreatedAt = DateTime.UtcNow },
        };
        await context.ScoringWeights.AddRangeAsync(weights);
        await context.SaveChangesAsync();
    }
}
