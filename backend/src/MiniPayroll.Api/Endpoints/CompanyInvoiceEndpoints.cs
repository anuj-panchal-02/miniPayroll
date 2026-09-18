using MiniPayroll.Api.Auth;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Endpoints;

public static class CompanyInvoiceEndpoints
{
    public static IEndpointRouteBuilder MapCompanyInvoiceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/company/invoices")
            .RequireAuthorization(CompanySetupAuthorization.Configure);
        group.MapGet("/", List);
        group.MapGet("/{invoiceId:guid}", Get);
        return routes;
    }

    private static async Task<IResult> List(
        InvoiceService invoices,
        CancellationToken cancellationToken)
    {
        var result = await invoices.ListOwnAsync(cancellationToken);
        return result.Status == InvoiceCommandStatus.Success
            ? Results.Ok(result.Invoices)
            : InvoiceError(result.Status);
    }

    private static async Task<IResult> Get(
        Guid invoiceId,
        InvoiceService invoices,
        CancellationToken cancellationToken)
    {
        var result = await invoices.GetOwnAsync(invoiceId, cancellationToken);
        return result.Status == InvoiceCommandStatus.Success
            ? Results.Ok(result.Invoice)
            : InvoiceError(result.Status);
    }

    private static IResult InvoiceError(InvoiceCommandStatus status) =>
        Results.Json(
            new { error = status switch
            {
                InvoiceCommandStatus.Forbidden => "You are not allowed to view invoices.",
                InvoiceCommandStatus.CompanyNotFound or InvoiceCommandStatus.NotFound =>
                    "The invoice was not found.",
                _ => "The invoice request could not be completed."
            }},
            statusCode: InvoiceHttpStatus.For(status));
}
