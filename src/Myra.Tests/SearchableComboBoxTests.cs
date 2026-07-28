using System;
using System.Collections.Generic;
using System.Linq;
using Myra.Graphics2D.UI;
using Myra.Utility.Search;
using NUnit.Framework;

namespace Myra.Tests
{
	[TestFixture]
	public class SearchableComboBoxTests
	{
		/// <summary>
		/// Scores candidates from a lookup, so ordering and <c>MinScore</c> can be driven exactly
		/// rather than inferred from a real strategy's heuristics.
		/// </summary>
		private class TableSearchStrategy : ISearchStrategy
		{
			private readonly Dictionary<string, double> _scores;

			public TableSearchStrategy(Dictionary<string, double> scores)
			{
				_scores = scores;
			}

			public SearchMatch Match(string candidate, string query)
			{
				if (!_scores.TryGetValue(candidate, out var score))
				{
					return SearchMatch.None;
				}

				return SearchMatch.Exact(score);
			}

			public ISearchStrategy Clone() => new TableSearchStrategy(new Dictionary<string, double>(_scores));
		}

		private class ProbedTextSearchComboBox : TextSearchComboBox<string>
		{
			public Widget Header { get; private set; }

			public IReadOnlyList<ToggleButton> Toggles =>
				((HorizontalStackPanel)Header).Widgets.OfType<ToggleButton>().ToArray();

			protected override Widget BuildSearchHeader()
			{
				Header = base.BuildSearchHeader();
				return Header;
			}
		}

		private static Desktop ShowOnDesktop(Widget widget)
		{
			var desktop = new Desktop
			{
				Root = widget
			};

			// Lay the tree out once, so bounds the popup is positioned against are real.
			desktop.UpdateLayout();

			return desktop;
		}

		private static SearchableComboBox<string> CreateOpenedComboBox(params string[] items)
		{
			var combo = new SearchableComboBox<string>();
			foreach (var item in items)
			{
				combo.Items.Add(item);
			}

			ShowOnDesktop(combo);
			combo.Open();

			return combo;
		}

		[Test]
		public void OpenListsEveryItemWhenQueryIsEmpty()
		{
			var combo = CreateOpenedComboBox("alpha", "beta", "gamma");

			Assert.IsTrue(combo.IsExpanded);
			CollectionAssert.AreEqual(new[] { "alpha", "beta", "gamma" }, combo.VisibleItems);
		}

		[Test]
		public void SearchTextFiltersTheVisibleItems()
		{
			var combo = CreateOpenedComboBox("alpha", "beta", "gamma");

			combo.SearchText = "a";

			CollectionAssert.AreEqual(new[] { "alpha", "beta", "gamma" }, combo.VisibleItems);

			combo.SearchText = "et";

			CollectionAssert.AreEqual(new[] { "beta" }, combo.VisibleItems);
		}

		[Test]
		public void DefaultSearchIsCaseInsensitiveSubstring()
		{
			var combo = CreateOpenedComboBox("Alpha", "beta");

			combo.SearchText = "ALPH";

			CollectionAssert.AreEqual(new[] { "Alpha" }, combo.VisibleItems);
		}

		[Test]
		public void NoMatchesLeavesNothingVisible()
		{
			var combo = CreateOpenedComboBox("alpha", "beta");

			combo.SearchText = "zzz";

			Assert.AreEqual(0, combo.VisibleItems.Count);
		}

		[Test]
		public void SearchTextChangedIsRaisedWithOldAndNewValue()
		{
			var combo = CreateOpenedComboBox("alpha");

			string oldValue = null;
			string newValue = null;
			var raised = 0;

			combo.SearchTextChanged += (s, e) =>
			{
				oldValue = e.OldValue;
				newValue = e.NewValue;
				++raised;
			};

			combo.SearchText = "a";
			combo.SearchText = "a";

			Assert.AreEqual(1, raised);
			Assert.AreEqual(string.Empty, oldValue);
			Assert.AreEqual("a", newValue);
		}

		[Test]
		public void NullSearchTextIsTreatedAsEmpty()
		{
			var combo = CreateOpenedComboBox("alpha", "beta");

			combo.SearchText = "a";
			combo.SearchText = null;

			Assert.AreEqual(string.Empty, combo.SearchText);
			Assert.AreEqual(2, combo.VisibleItems.Count);
		}

		[Test]
		public void MaxVisibleResultsCapsTheList()
		{
			var combo = CreateOpenedComboBox("a1", "a2", "a3", "a4");
			combo.MaxVisibleResults = 2;

			combo.SearchText = "a";

			CollectionAssert.AreEqual(new[] { "a1", "a2" }, combo.VisibleItems);
		}

		[Test]
		public void ZeroMaxVisibleResultsMeansNoCap()
		{
			var combo = CreateOpenedComboBox("a1", "a2", "a3");
			combo.MaxVisibleResults = 0;

			combo.SearchText = "a";

			Assert.AreEqual(3, combo.VisibleItems.Count);
		}

		[Test]
		public void ItemsAddedWhileOpenShowUpImmediately()
		{
			var combo = CreateOpenedComboBox("alpha");

			combo.Items.Add("beta");

			CollectionAssert.AreEqual(new[] { "alpha", "beta" }, combo.VisibleItems);
		}

		[Test]
		public void HigherScoringItemsComeFirstAndTiesKeepInsertionOrder()
		{
			var combo = new SearchableComboBox<string>
			{
				Strategy = new TableSearchStrategy(new Dictionary<string, double>
				{
					["low"] = 0.1d,
					["tieA"] = 0.5d,
					["tieB"] = 0.5d,
					["high"] = 0.9d
				})
			};

			foreach (var item in new[] { "low", "tieA", "tieB", "high", "unmatched" })
			{
				combo.Items.Add(item);
			}

			ShowOnDesktop(combo);
			combo.Open();

			CollectionAssert.AreEqual(new[] { "high", "tieA", "tieB", "low" }, combo.VisibleItems);
		}

		[Test]
		public void ReplacingTheStrategyRefiltersImmediately()
		{
			var combo = CreateOpenedComboBox("alpha", "beta");

			combo.SearchText = "a.p";
			Assert.AreEqual(0, combo.VisibleItems.Count);

			combo.Strategy = new TextQuerySearchStrategy
			{
				UseRegex = true
			};

			CollectionAssert.AreEqual(new[] { "alpha" }, combo.VisibleItems);
		}

		[Test]
		public void TextSelectorDrivesWhatIsSearched()
		{
			var combo = new SearchableComboBox<int>
			{
				TextSelector = i => "item" + i
			};

			combo.Items.Add(1);
			combo.Items.Add(2);

			ShowOnDesktop(combo);
			combo.Open();

			combo.SearchText = "item2";

			CollectionAssert.AreEqual(new[] { 2 }, combo.VisibleItems);
		}

		[Test]
		public void SelectedIndexRoundTrips()
		{
			var combo = CreateOpenedComboBox("alpha", "beta");

			combo.SelectedIndex = 1;

			Assert.AreEqual(1, combo.SelectedIndex);
			Assert.AreEqual("beta", combo.SelectedItem);
			Assert.IsTrue(combo.HasSelection);
		}

		[Test]
		public void SelectedIndexOutOfRangeClearsSelection()
		{
			var combo = CreateOpenedComboBox("alpha", "beta");

			combo.SelectedIndex = 1;
			combo.SelectedIndex = 99;

			Assert.IsNull(combo.SelectedIndex);
			Assert.IsFalse(combo.HasSelection);
		}

		[Test]
		public void SelectedIndexNullClearsSelection()
		{
			var combo = CreateOpenedComboBox("alpha", "beta");

			combo.SelectedIndex = 0;
			combo.SelectedIndex = null;

			Assert.IsNull(combo.SelectedIndex);
			Assert.IsFalse(combo.HasSelection);
		}

		[Test]
		public void SelectedItemChangedCarriesOldAndNewItem()
		{
			var combo = CreateOpenedComboBox("alpha", "beta");

			string oldItem = null;
			string newItem = null;
			combo.SelectedItemChanged += (s, e) =>
			{
				oldItem = e.OldValue;
				newItem = e.NewValue;
			};

			combo.SelectedIndex = 0;
			Assert.IsNull(oldItem);
			Assert.AreEqual("alpha", newItem);

			combo.SelectedIndex = 1;
			Assert.AreEqual("alpha", oldItem);
			Assert.AreEqual("beta", newItem);
		}

		/// <summary>
		/// A value-type item can never be null, so "nothing is selected" cannot be inferred from
		/// the selected item alone - doing so reports whatever position default(T) occupies in the
		/// list (index 0 here) as the selection of a combo box nothing was ever picked in.
		/// </summary>
		[Test]
		public void ValueTypeItemsStartWithNoSelection()
		{
			var combo = new SearchableComboBox<int>();
			combo.Items.Add(0);
			combo.Items.Add(1);

			Assert.IsFalse(combo.HasSelection);
			Assert.IsNull(combo.SelectedIndex);
		}

		[Test]
		public void ValueTypeSelectionCanBeSetAndCleared()
		{
			var combo = new SearchableComboBox<int>();
			combo.Items.Add(0);
			combo.Items.Add(1);

			combo.SelectedIndex = 1;
			Assert.IsTrue(combo.HasSelection);
			Assert.AreEqual(1, combo.SelectedIndex);
			Assert.AreEqual(1, combo.SelectedItem);

			combo.SelectedIndex = null;
			Assert.IsFalse(combo.HasSelection);
			Assert.IsNull(combo.SelectedIndex);
		}

		[Test]
		public void SelectingDefaultValuedItemIsStillASelection()
		{
			var combo = new SearchableComboBox<int>();
			combo.Items.Add(7);
			combo.Items.Add(0);

			combo.SelectedIndex = 1;

			Assert.IsTrue(combo.HasSelection);
			Assert.AreEqual(1, combo.SelectedIndex);
			Assert.AreEqual(0, combo.SelectedItem);
		}

		[Test]
		public void ClosingClearsTheSearchByDefault()
		{
			var combo = CreateOpenedComboBox("alpha", "beta");

			Assert.IsTrue(combo.ClearSearchOnClose);

			combo.SearchText = "al";
			combo.Close();

			Assert.AreEqual(string.Empty, combo.SearchText);
		}

		[Test]
		public void ClosingKeepsTheSearchWhenAsked()
		{
			var combo = CreateOpenedComboBox("alpha", "beta");
			combo.ClearSearchOnClose = false;

			combo.SearchText = "al";
			combo.Close();

			Assert.AreEqual("al", combo.SearchText);
		}

		[Test]
		public void CloseWithoutADesktopIsANoOp()
		{
			var combo = new SearchableComboBox<string>();

			Assert.DoesNotThrow(combo.Close);
		}

		[Test]
		public void OpenWithoutADesktopIsANoOp()
		{
			var combo = new SearchableComboBox<string>();
			combo.Items.Add("alpha");

			Assert.DoesNotThrow(combo.Open);
			Assert.IsFalse(combo.IsExpanded);
		}

		[Test]
		public void LeavingTheDesktopClosesTheDropdown()
		{
			var combo = CreateOpenedComboBox("alpha");
			var desktop = combo.Desktop;

			desktop.Root = null;

			Assert.IsNull(desktop.ContextMenu);
		}

		[Test]
		public void GoingInvisibleClosesTheDropdown()
		{
			var combo = CreateOpenedComboBox("alpha");
			var desktop = combo.Desktop;

			combo.Visible = false;

			Assert.IsNull(desktop.ContextMenu);
		}

		[Test]
		public void CopyFromCopiesItemsSettingsAndSelection()
		{
			var source = new SearchableComboBox<string>
			{
				MaxVisibleResults = 5,
				SearchVisibleThreshold = 3,
				ClearSearchOnClose = false,
				FocusSearchOnClick = false
			};

			source.Items.Add("alpha");
			source.Items.Add("beta");
			source.SelectedIndex = 1;

			var target = new SearchableComboBox<string>();
			target.CopyFrom(source);

			CollectionAssert.AreEqual(new[] { "alpha", "beta" }, target.Items);
			Assert.AreEqual(5, target.MaxVisibleResults);
			Assert.AreEqual(3, target.SearchVisibleThreshold);
			Assert.IsFalse(target.ClearSearchOnClose);
			Assert.IsFalse(target.FocusSearchOnClick);
			Assert.AreEqual(1, target.SelectedIndex);
		}

		/// <summary>
		/// Strategies are mutable, so a copied widget must get its own - otherwise retuning one
		/// combo box silently retunes the one it was copied from.
		/// </summary>
		[Test]
		public void CopyFromClonesTheStrategyRatherThanSharingIt()
		{
			var source = new SearchableComboBox<string>
			{
				Strategy = new SubstringSearchStrategy
				{
					CaseSensitive = true
				}
			};

			var target = new SearchableComboBox<string>();
			target.CopyFrom(source);

			Assert.AreNotSame(source.Strategy, target.Strategy);

			((SubstringSearchStrategy)target.Strategy).CaseSensitive = false;

			Assert.IsTrue(((SubstringSearchStrategy)source.Strategy).CaseSensitive);
		}

		[Test]
		public void ScoredComboBoxRejectsANullStrategy()
		{
			Assert.Throws<ArgumentNullException>(() => new ScoredSearchComboBox<string>(null));
		}

		[Test]
		public void ScoredComboBoxDropsMatchesBelowMinScore()
		{
			var strategy = new TableSearchStrategy(new Dictionary<string, double>
			{
				["weak"] = 0.2d,
				["strong"] = 0.8d
			});

			var combo = new ScoredSearchComboBox<string>(strategy)
			{
				MinScore = 0.5d
			};

			combo.Items.Add("weak");
			combo.Items.Add("strong");

			ShowOnDesktop(combo);
			combo.Open();

			CollectionAssert.AreEqual(new[] { "strong" }, combo.VisibleItems);
		}

		[Test]
		public void ScoredComboBoxKeepsEverythingAtTheDefaultMinScore()
		{
			var strategy = new TableSearchStrategy(new Dictionary<string, double>
			{
				["weak"] = 0.2d,
				["strong"] = 0.8d
			});

			var combo = new ScoredSearchComboBox<string>(strategy);

			combo.Items.Add("weak");
			combo.Items.Add("strong");

			ShowOnDesktop(combo);
			combo.Open();

			CollectionAssert.AreEqual(new[] { "strong", "weak" }, combo.VisibleItems);
		}

		[Test]
		public void TextComboBoxDefaultsToATextQueryStrategy()
		{
			var combo = new TextSearchComboBox();

			Assert.IsInstanceOf<TextQuerySearchStrategy>(combo.Strategy);
		}

		[Test]
		public void TextComboBoxHeaderCarriesThreeToggles()
		{
			var combo = new ProbedTextSearchComboBox();
			combo.Items.Add("alpha");

			ShowOnDesktop(combo);
			combo.Open();

			Assert.AreEqual(3, combo.Toggles.Count);
			CollectionAssert.AreEqual(new[] { false, false, false }, combo.Toggles.Select(t => t.IsToggled));
		}

		[Test]
		public void TextComboBoxTogglesDriveTheStrategy()
		{
			var combo = new ProbedTextSearchComboBox();
			combo.Items.Add("alpha");
			combo.Items.Add("Alpha bravo");

			ShowOnDesktop(combo);
			combo.Open();

			var strategy = (TextQuerySearchStrategy)combo.Strategy;

			combo.Toggles[0].IsToggled = true;
			Assert.IsTrue(strategy.CaseSensitive);

			combo.Toggles[1].IsToggled = true;
			Assert.IsTrue(strategy.WholeWord);

			combo.Toggles[2].IsToggled = true;
			Assert.IsTrue(strategy.UseRegex);
		}

		[Test]
		public void TextComboBoxTogglesRefilter()
		{
			var combo = new ProbedTextSearchComboBox();
			combo.Items.Add("alpha");
			combo.Items.Add("ALPHA");

			ShowOnDesktop(combo);
			combo.Open();

			combo.SearchText = "alpha";
			Assert.AreEqual(2, combo.VisibleItems.Count);

			combo.Toggles[0].IsToggled = true;

			CollectionAssert.AreEqual(new[] { "alpha" }, combo.VisibleItems);
		}

		/// <summary>
		/// The toggles are wired up once, when the header is built. Replacing the strategy after
		/// that has to rebind them, or they keep writing to a strategy nothing searches with - the
		/// buttons then visibly toggle while the results never change.
		/// </summary>
		[Test]
		public void TextComboBoxTogglesFollowAReplacedStrategy()
		{
			var combo = new ProbedTextSearchComboBox();
			combo.Items.Add("alpha");

			ShowOnDesktop(combo);
			combo.Open();

			var replacement = new TextQuerySearchStrategy();
			combo.Strategy = replacement;

			combo.Toggles[2].IsToggled = true;

			Assert.IsTrue(replacement.UseRegex);
		}

		[Test]
		public void TextComboBoxTogglesShowAReplacedStrategysFlags()
		{
			var combo = new ProbedTextSearchComboBox();
			combo.Items.Add("alpha");

			ShowOnDesktop(combo);
			combo.Open();

			combo.Strategy = new TextQuerySearchStrategy
			{
				CaseSensitive = true,
				UseRegex = true
			};

			CollectionAssert.AreEqual(new[] { true, false, true }, combo.Toggles.Select(t => t.IsToggled));
		}

		/// <summary>
		/// CopyFrom clones the strategy, so the copy's toggles must end up bound to the clone
		/// rather than to the strategy of the widget that was copied from.
		/// </summary>
		[Test]
		public void TextComboBoxTogglesRebindAfterCopyFrom()
		{
			var source = new ProbedTextSearchComboBox();
			source.Items.Add("alpha");

			var target = new ProbedTextSearchComboBox();
			ShowOnDesktop(target);
			target.Open();

			target.CopyFrom(source);

			var sourceStrategy = (TextQuerySearchStrategy)source.Strategy;
			var targetStrategy = (TextQuerySearchStrategy)target.Strategy;
			Assert.AreNotSame(sourceStrategy, targetStrategy);

			target.Toggles[2].IsToggled = true;

			Assert.IsTrue(targetStrategy.UseRegex);
			Assert.IsFalse(sourceStrategy.UseRegex);
		}

		[Test]
		public void InvalidRegexLeavesTheListEmptyRatherThanThrowing()
		{
			var combo = new TextSearchComboBox();
			combo.Items.Add("alpha");
			((TextQuerySearchStrategy)combo.Strategy).UseRegex = true;

			ShowOnDesktop(combo);
			combo.Open();

			Assert.DoesNotThrow(() => combo.SearchText = "(");
			Assert.AreEqual(0, combo.VisibleItems.Count);

			combo.SearchText = "alp";
			CollectionAssert.AreEqual(new[] { "alpha" }, combo.VisibleItems);
		}
	}
}
