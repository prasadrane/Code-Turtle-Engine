namespace SampleRepo;

public interface IEngine
{
    void Run();
}

public class TransientWorker : IEngine
{
    public void Run() { }
}

// Constructor-injected collaborator captured in a field (Idiomatic Architect judges lifetime).
public class SingletonOrchestrator
{
    private readonly TransientWorker _worker;

    public SingletonOrchestrator(TransientWorker worker)
    {
        _worker = worker;
    }

    public void RunAll() => _worker.Run();
}
