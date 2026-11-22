using AAALogistics.Application.Exports;
using AAALogistics.Application.Logistics;
using AAALogistics.Contracts;
using AAALogistics.Domain.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace AAALogistics.Api.Hosting;

internal static class ExportEndpointRouteBuilderExtensions
{
    private const string SpreadsheetContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const long MaxImportRequestBytes = 6L * 1024 * 1024;

    public static RouteGroupBuilder MapExportEndpoints(
        this RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/exports/materials.xlsx",
                async (
                    ILogisticsService logistics,
                    IExcelExportService exports,
                    CancellationToken cancellationToken) =>
                    Results.File(
                        exports.CreateMaterialsWorkbook(
                            await logistics.ListMaterialsAsync(cancellationToken)),
                        SpreadsheetContentType,
                        "materials.xlsx"))
            .RequireAuthorization(AppPermissions.MaterialManage)
            .WithName("ExportMaterials");

        endpoints.MapGet(
                "/exports/stock.xlsx",
                async (
                    ILogisticsService logistics,
                    IExcelExportService exports,
                    CancellationToken cancellationToken) =>
                    Results.File(
                        exports.CreateStockWorkbook(
                            await logistics.GetStockAsync(cancellationToken)),
                        SpreadsheetContentType,
                        "stock.xlsx"))
            .RequireAuthorization(AppPermissions.StockRead)
            .WithName("ExportStock");

        endpoints.MapPost(
                "/imports/materials/preview",
                (
                    IFormFile file,
                    IExcelImportService imports,
                    CancellationToken cancellationToken) =>
                    ImportAsync(file, stream => imports.PreviewMaterialsAsync(stream, cancellationToken)))
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxImportRequestBytes))
            .RequireAuthorization(AppPermissions.MaterialManage)
            .WithName("ImportMaterialsPreview");

        endpoints.MapPost(
                "/imports/materials/commit",
                (
                    IFormFile file,
                    IExcelImportService imports,
                    CancellationToken cancellationToken) =>
                    ImportAsync(file, stream => imports.CommitMaterialsAsync(stream, cancellationToken)))
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxImportRequestBytes))
            .RequireAuthorization(AppPermissions.MaterialManage)
            .WithName("ImportMaterialsCommit");

        endpoints.MapPost(
                "/admin/table-edit/evaluate",
                (AdminTableEditRequest request) =>
                {
                    var (allowed, message) = AdminTableBlacklist.EvaluateEdit(
                        request.TableName,
                        request.Values.Keys);
                    return allowed
                        ? Results.Ok(new { allowed, message })
                        : Results.Problem(
                            statusCode: StatusCodes.Status403Forbidden,
                            title: message,
                            type: "https://errors.aaalogistics.local/AAA-ADMIN-0001",
                            extensions: new Dictionary<string, object?>
                            {
                                ["code"] = "AAA-ADMIN-0001",
                            });
                })
            .RequireAuthorization(AppPermissions.AdminTableEdit)
            .WithName("AdminTableEditEvaluate");

        return endpoints;
    }

    private static async Task<IResult> ImportAsync<T>(IFormFile file, Func<Stream, Task<T>> import)
    {
        if (file.Length == 0
            || !string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return ApiProblems.Problem(
                StatusCodes.Status400BadRequest,
                "Seleccione un libro Excel (.xlsx) con datos.",
                "AAA-IMPORT-0005");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            return Results.Ok(await import(stream));
        }
        catch (LogisticsException exception)
        {
            return ApiProblems.Logistics(exception);
        }
        catch (ArgumentException exception)
        {
            return ApiProblems.Validation(exception);
        }
        catch (InvalidOperationException exception)
        {
            return ApiProblems.Business(exception);
        }
    }
}
