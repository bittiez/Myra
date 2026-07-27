#nullable enable

namespace Myra.Utility.Search
{
	/// <summary>
	/// Matches a single candidate string against a query. Multi-field weighting
	/// across several candidate strings is the caller's job (see <see cref="SearchScoring"/>).
	/// </summary>
	public interface ISearchStrategy
	{
		/// <summary>
		/// Matches <paramref name="candidate"/> against <paramref name="query"/>.
		/// </summary>
		/// <param name="candidate">The text being searched.</param>
		/// <param name="query">The query to look for. An empty query is conventionally a match of score 1.</param>
		/// <returns>The match, or <see cref="SearchMatch.None"/> when the candidate doesn't match.</returns>
		SearchMatch Match(string candidate, string query);

		/// <summary>
		/// Whether <paramref name="query"/> is well-formed for this strategy - false for, say, a
		/// malformed pattern in a regex-based strategy. Callers can use this to flag bad input
		/// instead of silently showing no results. Strategies that can't reject a query at all
		/// (the default) always return true.
		/// </summary>
		/// <param name="query">The query to validate.</param>
		/// <returns>True when the query can be matched with.</returns>
		bool IsQueryValid(string query) => true;
	}
}
