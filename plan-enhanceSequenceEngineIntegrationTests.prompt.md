## Plan: Enhance Sequence Engine Integration Tests

Investigate logs showing bishop sliding only generates 2 moves instead of expected 5+ diagonal moves. Root cause: SequenceEngine continuation logic incorrectly uses `dst_piece` (Empty) instead of `src_piece` (Bishop) when creating hypothetical perspectives, preventing pattern matching. This breaks multi-step sliding expansion.

### Steps
1. Fix SequenceEngine continuation bug: In `ExpandContinuationMoves`, change `.WithColumn("piece", Col("dst_piece"))` to `.WithColumn("piece", Col("src_piece"))` and `.WithColumn("generic_piece", Col("src_generic_piece"))` to enable proper actor matching at frontier positions.
2. Add integration test for recursive sequencing: Create `L2_SequenceEngine_RecursiveDepthTest` verifying bishop/rook expand up to 8 steps on empty board (e.g., rook at a1 reaches h1 in 7 moves).
3. Add integration test for In/Out sequencing: Create `L2_SequenceEngine_InOutFlagsTest` verifying pawn promotion sequences (OutX triggers InX) and castling (OutY/InY pairs).
4. Add integration test for variant consistency: Create `L2_SequenceEngine_VariantRespectTest` ensuring bishop only continues in same diagonal variant (Variant1-4) across steps.
5. Add integration test for Public flag: Create `L2_SequenceEngine_PublicFlagTest` verifying only sequences with Public flag in final step are included in candidates (e.g., bishop InF has Public, OutF does not).
6. Add integration test for Parallel flag: Create `L2_SequenceEngine_ParallelFlagTest` verifying multi-piece moves (e.g., castling moves king and rook simultaneously) collect both in single candidate set.
7. Add foundational tests: Create `L2_SequenceEngine_SrcDstConditionsTest` verifying src_conditions (e.g., Self|Bishop) and dst_conditions (Empty/Foe) are enforced; `L2_SequenceEngine_BoardStateModificationTest` ensuring moves update board state correctly; `L2_SequenceEngine_AbsoluteFactionTest` verifying post-move board describes White/Black pieces for rendering.
8. Update TestDependencyGraph.json: Add new test IDs under L2 layer with DependsOn "L2_SequenceEngine_BishopSliding", ChessRule links (e.g., A4 for bishop), and Essential=true.
9. Update TechTree.json: Add new services under level_2_sliding_moves (e.g., "SequenceEngine (Extended)") with dependencies and test IDs.
10. Update ChessRulesRegistry.json: Add new test IDs to serviceMap for SequenceEngine, and ensure rules like A4-A6 reference the new tests.

### Further Considerations
1. Parallelize test development: SequenceEngine fixes and recursive/variant tests can run first, followed by In/Out and Public/Parallel once basic sliding works.
2. Validate with existing L2 tests: After fixes, re-run L2_SequenceEngine_BishopSliding and L2_SequenceEngine_RookSliding to confirm 5+ moves.
3. Consider edge cases: Test boundary conditions (board edges), blockers, and mixed sequences (e.g., promotion + capture).
4. Avoid early materialization: Do not trigger any actions until the final stage so all transformations remain lazy. Early filtering and conditional joins are allowed and encouraged for optimization, but validity should primarily be tracked as dedicated columns that propagate through the pipeline. At the final materialization step, validity flags determine which rows are emitted.
