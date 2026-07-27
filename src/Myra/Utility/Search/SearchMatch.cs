#nullable enable

namespace Myra.Utility.Search
{
	/// <summary>
	/// Result of matching a single candidate string against a query.
	/// </summary>
	/// <param name="IsMatch">Whether the candidate matched at all. When false, the other members carry no meaning.</param>
	/// <param name="Score">Match quality, conventionally in 0..1, where higher is a better match. Used for ranking results.</param>
	/// <param name="Spans">Matched (start, length) ranges within the candidate, for highlighting. Null when the strategy doesn't report positions.</param>
	public readonly record struct SearchMatch(bool IsMatch, double Score, (int Start, int Len)[]? Spans = null)
	{
		/// <summary>A non-match: score zero and no spans.</summary>
		public static readonly SearchMatch None = new(false, 0d);

		/// <summary>
		/// Creates a match with no span information.
		/// </summary>
		/// <param name="score">Match quality, conventionally in 0..1. Defaults to a perfect match.</param>
		/// <returns>The match.</returns>
		public static SearchMatch Exact(double score = 1d) => new(true, score);

		/// <summary>
		/// Multiplies this match's score by <paramref name="f"/>, typically a per-field weight.
		/// </summary>
		/// <param name="f">The factor to scale the score by.</param>
		/// <returns>The scaled match, or <see cref="None"/> if this isn't a match (scaling a non-match stays a non-match).</returns>
		public SearchMatch Scaled(double f) => IsMatch ? this with { Score = Score * f } : None;
	}
}
