#nullable enable

using System;

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// Localization seam for the searchable combo box family. Consumers (e.g. TazUO) wire
	/// these funcs to their own localization system; Myra itself has no localization of its
	/// own and only knows the ASCII fallbacks.
	/// </summary>
	public static class SearchableComboBoxStrings
	{
		/// <summary>Placeholder shown in the empty search box. Falls back to "Search...".</summary>
		public static Func<string>? HintText;

		/// <summary>Label of the case-sensitivity toggle in <see cref="TextSearchComboBox{T}"/>'s header. Falls back to "Aa".</summary>
		public static Func<string>? CaseSensitive;

		/// <summary>Label of the whole-word toggle in <see cref="TextSearchComboBox{T}"/>'s header. Falls back to "ab|".</summary>
		public static Func<string>? WholeWord;

		/// <summary>Label of the regex toggle in <see cref="TextSearchComboBox{T}"/>'s header. Falls back to ".*".</summary>
		public static Func<string>? Regex;

		/// <summary>Text shown in place of the list when the query matches nothing. Falls back to "No results".</summary>
		public static Func<string>? NoResults;

		/// <summary>Tooltip on the search box while the query doesn't compile. Falls back to "Invalid regex".</summary>
		public static Func<string>? InvalidRegex;

		internal static string Get(Func<string>? f, string fallback) => f?.Invoke() ?? fallback;
	}
}
