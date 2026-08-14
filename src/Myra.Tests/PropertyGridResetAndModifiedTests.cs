using System.Linq;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Properties;
using NUnit.Framework;

namespace Myra.Tests
{
	/// <summary>
	/// Covers the reset-button / "modified" affordances added to <see cref="PropertyGrid"/>:
	/// <see cref="PropertyGrid.DefaultValueProvider"/>, <see cref="PropertyGrid.ResetOnlyWhenModified"/>,
	/// <see cref="PropertyGrid.ModifiedNameColor"/> and the localized display/description attributes.
	/// None of this existed before, so there is no prior coverage to extend.
	/// </summary>
	[TestFixture]
	public class PropertyGridResetAndModifiedTests
	{
		private class LeafModel
		{
			public float Value { get; set; } = 5f;

			public float ReadOnlyValue => 5f;

			[LocalizedDisplayName("model.value", "Fallback Value")]
			public float LocalizedValue { get; set; } = 1f;

			[LocalizedDescription("model.desc", "Fallback description")]
			public float DescribedValue { get; set; } = 1f;
		}

		private class ParentModel
		{
			public LeafModel Nested { get; set; } = new LeafModel();
		}

		private static Widget FindResetButton(PropertyGrid grid)
		{
			return grid.GetChildren(recursive: true)
				.OfType<Button>()
				.FirstOrDefault(b => (b.Content as Label)?.Text == grid.ResetButtonText);
		}

		private static Label FindLabel(PropertyGrid grid, string text)
		{
			return grid.GetChildren(recursive: true)
				.OfType<Label>()
				.FirstOrDefault(l => l.Text == text);
		}

		[Test]
		public void ResetButtonAppearsWhenDefaultValueProviderSuppliesADifferentDefault()
		{
			var grid = new PropertyGrid
			{
				DefaultValueProvider = (g, r) => r.Name == "Value" ? (object)0f : null,
				Object = new LeafModel { Value = 5f }
			};

			Assert.IsNotNull(FindResetButton(grid));
		}

		[Test]
		public void NoResetButtonWithoutADefaultValueProvider()
		{
			var grid = new PropertyGrid
			{
				Object = new LeafModel()
			};

			Assert.IsNull(FindResetButton(grid));
		}

		/// <summary>
		/// A record without a setter has nothing a reset could write back to - offering the button
		/// would be a dead end.
		/// </summary>
		[Test]
		public void NoResetButtonForARecordWithoutASetter()
		{
			var grid = new PropertyGrid
			{
				DefaultValueProvider = (g, r) => r.Name == "ReadOnlyValue" ? (object)0f : null,
				Object = new LeafModel()
			};

			Assert.IsNull(FindResetButton(grid));
		}

		[Test]
		public void ResetOnlyWhenModifiedDisablesTheButtonAtTheDefaultValue()
		{
			var grid = new PropertyGrid
			{
				DefaultValueProvider = (g, r) => r.Name == "Value" ? (object)5f : null,
				ResetOnlyWhenModified = true,
				Object = new LeafModel { Value = 5f }
			};

			Assert.IsFalse(FindResetButton(grid).Enabled);
		}

		[Test]
		public void ResetOnlyWhenModifiedEnablesTheButtonAwayFromTheDefaultValue()
		{
			var grid = new PropertyGrid
			{
				DefaultValueProvider = (g, r) => r.Name == "Value" ? (object)5f : null,
				ResetOnlyWhenModified = true,
				Object = new LeafModel { Value = 9f }
			};

			Assert.IsTrue(FindResetButton(grid).Enabled);
		}

		/// <summary>
		/// A record standing for a nested object is reset as a whole, and a freshly built default
		/// object never reference-equals the live instance - so a reference-typed group always reads
		/// as modified, by design (see PropertyGrid.IsModified).
		/// </summary>
		[Test]
		public void ReferenceTypedGroupsAlwaysReadAsModified()
		{
			var grid = new PropertyGrid
			{
				DefaultValueProvider = (g, r) => r.Name == "Nested" ? new LeafModel() : null,
				ResetOnlyWhenModified = true,
				Object = new ParentModel()
			};

			Assert.IsTrue(FindResetButton(grid).Enabled);
		}

		[Test]
		public void ModifiedNameColorAppliesOnlyAwayFromTheDefaultValue()
		{
			var atDefault = new PropertyGrid
			{
				DefaultValueProvider = (g, r) => r.Name == "Value" ? (object)5f : null,
				ModifiedNameColor = Color.Red,
				Object = new LeafModel { Value = 5f }
			};
			var unmodifiedColor = FindLabel(atDefault, "Value").TextColor;
			Assert.AreNotEqual(Color.Red, unmodifiedColor);

			var modified = new PropertyGrid
			{
				DefaultValueProvider = (g, r) => r.Name == "Value" ? (object)5f : null,
				ModifiedNameColor = Color.Red,
				Object = new LeafModel { Value = 9f }
			};
			Assert.AreEqual(Color.Red, FindLabel(modified, "Value").TextColor);
		}

		[Test]
		public void ClickingResetRestoresTheDefaultAndFiresPropertyChanged()
		{
			var model = new LeafModel { Value = 9f };
			var grid = new PropertyGrid
			{
				DefaultValueProvider = (g, r) => r.Name == "Value" ? (object)5f : null,
				Object = model
			};

			string changedName = null;
			grid.PropertyChanged += (s, e) => changedName = e.Data;

			((Button)FindResetButton(grid)).DoClick();

			Assert.AreEqual(5f, model.Value);
			Assert.AreEqual("Value", changedName);
		}

		/// <summary>
		/// Regression: DefaultValueProvider is a public mutable field, and RefreshModifiedIndicator
		/// used to call it unguarded. Clearing it after the grid tracked an indicator - then triggering
		/// any change that calls FireChanged - reached that call with the provider gone and threw.
		/// The still-wired reset button (built while the provider was set) is a real, public way to
		/// reach FireChanged without reflecting into a private method.
		/// </summary>
		[Test]
		public void ClearingDefaultValueProviderAfterTrackingDoesNotThrowOnTheNextChange()
		{
			var model = new LeafModel { Value = 9f };
			var grid = new PropertyGrid
			{
				DefaultValueProvider = (g, r) => r.Name == "Value" ? (object)5f : null,
				ResetOnlyWhenModified = true,
				Object = model
			};

			var resetButton = (Button)FindResetButton(grid);
			grid.DefaultValueProvider = null;

			Assert.DoesNotThrow(resetButton.DoClick);
			Assert.AreEqual(5f, model.Value);
		}

		[Test]
		public void LocalizerTranslatesALocalizedDisplayName()
		{
			var grid = new PropertyGrid
			{
				Localizer = (key, fallback) => key == "model.value" ? "Translated!" : null,
				Object = new LeafModel()
			};

			Assert.IsNotNull(FindLabel(grid, "Translated!"));
			Assert.IsNull(FindLabel(grid, "Fallback Value"));
		}

		[Test]
		public void WithoutALocalizerTheFallbackDisplayNameIsShown()
		{
			var grid = new PropertyGrid
			{
				Object = new LeafModel()
			};

			Assert.IsNotNull(FindLabel(grid, "Fallback Value"));
		}

		[Test]
		public void LocalizedDescriptionBecomesTheRowsTooltip()
		{
			var grid = new PropertyGrid
			{
				Object = new LeafModel()
			};

			var label = FindLabel(grid, "DescribedValue");

			Assert.AreEqual("Fallback description", label.Tooltip);
		}
	}
}
