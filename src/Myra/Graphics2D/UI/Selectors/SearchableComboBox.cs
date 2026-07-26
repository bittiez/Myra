#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using Myra.Events;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility.Search;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
#elif STRIDE
using Stride.Core.Mathematics;
using Stride.Input;
#else
using System.Drawing;
using Myra.Platform;
#endif

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// Combo box with an inline search box filtering the dropdown as the user types. Concrete
	/// and usable on its own with a plain substring search; <see cref="ScoredSearchComboBox{T}"/>
	/// and <see cref="TextSearchComboBox{T}"/> build on top of it with different strategies.
	/// </summary>
	public class SearchableComboBox<T> : Widget, ISearchInputBoxOwner
	{
		/// <summary>Drawn thickness, in pixels, of the divider under the search header.</summary>
		private const int DividerThickness = 1;

		/// <summary>Empty space, in pixels, kept above and below the header divider.</summary>
		private const int DividerSpacing = 4;

		private readonly List<T> _items = new();
		private readonly List<T> _visibleItems = new();
		private readonly ToggleButton _button;
		private readonly SearchInputBox _searchBox;
		private readonly ListView _listView;
		private readonly PopupPanel _popup;
		private readonly Label _noResultsLabel;
		private readonly Widget _headerDivider;

		private Widget? _searchHeader;
		private Thickness _baseHeaderMargin;
		private bool _popupContentBuilt;
		private bool _regexInvalid;
		private IBrush? _defaultSearchBoxBorder;
		private IBrush? _invalidBorderBrush;
		private ISearchStrategy _strategy;
		private string _searchText = string.Empty;
		private bool _showSearchDivider = true;

		public override Desktop Desktop
		{
			get => base.Desktop;

			internal set
			{
				if (Desktop != null)
				{
					// _popup floats independently on Desktop.Widgets (added via
					// ShowContextMenu), not as our own child, so it doesn't get torn down
					// just because we're leaving this Desktop. Close it explicitly here,
					// before we unsubscribe, so it isn't left orphaned - still visible and
					// receiving input, but with no listener left to ever close it again.
					Close();

					Desktop.ContextMenuClosed -= DesktopOnContextMenuClosed;
					Desktop.ContextMenuClosing -= DesktopOnContextMenuClosing;
				}

				base.Desktop = value;

				if (Desktop != null)
				{
					Desktop.ContextMenuClosed += DesktopOnContextMenuClosed;
					Desktop.ContextMenuClosing += DesktopOnContextMenuClosing;
				}
			}
		}

		[Browsable(false)]
		[XmlIgnore]
		public IList<T> Items => _items;

		[Browsable(false)]
		[XmlIgnore]
		public Func<T, string> TextSelector { get; set; } = DefaultTextSelector;

		[Browsable(false)]
		[XmlIgnore]
		public Func<T, string>? TooltipSelector { get; set; }

		[Browsable(false)]
		[XmlIgnore]
		public T? SelectedItem { get; private set; }

		[Browsable(false)]
		[XmlIgnore]
		public int? SelectedIndex
		{
			get
			{
				if (SelectedItem == null)
				{
					return null;
				}

				int idx = _items.IndexOf(SelectedItem);
				return idx < 0 ? null : idx;
			}

			set
			{
				if (value == null || value.Value < 0 || value.Value >= _items.Count)
				{
					SetSelectedItem(default, false);
					return;
				}

				SetSelectedItem(_items[value.Value], false);
			}
		}

		public event EventHandler<ValueChangedEventArgs<T>>? SelectedItemChanged;

		[Category("Behavior")]
		[DefaultValue(true)]
		public bool FocusSearchOnClick { get; set; } = true;

		[Category("Behavior")]
		[DefaultValue(true)]
		public bool ClearSearchOnClose { get; set; } = true;

		[Browsable(false)]
		[XmlIgnore]
		public string SearchText
		{
			get => _searchText;

			set
			{
				value ??= string.Empty;
				if (_searchText == value)
				{
					return;
				}

				string old = _searchText;
				_searchText = value;
				_searchBox.Text = value;

				SearchTextChanged?.Invoke(this, new ValueChangedEventArgs<string>(old, value));
				OnQueryChanged();
				InvalidateFilter();
			}
		}

		public event EventHandler<ValueChangedEventArgs<string>>? SearchTextChanged;

		[Category("Behavior")]
		public string SearchHintText
		{
			get => _searchBox.HintText ?? string.Empty;
			set => _searchBox.HintText = value;
		}

		[Category("Behavior")]
		[DefaultValue(0)]
		public int SearchVisibleThreshold { get; set; }

		[Category("Behavior")]
		[DefaultValue(0)]
		public int MaxVisibleResults { get; set; }

		[Category("Behavior")]
		[DefaultValue(300)]
		public int? DropdownMaximumHeight
		{
			get => _listView.MaxHeight;
			set => _listView.MaxHeight = value;
		}

		/// <summary>
		/// Off by default: the popup is measured against every item (see
		/// <see cref="MeasureFullContentWidth"/>), so it already fits the widest one and a
		/// horizontal scrollbar is normally dead weight taking up a row's worth of height.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(false)]
		public bool AllowHorizontalScroll
		{
			get => _listView.ScrollViewer.ShowHorizontalScrollBar;
			set => _listView.ScrollViewer.ShowHorizontalScrollBar = value;
		}

		[Browsable(false)]
		[XmlIgnore]
		public ISearchStrategy Strategy
		{
			get => _strategy;

			set
			{
				_strategy = value;
				InvalidateFilter();
			}
		}

		[Browsable(false)]
		[XmlIgnore]
		public bool IsExpanded => _button.IsPressed;

		/// <summary>Background of the dropdown popup (header + list + no-results text).</summary>
		[Category("Appearance")]
		public IBrush? PopupBackground
		{
			get => _popup.Background;
			set => _popup.Background = value;
		}

		/// <summary>
		/// Border of the dropdown popup (header + list + no-results text). The header divider
		/// follows it, so the popup frame and the divider read as one - set
		/// <see cref="SearchDividerBrush"/> afterwards to color the divider independently.
		/// </summary>
		[Category("Layout")]
		public IBrush? PopupBorder
		{
			get => _popup.Border;
			set
			{
				_popup.Border = value;

				// Guarded: clearing the popup's border shouldn't blank the divider into
				// invisibility, only stop tinting it.
				if (value != null)
				{
					_headerDivider.Background = value;
				}
			}
		}

		/// <summary>
		/// Color of the divider under the search header. Defaults to following
		/// <see cref="PopupBorder"/>; assign to override.
		/// </summary>
		[Category("Appearance")]
		public IBrush? SearchDividerBrush
		{
			get => _headerDivider.Background;
			set => _headerDivider.Background = value;
		}

		/// <summary>
		/// Whether the horizontal divider between the search header and the dropdown items is
		/// shown. On by default; independent of <see cref="SearchVisibleThreshold"/>, which
		/// governs whether the header (and this divider) show at all.
		/// </summary>
		[Category("Layout")]
		[DefaultValue(true)]
		public bool ShowSearchDivider
		{
			get => _showSearchDivider;
			set
			{
				_showSearchDivider = value;
				UpdateSearchHeaderVisibility();
			}
		}

		[Category("Layout")]
		public Thickness PopupBorderThickness
		{
			get => _popup.BorderThickness;
			set => _popup.BorderThickness = value;
		}

		[Category("Layout")]
		public Thickness PopupPadding
		{
			get => _popup.Padding;
			set => _popup.Padding = value;
		}

		public SearchableComboBox(string styleName = Stylesheet.DefaultStyleName)
		{
			_button = new ToggleButton
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Content = new Label
				{
					Text = string.Empty
				}
			};
			_button.PressedChanged += ButtonOnPressedChanged;

			ChildrenLayout = new SingleItemLayout<ToggleButton>(this)
			{
				Child = _button
			};

			AcceptsKeyboardFocus = true;

			_searchBox = new SearchInputBox
			{
				Owner = this,
				HintText = SearchableComboBoxStrings.Get(SearchableComboBoxStrings.HintText, "Search...")
			};
			_searchBox.TextChangedByUser += SearchBoxOnTextChangedByUser;
			_defaultSearchBoxBorder = _searchBox.Border;

			_listView = new ListView(null)
			{
				MaxHeight = 300
			};
			_listView.ScrollViewer.ShowHorizontalScrollBar = false;

			// Commit on activation, not on SelectedIndexChanged: the filter pre-selects the top
			// match, and re-selecting an already-selected row raises no change event - so a
			// selection-driven commit would leave the first (most relevant) row unclickable.
			_listView.ItemActivated += ListViewOnItemActivated;

			_popup = new PopupPanel
			{
				Owner = this,
				AcceptsKeyboardFocus = true,
				// Sane default so header/rows/no-results text don't touch the popup's
				// border; ApplySearchableComboBoxStyle only overrides this when the active
				// style actually defines a padding, and PopupPadding remains a direct,
				// always-available override on top of either.
				Padding = new Thickness(6)
			};

			_noResultsLabel = new Label
			{
				Text = SearchableComboBoxStrings.Get(SearchableComboBoxStrings.NoResults, "No results"),
				Visible = false
			};

			var dividerMargin = new Thickness(0, DividerSpacing);
			_headerDivider = new Widget
			{
				// Height is the widget's *total* footprint - Myra subtracts margin (and border
				// and padding) from it to get the drawn box - so the vertical margin has to be
				// baked in here. Height = DividerThickness with a non-zero margin would leave
				// the background bounds empty (negative, in fact) and draw nothing at all.
				Height = DividerThickness + dividerMargin.Top + dividerMargin.Bottom,
				Margin = dividerMargin,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				// Fallback only - kept in sync with PopupBorder (constructor, PopupBorder
				// setter, ApplySearchableComboBoxStyle) so the divider always reads as part of
				// the same popup frame instead of a separately-colored line.
				Background = new SolidBrush(new Color(255, 255, 255, 40))
			};

			HorizontalAlignment = HorizontalAlignment.Left;
			VerticalAlignment = VerticalAlignment.Top;

			_strategy = CreateDefaultStrategy();

			SetStyle(styleName);
		}

		private static string DefaultTextSelector(T x) => x?.ToString() ?? string.Empty;

		private void ButtonOnPressedChanged(object? sender, EventArgs e)
		{
			if (_button.IsPressed)
			{
				Open();
			}
		}

		private void DesktopOnContextMenuClosed(object? sender, GenericEventArgs<Widget> args)
		{
			if (args.Data != _popup)
			{
				return;
			}

			if (!IsMouseInside)
			{
				_button.IsPressed = false;
			}

			if (ClearSearchOnClose)
			{
				SearchText = string.Empty;
			}

			OnPopupClosed();
		}

		private void DesktopOnContextMenuClosing(object? sender, CancellableEventArgs<Widget> args)
		{
			if (args.Data != _popup)
			{
				return;
			}

			// Desktop closes a context menu based on ContextMenu.IsTouchInside, a flag cached
			// during the input pass - and anything that ends up above the popup in that pass
			// (an overlay widget consuming the touch, say) clears it even though the click did
			// land on us. Geometry is the authority here: recompute against the popup's live
			// bounds and cancel the close whenever the touch is genuinely inside it, so the
			// popup only ever closes on a real outside click or a committed selection.
			Point? touchPos = Desktop?.TouchPosition;
			if (touchPos != null && _popup.ContainsGlobalPoint(touchPos.Value))
			{
				args.Cancel = true;
			}
		}

		private void SearchBoxOnTextChangedByUser(object? sender, ValueChangedEventArgs<string> e)
		{
			SearchText = e.NewValue ?? string.Empty;
		}

		private void ListViewOnItemActivated(object? sender, GenericEventArgs<Widget> e)
		{
			int idx = _listView.Widgets.IndexOf(e.Data);
			if (idx < 0 || idx >= _visibleItems.Count)
			{
				return;
			}

			CommitItem(_visibleItems[idx]);
		}

		void ISearchInputBoxOwner.OnSearchBoxKeyUp() => NavigateList(Keys.Up);

		void ISearchInputBoxOwner.OnSearchBoxKeyDown() => NavigateList(Keys.Down);

		void ISearchInputBoxOwner.OnSearchBoxKeyEnter() => CommitSelectedOrTop();

		void ISearchInputBoxOwner.OnSearchBoxKeyEscape() => Close();

		/// <summary>
		/// Moves the list's highlight without committing - selection changes are just a
		/// highlight here, committing is Enter (<see cref="CommitSelectedOrTop"/>) or a row
		/// click (<see cref="ListView.ItemActivated"/>).
		/// </summary>
		private void NavigateList(Keys k) => _listView.OnKeyDown(k);

		private void CommitSelectedOrTop()
		{
			int idx = _listView.SelectedIndex ?? (_visibleItems.Count > 0 ? 0 : -1);
			if (idx < 0 || idx >= _visibleItems.Count)
			{
				return;
			}

			CommitItem(_visibleItems[idx]);
		}

		private void CommitItem(T item)
		{
			// Hide the popup BEFORE raising the event: rebuilding the widget tree from
			// inside a listener while the popup is still open corrupts it.
			Desktop?.HideContextMenu();

			SetSelectedItem(item, true);
		}

		private void SetSelectedItem(T? item, bool committed)
		{
			T? old = SelectedItem;
			SelectedItem = item;

			_button.Content = new Label
			{
				Text = item != null ? TextSelector(item) : string.Empty
			};

			if (committed)
			{
				OnSelectionCommitted(item!);
			}

			SelectedItemChanged?.Invoke(this, new ValueChangedEventArgs<T>(old!, item!));
		}

		public void Open()
		{
			if (Desktop == null)
			{
				return;
			}

			EnsurePopupContent();
			InvalidateFilter();

			_popup.Width = BorderBounds.Width;
			var pos = ToGlobal(new Point(0, Bounds.Height));
			Desktop.ShowContextMenu(_popup, pos);

			if (FocusSearchOnClick)
			{
				Desktop.FocusedKeyboardWidget = _searchBox;
			}

			OnPopupOpened();
		}

		public void Close()
		{
			if (Desktop == null || Desktop.ContextMenu != _popup)
			{
				return;
			}

			Desktop.HideContextMenu();
		}

		private void EnsurePopupContent()
		{
			if (_popupContentBuilt)
			{
				return;
			}

			_popupContentBuilt = true;

			_searchHeader = BuildSearchHeader();
			_baseHeaderMargin = _searchHeader.Margin;

			_popup.Widgets.Add(_searchHeader);
			_popup.Widgets.Add(_headerDivider);
			_popup.Widgets.Add(_listView);
			_popup.Widgets.Add(_noResultsLabel);

			UpdateSearchHeaderVisibility();
		}

		private void UpdateSearchHeaderVisibility()
		{
			if (_searchHeader == null)
			{
				return;
			}

			bool visible = _items.Count >= SearchVisibleThreshold;
			bool dividerVisible = visible && ShowSearchDivider;

			_searchHeader.Visible = visible;
			_headerDivider.Visible = dividerVisible;

			// Whatever sits right below the search box - the divider, or the first row when the
			// divider is off - must not end up flush against its bottom edge; scrolling the list
			// then reads as the rows "touching" the box. The divider carries its own spacing, so
			// only the divider-less case needs the header to reserve that gap itself.
			int extraBottom = dividerVisible ? 0 : DividerSpacing * 2 + DividerThickness;
			_searchHeader.Margin = new Thickness(
				_baseHeaderMargin.Left,
				_baseHeaderMargin.Top,
				_baseHeaderMargin.Right,
				_baseHeaderMargin.Bottom + extraBottom);
		}

		protected void InvalidateFilter()
		{
			_visibleItems.Clear();
			_listView.Widgets.Clear();

			string query = _searchText;
			bool queryValid = Strategy.IsQueryValid(query);

			UpdateInvalidQueryVisuals(!queryValid);

			if (queryValid)
			{
				int count = 0;
				foreach ((T item, SearchMatch match) in FilterAndOrder(query))
				{
					if (MaxVisibleResults > 0 && count >= MaxVisibleResults)
					{
						break;
					}

					_visibleItems.Add(item);
					_listView.Widgets.Add(CreateItemWidget(item, match));
					count++;
				}
			}

			bool noResults = _visibleItems.Count == 0;
			_listView.Visible = !noResults;
			_noResultsLabel.Visible = noResults;

			UpdateSearchHeaderVisibility();

			// Highlight the best match so Enter has an obvious target. Harmless now that
			// committing no longer rides on selection changes.
			if (_visibleItems.Count > 0)
			{
				_listView.SelectedIndex = 0;
			}
		}

		private void UpdateInvalidQueryVisuals(bool invalid)
		{
			if (invalid == _regexInvalid)
			{
				return;
			}

			_regexInvalid = invalid;

			if (invalid)
			{
				_invalidBorderBrush ??= new SolidBrush(Color.Red);
				_searchBox.Border = _invalidBorderBrush;
				_searchBox.Tooltip = SearchableComboBoxStrings.Get(SearchableComboBoxStrings.InvalidRegex, "Invalid regex");
			}
			else
			{
				_searchBox.Border = _defaultSearchBoxBorder;
				_searchBox.Tooltip = null;
			}
		}

		protected virtual Widget BuildSearchHeader() => _searchBox;

		protected virtual ISearchStrategy CreateDefaultStrategy() => new SubstringSearchStrategy();

		protected virtual IEnumerable<(T Item, SearchMatch Match)> FilterAndOrder(string query)
		{
			var results = new List<(T Item, SearchMatch Match, int OriginalIndex)>(_items.Count);

			for (int i = 0; i < _items.Count; i++)
			{
				T item = _items[i];
				string text = TextSelector(item) ?? string.Empty;
				SearchMatch match = Strategy.Match(text, query);
				if (match.IsMatch)
				{
					results.Add((item, match, i));
				}
			}

			return results
				.OrderByDescending(r => r.Match.Score)
				.ThenBy(r => r.OriginalIndex)
				.Select(r => (r.Item, r.Match));
		}

		protected virtual Widget CreateItemWidget(T item, SearchMatch match)
		{
			var label = new Label
			{
				Text = TextSelector(item) ?? string.Empty
			};

			string? tooltip = TooltipSelector?.Invoke(item);
			if (!string.IsNullOrEmpty(tooltip))
			{
				label.Tooltip = tooltip;
			}

			return label;
		}

		protected virtual void OnQueryChanged()
		{
		}

		protected virtual void OnPopupOpened()
		{
		}

		protected virtual void OnPopupClosed()
		{
		}

		protected virtual void OnSelectionCommitted(T item)
		{
		}

		protected override Point InternalMeasure(Point availableSize)
		{
			EnsurePopupContent();

			var result = base.InternalMeasure(availableSize);

			int popupWidth = MeasureFullContentWidth();
			if (popupWidth > result.X)
			{
				result.X = popupWidth;
			}

			result.X += 32;

			return result;
		}

		/// <summary>
		/// Measures the popup against every item, not whatever's currently filtered into
		/// <see cref="_listView"/> — otherwise the dropdown (and the closed button, since its
		/// width is derived from this) would shrink and grow as the user types a search query.
		/// </summary>
		private int MeasureFullContentWidth()
		{
			// WidgetsCollection.CopyTo throws NotImplementedException, so `new List<>(...)`
			// (which prefers ICollection<T>.CopyTo over enumerating) crashes here — build the
			// snapshot with an explicit loop instead, which only needs the enumerator.
			var filteredWidgets = new List<Widget>();
			foreach (Widget widget in _listView.Widgets)
			{
				filteredWidgets.Add(widget);
			}

			var savedWidth = _popup.Width;
			var wasVisible = _popup.Visible;

			_popup.Width = null;
			_popup.Visible = true;

			_listView.Widgets.Clear();
			foreach (T item in _items)
			{
				_listView.Widgets.Add(CreateItemWidget(item, SearchMatch.Exact(1d)));
			}

			Point measured = _popup.Measure(new Point(10000, 10000));

			_listView.Widgets.Clear();
			foreach (Widget widget in filteredWidgets)
			{
				_listView.Widgets.Add(widget);
			}

			_popup.Width = savedWidth;
			_popup.Visible = wasVisible;

			return measured.X;
		}

		protected override void InternalArrange()
		{
			base.InternalArrange();

			_popup.Width = BorderBounds.Width;
		}

		public override void OnKeyDown(Keys k)
		{
			base.OnKeyDown(k);
		}

		/// <summary>
		/// A widget can go invisible (tab switched away, container collapsed, ...) without
		/// ever leaving the Desktop, which the Desktop setter's cleanup doesn't catch. Close
		/// the popup here too so it doesn't keep floating over a hidden owner.
		/// </summary>
		public override void OnVisibleChanged()
		{
			base.OnVisibleChanged();

			if (!Visible)
			{
				Close();
			}
		}

		public void ApplySearchableComboBoxStyle(ComboBoxStyle style)
		{
			if (style.ListBoxStyle != null)
			{
				var dropdownMaximumHeight = DropdownMaximumHeight;
				_listView.ApplyListBoxStyle(style.ListBoxStyle);
				DropdownMaximumHeight = dropdownMaximumHeight;

				// The popup is a panel around the list (header + no-results text live outside
				// _listView's own bounds), so without this it has no background/border of its
				// own and renders as if the search box and rows were floating over nothing.
				_popup.Background = style.ListBoxStyle.Background;
				_popup.Border = style.ListBoxStyle.Border;
				_popup.BorderThickness = style.ListBoxStyle.BorderThickness;

				// Only follow the style's border color if it defines one - TazUO's combo skin
				// leaves ListBoxStyle.Border null, and taking that would blank the divider's
				// constructor fallback and make the line disappear entirely.
				if (style.ListBoxStyle.Border != null)
				{
					_headerDivider.Background = style.ListBoxStyle.Border;
				}

				// Only take the style's padding if it actually defines one - TazUO's combo
				// skin leaves ListBoxStyle.Padding at zero, and that shouldn't stomp the
				// built-in default set in the constructor.
				Thickness stylePadding = style.ListBoxStyle.Padding;
				if (stylePadding.Left > 0 || stylePadding.Right > 0 || stylePadding.Top > 0 || stylePadding.Bottom > 0)
				{
					_popup.Padding = stylePadding;
				}

				// _listView just got that same background/border/padding from
				// ApplyListBoxStyle above (it's meant to stand alone under ComboView). Clear
				// its copy so the popup is a single bordered panel instead of a bordered
				// panel with a second, differently-inset bordered panel nested inside it.
				_listView.Background = null;
				_listView.Border = null;
				_listView.BorderThickness = Thickness.Zero;
				_listView.Padding = Thickness.Zero;
			}

			_button.ApplyButtonStyle(style);
		}

		protected override void InternalSetStyle(Stylesheet stylesheet, string name)
		{
			ApplySearchableComboBoxStyle(stylesheet.ComboBoxStyles.SafelyGetStyle(name));
		}

		protected internal override void CopyFrom(Widget w)
		{
			base.CopyFrom(w);

			var other = (SearchableComboBox<T>)w;

			TextSelector = other.TextSelector;
			TooltipSelector = other.TooltipSelector;
			FocusSearchOnClick = other.FocusSearchOnClick;
			ClearSearchOnClose = other.ClearSearchOnClose;
			SearchHintText = other.SearchHintText;
			SearchVisibleThreshold = other.SearchVisibleThreshold;
			MaxVisibleResults = other.MaxVisibleResults;
			DropdownMaximumHeight = other.DropdownMaximumHeight;
			AllowHorizontalScroll = other.AllowHorizontalScroll;
			PopupBackground = other.PopupBackground;
			PopupBorder = other.PopupBorder;
			PopupBorderThickness = other.PopupBorderThickness;
			PopupPadding = other.PopupPadding;
			ShowSearchDivider = other.ShowSearchDivider;
			SearchDividerBrush = other.SearchDividerBrush;
			Strategy = other.Strategy;

			foreach (T item in other._items)
			{
				_items.Add(item);
			}

			SelectedIndex = other.SelectedIndex;
		}
	}
}
