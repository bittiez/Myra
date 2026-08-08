using System;
using Microsoft.Xna.Framework;
using Myra.Events;
using Myra.Graphics2D.UI;
using NUnit.Framework;

namespace Myra.Tests
{
	/// <summary>
	/// Covers the new <see cref="TouchEventArgs"/>/<see cref="TouchButton"/> plumbing through
	/// <see cref="Desktop"/> and <see cref="Widget"/>, and <see cref="Widget.MouseWheelRequiresFocus"/>.
	/// Both ride on <see cref="MyraEnvironment.MouseInfoGetter"/>, so a fake getter stands in for the
	/// platform mouse driver - there is nothing to click or scroll headlessly otherwise.
	/// </summary>
	[TestFixture]
	public class TouchAndWheelInputTests
	{
		private Func<MouseInfo> _originalGetter;

		[SetUp]
		public void SetUp()
		{
			_originalGetter = MyraEnvironment.MouseInfoGetter;
		}

		[TearDown]
		public void TearDown()
		{
			MyraEnvironment.MouseInfoGetter = _originalGetter;
		}

		private static Desktop CreateDesktop(Widget root)
		{
			var desktop = new Desktop
			{
				BoundsFetcher = () => new Rectangle(0, 0, 640, 480),
				Root = root
			};

			return desktop;
		}

		[Test]
		public void DesktopTouchDownReportsTheButtonThatTriggeredIt()
		{
			var mouse = new MouseInfo { Position = new Point(5, 5), IsRightButtonDown = true };
			MyraEnvironment.MouseInfoGetter = () => mouse;

			var desktop = CreateDesktop(new Label());

			TouchEventArgs captured = null;
			desktop.TouchDown += (s, e) => captured = e;

			desktop.Render();

			Assert.IsNotNull(captured, "TouchDown did not fire");
			Assert.AreEqual(TouchButton.Right, captured.Button);
			Assert.AreEqual(new Point(5, 5), captured.Position);
		}

		[Test]
		public void WidgetTouchDownCarriesTheDesktopsButton()
		{
			var widget = new Label { Left = 0, Top = 0, Width = 100, Height = 30 };
			var mouse = new MouseInfo { Position = new Point(10, 10), IsLeftButtonDown = true };
			MyraEnvironment.MouseInfoGetter = () => mouse;

			var desktop = CreateDesktop(widget);

			TouchEventArgs captured = null;
			widget.TouchDown += (s, e) => captured = e;

			desktop.Render();

			Assert.IsNotNull(captured, "TouchDown did not fire on the widget");
			Assert.AreEqual(TouchButton.Left, captured.Button);
		}

		[Test]
		public void MiddleButtonIsReportedAsSuch()
		{
			var mouse = new MouseInfo { Position = new Point(5, 5), IsMiddleButtonDown = true };
			MyraEnvironment.MouseInfoGetter = () => mouse;

			var desktop = CreateDesktop(new Label());

			TouchEventArgs captured = null;
			desktop.TouchDown += (s, e) => captured = e;

			desktop.Render();

			Assert.AreEqual(TouchButton.Middle, captured.Button);
		}

		[Test]
		public void MouseWheelRequiresFocusBlocksWheelOnAnUnfocusedWidget()
		{
			var spin = new SpinButton
			{
				Left = 0,
				Top = 0,
				Width = 100,
				Height = 30,
				Value = 5,
				MouseWheelRequiresFocus = true
			};

			var mouse = new MouseInfo { Position = new Point(10, 10), Wheel = 1 };
			MyraEnvironment.MouseInfoGetter = () => mouse;

			var desktop = CreateDesktop(spin);
			desktop.Render();

			Assert.AreEqual(5, spin.Value);
		}

		[Test]
		public void MouseWheelRequiresFocusAllowsWheelOnceFocused()
		{
			var spin = new SpinButton
			{
				Left = 0,
				Top = 0,
				Width = 100,
				Height = 30,
				Value = 5,
				MouseWheelRequiresFocus = true
			};

			var mouse = new MouseInfo { Position = new Point(10, 10), Wheel = 1 };
			MyraEnvironment.MouseInfoGetter = () => mouse;

			var desktop = CreateDesktop(spin);
			desktop.FocusedKeyboardWidget = spin;
			desktop.Render();

			Assert.AreEqual(6, spin.Value);
		}

		[Test]
		public void WithoutMouseWheelRequiresFocusHoveringIsEnough()
		{
			var spin = new SpinButton
			{
				Left = 0,
				Top = 0,
				Width = 100,
				Height = 30,
				Value = 5
			};

			var mouse = new MouseInfo { Position = new Point(10, 10), Wheel = 1 };
			MyraEnvironment.MouseInfoGetter = () => mouse;

			var desktop = CreateDesktop(spin);
			desktop.Render();

			Assert.AreEqual(6, spin.Value);
		}
	}
}
