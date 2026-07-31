using System.Collections.Generic;
using System.Collections.Specialized;
using Myra.Utility;
using NUnit.Framework;

namespace Myra.Tests
{
	[TestFixture]
	public class ObservableCollectionWithResetTests
	{
		[Test]
		public void ClearReportsWhatItRemoved()
		{
			var collection = new ObservableCollectionWithReset<string> { "a", "b" };
			IReadOnlyList<string> removed = null;

			collection.CollectionReset += (s, e) => removed = e.OldItems;
			collection.Clear();

			CollectionAssert.AreEqual(new[] { "a", "b" }, removed);
		}

		/// <summary>
		/// A clear that removed nothing is not reported. Handlers are then guaranteed a non-empty
		/// snapshot, and a handler that clears again has something to stop it - see
		/// <see cref="AHandlerThatClearsAgainTerminates"/>.
		/// </summary>
		[Test]
		public void AClearThatRemovedNothingIsNotReported()
		{
			var collection = new ObservableCollectionWithReset<string>();
			var resets = 0;

			collection.CollectionReset += (s, e) => ++resets;
			collection.Clear();

			Assert.AreEqual(0, resets);
		}

		[Test]
		public void CollectionResetFiresOnlyForClear()
		{
			var collection = new ObservableCollectionWithReset<string> { "a" };
			var resets = 0;

			collection.CollectionReset += (s, e) => ++resets;

			collection.Add("b");
			collection.Remove("b");
			collection.Add("c");
			collection[0] = "d";
			collection.Move(0, 1);

			Assert.AreEqual(0, resets);
		}

		/// <summary>
		/// The snapshot goes to every handler of a multicast event, so it must not be editable -
		/// otherwise the first handler decides what the rest see.
		/// </summary>
		[Test]
		public void TheReportedSnapshotIsReadOnly()
		{
			var collection = new ObservableCollectionWithReset<string> { "a" };
			var writable = true;

			collection.CollectionReset += (s, e) => writable = !((ICollection<string>)e.OldItems).IsReadOnly;
			collection.Clear();

			Assert.IsFalse(writable);
		}

		/// <summary>
		/// CollectionReset is raised outside BlockReentrancy on purpose: detaching is itself a
		/// reentrant mutation - nulling a widget's Desktop closes an open tooltip, which removes the
		/// tooltip widget from the collection being cleared. ObservableCollection's guard throws on
		/// exactly that, but only past one CollectionChanged handler - hence the two no-op
		/// subscriptions, without which the test would pass either way.
		/// </summary>
		[Test]
		public void AHandlerMayMutateTheCollectionWhileItIsBeingReported()
		{
			var collection = new ObservableCollectionWithReset<string> { "a" };

			collection.CollectionChanged += (s, e) => { };
			collection.CollectionChanged += (s, e) => { };
			collection.CollectionReset += (s, e) => collection.Add("added by the reset handler");

			collection.Clear();

			CollectionAssert.AreEqual(new[] { "added by the reset handler" }, collection);
		}

		/// <summary>
		/// Regression: raising unconditionally made a handler that clears again recurse until the
		/// stack blew. Staying silent on an empty clear breaks the cycle.
		/// </summary>
		[Test]
		public void AHandlerThatClearsAgainTerminates()
		{
			var collection = new ObservableCollectionWithReset<string> { "a" };
			var resets = 0;

			collection.CollectionReset += (s, e) =>
			{
				++resets;
				collection.Clear();
			};

			collection.Clear();

			Assert.AreEqual(1, resets);
		}

		/// <summary>
		/// The INotifyCollectionChanged contract is untouched: a clear still reports as a Reset
		/// carrying no items, so subscribers that know nothing about CollectionReset behave exactly
		/// as they would against a stock ObservableCollection.
		/// </summary>
		[Test]
		public void ClearStillRaisesAPlainResetOnCollectionChanged()
		{
			var collection = new ObservableCollectionWithReset<string> { "a" };
			NotifyCollectionChangedEventArgs seen = null;

			collection.CollectionChanged += (s, e) => seen = e;
			collection.Clear();

			Assert.IsNotNull(seen);
			Assert.AreEqual(NotifyCollectionChangedAction.Reset, seen.Action);
			Assert.IsNull(seen.OldItems);
		}
	}
}
