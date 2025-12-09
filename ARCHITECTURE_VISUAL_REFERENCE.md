# Chess Policy Architecture: Visual Reference

**Version:** 1.0  
**Date:** December 9, 2025

---

## Architecture Layers Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                          CHESS POLICY REFACTORED                   │
│                    (Clean Layered Architecture)                    │
└─────────────────────────────────────────────────────────────────────┘

                            LAYER 8
                    ┌──────────────────────┐
                    │ GameStateAnalyzer    │ ← Terminal Conditions
                    │ (D1, D2, D3, D4)     │   Checkmate, Stalemate
                    └──────────────────────┘
                             △
                             │
                            LAYER 7
                    ┌──────────────────────┐
                    │  TimelineEngine      │ ← Game Tree Orchestration
                    │  (Phase 6)           │   Recursive move generation
                    └──────────────────────┘
                             △
                    ┌────────┴────────┐
                    │                 │
                  LAYER 6        (Parallel)
         ┌──────────────────┐
         │ LegalityEngine   │  ← King Safety Validation
         │ (C1, C2, C3)     │    Pin detection
         └──────────────────┘    Discovered check
                  △
         ┌────────┴────────┐
         │                 │
       LAYER 5        (Parallel)
   ┌──────────────┐   ┌──────────────────────┐
   │SequenceEngine│   │ SimulationEngine     │
   │  Extended    │   │ (Extended)           │
   │ (B1,B2,B3,   │   │ (B1,B2,B3,B4,B5)     │
   │  B4,B5)      │   │ Special move effects │
   └──────────────┘   └──────────────────────┘
         △                      △
         └────────┬─────────────┘
                  │
                LAYER 4
         ┌──────────────────────┐
         │   ThreatEngine       │ ← Threat Detection
         │   (C1, C2, C3)       │   Opponent's moves
         └──────────────────────┘
                  △
                  │
                LAYER 3
         ┌──────────────────────┐
         │  SimulationEngine    │ ← Apply Moves
         │  (A1..A8, B1..B5)    │   Update board state
         └──────────────────────┘
                  △
                  │
                LAYER 2
         ┌──────────────────────┐
         │  SequenceEngine      │ ← Sliding Expansion
         │  (A4, A5, A6)        │   Recursive patterns
         └──────────────────────┘
                  △
                  │
                LAYER 1
    ┌────────────────────────────┐
    │ CandidateGenerator         │ ← Move Collection
    │ PatternMatcher             │   All atomic moves
    │ (A1, A3, A7, A8)           │   + sliding moves
    └────────────────────────────┘
                  △
                  │
                LAYER 0
    ┌────────────────────────────────────┐
    │ Foundation                         │ ← Static Data
    │ ├─ BoardStateProvider              │   Board arrays
    │ ├─ PatternRepository (60+ patterns)│   Pattern tables
    │ └─ PerspectiveEngine               │   Self/Ally/Foe
    └────────────────────────────────────┘

  Legend:
  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  △     = depends on
  ├─    = contains
  ──    = data flow
```

---

## Service Dependency Graph

```
                    ┌─────────────────┐
                    │  GameStateA...  │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │ TimelineEngine  │
            ┌───────┴────────┬────────┴───────┐
            │                │                │
    ┌───────▼────────┐  ┌────▼──────────┐ ┌──▼──────────────┐
    │LegalityEngine  │  │Threats        │ │CandidateGen     │
    └───────┬────────┘  │(multi-turn)   │ └─────┬───────────┘
            │           └────┬──────────┘       │
            │                │                  │
    ┌───────▼────────────────▼──────────┬───────▼───────────┐
    │ SimulationEngine                  │ SequenceEngine    │
    │ - Apply moves                     │ - Sliding expansion
    │ - Update flags                    │ - Multi-phase seq │
    └───────┬────────────────┬──────────┴───────┬───────────┘
            │                │                  │
    ┌───────▼────────────────▼──────────┬───────▼───────────┐
    │ CandidateGenerator                │ PatternMatcher    │
    │ - Collect all moves               │ - Apply patterns  │
    │ - Atomic + sliding                │ - Single moves    │
    └───────┬────────────────┬──────────┴───────┬───────────┘
            │                │                  │
            └────────────────┼──────────────────┘
                             │
                    ┌────────▼────────┐
                    │PerspectiveEngine│
                    └────────┬────────┘
                             │
                ┌────────────┼────────────┐
         ┌──────▼──────┐  ┌──▼─────────┐ │
         │ BoardState  │  │ Pattern    │ │
         │ Provider    │  │ Repository │ │
         └─────────────┘  └────────────┘ │
                                 (immutable)
```

---

## Data Flow: Computing Legal Moves

```
INPUT: Board State + Faction
  │
  ▼
┌──────────────────────────────┐
│ BoardStateProvider           │
│ Convert array → DataFrame    │
└──────────────────────────────┘
  │
  ▼
┌──────────────────────────────┐
│ PerspectiveEngine            │
│ Assign Self/Ally/Foe attrs   │
└──────────────────────────────┘
  │
  ▼
┌──────────────────────────────┐
│ CandidateGenerator           │
│ ├─ PatternMatcher (atomic)   │
│ └─ SequenceEngine (sliding)  │
│ → All possible moves         │
└──────────────────────────────┘
  │
  ▼
┌──────────────────────────────┐
│ SimulationEngine             │
│ Apply each candidate move    │
│ → Simulated board states     │
└──────────────────────────────┘
  │
  ▼
┌──────────────────────────────┐
│ ThreatEngine                 │
│ Compute threats in simulated │
│ board after each move        │
└──────────────────────────────┘
  │
  ▼
┌──────────────────────────────┐
│ LegalityEngine               │
│ Filter: move leaves king safe│
│ → LEGAL MOVES ONLY           │
└──────────────────────────────┘
  │
  ▼
OUTPUT: DataFrame of Legal Moves
```

---

## Pattern Application Pipeline

```
Pattern Processing Pipeline
═══════════════════════════════════════════════════════════

INPUT: Pattern (src_conditions, dst_conditions, delta, sequence, effects)
  │
  ▼
┌─────────────────────────────────────────────────────────┐
│ PHASE 1: Pattern Matching (PatternMatcher)              │
│ ─────────────────────────────────────────────────────   │
│ 1. Filter pieces where src_conditions match             │
│ 2. Compute dst_x = src_x + delta_x                      │
│ 3. Compute dst_y = src_y + delta_y                      │
│ 4. Check bounds (0-7)                                   │
│ 5. Fetch dst piece                                      │
│ 6. Check if dst_conditions match                        │
│ 7. YIELD: Single-step candidate                         │
└─────────────────────────────────────────────────────────┘
  │
  ├─→ [Stop: non-sliding move]
  │
  ├─→ [Continue: pattern.Repeats == true] → PHASE 2
  │
  ▼
┌─────────────────────────────────────────────────────────┐
│ PHASE 2: Sequence Expansion (SequenceEngine)            │
│ ─────────────────────────────────────────────────────   │
│ IF pattern.Sequence has OutX flag:                      │
│   1. Generate expansion candidates (recursive)          │
│   2. For each expanded candidate:                       │
│      - If it has InX matching: apply it                 │
│      - Stop sliding at first blocker                    │
│      - YIELD: Continuation candidate                    │
│                                                         │
│ IF pattern.Sequence has OutX → InX pair:               │
│   1. First phase uses OutX (pre-move checks)            │
│   2. Second phase uses InX (actual execution)           │
└─────────────────────────────────────────────────────────┘
  │
  ▼
OUTPUT: All valid moves from this pattern + piece
```

---

## Test Dependency Hierarchy

```
LAYER 0: FOUNDATION (No dependencies)
──────────────────────────────────────
  L0_Foundation_BoardStateProvider ← (foundation)
  L0_Foundation_PatternRepository ← (foundation)
  L0_Foundation_PerspectiveEngine ← depends on L0_BoardStateProvider

                     ↓
                     
LAYER 1: ATOMIC MOVES
──────────────────────────────────────
  L1_PatternMatcher_Pawn ← depends on L0_Perspective + L0_PatternRepository
  L1_PatternMatcher_Knight ← depends on L0_Perspective + L0_PatternRepository
  L1_PatternMatcher_King ← depends on L0_Perspective + L0_PatternRepository
  L1_CandidateGenerator_SimpleMoves ← depends on L1_PatternMatcher_*

                     ↓
                     
LAYER 2: SLIDING MOVES (Parallel to Layer 3)
──────────────────────────────────────
  L2_SequenceEngine_BishopSliding ← depends on L1_CandidateGenerator
  L2_SequenceEngine_RookSliding ← depends on L1_CandidateGenerator
  L2_CandidateGenerator_SlidingMoves ← depends on L2_SequenceEngine_*

                     ↓
                     
LAYER 3: SIMULATION
──────────────────────────────────────
  L3_SimulationEngine_SimpleMoves ← depends on L2_CandidateGenerator
  L3_SimulationEngine_FlagUpdates ← depends on L3_SimpleMoves

                     ↓
                     
LAYER 4: THREATS
──────────────────────────────────────
  L4_ThreatEngine_BasicThreats ← depends on L3_SimpleMoves
  L4_ThreatEngine_SlidingThreats ← depends on L2_SequenceEngine_*

                     ↓
                     
LAYER 5: SPECIAL MOVES (Parallel)
──────────────────────────────────────
  L5_SpecialMoves_Castling ← depends on L3_FlagUpdates + L4_SlidingThreats
  L5_SpecialMoves_EnPassant ← depends on L3_FlagUpdates
  L5_SpecialMoves_PawnPromotion ← depends on L3_SimpleMoves

                     ↓
                     
LAYER 6: LEGALITY (Parallel: 3 sub-tests)
──────────────────────────────────────
  L6_Legality_KingSafety ← depends on L4_BasicThreats + L3_SimpleMoves
  L6_Legality_PinDetection ← depends on L4_SlidingThreats
  L6_Legality_DiscoveredCheck ← depends on L4_SlidingThreats

                     ↓
                     
LAYER 7: TIMELINE (Parallel: 3 sub-tests)
──────────────────────────────────────
  L7_Timeline_SingleTurn ← depends on L6_Legality_*
  L7_Timeline_MultiTurn ← depends on L7_SingleTurn
  L7_Timeline_DepthCutoff ← depends on L7_SingleTurn

                     ↓
                     
LAYER 8: TERMINAL (Parallel)
──────────────────────────────────────
  L8_Terminal_Checkmate ← depends on L7_MultiTurn
  L8_Terminal_Stalemate ← depends on L7_MultiTurn

Legend: ← = test dependency
```

---

## Chess Rules Matrix

```
BASIC MOVEMENT
═══════════════════════════════════════════════════════════════════
Rule  Piece    Movement         Captures  Repeats  Service Chain
─────────────────────────────────────────────────────────────────
A1    Pawn     (0,1) forward    NO        NO       PatternMatcher
A2    Pawn     (0,2) initial    NO        NO       PatternMatcher + Sim
A3    Pawn     (1,1) diagonal   YES       NO       PatternMatcher
A4    Bishop   (1,1) all dirs   YES       YES      SequenceEngine
A5    Rook     (1,0)/(0,1)      YES       YES      SequenceEngine
A6    Queen    A4 + A5          YES       YES      SequenceEngine
A7    Knight   (2,1)/(1,2)      YES       NO       PatternMatcher
A8    King     (1,*) all dirs   YES       NO       PatternMatcher

SPECIAL MOVES
═══════════════════════════════════════════════════════════════════
Rule  Type           Conditions              Service Chain
─────────────────────────────────────────────────────────────────
B1    Promotion      Pawn at edge            SequenceEngine + Sim
B2    Castling (K)   King/Rook unmoved       SequenceEngine + Legality
B3    Castling (Q)   King/Rook unmoved       SequenceEngine + Legality
B4    En Passant     Adjacent passing pawn  SequenceEngine + Sim
B5    Flag Reset     End of turn             SequenceEngine + Sim

LEGALITY
═══════════════════════════════════════════════════════════════════
Rule  Name                  Service Chain
─────────────────────────────────────────────────────────────────
C1    King Safety           ThreatEngine + Sim + Legality
C2    Pin Detection         ThreatEngine + Legality
C3    Discovered Check      ThreatEngine + Legality
C4    No Friendly Fire      PatternMatcher
C5    Board Boundaries      PatternMatcher + SequenceEngine

TERMINAL
═══════════════════════════════════════════════════════════════════
Rule  Condition           Service Chain
─────────────────────────────────────────────────────────────────
D1    Checkmate           CandidateGenerator + Legality + GameStateA...
D2    Stalemate           CandidateGenerator + Legality + GameStateA...
D3    3-Fold Repetition   GameStateAnalyzer
D4    50-Move Rule        GameStateAnalyzer
```

---

## Critical Path Timeline

```
WEEK 1
├─ L0_Foundation_BoardStateProvider ✓
├─ L0_Foundation_PatternRepository ✓
└─ L0_Foundation_PerspectiveEngine ✓

WEEK 2
├─ L1_PatternMatcher_Pawn ✓
├─ L1_PatternMatcher_Knight ✓
├─ L1_PatternMatcher_King ✓
└─ L1_CandidateGenerator_SimpleMoves ✓

WEEK 3
├─ L2_SequenceEngine_BishopSliding ✓
├─ L2_SequenceEngine_RookSliding ✓
└─ L2_CandidateGenerator_SlidingMoves ✓

WEEK 4
├─ L3_SimulationEngine_SimpleMoves ✓
└─ L3_SimulationEngine_FlagUpdates ✓

WEEK 5-6
├─ L4_ThreatEngine_BasicThreats ✓
└─ L4_ThreatEngine_SlidingThreats ✓

WEEK 5-6 (PARALLEL)
├─ L5_SpecialMoves_Castling ✓
├─ L5_SpecialMoves_EnPassant ✓
└─ L5_SpecialMoves_PawnPromotion ✓

WEEK 7
├─ L6_Legality_KingSafety ✓
├─ L6_Legality_PinDetection ✓
└─ L6_Legality_DiscoveredCheck ✓

WEEK 8
├─ L7_Timeline_SingleTurn ✓
├─ L7_Timeline_MultiTurn ✓
└─ L7_Timeline_DepthCutoff ✓

WEEK 9
├─ L8_Terminal_Checkmate ✓
└─ L8_Terminal_Stalemate ✓

TOTAL: 9-10 weeks (vs. 17 weeks if fully sequential)
```

---

## Service Responsibility Matrix

```
Service              │ Layers │ Rules  │ Tests │ Est. LOC
─────────────────────┼────────┼────────┼───────┼─────────
BoardStateProvider   │   0    │   -    │   1   │   50
PatternRepository    │   0    │   -    │   1   │  100
PerspectiveEngine    │   0    │   -    │   1   │   75
                     │        │        │       │
PatternMatcher       │   1    │ A1,A3  │   3   │  150
CandidateGenerator   │  1-2   │ A1..A8 │   2   │  100
                     │        │        │       │
SequenceEngine       │   2,5  │ A4..B5 │   5   │  300
SimulationEngine     │   3,5  │ A1..B5 │   2   │  200
                     │        │        │       │
ThreatEngine         │   4    │ C1..C3 │   2   │  200
LegalityEngine       │   6    │ C1..C3 │   3   │  250
                     │        │        │       │
TimelineEngine       │   7    │   -    │   3   │  200
GameStateAnalyzer    │   8    │ D1..D4 │   2   │  150
                     │        │        │       │
TOTALS               │   8    │  20    │  24   │ 1,775

Notes:
- LOC = Estimated Lines of Code per service
- Tests = Number of Essential tests
- Rules = Chess rules supported
```

---

## Execution Flow Example: White Pawn Moves

```
Board State: White pawn at (4, 1)
Faction: White
═══════════════════════════════════════════════════════════════════

1. BoardStateProvider
   Input: 8×8 board array
   Output: DataFrame [x=4, y=1, piece=Pawn, color=White]

2. PerspectiveEngine
   Input: Pawn DataFrame
   Output: [x=4, y=1, piece=Pawn, Self=true, Ally=true, Foe=false]

3. CandidateGenerator
   └─ PatternMatcher
      Pattern A1: (0,1) forward
      ├─ dst_x=4, dst_y=2
      ├─ dst_piece=Empty
      ├─ Matches! → Candidate (4,1)→(4,2)
      │
      Pattern A2: (0,2) initial  
      ├─ Pawn has Mint flag
      ├─ dst_x=4, dst_y=3
      ├─ dst_piece=Empty
      ├─ Matches! → Candidate (4,1)→(4,3)
      │
      Pattern A3a: (1,1) diagonal capture (Q1)
      ├─ dst_x=5, dst_y=2
      ├─ dst_piece=Black Pawn
      ├─ Matches! → Candidate (4,1)→(5,2)
      │
      Pattern A3b: (1,1) diagonal capture (Q2)
      ├─ dst_x=3, dst_y=2
      ├─ dst_piece=Empty
      ├─ NO match (needs Foe)
      │
      YIELD: 3 candidates
   
4. SimulationEngine
   For each candidate:
   ├─ Candidate (4,1)→(4,2)
   │  Output: Pawn at (4,2), square (4,1) empty
   │
   ├─ Candidate (4,1)→(4,3)
   │  Output: Pawn at (4,3), square (4,1) empty, Mint flag removed
   │
   └─ Candidate (4,1)→(5,2)
      Output: Pawn at (5,2), Black piece removed, square (4,1) empty

5. ThreatEngine
   For each simulated board:
   ├─ After (4,1)→(4,2): What squares does Black threaten?
   │  (Check if pawn at (4,2) is safe from Black's pieces)
   │
   ├─ After (4,1)→(4,3): What squares does Black threaten?
   │  (Check if pawn at (4,3) is safe)
   │
   └─ After (4,1)→(5,2): What squares does Black threaten?
      (Check if pawn at (5,2) is safe from Black's pieces)

6. LegalityEngine
   For each candidate:
   ├─ Is White's King safe after this move?
   ├─ Is any piece pinned and violating pin constraint?
   ├─ Does this move expose King to discovered check?
   │
   └─ FILTER: Keep only moves where King is safe
   
7. Output: Legal White Pawn Moves
   ├─ (4,1) → (4,2)    [Normal advance]
   ├─ (4,1) → (4,3)    [Double advance, sets Passing flag]
   └─ (4,1) → (5,2)    [Capture]
   
   (All 3 are legal if King is safe after each move)

8. TimelineEngine (recursion)
   ├─ For each legal move
   ├─ Get new perspectives
   ├─ Compute Black's legal responses
   ├─ Build deeper game tree
   └─ Repeat until maxDepth reached

9. GameStateAnalyzer
   └─ After all moves generated:
      ├─ Is it checkmate? (White king in check + no legal moves)
      ├─ Is it stalemate? (White king NOT in check + no legal moves)
      ├─ Is it draw by repetition?
      ├─ Is it draw by 50-move rule?
      └─ Otherwise: game continues
```

---

## Quick Service Lookup

```
I need to...              │ Service(s)          │ File
──────────────────────────┼─────────────────────┼────────────────────
Load the board            │ BoardStateProvider  │ Policy/Foundation/
Get patterns              │ PatternRepository   │ Policy/Foundation/
Assign perspective bits   │ PerspectiveEngine   │ Policy/Perspectives/
Match one pattern         │ PatternMatcher      │ Policy/Patterns/
Collect all moves         │ CandidateGenerator  │ Policy/Candidates/
Handle sliding pieces     │ SequenceEngine      │ Policy/Sequences/
Apply a move              │ SimulationEngine    │ Policy/Simulation/
Find threatened cells     │ ThreatEngine        │ Policy/Threats/
Filter illegal moves      │ LegalityEngine      │ Policy/Validation/
Build game tree           │ TimelineEngine      │ Policy/Timeline/
Detect checkmate          │ GameStateAnalyzer   │ Policy/Terminal/
```

---

**This document serves as a quick visual reference. For detailed descriptions, see:**
- `ARCHITECTURE_PLAN.md` — Full architectural narrative
- `ChessRulesRegistry.json` — Rule specifications
- `TestDependencyGraph.json` — Test definitions and traits
- `TEST_TROUBLESHOOTING_GUIDE.md` — Debugging workflow
