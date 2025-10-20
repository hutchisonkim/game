using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core;

// Centralized candidate manipulation helpers.
// Responsible for per-cell direction processing (bounds/occupancy) and
// applying optional policies to candidate streams.
internal static class ActionCandidates
{
	public static IEnumerable<ActionCandidate> ProcessCellCandidates(IEnumerable<ActionCandidate> cellCandidates, Func<int, int, bool> isInside, Func<int, int, object?> getPieceAt)
	{
		ActionCandidate? previous = null;
		bool directionBlocked = false;

		foreach (var cand in cellCandidates)
		{
			// If the pattern changed (or steps reset), start a new direction
			if (previous == null || !ReferenceEquals(previous.Pattern, cand.Pattern) || cand.Steps <= previous.Steps)
			{
				directionBlocked = false;
			}

			previous = cand;

			if (directionBlocked) continue;

			// Check bounds
			if (!isInside(cand.ToRow, cand.ToCol))
			{
				// Once a direction goes out of bounds, further steps in this direction are irrelevant
				directionBlocked = true;
				continue;
			}

			var occupying = getPieceAt(cand.ToRow, cand.ToCol);

			// If square is empty, yield candidate. If occupied, yield candidate and block further steps in this direction.
			if (occupying == null)
			{
				yield return cand;
			}
			else
			{
				yield return cand;
				directionBlocked = true;
			}
		}
	}

	public static IEnumerable<ActionCandidate> ApplyPolicy(IEnumerable<ActionCandidate> candidates, IPolicy? policy)
	{
		if (policy == null) return candidates;
		return policy.Apply(candidates);
	}
}
