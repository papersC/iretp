using IRETP.Domain.Enums;
using MediatR;

namespace IRETP.Application.Features.Export.Commands;

public class ExportTransactionsCommand : IRequest<ExportResult>
{
    public string Format { get; set; } = "excel"; // excel, csv, pdf
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public List<Guid>? ZoneIds { get; set; }
    public List<PropertyType>? PropertyTypes { get; set; }
    public List<TransactionType>? TransactionTypes { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public decimal? AreaMin { get; set; }
    public decimal? AreaMax { get; set; }
    public FinancingMethod? FinancingMethod { get; set; }
}

public class ExportResult
{
    public byte[] FileContent { get; set; } = [];
    public string ContentType { get; set; } = default!;
    public string FileName { get; set; } = default!;
}
