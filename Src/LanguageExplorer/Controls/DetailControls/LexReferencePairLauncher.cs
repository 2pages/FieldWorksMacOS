// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;

namespace LanguageExplorer.Controls.DetailControls
{
	/// <summary />
	internal sealed class LexReferencePairLauncher : AtomicReferenceLauncher
	{
		/// <summary />
		private ICmObject m_displayParent;

		/// <summary />
		internal LexReferencePairLauncher()
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();
		}

		/// <summary />
		protected internal override ICmObject Target
		{
			get
			{
				var lr = (ILexReference)m_obj;
				if (lr.TargetsRS.Count < 2)
				{
					return null;
				}
				var target = lr.TargetsRS[0];
				if (target == m_displayParent)
				{
					target = lr.TargetsRS[1];
				}
				return target;
			}
			set
			{
				Debug.Assert(value != null);

				var index = 0;
				var lr = (ILexReference)m_obj;
				var item = lr.TargetsRS[0];
				if (item == m_displayParent)
				{
					index = 1;
				}
				// LT-13729: Remove old and then Insert new might cause the deletion of the lr, then the insert fails.
				lr.TargetsRS.Replace(index, (index < lr.TargetsRS.Count) ? 1 : 0, new List<ICmObject>() { value });
			}
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
			var type = (LexRefTypeTags.MappingTypes)lrt.MappingType;
			BaseGoDlg dlg = null;
			try
			{
				switch (type)
				{
					case LexRefTypeTags.MappingTypes.kmtSensePair:
					case LexRefTypeTags.MappingTypes.kmtSenseAsymmetricPair: // Sense pair with different Forward/Reverse names
						dlg = new LinkEntryOrSenseDlg();
						((LinkEntryOrSenseDlg)dlg).SelectSensesOnly = true;
						break;
					case LexRefTypeTags.MappingTypes.kmtEntryPair:
					case LexRefTypeTags.MappingTypes.kmtEntryAsymmetricPair: // Entry pair with different Forward/Reverse names
						dlg = new EntryGoDlg();
						break;
					case LexRefTypeTags.MappingTypes.kmtEntryOrSensePair:
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseAsymmetricPair: // Entry or sense pair with different Forward/Reverse
						dlg = new LinkEntryOrSenseDlg();
						((LinkEntryOrSenseDlg)dlg).SelectSensesOnly = false;
						break;
				}
				dlg.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
				Debug.Assert(dlg != null);
				var wp = new WindowParams();
				//on creating Pair Lexical Relation have an Add button and Add in the title bar
				if (Target == null)
				{
					wp.m_title = string.Format(LanguageExplorerControls.ksIdentifyXEntry, lrt.Name.BestAnalysisAlternative.Text);
					wp.m_btnText = LanguageExplorerResources.ks_Add;
				}
				else
				{
					//Otherwise we are Replacing the item
					wp.m_title = string.Format(LanguageExplorerControls.ksReplaceXEntry);
					wp.m_btnText = LanguageExplorerControls.ks_Replace;
				}

				dlg.SetDlgInfo(m_cache, wp);
				dlg.SetHelpTopic("khtpChooseLexicalRelationAdd");
				if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
				{
					return;
				}
				if (dlg.SelectedObject != null)
				{
					AddItem(dlg.SelectedObject);
					// it is possible that the previous update has caused the data tree to refresh
					if (!IsDisposed)
					{
						m_atomicRefView.RootBox.Reconstruct(); // view is somehow too complex for auto-update.
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
			AddItem(obj, Target == null ? LanguageExplorerResources.ksUndoAddRef : LanguageExplorerControls.ksUndoReplaceRef, Target == null ? LanguageExplorerResources.ksRedoAddRef : LanguageExplorerControls.ksRedoReplaceRef);
		}

		#region Component Designer generated code
		/// <summary>
		/// Everything except the Name is taken care of by the Superclass.
		/// </summary>
		private void InitializeComponent()
		{
			this.Name = "LexReferencePairLauncher";
		}
		#endregion

		/// <summary />
		protected override AtomicReferenceView CreateAtomicReferenceView()
		{
			var pv = new LexReferencePairView();
			if (m_displayParent != null)
			{
				pv.DisplayParent = m_displayParent;
			}
			return pv;
		}

		/// <summary />
		public ICmObject DisplayParent
		{
			set
			{
				m_displayParent = value;
				if (m_atomicRefView != null)
				{
					((LexReferencePairView)m_atomicRefView).DisplayParent = value;
				}
			}
		}
	}
}