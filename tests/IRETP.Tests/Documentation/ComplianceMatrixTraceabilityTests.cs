using System.Text.RegularExpressions;

namespace IRETP.Tests.Documentation;

/// <summary>
/// Self-validating compliance matrix — every file-path claim in
/// docs/COMPLIANCE_MATRIX.md must resolve to a real file on disk. Prevents
/// the matrix from drifting out of sync with the code as RFP requirements
/// move around. If this test starts failing, either the file was moved
/// (update the matrix) or the matrix references something aspirational
/// (delete the claim).
/// </summary>
public class ComplianceMatrixTraceabilityTests
{
    [Theory]
    [InlineData("COMPLIANCE_MATRIX.md")]
    [InlineData("DESC_ISR_V3_COMPLIANCE.md")]
    [InlineData("OWASP_TOP_10_MAPPING.md")]
    public void Every_file_path_claimed_in_a_compliance_doc_must_exist(string docName)
    {
        var repoRoot = FindRepoRoot();
        var docPath = Path.Combine(repoRoot, "docs", docName);
        Assert.True(File.Exists(docPath), $"Doc not found at {docPath}");

        var content = File.ReadAllText(docPath);

        // Extract every backticked token that looks like a real file path:
        // contains a "/" AND ends in a known source-or-config extension.
        // Excludes API paths like /api/dashboard/kpis (no extension).
        var pathRegex = new Regex(
            @"`([A-Za-z.][A-Za-z0-9_./\-]*\.(cs|razor|md|js|json|csproj|sln|bicep|yml|yaml|css))`",
            RegexOptions.Compiled);

        var claims = pathRegex.Matches(content)
            .Select(m => m.Groups[1].Value)
            .Where(p => p.Contains('/'))            // bare filenames can resolve in multiple locations — skip
            .Distinct()
            .ToList();

        Assert.NotEmpty(claims); // sanity — every compliance doc should claim *some* files

        var missing = new List<string>();
        foreach (var claim in claims)
        {
            // Claims are written with /-style separators; resolve against the repo root.
            var resolved = Path.Combine(repoRoot, claim.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(resolved) && !Directory.Exists(resolved))
            {
                missing.Add(claim);
            }
        }

        Assert.True(
            missing.Count == 0,
            $"{docName} references files that don't exist:\n  - " + string.Join("\n  - ", missing));
    }

    [Fact]
    public void Every_doc_in_familiarisation_pack_must_be_linked_from_the_readme()
    {
        var repoRoot = FindRepoRoot();
        var familiarisationDir = Path.Combine(repoRoot, "docs", "familiarisation");
        Assert.True(Directory.Exists(familiarisationDir));

        var docsInPack = Directory.GetFiles(familiarisationDir, "*.md")
            .Select(Path.GetFileName)
            .Where(f => !string.Equals(f, "README.md", StringComparison.OrdinalIgnoreCase))
            .Select(f => f!)
            .ToList();

        var readmePath = Path.Combine(familiarisationDir, "README.md");
        var readme = File.ReadAllText(readmePath);

        var missing = docsInPack.Where(d => !readme.Contains(d)).ToList();
        Assert.True(
            missing.Count == 0,
            "Familiarisation docs not linked from README.md:\n  - " + string.Join("\n  - ", missing));
    }

    [Fact]
    public void Architecture_doc_references_must_resolve()
    {
        // The ARCHITECTURE_INTEGRATION_MAP advertises a specific set of docs;
        // each one must exist so DLD reviewers can navigate the pack.
        var repoRoot = FindRepoRoot();
        var expected = new[]
        {
            "docs/ARCHITECTURE.md",
            "docs/ARCHITECTURE_INTEGRATION_MAP.md",
            "docs/VISUALISATION_LAYER_ANALYSIS.md",
            "docs/COMPLIANCE_MATRIX.md",
            "docs/DESC_ISR_V3_COMPLIANCE.md",
            "docs/OWASP_TOP_10_MAPPING.md",
            "docs/RISK_REGISTER.md",
            "docs/EXIT_PLAN.md",
            "docs/THREAT_MODEL.md",
            "docs/UAT_PLAN.md",
            "docs/V1_3_ALIGNMENT_SUMMARY.md",
            "docs/familiarisation/README.md",
            "docs/familiarisation/01_source_inventory.md",
            "docs/familiarisation/02_field_mapping.md",
            "docs/familiarisation/03_data_quality_baseline.md",
            "docs/familiarisation/04_calculation_rules.md",
            "docs/familiarisation/05_historical_data.md",
            "docs/familiarisation/06_fabric_environment.md",
            "docs/API_REFERENCE.md",
            "docs/RUNBOOK.md",
            "docs/DISASTER_RECOVERY.md",
            "docs/DATA_DICTIONARY.md",
        };

        var missing = expected
            .Where(rel => !File.Exists(Path.Combine(repoRoot, rel.Replace('/', Path.DirectorySeparatorChar))))
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Required documentation files are missing:\n  - " + string.Join("\n  - ", missing));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "IRETP.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate IRETP.sln walking up from " + AppContext.BaseDirectory);
    }
}
