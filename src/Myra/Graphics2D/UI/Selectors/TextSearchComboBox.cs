#nullable enable

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
		private TextQuerySearchStrategy? _textStrategy;

		/// <summary>
		/// Creates the combo box.
		/// </summary>
		/// <param name="styleName">Name of the stylesheet's combo box style to apply.</param>
		public TextSearchComboBox(string styleName = Stylesheet.DefaultStyleName) : base(styleName)
		{
		}

		/// <summary>
		/// Creates the <see cref="TextQuerySearchStrategy"/> the header's toggle buttons drive.
		/// </summary>
		/// <returns>The strategy to search with.</returns>
		protected override ISearchStrategy CreateDefaultStrategy()
		{
			_textStrategy = new TextQuerySearchStrategy();
			return _textStrategy;
		}

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

			header.Widgets.Add(CreateToggle(
				SearchableComboBoxStrings.Get(SearchableComboBoxStrings.CaseSensitive, "Aa"),
				_textStrategy!.CaseSensitive,
				v =>
				{
					_textStrategy.CaseSensitive = v;
					InvalidateFilter();
				}));

			header.Widgets.Add(CreateToggle(
				SearchableComboBoxStrings.Get(SearchableComboBoxStrings.WholeWord, "ab|"),
				_textStrategy.WholeWord,
				v =>
				{
					_textStrategy.WholeWord = v;
					InvalidateFilter();
				}));

			header.Widgets.Add(CreateToggle(
				SearchableComboBoxStrings.Get(SearchableComboBoxStrings.Regex, ".*"),
				_textStrategy.UseRegex,
				v =>
				{
					_textStrategy.UseRegex = v;
					InvalidateFilter();
				}));

			return header;
		}

		private static ToggleButton CreateToggle(string text, bool initial, System.Action<bool> onChanged)
		{
			var button = new ToggleButton
			{
				IsToggled = initial,
				Content = new Label
				{
					Text = text
				}
			};

			button.IsToggledChanged += (_, _) => onChanged(button.IsToggled);

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
