# Integration Test Troubleshooting Guide

**Version:** 1.0  
**Date:** December 9, 2025  
**Audience:** Developers working on chess policy refactoring

---

## Overview

This guide explains how to use **xUnit Traits** and **Dependency Graph** to troubleshoot failing tests in a structured, hierarchical manner. Instead of randomly debugging a failing test, use this strategy to **isolate the root cause quickly**.

---

## Part 1: Understanding the Trait System

### The Four Core Traits

#### 1. **Essential Trait**
```csharp
[Trait("Essential", "true")]
public void L6_Legality_KingSafety() { }
```

**Meaning:** This test is critical for refactoring progress and must pass in CI.

**Action on Failure:** 
- Block PR until fixed
- All dependent tests automatically skipped (use DependsOn trait)
- High priority: fix immediately

---

#### 2. **DependsOn Trait**
```csharp
[Trait("Essential", "true")]
[Trait("DependsOn", "L4_ThreatEngine_BasicThreats")]
public void L6_Legality_KingSafety() { }
```

**Meaning:** This test cannot pass unless `L4_ThreatEngine_BasicThreats` passes first.

**Action on Failure:**
1. Run parent test: `L4_ThreatEngine_BasicThreats`
2. If parent FAILS → fix that test first, then re-run this one
3. If parent PASSES → bug is in this test's own service, debug directly

**Multiple Dependencies:**
```csharp
[Trait("DependsOn", "L4_ThreatEngine_BasicThreats;L3_SimulationEngine_SimpleMoves")]
```

---

#### 3. **Category Trait**
```csharp
[Trait("Category", "Threats")]
public void L4_ThreatEngine_BasicThreats() { }
```

**Meaning:** Groups tests by architectural layer. Helps identify which service to debug.

**Categories:**
- `Foundation` → Debug: BoardStateProvider, PatternRepository, PerspectiveEngine
- `Atomic` → Debug: PatternMatcher
- `Sliding` → Debug: SequenceEngine (part 1)
- `Simulation` → Debug: SimulationEngine
- `Threats` → Debug: ThreatEngine
- `SpecialMoves` → Debug: SequenceEngine (part 2), SimulationEngine (part 2)
- `Legality` → Debug: LegalityEngine
- `Timeline` → Debug: TimelineEngine
- `Terminal` → Debug: GameStateAnalyzer

---

#### 4. **ChessRule Trait**
```csharp
[Trait("ChessRule", "A1")]
public void L1_PatternMatcher_Pawn() { }
```

**Meaning:** Links test to a specific chess rule from the rules registry.

**Rules:**
- `A1`–`A8`: Basic movement
- `B1`–`B5`: Special moves
- `C1`–`C5`: Legality rules
- `D1`–`D4`: Terminal conditions

**Action on Failure:** Consult `ChessRulesRegistry.json` to understand what the rule requires.

---

## Part 2: Troubleshooting Workflow

### Step 1: A Test Fails

When you see:
```
FAILED L6_Legality_KingSafety
```

### Step 2: Check the Dependency Chain

Look up the test in `TestDependencyGraph.json`:

```json
{
  "testId": "L6_Legality_KingSafety",
  "dependsOn": ["L4_ThreatEngine_BasicThreats", "L3_SimulationEngine_SimpleMoves"],
  ...
}
```

### Step 3: Run Parent Tests First

```powershell
# Run the immediate parents
dotnet test --filter "Name=L4_ThreatEngine_BasicThreats"
dotnet test --filter "Name=L3_SimulationEngine_SimpleMoves"
```

### Step 4: Interpret Results

#### Scenario A: Parent Test FAILS
```
FAILED L4_ThreatEngine_BasicThreats
```

**Action:**
1. Stop here — don't debug `L6_Legality_KingSafety` yet
2. Fix `ThreatEngine` (the failing parent service)
3. Re-run parent until it PASSES
4. Then re-run `L6_Legality_KingSafety`

**Why?** The legality check *depends on* correct threat detection. Fixing the parent fixes the downstream failure.

---

#### Scenario B: Parent Tests PASS
```
PASSED L4_ThreatEngine_BasicThreats
PASSED L3_SimulationEngine_SimpleMoves
FAILED L6_Legality_KingSafety
```

**Action:**
1. Parents are good — the bug is in `LegalityEngine` itself
2. Look at the Category: `Legality`
3. Review `LegalityEngine.cs` source code
4. Write a minimal reproduction test focusing on legality logic

**Example Minimal Repro:**
```csharp
[Fact]
public void TestKingSafetyAfterMove()
{
    // Arrange: board where king is threatened
    var board = GetBoardWithKingThreatenedBy(piece: Piece.Bishop);
    var perspectives = GetPerspectives(board, new[] { Piece.White });
    var candidates = GetCandidates(perspectives); // candidate move
    
    // Act: filter illegal moves
    var legalMoves = LegalityEngine.FilterMovesLeavingKingInCheck(
        candidates, perspectives, patterns, factions);
    
    // Assert: move should be filtered out
    Assert.DoesNotContain(candidates.First(), legalMoves);
}
```

---

### Step 5: Identify the Service

Use the **Category** trait to narrow down which service needs debugging:

| Category | Service | File Path |
|----------|---------|-----------|
| Foundation | BoardStateProvider, PatternRepository, PerspectiveEngine | `Policy/Foundation/` |
| Atomic | PatternMatcher | `Policy/Patterns/PatternMatcher.cs` |
| Sliding | SequenceEngine | `Policy/Sequences/SequenceEngine.cs` |
| Simulation | SimulationEngine | `Policy/Simulation/SimulationEngine.cs` |
| Threats | ThreatEngine | `Policy/Threats/ThreatEngine.cs` |
| SpecialMoves | SequenceEngine, SimulationEngine | Both files above |
| Legality | LegalityEngine | `Policy/Validation/LegalityEngine.cs` |
| Timeline | TimelineEngine | `Policy/Timeline/TimelineEngine.cs` |
| Terminal | GameStateAnalyzer | `Policy/Terminal/GameStateAnalyzer.cs` |

---

### Step 6: Review the Chess Rule

Look up the failing test's **ChessRule** trait in `ChessRulesRegistry.json`:

```json
{
  "C1": {
    "name": "King Safety (Cannot Move Into Check)",
    "sourceCondition": "Any move candidate",
    "destinationCondition": "After move, king must not be in threatened square",
    ...
  }
}
```

This tells you:
- What the rule means
- What conditions must be satisfied
- Which services are responsible
- Any dependent rules

---

## Part 3: Troubleshooting Decision Tree

```
Test Fails
│
├─→ Are parent tests passing?
│   │
│   ├─ NO (Parent fails)
│   │   └─→ FIX PARENT SERVICE FIRST
│   │       └─→ Then re-run failing test
│   │
│   └─ YES (Parent passes)
│       └─→ Bug is in this test's own service
│           └─→ Identify service by Category trait
│               └─→ Review service code
│                   └─→ Write minimal repro test
│                       └─→ Fix bug in service
│
└─→ Multiple parents failing?
    └─→ Start with lowest-layer parent
        (Foundation > Atomic > Sliding > Simulation > Threats)
```

---

## Part 4: Running Tests Intelligently

### Run All Essential Tests (CI Pipeline)
```powershell
dotnet test --filter "Essential=true" `
    --logger:"console;verbosity=detailed"
```

Shows overall progress toward completion.

---

### Run Tests by Layer

Run all tests for a specific architectural layer:

```powershell
# Run Foundation layer
dotnet test --filter "Category=Foundation"

# Run Atomic layer
dotnet test --filter "Category=Atomic"

# Run Simulation layer
dotnet test --filter "Category=Simulation"
```

**Use Case:** After fixing a service, run all tests in its category to verify nothing broke.

---

### Run Tests by Rule

Run all tests related to a specific chess rule:

```powershell
# Run all tests for pawn movement (A1)
dotnet test --filter "ChessRule=A1"

# Run all tests for king safety (C1)
dotnet test --filter "ChessRule=C1"
```

**Use Case:** When you know the rule is broken, test all related scenarios.

---

### Run with Dependency Awareness (Custom Script)

Create a PowerShell script `run-tests-with-dependencies.ps1`:

```powershell
param(
    [string]$Filter = "Essential=true"
)

# Load dependency graph
$depGraph = Get-Content TestDependencyGraph.json | ConvertFrom-Json

# Find all tests matching filter
$tests = $depGraph.testSuite | Where-Object { 
    $_.traits.Essential -eq "true" 
}

# Build execution order (topological sort by DependsOn)
$executed = @{}
$failed = @{}

foreach ($test in $tests) {
    # Check if parents passed
    $parentsFailed = $test.dependsOn | Where-Object { 
        $failed[$_] -eq $true 
    }
    
    if ($parentsFailed.Count -gt 0) {
        Write-Host "⊘ SKIP $($test.testId) (parent failed)" -ForegroundColor Yellow
        continue
    }
    
    # Run test
    Write-Host "▶ RUN $($test.testId)" -ForegroundColor Cyan
    $result = dotnet test --filter "Name=$($test.testId)" 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ PASS $($test.testId)" -ForegroundColor Green
        $executed[$test.testId] = $true
    } else {
        Write-Host "✗ FAIL $($test.testId)" -ForegroundColor Red
        $failed[$test.testId] = $true
    }
}

# Summary
Write-Host ""
Write-Host "Results:" -ForegroundColor Cyan
Write-Host "  Passed: $($executed.Count)"
Write-Host "  Failed: $($failed.Count)"
Write-Host "  Skipped: $(($tests.Count) - $executed.Count - $failed.Count)"
```

**Usage:**
```powershell
./run-tests-with-dependencies.ps1 -Filter "Category=Simulation"
```

**Behavior:**
- Runs tests in dependency order
- Skips tests whose parents failed
- Reports which failures cascade from which parents
- Helps isolate root cause

---

## Part 5: Common Failure Patterns

### Pattern 1: Cascading Failure

**Symptom:**
```
FAILED L4_ThreatEngine_BasicThreats
FAILED L6_Legality_KingSafety  (DependsOn L4)
FAILED L7_Timeline_SingleTurn  (DependsOn L6)
FAILED L8_Terminal_Checkmate   (DependsOn L7)
```

**Root Cause:** Bug in `ThreatEngine`

**Fix:**
1. Debug `L4_ThreatEngine_BasicThreats`
2. Run `L4_ThreatEngine_BasicThreats` until it passes
3. All downstream tests will likely pass automatically

**Lesson:** Always start with the lowest-layer failure.

---

### Pattern 2: Isolated Failure

**Symptom:**
```
PASSED L4_ThreatEngine_BasicThreats
PASSED L6_Legality_KingSafety (child of L4)
FAILED L6_Legality_PinDetection (sibling of KingSafety)
PASSED L7_Timeline_SingleTurn (depends on both)
```

**Root Cause:** Bug specific to `PinDetection` logic in `LegalityEngine`

**Fix:**
1. Both parents passed → bug is in `PinDetection` itself
2. Review `LegalityEngine.cs`, look for pin-specific logic
3. Pin detection uses `ThreatEngine` + `SimulationEngine`, but both passed
4. Bug must be in the pin-specific combination logic

---

### Pattern 3: Hidden Dependency

**Symptom:**
```
PASSED L1_PatternMatcher_Pawn
PASSED L1_PatternMatcher_Knight
FAILED L2_CandidateGenerator_SlidingMoves
```

**Root Cause:** `CandidateGenerator` depends on merging atomic + sliding, but something broke in the merge

**Fix:**
1. Check `L2_SequenceEngine_BishopSliding` and `L2_SequenceEngine_RookSliding`
2. If both pass → issue is in the *merge logic*
3. Write test specifically for merging atomic + sliding candidates

---

## Part 6: Writing Effective Minimal Repro Tests

When you've isolated a failing service, write a small test that reproduces the bug:

### Template

```csharp
[Theory]
[InlineData("scenario description")]
public void Service_Condition_ExpectedOutcome(string scenario)
{
    // Arrange: Create minimal state that reproduces the bug
    var input = CreateTestInput(scenario);
    
    // Act: Call the service method
    var result = ServiceUnderTest.MethodName(input);
    
    // Assert: Verify expected behavior
    Assert.NotNull(result);
    Assert.Equal(expected, result.Property);
}
```

### Example: ThreatEngine Bug

```csharp
[Fact]
public void ThreatEngine_ComputeThreats_SlidingBishopThreatDetected()
{
    // Arrange
    var board = CreateBoard();
    board[4, 4] = Piece.White | Piece.Bishop; // bishop at e4
    var perspectives = GetPerspectives(board, Piece.Black);
    var patterns = PatternRepository.GetPatterns();
    
    // Act
    var threats = ThreatEngine.ComputeThreats(
        perspectives, patterns, specificFactions: new[] { Piece.Black });
    
    // Assert: Bishop should threaten diagonal squares
    Assert.Contains((3, 3), threats.Select(t => (t.x, t.y)));
    Assert.Contains((5, 5), threats.Select(t => (t.x, t.y)));
    Assert.Contains((2, 2), threats.Select(t => (t.x, t.y)));
}
```

**Key Points:**
- Use inline data for multiple scenarios
- Focus on one behavior at a time
- Name test: `Service_Condition_Expected`
- Arrange minimal state (small board, specific pieces)
- Act on one method
- Assert specific property

---

## Part 7: CI/CD Integration

### GitHub Actions Configuration

`.github/workflows/chess-tests.yml`:

```yaml
name: Chess Policy Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0'
      
      - name: Run Foundation Tests
        run: |
          dotnet test --filter "Category=Foundation" \
            --logger:"console;verbosity=detailed"
      
      - name: Run Essential Tests
        run: |
          dotnet test --filter "Essential=true" \
            --logger:"console;verbosity=detailed"
      
      - name: Generate Test Report
        if: always()
        run: |
          dotnet test --filter "Essential=true" \
            --logger:"json:test-report.json"
      
      - name: Upload Report
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: test-report
          path: test-report.json
```

---

## Part 8: Progress Tracking

### Completion Checklist

Use this to track refactoring progress:

- [ ] Layer 0: All Foundation tests pass (3/3)
- [ ] Layer 1: All Atomic tests pass (3/3)
- [ ] Layer 2: All Sliding tests pass (3/3)
- [ ] Layer 3: All Simulation tests pass (2/2)
- [ ] Layer 4: All Threats tests pass (2/2)
- [ ] Layer 5: All SpecialMoves tests pass (3/3)
- [ ] Layer 6: All Legality tests pass (3/3)
- [ ] Layer 7: All Timeline tests pass (3/3)
- [ ] Layer 8: All Terminal tests pass (2/2)

**Total Essential Tests:** 24/24

---

## Part 9: Quick Reference

### All Test IDs

```
L0: Foundation
├─ L0_Foundation_BoardStateProvider
├─ L0_Foundation_PatternRepository
└─ L0_Foundation_PerspectiveEngine

L1: Atomic
├─ L1_PatternMatcher_Pawn
├─ L1_PatternMatcher_Knight
├─ L1_PatternMatcher_King
└─ L1_CandidateGenerator_SimpleMoves

L2: Sliding
├─ L2_SequenceEngine_BishopSliding
├─ L2_SequenceEngine_RookSliding
└─ L2_CandidateGenerator_SlidingMoves

L3: Simulation
├─ L3_SimulationEngine_SimpleMoves
└─ L3_SimulationEngine_FlagUpdates

L4: Threats
├─ L4_ThreatEngine_BasicThreats
└─ L4_ThreatEngine_SlidingThreats

L5: Special Moves
├─ L5_SpecialMoves_Castling
├─ L5_SpecialMoves_EnPassant
└─ L5_SpecialMoves_PawnPromotion

L6: Legality
├─ L6_Legality_KingSafety
├─ L6_Legality_PinDetection
└─ L6_Legality_DiscoveredCheck

L7: Timeline
├─ L7_Timeline_SingleTurn
├─ L7_Timeline_MultiTurn
└─ L7_Timeline_DepthCutoff

L8: Terminal
├─ L8_Terminal_Checkmate
└─ L8_Terminal_Stalemate
```

---

## Summary

**Key Principle:** Use the dependency chain to isolate root causes. Always fix parents before children.

1. **Read traits:** Essential, DependsOn, Category, ChessRule
2. **Check parents:** Run parent tests first
3. **Identify service:** Use Category trait
4. **Write minimal repro:** Target the failing service
5. **Fix bug:** Modify the service code
6. **Re-run:** Verify fix propagates downstream

This structured approach turns debugging from guesswork into systematic root-cause analysis.
