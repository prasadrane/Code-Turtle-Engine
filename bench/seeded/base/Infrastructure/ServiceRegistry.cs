namespace Shop.Infrastructure;

public enum Lifetime { Singleton, Scoped, Transient }

public sealed record ServiceDescriptor(Type Service, Type Implementation, Lifetime Lifetime);

public sealed class ServiceRegistry
{
    private readonly List<ServiceDescriptor> _descriptors = new();

    public IReadOnlyList<ServiceDescriptor> Descriptors => _descriptors;

    public void AddSingleton<TService, TImpl>() where TImpl : TService => Add<TService, TImpl>(Lifetime.Singleton);
    public void AddScoped<TService, TImpl>() where TImpl : TService => Add<TService, TImpl>(Lifetime.Scoped);
    public void AddTransient<TService, TImpl>() where TImpl : TService => Add<TService, TImpl>(Lifetime.Transient);

    private void Add<TService, TImpl>(Lifetime lifetime) =>
        _descriptors.Add(new ServiceDescriptor(typeof(TService), typeof(TImpl), lifetime));
}
