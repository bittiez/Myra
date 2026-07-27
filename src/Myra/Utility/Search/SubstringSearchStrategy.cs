#nullable enable

using System;
using System.ComponentModel;

namespace Myra.Utility.Search
{
	/// <summary>
	/// Plain substring match, case-sensitive or not.
	/// </summary>
	public class SubstringSearchStrategy : ISearchStrategy
	{
		/// <summary>Whether the comparison is case-sensitive. Off by default.</summary>
		[DefaultValue(false)]
		public bool CaseSensitive { get; set; }

		/// <summary>
		/// Matches when <paramref name="query"/> occurs anywhere in <paramref name="candidate"/>.
		/// All hits score 1 - there's no notion of a better or worse substring match - so callers
		/// ranking by score alone keep the candidates' original order.
		/// </summary>
		/// <param name="candidate">The text being searched.</param>
		/// <param name="query">The substring to look for. An empty query matches everything.</param>
		/// <returns>A match carrying the span of the first occurrence, or <see cref="SearchMatch.None"/>.</returns>
		public SearchMatch Match(string candidate, string query)
		{
			if (string.IsNullOrEmpty(query))
			{
				return SearchMatch.Exact(1d);
			}

			StringComparison comparison = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
			int idx = candidate.IndexOf(query, comparison);
			if (idx < 0)
			{
				return SearchMatch.None;
			}

			return new SearchMatch(true, 1d, new (int Start, int Len)[] { (idx, query.Length) });
		}

		/// <inheritdoc />
		public ISearchStrategy Clone() => (ISearchStrategy)MemberwiseClone();
	}
}
