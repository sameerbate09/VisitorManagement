using Microsoft.Extensions.Configuration;

namespace VisitorManagement.Infrastructure.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;   
    public int ExpiryMinutes { get; set; } = 60;

    /// <summary>Reads the section and fails fast if it is incomplete.</summary>
    public static JwtOptions Load(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(options.SigningKey) || options.SigningKey.Length < 32)
            throw new InvalidOperationException(
                "Jwt:SigningKey is missing or shorter than 32 characters. " +
                "Set it with user secrets or the Jwt__SigningKey environment variable.");

        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be configured.");

        if (options.ExpiryMinutes <= 0)
            throw new InvalidOperationException("Jwt:ExpiryMinutes must be greater than zero.");

        return options;
    }
}