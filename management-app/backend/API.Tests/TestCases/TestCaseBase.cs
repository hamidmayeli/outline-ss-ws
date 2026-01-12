namespace OutlineManager.API.Tests.TestCases;

public abstract class TestCaseBase : IAsyncDisposable, IDisposable
{
    protected TestFixture _fixture = new ();

    public void Dispose()
    {
        ((IDisposable)_fixture).Dispose();
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ((IAsyncDisposable)_fixture).DisposeAsync();
    }
}
