# Plan: Pure Lazy DataFrame Refactoring with Validity Propagation

## Core Insight

Instead of:
```csharp
for each depth:
  if (frontier.IsEmpty()) break  // ❌ Materializes
  compute next moves
```

Do:
```csharp
allMoves = union of all depths with validity flags
filter by validity at the end  // ✅ Single materialization
```

## The Problem We're Solving

The current architecture materializes during iteration to make control-flow decisions (IsEmpty checks, collecting Out flags). We can eliminate this by **propagating validity as data** through the DataFrame graph, deferring all decisions to the final materialization point.

## Refactoring Steps

### Step 1: Replace iterative sequenced moves with depth-tagged union

Change `ComputeSequencedMoves` to pre-generate all possible depths (0 to maxDepth) as a single unioned DataFrame with `depth` column, then use window functions to mark which moves are valid based on whether their parent depth had valid moves, eliminating the `IsEmpty()` loop guard.

**Current approach:**
```csharp
for (int depth = 0; depth < maxDepth && !IsEmpty(currentEntryMoves); depth++)
{
    // Collect Out flags - MATERIALIZATION
    var outFlagsRows = currentEntryMoves.Select(...).Distinct().Collect();
    
    // Compute next moves
    var nextPerspectives = ComputeNextPerspectivesFromMoves(...);
    var continuationMoves = ComputeNextCandidatesInternal(...);
    
    currentEntryMoves = nextEntryMoves;
}
```

**New approach:**
```csharp
// Generate all possible depth levels as a DataFrame
var depths = spark.Range(0, maxDepth).Select(Lit(...).Alias("depth"));

// Cross-join entry patterns with depths to create all possible expansions
var allPotentialMoves = entryPatternsDf
    .CrossJoin(depths)
    .WithColumn("parent_depth", Col("depth") - 1);

// Use window functions to propagate validity from parent to child
var validityWindow = Window.PartitionBy("src_x", "src_y", "variant").OrderBy("depth");
var movesWithValidity = allPotentialMoves
    .WithColumn("parent_was_valid", 
        Coalesce(Lag(Col("is_valid"), 1).Over(validityWindow), Lit(true)))
    .WithColumn("is_valid", 
        When(Col("depth") == 0, Lit(true))
        .Otherwise(Col("parent_was_valid") & Col("dst_generic_piece").Contains(Lit("Empty"))));

// Final filter - single materialization point
return movesWithValidity.Filter(Col("is_valid") & Col("is_public"));
```

### Step 2: Propagate validity flags instead of filtering early

Add `is_valid` column to moves throughout the pipeline: entry moves start valid, continuation moves inherit validity from their entry move parent (via join on src position + direction variant), pin detection adds `leaves_king_safe` column, final filter is `WHERE is_valid AND leaves_king_safe`.

**Key insight:** Instead of filtering invalid moves early (which requires materialization to know what's invalid), tag everything with validity and let the query optimizer push down the final filter.

```csharp
// Entry moves start as valid
var entryMoves = ComputeNextCandidatesInternal(...)
    .WithColumn("is_valid_entry", Lit(true))
    .WithColumn("move_type", Lit("entry"));

// Continuation moves inherit from entry
var continuationMoves = entryMoves
    .Join(continuationPatternsDf, joinCondition)
    .WithColumn("is_valid_continuation", 
        Col("is_valid_entry") & Col("dst_conditions_met"))
    .WithColumn("move_type", Lit("continuation"));

// Union and propagate
var allMoves = entryMoves.Union(continuationMoves)
    .WithColumn("is_valid", 
        When(Col("move_type") == "entry", Col("is_valid_entry"))
        .Otherwise(Col("is_valid_continuation")));
```

### Step 3: Replace pin detection with lazy simulation graph

Create a cross-product DataFrame of `(candidate_move_id, simulated_king_position)` by joining candidates with a simulated board state (using withColumn transformations for src→empty, dst→piece), join with threat patterns to get `(move_id, is_king_threatened)`, anti-join back to candidates - all lazy until final `SELECT move_id WHERE NOT is_king_threatened`.

**Current approach (geometric):**
```csharp
// CrossJoin with opponent sliding pieces - MATERIALIZATION
var opponentSlidingPieces = perspectivesDf.Filter(...).Collect();

// Geometric collinearity checks
var pinCheck = candidates.CrossJoin(opponentSlidingPieces)
    .WithColumn("is_on_same_file", ...) // Complex geometric logic
    .WithColumn("is_on_same_diagonal", ...);
```

**New approach (simulation-based):**
```csharp
// Tag each candidate with unique ID (lazy)
var candidatesWithId = candidates
    .WithColumn("move_id", MonotonicallyIncreasingId());

// Simulate board for ALL candidates in one DataFrame (lazy cross-product)
var simulatedBoards = candidatesWithId
    .Join(perspectivesDf.Alias("board"), Col("board.perspective_x") == Col("perspective_x"))
    .WithColumn("simulated_piece",
        When(Col("board.x") == Col("src_x") & Col("board.y") == Col("src_y"), 
            Lit((int)Piece.Empty))  // Source becomes empty
        .When(Col("board.x") == Col("dst_x") & Col("board.y") == Col("dst_y"), 
            Col("src_piece"))  // Destination gets piece
        .Otherwise(Col("board.piece")));  // Others unchanged

// Compute threats on simulated boards (lazy)
var threatsPerMove = simulatedBoards
    .Join(threatPatternsDf, threatJoinCondition)
    .GroupBy("move_id")
    .Agg(Collect_Set("threatened_cell").Alias("threatened_cells"));

// Check king safety (lazy)
var movesWithSafety = candidatesWithId
    .Join(threatsPerMove, "move_id", "left")
    .WithColumn("leaves_king_safe", 
        Not(Array_Contains(Col("threatened_cells"), Col("king_position"))));

// Final filter - SINGLE MATERIALIZATION
return movesWithSafety
    .Filter(Col("leaves_king_safe"))
    .Drop("move_id", "threatened_cells", "leaves_king_safe");
```

### Step 4: Consolidate timeline with timestep-tagged union

Replace `BuildTimeline`'s loop with: generate perspectives for timestep 0..maxDepth in parallel via cross-join with literal timestep values, compute candidates for each timestep with join condition `WHERE t.timestep = candidates.parent_timestep + 1`, union all timesteps, filter at query time `WHERE timestep = desired` - defers materialization to the point of consumption.

**Current approach:**
```csharp
var timelineDf = perspectivesWithThreats.WithColumn("timestep", Lit(0));

for (int depth = 1; depth <= maxDepth; depth++)
{
    var currentPerspectives = timelineDf.Filter(Col("timestep") == depth - 1);  // ❌
    var candidatesDf = ComputeNextCandidates(...);
    var nextPerspectivesDf = SimulateBoardAfterMove(...);
    
    timelineDf = timelineDf.Union(nextPerspectivesWithThreats);
}
```

**New approach:**
```csharp
// Generate all timesteps as a DataFrame
var timesteps = spark.Range(0, maxDepth + 1)
    .Select(Col("id").Alias("timestep"));

// Create initial state at timestep 0
var initialState = perspectivesWithThreats
    .WithColumn("timestep", Lit(0))
    .WithColumn("parent_timestep", Lit(-1));

// Generate all possible moves for all timesteps (lazy cross-join)
var allTimestepMoves = timesteps
    .Filter(Col("timestep") > 0)
    .Join(initialState, Col("parent_timestep") == Col("timestep") - 1)
    .Join(patternsDf, patternJoinCondition)
    .WithColumn("next_timestep", Col("timestep"))
    .WithColumn("is_valid_for_timestep", 
        // Validity conditions based on parent state
    );

// Union all states
var fullTimeline = initialState
    .Union(allTimestepMoves.WithColumnRenamed("next_timestep", "timestep"))
    .Cache();  // Cache the plan, not the data

// Query time filtering - SINGLE MATERIALIZATION per query
return fullTimeline.Filter(Col("timestep") == desiredTimestep);
```

### Step 5: Eliminate Collect() for Out flags via bitwise aggregation

Replace `Collect().foreach { outFlags |= row }` with DataFrame aggregation: `entryMoves.agg(bit_or(sequence & OutMask)).alias("active_flags")`, use this aggregated column directly in join conditions for continuation patterns without materializing rows.

**Current approach:**
```csharp
// MATERIALIZATION to aggregate flags
var outFlagsRows = currentEntryMoves
    .Select(Col("sequence").BitwiseAND(Lit(outMask | variantMask)).Alias("out_flags"))
    .Distinct()
    .Collect();

int activeOutFlags = 0;
foreach (var row in outFlagsRows)
{
    activeOutFlags |= row.GetAs<int>("out_flags");
}

var activeSequence = (Sequence)activeOutFlags;
```

**New approach:**
```csharp
// Aggregate flags as a DataFrame column (lazy)
var activeFlagsDF = currentEntryMoves
    .Agg(
        // Bitwise OR aggregation (may need UDF if not available)
        Max(Col("sequence").BitwiseAND(Lit(outMask | variantMask))).Alias("active_out_flags")
    );

// Use the aggregated flags in joins (lazy)
var continuationMoves = nextPerspectives
    .CrossJoin(activeFlagsDF)  // Broadcast join (1 row)
    .Join(continuationPatternsDf, 
        (Col("sequence").BitwiseAND(Lit(inMask)).BitwiseAND(Col("active_out_flags") >> 1))
        .EqualTo(Col("sequence").BitwiseAND(Lit(inMask))));
```

## Further Considerations

### 1. Memory vs materialization tradeoff

**Question:** The unioned DataFrame of all depths/timesteps will be larger in the query plan but never fully materialized. Does Spark's query optimizer handle large unions efficiently, or does it eventually spill to disk? We may need to add `.coalesce()` or `.repartition()` hints.

**Mitigation strategies:**
- Use `.coalesce(numPartitions)` to reduce shuffle partitions
- Add `.persist(StorageLevel.MEMORY_AND_DISK)` on the plan itself (not data)
- Use broadcast joins for small DataFrames (patterns, depths)
- Add explicit `.repartition()` on high-cardinality keys before window operations

### 2. Window functions for parent-child validity

**Question:** For marking moves valid based on parent depth validity, should we use `lag() OVER (PARTITION BY src_x, src_y, variant ORDER BY depth)` or explicit self-joins? Self-joins are more explicit but create larger query plans.

**Recommendation:** Start with window functions (simpler code), fall back to self-joins if window functions cause performance issues. Window functions are typically optimized well by Spark.

**Window approach:**
```csharp
var validityWindow = Window
    .PartitionBy("src_x", "src_y", "variant_flags")
    .OrderBy("depth");

movesWithValidity = moves
    .WithColumn("parent_had_valid_move", 
        Coalesce(Lag("is_valid", 1).Over(validityWindow), Lit(true)));
```

**Self-join approach:**
```csharp
movesWithValidity = moves.Alias("child")
    .Join(moves.Alias("parent"),
        (Col("child.src_x") == Col("parent.dst_x")) &
        (Col("child.src_y") == Col("parent.dst_y")) &
        (Col("child.depth") == Col("parent.depth") + 1),
        "left_outer")
    .WithColumn("parent_had_valid_move", 
        Coalesce(Col("parent.is_valid"), Lit(true)));
```

### 3. Simulated board representation

**Question:** For pin detection without geometric checks, we could represent the simulated board as a DataFrame with `(move_id, x, y, piece)` rows. This creates a cross-product of `#moves × 64 cells`, but it's lazy. Is this acceptable, or should we use a more compact representation like only storing "deltas" (src_empty, dst_filled)?

**Analysis:**
- **Full board per move:** `#candidates × 64 = ~200 × 64 = 12,800 rows` in query plan
- **Delta representation:** `#candidates × 2 = ~400 rows` (only changed cells)
- **Memory impact:** Lazy means these are just plan nodes, not materialized data

**Recommendation:** Use delta representation initially:
```csharp
// Only track changed cells
var boardDeltas = candidates
    .WithColumn("move_id", MonotonicallyIncreasingId())
    .SelectExpr(
        "move_id",
        "array(struct(src_x, src_y, 'Empty' as piece), " +
        "      struct(dst_x, dst_y, src_piece as piece)) as deltas"
    )
    .WithColumn("delta", Explode(Col("deltas")));

// Join with original board and apply deltas
var simulatedBoard = perspectivesDf
    .Join(boardDeltas, joinCondition)
    .WithColumn("effective_piece",
        Coalesce(Col("delta.piece"), Col("original_piece")));
```

## Implementation Order

1. **Start with Step 5** (bitwise aggregation) - smallest change, proves the concept
2. **Then Step 2** (validity propagation) - foundational for other steps
3. **Then Step 3** (pin detection) - most complex, highest impact
4. **Then Step 1** (sequenced moves) - builds on validity propagation
5. **Finally Step 4** (timeline) - integrates everything

## Success Criteria

- ✅ Zero `Collect()` or `IsEmpty()` calls in hot paths
- ✅ All tests pass (83 Fast tests)
- ✅ Memory usage does not exceed current implementation
- ✅ Performance comparable or better (target: < 2 minutes for Fast tests)
- ✅ Code is more declarative and easier to reason about

## Risks and Mitigation

| Risk | Mitigation |
|------|------------|
| Query plan explosion (too many unions/joins) | Monitor with `.explain()`, add `.coalesce()` hints |
| Out of memory from large cross-products | Use broadcast joins for small tables, add `.repartition()` |
| Slower than geometric approach | Profile with Spark UI, may need hybrid approach |
| Spark.NET API limitations (no bit_or aggregation) | Implement custom UDF or use Max() as approximation |
| Window functions too slow | Fall back to self-joins |

## Next Steps

1. Create a feature branch for experimentation
2. Implement Step 5 (bitwise aggregation) as proof of concept
3. Run Fast tests and compare performance
4. Iterate based on results
