namespace VisitorManagement.IntegrationTests.Support;

/// <summary>One shared API instance. Tests in the collection run one after another.</summary>
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}