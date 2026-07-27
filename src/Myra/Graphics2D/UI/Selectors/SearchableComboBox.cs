#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

		private readonly ObservableCollection<T> _items = new();
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
		private int? _contentWidth;
		private bool _filterDirty = true;
		private Func<T, string> _textSelector = DefaultTextSelector;

		/// <summary>
		/// The Desktop this widget lives on. Overridden to close the dropdown and move the
		/// context-menu subscriptions across whenever it changes.
		/// </summary>
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

		/// <summary>
		/// The full, unfiltered item list, in the order they were added - which is also the order
		/// equally-scoring matches appear in, and what <see cref="SelectedIndex"/> indexes into.
		/// The dropdown tracks changes to it on its own.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public IList<T> Items => _items;

		/// <summary>
		/// Maps an item to the text shown for it and searched against. Defaults to
		/// <c>ToString()</c> (empty for null).
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Func<T, string> TextSelector
		{
			get => _textSelector;

			set
			{
				_textSelector = value;
				InvalidateContentWidth();
			}
		}

		/// <summary>
		/// Optional per-item tooltip text. Items whose selector returns null or empty get no
		/// tooltip. Null (the default) means no tooltips at all.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Func<T, string>? TooltipSelector { get; set; }

		/// <summary>
		/// The currently selected item, or default when nothing is selected. Set it through
		/// <see cref="SelectedIndex"/>.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public T? SelectedItem { get; private set; }

		/// <summary>
		/// Index of the selected item within <see cref="Items"/>, or null when nothing is
		/// selected. Setting it out of range (or to null) clears the selection. Assigning does not
		/// count as the user committing a choice, so
		/// <see cref="OnSelectionCommitted"/> isn't called - but
		/// <see cref="SelectedItemChanged"/> still fires.
		/// </summary>
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

		/// <summary>
		/// Raised whenever the selection changes, however it changed - a user pick or an assignment
		/// to <see cref="SelectedIndex"/>. When a user pick triggers it the dropdown is already
		/// closed, so handlers are free to rebuild the widget tree.
		/// </summary>
		public event EventHandler<ValueChangedEventArgs<T>>? SelectedItemChanged;

		/// <summary>
		/// Whether opening the dropdown puts the keyboard caret straight in the search box, so the
		/// user can type without clicking it first. On by default.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(true)]
		public bool FocusSearchOnClick { get; set; } = true;

		/// <summary>
		/// Whether closing the dropdown resets <see cref="SearchText"/>, so it reopens showing the
		/// full list rather than the last query's results. On by default.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(true)]
		public bool ClearSearchOnClose { get; set; } = true;

		/// <summary>
		/// The current query. Setting it updates the search box, raises
		/// <see cref="SearchTextChanged"/> and re-filters the dropdown; null is treated as empty.
		/// </summary>
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

		/// <summary>Raised after <see cref="SearchText"/> changes, before the dropdown is re-filtered.</summary>
		public event EventHandler<ValueChangedEventArgs<string>>? SearchTextChanged;

		/// <summary>
		/// Placeholder text shown in the empty search box. Defaults to
		/// <see cref="SearchableComboBoxStrings.HintText"/>.
		/// </summary>
		[Category("Behavior")]
		public string SearchHintText
		{
			get => _searchBox.HintText ?? string.Empty;
			set => _searchBox.HintText = value;
		}

		/// <summary>
		/// Item count from which the search header (and its divider) is shown - below it the
		/// dropdown is a plain list, since searching a handful of items is pointless. Zero (the
		/// default) always shows it.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(0)]
		public int SearchVisibleThreshold { get; set; }

		/// <summary>
		/// Cap on how many matches the dropdown lists, keeping a broad query from rendering
		/// thousands of rows. Zero (the default) means no cap. The best-scoring matches survive
		/// the cut, since the list is trimmed after ordering.
		/// </summary>
		[Category("Behavior")]
		[DefaultValue(0)]
		public int MaxVisibleResults { get; set; }

		/// <summary>
		/// Height, in pixels, at which the item list starts scrolling instead of growing. Null
		/// lets it grow without limit.
		/// </summary>
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

		/// <summary>
		/// Strategy used to match and score items against the query. Assigning re-filters the
		/// dropdown immediately. Defaults to whatever <see cref="CreateDefaultStrategy"/> returns.
		/// </summary>
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

		/// <summary>Whether the dropdown is currently open.</summary>
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

		/// <summary>Width, in pixels per edge, of the dropdown popup's border.</summary>
		[Category("Layout")]
		public Thickness PopupBorderThickness
		{
			get => _popup.BorderThickness;
			set => _popup.BorderThickness = value;
		}

		/// <summary>
		/// Space between the dropdown popup's border and its contents. Taken from the style when it
		/// defines one, otherwise a built-in default; assigning always wins over both.
		/// </summary>
		[Category("Layout")]
		public Thickness PopupPadding
		{
			get => _popup.Padding;
			set => _popup.Padding = value;
		}

		/// <summary>
		/// Creates the combo box.
		/// </summary>
		/// <param name="styleName">Name of the stylesheet's combo box style to apply.</param>
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
			_items.CollectionChanged += ItemsOnCollectionChanged;

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

		/// <summary>
		/// Opens the dropdown directly below the widget, as the Desktop's context menu. No-op when
		/// the widget isn't on a Desktop.
		/// </summary>
		public void Open()
		{
			if (Desktop == null)
			{
				return;
			}

			EnsurePopupContent();
			RebuildFilterIfDirty();

			_popup.Width = BorderBounds.Width;
			var pos = ToGlobal(new Point(0, Bounds.Height));
			Desktop.ShowContextMenu(_popup, pos);

			if (FocusSearchOnClick)
			{
				Desktop.FocusedKeyboardWidget = _searchBox;
			}

			OnPopupOpened();
		}

		/// <summary>
		/// Closes the dropdown. No-op unless it's the Desktop's current context menu, so this
		/// never closes someone else's menu.
		/// </summary>
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

		private void ItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			InvalidateContentWidth();
			InvalidateFilter();
		}

		/// <summary>
		/// Marks the dropdown's rows stale, to be rebuilt from the current query and item list
		/// before it is next shown. Call it after changing anything the active strategy scores by;
		/// changes to <see cref="Items"/>, <see cref="SearchText"/> and <see cref="Strategy"/>
		/// already do.
		/// </summary>
		protected void InvalidateFilter()
		{
			_filterDirty = true;

			// Rebuilding is O(items) and allocates a widget per match, so a closed dropdown just
			// takes the flag - that way bulk item edits cost one rebuild at Open(), not one each.
			if (IsExpanded)
			{
				RebuildFilter();
			}
		}

		private void RebuildFilterIfDirty()
		{
			if (_filterDirty)
			{
				RebuildFilter();
			}
		}

		private void RebuildFilter()
		{
			_filterDirty = false;
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

		/// <summary>
		/// Builds the widget placed above the divider in the dropdown. The base implementation is
		/// the bare search box; override to surround it with extra controls, as
		/// <see cref="TextSearchComboBox{T}"/> does with its search-mode toggles. Called once, the
		/// first time the popup's content is built.
		/// </summary>
		/// <returns>The header widget. Must contain the search box.</returns>
		protected virtual Widget BuildSearchHeader() => _searchBox;

		/// <summary>
		/// Supplies the initial <see cref="Strategy"/>. Called from the constructor, so an override
		/// must not rely on subclass state that hasn't been initialised yet.
		/// </summary>
		/// <returns>The strategy to start with.</returns>
		protected virtual ISearchStrategy CreateDefaultStrategy() => new SubstringSearchStrategy();

		/// <summary>
		/// Picks the items matching <paramref name="query"/> and puts them in display order:
		/// highest <see cref="SearchMatch.Score"/> first, ties broken by their position in
		/// <see cref="Items"/> so equally-good matches keep the order they were added in.
		/// </summary>
		/// <param name="query">The current search text.</param>
		/// <returns>The matching items with their matches, in the order they should be listed.</returns>
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

		/// <summary>
		/// Builds the row widget for one matched item. The base implementation is a label carrying
		/// <see cref="TextSelector"/>'s text and, if any, <see cref="TooltipSelector"/>'s tooltip;
		/// override to render richer rows, e.g. highlighting <see cref="SearchMatch.Spans"/>.
		/// </summary>
		/// <param name="item">The item to build a row for.</param>
		/// <param name="match">That item's match, whose spans say which parts of the text matched.</param>
		/// <returns>The row widget. The list wraps it in its own clickable row.</returns>
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

		/// <summary>
		/// Called after <see cref="SearchText"/> changed and before the list is re-filtered - the
		/// hook for adjusting the strategy to the new query (fuzziness by query length, say).
		/// </summary>
		protected virtual void OnQueryChanged()
		{
		}

		/// <summary>Called after the dropdown has opened and been populated.</summary>
		protected virtual void OnPopupOpened()
		{
		}

		/// <summary>
		/// Called after the dropdown closed, however it closed - a pick, a click outside, Escape, or
		/// the widget leaving the Desktop.
		/// </summary>
		protected virtual void OnPopupClosed()
		{
		}

		/// <summary>
		/// Called when the user actually picks an item, by clicking a row or pressing Enter - unlike
		/// <see cref="SelectedItemChanged"/>, assignments to <see cref="SelectedIndex"/> don't get
		/// here. The dropdown is already closed at this point.
		/// </summary>
		/// <param name="item">The item the user picked.</param>
		protected virtual void OnSelectionCommitted(T item)
		{
		}

		/// <summary>
		/// Measures the closed widget wide enough for the widest item, so the button doesn't resize
		/// as the selection changes.
		/// </summary>
		/// <param name="availableSize">Space available to the widget.</param>
		/// <returns>The desired size.</returns>
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
			// Heavyweight: a widget per item, plus a swap of the live list. Re-filtering invalidates
			// the popup's layout, so without this it would rerun on every keystroke.
			if (_contentWidth != null)
			{
				return _contentWidth.Value;
			}

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
			// Clear() drops the selection and the restore loop re-wraps every row in a fresh
			// ListViewButton, so the highlight can't survive the swap on its own.
			var savedSelectedIndex = _listView.SelectedIndex;

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

			if (savedSelectedIndex != null && savedSelectedIndex.Value < filteredWidgets.Count)
			{
				_listView.SelectedIndex = savedSelectedIndex;
			}

			_popup.Width = savedWidth;
			_popup.Visible = wasVisible;

			_contentWidth = measured.X;

			return measured.X;
		}

		/// <summary>
		/// Drops the cached full-content width, so the next measure pass recomputes how wide the
		/// widget has to be to fit its widest item. <see cref="Items"/> changes do this on their
		/// own; call it after anything else that changes an item's rendered width.
		/// </summary>
		protected void InvalidateContentWidth()
		{
			_contentWidth = null;
			InvalidateMeasure();
		}

		/// <summary>
		/// Arranges the widget and keeps the dropdown's width in sync with it, so an open popup
		/// still lines up after a resize.
		/// </summary>
		protected override void InternalArrange()
		{
			base.InternalArrange();

			_popup.Width = BorderBounds.Width;
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

		/// <summary>
		/// Applies a combo box style to the button and, via its list box style, to the dropdown -
		/// which is a single bordered panel here rather than <c>ComboView</c>'s bare list, so the
		/// list's own frame is cleared and the panel takes it over.
		/// </summary>
		/// <param name="style">The style to apply.</param>
		public void ApplySearchableComboBoxStyle(ComboBoxStyle style)
		{
			if (style.ListBoxStyle != null)
			{
				ApplyDropdownListStyle(style.ListBoxStyle);

				// The header and no-results text live outside _listView's bounds, so the frame has
				// to be on the popup or they render over nothing.
				_popup.Background = style.ListBoxStyle.Background;
				_popup.Border = style.ListBoxStyle.Border;
				_popup.BorderThickness = style.ListBoxStyle.BorderThickness;

				// TazUO's combo skin leaves these unset; taking them anyway would blank the
				// divider and stomp the constructor's padding default.
				if (style.ListBoxStyle.Border != null)
				{
					_headerDivider.Background = style.ListBoxStyle.Border;
				}

				Thickness stylePadding = style.ListBoxStyle.Padding;
				if (stylePadding.Left > 0 || stylePadding.Right > 0 || stylePadding.Top > 0 || stylePadding.Bottom > 0)
				{
					_popup.Padding = stylePadding;
				}
			}
			else if (_listView.ListBoxStyle == null)
			{
				// ListView.Wrap() dereferences ListBoxStyle on every row added, and the list is
				// built style-less on purpose - fall back so adding items can't NRE.
				ApplyDropdownListStyle(Stylesheet.Current.ListBoxStyles.SafelyGetStyle(Stylesheet.DefaultStyleName));
			}

			// Fonts and metrics just moved, so the cached width no longer describes the items.
			InvalidateContentWidth();

			_button.ApplyButtonStyle(style);
		}

		// ApplyListBoxStyle stomps MaxHeight (the dropdown's height cap) and gives the list a frame
		// meant for a standalone ComboView list, which would nest inside the popup's own.
		private void ApplyDropdownListStyle(ListBoxStyle listBoxStyle)
		{
			var dropdownMaximumHeight = DropdownMaximumHeight;
			_listView.ApplyListBoxStyle(listBoxStyle);
			DropdownMaximumHeight = dropdownMaximumHeight;

			_listView.Background = null;
			_listView.Border = null;
			_listView.BorderThickness = Thickness.Zero;
			_listView.Padding = Thickness.Zero;
		}

		/// <summary>
		/// Applies the named combo box style from a stylesheet.
		/// </summary>
		/// <param name="stylesheet">The stylesheet to take the style from.</param>
		/// <param name="name">Name of the combo box style.</param>
		protected override void InternalSetStyle(Stylesheet stylesheet, string name)
		{
			ApplySearchableComboBoxStyle(stylesheet.ComboBoxStyles.SafelyGetStyle(name));
		}

		/// <summary>
		/// Replaces this combo box's settings, items and selection with another's. The item
		/// references themselves are shared, not cloned; <see cref="Strategy"/> is cloned, since it
		/// is mutable and the two widgets must be able to be retuned independently.
		/// </summary>
		/// <param name="w">The widget to copy from. Must be a <see cref="SearchableComboBox{T}"/> of the same item type.</param>
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
			Strategy = other.Strategy.Clone();

			_items.Clear();
			foreach (T item in other._items)
			{
				_items.Add(item);
			}

			SelectedIndex = other.SelectedIndex;
		}
	}
}
