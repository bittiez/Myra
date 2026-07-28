#nullable enable

using System;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Myra.Utility.Search
{
	/// <summary>
	/// Literal, whole-word or regex search, with a lazily compiled/cached <see cref="Regex"/>.
	/// </summary>
	public class TextQuerySearchStrategy : ISearchStrategy
	{
		/// <summary>
		/// Ceiling on a single <see cref="Regex.Match(string)"/>. The query is user input matched
		/// on the UI thread once per candidate per keystroke, so a pattern that backtracks
		/// catastrophically (<c>(a+)+$</c> and friends) has to be cut off rather than allowed to
		/// hang the application; a timed-out match is reported as a non-match.
		/// </summary>
		private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(500);

		private string? _cachedQuery;
		private bool _cachedCaseSensitive;
		private bool _cachedWholeWord;
		private bool _cachedUseRegex;
		private Regex? _cachedRegex;
		private bool _cachedValid;
		private bool _cacheReady;

		/// <summary>Whether matching is case-sensitive. Off by default.</summary>
		[DefaultValue(false)]
		public bool CaseSensitive { get; set; }

		/// <summary>
		/// Whether the query must match on word boundaries (it gets wrapped in <c>\b...\b</c>).
		/// Off by default. Combines with <see cref="UseRegex"/>.
		/// </summary>
		[DefaultValue(false)]
		public bool WholeWord { get; set; }

		/// <summary>
		/// Whether the query is a regular expression. Off by default, in which case the query is
		/// escaped and matched literally.
		/// </summary>
		[DefaultValue(false)]
		public bool UseRegex { get; set; }

		/// <summary>
		/// Whether <paramref name="query"/> compiles. Only ever false with <see cref="UseRegex"/>
		/// on and a malformed pattern; literal queries are always valid.
		/// </summary>
		/// <param name="query">The query to validate.</param>
		/// <returns>True when the query can be matched with.</returns>
		public bool IsQueryValid(string query)
		{
			EnsureRegex(query);
			return _cachedValid;
		}

		/// <summary>
		/// Matches <paramref name="candidate"/> against <paramref name="query"/>, honouring
		/// <see cref="CaseSensitive"/>, <see cref="WholeWord"/> and <see cref="UseRegex"/>. All
		/// hits score 1 - the query either matches or it doesn't - so callers ranking by score
		/// alone keep the candidates' original order.
		/// </summary>
		/// <param name="candidate">The text being searched.</param>
		/// <param name="query">The query to look for. An empty query matches everything; a malformed regex matches nothing.</param>
		/// <returns>A match carrying the span of the first hit, or <see cref="SearchMatch.None"/>.</returns>
		public SearchMatch Match(string candidate, string query)
		{
			if (string.IsNullOrEmpty(query))
			{
				return SearchMatch.Exact(1d);
			}

			EnsureRegex(query);

			if (!_cachedValid || _cachedRegex == null)
			{
				return SearchMatch.None;
			}

			System.Text.RegularExpressions.Match m;
			try
			{
				m = _cachedRegex.Match(candidate);
			}
			catch (RegexMatchTimeoutException)
			{
				// See MatchTimeout: a pathological pattern is reported as a non-match rather
				// than allowed to wedge the thread it was typed on.
				return SearchMatch.None;
			}

			if (!m.Success)
			{
				return SearchMatch.None;
			}

			return new SearchMatch(true, 1d, new (int Start, int Len)[] { (m.Index, m.Length) });
		}

		/// <inheritdoc />
		// The copied regex cache stays valid: Regex is immutable, and the cache keys come across
		// with it.
		public ISearchStrategy Clone() => (ISearchStrategy)MemberwiseClone();

		private void EnsureRegex(string query)
		{
			if (_cacheReady
				&& _cachedQuery == query
				&& _cachedCaseSensitive == CaseSensitive
				&& _cachedWholeWord == WholeWord
				&& _cachedUseRegex == UseRegex)
			{
				return;
			}

			_cachedQuery = query;
			_cachedCaseSensitive = CaseSensitive;
			_cachedWholeWord = WholeWord;
			_cachedUseRegex = UseRegex;
			_cacheReady = true;

			string pattern = UseRegex ? query : Regex.Escape(query);
			if (WholeWord)
			{
				// Non-capturing group, not bare concatenation: `\b` binds tighter than `|`, so
				// wrapping `cat|dog` as `\bcat|dog\b` would only word-anchor the first and last
				// branch. Harmless for an escaped literal, required for a user-typed regex.
				pattern = $@"\b(?:{pattern})\b";
			}

			RegexOptions options = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;

			try
			{
				_cachedRegex = new Regex(pattern, options, MatchTimeout);
				_cachedValid = true;
			}
			catch (ArgumentException)
			{
				_cachedRegex = null;
				_cachedValid = false;
			}
		}
	}
}
