// Copyright (c) 2016-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Diagnostics;
using System.Windows.Forms;
using LanguageExplorer.Controls;
using LanguageExplorer.Controls.DetailControls;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;

namespace LanguageExplorer.Areas.Lexicon
{
	/// <summary />
	internal class LexReferenceUnidirectionalLauncher : VectorReferenceLauncher
	{
		public LexReferenceUnidirectionalLauncher()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Wrapper for HandleChooser() to make it available to the slice.
		/// </summary>
		internal void LaunchChooser()
		{
			HandleChooser();
		}

		/// <summary>
		/// Override method to handle launching of a chooser for selecting lexical entries.
		/// </summary>
		protected override void HandleChooser()
		{
			var lrt = (ILexRefType)m_obj.Owner;
			var type = lrt.MappingType;
			BaseGoDlg dlg = null;
			try
			{
				switch ((LexRefTypeTags.MappingTypes)type)
				{
					case LexRefTypeTags.MappingTypes.kmtSenseUnidirectional:
						dlg = new LinkEntryOrSenseDlg();
						((LinkEntryOrSenseDlg)dlg).SelectSensesOnly = true;
						break;
					case LexRefTypeTags.MappingTypes.kmtEntryUnidirectional:
						dlg = new EntryGoDlg();
						break;
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseUnidirectional:
						dlg = new LinkEntryOrSenseDlg();
						break;
				}
				dlg.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
				Debug.Assert(dlg != null);
				var wp = new WindowParams
				{
					m_title = string.Format(LexiconResources.ksIdentifyXEntry, lrt.Name.BestAnalysisAlternative.Text), m_btnText = LanguageExplorerResources.ks_Add
				};
				dlg.SetDlgInfo(m_cache, wp);
				dlg.SetHelpTopic("khtpChooseLexicalRelationAdd");
				if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
				{
					if (!((ILexReference)m_obj).TargetsRS.Contains(dlg.SelectedObject))
					{
						AddItem(dlg.SelectedObject);
					}
				}
			}
			finally
			{
				dlg?.Dispose();
			}
		}

		/// <summary />
		public override void AddItem(ICmObject obj)
		{
			AddItem(obj, LanguageExplorerResources.ksUndoAddRef, LanguageExplorerResources.ksRedoAddRef);
		}

		/// <summary />
		protected override VectorReferenceView CreateVectorReferenceView()
		{
			return new LexReferenceUnidirectionalView();
		}

		#region Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.Name = "LexReferenceUnidirectionalLauncher";
		}
		#endregion
	}
}