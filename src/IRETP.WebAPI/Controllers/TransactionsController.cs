using IRETP.Application.Features.Export.Commands;
using IRETP.Application.Features.Transactions.Queries;
using IRETP.Domain.Enums;
using IRETP.WebAPI.Middleware;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TransactionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Search and filter real-estate transactions.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] List<Guid>? zoneIds,
        [FromQuery] List<PropertyType>? propertyTypes,
        [FromQuery] List<TransactionType>? transactionTypes,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] decimal? priceMin,
        [FromQuery] decimal? priceMax,
        [FromQuery] decimal? areaMin,
        [FromQuery] decimal? areaMax,
        [FromQuery] FinancingMethod? financingMethod,
        [FromQuery] string? sortBy,
        [FromQuery] bool sortDesc = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var query = new GetTransactionsQuery
        {
            ZoneIds = zoneIds,
            PropertyTypes = propertyTypes,
            TransactionTypes = transactionTypes,
            DateFrom = dateFrom,
            DateTo = dateTo,
            PriceMin = priceMin,
            PriceMax = priceMax,
            AreaMin = areaMin,
            AreaMax = areaMax,
            FinancingMethod = financingMethod,
            SortBy = sortBy,
            SortDesc = sortDesc,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Export transactions in the specified format (excel, csv, pdf).
    /// Anonymous users must attach a CAPTCHA token (RFP 10.3).
    /// </summary>
    [HttpGet("export/{format}")]
    [CaptchaRequired]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportTransactions(
        string format,
        [FromQuery] List<Guid>? zoneIds,
        [FromQuery] List<PropertyType>? propertyTypes,
        [FromQuery] List<TransactionType>? transactionTypes,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] decimal? priceMin,
        [FromQuery] decimal? priceMax,
        [FromQuery] decimal? areaMin,
        [FromQuery] decimal? areaMax,
        [FromQuery] FinancingMethod? financingMethod,
        CancellationToken ct = default)
    {
        var allowedFormats = new[] { "excel", "csv", "pdf" };
        if (!allowedFormats.Contains(format.ToLowerInvariant()))
            return BadRequest(new { message = $"Unsupported format '{format}'. Allowed: excel, csv, pdf." });

        var command = new ExportTransactionsCommand
        {
            Format = format.ToLowerInvariant(),
            ZoneIds = zoneIds,
            PropertyTypes = propertyTypes,
            TransactionTypes = transactionTypes,
            DateFrom = dateFrom,
            DateTo = dateTo,
            PriceMin = priceMin,
            PriceMax = priceMax,
            AreaMin = areaMin,
            AreaMax = areaMax,
            FinancingMethod = financingMethod
        };

        var result = await _mediator.Send(command, ct);
        return File(result.FileContent, result.ContentType, result.FileName);
    }
}
