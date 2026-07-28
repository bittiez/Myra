#nullable enable

using System.Collections.Generic;

namespace Myra.Utility.Search
{
	/// <summary>
	/// Helpers for combining single-field <see cref="ISearchStrategy"/> matches into a
	/// multi-field score.
	/// </summary>
	public static class SearchScoring
	{
		/// <summary>
		/// Returns the match with the highest <c>Score * Weight</c> among <paramref name="fields"/>,
		/// or <see cref="SearchMatch.None"/> if none of them match.
		/// </summary>
		public static SearchMatch Best(ISearchStrategy strategy, string query, params (string? Text, double Weight)[] fields)
		{
			SearchMatch best = SearchMatch.None;

			foreach ((string? text, double weight) in fields)
			{
				if (text == null)
				{
					continue;
				}

				SearchMatch match = strategy.Match(text, query).Scaled(weight);
				if (match.IsMatch && (!best.IsMatch || match.Score > best.Score))
				{
					best = match;
				}
			}

			return best;
		}

		/// <summary>
		/// Returns the highest-scoring match among <paramref name="texts"/>, each weighted equally
		/// by <paramref name="weight"/>.
		/// </summary>
		public static SearchMatch BestOfMany(ISearchStrategy strategy, string query, IEnumerable<string?> texts, double weight)
		{
			SearchMatch best = SearchMatch.None;

			foreach (string? text in texts)
			{
				if (text == null)
				{
					continue;
				}

				SearchMatch match = strategy.Match(text, query).Scaled(weight);
				if (match.IsMatch && (!best.IsMatch || match.Score > best.Score))
				{
					best = match;
				}
			}

			return best;
		}
	}
}
