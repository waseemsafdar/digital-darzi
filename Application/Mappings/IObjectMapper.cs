namespace Application.Mappings;

/// <summary>
/// Minimal explicit mapper used by the generic CRUD services.
/// Replaces AutoMapper (14.x carries advisory GHSA-rvv3-g6hj-g44x and the patched line is
/// commercially licensed). Every mapping is a hand-written delegate in <see cref="MappingRegistry"/>,
/// so an unmapped pair fails fast instead of silently copying the wrong members.
/// </summary>
public interface IObjectMapper
{
    /// <summary>Creates a new <typeparamref name="TDest"/> from <paramref name="source"/>.</summary>
    TDest Map<TDest>(object source);

    /// <summary>Copies <paramref name="source"/> onto an existing <paramref name="destination"/> (partial update semantics).</summary>
    void Map<TSource, TDest>(TSource source, TDest destination) where TSource : notnull where TDest : notnull;
}

public sealed class ObjectMapper : IObjectMapper
{
    private readonly MappingRegistry _registry;

    public ObjectMapper(MappingRegistry registry) => _registry = registry;

    public TDest Map<TDest>(object source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var create = _registry.FindCreate(source.GetType(), typeof(TDest))
            ?? throw new InvalidOperationException($"No mapping registered from {source.GetType().Name} to {typeof(TDest).Name}.");
        return (TDest)create(source);
    }

    public void Map<TSource, TDest>(TSource source, TDest destination) where TSource : notnull where TDest : notnull
    {
        var apply = _registry.FindApply(source.GetType(), destination.GetType())
            ?? throw new InvalidOperationException($"No update mapping registered from {source.GetType().Name} to {destination.GetType().Name}.");
        apply(source, destination);
    }
}
