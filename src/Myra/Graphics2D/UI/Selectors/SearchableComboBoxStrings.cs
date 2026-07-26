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
		public static Func<string>? HintText;
		public static Func<string>? CaseSensitive;
		public static Func<string>? WholeWord;
		public static Func<string>? Regex;
		public static Func<string>? NoResults;
		public static Func<string>? InvalidRegex;

		internal static string Get(Func<string>? f, string fallback) => f?.Invoke() ?? fallback;
	}
}
