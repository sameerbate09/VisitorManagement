using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisitorManagement.Domain.Entities
{
    public class AuditLog
    {
        public long Id { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;   // unique: duplicate-safe processing
        public string EntityName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;      // API / FUNCTION
        public string? Details { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
