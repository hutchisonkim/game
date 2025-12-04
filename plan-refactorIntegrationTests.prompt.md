# Plan: Refactor All Integration Tests to Use ChessPolicyRefactored

**Goal**: Migrate all 83 integration tests from `ChessPolicy` to `ChessPolicyRefactored` in phases ordered by intrusiveness and complexity, ensuring backward compatibility and maintaining test coverage throughout.

---

## Current State Analysis

**Test Files** (16 files, ~83-89 tests total):
- ✅ **5 tests already refactored** with `[Trait("Debug", "True")]`
- 🔄 **78-84 tests remaining** to refactor

**Test Categories by Complexity**:

| File | Tests | Complexity | Policy Dependency |
|------|-------|-----------|-------------------|
| InfrastructureTests.cs | 3 | **Low** | Minimal (Board only) |
| PatternFactoryTests.cs | 5 | **Low** | PatternFactory only |
| PerspectiveTests.cs | 5 | **Low** | GetPerspectives |
| BoardSimulationTests.cs | 6 | **Medium** | SimulateBoardAfterMove |
| ThreatenedCellsTests.cs | 5 | **Medium** | GetPerspectivesWithThreats |
| PawnMovementTests.cs | 5 | **Medium** | Pattern matching + moves |
| KnightMovementTests.cs | 7 | **Medium** | Pattern matching + moves |
| BishopMovementTests.cs | 4 | **Medium** | Pattern matching + sliding |
| RookMovementTests.cs | 6 | **Medium** | Pattern matching + sliding |
| QueenMovementTests.cs | 3 | **Medium** | Pattern matching + sliding |
| KingMovementTests.cs | 4 | **Medium** | Pattern matching + moves |
| SequenceParameterTests.cs | 16 | **High** | Sequence flags + internal logic |
| EnPassantTests.cs | 4 | **High** | Complex multi-step patterns |
| CastlingTests.cs | 9 | **High** | Complex multi-step patterns |
| KingInCheckTests.cs | 7 | **Very High** | FilterMovesLeavingKingInCheck + legality |

---

## Refactoring Steps (Ordered by Intrusiveness)

### **Phase 1: Foundation & Simple Tests** ✅ PARTIALLY COMPLETE
**Intrusiveness**: Minimal | **Complexity**: Low | **Risk**: Very Low

**Rationale**: These tests validate static data and simple transformations with minimal dependencies.

#### Step 1.1: Infrastructure Tests (3 tests)
- **File**: `InfrastructureTests.cs`
- **Already Compatible**: Tests mostly validate Board.Default and Spark connectivity
- **Change Required**: None to minimal (Board is shared across both policies)
- **Strategy**: Add refactored versions as Debug tests

#### Step 1.2: Pattern Factory Tests (5 tests)
- **File**: `PatternFactoryTests.cs`
- **Current**: Uses `ChessPolicy.PatternFactory`
- **New**: Use `PatternRepository.GetPatterns()`
- **Change Required**: Swap factory instantiation
- **Strategy**: Add refactored tests using PatternRepository

#### Step 1.3: Complete Perspective Tests (3 remaining)
- **File**: `PerspectiveTests.cs`
- **Current**: 2 refactored ✅, 3 remaining
- **Change Required**: Replace `_policy.GetPerspectives()` with `refactoredPolicy.GetPerspectives()`
- **Strategy**: Add refactored versions for remaining tests

---

### **Phase 2: Simulation & Threat Tests**
**Intrusiveness**: Low-Medium | **Complexity**: Medium | **Risk**: Low

**Rationale**: Tests that validate the newly extracted layers (Simulation, Perspectives with threats).

#### Step 2.1: Complete Board Simulation Tests (4 remaining)
- **File**: `BoardSimulationTests.cs`
- **Current**: 1 refactored ✅, 5 remaining
- **Change Required**: Use `refactoredPolicy.SimulateBoardAfterMove()`
- **Strategy**: Validate SimulationEngine layer

#### Step 2.2: Complete Threatened Cells Tests (4 remaining)
- **File**: `ThreatenedCellsTests.cs`
- **Current**: 1 refactored ✅, 4 remaining
- **Change Required**: Use `refactoredPolicy.GetPerspectivesWithThreats()`
- **Strategy**: Validate PerspectiveEngine.ApplyThreatMask()

---

### **Phase 3: Basic Movement Tests**
**Intrusiveness**: Medium | **Complexity**: Medium | **Risk**: Low-Medium

**Rationale**: Tests that validate pattern matching for non-sliding pieces. These rely on TimelineService.ComputeNextCandidates but don't test complex sequences.

#### Step 3.1: Pawn Movement Tests (5 tests)
- **File**: `PawnMovementTests.cs`
- **Dependencies**: GetPerspectives + ComputeNextCandidates
- **Change Required**: Use refactored policy for perspectives
- **Notes**: Pawn has simple sequences (OutA, InA)

#### Step 3.2: Knight Movement Tests (6 remaining)
- **File**: `KnightMovementTests.cs`
- **Current**: 1 refactored ✅, 6 remaining
- **Dependencies**: GetPerspectives + ComputeNextCandidates
- **Change Required**: Minimal (knight has no sequences)
- **Notes**: Simplest piece to refactor

#### Step 3.3: King Movement Tests (4 tests)
- **File**: `KingMovementTests.cs`
- **Dependencies**: GetPerspectives + ComputeNextCandidates
- **Change Required**: Minimal (king has no sequences)
- **Notes**: No sliding, but castling is separate

---

### **Phase 4: Sliding Piece Tests**
**Intrusiveness**: Medium-High | **Complexity**: Medium-High | **Risk**: Medium

**Rationale**: Tests that validate sliding logic (InstantRecursive sequences). These depend heavily on TimelineService.ComputeSequencedMoves.

#### Step 4.1: Bishop Movement Tests (3 remaining)
- **File**: `BishopMovementTests.cs`
- **Current**: 1 refactored ✅, 3 remaining
- **Dependencies**: GetPerspectives + ComputeSequencedMoves
- **Change Required**: Tests use sliding patterns (OutF, InF)
- **Notes**: Diagonal sliding only

#### Step 4.2: Rook Movement Tests (6 tests)
- **File**: `RookMovementTests.cs`
- **Dependencies**: GetPerspectives + ComputeSequencedMoves
- **Change Required**: Tests use sliding patterns (OutI, InI)
- **Notes**: Orthogonal sliding

#### Step 4.3: Queen Movement Tests (3 tests)
- **File**: `QueenMovementTests.cs`
- **Dependencies**: GetPerspectives + ComputeSequencedMoves
- **Change Required**: Tests use sliding patterns (OutG, OutH)
- **Notes**: Both diagonal and orthogonal

---

### **Phase 5: Sequence Logic Tests**
**Intrusiveness**: High | **Complexity**: High | **Risk**: Medium-High

**Rationale**: Tests that validate internal sequence flag logic. These are tightly coupled to TimelineService implementation details.

#### Step 5.1: Sequence Parameter Tests (16 tests)
- **File**: `SequenceParameterTests.cs`
- **Dependencies**: Deep TimelineService internals
- **Tests Include**:
  - Flag validation (InMask, OutMask)
  - Bit shift validation (Out >> 1 = In)
  - ConvertOutFlagsToInFlags
  - Pattern sequence filtering
  - ActiveSequence parameter behavior
- **Strategy**: 
  - **Option A**: Keep tests as-is (they validate TimelineService which refactored policy uses)
  - **Option B**: Add refactored versions that test through the facade
  - **Recommended**: Option A + add facade-level integration tests

---

### **Phase 6: Complex Multi-Step Move Tests**
**Intrusiveness**: Very High | **Complexity**: Very High | **Risk**: High

**Rationale**: Tests that validate complex, multi-pattern sequences. These are the most coupled to TimelineService.

#### Step 6.1: En Passant Tests (4 tests)
- **File**: `EnPassantTests.cs`
- **Dependencies**: 
  - Pattern matching (OutE, InE)
  - Sequence chaining (capture sideways → move forward)
  - Passing flag logic
- **Change Required**: High (multi-step sequence validation)
- **Notes**: Complex pattern interaction

#### Step 6.2: Castling Tests (9 tests)
- **File**: `CastlingTests.cs`
- **Dependencies**:
  - Pattern matching (OutD, InD)
  - Parallel sequences
  - Mint flag preservation
  - Threatened cell integration
- **Change Required**: Very High (parallel multi-piece move)
- **Notes**: Most complex move in chess

---

### **Phase 7: Legality & Check Validation**
**Intrusiveness**: Very High | **Complexity**: Very High | **Risk**: Very High

**Rationale**: Tests that validate the most complex logic: FilterMovesLeavingKingInCheck. This hasn't been extracted yet.

#### Step 7.1: King In Check Tests (7 tests)
- **File**: `KingInCheckTests.cs`
- **Dependencies**:
  - FilterMovesLeavingKingInCheck
  - Pin detection
  - Discovered check logic
  - Board simulation
  - Threat computation
- **Change Required**: Very High (complex legality engine)
- **Strategy**: 
  - **WAIT** until LegalityEngine is extracted (Phase 2 of original plan)
  - Add facade method to ChessPolicyRefactored when ready
- **Notes**: Most complex test suite, needs dedicated engine extraction first

---

## Missing Test Coverage

### ✅ **Adequately Covered**:
- Basic piece movement (all pieces)
- Pattern matching
- Board simulation
- Perspective generation
- Threat computation
- Sequence flags
- Special moves (castling, en passant)
- Check validation

### ⚠️ **Gaps Identified**:

1. **Timeline Building Tests**
   - `BuildTimeline()` is tested indirectly but no dedicated tests
   - **Recommendation**: Add tests for multi-depth timeline exploration

2. **Pattern Repository Caching**
   - No tests for `PatternRepository.ClearCache()`
   - **Recommendation**: Add test validating cache behavior

3. **BoardStateProvider Tests**
   - No dedicated tests for `GetPieces()` conversion
   - **Recommendation**: Add test validating schema and board→DataFrame conversion

4. **PerspectiveEngine Layer Tests**
   - No isolated tests for `RecomputeGenericPiece()`
   - **Recommendation**: Add test after SimulateBoardAfterMove

5. **Error/Edge Cases**:
   - Empty board edge cases
   - Invalid move attempts
   - Board boundaries
   - **Recommendation**: Add negative test cases

6. **Performance Tests**:
   - No tests marked `[Trait("Performance", "Benchmark")]`
   - **Recommendation**: Add benchmarks for critical paths

7. **Promotion Tests**:
   - Pawn promotion patterns exist but no integration tests
   - **Recommendation**: Add dedicated promotion test file

---

## Implementation Strategy

### Recommended Approach: **Incremental Parallel Testing**

Instead of replacing existing tests, **add refactored versions** alongside originals:

```csharp
[Fact]
[Trait("Performance", "Fast")]
public void OriginalTest() { /* uses ChessPolicy */ }

[Fact]
[Trait("Performance", "Fast")]
[Trait("Debug", "True")]
[Trait("Refactored", "True")]
public void OriginalTest_Refactored() { /* uses ChessPolicyRefactored */ }
```

**Benefits**:
- ✅ No risk to existing tests
- ✅ Side-by-side validation
- ✅ Easy rollback
- ✅ Gradual migration
- ✅ Both implementations tested continuously

**When to Remove Originals**:
- After all refactored tests pass for 2+ weeks
- After ChessPolicyB is formally deprecated
- After Phase 2 of architecture refactoring completes

---

## Execution Timeline

### Sprint 1 (Immediate): Foundation ✅ DONE
- ✅ Phase 1.1: Infrastructure (3 tests)
- ✅ Phase 1.2: Pattern Factory (5 tests)
- ✅ Phase 1.3: Perspectives (3 tests)

### Sprint 2 (Next): Simulation & Threats
- Phase 2.1: Board Simulation (4 tests)
- Phase 2.2: Threatened Cells (4 tests)
- **Total**: 8 tests

### Sprint 3: Basic Movement
- Phase 3.1: Pawn (5 tests)
- Phase 3.2: Knight (6 tests)
- Phase 3.3: King (4 tests)
- **Total**: 15 tests

### Sprint 4: Sliding Pieces
- Phase 4.1: Bishop (3 tests)
- Phase 4.2: Rook (6 tests)
- Phase 4.3: Queen (3 tests)
- **Total**: 12 tests

### Sprint 5: Sequences
- Phase 5.1: Sequence Parameters (16 tests - selective)
- **Total**: ~8 tests (facade-level only)

### Sprint 6: Complex Moves
- Phase 6.1: En Passant (4 tests)
- Phase 6.2: Castling (9 tests)
- **Total**: 13 tests

### Sprint 7 (Future): Legality
- **BLOCKED**: Requires LegalityEngine extraction first
- Phase 7.1: King In Check (7 tests)
- **Total**: 7 tests

---

## Success Metrics

- **Code Coverage**: Maintain 100% for refactored components
- **Test Pass Rate**: 100% for both original and refactored
- **Performance**: Refactored tests ≤ 110% of original execution time
- **Regression**: Zero regressions in existing tests during migration
