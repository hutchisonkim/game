# Plan: Remaining Work for Pure Lazy DataFrame Implementation

## ✅ Completed (Step 1)
- **`ComputeSequencedMoves`** fully refactored with depth-tagged union
  - Zero `Collect()` calls for Out flags ✅
  - Zero `IsEmpty()` checks in main logic ✅  
  - All 83 Fast tests pass ✅
  - Uses window functions for validity propagation ✅
  - Single materialization point at final filter ✅

## 🔴 Remaining Work

---

## Step 2: Apply Same Pattern to `ComputeSlidingThreats`

**Location:** Lines 1033-1145  
**Current Status:** Still uses iterative loop with materializations

**Materializations to eliminate:**
- Line 1081: `IsEmpty(currentFrontier)` 
- Line 1106: `IsEmpty(nextPerspectives)`
- Line 1115: `Collect().First()` for Out flags
- Line 1132: `IsEmpty(nextMoves)`

**Implementation:** Copy the depth-tagged union pattern from `ComputeSequencedMoves`:
1. Generate depths DataFrame [0..maxDepth]
2. Cross-join `currentFrontier × depths`
3. Use window functions to propagate validity (empty squares only)
4. Aggregate Out flags lazily per depth
5. Single filter at end for valid threatened cells

**Success Criteria:**
- Zero `IsEmpty()` / `Collect()` in hot path
- Threatened cells tests continue to pass
- Memory usage comparable or better

---

## Step 3: Eliminate Timeline Loop (`BuildTimeline`)

**Location:** Lines 157-191  
**Current Status:** Iterative `for` loop with filtering per depth

**Current Issues:**
- Line 171: Filters `timelineDf` for each depth (not materialized but creates plan bloat)
- Implicitly creates unions in each iteration
- Not leveraging Spark's optimization potential

**Target Architecture:**
```csharp
// Generate all timesteps as one DataFrame
var timesteps = spark.Range(0, maxDepth + 1);

// Create initial state at timestep 0
var initialState = perspectivesWithThreats.WithColumn("timestep", Lit(0));

// Tag all board states with timestep using validity window
// (similar to depth-tagged pattern, but for board states)
var validityWindow = Window.PartitionBy("x", "y").OrderBy("timestep");
var allBoardStates = ... // Complex: need to propagate board state through moves

// Single filter at query time
return allBoardStates.Filter(Col("timestep") == desiredTimestep);
```

**Challenge:** Board state propagation is more complex than move validity  
**Recommendation:** Defer until Steps 2, 4, 5 are complete (lowest ROI)

---

## Step 4: Replace Pin Detection with Lazy Simulation

**Location:** Lines 1336-1640 (in `FilterMovesLeavingKingInCheck`)  
**Current Status:** Geometric collinearity checks with cross-joins

**Materializations to eliminate:**
- Line 1357: `IsEmpty(kingPerspective)`
- Line 1363: `Collect().First()` to get king position
- Line 1515: `IsEmpty(opponentSlidingPieces)`

**Target Architecture:**
```csharp
// Tag candidates with move_id (lazy)
var candidatesWithId = candidates.WithColumn("move_id", MonotonicallyIncreasingId());

// Simulate boards for ALL candidates (lazy cross-product)
var simulatedBoards = candidatesWithId
    .Join(perspectivesDf, "perspective_x", "perspective_y")
    .WithColumn("simulated_piece",
        When(Col("x") == Col("src_x") & Col("y") == Col("src_y"), Lit(Piece.Empty))
        .When(Col("x") == Col("dst_x") & Col("y") == Col("dst_y"), Col("src_piece"))
        .Otherwise(Col("piece")));

// Compute threats per move (lazy)
var threatsPerMove = simulatedBoards
    .Join(threatPatternsDf, threatConditions)
    .GroupBy("move_id")
    .Agg(Collect_Set("threatened_cell").Alias("threats"));

// Check king safety (lazy)
return candidatesWithId
    .Join(threatsPerMove, "move_id", "left")
    .Filter(Not(Array_Contains(Col("threats"), Col("king_position"))))
    .Drop("move_id", "threats");
```

**Success Criteria:**
- Zero geometric collinearity checks
- Zero `Collect()` for king position
- Check validation tests pass
- Performance comparable (target: within 2x current)

---

## Step 5: Eliminate All `IsEmpty()` Helper Usage

**Locations:** 12 remaining usages (see grep results)
- Line 199: Definition (keep)
- Lines 1056, 1081, 1106, 1132, 1217, 1336, 1357, 1515, 1636, 1668, 1681: Actual usage

**Strategy:** 
Each `IsEmpty()` check is a control-flow decision. Replace with:
1. **Early returns:** Keep for validation (lines 1217, 1336, 1636, 1668, 1681) - acceptable
2. **Loop guards:** Replace with validity propagation (Steps 2, 3 will eliminate 1056, 1081, 1106, 1132)
3. **Conditional logic:** Use DataFrame `.Filter()` with `Coalesce()` defaults (1357, 1515)

**Example transformation:**
```csharp
// Before:
if (IsEmpty(opponentSlidingPieces)) {
    validNonKingMoves = nonKingMovesCheck;
} else {
    validNonKingMoves = nonKingMovesCheck.Join(...);
}

// After:
var opponentCount = opponentSlidingPieces.Agg(Count("*")).Alias("opp_count");
validNonKingMoves = nonKingMovesCheck
    .CrossJoin(opponentCount)
    .Join(opponentSlidingPieces, Col("opp_count") > 0, "left_outer")
    .WithColumn("needs_pin_check", Col("opp_count") > 0)
    .Filter(...); // Conditional logic pushed into filter
```

---

## Step 6: Replace Final `Collect()` Calls

**Locations:** 3 remaining
- Line 201: `IsEmpty()` helper (will be eliminated by Step 5)
- Line 1115: Out flags in `ComputeSlidingThreats` (eliminated by Step 2)
- Line 1363: King position in `FilterMovesLeavingKingInCheck` (eliminated by Step 4)

**After Steps 2-5 complete:** Zero `Collect()` calls remain ✅

---

## Implementation Priority

1. **Step 2** (ComputeSlidingThreats) - Highest ROI, proven pattern ⚡
2. **Step 4** (Pin detection) - Complex but high value 🎯
3. **Step 5** (IsEmpty elimination) - Cleanup, builds on 2 & 4 🧹
4. **Step 6** (Final Collect) - Should be automatic after 2-5 ✅
5. **Step 3** (Timeline loop) - Lowest ROI, highest complexity 🤔

---

## Success Metrics (Overall)

- ✅ Zero `Collect()` in hot paths (validation exceptions OK)
- ✅ Zero `IsEmpty()` in iteration loops
- ✅ All 83 Fast tests pass
- ✅ Memory usage ≤ current baseline
- ✅ Performance within 20% (ideally better)
- ✅ Code is more declarative

---

## Risk Management

| Step | Risk | Mitigation |
|------|------|------------|
| Step 2 | Query plan bloat | Add `.Coalesce()`, monitor with `.Explain()` |
| Step 4 | Simulate × candidates explosion | Use broadcast joins, filter early |
| Step 5 | Regression in edge cases | Add explicit tests for each `IsEmpty()` removal |
| All | Performance degradation | Profile each step, maintain rollback capability |

---

## Next Action

**Implement Step 2** (ComputeSlidingThreats) using the proven depth-tagged union pattern from Step 1.
