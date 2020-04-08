// Copyright (c) 2016-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using SIL.LCModel;

namespace LanguageExplorer.Controls.DetailControls.Slices
{
	/// <summary />
	internal sealed class LexReferenceUnidirectionalSlice : CustomReferenceVectorSlice, ILexReferenceSlice
	{
		/// <summary />
		internal LexReferenceUnidirectionalSlice()
			: base(new LexReferenceUnidirectionalLauncher())
		{
		}

		public override bool HandleDeleteCommand()
		{
			((LexReferenceMultiSlice)ParentSlice).DeleteReference(GetObjectForMenusToOperateOn() as ILexReference);
			return true; // delete was done
		}

		public override void HandleLaunchChooser()
		{
			((LexReferenceUnidirectionalLauncher)Control).LaunchChooser();
		}

		public override void HandleEditCommand()
		{
			((LexReferenceMultiSlice)ParentSlice).EditReferenceDetails(GetObjectForMenusToOperateOn() as ILexReference);
		}
	}
}