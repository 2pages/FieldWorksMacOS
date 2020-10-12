// SilSidePane, Copyright 2009-2020 SIL International. All rights reserved.
// SilSidePane is licensed under the Code Project Open License (CPOL), <http://www.codeproject.com/info/cpol10.aspx>.
// Derived from OutlookBar v2 2005 <http://www.codeproject.com/KB/vb/OutlookBar.aspx>, Copyright 2007 by Star Vega.
// Changed in 2008 and 2009 by SIL International to convert to C# and add more functionality.

using System;
using System.Drawing;

namespace LanguageExplorer.Controls.SilSidePane
{
	/// <summary>
	/// Item of a Tab in a SidePane
	/// </summary>
	public class Item
	{
		/// <summary>
		/// Actual underlying widget associated with this Item instance
		/// </summary>
		internal object UnderlyingWidget { get; set; }

		/// <summary>Internal name of the tab</summary>
		public string Name { get; set; }

		/// <summary>Text that displays on the tab</summary>
		public string Text { get; set; }

		/// <summary />
		public Image Icon { get; set; }

		/// <summary>
		/// A place where clients can store arbitrary data associated with this item.
		/// </summary>
		public object Tag { get; set; }

		/// <summary />
		public Item(string name)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Text = name;
		}

		/// <summary />
		public override string ToString()
		{
			return Name;
		}
	}
}