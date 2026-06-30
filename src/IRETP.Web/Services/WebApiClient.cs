using System.Net.Http.Json;
using IRETP.Application.DTOs;

namespace IRETP.Web.Services;

public class WebApiClient
{
    private readonly HttpClient _http;
    private string? _token;
    private string? _captchaToken;

    public WebApiClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("WebApi");
    }

    public void SetToken(string? token)
    {
        _token = token;
        if (token != null)
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        else
            _http.DefaultRequestHeaders.Authorization = null;
    }

    // -----------------------------------------------------------------------
    // CAPTCHA (RFP 10.3)
    // -----------------------------------------------------------------------
    public string? CaptchaToken => _captchaToken;
    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);

    public async Task<CaptchaChallengeDto?> GetCaptchaChallengeAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<CaptchaChallengeDto>("api/captcha/challenge");
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> VerifyCaptchaAsync(string challengeId, string answer)
    {
        var response = await _http.PostAsJsonAsync("api/captcha/verify",
            new { challengeId, answer });
        if (!response.IsSuccessStatusCode) return null;
        var result = await response.Content.ReadFromJsonAsync<CaptchaTokenEnvelope>();
        _captchaToken = result?.Token;
        return _captchaToken;
    }

    private void AttachCaptchaHeader(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_captchaToken))
        {
            request.Headers.Remove("X-Captcha-Token");
            request.Headers.Add("X-Captcha-Token", _captchaToken);
        }
    }

    // Dashboard
    public async Task<DashboardKpiDto?> GetDashboardKpisAsync() =>
        await _http.GetFromJsonAsync<DashboardKpiDto>("api/dashboard/kpis");

    // Map
    public async Task<List<ZoneHeatmapItem>?> GetZoneHeatmapAsync() =>
        await _http.GetFromJsonAsync<List<ZoneHeatmapItem>>("api/map/zones/heatmap");

    public async Task<ZoneDetailDto?> GetZoneDetailAsync(Guid zoneId) =>
        await _http.GetFromJsonAsync<ZoneDetailDto>($"api/map/zones/{zoneId}");

    public async Task<List<ZoneDetailDto>?> CompareZonesAsync(IEnumerable<Guid> zoneIds)
    {
        var qs = string.Join("&", zoneIds.Select(id => $"zoneIds={id}"));
        return await _http.GetFromJsonAsync<List<ZoneDetailDto>>($"api/map/zones/compare?{qs}");
    }

    public async Task<List<ProjectMapPin>?> GetProjectsAsync(Guid? zoneId = null, int? status = null) =>
        await _http.GetFromJsonAsync<List<ProjectMapPin>>(
            $"api/map/projects?zoneId={zoneId}&status={status}");

    public async Task<ProjectDetailDto?> GetProjectDetailAsync(Guid projectId) =>
        await _http.GetFromJsonAsync<ProjectDetailDto>($"api/map/projects/{projectId}");

    // Price Index
    public async Task<PriceIndexResult?> GetPriceIndexAsync(Guid? zoneId = null, string? propertyType = null) =>
        await _http.GetFromJsonAsync<PriceIndexResult>(
            $"api/price-index?zoneId={zoneId}&propertyType={propertyType}");

    // Rental Index
    public async Task<RentalIndexResult?> GetRentalIndexAsync(Guid? zoneId = null) =>
        await _http.GetFromJsonAsync<RentalIndexResult>($"api/rental-index?zoneId={zoneId}");

    public async Task<List<RentalYieldCalculationDto>?> GetRentalYieldAsync(List<Guid>? zoneIds = null) =>
        await _http.GetFromJsonAsync<List<RentalYieldCalculationDto>>(
            $"api/rental-index/yield-calculator?{string.Join("&", (zoneIds ?? []).Select(z => $"zoneIds={z}"))}");

    // Transactions
    public async Task<TransactionPageResult?> GetTransactionsAsync(int page = 1, int pageSize = 20) =>
        await _http.GetFromJsonAsync<TransactionPageResult>($"api/transactions?page={page}&pageSize={pageSize}");

    public async Task<byte[]?> ExportTransactionsAsync(string format)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/transactions/export/{format}");
        AttachCaptchaHeader(request);
        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsByteArrayAsync();
    }

    // Developers
    public async Task<List<PublicDeveloperScorecardDto>?> GetDeveloperScorecardsAsync() =>
        await _http.GetFromJsonAsync<List<PublicDeveloperScorecardDto>>("api/developers/scorecards");

    // GRETI
    public async Task<GretiDashboardDto?> GetGretiDashboardAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<GretiDashboardDto>("api/greti/dashboard");
        }
        catch
        {
            return null;
        }
    }

    // ESG / Sustainability (RFP 20)
    public async Task<EsgDashboardDto?> GetEsgDashboardAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<EsgDashboardDto>("api/esg/dashboard");
        }
        catch
        {
            return null;
        }
    }

    // International Benchmarking (RFP 20)
    public async Task<BenchmarkDashboardDto?> GetBenchmarkDashboardAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<BenchmarkDashboardDto>("api/benchmark/dashboard");
        }
        catch
        {
            return null;
        }
    }

    // Investment Profile PDF (RFP 20 — Phase 4 deliverable #52)
    public async Task<(byte[] Content, string FileName)?> DownloadZoneInvestmentProfileAsync(Guid zoneId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/investment-profile/zone/{zoneId}");
        AttachCaptchaHeader(request);
        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsByteArrayAsync();
        var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                       ?? $"IRETP_ZoneProfile_{zoneId:N}.pdf";
        return (content, fileName);
    }

    public async Task<(byte[] Content, string FileName)?> DownloadProjectInvestmentProfileAsync(Guid projectId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/investment-profile/project/{projectId}");
        AttachCaptchaHeader(request);
        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsByteArrayAsync();
        var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                       ?? $"IRETP_ProjectProfile_{projectId:N}.pdf";
        return (content, fileName);
    }

    // Mortgage & Debt transparency (RFP 20)
    public async Task<MortgageDashboardDto?> GetMortgageDashboardAsync(int lookbackMonths = 24)
    {
        try
        {
            return await _http.GetFromJsonAsync<MortgageDashboardDto>(
                $"api/mortgage/dashboard?lookbackMonths={lookbackMonths}");
        }
        catch
        {
            return null;
        }
    }

    // Beneficial ownership (RFP 20)
    public async Task<List<BeneficialOwnershipDto>?> GetBeneficialOwnershipAsync(Guid developerId)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<BeneficialOwnershipDto>>(
                $"api/developers/{developerId}/ownership");
        }
        catch
        {
            return null;
        }
    }

    // CMS — versioning & preview workflow (RFP FR002)
    public async Task<List<CmsVersionDto>?> GetCmsVersionsAsync(Guid cmsContentId, int limit = 50)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<CmsVersionDto>>(
                $"api/cms/{cmsContentId}/versions?limit={limit}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> RollbackCmsVersionAsync(Guid cmsContentId, Guid versionId)
    {
        var response = await _http.PostAsync(
            $"api/cms/{cmsContentId}/rollback/{versionId}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<CmsPreviewLinkDto?> CreateCmsPreviewLinkAsync(Guid versionId, int ttlHours = 48)
    {
        var response = await _http.PostAsync(
            $"api/cms/versions/{versionId}/preview-link?ttlHours={ttlHours}", null);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<CmsPreviewLinkDto>();
    }

    public async Task<CmsPreviewContentDto?> GetCmsPreviewAsync(string token)
    {
        try
        {
            return await _http.GetFromJsonAsync<CmsPreviewContentDto>($"api/cms/preview/{token}");
        }
        catch
        {
            return null;
        }
    }

    // Analytics
    public async Task<AnalyticsResultDto?> ExecuteAnalyticsAsync(object request)
    {
        var response = await _http.PostAsJsonAsync("api/analytics/query", request);
        return await response.Content.ReadFromJsonAsync<AnalyticsResultDto>();
    }

    public async Task<(byte[] Content, string ContentType, string FileName)?> ExportAnalyticsAsync(
        string format, object requestBody)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"api/analytics/export/{format}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(requestBody)
        };
        AttachCaptchaHeader(httpRequest);

        var response = await _http.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode) return null;

        var content = await response.Content.ReadAsByteArrayAsync();
        var mime = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                       ?? response.Content.Headers.ContentDisposition?.FileName
                       ?? $"IRETP_Analytics.{format}";
        fileName = fileName.Trim('"');
        return (content, mime, fileName);
    }

    // AI Agent
    public async Task<AiQueryResponse?> QueryAiAgentAsync(string query, string language, string? sessionId)
    {
        var response = await _http.PostAsJsonAsync("api/ai/query",
            new { query, language, sessionId });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AiQueryResponse>();
    }

    // Alerts & Notifications
    public async Task<NotificationPageResult?> GetNotificationsAsync(bool? isRead = null, int page = 1, int pageSize = 50)
    {
        var url = $"api/alerts/notifications?page={page}&pageSize={pageSize}"
                  + (isRead.HasValue ? $"&isRead={isRead.Value.ToString().ToLowerInvariant()}" : "");
        try
        {
            return await _http.GetFromJsonAsync<NotificationPageResult>(url);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> MarkNotificationReadAsync(Guid id)
    {
        var response = await _http.PutAsync($"api/alerts/notifications/{id}/read", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> MarkAllNotificationsReadAsync()
    {
        var response = await _http.PutAsync("api/alerts/notifications/read-all", null);
        return response.IsSuccessStatusCode;
    }

    // Alert configurations
    public async Task<List<InvestorAlertDto>?> GetAlertConfigurationsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<InvestorAlertDto>>("api/alerts/configurations");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> ConfigureAlertAsync(object command)
    {
        var response = await _http.PostAsJsonAsync("api/alerts/configure", command);
        if (!response.IsSuccessStatusCode) return null;
        var result = await response.Content.ReadFromJsonAsync<IdEnvelope>();
        return result?.Id;
    }

    public async Task<bool> DeleteAlertAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/alerts/{id}");
        return response.IsSuccessStatusCode;
    }

    // Watchlist
    public async Task<List<WatchlistItemDto>?> GetWatchlistAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<WatchlistItemDto>>("api/alerts/watchlist");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> AddWatchlistItemAsync(Guid? projectId, Guid? zoneId, Guid? developerId)
    {
        var response = await _http.PostAsJsonAsync("api/alerts/watchlist",
            new { projectId, zoneId, developerId });
        if (!response.IsSuccessStatusCode) return null;
        var result = await response.Content.ReadFromJsonAsync<IdEnvelope>();
        return result?.Id;
    }

    public async Task<bool> RemoveWatchlistItemAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/alerts/watchlist/{id}");
        return response.IsSuccessStatusCode;
    }

    private sealed class IdEnvelope
    {
        public Guid? Id { get; set; }
    }

    // Account (RFP 19.2 PDPL)
    public async Task<AccountProfileResponse?> GetAccountProfileAsync()
    {
        try { return await _http.GetFromJsonAsync<AccountProfileResponse>("api/account/profile"); }
        catch { return null; }
    }

    public async Task<bool> UpdateAccountConsentAsync(bool marketing, bool aiMemory, bool usageAnalytics)
    {
        var response = await _http.PutAsJsonAsync("api/account/consent",
            new { marketing, aiMemory, usageAnalytics });
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAccountProfileAsync(object update)
    {
        var response = await _http.PutAsJsonAsync("api/account/profile", update);
        return response.IsSuccessStatusCode;
    }

    public async Task<(byte[] Content, string FileName)?> DownloadPersonalDataAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/account/data-export");
        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsByteArrayAsync();
        var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                       ?? "IRETP_PersonalData.json";
        return (content, fileName);
    }

    // Auth
    public async Task<LoginResponse?> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new { email, password });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LoginResponse>();
    }

    public async Task<bool> RegisterAsync(string email, string password, string firstName, string lastName)
    {
        var response = await _http.PostAsJsonAsync("api/auth/register",
            new { email, password, firstName, lastName });
        return response.IsSuccessStatusCode;
    }
}

// Simple DTOs for API responses that don't exist in Application layer
public class ZoneHeatmapItem
{
    public Guid ZoneId { get; set; }
    public string Name { get; set; } = "";
    public string? NameAr { get; set; }
    public double CenterLat { get; set; }
    public double CenterLng { get; set; }
    public string? GeoJson { get; set; }
    public int TransactionCount { get; set; }
    public decimal AvgPricePerSqft { get; set; }
    public decimal AvgRentalYield { get; set; }
}

public class ProjectMapPin
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? NameAr { get; set; }
    public string? DeveloperName { get; set; }
    public string? ZoneName { get; set; }
    public int Status { get; set; }
    public decimal CompletionPercentage { get; set; }
    public int TotalUnits { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class PriceIndexResult
{
    public List<PriceIndexEntry> Entries { get; set; } = [];
}

public class PriceIndexEntry
{
    public int Year { get; set; }
    public int Quarter { get; set; }
    public int? Month { get; set; }
    public decimal AveragePricePerSqft { get; set; }
    public int TransactionCount { get; set; }
    public decimal? QuarterlyChange { get; set; }
    public decimal? AnnualChange { get; set; }
    public string? ZoneName { get; set; }
}

public class RentalIndexResult
{
    public List<RentalIndexEntry> DataPoints { get; set; } = [];
    public decimal CurrentAvgRent { get; set; }
    public decimal CurrentAvgYield { get; set; }
}

public class RentalIndexEntry
{
    public int Year { get; set; }
    public int Quarter { get; set; }
    public decimal AverageAnnualRent { get; set; }
    public decimal GrossRentalYield { get; set; }
    public string? ZoneName { get; set; }
    public string? UnitType { get; set; }
}

public class TransactionPageResult
{
    public List<TransactionItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class TransactionItem
{
    public Guid Id { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? ZoneName { get; set; }
    public string? ProjectName { get; set; }
    public string? PropertyType { get; set; }
    public string? TransactionType { get; set; }
    public decimal AreaSqft { get; set; }
    public decimal TransactionValue { get; set; }
    public decimal PricePerSqft { get; set; }
    public bool IsOffPlan { get; set; }
}

public class AnalyticsResultDto
{
    public List<Dictionary<string, object>> DataPoints { get; set; } = [];
    public Dictionary<string, object>? SummaryStats { get; set; }
    public string? RecommendedChartType { get; set; }
}

public class LoginResponse
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public int ExpiresIn { get; set; }
    public LoginUser? User { get; set; }
}

public class LoginUser
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public bool IsInternalUser { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class AiQueryResponse
{
    public string Answer { get; set; } = "";
    public string? ChartConfigJson { get; set; }
    public string? DataJson { get; set; }
    public string SourceCitation { get; set; } = "";
    public string ModelUsed { get; set; } = "";
}

public class CaptchaChallengeDto
{
    public string ChallengeId { get; set; } = "";
    public string Prompt { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public class CaptchaTokenEnvelope
{
    public string? Token { get; set; }
}

public class AccountProfileResponse
{
    public string? Email { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string PreferredLanguage { get; set; } = "en";
    public string PreferredCurrency { get; set; } = "AED";
    public bool ConsentMarketing { get; set; }
    public bool ConsentAiMemory { get; set; }
    public bool ConsentUsageAnalytics { get; set; }
    public DateTime? ConsentUpdatedAt { get; set; }
}

public class NotificationPageResult
{
    public List<NotificationApiItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class NotificationApiItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string TitleAr { get; set; } = "";
    public string Message { get; set; } = "";
    public string MessageAr { get; set; } = "";
    public string? Link { get; set; }
    public string Channel { get; set; } = "InApp";
    public string? Category { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
