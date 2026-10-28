namespace VisitorManagement.Application.Abstractions;

public interface ICorrelationIdAccessor
{
    string CorrelationId { get; }
}