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

		public TextSearchComboBox(string styleName = Stylesheet.DefaultStyleName) : base(styleName)
		{
		}

		protected override ISearchStrategy CreateDefaultStrategy()
		{
			_textStrategy = new TextQuerySearchStrategy();
			return _textStrategy;
		}

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

	public class TextSearchComboBox : TextSearchComboBox<string>
	{
		public TextSearchComboBox(string styleName = Stylesheet.DefaultStyleName) : base(styleName)
		{
		}
	}
}
