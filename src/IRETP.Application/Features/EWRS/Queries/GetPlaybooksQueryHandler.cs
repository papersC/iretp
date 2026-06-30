using System.Text.Json;
using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.EWRS.Queries;

public class GetPlaybooksQueryHandler : IRequestHandler<GetPlaybooksQuery, List<PlaybookDto>>
{
    private readonly IRepository<RiskThreshold> _thresholdRepo;

    public GetPlaybooksQueryHandler(IRepository<RiskThreshold> thresholdRepo)
    {
        _thresholdRepo = thresholdRepo;
    }

    public async Task<List<PlaybookDto>> Handle(GetPlaybooksQuery request, CancellationToken ct)
    {
        var thresholds = await _thresholdRepo.Query()
            .OrderBy(t => t.IndicatorName)
            .ToListAsync(ct);

        return thresholds.Select(t => new PlaybookDto
        {
            ThresholdId = t.Id,
            IndicatorKey = t.IndicatorKey,
            IndicatorName = t.IndicatorName,
            Steps = ParseSteps(t.PlaybookStepsJson)
        }).ToList();
    }

    private static List<string> ParseSteps(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }
}
