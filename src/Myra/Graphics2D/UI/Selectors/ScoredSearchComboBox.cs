#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility.Search;

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// Searchable combo box whose ordering and visibility are driven purely by
	/// <see cref="SearchMatch.Score"/>. The strategy is a required constructor argument
	/// (there is no substring-search fallback) so fuzzy implementations such as TazUO's
	/// Levenshtein strategy can live outside Myra while still building on this class.
	/// </summary>
	public class ScoredSearchComboBox<T> : SearchableComboBox<T>
	{
		[Category("Behavior")]
		[DefaultValue(0d)]
		public double MinScore { get; set; }

		public ScoredSearchComboBox(ISearchStrategy strategy, string styleName = Stylesheet.DefaultStyleName) : base(styleName)
		{
			Strategy = strategy;
		}

		protected override IEnumerable<(T Item, SearchMatch Match)> FilterAndOrder(string query)
		{
			return base.FilterAndOrder(query).Where(r => r.Match.Score >= MinScore);
		}
	}
}
