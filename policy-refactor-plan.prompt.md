Below is a **clean, incremental plan to refactor your ChessPolicy** based on the complexity layers you identified, mapping them into a coherent architecture.
This plan respects your Spark-driven, pattern-emergent engine and keeps all key properties (declarative rules, lazy evaluation, minimal materialization, and simulation-based correctness).

---

# ✅ **Refactoring Plan for ChessPolicy**

Your current architecture already has the ingredients of a general rule engine, but they are deeply intertwined. The goal is to **separate concerns so the engine becomes:**

1. predictable
2. inspectable
3. debuggable
4. replaceable in layers
5. performant by keeping materialization to a minimum

Below is a layered breakdown + the corresponding refactor steps.

---

# 🔷 **Layer 1 — Static Foundations**

**Goal:** isolate invariant data: board state → pieces → patterns.

### **Already present**

* `PieceFactory`
* `PatternFactory`

### **Refactor**

Create a new module:

```
Game.Chess.Policy.Foundation/
    BoardStateProvider.cs  (wraps piecesDf retrieval + schema)
    PatternRepository.cs   (caches pattern tables, dedup logic)
```

### **Why**

This moves all *static declarative knowledge* to its own layer.
Everything above uses these as read-only “tables”.

---

# 🔷 **Layer 2 — Perspective Engine**

**Goal:** compute *Self/Ally/Foe* context for all actors without any notion of legality.

### **Already present**

* `GetPerspectivesDfFromPieces()`

### **Refactor**

Promote this to a standalone service:

```
Game.Chess.Policy.Perspectives/
    PerspectiveEngine.cs
```

With methods:

```
DataFrame BuildPerspectives(BoardState)
DataFrame ApplyThreatMask(Perspectives, Threats)
```

Make this layer **pure**, with no simulation, no patterns, no turn logic.

### **Why**

This becomes the central “view model” for the rest of the engine.

---

# 🔷 **Layer 3 — Pattern Matching Engine (Static Moves Only)**

**Goal:** from a perspective, generate pattern-matching candidates (ignoring sequences / recursion).

You already have this in `ComputeNextCandidates` but it is mixed with:

* sequence logic
* sliding logic
* threatened logic
* actor-filtering
* turn filtering
* lookup using original_perspective_x/y

### **Refactor**

Extract the pure pattern application:

```
Game.Chess.Policy.Patterns/
    PatternMatcher.cs
```

Expose:

```
DataFrame MatchAtomicPatterns(Perspectives, Patterns, Faction)
```

This function does EXACTLY the following, and nothing more:

✔ filter src_conditions
✔ compute dst_x / dst_y
✔ fetch dst_generic_piece
✔ apply dst_conditions

No sequencing, no “InstantRecursive”, no “Public”, no in/out pair logic.

### **Why**

This becomes equivalent to “one movement micro-step”.

---

# 🔷 **Layer 4 — Sequencing Engine (Sliding, Multi-Step Moves)**

You already have deep logic implementing:

* OutX → InX matching
* InstantRecursive
* Variant flags
* directional sliding
* continuation sequences
* lazy multi-depth exploration

But it's entangled in `ComputeNextCandidates`, `ComputeSequencedMoves`, and `ComputeSlidingThreats`.

### **Refactor**

Create:

```
Game.Chess.Policy.Sequences/
    SequenceEngine.cs
```

With two primitives:

```
DataFrame ExpandEntryMoves(...)
DataFrame ExpandContinuationMoves(...)
```

And a unifying method:

```
DataFrame ExpandSequencedMoves(Perspectives, Patterns, Faction)
```

### **Goal**

The SequenceEngine consumes only:

* Pattern table
* PatternMatcher.MatchAtomicPatterns()
* the perspective table

and produces **completed multi-step moves for sliding pieces**.

### **Why**

Your sliding computation becomes correct, clean, and testable.

---

# 🔷 **Layer 5 — Candidate Generation (Atomic + Sequenced)**

This layer merges the two previous layers:

```
Game.Chess.Policy.Candidates/
    CandidateGenerator.cs
```

Exposes:

```
DataFrame GetMoves(Perspective, Patterns, Faction)
    = atomic moves
    + sequenced moves
```

### **Why**

This is the first place where you get *something meaningful to a chess game*.

---

# 🔷 **Layer 6 — Forward Simulation Engine**

You already have `SimulateBoardAfterMove`.
This is good and should be isolated.

### **Refactor**

Create module:

```
Game.Chess.Policy.Simulation/
    SimulationEngine.cs
```

Expose:

```
DataFrame Simulate(Perspectives, Move)
```

Key points:

* **stateless**
* used by threat engine and move validation

---

# 🔷 **Layer 7 — Threat Engine**

This evaluates all squares threatened by opponent.

### **Already implemented** but deeply mixed with pattern logic.

### **Refactor**

Create:

```
Game.Chess.Policy.Threats/
    ThreatEngine.cs
```

Expose:

```
DataFrame ComputeThreats(Perspectives, Patterns, OpponentFaction)
```

Internally it will call:

* CandidateGenerator.GetMoves()
* but with `dst_conditions = NONE`
* and ignoring `Public` restrictions

This decouples threat logic from move legality.

---

# 🔷 **Layer 8 — Legality Engine (King Safety, Pins, etc.)**

Your `FilterMovesLeavingKingInCheck` is correct but complex.

### **Refactor**

Create:

```
Game.Chess.Policy.Validation/
    LegalityEngine.cs
```

Expose:

```
DataFrame FilterIllegalMoves(Candidates, Perspectives, Patterns, Faction)
```

Internally:

1. identify king
2. simulate each move
3. call ThreatEngine on simulated state
4. reject illegal moves

This is where all chess “laws” reside.

---

# 🔷 **Layer 9 — Timeline Engine**

Finally, your existing `BuildTimeline` becomes a thin orchestrator:

```
Game.Chess.Policy.Timeline/
    TimelineEngine.cs
```

Build timeline via:

```
Perspectives → Candidates → LegalityEngine → Simulation → NextPerspectives → ThreatEngine
```

### **Goal**

TimelineEngine should contain **no move logic** of its own.
Only scheduling + recursion depth control.

---

# 🔷 **Bonus: Lazy / Branch-Pruning Optimization Strategy**

You mentioned a key insight:

> reducing candidates through forward simulation that branch-invalidate the candidate **without materializing intermediate levels**.

Yes.

This is exactly how the new architecture enables *non-materialized lazy filtering*:

### Perform the following in a streaming style:

```
for each MoveCandidate in small_df_of_candidates:
    simulate only that move → small_df
    compute threats for only that move → small_df
    validate → accept/reject
```

Since candidates per turn are realistically 20–40:

* simulation DF ~ 64 rows × 40 → tiny
* threats DF ~ ~400 rows → tiny
* Spark will materialize nearly nothing expensive

This becomes extremely fast.

---

# 🔷 **Final Architecture Overview**

```
Foundation
├── PieceFactory
├── PatternRepository

Perspective
├── PerspectiveEngine

Patterns
├── PatternMatcher (atomic)

Sequences
├── SequenceEngine (sliding)

Candidates
├── CandidateGenerator (atomic + sliding)

Simulation
├── SimulationEngine

Threats
├── ThreatEngine

Validation
├── LegalityEngine

Timeline
└── TimelineEngine (orchestrates everything)
```

---

# 🎯 **What You Gain**

### ✔ cleaner runtime

### ✔ single-purpose layers

### ✔ move logic isolated from Spark orchestration

### ✔ correct sliding logic (fully isolated)

### ✔ lazy candidate evaluation

### ✔ simpler debugging

### ✔ ability to unit-test each engine with tiny DataFrames

### ✔ foundation for custom rule-sets (variant chess, atomic chess, etc.)

---
