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
		/// <summary>
		/// Lowest <see cref="SearchMatch.Score"/> a match needs to stay in the dropdown. Zero (the
		/// default) keeps everything the strategy matched; raising it trims weak fuzzy hits.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(0d)]
		public double MinScore { get; set; }

		/// <summary>
		/// Creates the combo box.
		/// </summary>
		/// <param name="strategy">Strategy used to match and score items. Required - this class has no fallback.</param>
		/// <param name="styleName">Name of the stylesheet's combo box style to apply.</param>
		public ScoredSearchComboBox(ISearchStrategy strategy, string styleName = Stylesheet.DefaultStyleName) : base(styleName)
		{
			Strategy = strategy;
		}

		/// <summary>
		/// Filters and orders as the base class does, then drops anything scoring below
		/// <see cref="MinScore"/>.
		/// </summary>
		/// <param name="query">The current search text.</param>
		/// <returns>The matching items with their matches, best-scoring first.</returns>
		protected override IEnumerable<(T Item, SearchMatch Match)> FilterAndOrder(string query)
		{
			return base.FilterAndOrder(query).Where(r => r.Match.Score >= MinScore);
		}
	}
}
