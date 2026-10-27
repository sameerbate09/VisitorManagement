namespace VisitorManagement.Api.Authorization;

public static class Policies
{
    public const string CanViewVisitors = "CanViewVisitors";     // Admin + User
    public const string CanManageVisitors = "CanManageVisitors"; // Admin only
}