using System.Diagnostics;
using Myra.Utility.Search;
using NUnit.Framework;

namespace Myra.Tests
{
	[TestFixture]
	public class SearchStrategyTests
	{
		[Test]
		public void SearchMatchNoneIsNotAMatch()
		{
			Assert.IsFalse(SearchMatch.None.IsMatch);
			Assert.AreEqual(0d, SearchMatch.None.Score);
			Assert.IsNull(SearchMatch.None.Spans);
		}

		[Test]
		public void SearchMatchExactDefaultsToPerfectScore()
		{
			var match = SearchMatch.Exact();

			Assert.IsTrue(match.IsMatch);
			Assert.AreEqual(1d, match.Score);
		}

		[Test]
		public void SearchMatchScaledMultipliesScore()
		{
			var match = SearchMatch.Exact(0.5d).Scaled(0.5d);

			Assert.IsTrue(match.IsMatch);
			Assert.AreEqual(0.25d, match.Score);
		}

		[Test]
		public void SearchMatchScaledKeepsNonMatchNonMatch()
		{
			var match = SearchMatch.None.Scaled(10d);

			Assert.IsFalse(match.IsMatch);
			Assert.AreEqual(0d, match.Score);
		}

		[Test]
		public void SubstringEmptyQueryMatchesEverything()
		{
			var strategy = new SubstringSearchStrategy();

			Assert.IsTrue(strategy.Match("anything", string.Empty).IsMatch);
			Assert.AreEqual(1d, strategy.Match("anything", string.Empty).Score);
		}

		[Test]
		public void SubstringIsCaseInsensitiveByDefault()
		{
			var strategy = new SubstringSearchStrategy();

			Assert.IsFalse(strategy.CaseSensitive);
			Assert.IsTrue(strategy.Match("Hello World", "hello").IsMatch);
		}

		[Test]
		public void SubstringHonoursCaseSensitivity()
		{
			var strategy = new SubstringSearchStrategy
			{
				CaseSensitive = true
			};

			Assert.IsFalse(strategy.Match("Hello World", "hello").IsMatch);
			Assert.IsTrue(strategy.Match("Hello World", "Hello").IsMatch);
		}

		[Test]
		public void SubstringReportsSpanOfFirstOccurrence()
		{
			var strategy = new SubstringSearchStrategy();

			var match = strategy.Match("abcabc", "bc");

			Assert.IsTrue(match.IsMatch);
			Assert.IsNotNull(match.Spans);
			Assert.AreEqual(1, match.Spans.Length);
			Assert.AreEqual(1, match.Spans[0].Start);
			Assert.AreEqual(2, match.Spans[0].Len);
		}

		[Test]
		public void SubstringMissReturnsNone()
		{
			var strategy = new SubstringSearchStrategy();

			Assert.IsFalse(strategy.Match("abc", "zzz").IsMatch);
		}

		[Test]
		public void SubstringCloneIsIndependent()
		{
			var strategy = new SubstringSearchStrategy
			{
				CaseSensitive = true
			};

			var clone = (SubstringSearchStrategy)strategy.Clone();
			clone.CaseSensitive = false;

			Assert.IsTrue(strategy.CaseSensitive);
			Assert.IsFalse(clone.CaseSensitive);
		}

		[Test]
		public void TextQueryTreatsQueryAsLiteralByDefault()
		{
			var strategy = new TextQuerySearchStrategy();

			Assert.IsFalse(strategy.UseRegex);
			Assert.IsFalse(strategy.Match("abc", "a.c").IsMatch);
			Assert.IsTrue(strategy.Match("a.c", "a.c").IsMatch);
		}

		[Test]
		public void TextQueryMatchesRegexWhenEnabled()
		{
			var strategy = new TextQuerySearchStrategy
			{
				UseRegex = true
			};

			Assert.IsTrue(strategy.Match("abc", "a.c").IsMatch);
		}

		[Test]
		public void TextQueryHonoursCaseSensitivity()
		{
			var strategy = new TextQuerySearchStrategy();

			Assert.IsTrue(strategy.Match("ABC", "abc").IsMatch);

			strategy.CaseSensitive = true;

			Assert.IsFalse(strategy.Match("ABC", "abc").IsMatch);
		}

		[Test]
		public void TextQueryWholeWordRequiresWordBoundaries()
		{
			var strategy = new TextQuerySearchStrategy
			{
				WholeWord = true
			};

			Assert.IsFalse(strategy.Match("abcdef", "abc").IsMatch);
			Assert.IsTrue(strategy.Match("abc def", "abc").IsMatch);
		}

		/// <summary>
		/// The pattern is wrapped as <c>\b...\b</c>; without a non-capturing group around it the
		/// wrapping binds to the first and last branch of a top-level alternation only, so
		/// <c>cat|dog</c> becomes <c>\bcat|dog\b</c> - "dog" then matches mid-word and "cat" at the
		/// end of one, both against what whole-word means.
		/// </summary>
		[Test]
		public void TextQueryWholeWordAppliesToWholeRegexAlternation()
		{
			var strategy = new TextQuerySearchStrategy
			{
				UseRegex = true,
				WholeWord = true
			};

			Assert.IsTrue(strategy.Match("a cat here", "cat|dog").IsMatch);
			Assert.IsTrue(strategy.Match("a dog here", "cat|dog").IsMatch);
			Assert.IsFalse(strategy.Match("hotdogs", "cat|dog").IsMatch);
			Assert.IsFalse(strategy.Match("bobcat", "cat|dog").IsMatch);
		}

		[Test]
		public void TextQueryEmptyQueryMatchesEverythingEvenInRegexMode()
		{
			var strategy = new TextQuerySearchStrategy
			{
				UseRegex = true
			};

			Assert.IsTrue(strategy.Match("anything", string.Empty).IsMatch);
		}

		[Test]
		public void TextQueryReportsInvalidRegex()
		{
			var strategy = new TextQuerySearchStrategy
			{
				UseRegex = true
			};

			Assert.IsFalse(((ISearchStrategy)strategy).IsQueryValid("("));
			Assert.IsFalse(strategy.Match("(", "(").IsMatch);
		}

		[Test]
		public void TextQueryLiteralQueryIsAlwaysValid()
		{
			var strategy = new TextQuerySearchStrategy();

			Assert.IsTrue(((ISearchStrategy)strategy).IsQueryValid("("));
			Assert.IsTrue(strategy.Match("a ( b", "(").IsMatch);
		}

		/// <summary>
		/// The compiled regex is cached by query; changing a flag has to invalidate that cache or
		/// the strategy keeps matching with the previous settings.
		/// </summary>
		[Test]
		public void TextQueryRecompilesWhenOptionsChange()
		{
			var strategy = new TextQuerySearchStrategy();

			Assert.IsTrue(strategy.Match("ABC", "abc").IsMatch);

			strategy.CaseSensitive = true;
			Assert.IsFalse(strategy.Match("ABC", "abc").IsMatch);

			strategy.CaseSensitive = false;
			Assert.IsTrue(strategy.Match("ABC", "abc").IsMatch);

			strategy.UseRegex = true;
			Assert.IsTrue(strategy.Match("abc", "a.c").IsMatch);

			strategy.UseRegex = false;
			Assert.IsFalse(strategy.Match("abc", "a.c").IsMatch);
		}

		[Test]
		public void TextQueryReportsSpanOfFirstHit()
		{
			var strategy = new TextQuerySearchStrategy();

			var match = strategy.Match("xxabcxx", "abc");

			Assert.IsTrue(match.IsMatch);
			Assert.IsNotNull(match.Spans);
			Assert.AreEqual(1, match.Spans.Length);
			Assert.AreEqual(2, match.Spans[0].Start);
			Assert.AreEqual(3, match.Spans[0].Len);
		}

		[Test]
		public void TextQueryCloneIsIndependent()
		{
			var strategy = new TextQuerySearchStrategy
			{
				UseRegex = true,
				WholeWord = true,
				CaseSensitive = true
			};

			var clone = (TextQuerySearchStrategy)strategy.Clone();
			clone.UseRegex = false;
			clone.WholeWord = false;
			clone.CaseSensitive = false;

			Assert.IsTrue(strategy.UseRegex);
			Assert.IsTrue(strategy.WholeWord);
			Assert.IsTrue(strategy.CaseSensitive);

			// The clone carried the original's cache keys across; matching with the changed
			// settings has to recompile rather than reuse the copied regex.
			Assert.IsTrue(clone.Match("abc", "ABC").IsMatch);
		}

		/// <summary>
		/// The query comes straight from a text box the user types into, and matching runs on the
		/// UI thread for every item on every keystroke - a pathological pattern must fail the match
		/// rather than wedge the client.
		/// </summary>
		[Test]
		public void TextQueryCatastrophicBacktrackingDoesNotHang()
		{
			var strategy = new TextQuerySearchStrategy
			{
				UseRegex = true
			};

			var candidate = new string('a', 40) + "b";

			var sw = Stopwatch.StartNew();
			var match = strategy.Match(candidate, "(a+)+$");
			sw.Stop();

			Assert.IsFalse(match.IsMatch);
			Assert.Less(sw.ElapsedMilliseconds, 2000);
		}

		[Test]
		public void ScoringBestPicksHighestWeightedField()
		{
			var strategy = new SubstringSearchStrategy();

			var match = SearchScoring.Best(strategy, "a", ("a", 0.25d), ("xa", 0.75d));

			Assert.IsTrue(match.IsMatch);
			Assert.AreEqual(0.75d, match.Score);
		}

		[Test]
		public void ScoringBestSkipsNullFields()
		{
			var strategy = new SubstringSearchStrategy();

			var match = SearchScoring.Best(strategy, "a", (null, 1d), ("a", 0.5d));

			Assert.IsTrue(match.IsMatch);
			Assert.AreEqual(0.5d, match.Score);
		}

		[Test]
		public void ScoringBestReturnsNoneWhenNothingMatches()
		{
			var strategy = new SubstringSearchStrategy();

			Assert.IsFalse(SearchScoring.Best(strategy, "zzz", ("a", 1d), ("b", 1d)).IsMatch);
		}

		[Test]
		public void ScoringBestKeepsFirstOnTie()
		{
			var strategy = new SubstringSearchStrategy();

			var match = SearchScoring.Best(strategy, "a", ("first a", 1d), ("second a", 1d));

			Assert.IsTrue(match.IsMatch);
			// Both score 1; the span identifies which field won - "first a" has its hit at index 6.
			Assert.AreEqual(6, match.Spans[0].Start);
		}

		[Test]
		public void ScoringBestOfManyWeighsEveryTextEqually()
		{
			var strategy = new SubstringSearchStrategy();

			var match = SearchScoring.BestOfMany(strategy, "a", new[] { null, "b", "a" }, 0.5d);

			Assert.IsTrue(match.IsMatch);
			Assert.AreEqual(0.5d, match.Score);
		}

		[Test]
		public void ScoringBestOfManyReturnsNoneWhenNothingMatches()
		{
			var strategy = new SubstringSearchStrategy();

			Assert.IsFalse(SearchScoring.BestOfMany(strategy, "zzz", new[] { "a", "b" }, 1d).IsMatch);
		}
	}
}
