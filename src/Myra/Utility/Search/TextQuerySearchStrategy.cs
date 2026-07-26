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
		private string? _cachedQuery;
		private bool _cachedCaseSensitive;
		private bool _cachedWholeWord;
		private bool _cachedUseRegex;
		private Regex? _cachedRegex;
		private bool _cachedValid;
		private bool _cacheReady;

		[DefaultValue(false)]
		public bool CaseSensitive { get; set; }

		[DefaultValue(false)]
		public bool WholeWord { get; set; }

		[DefaultValue(false)]
		public bool UseRegex { get; set; }

		public bool IsQueryValid(string query)
		{
			EnsureRegex(query);
			return _cachedValid;
		}

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

			var m = _cachedRegex.Match(candidate);
			if (!m.Success)
			{
				return SearchMatch.None;
			}

			return new SearchMatch(true, 1d, new (int Start, int Len)[] { (m.Index, m.Length) });
		}

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
				pattern = $@"\b{pattern}\b";
			}

			RegexOptions options = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;

			try
			{
				_cachedRegex = new Regex(pattern, options);
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
