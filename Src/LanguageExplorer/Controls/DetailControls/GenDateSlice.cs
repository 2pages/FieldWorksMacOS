// Copyright (c) 2009-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.Xml;

namespace LanguageExplorer.Controls.DetailControls
{
	/// <summary>
	/// This is a generic date slice.
	/// </summary>
	internal class GenDateSlice : FieldSlice
	{
		internal GenDateSlice(LcmCache cache, ICmObject obj, int flid)
			: base(new GenDateLauncher(), cache, obj, flid)
		{
		}

		public override void FinishInit()
		{
			base.FinishInit();
			// have chooser title use the same text as the label
			m_fieldName = StringTable.Table.LocalizeAttributeValue(XmlUtils.GetOptionalAttributeValue(ConfigurationNode, "label", m_fieldName));
			((GenDateLauncher)Control).Initialize(Cache, MyCmObject, m_flid, m_fieldName, PersistenceProvider, "", "analysis");
		}

		protected override void UpdateDisplayFromDatabase()
		{
			((GenDateLauncher)Control).UpdateDisplayFromDatabase();
		}
	}
}