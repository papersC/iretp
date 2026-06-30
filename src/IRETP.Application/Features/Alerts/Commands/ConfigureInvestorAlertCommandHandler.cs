using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Alerts.Commands;

public class ConfigureInvestorAlertCommandHandler
    : IRequestHandler<ConfigureInvestorAlertCommand, Guid>
{
    private readonly IRepository<InvestorAlert> _alertRepo;
    private readonly IUnitOfWork _unitOfWork;

    public ConfigureInvestorAlertCommandHandler(
        IRepository<InvestorAlert> alertRepo,
        IUnitOfWork unitOfWork)
    {
        _alertRepo = alertRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(
        ConfigureInvestorAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = new InvestorAlert
        {
            UserId = request.UserId ?? throw new ArgumentException("UserId is required.", nameof(request)),
            AlertType = request.AlertType,
            ZoneId = request.ZoneId,
            DeveloperId = request.DeveloperId,
            ProjectId = request.ProjectId,
            ThresholdValue = request.ThresholdValue,
            ThresholdDirection = request.ThresholdDirection,
            Frequency = request.Frequency,
            IsEmailEnabled = request.IsEmailEnabled,
            IsSmsEnabled = request.IsSmsEnabled,
            IsPushEnabled = request.IsPushEnabled,
            IsActive = true
        };

        await _alertRepo.AddAsync(alert, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return alert.Id;
    }
}
