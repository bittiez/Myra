using System;
using System.ComponentModel;

namespace Myra.Graphics2D.UI.Properties
{
	/// <summary>
	/// A display name that can be translated: a lookup key plus the text to fall back on.
	/// <para>
	/// Extends <see cref="DisplayNameAttribute"/> and reports the fallback as its
	/// <see cref="DisplayNameAttribute.DisplayName"/>, so a grid with no
	/// <see cref="PropertyGrid.Localizer"/> - and any other consumer of the framework attribute -
	/// still shows readable text rather than a raw key.
	/// </para>
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
	public sealed class LocalizedDisplayNameAttribute : DisplayNameAttribute
	{
		/// <summary>Lookup key for the translation.</summary>
		public string Key { get; }

		/// <summary>Builds the attribute.</summary>
		/// <param name="key">Lookup key for the translation.</param>
		/// <param name="fallback">Text to show when nothing translates the key.</param>
		public LocalizedDisplayNameAttribute(string key, string fallback) : base(fallback)
		{
			Key = key;
		}
	}

	/// <summary>
	/// A description - the tooltip text - that can be translated. The same arrangement as
	/// <see cref="LocalizedDisplayNameAttribute"/>, for the longer string.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
	public sealed class LocalizedDescriptionAttribute : DescriptionAttribute
	{
		/// <summary>Lookup key for the translation.</summary>
		public string Key { get; }

		/// <summary>Builds the attribute.</summary>
		/// <param name="key">Lookup key for the translation.</param>
		/// <param name="fallback">Text to show when nothing translates the key.</param>
		public LocalizedDescriptionAttribute(string key, string fallback) : base(fallback)
		{
			Key = key;
		}
	}
}
