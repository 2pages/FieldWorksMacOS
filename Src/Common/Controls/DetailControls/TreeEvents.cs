// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Drawing;
using System.Windows.Forms;

namespace SIL.FieldWorks.Common.Framework.DetailControls
{
	/// <summary>
	/// This is used for a slice to ask the data tree to display a context menu.
	/// </summary>
	public delegate void TreeNodeEventHandler (object sender, TreeNodeEventArgs e);

	/// <remarks>
	/// This is the argument for a TreeNodeEventHandler event.
	/// </remarks>
	public class TreeNodeEventArgs : EventArgs
	{
		private Slice m_slice;
		private Point m_location;
		private Control m_contextControl;

		/// <summary>
		/// Constructor
		/// </summary>
		public TreeNodeEventArgs(Control context, Slice slice, Point location)
		{
			m_location = location;
			m_slice= slice;
			m_contextControl =context;
		}

		public Slice Slice
		{
			get
			{
				return m_slice;
			}
		}
		public Control Context
		{
			get
			{
				return m_contextControl;
			}
		}
		public Point Location
		{
			get
			{
				return m_location;
			}
		}
	}

	/// <summary>
	/// This is used to request slice context menus.
	/// </summary>
	public class SliceMenuRequestArgs : EventArgs
	{
		private Slice m_slice;
		private bool m_hotLinksOnly;

		public SliceMenuRequestArgs(Slice slice, bool hotLinksOnly)
		{
			m_slice = slice;
			m_hotLinksOnly = hotLinksOnly;
		}

		public Slice Slice
		{
			get { return m_slice; }
		}
		public bool HotLinksOnly
		{
			get { return m_hotLinksOnly; }
		}
	}

	public delegate ContextMenu SliceShowMenuRequestHandler (object sender, SliceMenuRequestArgs e);
}
