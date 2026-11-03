namespace Shared.Errors;

public record ApiError(
    string Code,
    string Message,
    IEnumerable<string>? Details = null
);
