#if !NET5_0_OR_GREATER

using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
	/// <summary>
	/// Required by the compiler to emit <c>init</c> accessors, which positional
	/// <c>readonly record struct</c>s generate. Absent from netstandard2.0.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	internal static class IsExternalInit
	{
	}
}

#endif
