using System.Security.Cryptography;
using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.CMS.Commands;

/// <summary>
/// Issues a shareable preview URL for a draft version so DLD senior management
/// can review content before it is published (RFP FR002). The token is a
/// 192-bit URL-safe random string — not a signed JWT — because the value is
/// stored on the version row itself and validated server-side.
/// </summary>
public class CreatePreviewLinkCommandHandler
    : IRequestHandler<CreatePreviewLinkCommand, CmsPreviewLinkDto?>
{
    private readonly IRepository<CmsContentVersion> _versionRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePreviewLinkCommandHandler(
        IRepository<CmsContentVersion> versionRepo, IUnitOfWork unitOfWork)
    {
        _versionRepo = versionRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<CmsPreviewLinkDto?> Handle(
        CreatePreviewLinkCommand request, CancellationToken cancellationToken)
    {
        var version = await _versionRepo.GetByIdAsync(request.VersionId, cancellationToken);
        if (version is null) return null;

        var token = GenerateToken();
        var expiresAt = DateTime.UtcNow.AddHours(Math.Clamp(request.TtlHours, 1, 24 * 14));

        version.PreviewToken = token;
        version.PreviewTokenExpiresAt = expiresAt;
        version.UpdatedAt = DateTime.UtcNow;
        version.UpdatedBy = request.UserId;

        _versionRepo.Update(version);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CmsPreviewLinkDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            VersionNumber = version.VersionNumber
        };
    }

    private static string GenerateToken()
    {
        Span<byte> buffer = stackalloc byte[24]; // 192 bits
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
