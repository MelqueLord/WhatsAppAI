namespace WhatsAppAI.Application.Automation.Policy;

public sealed class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private int _failureCount;
    private DateTime? _lastFailureAt;
    private CircuitState _state = CircuitState.Closed;

    public CircuitBreaker(int failureThreshold = 5, TimeSpan? resetTimeout = null)
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout ?? TimeSpan.FromMinutes(5);
    }

    public CircuitState State
    {
        get
        {
            if (_state == CircuitState.Open && _lastFailureAt.HasValue
                && DateTime.UtcNow - _lastFailureAt.Value > _resetTimeout)
            {
                _state = CircuitState.HalfOpen;
            }
            return _state;
        }
    }

    public bool CanExecute() => State != CircuitState.Open;

    public void RecordSuccess()
    {
        _failureCount = 0;
        _state = CircuitState.Closed;
    }

    public void RecordFailure()
    {
        _failureCount++;
        _lastFailureAt = DateTime.UtcNow;

        if (_failureCount >= _failureThreshold)
            _state = CircuitState.Open;
    }
}

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}
