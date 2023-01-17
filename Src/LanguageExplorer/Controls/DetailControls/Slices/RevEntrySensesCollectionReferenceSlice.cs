// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

namespace LanguageExplorer.Controls.DetailControls.Slices
{
	/// <summary>
	/// Provides a class that uses the Go dlg for selecting entries for subentries.
	/// </summary>
	internal sealed class RevEntrySensesCollectionReferenceSlice : CustomReferenceVectorSlice
	{
		/// <summary />
		internal RevEntrySensesCollectionReferenceSlice()
			: base(new RevEntrySensesCollectionReferenceLauncher())
		{
		}
	}
}