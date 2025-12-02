# Step 1 Implementation Plan: Depth-Tagged Union for Sequenced Moves

## Goal
Replace the iterative loop in `ComputeSequencedMoves` (lines 566-621) with a declarative depth-tagged DataFrame union that propagates validity through window functions, eliminating `IsEmpty()` checks and deferring materialization to the final filter.

## Current Architecture Analysis

**Materialization points in current code:**
1. **Line 525:** `if (!IsEmpty(entryMoves))` - materializes to check emptiness
2. **Line 533:** `Collect().First()` - materializes Out flags for initial continuation
3. **Line 566:** `for (int depth = 0; depth < maxDepth && !IsEmpty(currentEntryMoves); depth++)` - materializes in loop guard
4. **Line 578:** `Collect().First()` - materializes Out flags in each iteration
5. **Line 593:** Implicitly materializes when checking `!IsEmpty(nextPerspectives)`

**Current flow:**
```
entryMoves (depth 0)
  ↓
[MATERIALIZE: get Out flags] → continuation moves from original position
  ↓
for depth 0..maxDepth:
  [MATERIALIZE: IsEmpty check]
  [MATERIALIZE: get Out flags]
  compute nextPerspectives from currentEntryMoves
  [MATERIALIZE: IsEmpty check]
  compute continuation moves from nextPerspectives
  compute next entry moves from nextPerspectives
  currentEntryMoves = next entry moves
  ↓
return union of all continuation moves
```

## New Architecture Design

**Target flow:**
```
Generate depths DataFrame [0..maxDepth]
  ↓
Cross-join entryPatterns × depths → all potential entry moves at all depths
  ↓
Add depth-aware columns:
  - sequence_key (for partitioning by src position + variant)
  - parent_depth (depth - 1)
  ↓
Self-join to simulate perspective transitions:
  moves[depth=N] JOIN moves[depth=N-1] on (dst matches src)
  ↓
Tag with validity flags via window functions:
  - is_blocked: next square is non-empty
  - is_valid: parent was not blocked
  ↓
Compute continuation moves with lazy Out flags:
  - Group entry moves by depth to get active flags
  - Join continuation patterns with flags DataFrame
  ↓
Union entry + continuation moves
  ↓
Single filter: WHERE is_valid AND move_type='continuation'
```

## Implementation Steps

### Step 1.1: Add depth column infrastructure
**Location:** After line 510 (after computing initial entry moves)

**Changes:**
```csharp
// Tag entry moves with depth 0 and sequence key for windowing
var entryMovesWithDepth = entryMoves
    .WithColumn("depth", Lit(0))
    .WithColumn("sequence_key", Concat(
        Col("src_x").Cast("string"), Lit("_"),
        Col("src_y").Cast("string"), Lit("_"),
        Col("sequence").BitwiseAND(Lit(variantMask)).Cast("string")
    ));
```

### Step 1.2: Generate all potential depths
**Location:** After step 1.1

**Changes:**
```csharp
// Generate all depth levels for expansion
var depths = _spark.Range(0, maxDepth).Select(Col("id").Alias("target_depth"));
```

### Step 1.3: Cross-join to generate all potential entry moves at all depths
**Location:** After step 1.2

**Changes:**
```csharp
// Create all potential entry moves across all depths
// This is lazy - query plan only, not materialized
var allPotentialEntryMoves = entryMovesWithDepth
    .CrossJoin(depths)
    .Filter(Col("target_depth") >= Col("depth")) // Only future depths
    .WithColumn("expansion_depth", Col("target_depth") - Col("depth"));
```

### Step 1.4: Simulate perspective transitions via self-join
**Location:** After step 1.3

**Changes:**
```csharp
// Simulate moving the piece: parent's dst becomes child's src
// This creates the chain: entry[0] -> entry[1] -> entry[2] -> ...
var perspectiveTransitions = allPotentialEntryMoves.Alias("child")
    .Join(
        allPotentialEntryMoves.Alias("parent"),
        (Col("child.src_x") == Col("parent.dst_x")) &
        (Col("child.src_y") == Col("parent.dst_y")) &
        (Col("child.sequence_key") == Col("parent.sequence_key")) &
        (Col("child.depth") == Col("parent.depth") + 1),
        "left_outer"
    )
    .Select(
        Col("child.*"),
        Col("parent.dst_generic_piece").Alias("parent_dst_piece")
    );
```

### Step 1.5: Add validity propagation via window functions
**Location:** After step 1.4

**Changes:**
```csharp
// Validity: can only continue if parent hit empty square
var validityWindow = Window
    .PartitionBy("sequence_key")
    .OrderBy("depth");

var movesWithValidity = perspectiveTransitions
    .WithColumn("parent_was_blocked",
        Coalesce(
            Lag(
                Col("dst_generic_piece").BitwiseAND(Lit((int)Piece.Empty)).EqualTo(Lit(0)),
                1
            ).Over(validityWindow),
            Lit(false)
        )
    )
    .WithColumn("is_valid_entry",
        When(Col("depth") == 0, Lit(true))
        .Otherwise(Not(Col("parent_was_blocked")))
    );
```

### Step 1.6: Compute lazy Out flags aggregation
**Location:** After step 1.5

**Changes:**
```csharp
// Aggregate Out flags per depth WITHOUT materializing
var outFlagsByDepth = movesWithValidity
    .Filter(Col("is_valid_entry"))
    .GroupBy("depth")
    .Agg(
        Max(Col("sequence").BitwiseAND(Lit(outMask | variantMask))).Alias("active_out_flags")
    );
```

### Step 1.7: Compute continuation moves with lazy flags
**Location:** After step 1.6

**Changes:**
```csharp
// Compute continuation moves from valid entry positions
// Join with Out flags to enable In-matched continuation patterns
var continuationMoves = movesWithValidity
    .Filter(Col("is_valid_entry"))
    .Join(outFlagsByDepth.Alias("flags"), Col("depth") == Col("flags.depth"))
    .Join(
        continuationPatternsDf,
        // Pattern's In flags must match entry's Out flags
        // Use the lazy flags column, not materialized int
        (Col("sequence").BitwiseAND(Lit(inMask)).BitwiseAND(
            (Col("flags.active_out_flags") >> 1) | Col("flags.active_out_flags")
        )).EqualTo(Col("sequence").BitwiseAND(Lit(inMask)))
    )
    .WithColumn("is_valid_continuation", Col("is_valid_entry"))
    .WithColumn("move_type", Lit("continuation"))
    .Drop("flags.depth", "flags.active_out_flags");
```

### Step 1.8: Replace loop with single filter
**Location:** Replace lines 566-621 (entire loop)

**Changes:**
```csharp
// Single materialization point: filter for valid continuation moves
return continuationMoves
    .Filter(Col("is_valid_continuation"))
    .Drop("depth", "sequence_key", "parent_dst_piece", "parent_was_blocked", "is_valid_entry");
```

## Risks and Mitigations

### Risk 1: Cross-join explosion
**Problem:** `entryMoves × depths` creates `~50 × 7 = 350 rows`, then self-join squares it to `~122,500 rows` in query plan.

**Mitigation:**
- Add `.Coalesce(10)` after cross-join to reduce partitions
- Monitor with `.Explain()` to check if Spark optimizes away unused depths
- Consider adding early filter: `.Filter(Col("expansion_depth") <= 1)` to limit lookahead

### Risk 2: Window function performance
**Problem:** Window functions over 800+ partitions (sequence_keys) may be slow.

**Mitigation:**
- Start with window functions as they're simpler
- If slow, fall back to self-join approach (see plan section 2.2)
- Add explicit `.Repartition("sequence_key")` before window function

### Risk 3: Lazy Out flags not truly lazy
**Problem:** `GroupBy().Agg()` may materialize when joined multiple times.

**Mitigation:**
- Cache the outFlagsByDepth DataFrame: `.Persist(StorageLevel.MEMORY_ONLY)`
- This caches the plan, allowing Spark to optimize pushdowns
- Monitor memory usage to ensure no OOM

### Risk 4: Initial continuation moves logic
**Problem:** Current code has special case for "first square in each direction" (lines 527-558) that computes continuation moves from original position, not from entry move destinations.

**Mitigation:**
- Keep this special case initially by computing it separately
- In new architecture, this becomes: continuation moves where depth=0
- Ensure `outFlagsByDepth` includes depth 0 aggregation

## Testing Strategy

1. **Incremental verification:**
   - After each step, add debug `.Show()` calls (commented out by default)
   - Verify row counts match expectations before proceeding
   
2. **Correctness validation:**
   - Run single test: `BishopMovementTests.EmptyBoard_BishopInCenter_CanMoveToAdjacentDiagonals`
   - Compare output move count with current implementation
   - If mismatch, use `.Explain()` to debug query plan

3. **Performance baseline:**
   - Measure current loop time with Stopwatch
   - Compare with new approach on same test
   - Target: within 20% of current performance

4. **Full regression:**
   - Run Fast tests (83 tests)
   - All must pass with identical move counts

## Rollback Plan

If implementation fails or causes issues:
1. Keep original `ComputeSequencedMoves` as `ComputeSequencedMoves_V1`
2. Implement new version as `ComputeSequencedMoves_V2`
3. Add feature flag: `bool useDepthTaggedUnion = false`
4. Easy rollback by flipping flag

## Success Metrics

- ✅ Zero `IsEmpty()` calls in ComputeSequencedMoves
- ✅ Zero `Collect()` calls for Out flags in ComputeSequencedMoves
- ✅ All 83 Fast tests pass
- ✅ Performance within 20% of baseline (ideally better due to query optimization)
- ✅ Memory usage not exceeding baseline

## Next Steps After Completion

Once Step 1 is validated:
1. Apply same pattern to `ComputeThreatenedCellsFromSliding` (lines 1040-1110)
2. Proceed to Step 2: Validity propagation in main candidate computation
3. Eventually replace `BuildTimeline` loop with same approach (Step 4)
