// Copyright (c) 2008-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

namespace LanguageExplorer.Controls.DetailControls
{
	/// <summary>
	/// This interface is an extra one that must be implemented by subclasses of IRootSite.
	/// The typical implementation of SetRoot is to call SetRootObject on the
	/// RootBox, passing also your standard view constructor and other arguments.
	/// </summary>
	internal interface IChangeRootObject
	{
		/// <summary />
		void SetRoot(int hvo);
	}
}