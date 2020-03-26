// Copyright (c) 2011-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using SIL.LCModel.Core.KernelInterfaces;

namespace LanguageExplorer.Controls.XMLViews
{
	/// <summary>
	/// This is a base class for filter combo items that don't actually involve a mather. Typically
	/// (e.g., TextComboItem) the only purpose is for Invoke to launch a dialog.
	/// </summary>
	internal class NoChangeFilterComboItem : FilterComboItem
	{
		/// <summary />
		internal NoChangeFilterComboItem(ITsString tssName) : base(tssName, null, null)
		{
		}

		/// <summary>
		/// Default for this class is to do nothing.
		/// </summary>
		internal override bool Invoke()
		{
			return false; // no filter was applied.
		}
	}
}