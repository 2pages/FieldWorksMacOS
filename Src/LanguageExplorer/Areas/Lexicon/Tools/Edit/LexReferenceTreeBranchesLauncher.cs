// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Windows.Forms;
using LanguageExplorer.Controls.DetailControls;
using LanguageExplorer.Controls.LexText;
using SIL.LCModel;

namespace LanguageExplorer.Areas.Lexicon.Tools.Edit
{
	/// <summary>
	/// Summary description for LexReferenceTreeBranchesLauncher.
	/// </summary>
	internal sealed class LexReferenceTreeBranchesLauncher : VectorReferenceLauncher
	{
		/// <summary />
		public LexReferenceTreeBranchesLauncher()
		{
			// This call is required by the Windows Form Designer.
			InitializeComponent();
		}

		/// <summary>
		/// Wrapper for HandleChooser() to make it available to the slice.
		/// </summary>
		internal void LaunchChooser()
		{
			CheckDisposed();

			HandleChooser();
		}

		/// <summary>
		/// Override method to handle launching of a chooser for selecting lexical entries.
		/// </summary>
		protected override void HandleChooser()
		{
			ILexRefType lrt = (ILexRefType)m_obj.Owner;
			int type = lrt.MappingType;
			BaseGoDlg dlg = null;
			try
			{
				switch ((LexRefTypeTags.MappingTypes)type)
				{
					case LexRefTypeTags.MappingTypes.kmtSenseTree:
						dlg = new LinkEntryOrSenseDlg();
						(dlg as LinkEntryOrSenseDlg).SelectSensesOnly = true;
						break;
					case LexRefTypeTags.MappingTypes.kmtEntryTree:
						dlg = new EntryGoDlg();
						break;
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseTree:
						dlg = new LinkEntryOrSenseDlg();
						break;
				}
				Debug.Assert(dlg != null);
				var wp = new WindowParams
				{
					m_title = String.Format(LanguageExplorerResources.ksIdentifyXEntry,
					lrt.Name.BestAnalysisAlternative.Text),
					m_btnText = LanguageExplorerResources.ks_Add
				};
				dlg.SetDlgInfo(m_cache, wp, PropertyTable, Publisher, Subscriber);
				dlg.SetHelpTopic("khtpChooseLexicalRelationAdd");
				if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
				{
					if (!(m_obj as ILexReference).TargetsRS.Contains(dlg.SelectedObject))
						AddItem(dlg.SelectedObject);
				}
			}
			finally
			{
				if (dlg != null)
					dlg.Dispose();
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
			return new LexReferenceTreeBranchesView();
		}

		#region Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.Name = "LexReferenceTreeBranchesLauncher";
		}
		#endregion
	}
}
