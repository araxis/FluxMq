using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace FluxMq.Cli;

internal sealed class CliTypeRegistrar : ITypeRegistrar, IDisposable
{
    private readonly IServiceCollection _services;

    public CliTypeRegistrar(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public ITypeResolver Build()
    {
        return new CliTypeResolver(_services.BuildServiceProvider());
    }

    public void Register(Type service, Type implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        _services.AddSingleton(service, _ => factory());
    }

    public void Dispose()
    {
    }

    private sealed class CliTypeResolver : ITypeResolver, IDisposable
    {
        private readonly ServiceProvider _provider;

        public CliTypeResolver(ServiceProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public object? Resolve(Type? type)
        {
            return type is null ? null : _provider.GetService(type);
        }

        public void Dispose()
        {
            _provider.Dispose();
        }
    }
}
