using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class CircuitBreakerTests
{
    [Fact]
    public void OpensAfterFailureThresholdAndRejectsCalls()
    {
        var breaker = new CircuitBreaker(2, TimeSpan.FromMinutes(1));

        Assert.True(breaker.CanExecute());
        breaker.RecordFailure();
        Assert.True(breaker.CanExecute());
        breaker.RecordFailure();

        Assert.Equal(CircuitState.Open, breaker.State);
        Assert.False(breaker.CanExecute());
    }

    [Fact]
    public void SuccessResetsFailuresAndClosesCircuit()
    {
        var breaker = new CircuitBreaker(2, TimeSpan.FromMinutes(1));

        breaker.RecordFailure();
        breaker.RecordSuccess();
        breaker.RecordFailure();

        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.True(breaker.CanExecute());
    }

    [Fact]
    public async Task OpensAgainWhenHalfOpenProbeFails()
    {
        var breaker = new CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

        breaker.RecordFailure();
        await Task.Delay(10);

        Assert.True(breaker.CanExecute());
        Assert.Equal(CircuitState.HalfOpen, breaker.State);
        breaker.RecordFailure();

        Assert.Equal(CircuitState.Open, breaker.State);
        Assert.False(breaker.CanExecute());
    }
}
