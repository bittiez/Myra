using System;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using System.Drawing;
#endif

namespace Myra.Events
{
	/// <summary>Which button (physical or touch-equivalent) produced a touch event.</summary>
	public enum TouchButton
	{
		None,
		Left,
		Middle,
		Right
	}

	/// <summary>Carries the position and originating button of a touch down/up event.</summary>
	public sealed class TouchEventArgs : EventArgs
	{
		public Point Position { get; }
		public TouchButton Button { get; }

		public TouchEventArgs(Point position, TouchButton button)
		{
			Position = position;
			Button = button;
		}
	}
}
