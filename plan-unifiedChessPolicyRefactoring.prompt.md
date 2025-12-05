# **UNIFIED CHESS POLICY REFACTORING PLAN**

**Status: December 5, 2025**  
**Current State: Phase 1 complete (4 layers extracted), 16 essential tests marked**  
**Goal: Complete all 9 architectural layers with full essential test coverage**

---

## **Executive Summary**

This unified plan combines the best aspects of three complementary strategies:

1. **Architecture (Plan 1):** 9-layer separation of concerns
2. **Test Migration (Plan 2):** Phased refactoring from low-risk to high-risk
3. **Test Streamlining (Plan 3):** Essential tests + debug tiers for fast feedback

**Current Progress:**
- ✅ Phase 1 Architecture: 4 layers extracted (Foundation, Perspectives, Simulation, Facade)
- ✅ Phase 1 Tests: 16 essential tests marked with `[Trait("Essential", "True")]` 
- ✅ Test Infrastructure: Scripts ready (spark-start-and-test-essential.ps1)
- 🔄 Remaining: 5 architectural layers + full refactored test coverage

**Target Completion:**
- All 9 architectural layers extracted (~2-3 weeks)
- 62+ refactored tests (existing + new) marked with `[Trait("Debug", "True")]`
- Full test coverage for ChessPolicyRefactored with risk-managed phases

---

## **Part I: Architectural Layers — Current vs. Target**

### **Layer 1: Foundation** ✅ COMPLETE
**Status:** Extracted and working
- `BoardStateProvider` - Board → DataFrame conversion
- `PatternRepository` - 120+ pattern definitions with deduplication
- **Tests Covered:** 3 essential + 9 debug tests

### **Layer 2: Perspectives** ✅ COMPLETE
**Status:** Extracted and working
- `PerspectiveEngine` - Self/Ally/Foe computation
- **Tests Covered:** 4 essential + 8 debug tests

### **Layer 3: Simulation** ✅ COMPLETE
**Status:** Extracted and working
- `SimulationEngine` - Stateless move simulation
- **Tests Covered:** 2 essential + 5 debug tests

### **Layer 4: Threat Engine** 🔄 PARTIALLY COMPLETE
**Status:** Uses TimelineService.ComputeThreatenedCells (needs extraction)
- **Current:** Delegated to TimelineService
- **Target:** Extract `ThreatEngine` with clean interface
- **Tests Covered:** 2 essential + 4 debug tests

### **Layer 5: Pattern Matcher** ⏳ NOT STARTED
**Status:** Mixed into TimelineService (~300 lines)
- **Goal:** Extract atomic pattern matching (non-sequenced moves)
- **Scope:** Pure pattern application without sequencing logic
- **Tests to Add:** ~8-10 new tests for atomic matching

### **Layer 6: Sequence Engine** ⏳ NOT STARTED
**Status:** Mixed into TimelineService (~400-500 lines)
- **Goal:** Extract sliding logic and multi-step sequences
- **Scope:** OutX→InX matching, InstantRecursive, variant flags
- **Tests to Add:** ~12-15 new tests for sequence expansion

### **Layer 7: Candidate Generator** ⏳ NOT STARTED
**Status:** Merge of PatternMatcher + SequenceEngine outputs
- **Goal:** Combine atomic + sequenced moves
- **Scope:** Single interface for all candidate moves (Layer 5 + Layer 6)
- **Tests to Add:** ~6-8 tests for candidate composition

### **Layer 8: Legality Engine** 🚫 BLOCKED
**Status:** FilterMovesLeavingKingInCheck still in TimelineService
- **Goal:** Extract king safety, pin detection, discovered checks
- **Scope:** Pure legality filtering with simulation integration
- **Blocker:** Requires working Threat and Candidate engines first
- **Tests Blocked:** 7 KingInCheckTests + 2 additional new tests

### **Layer 9: Timeline Engine** ⏳ NOT STARTED
**Status:** Will become thin orchestrator after other layers extracted
- **Goal:** Multi-depth move exploration with lazy evaluation
- **Scope:** Coordinate Foundation + Perspectives + Candidates + Simulation + Threats + Legality
- **Tests:** Unchanged (existing timeline tests still pass)

---

## **Part II: Smart Refactoring Phases**

### **Phase 2A: Extract Pattern Matcher** (1-2 days)
**Intrusiveness:** Low  
**Risk:** Low  
**Scope:** Atomic pattern matching only

**Deliverables:**
```csharp
Game.Chess.Policy.Patterns/PatternMatcher.cs
├─ MatchAtomicPatterns(Perspectives, Patterns, Faction)
└─ Returns: candidate moves without sequencing
```

**New Tests (8-10):**
- Test each piece type pattern matching in isolation
- Verify src_conditions, dst_conditions filtering
- Validate dst_x/dst_y computation
- Test capture vs. non-capture patterns

**Refactoring Steps:**
1. Extract ~300 lines from TimelineService.ComputeNextCandidates
2. Create pure function with no state
3. Add tests for each piece type
4. Mark as `[Trait("Debug", "True")]` and `[Trait("Phase", "2A")]`

**Integration Point:**
- ChessPolicyRefactored.GetCandidates() → uses PatternMatcher
- Tests run in Phase 2A (not Phase 1)

---

### **Phase 2B: Extract Threat Engine** (1 day)
**Intrusiveness:** Low-Medium  
**Risk:** Low  
**Scope:** Threat computation isolated from move legality

**Deliverables:**
```csharp
Game.Chess.Policy.Threats/ThreatEngine.cs
├─ ComputeThreats(Perspectives, Patterns, OpponentFaction)
├─ Internally: calls PatternMatcher with dst_conditions=NONE
└─ Returns: threat mask for all opponent pieces
```

**New Tests (4-6):**
- Test knight threat calculation
- Test rook/bishop/queen sliding threats
- Test threat masking integration
- Test empty board threat edge cases

**Refactoring Steps:**
1. Extract threat logic from TimelineService
2. Reuse PatternMatcher from Phase 2A
3. Remove dst_conditions filtering for threats
4. Add threat integration tests
5. Mark as `[Trait("Debug", "True")]` and `[Trait("Phase", "2B")]`

**Integration Point:**
- ChessPolicyRefactored.GetThreats() → uses ThreatEngine
- GetPerspectivesWithThreats already calls this concept

---

### **Phase 2C: Extract Sequence Engine** (2-3 days)
**Intrusiveness:** Medium  
**Risk:** Medium  
**Scope:** Sliding logic and multi-step sequences

**Deliverables:**
```csharp
Game.Chess.Policy.Sequences/SequenceEngine.cs
├─ ExpandEntryMoves(...)        // Start multi-step sequences
├─ ExpandContinuationMoves(...) // Continue sliding
└─ ExpandSequencedMoves(...)    // Orchestrate both
```

**New Tests (12-15):**
- Test sliding expansion (rook orthogonal, bishop diagonal)
- Test sequence continuation (OutF→InF, OutI→InI)
- Test variant flags (OutG, OutH for queen)
- Test en passant sequences (OutE→InE)
- Test castling sequences (OutD→InD with Mint flag)
- Test termination conditions (blocked by piece)

**Refactoring Steps:**
1. Extract ~400-500 lines from TimelineService
2. Separate entry vs. continuation logic
3. Preserve InstantRecursive and variant handling
4. Add comprehensive sequence tests
5. Mark as `[Trait("Debug", "True")]` and `[Trait("Phase", "2C")]`

**Integration Point:**
- ChessPolicyRefactored.GetSequencedMoves() → uses SequenceEngine

---

### **Phase 3: Merge Pattern + Sequence into Candidate Generator** (1 day)
**Intrusiveness:** Low  
**Risk:** Low  
**Scope:** Compose atomic + sequenced moves

**Deliverables:**
```csharp
Game.Chess.Policy.Candidates/CandidateGenerator.cs
├─ GetMoves(Perspectives, Patterns, Faction)
├─ Calls: PatternMatcher.MatchAtomicPatterns()
├─ Calls: SequenceEngine.ExpandSequencedMoves()
└─ Returns: all candidate moves (atomic + sliding)
```

**New Tests (4-6):**
- Test combined move generation for all pieces
- Test that atomic + sliding are both present
- Test move deduplication
- Test empty board vs. blocked scenarios

**Refactoring Steps:**
1. Create thin facade over PatternMatcher + SequenceEngine
2. Add composition tests
3. Mark as `[Trait("Debug", "True")]` and `[Trait("Phase", "3")]`
4. Update essential tests to use this interface

**Integration Point:**
- ChessPolicyRefactored.GetMoves() → uses CandidateGenerator
- Simplifies BuildTimeline call chain

---

### **Phase 4: Complete Move Legality Tests** (1-2 days)
**Intrusiveness:** Medium  
**Risk:** Low-Medium  
**Scope:** Add missing test coverage for current implementation

**Deliverables:**
- Test en passant move validation (without legality engine yet)
- Test castling move validation
- Test pawn promotion patterns
- Test all piece movement rules

**New Tests (10-15):**
- En Passant sequence tests (4-6)
- Castling sequence tests (4-6)
- Pawn promotion tests (2-3)

**Refactoring Steps:**
1. Add tests for special moves using current implementation
2. Ensure all 144 test cases pass
3. Mark as `[Trait("Debug", "True")]` and `[Trait("Phase", "4")]`
4. Verify no regressions in essential tests

**Integration Point:**
- Validates that CandidateGenerator works for all move types
- Sets up foundation for Phase 5 (Legality Engine)

---

### **Phase 5: Extract Legality Engine** (2-3 days)
**Intrusiveness:** High  
**Risk:** High  
**Scope:** King safety, pin detection, discovered check filtering

**Deliverables:**
```csharp
Game.Chess.Policy.Validation/LegalityEngine.cs
├─ FilterIllegalMoves(Candidates, Perspectives, Patterns, Faction)
├─ Steps:
│  1. Identify king location
│  2. Simulate each move
│  3. Compute threats on simulated state
│  4. Reject moves leaving king in check
└─ Returns: only legal moves
```

**New Tests (8-12):**
- Test king safety filtering
- Test pin detection
- Test discovered check prevention
- Test edge cases (castling through threats, etc.)

**Refactoring Steps:**
1. Extract FilterMovesLeavingKingInCheck from TimelineService
2. Wire up Simulation + Threat engines
3. Add comprehensive legality tests
4. Mark as `[Trait("Debug", "True")]` and `[Trait("Phase", "5")]`
5. UNBLOCK 7 KingInCheckTests

**Integration Point:**
- ChessPolicyRefactored.FilterLegalMoves() → uses LegalityEngine
- Enables full move filtering with king safety

---

### **Phase 6: Simplify Timeline Engine** (0.5 days)
**Intrusiveness:** Very Low  
**Risk:** Very Low  
**Scope:** Thin orchestrator using extracted layers

**Deliverables:**
```csharp
Game.Chess.Policy.Timeline/TimelineEngine.cs
├─ BuildTimeline(Perspectives, Patterns, Faction, maxDepth)
├─ Orchestration:
│  1. GetCandidates (via CandidateGenerator)
│  2. FilterLegalMoves (via LegalityEngine)
│  3. SimulateBoardAfterMove (via SimulationEngine)
│  4. ComputeThreats (via ThreatEngine)
│  5. Recurse up to maxDepth
└─ Returns: full timeline of legal moves
```

**New Tests (2-4):**
- Test multi-depth exploration
- Test lazy evaluation without full materialization
- Test termination conditions

**Refactoring Steps:**
1. Replace TimelineService calls with new engines
2. Verify no behavior change
3. Add final orchestration tests
4. Mark as `[Trait("Debug", "True")]` and `[Trait("Phase", "6")]`

**Integration Point:**
- ChessPolicyRefactored.BuildTimeline() uses refined orchestrator
- Original TimelineService still available as fallback

---

## **Part III: Test Coverage Strategy**

### **Test Organization by Phase**

| Phase | Layer | New Tests | Total Phase Tests | Essential? | Debug Only? |
|-------|-------|-----------|-------------------|-----------|-----------|
| 1 ✅ | Foundation, Perspectives, Simulation | 16 | 16 | ✅ 16 | 0 |
| 2A | PatternMatcher | 8-10 | 8-10 | 1-2 | 6-8 |
| 2B | ThreatEngine | 4-6 | 4-6 | 1-2 | 2-4 |
| 2C | SequenceEngine | 12-15 | 12-15 | 2-3 | 9-12 |
| 3 | CandidateGenerator | 4-6 | 4-6 | 1-2 | 2-4 |
| 4 | Move Legality | 10-15 | 10-15 | 2-3 | 7-12 |
| 5 | LegalityEngine | 8-12 | 8-12 | 2 | 6-10 |
| 6 | TimelineEngine | 2-4 | 2-4 | 1 | 1-3 |
| **TOTAL** | **9 layers** | **~60-80** | **~60-80** | **~16-20** | **~44-60** |

### **Essential Tests (16-20 total)**

**Already Marked (16):**
1. BasicDataFrame_CreateAndCount_Returns3Rows_Refactored
2. PatternFactory_GetPatterns_ReturnsNonEmptyDataFrame_Refactored
3. StandardBoard_GetPerspectives_ContainsSelfAllyFoeFlags_Refactored
4. GetPerspectivesWithThreats_BlackRookAtCorner_MarksThreatenedCells
5. GetPerspectivesWithThreats_IntegrationTest_Refactored
6. SimulateBoardAfterMove_SimpleMove_UpdatesSourceAndDestination_Refactored
7. WhitePawn_AtStartPosition_CanMoveForwardOneSquare_Refactored
8. EmptyBoard_KnightInCenter_Has8Moves_Refactored
9. EmptyBoard_KingInCenter_Has8Moves_Refactored
10. EmptyBoard_BishopInCenter_CanMoveToAdjacentDiagonals_Refactored
11. EmptyBoard_RookInCenter_CanMoveToAdjacentSquares_Refactored
12. EmptyBoard_QueenInCenter_CanMoveToAdjacent8Directions_Refactored
13. WhitePawn_WithPassingBlackPawnAdjacent_HasEnPassantPattern_Refactored
14. KingMint_WithMintRook_CastlingPatternExists_Refactored
15. SequenceMasks_InMaskAndOutMask_CoverAllInAndOutFlags
16. SequenceConversion_OutShiftedRight_EqualsCorrespondingIn

**Add in Future Phases (4-8 more):**
- 1 from Phase 2A (atomic pattern matching smoke test)
- 1 from Phase 2B (threat computation smoke test)
- 1-2 from Phase 2C (sequence expansion smoke test)
- 1 from Phase 4 (en passant OR castling smoke test)
- 1-2 from Phase 5 (king safety smoke test)
- 1 from Phase 6 (timeline orchestration smoke test)

**Execution Profile:**
- `Essential=True` → ~20 tests, ~25-30 seconds
- `Debug=True` → ~62-76 tests, ~1-1.5 minutes  
- `Performance=Fast` → ~140+ tests, ~2 minutes
- No filter → All tests, ~2+ minutes

---

## **Part IV: Risk Mitigation & Success Criteria**

### **Risks & Mitigations**

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Extraction breaks existing tests | High | Parallel testing (orig + refactored) until stable |
| Sequence logic too complex to extract | High | Phase 2C given 2-3 days with flexibility |
| Legality engine unblocks too late | Medium | Can complete Phases 2-4 in parallel |
| Performance regression | Medium | Benchmark each layer before/after |
| Test count explosion (90 → 160+) | Low | Mark with Phase trait for filtering |

### **Success Metrics**

**Code Quality:**
- ✅ All 9 layers extracted with <300 LOC each
- ✅ Zero circular dependencies between layers
- ✅ 100% backward compatibility with ChessPolicy

**Test Coverage:**
- ✅ 16-20 essential tests marked and passing
- ✅ 60-80 total refactored tests marked with `[Trait("Debug", "True")]`
- ✅ 100% pass rate across all phases
- ✅ No regressions from original 144-test suite

**Performance:**
- ✅ Essential tests: ~25-30 seconds (vs. 2 minutes for full suite)
- ✅ Debug tests: ~1-1.5 minutes
- ✅ Full suite: ≤110% of original execution time

**Maintainability:**
- ✅ Each layer testable independently
- ✅ Clear responsibility boundaries
- ✅ Removal of 2000+ line monolith

---

## **Part V: Implementation Timeline**

### **Sprint 1 (Immediate - Complete)**
- ✅ Phase 1: Foundation, Perspectives, Simulation
- ✅ 16 essential tests marked

### **Sprint 2 (Next 3-4 days)**
- Phase 2A: PatternMatcher extraction
- Phase 2B: ThreatEngine extraction
- Add 12-16 new debug tests

### **Sprint 3 (Following 3-4 days)**
- Phase 2C: SequenceEngine extraction
- Phase 3: CandidateGenerator composition
- Add 8-12 new debug tests

### **Sprint 4 (Following 2-3 days)**
- Phase 4: Complete move legality tests
- Phase 5: LegalityEngine extraction
- Add 18-24 new debug tests
- UNBLOCK 7 KingInCheckTests

### **Sprint 5 (Final 1 day)**
- Phase 6: Simplify TimelineEngine
- Final validation and refactoring

**Total Timeline:** ~2-3 weeks to full completion

---

## **Part VI: File Changes Summary**

### **New Files to Create**

```
src/Game.Chess/Policy/
├── Patterns/
│   └── PatternMatcher.cs          (Phase 2A)
├── Threats/
│   └── ThreatEngine.cs            (Phase 2B)
├── Sequences/
│   └── SequenceEngine.cs          (Phase 2C)
├── Candidates/
│   └── CandidateGenerator.cs      (Phase 3)
├── Validation/
│   └── LegalityEngine.cs          (Phase 5)
├── Timeline/
│   └── TimelineEngine.cs          (Phase 6)
└── Examples/
    └── *existing examples*
```

### **Modified Files**

```
src/Game.Chess/History/
└── ChessPolicyB.cs    (TimelineService: delegate to new engines)

src/Game.Chess/Policy/
├── ChessPolicyRefactored.cs  (add new engine methods)
└── README.md                 (update documentation)

tests/Game.Chess.Tests.Integration/
├── *15 existing test files*  (add Phase-specific new tests)
├── *existing refactored tests* (already marked Debug=True)
└── NEW test files as needed for new layers
```

---

## **Part VII: Next Immediate Actions**

**For Next Session (1 hour):**
1. ✅ Review this unified plan with stakeholders
2. Create Phase 2A branch
3. Extract PatternMatcher (~300 LOC from TimelineService)
4. Write 8-10 smoke tests for PatternMatcher
5. Mark tests with `[Trait("Debug", "True"), Trait("Phase", "2A")]`

**Success Criteria for Phase 2A:**
- PatternMatcher extracted to `Game.Chess.Policy.Patterns/`
- All new tests passing
- No regression in existing 144-test suite
- Essential tests still passing (16/16)

---

## **Synthesis: How This Plan Unifies All Three**

This unified plan combines:
- **Plan 1's** architectural clarity (9 well-defined layers)
- **Plan 2's** risk-managed phasing (low→high intrusiveness)
- **Plan 3's** pragmatic test strategy (essential + debug tiers)

**Key Innovation:**
- Refactoring phases align with architectural layers
- Each phase adds both new architecture AND new tests
- Essential tests marked incrementally across phases
- Debug tests provide comprehensive validation at each step
- Risk managed through parallel testing approach

**Why This Works:**
1. **Clear endpoints**: Each phase has extraction, testing, and integration criteria
2. **Risk mitigation**: Can run tests in parallel with original implementation
3. **Fast feedback**: Essential tests enable rapid iteration
4. **Complete coverage**: Debug tests ensure comprehensive validation
5. **Maintainable**: Each layer has clear responsibility and testability

