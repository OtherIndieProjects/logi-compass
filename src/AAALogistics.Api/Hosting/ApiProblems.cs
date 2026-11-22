using AAALogistics.Application.Logistics;
using Microsoft.AspNetCore.Http;

namespace AAALogistics.Api.Hosting;

internal static class ApiProblems
{
    public const string ValidationCode = "AAA-VALIDATION-0001";
    public const string BusinessCode = "AAA-BUSINESS-0001";

    private const string GenericValidationMessage = "Valor no válido o fuera del rango permitido.";

    private const string GenericBusinessMessage =
        "No se pudo completar la operación con el estado actual de los datos. Actualice e inténtelo de nuevo.";

    public static IResult Problem(int statusCode, string title, string code) =>
        Results.Problem(
            statusCode: statusCode,
            title: title,
            type: $"https://errors.aaalogistics.local/{code}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
            });

    public static IResult Logistics(LogisticsException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Problem(
            exception.Kind switch
            {
                LogisticsErrorKind.NotFound => StatusCodes.Status404NotFound,
                LogisticsErrorKind.Conflict => StatusCodes.Status409Conflict,
                LogisticsErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest,
            },
            exception.Message,
            exception.Code);
    }

    public static IResult Validation(ArgumentException exception) =>
        Problem(
            StatusCodes.Status400BadRequest,
            ValidationMessage(exception),
            ValidationCode);

    public static IResult Business(InvalidOperationException exception) =>
        Problem(
            StatusCodes.Status409Conflict,
            BusinessMessage(exception),
            BusinessCode);

    public static string ValidationMessage(ArgumentException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var message = exception.Message;
        var parameter = exception.ParamName;
        if (!string.IsNullOrEmpty(parameter))
        {
            var suffix = $" (Parameter '{parameter}')";
            var index = message.IndexOf(suffix, StringComparison.Ordinal);
            if (index >= 0)
            {
                message = message[..index];
            }
        }

        // Runtime default texts are English; never surface them to the Spanish UI.
        if (!ThrownByApplicationCode(exception)
            || message.StartsWith("Specified argument", StringComparison.Ordinal)
            || message.StartsWith("Value does not fall", StringComparison.Ordinal)
            || message.StartsWith("Value cannot be null", StringComparison.Ordinal))
        {
            message = GenericValidationMessage;
        }

        return string.IsNullOrEmpty(parameter)
            ? message
            : $"{message} (campo: {parameter})";
    }

    /// <summary>
    /// Business rules throw <see cref="InvalidOperationException"/> with Spanish text; framework
    /// ones (EF Core, LINQ) carry English internals that must not reach the client.
    /// </summary>
    public static string BusinessMessage(InvalidOperationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return ThrownByApplicationCode(exception) ? exception.Message : GenericBusinessMessage;
    }

    private static bool ThrownByApplicationCode(Exception exception) =>
        exception.TargetSite?.DeclaringType?.Assembly.GetName().Name?
            .StartsWith("AAALogistics.", StringComparison.Ordinal) == true;
}
