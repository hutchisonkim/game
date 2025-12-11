# Chess Policy Refactoring: Complete Plan

**Date:** December 9, 2025  
**Status:** Planning Phase  
**Target:** Layered, testable, pattern-driven chess rules engine

---

## Part 1: Chess Rules Registry

This section maps **every standard chess rule** to its:
- Source conditions (what pieces/board state trigger it)
- Destination conditions (what makes it valid)
- Dependencies on other rules
- Responsible policy services

### Category A: Basic Movement Rules

#### A1. Pawn Forward (Normal Move)
- **Source Condition:** `Piece.Self | Piece.Pawn` + `~Piece.Mint` (not unmoved)
- **Destination Condition:** `Piece.Empty`
- **Pattern:** `(0, 1)` forward (relative to piece direction)
- **Behavior:** Single-step, non-recursive
- **Responsible Services:**
  - `PatternRepository` (defines pattern)
  - `PatternMatcher` (matches atomically)
  - `CandidateGenerator` (surfaces as legal move if no pin)
- **Dependencies:** None

#### A2. Pawn Initial Move (Double Step)
- **Source Condition:** `Piece.Self | Piece.MintPawn`
- **Destination Condition:** `Piece.Empty`
- **Pattern:** `(0, 2)` forward
- **Behavior:** Single-step, special case
- **Responsible Services:** Same as A1 + `SimulationEngine` (needs to mark `Passing` flag)
- **Dependencies:** A1 (pawn basic move)

#### A3. Pawn Capture (Diagonal)
- **Source Condition:** `Piece.Self | Piece.Pawn`
- **Destination Condition:** `Piece.Foe` (enemy piece)
- **Pattern:** `(1, 1)` forward diagonal (both Q1 and Q2 variants)
- **Behavior:** Non-repeating capture-only
- **Responsible Services:**
  - `PatternRepository`
  - `PatternMatcher`
  - `CandidateGenerator`
- **Dependencies:** None

#### A4. Bishop Movement (All Directions)
- **Source Condition:** `Piece.Self | Piece.Bishop`
- **Destination Condition:** `Piece.Empty` (recursive to find all squares) + `Piece.Foe` (final capture)
- **Pattern:** `(1, 1)` in all 4 quadrants (8 patterns total: 4 Empty OutF + 4 Foe InF)
- **Behavior:** Sliding (recursive), must not jump pieces
  - **Empty OutF patterns:** Recursive expansion through empty squares (InstantRecursive, no Public flag, internal)
  - **Foe InF patterns:** Capture endpoint (Public flag, terminal move)
- **Responsible Services:**
  - `PatternRepository`
  - `SequenceEngine` (chains OutF→InF for sliding expansion)
  - `CandidateGenerator`
- **Dependencies:** None

#### A5. Rook Movement (Cardinal Directions)
- **Source Condition:** `Piece.Self | Piece.Rook`
- **Destination Condition:** `Piece.Empty` (recursive) + `Piece.Foe` (capture)
- **Pattern:** `(1, 0)` and `(0, 1)` in all directions (8 patterns total: 4 Empty OutI + 4 Foe InI)
- **Behavior:** Sliding, must not jump
  - **Empty OutI patterns:** Recursive with `~Piece.Mint` effect (for castling detection)
  - **Foe InI patterns:** Capture with `~Piece.Mint` effect
- **Responsible Services:**
  - `PatternRepository`
  - `SequenceEngine`
  - `CandidateGenerator`
- **Dependencies:** None

#### A6. Queen Movement (Bishop + Rook)
- **Source Condition:** `Piece.Self | Piece.Queen`
- **Destination Condition:** `Piece.Empty` (recursive) + `Piece.Foe` (capture)
- **Pattern:** Union of bishop `(1,1)` + rook `(1,0)` and `(0,1)` patterns (16 patterns total: 8 Empty + 8 Foe)
- **Behavior:** Sliding, both diagonal and cardinal
  - **Empty patterns:** 4 OutG (bishop) + 4 OutH (rook) for recursive expansion
  - **Foe patterns:** 4 InG (bishop) + 4 InH (rook) for capture
- **Responsible Services:** Same as A4 + A5
- **Dependencies:** A4, A5 (conceptually)

#### A7. Knight Movement (L-Shape)
- **Source Condition:** `Piece.Self | Piece.Knight`
- **Destination Condition:** `Piece.Empty` (move) + `Piece.Foe` (capture)
- **Pattern:** `(2, 1)` and `(1, 2)` in all quadrants (16 patterns total: 8 Empty + 8 Foe)
- **Behavior:** Non-sliding (atomic), ignores intermediate cells
  - Both Empty and Foe patterns use `Sequence.None` (no recursion)
  - Direct move or capture in single step
- **Responsible Services:**
  - `PatternRepository`
  - `PatternMatcher`
  - `CandidateGenerator`
- **Dependencies:** None

#### A8. King Movement (One Square)
- **Source Condition:** `Piece.Self | Piece.King`
- **Destination Condition:** `Piece.Empty` (move) + `Piece.Foe` (capture), both safe (not threatened)
- **Pattern:** All 8 directions (cardinal + diagonal), distance 1 (16 patterns total: 8 Empty + 8 Foe)
- **Behavior:** Atomic, single-step, **cannot move to threatened square**
  - 4 Empty patterns for rook-like moves with `~Piece.Mint` effect
  - 4 Empty patterns for bishop-like moves with `~Piece.Mint` effect
  - 4 Foe patterns for rook-like capture with `~Piece.Mint` effect
  - 4 Foe patterns for bishop-like capture with `~Piece.Mint` effect
- **Responsible Services:**
  - `PatternRepository`
  - `PatternMatcher`
  - `CandidateGenerator`
  - `LegalityEngine` (validates king safety via `ThreatEngine`)
- **Dependencies:** A3.2 (threat detection)

---

### Category B: Special Move Rules

#### B1. Pawn Promotion
- **Source Condition:** Pawn reaches opposite edge
- **Destination Condition:** Any of 4 promotion pieces (Knight, Bishop, Rook, Queen)
- **Pattern:** Triggered at row 7 (for white) or row 0 (for black)
- **Behavior:** 
  - Sequence phase: detect promotion trigger
  - Instant mandatory phase: execute promotion
- **Responsible Services:**
  - `PatternRepository` (defines 4 separate patterns for 4 promotions)
  - `SequenceEngine` (InAB_OutC logic)
  - `SimulationEngine` (applies piece transformation)
- **Dependencies:** A1, A3 (pawn moves that can trigger promotion)

#### B2. Castling (Kingside)
- **Source Condition:** 
  - White: `MintKing` at (4, 0) + `MintRook` at (7, 0)
  - Black: `MintKing` at (4, 7) + `MintRook` at (7, 7)
  - King has never moved (Mint flag)
  - No pieces between them
  - King not in check
  - King doesn't move through check
  - Rook not in check
- **Destination Condition:** King to (6, y), Rook to (5, y)
- **Pattern:** Parallel sequence with two actors
- **Behavior:** 
  - OutD (pre-move safety check)
  - InD (parallel movement execution)
  - Must validate intermediate cells are not threatened
- **Responsible Services:**
  - `PatternRepository` (Variant2 patterns)
  - `SequenceEngine` (parallel sequencing)
  - `SimulationEngine` (atomic swap of king+rook)
  - `LegalityEngine` (validates no-check-through requirement)
- **Dependencies:** A8, A3.2 (king safety)

#### B3. Castling (Queenside)
- Same as B2 but for kingside with different coordinates
- **Dependencies:** B2

#### B4. En Passant (French: "in passing")
- **Source Condition:** 
  - Self: `Piece.Pawn` at row 4 (white) or 3 (black)
  - Adjacent: `Piece.PassingFoe` (foe pawn that just moved 2 squares)
- **Destination Condition:** `Piece.Empty` diagonal forward
- **Pattern:** Pair sequence:
  - OutE: detect adjacent passing foe
  - InE: move forward to empty square
- **Behavior:** 
  - Candidate includes capture of passing foe
  - Simulation removes the passing foe from the side, not forward
- **Responsible Services:**
  - `PatternRepository` (OutE/InE patterns)
  - `SequenceEngine` (pair sequencing)
  - `SimulationEngine` (removes foe from side)
  - `LegalityEngine` (validates move doesn't expose king)
- **Dependencies:** A2 (pawn double step sets Passing flag)

#### B5. Passing Flag Reset
- **Source Condition:** Any piece on any square
- **Destination Condition:** Reset Passing flag after each opponent turn
- **Pattern:** `(0, 0)` special pattern
- **Behavior:** Instant mandatory, not a real move
- **Responsible Services:**
  - `PatternRepository`
  - `SequenceEngine`
  - `SimulationEngine` (applies bitwise AND with ~Passing)
- **Dependencies:** B4 (En passant relies on passing flag)

---

### Category C: Legality & Constraint Rules

#### C1. King Safety (Cannot Move Into Check)
- **Condition:** After any move, the king must not be in a threatened square
- **Mechanism:**
  1. Simulate the candidate move
  2. Compute opponent's threatened cells in simulated state
  3. Check if player's king is in threatened cell
  4. Reject if true
- **Responsible Services:**
  - `SimulationEngine` (simulates move)
  - `ThreatEngine` (computes threats in simulated board)
  - `LegalityEngine` (orchestrates validation)
- **Dependencies:** None (foundational)

#### C2. Pin Detection (Indirect King Attack)
- **Condition:** A piece cannot move if doing so exposes the king to attack along a line
- **Mechanism:**
  1. Compute lines from king through each ally piece to opponent's pieces
  2. If simulating the move breaks the king's protection on that line, reject
- **Responsible Services:**
  - `ThreatEngine` (identifies pinning attackers)
  - `SimulationEngine` (tests hypothetical move)
  - `LegalityEngine` (filters by pin)
- **Dependencies:** C1

#### C3. Discovered Check (Moving Exposes King)
- **Condition:** A piece cannot move if it was blocking an attack on the king
- **Mechanism:** Similar to C2 but from behind the moving piece
- **Responsible Services:** Same as C2
- **Dependencies:** C1

#### C4. No Friendly Fire
- **Condition:** A piece cannot capture its own side's pieces (unless explicitly testing scenarios)
- **Mechanism:**
  - Pattern matching filters dst_conditions with `Piece.Foe`
  - If `includeFriendlyfire = false`, skip moves to allied pieces
- **Responsible Services:**
  - `PatternMatcher` (dst_conditions filtering)
  - `CandidateGenerator` (respects filter flag)
- **Dependencies:** None

#### C5. Board Boundaries
- **Condition:** No moves outside the 8×8 board
- **Mechanism:** Position validation in pattern expansion
- **Responsible Services:**
  - `PatternMatcher` (computes dst_x / dst_y and rejects out of bounds)
  - `SequenceEngine` (breaks sliding on edge)
- **Dependencies:** None

---

### Category D: Terminal Conditions

#### D1. Checkmate
- **Condition:** King is in check AND no legal moves exist that leave king safe
- **Mechanism:**
  1. Compute all candidate moves
  2. Filter by `LegalityEngine.FilterMovesLeavingKingInCheck()`
  3. If result is empty, checkmate
- **Responsible Services:**
  - `CandidateGenerator` (all moves)
  - `LegalityEngine` (legality filter)
  - `TimelineEngine` (game-end logic)
- **Dependencies:** C1, C2, C3

#### D2. Stalemate
- **Condition:** King is NOT in check BUT no legal moves exist
- **Mechanism:** Same as D1 but check first that king is safe
- **Responsible Services:** Same as D1
- **Dependencies:** C1, C2, C3

#### D3. Three-Fold Repetition (Optional)
- **Condition:** Same board position occurs 3 times
- **Mechanism:** Track board history, compare hash
- **Responsible Services:** Game state manager (outside policy engine)
- **Dependencies:** None

#### D4. 50-Move Rule (Optional)
- **Condition:** 50 moves without pawn move or capture
- **Mechanism:** Track move counter, reset on pawn move or capture
- **Responsible Services:** Game state manager
- **Dependencies:** None

---

## Part 2: Tech Tree

The **Tech Tree** defines which services must exist and in what order to support each chess rule.

```
Level 0: FOUNDATION (Prerequisite)
├── Service: BoardStateProvider
│   └── Responsibility: Convert board array → DataFrame with position/piece/color
│   └── Tests: [L0_Foundation_BoardStateProvider]
│
├── Service: PatternRepository
│   └── Responsibility: Load & cache all 60+ movement patterns
│   └── Tests: [L0_Foundation_PatternRepository]
│
└── Service: PerspectiveEngine
    └── Responsibility: Convert pieces → Self/Ally/Foe views
    └── Tests: [L0_Foundation_PerspectiveEngine]

Level 1: ATOMIC MOVES (Single-Step)
├── Service: PatternMatcher
│   └── Responsibility: Apply one pattern to one piece → candidates
│   └── Supports: A1, A3, A7, A8 (non-sliding moves)
│   └── Tests: [L1_PatternMatcher_Pawn, L1_PatternMatcher_Knight, L1_PatternMatcher_King]
│
└── Service: CandidateGenerator (Atomic Mode)
    └── Responsibility: Collect all atomic matches for all pieces
    └── Supports: A1, A3, A7, A8
    └── Tests: [L1_CandidateGenerator_SimpleMoves]

Level 2: SLIDING MOVES
├── Service: SequenceEngine
│   └── Responsibility: Expand patterns recursively, stop at blocker
│   └── Supports: A4, A5, A6 (sliding pieces)
│   └── Tests: [L2_SequenceEngine_BishopSliding, L2_SequenceEngine_RookSliding]
│
└── Service: CandidateGenerator (With Sequences)
    └── Responsibility: Merge atomic + sliding candidates
    └── Supports: A4, A5, A6 + A1–A8
    └── Tests: [L2_CandidateGenerator_SlidingMoves]

Level 3: SIMULATION
├── Service: SimulationEngine
│   └── Responsibility: Apply a move → new board state (pieces + flags)
│   └── Supports: All moves (A1–B5)
│   └── Tests: [L3_SimulationEngine_SimpleMoves, L3_SimulationEngine_FlagUpdates]
│
└── Enables: Forward simulation for threat detection & legality checks

Level 4: THREATS
├── Service: ThreatEngine
│   └── Responsibility: Compute all cells threatened by opponent
│   └── Uses: CandidateGenerator + SimulationEngine
│   └── Supports: C1, C2, C3, A8 (king safety)
│   └── Tests: [L4_ThreatEngine_BasicThreats, L4_ThreatEngine_SlidingThreats]
│
└── Key insight: Threat = opponent can move to that square (ignoring "Public" tag)

Level 5: SPECIAL MOVES (Promotion, Castling, En Passant)
├── Service: SequenceEngine (Extended)
│   └── Responsibility: Handle multi-phase patterns (OutX → InX)
│   └── Supports: B1 (promotion), B2–B3 (castling), B4 (en passant)
│   └── Tests: [L5_SpecialMoves_PawnPromotion, L5_SpecialMoves_Castling, L5_SpecialMoves_EnPassant]
│
└── Service: SimulationEngine (Extended)
    └── Responsibility: Apply piece transformations & flag updates
    └── Supports: B1–B5
    └── Tests: [L5_Simulation_Promotion, L5_Simulation_FlagReset]

Level 6: LEGALITY (King Safety Validation)
├── Service: LegalityEngine
│   └── Responsibility: Filter moves that leave king in check
│   └── Uses: SimulationEngine + ThreatEngine + CandidateGenerator
│   └── Supports: C1, C2, C3 (all legality constraints)
│   └── Tests: [L6_Legality_KingSafety, L6_Legality_PinDetection, L6_Legality_DiscoveredCheck]
│
└── Phase 5 of original plan → critical for correctness

Level 7: TIMELINE (Game Tree)
├── Service: TimelineEngine
│   └── Responsibility: Orchestrate: Candidates → Legality → Simulation → Threats → Next Perspectives
│   └── Supports: Recursive game tree building
│   └── Tests: [L7_Timeline_SingleTurn, L7_Timeline_MultiTurn, L7_Timeline_DepthCutoff]
│
└── Phase 6 of original plan → orchestrator

Level 8: TERMINAL CONDITIONS (Game End)
├── Service: GameStateAnalyzer (new, thin wrapper)
│   └── Responsibility: Detect checkmate, stalemate, draw conditions
│   └── Supports: D1, D2, D3, D4
│   └── Tests: [L8_Terminal_Checkmate, L8_Terminal_Stalemate]
│
└── Uses all previous layers
```

---

## Part 3: Test Strategy & Traits

We use **xUnit Traits** to:
1. Mark tests as **Essential** (must pass for CI)
2. Declare **Dependencies** on other tests (parent must pass before running child)
3. Organize by **Category** (Foundation, Atomic, Sliding, etc.)

### xUnit Trait System

#### Trait 1: `[Trait("Essential", "true")]`
- Tests critical for the refactoring progress
- Always run in CI
- Examples: `L0_Foundation_*`, `L1_PatternMatcher_*`, `L6_Legality_*`

#### Trait 2: `[Trait("DependsOn", "parent_test_name")]`
- Declares that this test requires another test to pass first
- Allows intelligent skip/retry on failure
- Examples:
  ```csharp
  [Trait("Essential", "true")]
  [Trait("DependsOn", "L0_Foundation_BoardStateProvider")]
  public void L1_PatternMatcher_Pawn() { ... }
  ```

#### Trait 3: `[Trait("Category", "Foundation|Atomic|Sliding|Simulation|Threats|SpecialMoves|Legality|Timeline|Terminal")]`
- Groups tests by architectural layer
- Enables running all tests for a layer
- Examples: `[Trait("Category", "Simulation")]`

#### Trait 4: `[Trait("ChessRule", "rule_id")]`
- Links test to rule in the Chess Rules Registry
- Examples: `[Trait("ChessRule", "A1")]` for pawn forward

### Essential Test Suite

The **Essential=true** tests are the critical path for refactoring validation:

| Test ID | Rule(s) | Dependency | Purpose |
|---------|---------|-----------|---------|
| `L0_Foundation_BoardStateProvider` | — | None | Verify board → DataFrame conversion |
| `L0_Foundation_PatternRepository` | — | None | Verify pattern table loads correctly |
| `L0_Foundation_PerspectiveEngine` | — | L0_Foundation_BoardStateProvider | Verify Self/Ally/Foe assignment |
| `L1_PatternMatcher_Pawn` | A1, A3 | L0_Foundation_PerspectiveEngine | Verify atomic pawn moves |
| `L1_PatternMatcher_Knight` | A7 | L0_Foundation_PerspectiveEngine | Verify knight L-shape moves |
| `L1_PatternMatcher_King` | A8 | L0_Foundation_PerspectiveEngine | Verify king 1-square moves |
| `L1_CandidateGenerator_SimpleMoves` | A1, A3, A7, A8 | L1_PatternMatcher_* | Verify candidates collected correctly |
| `L2_SequenceEngine_BishopSliding` | A4 | L1_CandidateGenerator_SimpleMoves | Verify bishop diagonal sliding with blockers |
| `L2_SequenceEngine_RookSliding` | A5 | L1_CandidateGenerator_SimpleMoves | Verify rook cardinal sliding |
| `L2_CandidateGenerator_SlidingMoves` | A4, A5, A6 | L2_SequenceEngine_* | Verify atomic + sliding merge |
| `L3_SimulationEngine_SimpleMoves` | A1, A3, A7 | L1_CandidateGenerator_SimpleMoves | Verify board state after simple move |
| `L3_SimulationEngine_FlagUpdates` | A2, B5 | L3_SimulationEngine_SimpleMoves | Verify Mint, Passing flags updated |
| `L4_ThreatEngine_BasicThreats` | C1 | L3_SimulationEngine_SimpleMoves | Verify threatened cells detected |
| `L4_ThreatEngine_SlidingThreats` | C1 | L2_SequenceEngine_* | Verify sliding piece threats |
| `L5_SpecialMoves_Castling` | B2, B3 | L3_SimulationEngine_* | Verify castling legality & king/rook movement |
| `L5_SpecialMoves_EnPassant` | B4 | L3_SimulationEngine_FlagUpdates | Verify en passant capture & flag interaction |
| `L5_SpecialMoves_PawnPromotion` | B1 | L3_SimulationEngine_* | Verify pawn → promoted piece transformation |
| `L6_Legality_KingSafety` | C1 | L4_ThreatEngine_BasicThreats | Verify king can't move to threatened square |
| `L6_Legality_PinDetection` | C2 | L4_ThreatEngine_SlidingThreats | Verify pinned pieces can't move |
| `L6_Legality_DiscoveredCheck` | C3 | L4_ThreatEngine_SlidingThreats | Verify moving piece blocking attack is rejected |
| `L7_Timeline_SingleTurn` | — | L6_Legality_* | Verify timeline builds 1 turn correctly |
| `L7_Timeline_MultiTurn` | — | L7_Timeline_SingleTurn | Verify timeline builds N turns with correct alternation |
| `L8_Terminal_Checkmate` | D1 | L7_Timeline_MultiTurn | Verify checkmate detected |
| `L8_Terminal_Stalemate` | D2 | L7_Timeline_MultiTurn | Verify stalemate detected |

---

## Part 4: Integration Test Structure

### Failing Test Troubleshooting Strategy

When a test fails, use the **DependsOn** trait to isolate the root cause:

1. **Test fails: `L6_Legality_KingSafety`**
   - Check parents: `L4_ThreatEngine_BasicThreats`, `L3_SimulationEngine_SimpleMoves`
   - If parent fails → fix that service first
   - If parent passes → bug is in `LegalityEngine` itself

2. **Test fails: `L2_SequenceEngine_BishopSliding`**
   - Check parent: `L1_CandidateGenerator_SimpleMoves`
   - Check dependency: `L0_Foundation_PatternRepository` (must have bishop patterns)
   - If parent/dependency fails → fix that first
   - Otherwise → bug in `SequenceEngine` sliding logic

3. **Test fails: `L7_Timeline_MultiTurn`**
   - Parent: `L7_Timeline_SingleTurn` must pass
   - Recursively check all L6 legality tests
   - If those pass → bug is in `TimelineEngine` orchestration logic

### Running Tests by Category

```powershell
# Run all Foundation tests
dotnet test --filter "Category=Foundation" --logger:"console;verbosity=detailed"

# Run all Essential tests
dotnet test --filter "Essential=true" --logger:"console;verbosity=detailed"

# Run a specific rule's tests
dotnet test --filter "ChessRule=A1" --logger:"console;verbosity=detailed"

# Run with dependency awareness (pseudo-code, implemented as custom runner)
./run-tests-with-dependencies.ps1 -Filter "Category=Sliding"
```

### Test Failure Investigation Flow

```
Test Fails
├─ Is parent test passing?
│  ├─ NO → Fix parent service first
│  └─ YES ↓
├─ What category? (use Category trait)
│  ├─ Foundation → Debug BoardStateProvider / PatternRepository / PerspectiveEngine
│  ├─ Atomic → Debug PatternMatcher
│  ├─ Sliding → Debug SequenceEngine
│  ├─ Simulation → Debug SimulationEngine
│  ├─ Threats → Debug ThreatEngine
│  ├─ SpecialMoves → Debug SequenceEngine + SimulationEngine
│  ├─ Legality → Debug LegalityEngine (using ThreatEngine + SimulationEngine)
│  └─ Timeline → Debug TimelineEngine orchestration
└─ Write minimal reproduction test targeting the failing service
```

---

## Part 5: JSON Configuration Files

See: `ChessRulesRegistry.json`, `TestDependencyGraph.json`, `TechTree.json` (generated alongside this document)

---

## Next Steps

1. **Create test infrastructure** that reads `TestDependencyGraph.json` and:
   - Executes tests in dependency order
   - Skips tests whose parents failed
   - Reports which parent failure caused which child skip

2. **Implement Essential tests** for each layer in order:
   - Layer 0 (Foundation)
   - Layer 1 (Atomic)
   - Layer 2 (Sliding)
   - Layer 3 (Simulation)
   - Layer 4 (Threats)
   - Layer 5 (Special Moves)
   - Layer 6 (Legality)
   - Layer 7 (Timeline)
   - Layer 8 (Terminal Conditions)

3. **Measure progress** by counting passing Essential tests → tech tree completion percentage

4. **Parallel development**: Once Foundation (L0) is solid, develop L1–L7 in parallel by feature area (Atomic, Sliding, etc.)
