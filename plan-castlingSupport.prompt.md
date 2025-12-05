# Plan: Castling Support via Progressive Integration

Add Castling move generation and validation by integrating SequenceEngine → CandidateGenerator → ChessPolicyRefactored, leveraging pattern sequences (EmptyAndSafe for king, InD step 2) to validate threat paths declaratively.

## Steps

### 1. Verify CandidateGenerator calls SequenceEngine

**Objective:** Ensure `CandidateGenerator.GetMoves()` already composes both `PatternMatcher.MatchAtomicPatterns()` and `SequenceEngine.ExpandSequencedMoves()`, returning castling moves with full threat path validation via pattern constraints.

**Details:**
- King OutD patterns require `Piece.EmptyAndSafe` (Empty | ~Threatened) destination
- Rook InD patterns require `Piece.AllyKing` destination (completes move to landing square)
- Together: Each step of castling sequence validates the square is not threatened

**Validation:**
- Inspect `CandidateGenerator.cs` to confirm `SequenceEngine.ExpandSequencedMoves()` is called
- Verify castling moves (Variant1/Variant2 kingside/queenside) are included in returned move set
- Confirm threat path validation happens via pattern constraints, not post-move filtering

---

### 2. Wire CandidateGenerator into ChessPolicyRefactored.BuildTimeline()

**Objective:** Replace delegation to legacy `TimelineService.ComputeNextCandidates()` with `CandidateGenerator.GetMoves()`, enabling multi-depth castling move generation where each pattern step validates constraints declaratively.

**Details:**
- Locate `ChessPolicyRefactored.BuildTimeline()` method
- Find line delegating to legacy `TimelineService`
- Replace with call to `CandidateGenerator.GetMoves(perspectives, patternsDf, factions, turn)`
- Ensure method signature matches and return types align

**Impact:**
- Castling moves now generated via integrated pipeline
- All move types (atomic + sequenced) available at all depths
- Threat validation via pattern constraints (not post-filtering)

---

### 3. Add Essential test: Castling moves generate via integrated pipeline

**Test Name:** `CastlingIntegration_MovesGenerateViaRefactoredPipeline_Refactored`

**Objective:** Verify `BuildTimeline(depth=1)` returns castling moves when starting position has Mint king & rook, no blockers between them, and king destination not threatened.

**Test Setup:**
- Create board: White king on e1 (Mint), white rook on h1 (Mint)
- Black pieces positioned so king-side castling is valid (no threats on f1, g1)
- Call `ChessPolicyRefactored.BuildTimeline(board, factions, depth=1)`

**Assertions:**
- Result contains castling moves for white (kingside Variant2)
- Move has src_x=4, src_y=0 (white king)
- Move has dst_x=6, dst_y=0 (castled king position)
- Move has dst_x=5, dst_y=0 for rook (castled rook position) in same move sequence

**Trait:** `[Trait("Essential", "True")]`

---

### 4. Verify EmptyAndSafe pattern constraints

**Objective:** Confirm pattern sequences enforce threat validation at each step:

**Details:**
- King OutD patterns must require `Piece.EmptyAndSafe` destination
- Rook InD patterns must require `Piece.AllyKing` destination
- Perspective data must flow correctly from OutD entry step through InD continuation step
- LegalityEngine.FilterLegalMoves() validates no threatened destinations remain

**Validation:**
- Inspect `PatternRepository.cs` for castling pattern definitions
- Verify `Piece.EmptyAndSafe` is used in king OutD patterns
- Verify `Piece.AllyKing` is used in rook InD patterns
- Check perspective computation includes opponent threats at each board state

**Note:** Pattern constraints are declarative; if a square is threatened, it won't match EmptyAndSafe destination condition and move won't be generated.

---

### 5. Add Essential test: Castling path threat validation

**Test Name:** `CastlingLegality_RejectsPathThreat_Refactored`

**Objective:** Verify castling moves are rejected when intermediate king path squares are threatened.

**Test Setup:**
- White king e1 (Mint), white rook h1 (Mint)
- Black rook on f8 attacking f-file (threatens f1, not f-file for kingside)
- **Alternative:** Black bishop on c4 attacking diagonal (threatens f1 for kingside castling)
- Call `ChessPolicyRefactored.GetPerspectivesWithThreats()` to get threatened squares
- Call `CandidateGenerator.GetMoves()` to generate candidates
- Call `LegalityEngine.FilterLegalMoves()` to filter legal moves

**Assertions:**
- No kingside castling moves in result (f1 threatened, so EmptyAndSafe fails)
- Queenside castling may still be available (different path)

**Trait:** `[Trait("Essential", "True")]`

---

### 6. Wire TimelineEngine.BuildTimeline()

**Objective:** Replace `ChessPolicyRefactored.BuildTimeline()` delegation to legacy with `TimelineEngine.BuildTimeline()`, ensuring multi-depth recursion uses integrated CandidateGenerator + LegalityEngine pipeline.

**Details:**
- Locate `TimelineEngine.BuildTimeline()` method
- Verify it uses `CandidateGenerator.GetMoves()` (from Step 2)
- Verify it uses `LegalityEngine.FilterLegalMoves()` (already integrated)
- Replace delegation in `ChessPolicyRefactored.BuildTimeline()` with `TimelineEngine.BuildTimeline()`
- Ensure recursive depth handling mirrors legacy behavior

**Impact:**
- Multi-depth game tree includes castling moves at all depths
- Mint flag removal prevents castling in depth 2+ (flag lost after first move)
- Full integration of 9-layer architecture complete

---

## Further Considerations

### 1. Pattern sequence validation order

**Question:** In castling OutD/InD sequences, does PatternMatcher validate **EmptyAndSafe at each step** or just the final destination?

**Details:**
- Check if perspective data correctly flows from OutD entry step through InD continuation step
- Verify PatternMatcher applies sequence filtering correctly with Parallel flag
- Ensure threat checking happens at pattern match time, not post-move

**Recommendation:** Add debug test to inspect pattern matching intermediate results if unclear.

**Debug Test:** Log all intermediate perspective matches during castling pattern expansion to verify each square in sequence validates EmptyAndSafe.

---

### 2. Parallel sequence handling

**Question:** Castling patterns use `Sequence.Parallel | Instant | Recursive`. Does SequenceEngine correctly handle Parallel flag?

**Details:**
- Parallel means king+rook move simultaneously in same turn
- Verify SequenceEngine generates both moves as single composite move entry
- Ensure no existing code assumes single-piece-per-move model
- Check move representation includes both source/destination pairs

**Recommendation:** Verify no existing code assumes single-piece-per-move model by searching for assumptions about move structure.

**Test:** Create unit test for SequenceEngine with Parallel flag, verify output contains both king and rook destination pairs.

---

### 3. Test execution timing

**Question:** How to ensure test results are visible after task execution?

**Details:**
- Build and publish integration test project
- Run `Spark: Start (if needed) + Test (Essential)` task
- **Wait 20 seconds after task ends** before checking logs (task completes before test results finish writing)
- Task output ends before tests actually complete

**Recommendation:** 
- Add brief delay in test execution script or display progress indicator
- Or monitor Spark runner logs for test completion marker
- Manually wait 20s before checking output if timing unclear
- Run `Cleanup` task if the runner gets stuck

---

## Testing Strategy

### Minimal Essential Tests (3 total)

1. **Castling moves generate via integrated pipeline**
   - Validates: CandidateGenerator integration, pattern sequence matching, Variant1/2 generation
   - Depth: 1
   - Setup: Standard starting position (Mint king+rook, clear path)
   - Assert: Castling moves present in BuildTimeline result

2. **Castling path threat validation**
   - Validates: EmptyAndSafe constraint, LegalityEngine filtering, threat-based rejection
   - Depth: 1
   - Setup: Starting position + black piece attacking f1 (kingside path)
   - Assert: Kingside castling rejected, queenside available

3. **Castling in multi-depth timeline**
   - Validates: TimelineEngine integration, Mint flag removal prevents castling depth 2+
   - Depth: 3
   - Setup: Castling available at depth 1, verify Mint flag removed after move
   - Assert: Castling in depth 1 result, no castling in depth 2 positions (flag gone)

### Test Execution

```
# After implementing changes:
1. Build integration test project (dotnet publish)
2. Run task: "Spark: Start (if needed) + Test (Essential)"
3. Wait 20 seconds (task ends before tests complete)
4. Check output for 3 Essential tests passing
```

---

## Integration Points

### CandidateGenerator (Layer 7)

**Current Status:** Fully implemented, not called by ChessPolicyRefactored

**Required Integration:**
- `CandidateGenerator.GetMoves(perspectives, patterns, factions, turn)` returns atomic + sequenced moves
- Call from `ChessPolicyRefactored.BuildTimeline()` instead of legacy `TimelineService`

**Castling Handling:**
- Returns both kingside (Variant1) and queenside (Variant2) castling moves
- Each move validated via pattern constraints (EmptyAndSafe for king steps, AllyKing for rook step)

---

### SequenceEngine (Layer 6)

**Current Status:** Fully implemented, not called by ChessPolicyRefactored

**Required Integration:**
- `SequenceEngine.ExpandSequencedMoves()` called by `CandidateGenerator.GetMoves()`
- Already expands OutD/InD sequences with threat-validated destinations

**Castling Handling:**
- Processes Parallel flag for simultaneous king+rook movement
- Expands OutD entry to InD continuation for both pieces
- Validates each step's destination against EmptyAndSafe/AllyKing constraints

---

### LegalityEngine (Layer 8)

**Current Status:** Already integrated into ChessPolicyRefactored.FilterLegalMoves()

**Current Validation:**
- King safety (move doesn't leave/put king in check)
- Pin detection (pinned pieces can't move away from pin)
- Discovered check prevention (can't unblock own king)

**Castling-Specific:**
- Threat path validation happens at pattern level (EmptyAndSafe constraint)
- LegalityEngine double-checks king safety after simulated move
- No additional castling logic needed in LegalityEngine

---

### TimelineEngine (Layer 9)

**Current Status:** Created but not used; ChessPolicyRefactored delegates to legacy

**Required Integration:**
- Replace `ChessPolicyRefactored.BuildTimeline()` delegation with `TimelineEngine.BuildTimeline()`
- TimelineEngine calls CandidateGenerator (from Step 2) for move generation
- TimelineEngine calls LegalityEngine (already integrated) for move filtering
- Recursively builds multi-depth game tree

---

## Success Criteria

- [ ] Step 1: CandidateGenerator confirmed to call SequenceEngine
- [ ] Step 2: CandidateGenerator wired into ChessPolicyRefactored.BuildTimeline()
- [ ] Step 3: Essential test passes (castling moves generate)
- [ ] Step 4: EmptyAndSafe/AllyKing patterns verified in PatternRepository
- [ ] Step 5: Essential test passes (threat path validation)
- [ ] Step 6: TimelineEngine wired; ChessPolicyRefactored uses it
- [ ] All 3 Essential tests passing via "Spark: Start (if needed) + Test (Essential)" task
- [ ] Castling moves work at all depths with Mint flag persistence
