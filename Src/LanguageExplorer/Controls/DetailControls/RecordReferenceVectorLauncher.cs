// Copyright (c) 2009-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Linq;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;

namespace LanguageExplorer.Controls.DetailControls
{
	/// <summary />
	internal sealed class RecordReferenceVectorLauncher : VectorReferenceLauncher
	{
		/// <summary />
		protected override void HandleChooser()
		{
			using (var dlg = new RecordGoDlg())
			{
				dlg.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
				var wp = new WindowParams
				{
					m_title = LanguageExplorerResources.ksIdentifyRecord,
					m_btnText = LanguageExplorerResources.ks_Add
				};
				dlg.SetDlgInfo(m_cache, wp);
				dlg.SetHelpTopic(Slice.GetChooserHelpTopicID());
				if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
				{
					AddItem(dlg.SelectedObject);
				}
			}
		}

		/// <summary />
		public override void AddItem(ICmObject obj)
		{
			if (!Targets.Contains(obj))
			{
				AddItem(obj, LanguageExplorerResources.ksUndoAddRef, LanguageExplorerResources.ksRedoAddRef);
			}
		}
	}
}