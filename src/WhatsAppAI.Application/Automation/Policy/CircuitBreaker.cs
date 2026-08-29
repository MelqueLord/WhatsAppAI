namespace WhatsAppAI.Application.Automation.Policy;

public sealed class CircuitBreaker
{
    private readonly Lock _sync = new();
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private int _failureCount;
    private DateTime? _lastFailureAt;
    private CircuitState _state = CircuitState.Closed;
    private bool _halfOpenProbeInFlight;

    public CircuitBreaker(int failureThreshold = 5, TimeSpan? resetTimeout = null)
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout ?? TimeSpan.FromMinutes(5);
    }

    public CircuitState State
    {
        get
        {
            lock (_sync)
            {
                TransitionToHalfOpenIfReady();
                return _state;
            }
        }
    }

    public bool CanExecute()
    {
        lock (_sync)
        {
            TransitionToHalfOpenIfReady();
            if (_state == CircuitState.Open)
                return false;
            if (_state == CircuitState.HalfOpen)
            {
                if (_halfOpenProbeInFlight)
                    return false;

                _halfOpenProbeInFlight = true;
            }

            return true;
        }
    }

    public void RecordSuccess()
    {
        lock (_sync)
        {
            _failureCount = 0;
            _lastFailureAt = null;
            _halfOpenProbeInFlight = false;
            _state = CircuitState.Closed;
        }
    }

    public void RecordFailure()
    {
        lock (_sync)
        {
            _failureCount++;
            _lastFailureAt = DateTime.UtcNow;
            _halfOpenProbeInFlight = false;

            if (_failureCount >= _failureThreshold)
                _state = CircuitState.Open;
        }
    }

    private void TransitionToHalfOpenIfReady()
    {
        if (_state == CircuitState.Open && _lastFailureAt.HasValue &&
            DateTime.UtcNow - _lastFailureAt.Value >= _resetTimeout)
        {
            _state = CircuitState.HalfOpen;
            _halfOpenProbeInFlight = false;
        }
    }
}

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}
