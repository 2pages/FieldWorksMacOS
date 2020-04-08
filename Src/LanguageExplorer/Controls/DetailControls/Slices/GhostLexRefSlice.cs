// Copyright (c) 2009-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using SIL.FieldWorks.Common.FwUtils;

namespace LanguageExplorer.Controls.DetailControls.Slices
{
	/// <summary>
	/// A slice that is used for the ghost "Components" and "Variant of" fields of ILexEntry.
	/// It tries to behave like an empty list of components, but in fact the only real content is
	/// the chooser button, which behaves much like the one in an EntrySequenceReferenceSlice
	/// as used to display the ComponentLexemes of a LexEntryRef, except the list is always
	/// empty and hence not really there.
	/// </summary>
	internal sealed class GhostLexRefSlice : Slice
	{
		/// <summary />
		public override void FinishInit()
		{
			var btnLauncher = new GhostLexRefLauncher(MyCmObject, ConfigurationNode);
			btnLauncher.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
			Control = btnLauncher;

		}

		/// <summary />
		public override void Install(DataTree parentDataTree)
		{
			// JohnT: This is an awful way to make the button fit neatly, but I can't find a better one.
			Control.Height = Height;
			// It doesn't need most of the usual info, but the Mediator is important if the user
			// asks to Create a new lex entry from inside the first dialog (LT-9679).
			// We'd pass 0 and null for flid and fieldname, but there are Asserts to prevent this.
			var btnLauncher = (ButtonLauncher)Control;
			btnLauncher.Initialize(Cache, MyCmObject, 1, "nonsence", null, null, null);
			base.Install(parentDataTree);
		}
	}
}
