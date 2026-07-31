using Myra.Graphics2D.UI;
using NUnit.Framework;

namespace Myra.Tests
{
	[TestFixture]
	public class WidgetChildrenTests
	{
		[Test]
		public void AddAttachesChild()
		{
			var panel = new VerticalStackPanel();
			var child = new Label();

			panel.Widgets.Add(child);

			Assert.AreEqual(panel, child.Parent);
		}

		[Test]
		public void RemoveDetachesChild()
		{
			var panel = new VerticalStackPanel();
			var child = new Label();

			panel.Widgets.Add(child);
			panel.Widgets.Remove(child);

			Assert.IsNull(child.Parent);
		}

		/// <summary>
		/// Regression: Clear() raises Reset without OldItems, and the children copy the handler fell
		/// back on is only refreshed by a layout pass - with none between the add and the clear it
		/// rebuilt to empty and detached nothing. Detaching now runs off the ObservableCollection-
		/// WithReset snapshot instead.
		/// </summary>
		[Test]
		public void ClearDetachesChildrenAddedSinceLastLayoutPass()
		{
			var panel = new VerticalStackPanel();
			var child = new Label();

			panel.Widgets.Add(child);
			panel.Widgets.Clear();

			Assert.IsNull(child.Parent);
			Assert.IsNull(child.Desktop);
		}

		[Test]
		public void ClearDetachesChildrenAfterLayoutPass()
		{
			var panel = new VerticalStackPanel();
			var child = new Label();

			panel.Widgets.Add(child);
			panel.Measure(new Microsoft.Xna.Framework.Point(1000, 1000));
			panel.Widgets.Clear();

			Assert.IsNull(child.Parent);
		}

		/// <summary>
		/// Same regression, on the add/clear churn path a filtered dropdown runs per keystroke.
		/// </summary>
		[Test]
		public void RepeatedAddClearCyclesDetachEveryChild()
		{
			var panel = new VerticalStackPanel();

			for (var i = 0; i < 3; ++i)
			{
				var child = new Label();
				panel.Widgets.Add(child);
				panel.Widgets.Clear();

				Assert.IsNull(child.Parent, $"child of cycle {i} stayed attached");
			}

			Assert.AreEqual(0, panel.Widgets.Count);
		}

		/// <summary>
		/// Clear() has to reach the whole subtree, not just direct children: a widget that subscribes
		/// to its Desktop on attach (SearchableComboBox and its context-menu handlers) would
		/// otherwise stay subscribed for the Desktop's lifetime.
		/// </summary>
		[Test]
		public void ClearDetachesTheDesktopOfTheWholeSubtree()
		{
			var desktop = new Desktop();
			var panel = new VerticalStackPanel();
			var row = new HorizontalStackPanel();
			var leaf = new Label();

			row.Widgets.Add(leaf);
			panel.Widgets.Add(row);
			desktop.Widgets.Add(panel);

			Assert.AreEqual(desktop, leaf.Desktop, "precondition: Desktop propagates down on attach");

			panel.Widgets.Clear();

			Assert.IsNull(row.Desktop);
			Assert.IsNull(leaf.Desktop);
			Assert.IsNull(row.Parent);
		}

		/// <summary>
		/// Regression: assigning through the indexer raises Replace, which the handler did not cover,
		/// so the outgoing widget kept a stale Parent and the replacement never learned its own.
		/// ListView.SetChildByIndex is the only in-tree caller.
		/// </summary>
		[Test]
		public void ReplaceSwapsAttachment()
		{
			var panel = new VerticalStackPanel();
			var first = new Label();
			var second = new Label();

			panel.Widgets.Add(first);
			panel.Widgets[0] = second;

			Assert.IsNull(first.Parent);
			Assert.AreEqual(panel, second.Parent);
		}

		/// <summary>
		/// Replace fires even when both sides are the same widget, and the handler has to leave it
		/// alone: a detach/reattach round trip transiently nulls Desktop, closing an open context
		/// menu and dropping keyboard focus. Parent alone would not catch that, hence the count.
		/// </summary>
		[Test]
		public void ReplacingAWidgetWithItselfKeepsItAttached()
		{
			var panel = new DetachCountingStackPanel();
			var child = new Label();

			panel.Widgets.Add(child);
			panel.Widgets[0] = child;

			Assert.AreEqual(panel, child.Parent);
			Assert.AreEqual(0, panel.Detachments);
		}

		private class DetachCountingStackPanel : VerticalStackPanel
		{
			public int Detachments { get; private set; }

			protected override void OnChildRemoved(Widget w)
			{
				++Detachments;
				base.OnChildRemoved(w);
			}
		}

		/// <summary>
		/// A Clear() after a Replace has to detach the replacement, not the widget it replaced -
		/// i.e. the Reset snapshot reflects Replace as well as Add/Remove.
		/// </summary>
		[Test]
		public void ClearAfterReplaceDetachesTheReplacement()
		{
			var panel = new VerticalStackPanel();
			var first = new Label();
			var second = new Label();

			panel.Widgets.Add(first);
			panel.Widgets[0] = second;
			panel.Widgets.Clear();

			Assert.IsNull(second.Parent);
		}

		/// <summary>
		/// Move only reorders; the widget must stay attached, and a later Clear() must still
		/// detach it exactly once.
		/// </summary>
		[Test]
		public void MoveKeepsChildrenAttached()
		{
			var panel = new VerticalStackPanel();
			var first = new Label();
			var second = new Label();

			panel.Widgets.Add(first);
			panel.Widgets.Add(second);
			panel.Widgets.Move(0, 1);

			Assert.AreEqual(panel, first.Parent);
			Assert.AreEqual(panel, second.Parent);

			panel.Widgets.Clear();

			Assert.IsNull(first.Parent);
			Assert.IsNull(second.Parent);
		}

		/// <summary>
		/// A child removed before the clear must stay out of the Reset snapshot, or the clear would
		/// detach a widget that has since been adopted elsewhere.
		/// </summary>
		[Test]
		public void ClearDoesNotRedetachAnAlreadyRemovedChild()
		{
			var panel = new VerticalStackPanel();
			var removed = new Label();
			var kept = new Label();

			panel.Widgets.Add(removed);
			panel.Widgets.Add(kept);
			panel.Widgets.Remove(removed);

			var reattachedTo = new VerticalStackPanel();
			reattachedTo.Widgets.Add(removed);

			panel.Widgets.Clear();

			// The clear owns `kept` only; `removed` has since been adopted elsewhere and must
			// keep its new parent.
			Assert.IsNull(kept.Parent);
			Assert.AreEqual(reattachedTo, removed.Parent);
		}
	}
}
