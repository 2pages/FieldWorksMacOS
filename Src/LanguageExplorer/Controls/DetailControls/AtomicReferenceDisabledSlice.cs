// Copyright (c) 2003-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using SIL.LCModel;

namespace LanguageExplorer.Controls.DetailControls
{
	internal class AtomicReferenceDisabledSlice : AtomicReferenceSlice
	{
		public AtomicReferenceDisabledSlice(LcmCache cache, ICmObject obj, int flid)
			: base(cache, obj, flid)
		{
		}
		public override void FinishInit()
		{
			base.FinishInit();
			((AtomicReferenceView)((AtomicReferenceLauncher)Control).MainControl).FinishInit(ConfigurationNode);
		}
	}
}