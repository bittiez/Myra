using Myra.Graphics2D.UI;
using NUnit.Framework;

namespace Myra.Tests
{
	/// <summary>
	/// Desktop.Widgets rides the same ObservableCollectionWithReset as Widget.Children, but Desktop
	/// is not a Widget - it owns a second, independent copy of the attach/detach handlers. Covered
	/// separately so a fix applied to only one of the two shows up.
	/// </summary>
	[TestFixture]
	public class DesktopWidgetsTests
	{
		/// <summary>
		/// Regression: Clear() raises Reset without OldItems, and the widgets copy the handler fell
		/// back on is only refreshed by a layout pass - with none between the add and the clear it
		/// rebuilt to empty and detached nothing. A widget that subscribes to its Desktop on attach
		/// (SearchableComboBox and its context-menu handlers) then stayed subscribed for the
		/// Desktop's lifetime.
		/// </summary>
		[Test]
		public void ClearDetachesWidgetsAddedSinceLastLayoutPass()
		{
			var desktop = new Desktop();
			var widget = new Label();

			desktop.Widgets.Add(widget);
			desktop.Widgets.Clear();

			Assert.IsNull(widget.Desktop);
		}

		[Test]
		public void ClearDetachesTheWholeSubtree()
		{
			var desktop = new Desktop();
			var panel = new VerticalStackPanel();
			var leaf = new Label();

			panel.Widgets.Add(leaf);
			desktop.Widgets.Add(panel);

			Assert.AreEqual(desktop, leaf.Desktop, "precondition: Desktop propagates down on attach");

			desktop.Widgets.Clear();

			Assert.IsNull(panel.Desktop);
			Assert.IsNull(leaf.Desktop);
		}

		/// <summary>
		/// Regression: assigning through the indexer raises Replace, which the handler did not cover,
		/// so the outgoing widget kept a stale Desktop - and with it every subscription it took out
		/// on attach - while the replacement never got one.
		/// </summary>
		[Test]
		public void ReplaceSwapsAttachment()
		{
			var desktop = new Desktop();
			var first = new Label();
			var second = new Label();

			desktop.Widgets.Add(first);
			desktop.Widgets[0] = second;

			Assert.IsNull(first.Desktop);
			Assert.AreEqual(desktop, second.Desktop);
		}

		/// <summary>
		/// Replace fires even when both sides are the same widget, and the handler has to leave it
		/// alone: transiently nulling Desktop drops keyboard focus and closes an open context menu.
		/// Desktop alone would not catch a detach/reattach round trip, hence the focus assertion.
		/// </summary>
		[Test]
		public void ReplacingAWidgetWithItselfKeepsItAttachedAndFocused()
		{
			var desktop = new Desktop();
			var widget = new Label();

			desktop.Widgets.Add(widget);
			desktop.FocusedKeyboardWidget = widget;

			desktop.Widgets[0] = widget;

			Assert.AreEqual(desktop, widget.Desktop);
			Assert.AreEqual(widget, desktop.FocusedKeyboardWidget);
		}

		/// <summary>
		/// A Clear() after a Replace has to detach the replacement, not the widget it replaced -
		/// i.e. the Reset snapshot reflects Replace as well as Add/Remove.
		/// </summary>
		[Test]
		public void ClearAfterReplaceDetachesTheReplacement()
		{
			var desktop = new Desktop();
			var first = new Label();
			var second = new Label();

			desktop.Widgets.Add(first);
			desktop.Widgets[0] = second;
			desktop.Widgets.Clear();

			Assert.IsNull(second.Desktop);
		}

		/// <summary>
		/// Move only reorders; the widget stays attached, and a later Clear() still detaches it.
		/// </summary>
		[Test]
		public void MoveKeepsWidgetsAttached()
		{
			var desktop = new Desktop();
			var first = new Label();
			var second = new Label();

			desktop.Widgets.Add(first);
			desktop.Widgets.Add(second);
			desktop.Widgets.Move(0, 1);

			Assert.AreEqual(desktop, first.Desktop);
			Assert.AreEqual(desktop, second.Desktop);

			desktop.Widgets.Clear();

			Assert.IsNull(first.Desktop);
			Assert.IsNull(second.Desktop);
		}

		/// <summary>
		/// A widget removed before the clear must stay out of the Reset snapshot, or the clear would
		/// detach one that has since been adopted by another Desktop.
		/// </summary>
		[Test]
		public void ClearDoesNotRedetachAnAlreadyRemovedWidget()
		{
			var desktop = new Desktop();
			var other = new Desktop();
			var removed = new Label();
			var kept = new Label();

			desktop.Widgets.Add(removed);
			desktop.Widgets.Add(kept);
			desktop.Widgets.Remove(removed);
			other.Widgets.Add(removed);

			desktop.Widgets.Clear();

			Assert.IsNull(kept.Desktop);
			Assert.AreEqual(other, removed.Desktop);
		}
	}
}
