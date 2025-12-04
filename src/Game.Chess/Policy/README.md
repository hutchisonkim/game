# Chess Policy Refactoring - Layered Architecture

## Overview

The Chess Policy has been refactored from a monolithic 2000+ line class into clean, testable layers. This document describes the new architecture and how the layers interact.

## Architecture Layers

```
┌─────────────────────────────────────────┐
│   ChessPolicyRefactored (Facade)        │
└───────────┬─────────────────────────────┘
            │
    ┌───────┴────────┬────────────┬──────────────┐
    │                │            │              │
    ▼                ▼            ▼              ▼
┌─────────┐   ┌──────────┐  ┌──────────┐  ┌──────────┐
│Foundation│   │Perspective│  │Simulation│  │ Legacy   │
│  Layer   │   │  Engine   │  │  Engine  │  │TimelineService│
└──────────┘   └──────────┘  └──────────┘  └──────────┘
```

## Layer 1: Foundation

**Location**: `Game.Chess.Policy.Foundation/`

**Purpose**: Static, invariant data structures

### Components

#### BoardStateProvider
- Converts `Board` objects into Spark DataFrames
- Pure data transformation, no business logic
- Schema: `(x, y, piece)`

#### PatternRepository
- Provides cached, deduplicated pattern definitions
- All chess rules emerge from these declarative patterns
- Handles pattern caching for performance

**Design Principles**:
- Read-only operations
- No state beyond caching
- Declarative pattern definitions

## Layer 2: Perspectives

**Location**: `Game.Chess.Policy.Perspectives/`

**Purpose**: Compute Self/Ally/Foe relationships

### PerspectiveEngine

**Core Operation**: `BuildPerspectives(piecesDf, factions)`

Takes a board state and generates perspectives where each actor sees:
- **Self**: The actor's own position
- **Ally**: Pieces of the same faction
- **Foe**: Pieces of opposing factions

**Additional Operations**:
- `AddPerspectiveId()` - Identity tracking
- `ApplyThreatMask()` - Marks threatened cells
- `RecomputeGenericPiece()` - Updates relationships after moves

**Design Principles**:
- Pure transformation (no side effects)
- No concept of legality or threats (those are higher layers)
- Agnostic to specific chess rules

## Layer 3: Simulation

**Location**: `Game.Chess.Policy.Simulation/`

**Purpose**: Forward timeline simulation

### SimulationEngine

**Core Operation**: `SimulateBoardAfterMove(perspectives, moves, factions)`

Creates new board state by:
1. Marking source cells as empty
2. Placing moved pieces at destinations
3. Recomputing all perspectives for the new state

**Use Cases**:
- Timeline building (game tree exploration)
- Check validation (simulate to see if king is threatened)
- Sliding piece computation (iterative stepping)

**Design Principles**:
- Stateless (pure function)
- Reusable across threat/legality/timeline computations
- Preserves perspective relationships

## Layer 4: Legacy Timeline Service

**Location**: `Game.Chess.HistoryB.ChessPolicy.TimelineService`

**Purpose**: Backward compatibility during refactoring

The original `TimelineService` remains intact and provides:
- `BuildTimeline()` - Multi-depth game tree exploration
- `ComputeThreatenedCells()` - Threat detection
- `ComputeNextCandidates()` - Pattern-based move generation
- `FilterMovesLeavingKingInCheck()` - Legality validation

**Migration Strategy**:
This layer will be decomposed further into:
- **PatternMatcher** - Atomic pattern matching
- **SequenceEngine** - Sliding piece logic
- **ThreatEngine** - Threat computation
- **LegalityEngine** - Check validation
- **TimelineEngine** - Timeline orchestration

## Current State

### ✅ Completed
- Layer 1: Foundation (BoardStateProvider, PatternRepository)
- Layer 2: Perspectives (PerspectiveEngine)
- Layer 3: Simulation (SimulationEngine)
- ChessPolicyRefactored facade

### 🚧 In Progress
- Pattern Matching Engine (Layer 3 in original plan)
- Sequence Engine (Layer 4)
- Threat Engine (Layer 7)
- Legality Engine (Layer 8)

### 📋 Planned
- Timeline Engine (Layer 9)
- Complete migration from ChessPolicyB to refactored layers
- Deprecate ChessPolicyB once all tests pass with new architecture

## Testing Strategy

### Phase 1: Foundation Validation
Test BoardStateProvider and PatternRepository independently:
```csharp
var provider = new BoardStateProvider(spark);
var piecesDf = provider.GetPieces(board);
// Verify schema and row count
```

### Phase 2: Perspective Validation
Test PerspectiveEngine with known board states:
```csharp
var engine = new PerspectiveEngine();
var perspectives = engine.BuildPerspectives(piecesDf, factions);
// Verify Self/Ally/Foe bits are set correctly
```

### Phase 3: Simulation Validation
Test SimulationEngine with simple moves:
```csharp
var simulator = new SimulationEngine();
var newState = simulator.SimulateBoardAfterMove(perspectives, moves, factions);
// Verify piece positions updated correctly
```

### Phase 4: Integration Tests
Run existing test suite against ChessPolicyRefactored:
- All board simulation tests should pass
- All movement tests should pass
- All check detection tests should pass

## Design Benefits

### 1. Testability
Each layer can be unit tested independently with small DataFrames.

### 2. Debuggability
Clear separation allows inspecting intermediate results at each layer.

### 3. Performance
- Minimal materialization (lazy evaluation preserved)
- Caching at Foundation layer reduces redundant computation
- Simulation layer enables efficient candidate filtering

### 4. Extensibility
Easy to add new chess variants by:
- Modifying pattern definitions (Foundation)
- Adding new perspective relationships (Perspectives)
- Custom simulation logic (Simulation)

### 5. Maintainability
- Each file < 300 lines
- Single responsibility per class
- Clear layer dependencies (no cycles)

## Migration Path

1. **Incremental**: New code uses refactored layers, legacy code untouched
2. **Validation**: Run full test suite continuously
3. **Decomposition**: Extract remaining engines from TimelineService
4. **Deprecation**: Mark ChessPolicyB obsolete once migration complete
5. **Cleanup**: Remove legacy code after confidence period

## Future Enhancements

### Pattern-Driven Rule Engine
The foundation is now in place for a general rule engine that can handle:
- Chess variants (Chess960, Atomic Chess, etc.)
- Custom piece types and movement patterns
- Alternative win conditions
- Multi-player variants

### Optimization Opportunities
- Pattern compilation (pre-compute common sequences)
- Lazy branch pruning (filter candidates before materialization)
- Parallel timeline exploration (multi-turn lookahead)
- Caching of threat computations

## References

- Original implementation: `src/Game.Chess/History/ChessPolicyB.cs`
- Refactoring plan: `policy-refactor-plan.prompt.md`
- Integration tests: `tests/Game.Chess.Tests.Integration/`
