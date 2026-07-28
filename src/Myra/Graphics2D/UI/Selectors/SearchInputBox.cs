#nullable enable

using Myra.Graphics2D.UI.Styles;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
#elif STRIDE
using Stride.Core.Mathematics;
using Stride.Input;
#else
using System.Numerics;
using Myra.Platform;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// Non-generic hook so <see cref="SearchInputBox"/> can forward navigation keys to its
	/// owning <c>SearchableComboBox&lt;T&gt;</c> without itself being generic.
	/// </summary>
	internal interface ISearchInputBoxOwner
	{
		void OnSearchBoxKeyUp();
		void OnSearchBoxKeyDown();
		void OnSearchBoxKeyEnter();
		void OnSearchBoxKeyEscape();
	}

	/// <summary>
	/// TextBox used as the search header of a searchable combo box. Up/Down/Enter/Escape are
	/// forwarded to the owner before the base TextBox gets a chance to handle them, since the
	/// base class otherwise consumes Up/Down for caret movement rather than list navigation.
	/// </summary>
	internal class SearchInputBox : TextBox
	{
		public ISearchInputBoxOwner? Owner { get; set; }

		public SearchInputBox(string styleName = Stylesheet.DefaultStyleName) : base(styleName)
		{
			Multiline = false;
		}

		public override void OnKeyDown(Keys k)
		{
			switch (k)
			{
				case Keys.Up:
					Owner?.OnSearchBoxKeyUp();
					return;

				case Keys.Down:
					Owner?.OnSearchBoxKeyDown();
					return;

				case Keys.Enter:
					Owner?.OnSearchBoxKeyEnter();
					break;

				case Keys.Escape:
					Owner?.OnSearchBoxKeyEscape();
					break;
			}

			base.OnKeyDown(k);
		}

		// Base TextBox hides HintText while focused (it's what stops the hint from
		// overlapping the caret) - but that means opening the popup with FocusSearchOnClick
		// immediately blanks the hint. Painting it back in ourselves when focused-and-empty
		// keeps the placeholder visible without touching TextBox's private hint machinery.
		public override void InternalRender(RenderContext context)
		{
			base.InternalRender(context);

			if (!IsKeyboardFocused || !string.IsNullOrEmpty(Text) || string.IsNullOrEmpty(HintText) || Font == null)
			{
				return;
			}

			var bounds = ActualBounds;
			float oldOpacity = context.Opacity;
			context.Opacity *= 0.5f;
			context.DrawString(Font, HintText, new Vector2(bounds.X, bounds.Y), TextColor);
			context.Opacity = oldOpacity;
		}
	}
}
