// Copyright (c) 2003-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Application;
using SIL.LCModel.Core.Cellar;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;
using SIL.Xml;

namespace LanguageExplorer.Controls.DetailControls
{
	/// <summary>
	/// Main class for displaying the VectorReferenceSlice.
	/// </summary>
	internal class VectorReferenceView : ReferenceViewBase
	{
		#region Constants and data members

		// View frags.
		public const int kfragTargetVector = 1;
		public const int kfragTargetObj = 2;
		protected VectorReferenceVc m_VectorReferenceVc;
		protected string m_displayWs;
		internal XElement ConfigurationNode { get; set; }
		private string m_textStyle;

		/// <summary>
		/// This allows the view to communicate size changes to the embedding slice.
		/// </summary>
		internal event FwViewSizeChangedEventHandler ViewSizeChanged;

		#endregion // Constants and data members

		#region Construction, initialization, and disposal

		public VectorReferenceView()
		{
			InitializeComponent();
		}

		public void Initialize(ICmObject rootObj, int rootFlid, string rootFieldName, LcmCache cache, string displayNameProperty, string displayWs)
		{
			m_displayWs = displayWs;
			Initialize(rootObj, rootFlid, rootFieldName, cache, displayNameProperty);
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}

			base.Dispose(disposing);

			if (disposing)
			{
			}
			m_VectorReferenceVc = null;
			m_rootObj = null;
			m_displayNameProperty = null;
		}

		/// <summary>
		/// Reload the vector in the root box, presumably after it's been modified by a chooser.
		/// </summary>
		public virtual void ReloadVector()
		{
			RootBox.SetRootObject(m_rootObj?.Hvo ?? 0, m_VectorReferenceVc, kfragTargetVector, RootBox.Stylesheet);
		}

		/// <summary>
		/// Get any text styles from configuration node (which is now available; it was not at construction)
		/// </summary>
		/// <param name="configurationNode"></param>
		public void FinishInit(XElement configurationNode)
		{
			var textStyle = configurationNode.Attribute("textStyle");
			if (textStyle == null)
			{
				return;
			}
			TextStyle = textStyle.Value;
			if (m_VectorReferenceVc != null)
			{
				m_VectorReferenceVc.TextStyle = textStyle.Value;
			}
		}

		#endregion // Construction, initialization, and disposal

		#region RootSite required methods

		public override void MakeRoot()
		{
			if (m_cache == null || DesignMode)
			{
				return;
			}
			m_VectorReferenceVc = CreateVectorReferenceVc();
			base.MakeRoot();
			RootBox.DataAccess = GetDataAccess();
			SetupRoot();
		}

		protected override void SetupRoot()
		{
			m_VectorReferenceVc.Reuse(m_rootFlid, m_displayNameProperty, m_displayWs);
			ReloadVector();
		}

		protected virtual VectorReferenceVc CreateVectorReferenceVc()
		{
			return new VectorReferenceVc(m_cache, m_rootFlid, m_displayNameProperty, m_displayWs);
		}

		protected virtual ISilDataAccess GetDataAccess()
		{
			return m_cache.DomainDataByFlid;
		}

		#endregion // RootSite required methods

		#region other overrides and related methods

		protected override void HandleSelectionChange(IVwRootBox rootb, IVwSelection vwselNew)
		{
			if (vwselNew == null)
			{
				return;
			}
			var cvsli = vwselNew.CLevels(false);
			// CLevels includes the string property itself, but AllTextSelInfo doesn't need it.
			cvsli--;
			if (cvsli == 0)
			{
				// No objects in selection: don't allow a selection.
				RootBox.DestroySelection();
				// Enhance: invoke launcher's selection dialog.
				return;
			}
			vwselNew.TextSelInfo(false, out _, out var ichAnchor, out _, out var hvoObj, out _, out _);
			vwselNew.TextSelInfo(true, out _, out var ichEnd, out _, out var hvoObjEnd, out _, out _);
			if (hvoObj != hvoObjEnd)
			{
				return;
			}
			var rgvsli = SelLevInfo.AllTextSelInfo(vwselNew, cvsli, out var ihvoRoot, out _, out _, out ichAnchor, out ichEnd, out _, out _, out _, out _);
			Debug.Assert(RootBox != null);
			// Create a selection that covers the entire target object.  If it differs from
			// the new selection, we'll install it (which will recurse back to this method).
			var vwselWhole = RootBox.MakeTextSelInObj(ihvoRoot, cvsli, rgvsli, 0, null, false, false, false, true, false);
			if (vwselWhole == null)
			{
				return;
			}
			vwselWhole.TextSelInfo(false, out _, out var ichAnchorWhole, out _, out var hvoObjWhole, out _, out _);
			vwselWhole.TextSelInfo(true, out _, out var ichEndWhole, out _, out var hvoObjEndWhole, out _, out _);
			if (hvoObj == hvoObjWhole && hvoObjEnd == hvoObjEndWhole && (ichAnchor != ichAnchorWhole || ichEnd != ichEndWhole))
			{
				// Install it this time!
				RootBox.MakeTextSelInObj(ihvoRoot, cvsli, rgvsli, 0, null, false, false, false, true, true);
			}
		}

		/// <summary>
		/// override this to allow deleting an item IF the key is Delete.
		/// </summary>
		protected override void OnKeyDown(KeyEventArgs e)
		{
			HandleKeyDown(e);
			if (!IsDisposed)        // Delete() can cause the view to be removed.
			{
				base.OnKeyDown(e);
			}
		}

		protected virtual void HandleKeyDown(KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.Delete:
					Delete();
					e.Handled = true;
					break;
				case Keys.Left:
					MoveItem(false);
					e.Handled = true;
					break;
				case Keys.Right:
					MoveItem(true);
					e.Handled = true;
					break;
			}
		}

		protected override void OnKeyPress(KeyPressEventArgs e)
		{
			HandleKeyPress(e);
			if (!IsDisposed)
			{
				base.OnKeyPress(e);
			}
		}

		protected virtual void HandleKeyPress(KeyPressEventArgs e)
		{
			if (e.KeyChar != (char)Keys.Back)
			{
				return;
			}
			Delete();
			e.Handled = true;
		}

		internal bool CanMoveItem(bool forward, out bool visible)
		{
			return PrepareForMoveItem(forward, out visible, out _, out _);
		}

		internal void MoveItem(bool forward)
		{
			if (!PrepareForMoveItem(forward, out _, out var ihvo, out var vals))
			{
				return;
			}
			UndoableUnitOfWorkHelper.Do(DetailControlsStrings.ksUndoReorder, DetailControlsStrings.ksRedoReorder, Cache.ActionHandlerAccessor,
				() => ReorderItems(vals));
			// Create a new selection of the moved object.
			var rgvsli = new SelLevInfo[1];
			rgvsli[0].ihvo = ihvo;
			rgvsli[0].tag = m_rootFlid;
			RootBox.MakeTextSelInObj(0, 1, rgvsli, 0, null, false, false, false, true, true);
		}

		private void ReorderItems(List<ICmObject> vals)
		{
			if (RootPropertyIsRealRefSequence())
			{
				// Since we are re-ordering, presume all objects are replaced by the entire new value.
				Cache.DomainDataByFlid.Replace(m_rootObj.Hvo, m_rootFlid, 0, vals.Count, vals.Select(obj => obj.Hvo).ToArray(), vals.Count);
			}
			else
			{
				VirtualOrderingServices.SetVO(m_rootObj, m_rootFlid, vals);
			}
		}

		internal void RemoveOrdering()
		{
			UndoableUnitOfWorkHelper.Do(DetailControlsStrings.ksUndoAlphabeticalOrder, DetailControlsStrings.ksRedoAlphabeticalOrder, Cache.ActionHandlerAccessor,
				() => VirtualOrderingServices.ResetVO(m_rootObj, m_rootFlid));
		}

		/// <summary>
		/// This method will determine if the Item can be moved, if it can all the out variables will be set in preparation
		/// for use by MoveItem
		/// </summary>
		/// <param name="forward"></param>
		/// <param name="visible">Used to indicate if a move is even legal</param>
		/// <param name="ihvo">returns the index of the selected object? Not sure this works</param>
		/// <param name="vals">The new ordering of the items in the vector after the requested move</param>
		/// <returns>true if the Move could be accomplished</returns>
		private bool PrepareForMoveItem(bool forward, out bool visible, out int ihvo, out List<ICmObject> vals)
		{
			ihvo = 0; // default
			vals = null; // default
			visible = false;
			// Fundamentally, we can handle either reference sequence properties or ones we are explicitly told
			// to create VirtualPropOrderings for.
			// Some slices disabled (LT-18266, LT-20512)
			if (!RootPropertyIsRealRefSequence() && !RootPropertySupportsVirtualOrdering() ||
				m_rootFieldName == "Complex Forms" || m_rootFieldName == "Compare" ||
				GetType().Name == "LexReferenceCollectionView" || GetType().Name == "LexReferenceTreeBranchesView")
			{
				return false;
			}

			visible = true; // Command makes sense even if we can't actually do it now.
			if (RootBox.Selection == null)
			{
				return false;
			}
			var cvsli = RootBox.Selection.CLevels(false);
			// CLevels includes the string property itself, but AllTextSelInfo doesn't need it.
			cvsli--;
			if (cvsli <= 0)
			{
				// No object in selection, so quit.
				return false;
			}
			//the index (ihvo) set by this method is the index of the visible items, any logic using this index must also deal with only the visible items
			RootBox.Selection.PropInfo(false, cvsli, out var hvoObj, out var flid, out ihvo, out _, out _);
			Debug.Assert(hvoObj == m_rootObj.Hvo);
			Debug.Assert(flid == m_rootFlid);
			//set vals to the visible items
			var visibleItems = GetVisibleItemList();
			vals = visibleItems;
			var move = visibleItems[ihvo];
			visibleItems.RemoveAt(ihvo);
			if (forward)
			{
				if (ihvo + 1 > visibleItems.Count)
				{
					return false;
				}
				ihvo++;
			}
			else
			{
				if (ihvo == 0)
				{
					return false;
				}
				ihvo--;
			}
			visibleItems.Insert(ihvo, move);
			AddHiddenItems(visibleItems);
			return true;
		}

		/// <summary>
		/// The default AddHiddenItems appends hidden items to the end.
		/// </summary>
		protected virtual void AddHiddenItems(List<ICmObject> items)
		{
			items.AddRange(GetHiddenItemList()); //add the parent (invisible) reference and any other invisibles back into the collection
		}

		/// <summary>
		/// This method will return the list of items in this vector which are visible to the user, the base class version returns all items.
		/// </summary>
		protected virtual List<ICmObject> GetVisibleItemList()
		{
			var objRepo = Cache.ServiceLocator.ObjectRepository;
			return (RootBox.DataAccess as ISilDataAccessManaged)?.VecProp(m_rootObj.Hvo, m_rootFlid).Where(i => objRepo.GetObject(i) != null).Select(i => objRepo.GetObject(i)).ToList();
		}
		/// <summary>
		/// This method will return the list of items in this vector which are hidden from the user, the base class version returns an empty list.
		/// </summary>
		protected virtual List<ICmObject> GetHiddenItemList()
		{
			return new List<ICmObject>();
		}

		internal bool RootPropertySupportsVirtualOrdering()
		{
			return XmlUtils.GetOptionalBooleanAttributeValue(ConfigurationNode, "reorder", false);
		}

		/// <summary>
		/// Return true if the property is a non-virtual reference sequence.
		/// </summary>
		private bool RootPropertyIsRealRefSequence()
		{
			if (Cache.MetaDataCacheAccessor.GetFieldType(m_rootFlid) != (int)CellarPropertyType.ReferenceSequence)
			{
				return false;
			}
			return !Cache.MetaDataCacheAccessor.get_IsVirtual(m_rootFlid);
		}

		protected virtual void Delete()
		{
			Delete(string.Format(DetailControlsStrings.ksUndoDeleteItem, m_rootFieldName), string.Format(DetailControlsStrings.ksRedoDeleteItem, m_rootFieldName));
		}

		protected void Delete(string undoText, string redoText)
		{
			var sel = RootBox.Selection;
			if (CheckForValidDelete(sel, out var cvsli, out var hvoObj))
			{
				DeleteObjectFromVector(sel, cvsli, hvoObj, undoText, redoText);
			}
		}

		protected void DeleteObjectFromVector(IVwSelection sel, int cvsli, int hvoObj, string undoText, string redoText)
		{
			var hvoObjEnd = hvoObj;
			var rgvsli = SelLevInfo.AllTextSelInfo(sel, cvsli, out var ihvoRoot, out _, out _, out var ichAnchor, out var ichEnd, out _, out _, out _, out _);
			Debug.Assert(RootBox != null);
			// Create a selection that covers the entire target object.  If it differs from
			// the new selection, we'll install it (which will recurse back to this method).
			var vwselWhole = RootBox.MakeTextSelInObj(ihvoRoot, cvsli, rgvsli, 0, null, false, false, false, true, false);
			if (vwselWhole == null)
			{
				return;
			}
			vwselWhole.TextSelInfo(false, out _, out var ichAnchorWhole, out _, out var hvoObjWhole, out _, out _);
			vwselWhole.TextSelInfo(true, out _, out var ichEndWhole, out _, out var hvoObjEndWhole, out _, out _);
			if (hvoObj != hvoObjWhole || hvoObjEnd != hvoObjEndWhole || ichAnchor != ichAnchorWhole || ichEnd != ichEndWhole)
			{
				return;
			}
			// We've selected the whole string for it, so remove the object from the vector.
			var hvosOld = m_cache.GetManagedSilDataAccess().VecProp(m_rootObj.Hvo, m_rootFlid);
			UpdateTimeStampsIfNeeded(hvosOld);
			for (var i = 0; i < hvosOld.Length; ++i)
			{
				if (hvosOld[i] == hvoObj)
				{
					RemoveObjectFromList(hvosOld, i, undoText, redoText);
					break;
				}
			}
		}

		/// <summary>
		/// When deleting from a LexReference, all the affected LexEntry objects need to
		/// have their timestamps updated.  (See LT-5523.)  Most of the time, this operation
		/// does nothing.
		/// </summary>
		protected virtual void UpdateTimeStampsIfNeeded(int[] hvos)
		{
		}

		protected bool CheckForValidDelete(IVwSelection sel, out int cvsli, out int hvoObj)
		{
			hvoObj = 0;
			if (sel == null)
			{
				cvsli = 0; // satisfy compiler.
				return false; // nothing selected, give up.
			}
			cvsli = sel.CLevels(false);
			// CLevels includes the string property itself, but AllTextSelInfo doesn't need it.
			cvsli--;
			if (cvsli == 0)
			{
				// No object in selection, so quit.
				return false;
			}
			sel.TextSelInfo(false, out _, out _, out _, out hvoObj, out _, out _);
			sel.TextSelInfo(true, out _, out _, out _, out var hvoObjEnd, out _, out _);
			return (hvoObj == hvoObjEnd);
		}

		/// <summary>
		/// Remove the indicated object from the list, if possible.
		/// </summary>
		protected virtual void RemoveObjectFromList(int[] hvosOld, int ihvo, string undoText, string redoText)
		{
			if (Cache.MetaDataCacheAccessor.get_IsVirtual(m_rootFlid))
			{
				// debug run to see what context variable content is available for the following:
				// which virtual property is this? (like PublishIn)
				//    get real_prop_name (like DoNotPublishIn) from  part/slice/@visField
				var field = XmlUtils.GetOptionalAttributeValue(ConfigurationNode, "field");
				var visField = XmlUtils.GetOptionalAttributeValue(ConfigurationNode, "visField");
				//    get its real property object via its name (like DoNotPublishIn)
				if (visField != null)
				{
					// remove the item from the virtual list property - thus adding it to the real property
					RemoveObjectFromEditableList(ihvo, undoText, redoText);
				}
				return;
			}
			RemoveObjectFromEditableList(ihvo, undoText, redoText);
		}

		/// <summary>
		/// Remove the indicated object from the editable list which can be virtual (like LcmInvertSet).
		/// </summary>
		private void RemoveObjectFromEditableList(int ihvo, string undoText, string redoText)
		{
			var startHeight = 0;
			if (RootBox != null)
			{
				startHeight = RootBox.Height;
			}
			UndoableUnitOfWorkHelper.Do(undoText, redoText, m_rootObj, () => m_cache.DomainDataByFlid.Replace(m_rootObj.Hvo, m_rootFlid, ihvo, ihvo + 1, new int[0], 0));
			if (RootBox != null)
			{
				CheckViewSizeChanged(startHeight, RootBox.Height);
				// Redisplay (?) the vector property.
				RootBox.SetRootObject(m_rootObj.Hvo, m_VectorReferenceVc, kfragTargetVector, RootBox.Stylesheet);
			}
		}

		/// <summary>
		/// Update the root object. This is currently used when one is created, so it doesn't need to handle null object.
		/// </summary>
		internal void UpdateRootObject(ICmObject root)
		{
			m_rootObj = root;
			RootBox.SetRootObject(m_rootObj.Hvo, m_VectorReferenceVc, kfragTargetVector, RootBox.Stylesheet);
		}

		protected void CheckViewSizeChanged(int startHeight, int endHeight)
		{
			if (startHeight != endHeight)
			{
				ViewSizeChanged?.Invoke(this, new FwViewSizeEventArgs(endHeight, RootBox.Width));
			}
		}

		#endregion

		#region Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			//
			// VectorReferenceView
			//
			this.Name = "VectorReferenceView";
			this.Size = new System.Drawing.Size(232, 40);

		}
		#endregion

		public ICmObject SelectedObject
		{
			get
			{
				var sel = RootBox.Selection;
				if (sel == null)
				{
					return null; // nothing selected, give up.
				}
				var cvsli = sel.CLevels(false);
				// CLevels includes the string property itself, but AllTextSelInfo doesn't need it.
				cvsli--;
				if (cvsli == 0)
				{
					// No object in selection, so quit.
					return null;
				}
				sel.TextSelInfo(false, out _, out _, out _, out var hvoObj, out _, out _);
				return m_cache.ServiceLocator.IsValidObjectId(hvoObj) ? m_cache.ServiceLocator.GetObject(hvoObj) : null;
			}

			set
			{
				if (value == null)
				{
					RootBox.MakeSimpleSel(true, true, true, true);
				}
				else
				{
					var count = m_cache.DomainDataByFlid.get_VecSize(m_rootObj.Hvo, m_rootFlid);
					int i;
					for (i = 0; i < count; ++i)
					{
						var hvo = m_cache.DomainDataByFlid.get_VecItem(m_rootObj.Hvo, m_rootFlid, i);
						if (hvo == value.Hvo)
						{
							break;
						}
					}
					var levels = new SelLevInfo[1];
					levels[0].ihvo = i;
					levels[0].tag = m_rootFlid;
					RootBox.MakeTextSelInObj(0, levels.Length, levels, 0, null, true, true, true, true, true);
				}
			}
		}
	}
}