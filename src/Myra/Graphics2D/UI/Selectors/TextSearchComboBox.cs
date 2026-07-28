#nullable enable

using System;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility.Search;

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// Searchable combo box defaulting to <see cref="TextQuerySearchStrategy"/>, with a search
	/// header appending case-sensitive / whole-word / regex toggle buttons.
	/// </summary>
	public class TextSearchComboBox<T> : SearchableComboBox<T>
	{
		private ToggleButton? _caseSensitiveToggle;
		private ToggleButton? _wholeWordToggle;
		private ToggleButton? _regexToggle;
		private bool _syncingToggles;

		/// <summary>
		/// Creates the combo box.
		/// </summary>
		/// <param name="styleName">Name of the stylesheet's combo box style to apply.</param>
		public TextSearchComboBox(string styleName = Stylesheet.DefaultStyleName) : base(styleName)
		{
		}

		/// <summary>
		/// The active strategy, when it is one the header's toggles can drive. Null once
		/// <see cref="SearchableComboBox{T}.Strategy"/> has been replaced with something else - the
		/// toggles then have nothing to write to and go inactive rather than keep mutating a
		/// strategy nobody searches with.
		/// </summary>
		private TextQuerySearchStrategy? TextStrategy => Strategy as TextQuerySearchStrategy;

		/// <summary>
		/// Creates the <see cref="TextQuerySearchStrategy"/> the header's toggle buttons drive.
		/// </summary>
		/// <returns>The strategy to search with.</returns>
		protected override ISearchStrategy CreateDefaultStrategy() => new TextQuerySearchStrategy();

		/// <summary>
		/// Builds the search box plus the case-sensitive, whole-word and regex toggles, each wired
		/// to the matching <see cref="TextQuerySearchStrategy"/> flag.
		/// </summary>
		/// <returns>The widget to use as the dropdown's header.</returns>
		protected override Widget BuildSearchHeader()
		{
			var header = new HorizontalStackPanel
			{
				Spacing = 4
			};

			Widget searchBoxHost = base.BuildSearchHeader();
			searchBoxHost.HorizontalAlignment = HorizontalAlignment.Stretch;
			header.Widgets.Add(searchBoxHost);

			_caseSensitiveToggle = CreateToggle(
				SearchableComboBoxStrings.Get(SearchableComboBoxStrings.CaseSensitive, "Aa"),
				TextStrategy?.CaseSensitive ?? false,
				(s, v) => s.CaseSensitive = v);

			_wholeWordToggle = CreateToggle(
				SearchableComboBoxStrings.Get(SearchableComboBoxStrings.WholeWord, "ab|"),
				TextStrategy?.WholeWord ?? false,
				(s, v) => s.WholeWord = v);

			_regexToggle = CreateToggle(
				SearchableComboBoxStrings.Get(SearchableComboBoxStrings.Regex, ".*"),
				TextStrategy?.UseRegex ?? false,
				(s, v) => s.UseRegex = v);

			header.Widgets.Add(_caseSensitiveToggle);
			header.Widgets.Add(_wholeWordToggle);
			header.Widgets.Add(_regexToggle);

			return header;
		}

		/// <summary>
		/// Pushes the new strategy's flags onto the toggles, so they show - and go on driving -
		/// whatever is actually being searched with.
		/// </summary>
		protected override void OnStrategyChanged()
		{
			base.OnStrategyChanged();

			TextQuerySearchStrategy? strategy = TextStrategy;
			if (strategy == null || _caseSensitiveToggle == null || _wholeWordToggle == null || _regexToggle == null)
			{
				return;
			}

			// Suppressed: these assignments raise IsToggledChanged, which would write straight
			// back into the strategy and re-filter three times over for no change.
			_syncingToggles = true;
			try
			{
				_caseSensitiveToggle.IsToggled = strategy.CaseSensitive;
				_wholeWordToggle.IsToggled = strategy.WholeWord;
				_regexToggle.IsToggled = strategy.UseRegex;
			}
			finally
			{
				_syncingToggles = false;
			}
		}

		private ToggleButton CreateToggle(string text, bool initial, Action<TextQuerySearchStrategy, bool> apply)
		{
			var button = new ToggleButton
			{
				IsToggled = initial,
				Content = new Label
				{
					Text = text
				}
			};

			button.IsToggledChanged += (_, _) =>
			{
				if (_syncingToggles)
				{
					return;
				}

				// Resolved per click rather than captured: Strategy can be replaced at any point
				// (CopyFrom clones it, callers may swap it outright), and the toggle has to drive
				// the strategy in force now, not the one that existed when the header was built.
				TextQuerySearchStrategy? strategy = TextStrategy;
				if (strategy == null)
				{
					return;
				}

				apply(strategy, button.IsToggled);
				InvalidateFilter();
			};

			return button;
		}
	}

	/// <summary>
	/// <see cref="TextSearchComboBox{T}"/> over plain strings.
	/// </summary>
	public class TextSearchComboBox : TextSearchComboBox<string>
	{
		/// <summary>
		/// Creates the combo box.
		/// </summary>
		/// <param name="styleName">Name of the stylesheet's combo box style to apply.</param>
		public TextSearchComboBox(string styleName = Stylesheet.DefaultStyleName) : base(styleName)
		{
		}
	}
}
