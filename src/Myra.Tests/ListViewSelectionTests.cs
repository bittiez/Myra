using Myra.Graphics2D.UI;
using NUnit.Framework;

namespace Myra.Tests
{
	[TestFixture]
	public class ListViewSelectionTests
	{
		private static ListView CreateListView(params string[] texts)
		{
			var listView = new ListView();
			foreach (var text in texts)
			{
				listView.Widgets.Add(new Label
				{
					Text = text
				});
			}

			return listView;
		}

		[Test]
		public void SelectedIndexRoundTrips()
		{
			var listView = CreateListView("a", "b", "c");

			listView.SelectedIndex = 1;

			Assert.AreEqual(1, listView.SelectedIndex);
			Assert.AreEqual(listView.Widgets[1], listView.SelectedItem);
		}

		[Test]
		public void RemovingSelectedItemClearsSelection()
		{
			var listView = CreateListView("a", "b");

			listView.SelectedIndex = 1;
			listView.Widgets.RemoveAt(1);

			Assert.IsNull(listView.SelectedItem);
			Assert.IsNull(listView.SelectedIndex);
		}

		[Test]
		public void RemovingUnselectedItemKeepsSelection()
		{
			var listView = CreateListView("a", "b");
			var selected = listView.Widgets[1];

			listView.SelectedItem = selected;
			listView.Widgets.RemoveAt(0);

			Assert.AreEqual(selected, listView.SelectedItem);
		}

		/// <summary>
		/// A bulk clear drops the selected row like RemoveAt does, so the selection must not be
		/// left pointing at a widget that is no longer in the list.
		/// </summary>
		[Test]
		public void ClearClearsSelection()
		{
			var listView = CreateListView("a", "b");

			listView.SelectedIndex = 0;
			listView.Widgets.Clear();

			Assert.IsNull(listView.SelectedItem);
			Assert.IsNull(listView.SelectedIndex);
		}

		[Test]
		public void ClearWithNoSelectionRaisesNothing()
		{
			var listView = CreateListView("a", "b");

			var raised = 0;
			listView.SelectedIndexChanged += (s, e) => ++raised;

			listView.Widgets.Clear();

			Assert.AreEqual(0, raised);
		}

		/// <summary>
		/// Clearing a combo's items is the ordinary way of repopulating it. The clear nulls the
		/// list's selection, which the combo picks up to refresh its button - and must survive
		/// having no selected item to render.
		/// </summary>
		[Test]
		public void ComboViewSurvivesClearingItemsWhileSelected()
		{
			var comboView = new ComboView();
			comboView.Widgets.Add(new Label
			{
				Text = "a"
			});
			comboView.Widgets.Add(new Label
			{
				Text = "b"
			});

			comboView.SelectedItem = comboView.Widgets[0];

			Assert.DoesNotThrow(() => comboView.Widgets.Clear());
			Assert.IsNull(comboView.SelectedItem);
		}

		[Test]
		public void ComboViewSurvivesRemovingSelectedItem()
		{
			var comboView = new ComboView();
			comboView.Widgets.Add(new Label
			{
				Text = "a"
			});

			comboView.SelectedItem = comboView.Widgets[0];

			Assert.DoesNotThrow(() => comboView.Widgets.RemoveAt(0));
			Assert.IsNull(comboView.SelectedItem);
		}
	}
}
