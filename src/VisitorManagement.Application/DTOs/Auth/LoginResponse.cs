using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisitorManagement.Application.DTOs.Auth
{
    public record LoginResponse(
    string Token,
    DateTime ExpiresAtUtc,
    string Username,
    string DisplayName,
    string Role);
}
