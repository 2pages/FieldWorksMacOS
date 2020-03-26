// Copyright (c) 2003-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using SIL.FieldWorks.Resources;

namespace SIL.FieldWorks.FwCoreDlgs.Controls
{
	/// <summary />
	internal sealed class DropDownButton : Button
	{
		FwComboBoxBase m_comboBox;
		bool m_isHot;
		bool m_isPressed;

		/// <summary />
		internal DropDownButton(FwComboBoxBase comboBox)
		{
			m_comboBox = comboBox;
			if (Application.RenderWithVisualStyles)
			{
				DoubleBuffered = true;
			}
			else
			{
				Image = ResourceHelper.ComboMenuArrowIcon; // no text, just the image
				BackColor = SystemColors.Control;
			}
		}

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType() + ". ******");
			base.Dispose(disposing);
		}

		/// <summary />
		protected override Size DefaultSize => new Size(PreferredWidth, PreferredHeight);

		const int CP_DROPDOWNBUTTON = 1;
		const int CP_DROPDOWNBUTTONRIGHT = 6;

		private VisualStyleRenderer Renderer
		{
			get
			{
				if (!Application.RenderWithVisualStyles)
				{
					return null;
				}

				VisualStyleElement element;
				if (FwComboBoxBase.SupportsButtonStyle)
				{
					ComboBoxState curState;
					if (m_comboBox == null)
					{
						curState = ComboBoxState.Normal;
					}
					else if (m_comboBox.UseVisualStyleBackColor && m_comboBox.DropDownStyle == ComboBoxStyle.DropDownList)
					{
						curState = m_comboBox.State == ComboBoxState.Disabled ? ComboBoxState.Disabled : ComboBoxState.Normal;
					}
					else
					{
						switch (m_comboBox.State)
						{
							case ComboBoxState.Pressed:
							case ComboBoxState.Disabled:
								curState = m_comboBox.State;
								break;

							default:
								curState = m_isHot ? ComboBoxState.Hot : ComboBoxState.Normal;
								break;
						}
					}
					element = VisualStyleElement.CreateElement(FwComboBoxBase.COMBOBOX_CLASS, CP_DROPDOWNBUTTONRIGHT, (int)curState);
				}
				else
				{
					ComboBoxState curState;
					if (m_comboBox == null)
					{
						curState = ComboBoxState.Normal;
					}
					else if (m_comboBox.State == ComboBoxState.Pressed)
					{
						curState = m_isPressed ? ComboBoxState.Pressed : ComboBoxState.Normal;
					}
					else
					{
						curState = m_comboBox.State;
					}
					element = VisualStyleElement.CreateElement(FwComboBoxBase.COMBOBOX_CLASS, CP_DROPDOWNBUTTON, (int)curState);
				}
				return new VisualStyleRenderer(element);
			}
		}

		/// <inheritdoc />
		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			var renderer = Renderer;
			if (renderer != null)
			{
				if (renderer.IsBackgroundPartiallyTransparent())
				{
					renderer.DrawParentBackground(e.Graphics, ClientRectangle, this);
				}
				renderer.DrawBackground(e.Graphics, ClientRectangle, e.ClipRectangle);
			}
		}

		/// <summary>
		/// Gets the height of the preferred.
		/// </summary>
		internal int PreferredHeight
		{
			get
			{
				var renderer = Renderer;
				if (renderer != null)
				{
					using (var g = CreateGraphics())
					{
						return renderer.GetPartSize(g, ThemeSizeType.True).Height;
					}
				}
				return FwComboBoxBase.ComboHeight;
			}
		}

		/// <summary>
		/// Gets the width of the preferred.
		/// </summary>
		internal int PreferredWidth => 17;

		/// <inheritdoc />
		public override void NotifyDefault(bool value)
		{
			base.NotifyDefault(false);
		}

		/// <inheritdoc />
		protected override bool ShowFocusCues => false;

		/// <inheritdoc />
		protected override void OnGotFocus(EventArgs e)
		{
			base.OnGotFocus(e);
			if (Application.RenderWithVisualStyles && m_comboBox.State != ComboBoxState.Pressed && m_comboBox.State != ComboBoxState.Disabled)
			{
				m_comboBox.State = ComboBoxState.Normal;
			}
		}

		/// <inheritdoc />
		protected override void OnLostFocus(EventArgs e)
		{
			base.OnLostFocus(e);
			if (Application.RenderWithVisualStyles && m_comboBox.State != ComboBoxState.Pressed && m_comboBox.State != ComboBoxState.Disabled)
			{
				m_comboBox.State = ComboBoxState.Normal;
			}
		}

		/// <inheritdoc />
		protected override void OnMouseEnter(EventArgs e)
		{
			base.OnMouseEnter(e);
			if (Application.RenderWithVisualStyles && m_comboBox.State != ComboBoxState.Pressed && m_comboBox.State != ComboBoxState.Disabled)
			{
				m_comboBox.State = ComboBoxState.Hot;
			}
			m_isHot = true;
		}

		/// <inheritdoc />
		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			if (Application.RenderWithVisualStyles && m_comboBox.State != ComboBoxState.Pressed && m_comboBox.State != ComboBoxState.Disabled)
			{
				m_comboBox.State = ComboBoxState.Normal;
			}
			m_isHot = false;
			if (m_isPressed)
			{
				m_isPressed = false;
				Invalidate();
			}
		}

		/// <inheritdoc />
		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			if (Application.RenderWithVisualStyles && m_comboBox.State != ComboBoxState.Pressed && m_comboBox.State != ComboBoxState.Disabled)
			{
				m_comboBox.State = ComboBoxState.Hot;
			}
			if (m_isPressed)
			{
				m_isPressed = false;
				Invalidate();
			}
		}

		/// <inheritdoc />
		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if (Application.RenderWithVisualStyles && m_comboBox.State != ComboBoxState.Disabled)
			{
				m_comboBox.State = ComboBoxState.Pressed;
				m_isPressed = true;
			}
		}
	}
}