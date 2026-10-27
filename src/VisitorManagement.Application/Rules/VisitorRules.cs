using System.Text.RegularExpressions;

namespace VisitorManagement.Application.Rules;

public static class VisitorRules
{
    public const int MaxFutureDays = 365;
    public static readonly TimeSpan PastGrace = TimeSpan.FromMinutes(15);

    private static readonly Regex MobilePattern = new(@"^\+?[0-9][0-9 \-]*$", RegexOptions.Compiled);

    public static bool IsValidMobile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        if (!MobilePattern.IsMatch(trimmed)) return false;
        var digits = trimmed.Count(char.IsDigit);
        return digits is >= 7 and <= 15;
    }

    public static string NormalizeMobile(string value)
    {
        var trimmed = value.Trim();
        var prefix = trimmed.StartsWith('+') ? "+" : string.Empty;
        return prefix + new string(trimmed.Where(char.IsDigit).ToArray());
    }

    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static bool IsNotInPast(DateTime visitUtc, DateTime nowUtc) => visitUtc >= nowUtc - PastGrace;

    public static bool IsNotTooFarAhead(DateTime visitUtc, DateTime nowUtc) =>
        visitUtc <= nowUtc.AddDays(MaxFutureDays);
}