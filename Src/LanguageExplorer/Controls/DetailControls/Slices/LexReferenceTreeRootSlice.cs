// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using SIL.LCModel;

namespace LanguageExplorer.Controls.DetailControls.Slices
{
	/// <summary>
	/// LexReferenceTreeRootSlice is used to support selecting
	/// of a Sense or Entry tree.
	/// </summary>
	internal sealed class LexReferenceTreeRootSlice : CustomAtomicReferenceSlice, ILexReferenceSlice
	{
		/// <summary />
		internal LexReferenceTreeRootSlice()
			: base(new LexReferenceTreeRootLauncher())
		{
		}

		public override bool HandleDeleteCommand()
		{
			((LexReferenceMultiSlice)ParentSlice).DeleteFromReference(GetObjectForMenusToOperateOn() as ILexReference);
			return true; // delete was done
		}

		/// <summary />
		public override void HandleLaunchChooser()
		{
			((LexReferenceTreeRootLauncher)Control).LaunchChooser();
		}

		/// <summary />
		public override void HandleEditCommand()
		{
			((LexReferenceMultiSlice)ParentSlice).EditReferenceDetails(GetObjectForMenusToOperateOn() as ILexReference);
		}
	}
}