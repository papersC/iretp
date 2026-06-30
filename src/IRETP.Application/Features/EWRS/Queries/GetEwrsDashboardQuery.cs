using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.EWRS.Queries;

public class GetEwrsDashboardQuery : IRequest<EwrsDashboardDto>;
