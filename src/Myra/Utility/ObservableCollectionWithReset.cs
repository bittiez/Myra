using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Myra.Events;

namespace Myra.Utility;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that reports what a clear removed.
/// </summary>
/// <remarks>
/// A stock clear notifies as a Reset, which carries no items and arrives once the backing store is
/// already empty - so a handler cannot recover what left, and per-element cleanup (detaching a
/// widget, unsubscribing its events) silently does nothing. The items cannot be bolted onto that
/// event either: NotifyCollectionChangedEventArgs rejects a Reset carrying items. Hence the separate
/// <see cref="CollectionReset"/>, which a handler pairs with an early-out on Reset in its own
/// CollectionChanged handler.
/// </remarks>
/// <typeparam name="T">Element type.</typeparam>
public class ObservableCollectionWithReset<T> : ObservableCollection<T>
{
	/// <summary>
	/// Raised after a clear that removed something, with the items it removed. Follows the
	/// CollectionChanged Reset. Never for Add, Remove, Replace, or a clear of an empty collection.
	/// </summary>
	public event EventHandler<CollectionResetEventArgs<T>> CollectionReset;

	/// <summary>
	/// Clears the collection, then reports what it removed via <see cref="CollectionReset"/>.
	/// </summary>
	protected override void ClearItems()
	{
		CheckReentrancy();

		// Items is the live backing store base.ClearItems is about to empty, so snapshot first.
		var removed = Count > 0 ? new List<T>(Items) : null;

		base.ClearItems();

		// Silence on an empty clear also stops a handler that clears again from recursing: its own
		// clear finds the collection already empty and reports nothing back.
		if (removed == null)
			return;

		// Not raised under BlockReentrancy, unlike the base class's notifications. Detaching is
		// exactly the reentrant mutation that guard forbids - nulling a widget's Desktop closes an
		// open tooltip, which removes the tooltip widget from this very collection - and there is
		// nothing to protect: handlers iterate the snapshot, not live state.
		CollectionReset?.Invoke(this, new CollectionResetEventArgs<T>(removed));
	}
}
