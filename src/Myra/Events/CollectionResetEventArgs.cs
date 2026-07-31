using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Myra.Events;

/// <summary>
/// Carries the items a collection held immediately before it was cleared.
/// </summary>
/// <typeparam name="T">Element type of the collection that was cleared.</typeparam>
/// <param name="items">Snapshot taken before the clear. Null is treated as empty.</param>
public class CollectionResetEventArgs<T>(IList<T> items) : EventArgs
{
	/// <summary>
	/// The cleared items, in the order they were held. Never null, and never empty when raised by
	/// <see cref="Myra.Utility.ObservableCollectionWithReset{T}"/>.
	/// </summary>
	/// <remarks>
	/// Read-only because the event is multicast: a writable list would let the first handler edit
	/// what the rest see. Same reason NotifyCollectionChangedEventArgs wraps its own OldItems.
	/// </remarks>
	public IReadOnlyList<T> OldItems { get; } = items is null ? ReadOnlyCollection<T>.Empty : new ReadOnlyCollection<T>(items);
}
