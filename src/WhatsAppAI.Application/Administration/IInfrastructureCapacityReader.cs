namespace WhatsAppAI.Application.Administration;

public interface IInfrastructureCapacityReader
{
    Task<InfrastructureCapacityCounts> GetCountsAsync(
        CancellationToken cancellationToken = default);
}

public sealed record InfrastructureCapacityCounts(
    int Customers,
    int Lines,
    int Operators);
