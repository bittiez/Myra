using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Myra.Graphics2D.UI.ColorPicker;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility;
using System.Xml.Serialization;
using Myra.MML;
using Myra.Graphics2D.UI.File;
using System.IO;
using Myra.Attributes;
using FontStashSharp;
using FontStashSharp.RichText;
using Myra.Graphics2D.Brushes;
using AssetManagementBase;
using Myra.Events;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#elif STRIDE
using Stride.Core.Mathematics;
using Texture2D = Stride.Graphics.Texture;
#else
using System.Drawing;
using SolidBrush = Myra.Graphics2D.Brushes.SolidBrush;
using Color = FontStashSharp.FSColor;
#endif

namespace Myra.Graphics2D.UI.Properties
{
	public class PropertyGrid : Widget
	{
		private const string DefaultCategoryName = "Miscellaneous";

		private class SubGrid : Widget
		{
			private readonly GridLayout _layout = new GridLayout();

			private readonly ToggleButton _mark;
			private readonly PropertyGrid _propertyGrid;

			/// <summary>Group-level reset button, when the grid supplies defaults. May be null.</summary>
			private readonly Widget _reset;

			public ToggleButton Mark
			{
				get { return _mark; }
			}

			public PropertyGrid PropertyGrid
			{
				get { return _propertyGrid; }
			}

			public Rectangle HeaderBounds
			{
				get
				{
					// Anchored to ActualBounds rather than to the origin: margin, border and padding
					// all shift the content down and right, and the header highlight and its
					// hit test have to shift with it.
					var actualBounds = ActualBounds;
					var headerBounds = new Rectangle(actualBounds.X, actualBounds.Y, actualBounds.Width, _layout.GetRowHeight(0));

					return headerBounds;
				}
			}

			[Browsable(false)]
			[XmlIgnore]
			public bool IsEmpty
			{
				get
				{
					return _propertyGrid.IsEmpty;
				}
			}

			public SubGrid(PropertyGrid parent, object value, string header, string category, string filter, Record parentProperty)
			{
				ChildrenLayout = _layout;

				_layout.ColumnSpacing = 4;
				_layout.RowSpacing = 4;

				_layout.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
				_layout.ColumnsProportions.Add(new Proportion(ProportionType.Fill));
				_layout.RowsProportions.Add(new Proportion(ProportionType.Auto));
				_layout.RowsProportions.Add(new Proportion(ProportionType.Auto));

				_propertyGrid = new PropertyGrid(parent.PropertyGridStyle, category, parentProperty, parent)
				{
					Object = value,
					Filter = filter,
					HorizontalAlignment = HorizontalAlignment.Stretch,
				};
				Grid.SetColumn(_propertyGrid, 1);
				Grid.SetRow(_propertyGrid, 1);

				// Mark
				var markFactory = parent.MarkContentFactory;

				_mark = CreateMarkButton(parent, markFactory);
				Children.Add(_mark);

				WireMarkExpansion(parent, category, parentProperty, markFactory);

				var label = new Label(null)
				{
					Text = header,
				};
				label.ApplyLabelStyle(parent.PropertyGridStyle.LabelStyle);

				// Resets the group as a whole, which replaces every record below it.
				_reset = parent.CreateResetWidget(parentProperty, parentProperty != null && parentProperty.HasSetter);

				// Tracked against the parent, which owns the record this group stands for - a group
				// header's reset button goes stale exactly as a row's does.
				parent.TrackModifiedIndicator(parentProperty, _reset, label);

				Children.Add(CreateHeaderRow(label));

				HorizontalAlignment = HorizontalAlignment.Stretch;
				VerticalAlignment = VerticalAlignment.Stretch;
			}

			/// <summary>
			/// Builds the group's expand/collapse toggle. Content comes from <paramref name="markFactory"/>
			/// when supplied - with the button's own chrome stripped, since then the supplied widget is
			/// meant to be the whole control - or from <see cref="TreeStyle.MarkStyle"/> otherwise.
			/// </summary>
			/// <param name="parent">The grid the group belongs to, for its style and mark factory.</param>
			/// <param name="markFactory">Builds the mark's content given whether it's pressed, or null.</param>
			/// <returns>The unwired toggle button.</returns>
			private static ToggleButton CreateMarkButton(PropertyGrid parent, Func<bool, Widget> markFactory)
			{
				Widget markContent;

				if (markFactory != null)
				{
					markContent = markFactory(false);
				}
				else
				{
					var markImage = new Image();
					var imageStyle = parent.PropertyGridStyle.MarkStyle.ImageStyle;
					if (imageStyle != null)
					{
						markImage.ApplyPressableImageStyle(imageStyle);
					}

					markContent = markImage;
				}

				var mark = new ToggleButton(null)
				{
					HorizontalAlignment = HorizontalAlignment.Left,
					VerticalAlignment = VerticalAlignment.Center,
					Content = markContent
				};

				if (markFactory != null)
				{
					// A supplied mark is the whole control; the toggle button's own frame would
					// otherwise show through around it, and does so differently per state.
					mark.Background = null;
					mark.OverBackground = null;
					mark.PressedBackground = null;
					mark.Border = null;
					mark.BorderThickness = new Thickness(0);
					mark.Padding = new Thickness(0);
				}

				return mark;
			}

			/// <summary>
			/// Hooks the mark's press to show/hide the nested grid and track the category's expanded
			/// state, and sets its initial pressed state - expanded unless <paramref name="parentProperty"/>
			/// carries <see cref="DesignerFoldedAttribute"/>.
			/// </summary>
			/// <param name="parent">The grid the group belongs to, whose expanded-categories set is updated.</param>
			/// <param name="category">The category key tracked in <c>parent._expandedCategories</c>.</param>
			/// <param name="parentProperty">The record the group stands for, or null at the root category.</param>
			/// <param name="markFactory">Rebuilds the mark's content on every press, when supplied.</param>
			private void WireMarkExpansion(PropertyGrid parent, string category, Record parentProperty, Func<bool, Widget> markFactory)
			{
				_mark.PressedChanged += (sender, args) =>
				{
					if (markFactory != null)
					{
						_mark.Content = markFactory(_mark.IsPressed);
					}

					if (_mark.IsPressed)
					{
						Children.Add(_propertyGrid);
						parent._expandedCategories.Add(category);
					}
					else
					{
						Children.Remove(_propertyGrid);
						parent._expandedCategories.Remove(category);
					}
				};

				var expanded = true;
				if (parentProperty != null && parentProperty.FindAttribute<DesignerFoldedAttribute>() != null)
				{
					expanded = false;
				}

				if (expanded)
				{
					_mark.IsPressed = true;
				}
			}

			/// <summary>
			/// Lays out the group's title next to its reset button, when there is one. Returns the
			/// label alone otherwise, so a group without a reset button doesn't carry an empty panel.
			/// </summary>
			/// <param name="label">The group's title label.</param>
			/// <returns>The widget to place in the header row.</returns>
			private Widget CreateHeaderRow(Label label)
			{
				if (_reset == null)
				{
					Grid.SetColumn(label, 1);
					return label;
				}

				var headerPanel = new HorizontalStackPanel
				{
					Spacing = 4,
					VerticalAlignment = VerticalAlignment.Center
				};

				StackPanel.SetProportionType(label, ProportionType.Fill);
				label.VerticalAlignment = VerticalAlignment.Center;

				headerPanel.Widgets.Add(label);
				headerPanel.Widgets.Add(_reset);

				Grid.SetColumn(headerPanel, 1);

				return headerPanel;
			}

			public override void OnTouchDown(TouchEventArgs args)
			{
				base.OnTouchDown(args);

				if (_propertyGrid.ToggleGroupsOnSingleClick)
				{
					ToggleFromHeader();
				}
			}

			public override void OnTouchDoubleClick()
			{
				base.OnTouchDoubleClick();

				// In single-click mode the press has already toggled it; doing it again here would
				// undo that on every double click.
				if (!_propertyGrid.ToggleGroupsOnSingleClick)
				{
					ToggleFromHeader();
				}
			}

			/// <summary>
			/// Expands or collapses the group when the pointer is over the header, but not over one
			/// of the controls in it - those carry their own actions.
			/// </summary>
			private void ToggleFromHeader()
			{
				var mousePosition = Desktop.MousePosition;

				if (!HeaderBounds.Contains(ToLocal(mousePosition)))
				{
					return;
				}

				if (_mark.ContainsGlobalPoint(mousePosition))
				{
					return;
				}

				if (_reset != null && _reset.ContainsGlobalPoint(mousePosition))
				{
					return;
				}

				_mark.IsPressed = !_mark.IsPressed;
			}

			public override void InternalRender(RenderContext context)
			{
				if (_propertyGrid.PropertyGridStyle.SelectionHoverBackground != null && IsMouseInside)
				{
					var headerBounds = HeaderBounds;
					if (headerBounds.Contains(ToLocal(Desktop.MousePosition)))
					{
						_propertyGrid.PropertyGridStyle.SelectionHoverBackground.Draw(context, headerBounds);
					}
				}

				base.InternalRender(context);
			}
		}

		/// <summary>
		/// One row's "has this been touched yet" affordances, kept so they can be re-evaluated when a
		/// value changes. The state is a comparison against a default, which every edit can flip -
		/// deciding it once while building the row leaves the reset button dead for the rest of the
		/// grid's life.
		/// </summary>
		private sealed class ModifiedIndicator
		{
			public Record Record;
			public Widget ResetWidget;
			public Label NameLabel;
			public Color? UnmodifiedNameColor;
		}

		private readonly List<ModifiedIndicator> _modifiedIndicators = new List<ModifiedIndicator>();

		private readonly GridLayout _layout = new GridLayout();
		private readonly PropertyGrid _parentGrid;
		private Record _parentProperty;
		private readonly Dictionary<string, List<Record>> _records = new Dictionary<string, List<Record>>();
		private readonly HashSet<string> _expandedCategories = new HashSet<string>();
		private object _object;
		private bool _ignoreCollections;
		private readonly PropertyGridSettings _settings = new PropertyGridSettings();
		private string _filter;
		private Type _parentType;

		[Browsable(false)]
		[XmlIgnore]
		public TreeStyle PropertyGridStyle { get; private set; }

		[Browsable(false)]
		[XmlIgnore]
		public object Object
		{
			get { return _object; }

			set
			{
				if (value == _object)
				{
					return;
				}

				_object = value;
				Rebuild();

				ObjectChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// Used to determine the attached properties
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Type ParentType
		{
			get
			{
				if (_parentGrid != null)
				{
					return _parentGrid.ParentType;
				}

				return _parentType;
			}

			set
			{
				_parentType = value;
			}
		}

		[Browsable(false)]
		[XmlIgnore]
		public string Category { get; private set; }

		[Category("Behavior")]
		[DefaultValue(false)]
		public bool IgnoreCollections
		{
			get
			{
				if (_parentGrid != null)
				{
					return _parentGrid.IgnoreCollections;
				}

				return _ignoreCollections;
			}

			set
			{
				_ignoreCollections = value;
			}
		}

		[Browsable(false)]
		[XmlIgnore]
		public bool IsEmpty
		{
			get
			{
				return Children.Count == 0;
			}
		}

		[Browsable(false)]
		[XmlIgnore]
		public PropertyGridSettings Settings
		{
			get
			{
				if (_parentGrid != null)
				{
					return _parentGrid.Settings;
				}

				return _settings;
			}
		}

		[Browsable(false)]
		[XmlIgnore]
		public int FirstColumnWidth
		{
			get
			{
				return (int)_layout.ColumnsProportions[0].Value;
			}

			set
			{
				_layout.ColumnsProportions[0].Value = value;
			}
		}

		[DefaultValue(HorizontalAlignment.Stretch)]
		public override HorizontalAlignment HorizontalAlignment
		{
			get { return base.HorizontalAlignment; }
			set { base.HorizontalAlignment = value; }
		}

		[DefaultValue(VerticalAlignment.Stretch)]
		public override VerticalAlignment VerticalAlignment
		{
			get { return base.VerticalAlignment; }
			set { base.VerticalAlignment = value; }
		}

		[XmlIgnore]
		[Browsable(false)]
		public string Filter
		{
			get => _filter;
			set
			{
				if (_filter == value)
				{
					return;
				}

				_filter = value;
				Rebuild();
			}
		}

		[Browsable(false)]
		[XmlIgnore]
		public Func<object, Record, CustomValues> CustomValuesProvider;

		[Browsable(false)]
		[XmlIgnore]
		public Func<Record, object, object, bool> CustomSetter;

		[Browsable(false)]
		[XmlIgnore]
		public Func<Record, object, Widget> CustomWidgetProvider;

		/// <summary>
		/// Supplies the default value of a record. When it returns a non-null value for a settable
		/// record, a reset button is placed beside that record's editor; return null to leave a
		/// record without one. The grid is passed so the provider can locate the record via
		/// <see cref="ParentRecords"/>, which a nested object alone does not identify.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Func<PropertyGrid, Record, object> DefaultValueProvider;

		/// <summary>
		/// The records leading from the root grid down to this one, outermost first. Empty on the
		/// root grid.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public IReadOnlyList<Record> ParentRecords
		{
			get
			{
				var path = new List<Record>();

				for (var grid = this; grid._parentProperty != null; grid = grid._parentGrid)
				{
					path.Insert(0, grid._parentProperty);

					if (grid._parentGrid == null)
					{
						break;
					}
				}

				return path;
			}
		}

		/// <summary>
		/// Builds the per-record reset button, given the record and the action that performs the
		/// reset. Lets a caller supply a button in its own skin, or one whose glyph comes from a
		/// font the default style does not use. Falls back to a text button when null.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Func<Record, Action, Widget> ResetButtonFactory;

		/// <summary>Label of the fallback text reset button, used when there is no factory.</summary>
		[Browsable(false)]
		[XmlIgnore]
		public string ResetButtonText = "R";

		/// <summary>Tooltip of the fallback reset button. Null or empty leaves it without one.</summary>
		[Browsable(false)]
		[XmlIgnore]
		public string ResetButtonTooltip;

		/// <summary>
		/// Builds the content of a group's expand/collapse mark, given whether the group is
		/// currently expanded. When set, the mark's own button chrome is dropped so the supplied
		/// widget is the whole control. Falls back to <see cref="TreeStyle.MarkStyle"/> when null.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Func<bool, Widget> MarkContentFactory;

		/// <summary>
		/// Translates the strings shown for a record, given the lookup key and the fallback text
		/// carried by a <see cref="LocalizedDisplayNameAttribute"/> or
		/// <see cref="LocalizedDescriptionAttribute"/>.
		/// <para>
		/// Supplied per grid rather than held globally, so an application pays for translation only
		/// on the screens that ask for it. Left null, every localized attribute shows its fallback,
		/// which is why the fallback and not the key is what those attributes report as their
		/// framework value.
		/// </para>
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Func<string, string, string> Localizer;

		/// <summary>
		/// When true, a record's reset button is enabled only while that record differs from the
		/// default <see cref="DefaultValueProvider"/> reports for it. The button stays in place
		/// either way, so the row does not change width as values are edited.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public bool ResetOnlyWhenModified;

		/// <summary>
		/// Colour for the name of a record that differs from its default. Null leaves every name in
		/// the style's own colour.
		/// <para>
		/// Needs <see cref="DefaultValueProvider"/>: without one there is nothing to compare against
		/// and nothing is highlighted.
		/// </para>
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public Color? ModifiedNameColor;

		/// <summary>
		/// When true, the editors this grid builds take the mouse wheel only while focused. Hovering
		/// a numeric editor and scrolling the panel otherwise edits it, which is easy to do by
		/// accident and hard to notice.
		/// </summary>
		[Browsable(false)]
		[XmlIgnore]
		public bool MouseWheelRequiresFocusOnEditors;

		/// <summary>Vertical gap between rows.</summary>
		public int RowSpacing
		{
			get { return _layout.RowSpacing; }
			set { _layout.RowSpacing = value; }
		}

		/// <summary>Horizontal gap between the name and editor columns.</summary>
		public int ColumnSpacing
		{
			get { return _layout.ColumnSpacing; }
			set { _layout.ColumnSpacing = value; }
		}

		/// <summary>Extra vertical margin above and below each nested group.</summary>
		public int GroupSpacing { get; set; }

		/// <summary>Whether a horizontal separator is drawn before each nested group.</summary>
		public bool GroupSeparators { get; set; }

		/// <summary>
		/// Whether clicking a group header expands or collapses it. Off by default, which leaves
		/// the header needing a double click.
		/// </summary>
		public bool ToggleGroupsOnSingleClick { get; set; }

		public event EventHandler<GenericEventArgs<string>> PropertyChanged;
		public event EventHandler ObjectChanged;

		private PropertyGrid(TreeStyle style, string category, Record parentProperty, PropertyGrid parentGrid = null)
		{
			ChildrenLayout = _layout;

			_parentGrid = parentGrid;

			_parentProperty = parentProperty;
			_layout.ColumnSpacing = 8;
			_layout.RowSpacing = 8;
			_layout.ColumnsProportions.Add(new Proportion(ProportionType.Part, 1));
			_layout.ColumnsProportions.Add(new Proportion(ProportionType.Part, 1));

			Category = category;

			if (style != null)
			{
				ApplyPropertyGridStyle(style);
			}

			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Stretch;
			Filter = string.Empty;

			this.CustomWidgetProvider = parentGrid?.CustomWidgetProvider;
			this.CustomSetter = parentGrid?.CustomSetter;
			this.CustomValuesProvider = parentGrid?.CustomValuesProvider;

			if (parentGrid != null)
			{
				DefaultValueProvider = parentGrid.DefaultValueProvider;
				ResetButtonFactory = parentGrid.ResetButtonFactory;
				ResetButtonText = parentGrid.ResetButtonText;
				ResetButtonTooltip = parentGrid.ResetButtonTooltip;
				MarkContentFactory = parentGrid.MarkContentFactory;
				Localizer = parentGrid.Localizer;
				ResetOnlyWhenModified = parentGrid.ResetOnlyWhenModified;
				ModifiedNameColor = parentGrid.ModifiedNameColor;
				MouseWheelRequiresFocusOnEditors = parentGrid.MouseWheelRequiresFocusOnEditors;
				GroupSpacing = parentGrid.GroupSpacing;
				GroupSeparators = parentGrid.GroupSeparators;
				ToggleGroupsOnSingleClick = parentGrid.ToggleGroupsOnSingleClick;
				RowSpacing = parentGrid.RowSpacing;
				ColumnSpacing = parentGrid.ColumnSpacing;
			}
		}

		public PropertyGrid(TreeStyle style, string category) : this(style, category, null)
		{
		}

		public PropertyGrid(string category) : this(Stylesheet.Current.TreeStyle, category)
		{
		}

		public PropertyGrid() : this(DefaultCategoryName)
		{
		}

		private void FireChanged(string name)
		{
			// Up the chain as well as here: a nested struct is edited through its own grid but the
			// value the parent holds changes with it, so both their rows can flip.
			for (var grid = this; grid != null; grid = grid._parentGrid)
			{
				grid.RefreshModifiedIndicators();
			}

			var ev = PropertyChanged;

			var p = _parentGrid;
			while (p != null)
			{
				ev = p.PropertyChanged;
				p = p._parentGrid;
			}

			if (ev != null)
			{
				ev(this, new GenericEventArgs<string>(name));
			}
		}

		private static void UpdateLabelCount(Label textBlock, int count)
		{
			textBlock.Text = string.Format("{0} Items", count);
		}

		private void SetValue(Record record, object obj, object value)
		{
			if (CustomSetter != null && CustomSetter(record, obj, value))
			{
				return;
			}

			record.SetValue(obj, value);
		}

		private ComboView CreateCustomValuesEditor(Record record, CustomValues customValues, bool hasSetter)
		{
			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var cv = new ComboView();
			foreach (var v in customValues.Values)
			{
				var label = new Label
				{
					Text = v.Name,
					Tag = v.Value
				};

				cv.Widgets.Add(label);
			}

			cv.SelectedIndex = customValues.SelectedIndex;
			if (hasSetter)
			{
				cv.SelectedIndexChanged += (sender, args) =>
				{
					var item = cv.SelectedIndex != null ? customValues.Values[cv.SelectedIndex.Value].Value : null;
					SetValue(record, _object, item);
					FireChanged(record.Name);
				};
			}
			else
			{
				cv.Enabled = false;
			}

			return cv;
		}

		private CheckButton CreateBooleanEditor(Record record, bool hasSetter)
		{
			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var isChecked = (bool)value;
			var cb = new CheckButton
			{
				IsChecked = isChecked
			};

			if (hasSetter)
			{
				cb.Click += (sender, args) =>
				{
					SetValue(record, _object, cb.IsChecked);
					FireChanged(propertyType.Name);
				};
			}
			else
			{
				cb.Enabled = false;
			}

			return cb;
		}

		private Grid CreateColorEditor(Record record, bool hasSetter)
		{
			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var subGrid = new Grid
			{
				ColumnSpacing = 8,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			var isColor = propertyType == typeof(Color);

			subGrid.ColumnsProportions.Add(new Proportion());
			subGrid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));

			var color = Color.Transparent;
			if (isColor)
			{
				color = (Color)value;
			}
			else if (value != null)
			{
				color = ((Color?)value).Value;
			}

			var image = new Image
			{
				Renderable = Stylesheet.Current.WhiteRegion,
				VerticalAlignment = VerticalAlignment.Center,
				Width = 32,
				Height = 16,
				Color = color
			};

			subGrid.Widgets.Add(image);

			var button = new Button
			{
				Tag = value,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Content = new Label
				{
					HorizontalAlignment = HorizontalAlignment.Center,
					Text = "Change..."
				}
			};
			Grid.SetColumn(button, 1);

			subGrid.Widgets.Add(button);

			if (hasSetter)
			{
				button.Click += (sender, args) =>
				{
					var dlg = new ColorPickerDialog()
					{
						Color = image.Color
					};

					dlg.Closed += (s, a) =>
					{
						if (!dlg.Result)
						{
							return;
						}

						image.Color = dlg.Color;
						SetValue(record, _object, dlg.Color);

						FireChanged(propertyType.Name);
					};

					dlg.ShowModal(Desktop);
				};
			}
			else
			{
				button.Enabled = false;
			}

			return subGrid;
		}

		private Grid CreateBrushEditor(Record record, bool hasSetter)
		{
			var propertyType = record.Type;

			var value = record.GetValue(_object) as SolidBrush;

			var subGrid = new Grid
			{
				ColumnSpacing = 8,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			subGrid.ColumnsProportions.Add(new Proportion());
			subGrid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));

			var color = Color.Transparent;
			if (value != null)
			{
				color = value.Color;
			}

			var image = new Image
			{
				Renderable = Stylesheet.Current.WhiteRegion,
				VerticalAlignment = VerticalAlignment.Center,
				Width = 32,
				Height = 16,
				Color = color
			};

			subGrid.Widgets.Add(image);

			var button = new Button
			{
				Tag = value,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Content = new Label
				{
					Text = "Change...",
					HorizontalAlignment = HorizontalAlignment.Center,
				}
			};
			Grid.SetColumn(button, 1);

			subGrid.Widgets.Add(button);

			if (hasSetter)
			{
				button.Click += (sender, args) =>
				{
					var dlg = new ColorPickerDialog()
					{
						Color = image.Color
					};

					dlg.Closed += (s, a) =>
					{
						if (!dlg.Result)
						{
							return;
						}

						image.Color = dlg.Color;
						SetValue(record, _object, new SolidBrush(dlg.Color));
						var baseObject = _object as BaseObject;
						if (baseObject != null)
						{
							baseObject.Resources[record.Name] = dlg.Color.ToHexString();
						}
						FireChanged(propertyType.Name);
					};

					dlg.ShowModal(Desktop);
				};
			}
			else
			{
				button.Enabled = false;
			}

			return subGrid;
		}

		private ComboView CreateEnumEditor(Record record, bool hasSetter)
		{
			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var isNullable = propertyType.IsNullableEnum();
			var enumType = isNullable ? propertyType.GetNullableType() : propertyType;
			var values = Enum.GetValues(enumType);

			var cv = new ComboView();

			if (isNullable)
			{
				cv.Widgets.Add(new Label
				{
					Text = string.Empty
				});
			}

			foreach (var v in values)
			{
				cv.Widgets.Add(new Label
				{
					Text = v.ToString(),
					Tag = v
				});
			}

			var selectedIndex = Array.IndexOf(values, value);
			if (isNullable)
			{
				++selectedIndex;
			}
			cv.SelectedIndex = selectedIndex;

			if (hasSetter)
			{
				cv.SelectedIndexChanged += (sender, args) =>
				{
					if (cv.SelectedIndex != -1)
					{
						SetValue(record, _object, cv.SelectedItem.Tag);
						FireChanged(record.Name);
					}
				};
			}
			else
			{
				cv.Enabled = false;
			}

			return cv;
		}

		private SpinButton CreateNumericEditor(Record record, bool hasSetter)
		{
			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var numericType = propertyType;
			if (propertyType.IsNullablePrimitive())
			{
				numericType = propertyType.GetNullableType();
			}

			var spinButton = new SpinButton
			{
				Integer = numericType.IsNumericInteger(),
				Nullable = propertyType.IsNullablePrimitive(),
				Value = value != null ? (float)Convert.ChangeType(value, typeof(float)) : default(float?)
			};

			var rangeAttribute = record.FindAttribute<RangeAttribute>();
			if (rangeAttribute != null)
			{
				spinButton.Minimum = rangeAttribute.Minimum;
				spinButton.Maximum = rangeAttribute.Maximum;
			}

			if (hasSetter)
			{
				spinButton.ValueChanged += (sender, args) =>
				{
					try
					{
						object result;

						if (spinButton.Value != null)
						{
							result = Convert.ChangeType(spinButton.Value.Value, numericType);
						}
						else
						{
							result = null;
						}

						SetValue(record, _object, result);

						if (record.Type.IsValueType)
						{
							// Handle structs
							var tg = this;
							var pg = tg._parentGrid;
							while (pg != null && tg._parentProperty != null && tg._parentProperty.Type.IsValueType)
							{
								tg._parentProperty.SetValue(pg._object, tg._object);

								if (!tg._parentProperty.Type.IsValueType)
								{
									break;
								}

								tg = pg;
								pg = tg._parentGrid;
							}
						}

						FireChanged(record.Name);
					}
					catch (InvalidCastException)
					{
						// TODO: Rework this ugly type conversion solution
					}
					catch (Exception ex)
					{
						spinButton.Value = args.OldValue;
						var dialog = Dialog.CreateMessageBox("Error", ex.ToString());
						dialog.ShowModal(Desktop);
					}
				};
			}
			else
			{
				spinButton.Enabled = false;
			}

			return spinButton;
		}

		private TextBox CreateStringEditor(Record record, bool hasSetter)
		{
			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var tf = new TextBox
			{
				Text = value != null ? value.ToString() : string.Empty
			};

			if (hasSetter)
			{
				tf.TextChanged += (sender, args) =>
				{
					try
					{
						object result;

						if (propertyType.IsNullablePrimitive())
						{
							if (string.IsNullOrEmpty(tf.Text))
							{
								result = null;
							}
							else
							{
								result = Convert.ChangeType(tf.Text, record.Type.GetNullableType());
							}
						}
						else
						{
							result = Convert.ChangeType(tf.Text, record.Type);
						}

						SetValue(record, _object, result);

						if (record.Type.IsValueType)
						{
							var tg = this;
							var pg = tg._parentGrid;
							while (pg != null && tg._parentProperty != null)
							{
								tg._parentProperty.SetValue(pg._object, tg._object);

								if (!tg._parentProperty.Type.IsValueType)
								{
									break;
								}

								tg = pg;
								pg = tg._parentGrid;
							}
						}

						FireChanged(record.Name);
					}
					catch (Exception)
					{
						// TODO: Rework this ugly type conversion solution
					}
				};
			}
			else
			{
				tf.Enabled = false;
			}

			return tf;
		}

		private Grid CreateCollectionEditor(Record record, Type itemType)
		{
			var value = record.GetValue(_object);

			var items = (IList)value;

			var subGrid = new Grid
			{
				ColumnSpacing = 8,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			subGrid.ColumnsProportions.Add(new Proportion());
			subGrid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));

			var label = new Label
			{
				VerticalAlignment = VerticalAlignment.Center,
			};
			UpdateLabelCount(label, items.Count);

			subGrid.Widgets.Add(label);

			var button = new Button
			{
				Tag = value,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Content = new Label
				{
					Text = "Change...",
					HorizontalAlignment = HorizontalAlignment.Center,
				}
			};
			Grid.SetColumn(button, 1);

			button.Click += (sender, args) =>
			{
				var collectionEditor = new CollectionEditor(items, itemType);

				var dialog = Dialog.CreateMessageBox("Edit", collectionEditor);

				dialog.ButtonOk.Click += (o, eventArgs) =>
				{
					collectionEditor.SaveChanges();
					UpdateLabelCount(label, items.Count);
				};

				dialog.ShowModal(Desktop);
			};

			subGrid.Widgets.Add(button);

			return subGrid;
		}

		private Grid CreateFileEditor<T>(Record record, bool hasSetter, string filter, Func<string, T> loader)
		{
			if (Settings.AssetManager == null)
			{
				return null;
			}

			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var subGrid = new Grid
			{
				ColumnSpacing = 8,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			subGrid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));
			subGrid.ColumnsProportions.Add(new Proportion());

			var baseObject = _object as BaseObject;
			var path = string.Empty;
			if (baseObject != null)
			{
				baseObject.Resources.TryGetValue(record.Name, out path);
			}
			else if (Settings.ImagePropertyValueGetter != null)
			{
				path = Settings.ImagePropertyValueGetter(record.Name);
			}

			var textBox = new TextBox
			{
				Text = path
			};

			subGrid.Widgets.Add(textBox);

			var button = new Button
			{
				Tag = value,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Content = new Label
				{
					Text = "Change...",
					HorizontalAlignment = HorizontalAlignment.Center,
				}
			};
			Grid.SetColumn(button, 1);

			subGrid.Widgets.Add(button);

			if (hasSetter)
			{
				button.Click += (sender, args) =>
				{
					var dlg = new FileDialog(FileDialogMode.OpenFile)
					{
						Filter = filter
					};

					if (!string.IsNullOrEmpty(textBox.Text))
					{
						var filePath = textBox.Text;
						if (!Path.IsPathRooted(filePath) && !string.IsNullOrEmpty(Settings.BasePath))
						{
							filePath = Path.Combine(Settings.BasePath, filePath);
						}
						dlg.FilePath = filePath;
					}
					else if (!string.IsNullOrEmpty(Settings.BasePath))
					{
						dlg.Folder = Settings.BasePath;
					}

					dlg.Closed += (s, a) =>
					{
						if (!dlg.Result)
						{
							return;
						}

						try
						{
							var filePath = dlg.FilePath;
							if (!string.IsNullOrEmpty(Settings.BasePath))
							{
								filePath = PathUtils.TryToMakePathRelativeTo(filePath, Settings.BasePath);
							}

							var newValue = loader(filePath);
							textBox.Text = filePath;
							SetValue(record, _object, newValue);
							if (baseObject != null)
							{
								baseObject.Resources[record.Name] = filePath;
							}
							else if (Settings.ImagePropertyValueSetter != null)
							{
								Settings.ImagePropertyValueSetter(record.Name, filePath);
							}

							FireChanged(propertyType.Name);
						}
						catch (Exception)
						{

						}
					};

					dlg.ShowModal(Desktop);
				};
			}
			else
			{
				button.Enabled = false;
			}

			return subGrid;
		}

		private Widget CreateAttributeFileEditor(Record record, bool hasSetter, FilePathAttribute attribute)
		{
			var propertyType = record.Type;
			var value = record.GetValue(_object);

			var result = new HorizontalStackPanel
			{
				Spacing = 8
			};

			TextBox path = null;
			if (attribute.ShowPath)
			{
				path = new TextBox
				{
					Readonly = true,
					HorizontalAlignment = HorizontalAlignment.Stretch
				};

				if (value != null)
				{
					path.Text = value.ToString();
				}

				StackPanel.SetProportionType(path, ProportionType.Fill);
				result.Widgets.Add(path);
			}

			var button = new Button
			{
				Tag = value,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Content = new Label
				{
					Text = "Change...",
					HorizontalAlignment = HorizontalAlignment.Center,
				}
			};
			Grid.SetColumn(button, 1);

			if (hasSetter)
			{
				button.Click += (sender, args) =>
				{
					var dlg = new FileDialog(attribute.DialogMode)
					{
						Filter = attribute.Filter
					};

					if (value != null)
					{
						var filePath = value.ToString();
						if (!Path.IsPathRooted(filePath) && !string.IsNullOrEmpty(Settings.BasePath))
						{
							filePath = Path.Combine(Settings.BasePath, filePath);
						}
						dlg.FilePath = filePath;
					}
					else if (!string.IsNullOrEmpty(Settings.BasePath))
					{
						dlg.Folder = Settings.BasePath;
					}

					dlg.Closed += (s, a) =>
					{
						if (!dlg.Result)
						{
							return;
						}

						try
						{
							var filePath = dlg.FilePath;
							if (!string.IsNullOrEmpty(Settings.BasePath))
							{
								filePath = PathUtils.TryToMakePathRelativeTo(filePath, Settings.BasePath);
							}

							if (path != null)
							{
								path.Text = filePath;
							}

							SetValue(record, _object, filePath);

							FireChanged(propertyType.Name);
						}
						catch (Exception)
						{
						}
					};

					dlg.ShowModal(Desktop);
				};
			}
			else
			{
				button.Enabled = false;
			}

			result.Widgets.Add(button);

			return result;
		}

		private void FillSubGrid(ref int y, IReadOnlyList<Record> records)
		{
			for (var i = 0; i < records.Count; ++i)
			{
				var record = records[i];

				var hasSetter = record.HasSetter;
				if (_parentProperty != null && _parentProperty.Type.IsValueType && !_parentProperty.HasSetter)
				{
					hasSetter = false;
				}

				var value = record.GetValue(_object);
				Widget valueWidget = null;

				var oldY = y;

				var propertyType = record.Type;

				Proportion rowProportion;
				CustomValues customValues = null;

				var needsSubGrid = false;
				if ((valueWidget = CustomWidgetProvider?.Invoke(record, _object)) != null)
				{

				}
				else if (CustomValuesProvider != null && (customValues = CustomValuesProvider(_object, record)) != null)
				{
					if (customValues.Values.Length == 0)
					{
						continue;
					}

					valueWidget = CreateCustomValuesEditor(record, customValues, hasSetter);
					if (value != null && !value.GetType().IsPrimitive && value.GetType() != typeof(string))
					{
						needsSubGrid = true;
					}
				}
				else if (propertyType == typeof(bool))
				{
					valueWidget = CreateBooleanEditor(record, hasSetter);
				}
				else if (propertyType == typeof(Color) || propertyType == typeof(Color?))
				{
					valueWidget = CreateColorEditor(record, hasSetter);
				}
				else if (propertyType.IsEnum || propertyType.IsNullableEnum())
				{
					valueWidget = CreateEnumEditor(record, hasSetter);
				}
				else if (propertyType.IsNumericType() ||
						 (propertyType.IsNullablePrimitive() && propertyType.GetNullableType().IsNumericType()))
				{
					valueWidget = CreateNumericEditor(record, hasSetter);
				}
				else if (propertyType == typeof(string) && record.FindAttribute<FilePathAttribute>() != null)
				{
					var filePathAttr = record.FindAttribute<FilePathAttribute>();
					valueWidget = CreateAttributeFileEditor(record, hasSetter, filePathAttr);
				}
				else if (propertyType == typeof(string) || propertyType.IsPrimitive || propertyType.IsNullablePrimitive())
				{
					valueWidget = CreateStringEditor(record, hasSetter);
				}
				else if (typeof(IList).IsAssignableFrom(propertyType))
				{
					if (!IgnoreCollections)
					{
						var it = propertyType.FindGenericType(typeof(ICollection<>));
						if (it != null)
						{
							var itemType = it.GenericTypeArguments[0];
							if (value != null)
							{
								valueWidget = CreateCollectionEditor(record, itemType);
							}
						}
					}
				}
				else if (propertyType == typeof(SpriteFontBase))
				{
					valueWidget = CreateFileEditor(record, hasSetter, "*.fnt", name => Settings.AssetManager.LoadFont(name));
				}
				else if (propertyType == typeof(IBrush))
				{
					valueWidget = CreateBrushEditor(record, hasSetter);
				}
				else if (propertyType == typeof(IImage))
				{
					valueWidget = CreateFileEditor(record, hasSetter, "*.png|*.jpg|*.bmp|*.gif", name => Settings.AssetManager.LoadTextureRegion(name));
				}
#if !PLATFORM_AGNOSTIC
				else if (propertyType == typeof(Texture2D))
				{
					valueWidget = CreateFileEditor(record, hasSetter, "*.png|*.jpg|*.bmp|*.gif", name => Settings.AssetManager.LoadTexture2D(MyraEnvironment.GraphicsDevice, name));
				}
#if !STRIDE
				else if (propertyType == typeof(TextureCube))
				{
					valueWidget = CreateFileEditor(record, hasSetter, "*.dds", name => Settings.AssetManager.LoadTexture2D(MyraEnvironment.GraphicsDevice, name));
				}
#endif
#endif
				else
				{
					if (value == null)
					{
						var tb = new Label();
						tb.ApplyLabelStyle(PropertyGridStyle.LabelStyle);
						tb.Text = "null";

						valueWidget = tb;
					} else
					{
						needsSubGrid = true;
					}
				}

				if (valueWidget != null)
				{
					var name = DisplayNameOf(record);

					if (!PassesFilter(name))
					{
						continue;
					}

					var nameLabel = new Label
					{
						Text = name,
						VerticalAlignment = VerticalAlignment.Center,
					};

					if (MouseWheelRequiresFocusOnEditors)
					{
						RequireFocusForMouseWheel(valueWidget);
					}

					var description = DescriptionOf(record);
					if (!string.IsNullOrEmpty(description))
					{
						nameLabel.Tooltip = description;
						valueWidget.Tooltip = description;
					}

					Grid.SetColumn(nameLabel, 0);
					Grid.SetRow(nameLabel, oldY);

					Children.Add(nameLabel);

					var rowWidget = WrapWithResetButton(record, valueWidget, hasSetter, out var resetWidget);

					// Both affordances follow the value from here on, rather than being frozen as the
					// row was built.
					TrackModifiedIndicator(record, resetWidget, nameLabel);

					Grid.SetColumn(rowWidget, 1);
					Grid.SetRow(rowWidget, oldY);
					rowWidget.HorizontalAlignment = HorizontalAlignment.Stretch;
					rowWidget.VerticalAlignment = VerticalAlignment.Top;

					Children.Add(rowWidget);

					rowProportion = new Proportion(ProportionType.Auto);
					_layout.RowsProportions.Add(rowProportion);
					++y;
				}

				if (needsSubGrid)
				{
					// Subgrid
					if (value != null)
					{
						// A group is titled the same way a row is. Reading record.Name here instead
						// would leave a nested object as the one thing in the grid that ignores its
						// own display metadata.
						var groupName = DisplayNameOf(record);

						if (PassesFilter(groupName))
						{
							if (GroupSeparators && y > 0)
							{
								var separator = new HorizontalSeparator();
								Grid.SetColumnSpan(separator, 2);
								Grid.SetRow(separator, y);

								Children.Add(separator);

								_layout.RowsProportions.Add(new Proportion(ProportionType.Auto));
								++y;
							}

							var subGrid = new SubGrid(this, value, groupName, DefaultCategoryName, string.Empty, record);
							subGrid.Margin = new Thickness(0, GroupSpacing, 0, GroupSpacing);

							var groupDescription = DescriptionOf(record);
							if (!string.IsNullOrEmpty(groupDescription))
							{
								subGrid.Tooltip = groupDescription;
							}

							Grid.SetColumnSpan(subGrid, 2);
							Grid.SetRow(subGrid, y);

							Children.Add(subGrid);

							rowProportion = new Proportion(ProportionType.Auto);
							_layout.RowsProportions.Add(rowProportion);
							++y;
						}

						continue;
					}
				}
			}
		}

		/// <summary>
		/// Builds the button that restores <paramref name="record"/> to the default supplied by
		/// <see cref="DefaultValueProvider"/>, or null when there is no default to restore to.
		/// <para>
		/// Recursive by construction: a record holding a nested object is reset as a whole, which
		/// replaces everything below it.
		/// </para>
		/// </summary>
		private Widget CreateResetWidget(Record record, bool hasSetter)
		{
			if (record == null || DefaultValueProvider == null || !hasSetter)
			{
				return null;
			}

			var defaultValue = DefaultValueProvider(this, record);
			if (defaultValue == null)
			{
				return null;
			}

			Action reset = () =>
			{
				SetValue(record, _object, defaultValue);
				PropagateValueTypeChange();
				FireChanged(record.Name);

				// The editor widgets read their value once, at build time.
				Rebuild();
			};

			Widget resetWidget;

			if (ResetButtonFactory != null)
			{
				resetWidget = ResetButtonFactory(record, reset);
			}
			else
			{
				var button = Button.CreateTextButton(ResetButtonText);
				button.VerticalAlignment = VerticalAlignment.Center;

				if (!string.IsNullOrEmpty(ResetButtonTooltip))
				{
					button.Tooltip = ResetButtonTooltip;
				}

				button.Click += (sender, args) => reset();

				resetWidget = button;
			}

			return resetWidget;
		}

		/// <summary>
		/// Records a row's reset button and name label so their appearance can follow the value.
		/// Ignored when the grid has nothing to compare against, or has been asked for neither
		/// affordance.
		/// </summary>
		/// <param name="record">The record the row shows.</param>
		/// <param name="resetWidget">The row's reset button, or null.</param>
		/// <param name="nameLabel">The row's name label, or null.</param>
		private void TrackModifiedIndicator(Record record, Widget resetWidget, Label nameLabel)
		{
			if (record == null || DefaultValueProvider == null)
			{
				return;
			}

			if (!ResetOnlyWhenModified && ModifiedNameColor == null)
			{
				return;
			}

			_modifiedIndicators.Add(new ModifiedIndicator
			{
				Record = record,
				ResetWidget = resetWidget,
				NameLabel = nameLabel,

				// Captured rather than assumed: the label's colour comes from the style, and putting
				// a hard-coded one back would repaint every unmodified row the moment one was edited.
				UnmodifiedNameColor = nameLabel?.TextColor
			});

			RefreshModifiedIndicator(_modifiedIndicators[_modifiedIndicators.Count - 1]);
		}

		/// <summary>
		/// Re-reads every tracked row and updates its reset button and name colour. Called on each
		/// change rather than rebuilding the grid: a rebuild would replace the editor being typed
		/// into and take the caret with it.
		/// </summary>
		private void RefreshModifiedIndicators()
		{
			for (var i = 0; i < _modifiedIndicators.Count; ++i)
			{
				RefreshModifiedIndicator(_modifiedIndicators[i]);
			}
		}

		/// <summary>
		/// Re-evaluates one tracked row against its default and updates its reset button's enabled
		/// state and its name's colour to match.
		/// </summary>
		/// <param name="indicator">The row's tracked affordances.</param>
		private void RefreshModifiedIndicator(ModifiedIndicator indicator)
		{
			// DefaultValueProvider is a public mutable field - it can be cleared after the indicator
			// was tracked, and the next edit would otherwise reach this call with it gone.
			var defaultValue = DefaultValueProvider?.Invoke(this, indicator.Record);
			var modified = defaultValue != null && IsModified(indicator.Record, defaultValue);

			if (ResetOnlyWhenModified && indicator.ResetWidget != null)
			{
				indicator.ResetWidget.Enabled = modified;
			}

			if (ModifiedNameColor != null && indicator.NameLabel != null)
			{
				indicator.NameLabel.TextColor = modified ? ModifiedNameColor.Value : indicator.UnmodifiedNameColor ?? indicator.NameLabel.TextColor;
			}
		}

		/// <summary>
		/// Makes an editor take the mouse wheel only while focused, itself and everything inside it -
		/// a composite editor puts the wheel handling on an inner widget.
		/// </summary>
		/// <param name="widget">The editor.</param>
		private static void RequireFocusForMouseWheel(Widget widget)
		{
			if (widget == null)
			{
				return;
			}

			widget.MouseWheelRequiresFocus = true;

			foreach (var child in widget.ChildrenCopy)
			{
				RequireFocusForMouseWheel(child);
			}
		}

		/// <summary>
		/// Whether a record currently differs from the default supplied for it.
		/// </summary>
		/// <param name="record">The record to test.</param>
		/// <param name="defaultValue">Its default, as already resolved by the caller.</param>
		/// <returns>Whether the two differ.</returns>
		private bool IsModified(Record record, object defaultValue)
		{
			var value = record.GetValue(_object);

			// Value types and strings compare by content. A nested object reaches here through its
			// group header, and a reference type has no content comparison to make - so a group
			// standing for one always reads as modified. Better that way round than a reset button
			// that refuses to undo something.
			return !Equals(value, defaultValue);
		}

		/// <summary>
		/// Pairs an editor with its reset button, when there is one. Returns the editor untouched
		/// otherwise.
		/// </summary>
		private Widget WrapWithResetButton(Record record, Widget valueWidget, bool hasSetter, out Widget resetWidget)
		{
			resetWidget = CreateResetWidget(record, hasSetter);

			if (resetWidget == null)
			{
				return valueWidget;
			}

			var panel = new HorizontalStackPanel
			{
				Spacing = 4,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			// The editor keeps the stretch it would have had as the row's only widget, so pairing it
			// with a button doesn't shrink it back to its natural width and leave a gap.
			valueWidget.HorizontalAlignment = HorizontalAlignment.Stretch;
			valueWidget.VerticalAlignment = VerticalAlignment.Center;

			StackPanel.SetProportionType(valueWidget, ProportionType.Fill);
			panel.Widgets.Add(valueWidget);
			panel.Widgets.Add(resetWidget);

			return panel;
		}

		/// <summary>
		/// Writes this grid's object back up the chain of parent grids. Needed after any edit when
		/// the object is a boxed nested struct, since each level holds its own copy.
		/// </summary>
		private void PropagateValueTypeChange()
		{
			var grid = this;
			var parent = grid._parentGrid;

			while (parent != null && grid._parentProperty != null && grid._parentProperty.Type.IsValueType)
			{
				grid._parentProperty.SetValue(parent._object, grid._object);

				grid = parent;
				parent = grid._parentGrid;
			}
		}

		public bool PassesFilter(string name)
		{
			if (string.IsNullOrEmpty(Filter) || string.IsNullOrEmpty(name))
			{
				return true;
			}

			return name.ToLower().Contains(_filter.ToLower());
		}

		/// <summary>
		/// The text to title a record with: its localized display name, its plain one, or failing
		/// both the member's own name.
		/// </summary>
		/// <param name="record">The record being shown.</param>
		/// <returns>The text.</returns>
		private string DisplayNameOf(Record record)
		{
			var localized = record.FindAttribute<LocalizedDisplayNameAttribute>();

			if (localized != null)
			{
				return Localize(localized.Key, localized.DisplayName);
			}

			var displayName = record.FindAttribute<DisplayNameAttribute>();

			return displayName != null ? displayName.DisplayName : record.Name;
		}

		/// <summary>
		/// The tooltip text for a record, or null where it carries none.
		/// </summary>
		/// <param name="record">The record being shown.</param>
		/// <returns>The text, or null.</returns>
		private string DescriptionOf(Record record)
		{
			var localized = record.FindAttribute<LocalizedDescriptionAttribute>();

			if (localized != null)
			{
				return Localize(localized.Key, localized.Description);
			}

			var description = record.FindAttribute<DescriptionAttribute>();

			return description != null ? description.Description : null;
		}

		/// <summary>
		/// Runs a key and its fallback through <see cref="Localizer"/>. Without one, or where the
		/// localizer has nothing for the key, the fallback stands.
		/// </summary>
		/// <param name="key">The lookup key.</param>
		/// <param name="fallback">Text to use when the key does not resolve.</param>
		/// <returns>The text to show.</returns>
		private string Localize(string key, string fallback)
		{
			if (Localizer == null)
			{
				return fallback;
			}

			var localized = Localizer(key, fallback);

			return string.IsNullOrEmpty(localized) ? fallback : localized;
		}

		public void Rebuild()
		{
			_layout.RowsProportions.Clear();
			Children.Clear();
			_records.Clear();
			_expandedCategories.Clear();
			_modifiedIndicators.Clear();

			if (_object == null)
			{
				return;
			}

			// Properties
			var properties = from p in _object.GetType().GetProperties() select p;
			var records = new List<Record>();
			foreach (var property in properties)
			{
				if (property.GetGetMethod() == null ||
					!property.GetGetMethod().IsPublic ||
					property.GetGetMethod().IsStatic)
				{
					continue;
				}

				var hasSetter = property.GetSetMethod() != null &&
								property.GetSetMethod().IsPublic;

				var browsableAttr = property.FindAttribute<BrowsableAttribute>();
				if (browsableAttr != null && !browsableAttr.Browsable)
				{
					continue;
				}

				var readOnlyAttr = property.FindAttribute<ReadOnlyAttribute>();
				if (readOnlyAttr != null && readOnlyAttr.IsReadOnly)
				{
					hasSetter = false;
				}

				var record = new PropertyRecord(property)
				{
					HasSetter = hasSetter
				};

				var categoryAttr = property.FindAttribute<CategoryAttribute>();
				record.Category = categoryAttr != null ? categoryAttr.Category : DefaultCategoryName;

				records.Add(record);
			}

			// Fields
			var fields = from f in _object.GetType().GetFields() select f;
			foreach (var field in fields)
			{
				if (!field.IsPublic || field.IsStatic)
				{
					continue;
				}

				var browsableAttr = field.FindAttribute<BrowsableAttribute>();
				if (browsableAttr != null && !browsableAttr.Browsable)
				{
					continue;
				}

				var categoryAttr = field.FindAttribute<CategoryAttribute>();

				var hasSetter = true;
				var readOnlyAttr = field.FindAttribute<ReadOnlyAttribute>();
				if (readOnlyAttr != null && readOnlyAttr.IsReadOnly)
				{
					hasSetter = false;
				}

				var record = new FieldRecord(field)
				{
					HasSetter = hasSetter,
					Category = categoryAttr != null ? categoryAttr.Category : DefaultCategoryName
				};

				records.Add(record);
			}

			// Attached properties
			var asWidget = _object as Widget;
			if (asWidget != null && ParentType != null)
			{
				var attachedProperties = AttachedPropertiesRegistry.GetPropertiesOfType(ParentType);
				foreach (var attachedProperty in attachedProperties)
				{
					var record = new AttachedPropertyRecord(attachedProperty)
					{
						Category = attachedProperty.OwnerType.Name
					};

					records.Add(record);
				}
			}

			// Sort by categories
			for (var i = 0; i < records.Count; ++i)
			{
				var record = records[i];

				List<Record> categoryRecords;
				if (!_records.TryGetValue(record.Category, out categoryRecords))
				{
					categoryRecords = new List<Record>();
					_records[record.Category] = categoryRecords;
				}

				categoryRecords.Add(record);
			}

			// Sort by names within categories
			foreach (var category in _records)
			{
				category.Value.Sort((a, b) => Comparer<string>.Default.Compare(a.Name, b.Name));
			}

			var ordered = _records.OrderBy(key => key.Key);

			var y = 0;
			List<Record> defaultCategoryRecords;
			if (_records.TryGetValue(Category, out defaultCategoryRecords))
			{
				FillSubGrid(ref y, defaultCategoryRecords);
			}

			if (Category != DefaultCategoryName)
			{
				return;
			}

			foreach (var category in ordered)
			{
				if (category.Key == DefaultCategoryName)
				{
					continue;
				}

				var subGrid = new SubGrid(this, Object, category.Key, category.Key, Filter, null);
				Grid.SetColumnSpan(subGrid, 2);
				Grid.SetRow(subGrid, y); ;


				if (subGrid.IsEmpty)
				{
					continue;
				}

				Children.Add(subGrid);

				if (_expandedCategories.Contains(category.Key))
				{
					subGrid.Mark.IsPressed = true;
				}

				var rp = new Proportion(ProportionType.Auto);
				_layout.RowsProportions.Add(rp);

				y++;
			}
		}

		public void ApplyPropertyGridStyle(TreeStyle style)
		{
			ApplyWidgetStyle(style);

			PropertyGridStyle = style;
		}
	}
}
