#nullable enable

namespace Myra.Utility.Search
{
	/// <summary>
	/// Result of matching a single candidate string against a query.
	/// </summary>
	public readonly record struct SearchMatch(bool IsMatch, double Score, (int Start, int Len)[]? Spans = null)
	{
		public static readonly SearchMatch None = new(false, 0d);

		public static SearchMatch Exact(double score = 1d) => new(true, score);

		public SearchMatch Scaled(double f) => IsMatch ? this with { Score = Score * f } : None;
	}
}
