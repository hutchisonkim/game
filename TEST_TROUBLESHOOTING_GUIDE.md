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

### Quick Start: VS Code Task

The easiest way to run Essential tests on the Spark runner is using the built-in VS Code task:

**In VS Code:**
1. Press `Ctrl+Shift+B` (or `Cmd+Shift+B` on Mac)
2. Select `Spark: Start (if needed) + Test (Essential)`
3. Wait for runner to start (if needed)
4. Tail the output in the integrated terminal for live results

The task automatically:
- Starts the Spark runner if not already running
- Builds and publishes test DLLs and dependencies
- Runs all Essential tests
- Streams output to the terminal in real-time

**Note:** There is no need to manually build or publish test DLLs—the runner handles this automatically as part of the PowerShell script operations.

---

### Manual Test Filtering

For custom test filtering, run the underlying PowerShell script directly with different filter parameters.

**File:** `scripts/spark-start-and-test-essential.ps1`

**Build Handling:** The script automatically handles:
- Building the solution if needed
- Publishing test DLLs and their dependencies to the runner
- No manual `dotnet build` or `dotnet publish` required

You can run tests immediately after code changes without extra build steps.

This script accepts:
- `-Filter` parameter: xUnit trait filter (default: `Essential=True`)
- `-AlwaysStart` switch: Force restart runner even if already running
- `-Tail` flag: Stream output to console
- `-IncludeSparkLog` flag: Include Spark worker logs

**Examples:**

#### Run All Essential Tests (Default)
```powershell
cd c:\___work\game
pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1"
```

#### Run Tests by Layer
```powershell
# Run Foundation layer only
pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" -Filter "Category=Foundation"

# Run Atomic layer
pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" -Filter "Category=Atomic"

# Run Simulation layer
pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" -Filter "Category=Simulation"
```

#### Run Tests by Chess Rule
```powershell
# Test pawn movement (A1)
pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" -Filter "ChessRule=A1"

# Test king safety (C1)
pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" -Filter "ChessRule=C1"
```

#### Run Specific Test
```powershell
# Run single test by name
pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" -Filter "Name=L6_Legality_KingSafety"
```

#### Force Runner Restart + Run Tests
```powershell
# Helpful if runner is in a bad state
pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" -AlwaysStart -Filter "Essential=True"
```

---

### Live Output Monitoring

The script automatically tails test output to the terminal. You'll see:

```
[spark-start-and-test] ✓ All test subprocesses have exited
[spark-start-and-test] Waiting for all test subprocesses to exit...
PASSED  L0_Foundation_BoardStateProvider
PASSED  L0_Foundation_PatternRepository
PASSED  L0_Foundation_PerspectiveEngine
PASSED  L1_PatternMatcher_Pawn
PASSED  L1_PatternMatcher_Knight
FAILED  L2_SequenceEngine_BishopSliding
PASSED  L2_SequenceEngine_RookSliding
...
```

**Key Features:**
- Live streaming output as tests execute
- Process cleanup verification
- Clear pass/fail indicators
- Test subprocess management

---

### xUnit Filter Syntax

The `-Filter` parameter uses xUnit's filter syntax. Common patterns:

| Pattern | Example | Meaning |
|---------|---------|---------|
| Trait name | `Essential=True` | Tests with Essential=True trait |
| Multiple traits | `Essential=True&Category=Atomic` | AND condition (all must match) |
| Multiple values | `Category=Atomic\|Category=Sliding` | OR condition (any can match) |
| Test name | `Name=L6_Legality_KingSafety` | Exact test name match |
| Partial name | `Name~L6_` | Tests starting with "L6_" |
| Negation | `Essential!=False` | Tests where Essential is not False |

---

### Workflow: Finding and Fixing a Bug

1. **Run Essential tests via VS Code task** (Ctrl+Shift+B)
2. **Identify failing test** in the output
3. **Check dependencies** using the test ID (e.g., `L6_Legality_KingSafety`)
4. **Run parent tests** with custom filter:
   ```powershell
   pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" `
     -Filter "Name=L4_ThreatEngine_BasicThreats"
   ```
5. **Monitor live output** in terminal
6. **Fix the failing service** (see Part 5: Common Failure Patterns)
7. **Re-run tests** to verify fix

---

## Part 5: Common Failure Patterns

### Pattern 1: Cascading Failure

**Symptom:** Run Essential tests via VS Code task:
```powershell
Ctrl+Shift+B → Select "Spark: Start (if needed) + Test (Essential)"
```

Output shows:
```
FAILED L4_ThreatEngine_BasicThreats
FAILED L6_Legality_KingSafety  (DependsOn L4)
FAILED L7_Timeline_SingleTurn  (DependsOn L6)
FAILED L8_Terminal_Checkmate   (DependsOn L7)
```

**Root Cause:** Bug in `ThreatEngine`

**Fix:**
1. Run parent test manually:
   ```powershell
   pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" `
     -Filter "Name=L4_ThreatEngine_BasicThreats"
   ```
2. Debug `ThreatEngine` service
3. Re-run until it passes
4. All downstream tests will likely pass automatically

**Lesson:** Always start with the lowest-layer failure.

---

### Pattern 2: Isolated Failure

**Symptom:** Run Essential tests, see selective failure:
```
PASSED L4_ThreatEngine_BasicThreats
PASSED L6_Legality_KingSafety (child of L4)
FAILED L6_Legality_PinDetection (sibling of KingSafety)
PASSED L7_Timeline_SingleTurn (depends on both)
```

**Root Cause:** Bug specific to `PinDetection` logic in `LegalityEngine`

**Fix:**
1. Run the failing test:
   ```powershell
   pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" `
     -Filter "Name=L6_Legality_PinDetection"
   ```
2. Both parents passed → bug is in `PinDetection` itself
3. Review `LegalityEngine.cs`, look for pin-specific logic
4. Pin detection uses `ThreatEngine` + `SimulationEngine`, but both passed
5. Bug must be in the pin-specific combination logic

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
1. Run layer-specific tests:
   ```powershell
   pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" `
     -Filter "Category=Sliding"
   ```
2. Check `L2_SequenceEngine_BishopSliding` and `L2_SequenceEngine_RookSliding`
3. If both pass → issue is in the *merge logic*
4. Write test specifically for merging atomic + sliding candidates

---

## Part 6: Debugging Workflow

Once you've narrowed down the failing test, use this workflow:

1. **Run the failing test in isolation:**
   ```powershell
   pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" `
     -Filter "Name=<test_id>"
   ```
   Watch the terminal output for the assertion error and stack trace.

2. **Check the test's Category trait** to identify which service needs debugging:
   - Foundation → BoardStateProvider, PatternRepository, PerspectiveEngine
   - Atomic → PatternMatcher
   - Sliding → SequenceEngine
   - Simulation → SimulationEngine
   - Threats → ThreatEngine
   - SpecialMoves → SequenceEngine (part 2), SimulationEngine (part 2)
   - Legality → LegalityEngine
   - Timeline → TimelineEngine
   - Terminal → GameStateAnalyzer

3. **Open the service file** and review the logic

4. **Write a minimal reproduction test** focusing on just the failing behavior (see Part 7 below)

5. **Run the minimal repro** in your IDE with a debugger:
   ```powershell
   dotnet test <test_file> -v d --filter "Name=<minimal_test_name>"
   ```

6. **Set breakpoints** in the service and step through

7. **Fix the bug** and verify the fix passes both:
   - Your minimal repro test
   - The original failing test via the task

---

## Part 7: Writing Effective Minimal Repro Tests

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

### Running Tests in CI Pipeline

The preferred way to run tests is via the built-in VS Code task, which uses the Spark runner:

**VS Code Task:** `Spark: Start (if needed) + Test (Essential)`

This runs the underlying script with Essential tests:
```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" -Filter "Essential=True"
```

### GitHub Actions Configuration

`.github/workflows/chess-tests.yml`:

```yaml
name: Chess Policy Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0'
      
      - name: Start Spark Runner
        run: |
          pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-runner-start.ps1"
        timeout-minutes: 5
      
      - name: Run Foundation Tests
        run: |
          pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" `
            -Filter "Category=Foundation"
        timeout-minutes: 10
      
      - name: Run Essential Tests
        run: |
          pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-start-and-test-essential.ps1" `
            -Filter "Essential=True"
        timeout-minutes: 30
      
      - name: Cleanup Spark Runner
        if: always()
        run: |
          pwsh -NoProfile -ExecutionPolicy Bypass -File "scripts/spark-testctl.ps1" -Stop
        timeout-minutes: 5
```

**Key Benefits:**
- Uses Spark runner for distributed test execution
- Automatically starts/stops runner as needed
- Streams live output to CI logs
- Handles subprocess cleanup
- Supports custom filter expressions

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
