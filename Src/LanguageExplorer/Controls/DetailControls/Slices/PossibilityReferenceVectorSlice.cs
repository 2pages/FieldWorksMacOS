// Copyright (c) 2012-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Windows.Forms;
using SIL.LCModel;
using SIL.Xml;

namespace LanguageExplorer.Controls.DetailControls.Slices
{
	internal class PossibilityReferenceVectorSlice : ReferenceVectorSlice
	{
		protected PossibilityReferenceVectorSlice(Control control, LcmCache cache, ICmObject obj, int flid)
			: base(control, cache, obj, flid)
		{
		}

		internal PossibilityReferenceVectorSlice(LcmCache cache, ICmObject obj, int flid)
			: base(new PossibilityVectorReferenceLauncher(), cache, obj, flid)
		{
		}

		protected override string BestWsName
		{
			get
			{
				var list = (ICmPossibilityList)MyCmObject.ReferenceTargetOwner(m_flid);
				var parameters = ConfigurationNode.Element("deParams");
				return parameters == null ? list.IsVernacular ? "best vernoranal" : "best analorvern" : XmlUtils.GetOptionalAttributeValue(parameters, "ws", list.IsVernacular ? "best vernoranal" : "best analorvern");
			}
		}

		public override void FinishInit()
		{
			SetFieldFromConfig();
			base.FinishInit();
		}
	}
}