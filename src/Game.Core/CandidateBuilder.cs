using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core;

// Lightweight in-process Spark-style fluent builder for ActionCandidate streams.
// Supports lazy composition of maps, filters and flat-maps.
public sealed class CandidateBuilder
{
    private readonly IEnumerable<ActionCandidate> _source;

    public CandidateBuilder(IEnumerable<ActionCandidate> source)
    {
        _source = source ?? Enumerable.Empty<ActionCandidate>();
    }

    private CandidateBuilder(Func<IEnumerable<ActionCandidate>> factory)
    {
        _source = factory();
    }

    // Start from a sequence of cell actors by flat-mapping their candidate streams
    public static CandidateBuilder FromCells(IEnumerable<CellActor> cells)
    {
        return new CandidateBuilder(() => cells.SelectMany(c => c.GetActionCandidates()));
    }

    // Start from a sequence of cell actors and process each cell's candidate stream
    // using the provided board helpers (isInside/getPieceAt). This preserves per-cell
    // direction semantics (direction blocking, steps ordering) by processing each
    // cell's candidate sequence independently.
    public static CandidateBuilder FromCellActors(IEnumerable<CellActor> cells, Func<int, int, bool> isInside, Func<int, int, object?> getPieceAt)
    {
        return new CandidateBuilder(() => cells.SelectMany(c => ActionCandidates.ProcessCellCandidates(c.GetActionCandidates(), isInside, getPieceAt)));
    }

    // FlatMap: expand each element into many
    public CandidateBuilder FlatMap(Func<ActionCandidate, IEnumerable<ActionCandidate>> projector)
    {
        return new CandidateBuilder(() => _source.SelectMany(projector));
    }

    // Explode is Spark terminology for flatMap/unnest
    public CandidateBuilder Explode(Func<ActionCandidate, IEnumerable<ActionCandidate>> projector) => FlatMap(projector);

    // Where / Filter
    public CandidateBuilder Where(Func<ActionCandidate, bool> predicate)
    {
        return new CandidateBuilder(() => _source.Where(predicate));
    }

    // Map / Select
    public CandidateBuilder Select(Func<ActionCandidate, ActionCandidate> selector)
    {
        return new CandidateBuilder(() => _source.Select(selector));
    }

    // OrderBy / Sort support (in-memory). TKey must be comparable.
    public CandidateBuilder OrderBy<TKey>(Func<ActionCandidate, TKey> keySelector) where TKey : IComparable<TKey>
    {
        return new CandidateBuilder(() => _source.OrderBy(keySelector));
    }

    public CandidateBuilder OrderByDescending<TKey>(Func<ActionCandidate, TKey> keySelector) where TKey : IComparable<TKey>
    {
        return new CandidateBuilder(() => _source.OrderByDescending(keySelector));
    }

    // Alias
    public CandidateBuilder Sort<TKey>(Func<ActionCandidate, TKey> keySelector) where TKey : IComparable<TKey> => OrderBy(keySelector);

    // Apply an IPolicy (if null, leaves stream untouched)
    public CandidateBuilder ApplyPolicy(IPolicy? policy)
    {
        if (policy == null) return this;
        return new CandidateBuilder(() => policy.Apply(_source));
    }

    // Builds the resulting stream (lazy execution)
    public IEnumerable<ActionCandidate> Build() => _source;
}
