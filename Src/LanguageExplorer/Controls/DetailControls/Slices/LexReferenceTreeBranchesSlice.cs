// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using SIL.LCModel;

namespace LanguageExplorer.Controls.DetailControls.Slices
{
	/// <summary />
	internal sealed class LexReferenceTreeBranchesSlice : CustomReferenceVectorSlice, ILexReferenceSlice
	{
		/// <summary />
		internal LexReferenceTreeBranchesSlice()
			: base(new LexReferenceTreeBranchesLauncher())
		{
		}

		public override bool HandleDeleteCommand()
		{
			((LexReferenceMultiSlice)ParentSlice).DeleteReference(GetObjectForMenusToOperateOn() as ILexReference);
			return true; // delete was done
		}

		/// <summary />
		public override void HandleLaunchChooser()
		{
			((LexReferenceTreeBranchesLauncher)Control).LaunchChooser();
		}

		/// <summary />
		public override void HandleEditCommand()
		{
			((LexReferenceMultiSlice)ParentSlice).EditReferenceDetails(GetObjectForMenusToOperateOn() as ILexReference);
		}
	}
}