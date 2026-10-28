using Microsoft.Extensions.Configuration;

namespace VisitorManagement.Infrastructure.Messaging;

public class QueueOptions
{
    public const string SectionName = "Queue";

    public string ConnectionString { get; set; } = string.Empty;   // secret in Azure: env var / Key Vault
    public string QueueName { get; set; } = string.Empty;

    public static QueueOptions Load(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<QueueOptions>() ?? new QueueOptions();

        if (string.IsNullOrWhiteSpace(options.ConnectionString) || string.IsNullOrWhiteSpace(options.QueueName))
            throw new InvalidOperationException("Queue:ConnectionString and Queue:QueueName must be configured.");

        return options;
    }
}