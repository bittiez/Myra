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
		[DefaultValue(false)]
		public bool CaseSensitive { get; set; }

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
	}
}
