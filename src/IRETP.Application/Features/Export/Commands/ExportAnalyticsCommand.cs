using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Export.Commands;

/// <summary>
/// Export a slice-and-dice analytics result (RFP AN004). The four mandatory
/// formats are Excel (.xlsx) with a summary statistics sheet, CSV (raw data),
/// PDF (formatted report with DLD letterhead and an embedded bar chart of the
/// first metric per dimension bucket), and JSON (for API consumers).
/// </summary>
public class ExportAnalyticsCommand : IRequest<ExportResult>
{
    public string Format { get; set; } = "excel"; // excel | csv | pdf | json
    public AnalyticsQueryRequest Request { get; set; } = new();
}
