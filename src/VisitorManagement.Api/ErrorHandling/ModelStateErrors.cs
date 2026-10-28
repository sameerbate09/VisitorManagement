using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace VisitorManagement.Api.ErrorHandling;

public static class ModelStateErrors
{
    private const string Generic = "The value is not in a valid format.";

    public static Dictionary<string, string[]> From(ModelStateDictionary modelState)
    {
        var result = new Dictionary<string, string[]>();

        foreach (var (key, entry) in modelState)
        {
            if (entry is null || entry.Errors.Count == 0) continue;

            var field = NormalizeKey(key);
            var messages = entry.Errors
                .Select(e => e.Exception is not null || string.IsNullOrWhiteSpace(e.ErrorMessage)
                    ? Generic          // parser exceptions can reveal internals, so never pass them on
                    : e.ErrorMessage)
                .Distinct()
                .ToArray();

            result[field] = result.TryGetValue(field, out var existing)
                ? existing.Concat(messages).Distinct().ToArray()
                : messages;
        }

        return result;
    }

    private static string NormalizeKey(string key)
    {
        var trimmed = key.StartsWith("$.") ? key[2..] : key;
        if (string.IsNullOrEmpty(trimmed) || trimmed == "$") return "request";
        return char.ToLowerInvariant(trimmed[0]) + trimmed[1..];
    }
}