// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;

namespace LanguageExplorer.Controls.DetailControls.Slices
{
	/// <summary />
	/// <remarks>
	/// A test derives from this class, so it can't be made sealed.
	/// </remarks>
	internal class LexReferenceTreeRootLauncher : AtomicReferenceLauncher
	{
		/// <summary />
		internal LexReferenceTreeRootLauncher()
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();
		}

		/// <summary />
		protected internal override ICmObject Target
		{
			get => (m_obj as ILexReference)?.TargetsRS[0];
			set
			{
				Debug.Assert(value != null);
				var lr = (ILexReference)m_obj;
				var lrt = (ILexRefType)lr.Owner;
				var objToLink = GetChildObject();
				// See if there is another existing relation of the same type with the desired 'whole'.
				var newRef = lrt.MembersOC.FirstOrDefault(lr1 => lr1.TargetsRS.Count > 0 && lr1.TargetsRS[0] == value);
				if (newRef == null)
				{
					// No existing relationship to join
					if (lr.TargetsRS.Count == 2)
					{
						// The first one is the old 'whole' of which this is a part.
						// The second is objToLink.
						// No other parts use the same relation, and there is no other relation with the new 'whole'.
						// Therefore, we can just modify the relation.
						Debug.Assert(lr.TargetsRS[1] == objToLink);
						// Do this in one step; if we delete first, LCM gets too smart and deletes the whole lexical relation.
						lr.TargetsRS[0] = value;
						return; // must not try to insert into newRef.
					}
					// There are other parts in the relation.
					// We need to remove our self from the relation and create a new one for the new relationship.
					lr.TargetsRS.Remove(objToLink);
					newRef = lrt.Services.GetInstance<ILexReferenceFactory>().Create();
					lrt.MembersOC.Add(newRef);
					newRef.TargetsRS.Add(value); // must be the first (root) item
				}
				else
				{
					if (lr == newRef)
					{
						// Silly user tried to replace a relation with itself, don't do anything! [LT-10987]
						return;
					}
					// There's a new relationship objToLink needs to join.
					if (lr.TargetsRS.Count == 2)
					{
						// We were the only 'part' of the old relationship; it is now meaningless and needs to go away
						lrt.MembersOC.Remove(lr);
					}
					else
					{
						// Just remove objToLink from the old relationship
						lr.TargetsRS.Remove(objToLink);
					}
				}
				// Whatever else happens we end up identifying or creating a new relationship which we need to join.
				newRef.TargetsRS.Add(objToLink);
			}
		}

		/// <summary>
		/// Get the object for which we want to change the root. For example, the first object in
		/// the lex reference which is our m_obj is the one that is in some relation (typically 'whole')
		/// to all the others (typically 'parts'). We need to figure out which part we are trying
		/// to associate with a different whole.
		/// This control is always configured as the child of a LexReferenceTreeRootSlice, which always
		/// has a ParentSlice whose Object is the one we want.
		/// </summary>
		/// <remarks>internal and virtual to support testing...otherwise would be private</remarks>
		protected virtual ICmObject GetChildObject()
		{
			LexReferenceTreeRootSlice owningSlice = null;
			for (var parent = Parent; parent != null && owningSlice == null; parent = parent.Parent)
			{
				owningSlice = parent as LexReferenceTreeRootSlice;
			}
			return owningSlice == null ? throw new FwConfigurationException("LexReferenceTreeRootLauncher must be a child of a LexReferenceTreeRootSlice") : owningSlice.ParentSlice.MyCmObject;
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
					case LexRefTypeTags.MappingTypes.kmtSenseTree:
						dlg = new LinkEntryOrSenseDlg();
						((LinkEntryOrSenseDlg)dlg).SelectSensesOnly = true;
						break;
					case LexRefTypeTags.MappingTypes.kmtEntryTree:
						dlg = new EntryGoDlg();
						break;
					case LexRefTypeTags.MappingTypes.kmtEntryOrSenseTree:
						dlg = new LinkEntryOrSenseDlg();
						break;
				}
				dlg.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
				var wp = new WindowParams
				{
					m_title = string.Format(LanguageExplorerControls.ksReplaceXEntry),
					m_btnText = LanguageExplorerControls.ks_Replace
				};
				//This method is only called when we are Replacing the
				//tree root of a Whole/Part lexical relation
				dlg.SetDlgInfo(m_cache, wp);
				dlg.SetHelpTopic("khtpChooseLexicalRelationAdd");
				if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
				{
					if (dlg.SelectedObject != null)
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
			AddItem(obj, Target == null ? LanguageExplorerResources.ksUndoAddRef : LanguageExplorerControls.ksUndoReplaceRef, Target == null ? LanguageExplorerResources.ksRedoAddRef : LanguageExplorerControls.ksRedoReplaceRef);
		}

		#region Component Designer generated code
		/// <summary>
		/// Everything except the Name is taken care of by the Superclass.
		/// </summary>
		private void InitializeComponent()
		{
			this.Name = "LexReferenceTreeRootLauncher";
		}
		#endregion

		/// <summary />
		protected override AtomicReferenceView CreateAtomicReferenceView()
		{
			return new LexReferenceTreeRootView();
		}
	}
}