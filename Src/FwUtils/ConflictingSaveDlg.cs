// Copyright (c) 2010-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Drawing;
using System.Windows.Forms;

namespace SIL.FieldWorks.Common.FwUtils
{
	/// <summary>
	/// This dialog is like a message box, but offers two buttons, OK and "Refresh Now", which
	/// when clicked produces a DialogResult of 'Yes'.
	/// </summary>
	internal sealed partial class ConflictingSaveDlg : Form
	{
		/// <summary />
		internal ConflictingSaveDlg()
		{
			InitializeComponent();
			pictureBox1.BackgroundImage = SystemIcons.Warning.ToBitmap();
			pictureBox1.Size = SystemIcons.Warning.Size;
		}
	}
}
