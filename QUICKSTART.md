# 🚀 QUICK START: Chess Policy Refactoring

**Read this first** → 5 minutes → Know what to do

---

## The Plan in 30 Seconds

**What?** Refactor chess policy from tangled code into 9 clean services  
**How?** Implement 8 layers: Foundation → Atomic → Sliding → Simulation → Threats → Special → Legality → Timeline → Terminal  
**Validate?** 24 Essential tests (xUnit with traits)  
**Timeline?** 9–10 weeks (8 layers = 8 weeks + buffer)  

---

## 📍 You Are Here

```
You're starting the Chess Policy Refactoring
├─ ❌ Code doesn't exist yet
├─ ❌ Tests aren't written yet
├─ ✅ Complete plan IS documented (you have it)
└─ → Next: Pick your role below
```

---

## Pick Your Role

### 👨‍💻 I'm a Developer

**Action:**
1. Open `REFACTORING_SUMMARY.md` → Find "Next Steps for Developers"
2. Pick Week 1 (Foundation layer)
3. Look up tests in `TestDependencyGraph.json` for Layer 0
4. Implement service to pass test
5. Repeat next week

**When test fails:**
Open `TEST_TROUBLESHOOTING_GUIDE.md` Part 2 (Troubleshooting Workflow)

**Bookmark these:**
- `ARCHITECTURE_PLAN.md` Part 1 (chess rules reference)
- `ChessRulesRegistry.json` (rule specs)
- `TestDependencyGraph.json` (24 tests)
- `TEST_TROUBLESHOOTING_GUIDE.md` (when debugging)

---

### 🏗️ I'm an Architect/Tech Lead

**Action:**
1. Read `ARCHITECTURE_PLAN.md` (all 4 parts)
2. Review `ARCHITECTURE_VISUAL_REFERENCE.md` (diagrams)
3. Check existing `src/Game.Chess/Policy/ChessPolicyRefactored.cs`
4. Validate 8-layer structure aligns with codebase
5. Approve or adjust before developers start

**Watch for:**
- Any rule in `ChessRulesRegistry.json` missing from your codebase
- Any layer that seems out of order
- Any circular dependencies

**Sign off on:** All 4 parts of ARCHITECTURE_PLAN.md before Week 1 starts

---

### 🧪 I'm a QA/Test Engineer

**Action:**
1. Open `TestDependencyGraph.json`
2. Build test infrastructure that:
   - Reads JSON file
   - Generates xUnit test classes with traits
   - Applies `[Trait(...)]` attributes from JSON
3. Create PowerShell script for dependency-aware test execution
4. Set up GitHub Actions (template in `TEST_TROUBLESHOOTING_GUIDE.md` Part 7)

**Key insight:** Don't write 24 tests manually. Generate them from `TestDependencyGraph.json`.

**Bookmark:**
- `TestDependencyGraph.json` (test metadata)
- `TEST_TROUBLESHOOTING_GUIDE.md` Part 1 (trait system)

---

### 📊 I'm a Project Manager

**Action:**
1. Read `REFACTORING_SUMMARY.md` (10 min)
2. Open `TechTree.json` → view `implementationOrder`
3. Note `parallelizationOpportunities` (Phase 5 can split)
4. Plan team capacity for 9–10 weeks
5. Share `ARCHITECTURE_VISUAL_REFERENCE.md` critical path timeline with team

**Key metric:** Track "Essential tests passing" each week
- Week 1: 3/24 (Foundation)
- Week 2: 6/24 (+ Atomic)
- Week 3: 9/24 (+ Sliding)
- etc.

**When done:** All 24/24 tests pass = complete

---

## 📚 File Locations & What to Read

```
Root: c:\___work\game\

For Everyone:
  → REFACTORING_SUMMARY.md (start here)
  → ARCHITECTURE_VISUAL_REFERENCE.md (quick diagrams)
  → DOCUMENTATION_INDEX.md (navigation hub)

Developers:
  → ARCHITECTURE_PLAN.md Part 1 (chess rules)
  → ChessRulesRegistry.json (rule specs)
  → TestDependencyGraph.json (24 tests)
  → TEST_TROUBLESHOOTING_GUIDE.md (debugging)

Architects:
  → ARCHITECTURE_PLAN.md (all parts)
  → ARCHITECTURE_VISUAL_REFERENCE.md (diagrams)
  → TechTree.json (services by layer)

QA:
  → TestDependencyGraph.json (test config)
  → TEST_TROUBLESHOOTING_GUIDE.md Part 1, 4, 7
  → TechTree.json (progress tracking)

Managers:
  → TechTree.json (timeline)
  → ARCHITECTURE_VISUAL_REFERENCE.md (critical path)
  → REFACTORING_SUMMARY.md (progress metrics)
```

---

## 🎯 This Week's Goal

### Week 1 Checklist

- [ ] Read `REFACTORING_SUMMARY.md` (everyone)
- [ ] Read `ARCHITECTURE_PLAN.md` Part 1 (developers)
- [ ] Create test infrastructure from `TestDependencyGraph.json` (QA)
- [ ] Set up GitHub Actions (QA)
- [ ] Approve architecture (Architects)
- [ ] Plan sprint schedule (Managers)
- [ ] Developers start Layer 0:
  - [ ] `L0_Foundation_BoardStateProvider` test pass
  - [ ] `L0_Foundation_PatternRepository` test pass
  - [ ] `L0_Foundation_PerspectiveEngine` test pass

---

## ⚠️ Common Mistakes to Avoid

❌ **DON'T:** Start implementing without reading ARCHITECTURE_PLAN.md Part 1  
✅ **DO:** Understand all 20 chess rules first

❌ **DON'T:** Write 24 tests manually  
✅ **DO:** Generate tests from TestDependencyGraph.json

❌ **DON'T:** Implement services in random order  
✅ **DO:** Follow Tech Tree layers (0 → 1 → 2 → ... → 8)

❌ **DON'T:** Skip Layer 0 (Foundation)  
✅ **DO:** Foundation is prerequisite for everything else

❌ **DON'T:** Ignore test failures  
✅ **DO:** Use TEST_TROUBLESHOOTING_GUIDE.md to debug properly

---

## 📈 Progress Tracking

Check each week:

```powershell
# Run all Essential tests
dotnet test --filter "Essential=true"

# Should see: X/24 passed (X = week number × 3, roughly)
```

**Target:**
- Week 1: 3/24 (Foundation)
- Week 2: 6/24 (Atomic)
- Week 3: 9/24 (Sliding)
- ...
- Week 9: 24/24 (Complete!)

---

## 🚨 If You Get Stuck

**"Which test should I implement first?"**  
→ Find Layer 0 tests in TestDependencyGraph.json

**"What's a service supposed to do?"**  
→ Look up rule in ChessRulesRegistry.json → find ResponsibleServices array

**"A test failed, what do I do?"**  
→ Open TEST_TROUBLESHOOTING_GUIDE.md Part 2 (step-by-step workflow)

**"What's the architecture again?"**  
→ Open ARCHITECTURE_VISUAL_REFERENCE.md (diagrams)

**"Is the timeline realistic?"**  
→ Check TechTree.json → implementationOrder

---

## ✅ You're Ready!

**Next action:**
1. **Developers:** Open `ARCHITECTURE_PLAN.md` Part 1 → start reading chess rules
2. **QA:** Open `TestDependencyGraph.json` → start building test generator
3. **Architects:** Open `ARCHITECTURE_PLAN.md` → validate design
4. **Managers:** Open `TechTree.json` → plan sprint

**Questions?** Refer to `DOCUMENTATION_INDEX.md` (navigation hub)

---

**You have everything needed to succeed. Now start building!**

Created: December 9, 2025  
Status: ✅ Ready
