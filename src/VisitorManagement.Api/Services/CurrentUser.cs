using VisitorManagement.Application.Abstractions;

namespace VisitorManagement.Api.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public int UserId
    {
        get
        {
            var sub = _accessor.HttpContext?.User.FindFirst("sub")?.Value;
            return int.TryParse(sub, out var id)
                ? id
                : throw new InvalidOperationException("The authenticated user id is not available.");
        }
    }
}