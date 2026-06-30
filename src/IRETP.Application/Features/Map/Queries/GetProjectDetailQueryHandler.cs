using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.Map.Queries;

public class GetProjectDetailQueryHandler : IRequestHandler<GetProjectDetailQuery, ProjectDetailDto?>
{
    private readonly IRepository<Project> _projectRepo;

    public GetProjectDetailQueryHandler(IRepository<Project> projectRepo)
    {
        _projectRepo = projectRepo;
    }

    public async Task<ProjectDetailDto?> Handle(GetProjectDetailQuery request, CancellationToken cancellationToken)
    {
        var project = await _projectRepo.Query()
            .Include(p => p.Developer)
            .Include(p => p.Zone)
            .Include(p => p.Units)
            .Include(p => p.EscrowAccount)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (project == null) return null;

        return new ProjectDetailDto
        {
            Id = project.Id,
            Name = project.Name,
            NameAr = project.NameAr,
            DeveloperName = project.Developer.Name,
            DeveloperNameAr = project.Developer.NameAr,
            DeveloperId = project.DeveloperId,
            ZoneName = project.Zone.Name,
            ZoneId = project.ZoneId,
            Status = project.Status,
            CompletionPercentage = project.CompletionPercentage,
            TotalUnits = project.TotalUnits,
            SoldUnits = project.SoldUnits,
            AvailableUnits = project.AvailableUnits,
            ExpectedDeliveryDate = project.ExpectedDeliveryDate,
            ActualDeliveryDate = project.ActualDeliveryDate,
            Latitude = project.Latitude,
            Longitude = project.Longitude,
            DldRegistrationNumber = project.DldRegistrationNumber,
            TotalProjectCost = project.TotalProjectCost,
            Units = project.Units.Select(u => new ProjectUnitDto
            {
                PropertyType = u.PropertyType,
                Count = u.Count,
                AveragePrice = u.AveragePrice,
                AverageSizeSqft = u.AverageSizeSqft
            }).ToList(),
            EscrowSummary = project.EscrowAccount != null
                ? new EscrowSummaryDto
                {
                    AccountNumber = project.EscrowAccount.AccountNumber,
                    BankName = project.EscrowAccount.BankName,
                    CurrentBalance = project.EscrowAccount.CurrentBalance,
                    RequiredMinimumBalance = project.EscrowAccount.RequiredMinimumBalance,
                    TotalFundsReceived = project.EscrowAccount.TotalFundsReceived,
                    TotalAuthorisedWithdrawals = project.EscrowAccount.TotalAuthorisedWithdrawals,
                    RemainingConstructionCost = project.EscrowAccount.RemainingConstructionCost,
                    AdequacyRatio = project.EscrowAccount.AdequacyRatio,
                    Status = project.EscrowAccount.Status
                }
                : null
        };
    }
}
