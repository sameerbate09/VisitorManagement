using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisitorManagement.Domain.Enums;

namespace VisitorManagement.Domain.Entities
{
    public class Visitor
    {
        public int Id { get; set; }
        public string VisitorName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime VisitDate { get; set; }          // stored in UTC
        public string? Remarks { get; set; }
        public VisitorStatus Status { get; set; } = VisitorStatus.Scheduled;

        public int HostId { get; set; }
        public Host Host { get; set; } = null!;

        public int PurposeId { get; set; }
        public Purpose Purpose { get; set; } = null!;

        public int CreatedByUserId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int? UpdatedByUserId { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
