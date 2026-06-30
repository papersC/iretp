using System.Net.Http.Json;
using IRETP.Application.DTOs;
using IRETP.Application.DTOs.Fabric;

namespace IRETP.Web.Services;

public class AdminApiClient
{
    private readonly HttpClient _http;

    public AdminApiClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("AdminApi");
    }

    public void SetToken(string? token)
    {
        if (token != null)
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        else
            _http.DefaultRequestHeaders.Authorization = null;
    }

    // EWRS
    public async Task<EwrsDashboardData?> GetEwrsDashboardAsync() =>
        await _http.GetFromJsonAsync<EwrsDashboardData>("api/admin/ewrs/dashboard");

    public async Task<List<RiskAlertItem>?> GetRiskAlertsAsync() =>
        await _http.GetFromJsonAsync<List<RiskAlertItem>>("api/admin/ewrs/alerts");

    public async Task<bool> AcknowledgeAlertAsync(Guid id)
    {
        var response = await _http.PutAsync($"api/admin/ewrs/alerts/{id}/acknowledge", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ResolveAlertAsync(Guid id, string? notes = null)
    {
        var response = await _http.PutAsJsonAsync($"api/admin/ewrs/alerts/{id}/resolve", new { actionNotes = notes });
        return response.IsSuccessStatusCode;
    }

    public async Task<List<RiskThresholdItem>?> GetThresholdsAsync() =>
        await _http.GetFromJsonAsync<List<RiskThresholdItem>>("api/admin/ewrs/thresholds");

    // Escrow
    public async Task<List<EscrowItem>?> GetEscrowDashboardAsync() =>
        await _http.GetFromJsonAsync<List<EscrowItem>>("api/admin/escrow/dashboard");

    public async Task<(byte[] Content, string FileName)?> DownloadEscrowMonthlyReportAsync(
        Guid projectId, int? year = null, int? month = null)
    {
        var url = $"api/admin/escrow/{projectId}/monthly-report";
        if (year.HasValue && month.HasValue)
            url += $"?year={year}&month={month}";

        var response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        var content = await response.Content.ReadAsByteArrayAsync();
        var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                       ?? $"IRETP_Escrow_{projectId:N}.pdf";
        return (content, fileName);
    }

    // Developer Rating
    public async Task<DeveloperProfileData?> GetDeveloperProfileAsync(Guid id) =>
        await _http.GetFromJsonAsync<DeveloperProfileData>($"api/admin/developers/{id}/profile");

    public async Task<List<ScoringWeightItem>?> GetScoringWeightsAsync() =>
        await _http.GetFromJsonAsync<List<ScoringWeightItem>>("api/admin/developers/scoring-weights");

    // Audit
    public async Task<AuditLogPageResult?> GetAuditLogsAsync(
        string? entityType = null,
        string? action = null,
        string? search = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 50)
    {
        var url = $"api/admin/audit/logs?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(entityType)) url += $"&entityType={Uri.EscapeDataString(entityType)}";
        if (!string.IsNullOrEmpty(action)) url += $"&action={Uri.EscapeDataString(action)}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        if (from.HasValue) url += $"&from={from:yyyy-MM-ddTHH:mm:ssZ}";
        if (to.HasValue) url += $"&to={to:yyyy-MM-ddTHH:mm:ssZ}";

        try
        {
            return await _http.GetFromJsonAsync<AuditLogPageResult>(url);
        }
        catch
        {
            return null;
        }
    }

    // Playbooks (RFP 8.3)
    public async Task<List<PlaybookDto>?> GetPlaybooksAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<PlaybookDto>>("api/admin/ewrs/playbooks");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdatePlaybookAsync(Guid thresholdId, List<string> steps)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/admin/ewrs/playbooks/{thresholdId}", new { steps });
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAlertPlaybookProgressAsync(
        Guid alertId, List<PlaybookProgressEntry> progress)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/admin/ewrs/alerts/{alertId}/playbook", new { progress });
        return response.IsSuccessStatusCode;
    }

    // AI model status (RFP 5.3)
    public async Task<List<AIModelStatusItem>?> GetAIModelStatusAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<AIModelStatusItem>>("api/admin/ai-models/status");
        }
        catch
        {
            return null;
        }
    }

    // Microsoft Fabric / OneLake (RFP v1.3 §11.4)
    public async Task<FabricStatusItem?> GetFabricStatusAsync()
    {
        try { return await _http.GetFromJsonAsync<FabricStatusItem>("api/admin/fabric/status"); }
        catch { return null; }
    }

    public async Task<List<FabricSemanticModelItem>?> GetFabricSemanticModelsAsync()
    {
        try { return await _http.GetFromJsonAsync<List<FabricSemanticModelItem>>("api/admin/fabric/semantic-models"); }
        catch { return null; }
    }

    public async Task<FabricFreshnessItem?> GetFabricFreshnessAsync()
    {
        try { return await _http.GetFromJsonAsync<FabricFreshnessItem>("api/admin/fabric/freshness"); }
        catch { return null; }
    }

    // Developer comparison
    public async Task<DeveloperComparisonDto?> CompareDevelopersAsync(IEnumerable<Guid> developerIds)
    {
        var ids = string.Join("&", developerIds.Select(id => $"ids={id}"));
        try
        {
            return await _http.GetFromJsonAsync<DeveloperComparisonDto>(
                $"api/admin/developers/compare?{ids}");
        }
        catch
        {
            return null;
        }
    }
}

public class AuditLogPageResult
{
    public List<AuditLogItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class AIModelStatusItem
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public bool Active { get; set; }
    public long SuccessCalls { get; set; }
    public long FailedCalls { get; set; }
    public double AverageLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public DateTime? LastCalledAt { get; set; }
    public string? LastError { get; set; }
    public bool FallbackActive { get; set; }
}

public class AuditLogItem
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Action { get; set; } = "";
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}


// Admin DTOs
public class EwrsDashboardData
{
    public int TotalHighRiskProjects { get; set; }
    public int TotalWarningProjects { get; set; }
    public int ProjectsWithEscrowShortfall { get; set; }
    public int ProjectsWithConstructionHalt { get; set; }
    public int TotalActiveAlerts { get; set; }
    public int UnacknowledgedAlerts { get; set; }
    public List<ZoneRiskItem> ZoneRiskSummary { get; set; } = [];
    public List<RiskAlertItem> RecentAlerts { get; set; } = [];
}

public class ZoneRiskItem
{
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = "";
    public int HighRiskCount { get; set; }
    public int WarningCount { get; set; }
    public int TotalProjects { get; set; }
}

public class RiskAlertItem
{
    public Guid Id { get; set; }
    public string IndicatorType { get; set; } = "";
    public int RiskLevel { get; set; }
    public int AlertLevel { get; set; }
    public int Status { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? PlaybookProgressJson { get; set; }
    // RFP Section 8.2 — SLA tracking
    public DateTime? AcknowledgeDeadline { get; set; }
    public DateTime? ResolutionDeadline { get; set; }
    public DateTime? LastEscalatedAt { get; set; }
    public bool AutoEscalated { get; set; }
}

public class RiskThresholdItem
{
    public Guid Id { get; set; }
    public string IndicatorKey { get; set; } = "";
    public string IndicatorName { get; set; } = "";
    public decimal ThresholdValue { get; set; }
    public string? ThresholdUnit { get; set; }
    public int DefaultRiskLevel { get; set; }
    public int DefaultAlertLevel { get; set; }
}

public class EscrowItem
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public string? DeveloperName { get; set; }
    public string? BankName { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal RequiredMinimumBalance { get; set; }
    public decimal AdequacyRatio { get; set; }
    public int Status { get; set; }
}

public class DeveloperProfileData
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? NameAr { get; set; }
    public int TotalProjects { get; set; }
    public int CompletedProjects { get; set; }
    public int TotalUnitsDelivered { get; set; }
    public decimal OnTimeDeliveryRate { get; set; }
    public List<DeveloperProjectItem> Projects { get; set; } = [];
}

public class DeveloperProjectItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Status { get; set; }
    public decimal CompletionPercentage { get; set; }
    public int TotalUnits { get; set; }
}

public class ScoringWeightItem
{
    public Guid Id { get; set; }
    public string CriterionKey { get; set; } = "";
    public string CriterionName { get; set; } = "";
    public decimal Weight { get; set; }
}

public class FabricStatusItem
{
    public FabricSourceMode Mode { get; set; }
    public bool Available { get; set; }
    public string? WorkspaceId { get; set; }
    public string? LakehouseId { get; set; }
    public string? Region { get; set; }
    public string? Detail { get; set; }
    public DateTime ProbedAtUtc { get; set; }
}

public class FabricSemanticModelItem
{
    public string Name { get; set; } = "";
    public string Layer { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Measures { get; set; } = new();
    public List<string> Dimensions { get; set; } = new();
}

public class FabricFreshnessItem
{
    public DateTime? GoldLayerLastWriteUtc { get; set; }
    public DateTime? SilverLayerLastWriteUtc { get; set; }
    public TimeSpan? TransactionLag { get; set; }
    public TimeSpan? KpiLag { get; set; }
    public string? LastPipelineRunId { get; set; }
    public string? LastPipelineStatus { get; set; }
}
