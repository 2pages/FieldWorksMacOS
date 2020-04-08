// Copyright (c) 2012-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Drawing;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;

namespace LanguageExplorer.Controls.DetailControls.Slices
{
	/// <summary>
	/// This class shows the POS slice as being disabled.
	/// </summary>
	internal sealed class AtomicReferencePOSDisabledSlice : AtomicReferencePOSSlice
	{
		/// <summary />
		internal AtomicReferencePOSDisabledSlice(LcmCache cache, ICmObject obj, int flid, FlexComponentParameters flexComponentParameters)
			: base(cache, obj, flid, flexComponentParameters)
		{
			if (Tree != null)
			{
				Tree.ForeColor = SystemColors.GrayText;
			}
		}
	}
}