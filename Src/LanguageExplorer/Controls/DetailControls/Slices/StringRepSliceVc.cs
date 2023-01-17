// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using SIL.FieldWorks.Common.RootSites;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;

namespace LanguageExplorer.Controls.DetailControls.Slices
{
	internal sealed class StringRepSliceVc : FwBaseVc
	{
		internal static int Flid => PhEnvironmentTags.kflidStringRepresentation;

		public override void Display(IVwEnv vwenv, int hvo, int frag)
		{
			vwenv.AddStringProp(Flid, this);
		}
	}
}