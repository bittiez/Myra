#nullable enable

#if MONOGAME || FNA
using Microsoft.Xna.Framework.Input;
#elif STRIDE
using Stride.Input;
#else
using Myra.Platform;
#endif

namespace Myra.Graphics2D.UI
{
	/// <summary>
	/// Floating popup container shown via <c>Desktop.ShowContextMenu</c> by
	/// <see cref="SearchableComboBox{T}"/>. Forwards Up/Down/Enter/Escape to the owner when
	/// the popup itself (rather than the search box) holds keyboard focus.
	/// </summary>
	internal class PopupPanel : VerticalStackPanel
	{
		public ISearchInputBoxOwner? Owner { get; set; }

		public override void OnKeyDown(Keys k)
		{
			base.OnKeyDown(k);

			switch (k)
			{
				case Keys.Up:
					Owner?.OnSearchBoxKeyUp();
					break;

				case Keys.Down:
					Owner?.OnSearchBoxKeyDown();
					break;

				case Keys.Enter:
					Owner?.OnSearchBoxKeyEnter();
					break;

				case Keys.Escape:
					Owner?.OnSearchBoxKeyEscape();
					break;
			}
		}
	}
}
