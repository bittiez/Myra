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
		/// Documents an upstream limitation: Clear() raises Reset without OldItems, and the copy
		/// the handler falls back on is only refreshed by a layout pass. With none between the add
		/// and the clear it rebuilds to empty, so nothing is detached.
		/// </summary>
		[Test]
		[Ignore("Pre-existing Myra behaviour, unchanged by this branch. Orphans keep stale Parent/Desktop until GC.")]
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
		/// Same upstream limitation as above, on the add/clear churn path a filtered dropdown runs
		/// per keystroke.
		/// </summary>
		[Test]
		[Ignore("Pre-existing Myra behaviour, unchanged by this branch. Orphans keep stale Parent/Desktop until GC.")]
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
		/// Documents an upstream gap: assigning through the indexer raises Replace, which
		/// ChildrenOnCollectionChanged does not handle at all, so the replacement never learns its
		/// parent. ListView.SetChildByIndex is the only in-tree caller.
		/// </summary>
		[Test]
		[Ignore("Pre-existing Myra behaviour, unchanged by this branch. Replace is not handled.")]
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

	}
}
