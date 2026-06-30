using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.Analytics.Commands;

public class SaveAnalyticsViewCommandHandler : IRequestHandler<SaveAnalyticsViewCommand, Guid>
{
    /// <summary>
    /// RFP AN003 caps a personal dashboard at 12 saved views. Enforced
    /// server-side so a curl client can't bypass the UI's drag-and-drop grid
    /// constraints.
    /// </summary>
    public const int MaxSavedViewsPerUser = 12;

    /// <summary>
    /// RFP AN-006: shareable analysis links must remain valid for a minimum
    /// of 12 months. Stamped when a view is made public.
    /// </summary>
    public static readonly TimeSpan ShareTokenLifetime = TimeSpan.FromDays(365);

    private readonly IRepository<SavedAnalyticsView> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SaveAnalyticsViewCommandHandler(
        IRepository<SavedAnalyticsView> repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(SaveAnalyticsViewCommand request, CancellationToken cancellationToken)
    {
        var userId = request.UserId ?? throw new ArgumentException("UserId is required.", nameof(request));

        var existingCount = await _repository.Query()
            .Where(v => v.UserId == userId)
            .CountAsync(cancellationToken);

        if (existingCount >= MaxSavedViewsPerUser)
        {
            throw new InvalidOperationException(
                $"Saved views are capped at {MaxSavedViewsPerUser} per user (RFP AN003). " +
                "Delete an existing view before adding another.");
        }

        var now = DateTime.UtcNow;
        var view = new SavedAnalyticsView
        {
            UserId = userId,
            Name = request.Name,
            ConfigurationJson = request.ConfigurationJson,
            IsPublic = request.IsPublic,
            ShareToken = request.IsPublic ? Guid.NewGuid().ToString("N") : null,
            ShareTokenExpiresAt = request.IsPublic ? now.Add(ShareTokenLifetime) : null,
            DisplayOrder = request.DisplayOrder
        };

        await _repository.AddAsync(view, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return view.Id;
    }
}
