using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisitorManagement.Domain.Enums
{
    public enum VisitorStatus
    {
        Scheduled = 1,
        CheckedIn = 2,
        Completed = 3,
        Cancelled = 4
    }
}
