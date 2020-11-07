// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.RootSites;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Application;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.Xml;

namespace LanguageExplorer.Controls.XMLViews
{
	/// <summary>
	/// Control to show rows of data
	/// </summary>
	internal class XmlBrowseViewBase : RootSite, IVwNotifyChange, IPostLayoutInit, IClearValues
	{
		#region Events

		/// <summary>
		/// This event notifies you that the selected object changed, passing an argument from which you can
		/// directly obtain the new object. If you care more about the position of the object in the list
		/// (especially if the list may contain duplicates), you may wish to use the SelectedIndexChanged
		/// event instead. This SelectionChangedEvent will not fire if the selection moves from one
		/// occurrence of an object to another occurrence of the same object.
		/// </summary>
		public event FwSelectionChangedEventHandler SelectionChangedEvent;
		/// <summary>
		/// This event notifies you that the selected index changed. You can find the current index from
		/// the SelectedIndex property, and look up the object if needed...but if you mainly care about
		/// the object, it is probably better to use SelectionChangedEvent.
		/// </summary>
		public event EventHandler SelectedIndexChanged;

		#endregion Events

		#region Data members

		/// <summary />
		protected XmlBrowseViewVc m_xbvvc;
		/// <summary />
		protected XElement _configParamsElement;
		/// <summary />
		protected internal BrowseViewer m_bv;
		/// <summary> record list supplying browse view content </summary>
		protected ISortItemProvider m_sortItemProvider;
		/// <summary />
		protected int m_hvoOldSel;
		/// <summary />
		protected bool m_wantScrollIntoView = true;
		/// <summary />
		protected string m_id;
		/// <summary>
		/// index of selected row, initially none is selected.
		/// </summary>
		protected int m_selectedIndex = -1;
		/// <summary />
		protected SelectionHighlighting m_fSelectedRowHighlighting = SelectionHighlighting.border;
		/// <summary />
		private int _hvoRoot;
		/// <summary />
		private bool _rootObjectHasBeenSet;
		private bool _rootObjectHasBeenReset;
		/// <summary>
		/// see OnSaveScrollPosition
		/// </summary>
		protected int m_iTopOfScreenObjectForScrollPosition;
		/// <summary>
		/// see OnSaveScrollPosition
		/// </summary>
		protected int m_dyTopOfScreenOffset;
		/// <summary />
		protected int m_tagMe = XMLViewsDataCache.ktagTagMe;
		/// <summary />
		protected bool m_fHandlingMouseUp;
		private Point _scrollPosition = new Point(0, 0);
		private IContainer components;

		#endregion Data members

		#region Properties

		/// <summary />
		internal ISortItemProvider SortItemProvider => m_sortItemProvider;

		/// <summary>
		/// It's better to return our SDA directly rather than going to the root box, because occasionally
		/// when filtering or sorting we may need to obtain it before the root box is created.
		/// </summary>
		internal override ISilDataAccess DataAccess => SpecialCache;

		/// <summary>
		/// Return the VC. It has some important functions related to interpreting fragment IDs
		/// that the filter bar needs.
		/// </summary>
		internal virtual XmlBrowseViewVc Vc => m_xbvvc;

		/// <summary />
		public override bool RefreshDisplay()
		{
			var fChanged = Vc.RemoveInvalidColumns();
			if (fChanged)
			{
				m_bv.InstallNewColumns(Vc.ColumnSpecs);
			}
			base.RefreshDisplay();
			if (!Cache.ServiceLocator.IsValidObjectId(_hvoRoot))
			{
				_hvoRoot = 0;
				m_selectedIndex = -1;
			}
			var chvo = SpecialCache.get_VecSize(_hvoRoot, MainTag);
			if (m_selectedIndex >= chvo)
			{
				m_selectedIndex = chvo - 1;
			}
			if (m_selectedIndex >= 0)
			{
				var hvoNewObj = SpecialCache.get_VecItem(_hvoRoot, MainTag, m_selectedIndex);
				DoSelectAndScroll(hvoNewObj, m_selectedIndex);
			}
			//Enhance: if all the RefreshDisplay work has been done for all the descendants then return true here.
			return false;
		}

		/// <summary>
		/// Gets the HVO of the selected object.
		/// </summary>
		public int SelectedObject => SelectedIndex < 0 ? 0 : SpecialCache.get_VecSize(_hvoRoot, MainTag) <= SelectedIndex
				? 0 /* The only time this happens is during refresh. */
				: SpecialCache.get_VecItem(_hvoRoot, MainTag, SelectedIndex);

		/// <summary>
		/// Return the number of rows in the view.
		/// </summary>
		internal virtual int RowCount => SpecialCache.get_VecSize(_hvoRoot, MainTag);

		/// <summary>
		/// Return the index of the 'selected' row in the view.
		/// Returns -1 if nothing is selected, or there are no rows at all.
		/// If the selection spans multiple rows, returns the anchor row.
		/// If in select-only mode, there may be no selection, but it will then be the row
		/// last clicked.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Thrown on an attempt to set the index to less than -1, or more than the vector count.
		/// The new index can only be set to -1, IFF there are no elements in the vector.
		/// </exception>
		public int SelectedIndex
		{
			get => m_selectedIndex;
			set
			{
				if (_hvoRoot == 0)
				{
					// "_hvoRoot" can be 0 on startup.
					m_selectedIndex = -1;
					return;
				}
				if (value < -1)
				{
					throw new ArgumentOutOfRangeException("XmlBrowseViewBase.SelectedIndex", value.ToString(), "Index cannot be set to less than -1.");
				}
				if (m_selectedIndex == value && !_rootObjectHasBeenReset)
				{
					// It's useful to check this anyway, since the width of the window or something else
					// that affects visibility may have changed...but don't CHANGE the selection, the user may be editing...(LT-12092)
					if (value >= 0 && m_wantScrollIntoView)
					{
						MakeSelectionVisible(GetRowSelection(value));
					}
					return;
				}
				_rootObjectHasBeenReset = false;
				var oldSelectedIndex = m_selectedIndex;
				var objectCount = SpecialCache.get_VecSize(_hvoRoot, MainTag);
				// There have been some long standing and hard to reproduce bugs in this area where there is a mis-match between
				// the count of items in the current list, and the new index.
				// As of 1 Jan 2020, I (RandyR) think the issue is resolved. it was reproducible in the word list Concordance tool on startup.
				// I think I got it fixed by resetting the vector in the decorator, but time will tell....
				if (objectCount == 0)
				{
					// Nobody home, so quit.
					m_hvoOldSel = 0;
					m_selectedIndex = -1;
					PropertyTable.GetValue<IFwMainWnd>(FwUtilsConstants.window).IdleQueue.Add(IdleQueuePriority.Medium, FireSelectionChanged);
					return;
				}
				// objectCount > 0.
				if (value < 0 || value >= objectCount)
				{
					throw new ArgumentOutOfRangeException(@"XmlBrowseViewBase.SelectedIndex", value.ToString(), @"The new value must be zero, or greater, but less than the count of objects.");
				}
				var hvoObjNewSel = GetNewSelectionObject(value);
				// Set the member variable before firing the events,
				// in case the event handlers access the SelectedIndex property.
				// Wouldn't want them to get the wrong answer, and be confused.
				m_selectedIndex = value;
				if (hvoObjNewSel != m_hvoOldSel)
				{
					m_hvoOldSel = hvoObjNewSel;
					// Notify any delegates that the selection of the main object in the vector
					// has changed.
					if (SelectionChangedEvent != null && hvoObjNewSel != 0)
					{
						SelectionChangedEvent(this, new FwObjectSelectionEventArgs(hvoObjNewSel, value));
						// Recalculate the vector size since somebody somewhere may have deleted something.
						// See LT-6884 for an example.
						objectCount = SpecialCache.get_VecSize(_hvoRoot, MainTag);
					}
				}
				// Some of the changes below may destroy the new selection, especially setting the new index, but
				// possibly also clearing the old one. So save the selection info in order to restore it afterwards.
				SelectionHelper selection = null;
				var oldSelectionAnchorIndex = -1;
				var oldSelectionEndIndex = -1;
				if (RootBox != null)
				{
					var vwSelection = RootBox.Selection;
					oldSelectionEndIndex = GetRowIndexFromSelection(vwSelection, true);
					oldSelectionAnchorIndex = GetRowIndexFromSelection(vwSelection, false);
					selection = SelectionHelper.GetSelectionInfo(vwSelection, this);
				}
				// Don't set the data member here, as it may be too late for the clients of the above event.
				// If they access the SelectedIndex property between the event firing and this setting,
				// they would get the old value, which is wrong.
				//m_selectedIndex = value;
				SelectedIndexChanged?.Invoke(this, new EventArgs());
				if (oldSelectedIndex >= 0 && oldSelectedIndex < objectCount)
				{
					// Turn off the highlighting of the old item.
					var hvoObjOldSel = SpecialCache.get_VecItem(_hvoRoot, MainTag, oldSelectedIndex);
					try
					{
						RootBox.PropChanged(hvoObjOldSel, m_tagMe, 0, 0, 0);
					}
					catch
					{
						m_bv.RaiseSelectionDrawingFailure();
					}
				}
				// Turn on the highlighting of the new item.
				try
				{
					RootBox?.PropChanged(hvoObjNewSel, m_tagMe, 0, 0, 0);
				}
				catch
				{
					m_bv.RaiseSelectionDrawingFailure();
				}
				// TE-6912: This RestoreSelection scrolled back to top
				// This is tricky: if we have a multi-row selection, e.g., from a long drag or select all, we
				// DO want to restore it. Also if it is entirely within the new selected row, where the user might also have dragged.
				// OTOH, we must NOT restore one somewhere else, like the old selected row, because that will
				// move the selected row back there!
				if (!ReadOnlySelect && selection != null && (SelectedIndex == oldSelectionEndIndex || oldSelectionEndIndex != oldSelectionAnchorIndex))
				{
					selection.RestoreSelectionAndScrollPos();
				}
				// do Selection & Scroll after highlighting, so insertion point will show up.
				if (m_wantScrollIntoView)
				{
					DoSelectAndScroll(hvoObjNewSel, value);
					// allow preventing setting the focus (LT-9481)
					// Don't steal the focus from another window.  See FWR-1795.
					if (CanFocus && ParentForm == Form.ActiveForm)
					{
						Focus(); // Note: used to be part of DoSelectAndScroll, but I'm not sure why...
					}
				}
				Update();
				PropertyTable.GetValue<IFwMainWnd>(FwUtilsConstants.window).IdleQueue.Add(IdleQueuePriority.Medium, FireSelectionChanged);
			}
		}

		private bool FireSelectionChanged(object parameter)
		{
			if (IsDisposed)
			{
				throw new InvalidOperationException("Thou shalt not call methods after I am disposed!");
			}
			if (RootBox == null)
			{
				return true; // presumably we've been disposed; this happens (at least) in tests where a later test may simulate idle events.
			}
			var hvoObjNewSel = GetNewSelectionObject(m_selectedIndex);
			if (hvoObjNewSel == 0)
			{
				if (m_selectedIndex == 0)
				{
					m_selectedIndex = -1;
				}
				else if (m_selectedIndex > 0)
				{
					var cobj = SpecialCache.get_VecSize(_hvoRoot, MainTag);
					Debug.Assert(m_selectedIndex >= cobj);
					m_selectedIndex = cobj - 1;
					hvoObjNewSel = GetNewSelectionObject(m_selectedIndex);
				}
			}
			if (hvoObjNewSel != m_hvoOldSel)
			{
				m_hvoOldSel = hvoObjNewSel;
				// Notify any delegates that the selection of the main object in the vector
				// has changed.
				SelectionChangedEvent?.Invoke(this, new FwObjectSelectionEventArgs(hvoObjNewSel, m_selectedIndex));
			}
			SelectedIndexChanged?.Invoke(this, new EventArgs());
			return true;
		}

		/// <summary>
		/// Because this is a simple root site, which is derived from UserControl, we can't prevent MouseDown from
		/// giving it focus. But we can give other linked windows a chance to take it back.
		/// </summary>
		protected override void OnGotFocus(EventArgs e)
		{
			base.OnGotFocus(e);
			if (ReadOnlySelect)
			{
				// We should NOT have focus! See if someone can take it back.
				m_bv.Publisher.Publish(new PublisherParameterObject("BrowseViewStoleFocus", this));
			}
		}

		/// <summary>
		/// Convert the new selection into a real object before making any selection.
		/// Otherwise the views code might convert it during the selection, making the selection invalid.
		/// </summary>
		private int GetNewSelectionObject(int index)
		{
			if (index < 0)
			{
				return 0;
			}
			var cobj = SpecialCache.get_VecSize(_hvoRoot, MainTag);
			if (cobj == 0 || index >= cobj)
			{
				return 0;
			}
			return SpecialCache.get_VecItem(_hvoRoot, MainTag, index);
		}

		/// <summary />
		protected int m_ydSelTop;
		/// <summary />
		protected int m_ydSelBottom;
		/// <summary />
		protected int m_ydSelScrollPos;
		/// <summary />
		protected int m_iSelIndex;
		/// <summary>
		/// Handle the special aspects of adjusting the scroll position for a table of cells
		/// like we have in the browse view.  See LT-3607 for details of what can go wrong
		/// without this override.
		/// </summary>
		/// <param name="dxdSize">change in the horizontal size</param>
		/// <param name="dxdPosition">horizontal position where change occurred</param>
		/// <param name="dydSize">change in the vertical size</param>
		/// <param name="dydPosition">vertical position where change occurred</param>
		/// <returns></returns>
		protected override bool AdjustScrollRange1(int dxdSize, int dxdPosition, int dydSize, int dydPosition)
		{
			var dydRangeNew = AdjustedScrollRange.Height;
			// Remember: ScrollPosition returns negative values!
			var dydPosOld = -ScrollPosition.Y;
			int dydPosNew;
			// If the current position is after where the change occurred, it needs to
			// be adjusted by the same amount.
			if (dydPosOld > dydPosition)
			{
				dydPosNew = dydPosOld + dydSize;
			}
			else
			{
				dydPosNew = dydPosOld;
			}
			// But that doesn't quite work for browse view, because we want the whole row to
			// show, either at the top or the bottom.
			// Use values stored by MakeSelectionVisible if they're available.
			var iSelIndex = m_iSelIndex;
			if (iSelIndex == SelectedIndex && (m_ydSelTop != 0 || m_ydSelBottom != 0))
			{
				var dyBottomNew = dydPosNew + ClientHeight - m_dyHeader;
				var dySelTop = m_ydSelTop;
				var dySelBottom = m_ydSelBottom;
				//int dySelScrollPos = m_ydSelScrollPos;
				// clear these values so they won't be used when stale.
				m_ydSelTop = 0;
				m_ydSelBottom = 0;
				m_ydSelScrollPos = 0;
				m_iSelIndex = 0;
				if (dySelTop > dydPosition)
				{
					dySelTop += dydSize;
					dySelBottom += dydSize;
				}
				if (dySelTop < dydPosNew)
				{
					dydPosNew = dySelTop;
				}
				else if (dySelBottom > dyBottomNew)
				{
					var deltaY = dySelBottom - dyBottomNew;
					dydPosNew += deltaY;
				}
			}

			var dxdRangeNew = AutoScrollMinSize.Width + dxdSize;
			var dxdPosNew = -ScrollPosition.X;
			if (HScroll)
			{
				// Similarly for horizontal scroll bar.
				if (dxdPosNew > dxdPosition)
				{
					dxdPosNew += dxdSize;
				}
			}
			return UpdateScrollRange(dxdRangeNew, dxdPosNew, dydRangeNew, dydPosNew);
		}

		/// <summary>
		/// Make the selection that would be made by clicking at the specified mouse event,
		/// but don't install it.
		/// </summary>
		internal IVwSelection MakeSelectionAt(MouseEventArgs e)
		{
			using (new HoldGraphics(this))
			{
				var pt = PixelToView(new Point(e.X, e.Y));
				GetCoordRects(out var rcSrcRoot, out var rcDstRoot);
				// This can legitimately return null,
				// e.g. because they selected beyond the last item.
				return RootBox.MakeSelAt(pt.X, pt.Y, rcSrcRoot, rcDstRoot, false);
			}
		}

		internal void DoSelectionSideEffects(MouseEventArgs e)
		{
			var vwselNew = MakeSelectionAt(e);
			if (vwselNew != null)
			{
				DoSelectionSideEffects(vwselNew);
			}
		}

		private bool m_fHandlingSideEffects;

		/// <summary>
		/// Given a selection in the view, return the row index. Rarely may return -1 if unable to
		/// identify a row.
		/// </summary>
		/// <param name="sel"></param>
		/// <param name="fEndPoint">true to get index based on end of selection, false based on anchor.
		/// default is true, so in a long drag we get the mouse-up row.</param>
		/// <returns></returns>
		internal int GetRowIndexFromSelection(IVwSelection sel, bool fEndPoint)
		{
			if (sel == null)
			{
				return -1;
			}
			try
			{
				var clev = sel.CLevels(fEndPoint);
				sel.PropInfo(fEndPoint, clev - 1, out _, out var tag, out var ihvo, out _, out _);
				if (tag != MainTag) // not sure how this could happen, but the precaution was in an earlier version.
				{
					return -1;
				}
				return ihvo;
			}
			catch (System.Runtime.InteropServices.COMException)
			{
				// This shouldn't happen, but don't let it be catastrophic if it does.
			}
			return -1;
		}

		/// <summary>
		/// Common code to HandleSelectionChange and MouseDown.
		/// </summary>
		protected void DoSelectionSideEffects(IVwSelection sel)
		{
			// There is tricky and subtle stuff going on here. Se LT-3565, 6501, and 9192 for things
			// that should not be broken by any changes.
			// If we're in the middle of a drag, or processing mouse down, we don't want to change
			// the selected row...we're postponing that to mouse up.
			if (!m_fMouseUpEnabled)
			{
				SetSelectedIndex(GetRowIndexFromSelection(sel, true));
			}
		}

		internal void SetSelectedIndex(int ihvo)
		{
			if (m_fHandlingSideEffects)
			{
				return;
			}
			if (ihvo == -1)
			{
				// we seem to have cleared our list, so make sure we
				// can't use this value to get a list item.
				m_selectedIndex = -1;
				return;
			}
			if (SelectedIndex != ihvo) // No sense in waking up the beast for no reason.
			{
				m_fHandlingSideEffects = true;
				SelectedIndex = ihvo;
				m_fHandlingSideEffects = false;
			}
		}

		/// <summary>
		/// Gets the row/cell information of the current selection.
		/// </summary>
		internal static void GetCurrentTableCellInfo(IVwSelection vwsel, out int iLevel, out int iBox, out int iTableBox, out int cTableBoxes, out int iTableLevel, out int iCellBox, out int cCellBoxes, out int iCellLevel)
		{
			var cBoxes = -1;
			iBox = -1;
			iTableBox = -1;
			cTableBoxes = -1;
			iTableLevel = -1;
			var iRowBox = -1;
			var cRowBoxes = -1;
			var iRowLevel = -1;
			iCellBox = -1;
			cCellBoxes = -1;
			iCellLevel = -1;
			// Find the current table cell and advance to the next one, possibly on the
			// next or previous row.
			var cLevels = vwsel.get_BoxDepth(true);
			for (iLevel = 0; iLevel < cLevels; ++iLevel)
			{
				cBoxes = vwsel.get_BoxCount(true, iLevel);
				iBox = vwsel.get_BoxIndex(true, iLevel);
				var vbt = vwsel.get_BoxType(true, iLevel);
				switch (vbt)
				{
					case VwBoxType.kvbtTable:
						// Note that the layout should one (visible) row per "table", and
						// stacks the "table" boxes to form the visual table.  See JohnT
						// for an explanation of this nonintuitive use of tables and rows.
						// At least, i think JohnT knows why -- maybe it's RandyR?
						iTableBox = iBox;
						cTableBoxes = cBoxes;
						iTableLevel = iLevel;
						break;
					case VwBoxType.kvbtTableRow:
						iRowBox = iBox;
						cRowBoxes = cBoxes;
						iRowLevel = iLevel;
						break;
					case VwBoxType.kvbtTableCell:
						iCellBox = iBox;
						cCellBoxes = cBoxes;
						iCellLevel = iLevel;
						break;
				}
			}
			// Some simple sanity checking.
			Debug.Assert(cBoxes != -1);
			Debug.Assert(iBox != -1);
			Debug.Assert(iTableBox != -1);
			Debug.Assert(cTableBoxes != -1);
			Debug.Assert(iTableLevel != -1);
			Debug.Assert(iRowBox != -1);
			Debug.Assert(cRowBoxes != -1);
			Debug.Assert(iRowLevel != -1);
			Debug.Assert(iCellBox != -1);
			Debug.Assert(cCellBoxes != -1);
			Debug.Assert(iCellLevel != -1);
		}

		/// <summary>
		/// Handle the OnKeyDown event
		/// </summary>
		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);

			// if we've done a tab, expand the current selection to whole cell contents.
			var vwsel = RootBox.Selection;
			if (e.KeyCode == Keys.Tab && vwsel != null)
			{
				SelectContentsOfCell(vwsel);
			}

		}

		/// <summary />
		protected void SelectContentsOfCell(IVwSelection vwsel)
		{
			if (vwsel == null)
			{
				return;
			}

			GetCurrentTableCellInfo(vwsel, out var iLevel, out var iBox, out var iTableBox, out _, out _, out var iCellBox, out _, out var iCellLevel);
			var vwsel2 = RootBox.MakeSelInBox(vwsel, true, iCellLevel, iCellBox, true, true, false);
			if (vwsel2 == null)
			{
				return; // can't do anything, so give up.  See LT-9706.
			}
			// Make sure it's in the same cell. In pathological cases, for example, tabbing
			// from a bulk edit check box in a row that is all read-only, the resulting
			// selection may be in another row. If we can't make a valid selection where
			// we're trying to, don't change it at all.
			GetCurrentTableCellInfo(vwsel2, out var iLevel2, out var iBox2, out var iTableBox2, out _, out _, out var iCellBox2, out _, out iCellLevel);
			if (iLevel2 == iLevel && iBox2 == iBox && iTableBox2 == iTableBox && iCellBox2 == iCellBox)
			{
				vwsel2.Install();
			}
		}

		/// <summary>
		/// We only want to do something on mouse up events if the XmlBrowseView has gotten a
		/// mouseDown event.
		/// </summary>
		protected bool m_fMouseUpEnabled;

		/// <summary>
		/// MouseUp actions on XmlBrowseView should only be enabled if a preceeding MouseDown occured
		/// in the view.
		/// </summary>
		protected override void OnMouseDown(MouseEventArgs e)
		{
			m_fMouseUpEnabled = true;
			base.OnMouseDown(e);
		}

		/// <summary>
		/// Process left or right mouse button down
		/// </summary>
		protected override void OnMouseUp(MouseEventArgs e)
		{
			//If XmlBrowseView did not receive a mouse down event then we do not want to
			//do anything on the mouseUp because the mouseUp would have come from clicking
			//somewhere else. LT-8939
			if (!m_fMouseUpEnabled)
			{
				return;
			}

			try
			{
				var newSelectedIndex = GetRowIndexFromSelection(MakeSelectionAt(e), true);
				// If we leave this set, the base method call's side effects like updating the WS combo
				// don't happen.
				m_fHandlingMouseUp = false;
				// Do this before we do stuff that might mess up scroll positions and make the mouse
				// position invalid.
				base.OnMouseUp(e);
				m_fHandlingMouseUp = true;
				// preserve any selection the mouse down made, which may be destroyed by the process
				// of moving the highlight.
				var helper = SelectionHelper.Create(this);
				SetSelectedIndex(newSelectedIndex);
				helper?.SetSelection(this, true, false, VwScrollSelOpts.kssoDefault);
				m_bv?.BrowseViewMouseUp(e);
			}
			finally
			{
				m_fMouseUpEnabled = false;
				m_fHandlingMouseUp = false;
			}
		}

		/// <summary>
		/// A browse view does not HAVE to have a selection; forcing one (e.g., after clicking on a
		/// check box) and scrolling to the one we made at the start of the view is very disconcerting.
		/// </summary>
		protected override void EnsureDefaultSelection()
		{
		}

		/// <summary>
		/// Sets whether the selected row is indicated by highlighting.  Intended to be used from SetSelectedRowHighlighting.  Do not
		/// use directly, but override SetSelectedRowHighlighting if a different kind of highlighting is needed.
		/// </summary>
		internal SelectionHighlighting SelectedRowHighlighting
		{
			get => m_fSelectedRowHighlighting;
			set
			{
				if (m_fSelectedRowHighlighting == value)
				{
					return; // Nothing to do.
				}
				m_fSelectedRowHighlighting = value;
				// Turn on or off the highlighting of the current row.
				if (m_selectedIndex >= 0 && RootBox != null)
				{
					var hvoObjSel = SpecialCache.get_VecItem(_hvoRoot, MainTag, m_selectedIndex);
					RootBox.PropChanged(hvoObjSel, m_tagMe, 0, 0, 0);
				}
			}
		}

		/// <summary>
		/// True if we are running the read-only version of the view that is primarily used for
		/// selecting.
		/// </summary>
		protected virtual bool ReadOnlySelect => PropertyTable != null && _configParamsElement?.Attribute("editable") != null && !XmlUtils.GetBooleanAttributeValue(_configParamsElement, "editable");

		/// <summary>
		/// the object that has properties that are shown by this view.
		/// </summary>
		/// <remarks> this will be changed often in the case where this view is dependent on another one;
		/// that is, or some other browse view has a list and each time to selected item changes, our
		/// root object changes.
		/// </remarks>
		public virtual int RootObjectHvo
		{
			get => _hvoRoot;
			set
			{
				if (value == 0)
				{
					if (_hvoRoot != 0)
					{
						// Hmm, setting to zero after it had been something else. Sounds bad.
						throw new InvalidOperationException($"Changing to zero new root HVO, where the old root was '{_hvoRoot}'.");
					}
					// No root yet.
					return;
				}
				if (_rootObjectHasBeenSet && _hvoRoot == value)
				{
					return; // No sense getting all worked up, if it is the same as before.
				}
				// The Init method sets _hvoRoot directly, but we want to skip that set and look for later resets.
				if (_hvoRoot != 0 && _hvoRoot != value)
				{
					_rootObjectHasBeenReset = true;
				}
				_hvoRoot = value;
				_rootObjectHasBeenSet = true;
				RootBox.SetRootObject(_hvoRoot, Vc, XmlBrowseViewVc.kfragRoot, m_styleSheet);
				// This seems to be necessary to get the data entry row to resize even if the new
				// list is the same length as the old. Must NOT remember new positions, because
				// this can be called before Layout retrieves them!
				m_bv.AdjustColumnWidths(false);
				// The old index and selected object must be wrong by now, so reset them.
				m_hvoOldSel = 0;
				var chvo = SpecialCache.get_VecSize(_hvoRoot, MainTag);
				if (chvo == 0)
				{
					m_selectedIndex = -1;
				}
				else
				{
					var hvo = SpecialCache.get_VecItem(_hvoRoot, MainTag, 0);
					if (hvo == (int)SpecialHVOValues.kHvoObjectDeleted)
					{
						// Deleting everything in one view doesn't seem to fix the RecordList in
						// related views.  See LT-9711.
						IRecordListUpdater recordListUpdater = PropertyTable.GetValue<IRecordListRepository>(LanguageExplorerConstants.RecordListRepository).ActiveRecordList;
						if (recordListUpdater != null)
						{
							using (new WaitCursor(this))
							{
								//update the list, forcing a recursive refresh
								recordListUpdater.UpdateList(true);
							}
						}
						chvo = SpecialCache.get_VecSize(_hvoRoot, MainTag);
						if (chvo == 0)
						{
							m_selectedIndex = -1;
						}
						else
						{
							SelectedIndex = 0;
						}
					}
					else
					{
						SelectedIndex = 0;
					}
				}
			}
		}

		/// <summary>
		/// The identifier of the top-level list property being displayed.
		/// </summary>
		public int MainTag { get; protected set; }

		#endregion Properties

		#region Construction, Initialization and disposal

		/// <summary />
		internal XmlBrowseViewBase() : base(null)
		{
			InitializeComponent();
			AccessibleName = "XmlBrowseViewBase";
			BackColor = SystemColors.Window;
		}

		/// <summary>
		/// Initializes using <paramref name="configParamsElement"/>.
		/// </summary>
		internal void Init(XElement configParamsElement, int hvoRoot, int madeUpFieldIdentifier, LcmCache cache, BrowseViewer bv)
		{
			Debug.Assert((m_selectedIndex == -1), "Cannot set the index to less than zero before initializing.");
			Debug.Assert(_configParamsElement == null || _configParamsElement == configParamsElement, "XmlBrowseViewBase.Init: Mismatched configuration parameters.");

			_hvoRoot = hvoRoot;
			MainTag = madeUpFieldIdentifier;
			if (_configParamsElement == null)
			{
				_configParamsElement = configParamsElement;
			}
			// Do this early...we need the ID to restore the columns when the VC is created.
			m_id = XmlUtils.GetOptionalAttributeValue(_configParamsElement, "id");
			if (string.IsNullOrWhiteSpace(m_id))
			{
				throw new ArgumentNullException("No id element which is required.");
			}
			m_bv = bv;
			m_cache = cache;
			SpecialCache = m_bv.SpecialCache;
			// This is usually done in MakeRoot, but we need it to exist right from the start
			// because right after we make this window we use info from the VC to help make
			// the column headers.
			if (bv != null)
			{
				m_sortItemProvider = bv.SortItemProvider;
			}
			if (m_xbvvc == null)
			{
				// Merely asking the Vc will create one, if m_xbvvc is null.
				var dummy = Vc;
			}
			var sDefaultCursor = XmlUtils.GetOptionalAttributeValue(configParamsElement, "defaultCursor", null);
			// Set a default cursor for a ReadOnly view, if none is given.
			if (sDefaultCursor == null && ReadOnlySelect)
			{
				sDefaultCursor = "Arrow";
			}
			if (sDefaultCursor != null)
			{
				switch (sDefaultCursor)
				{
					case "IBeam":
						EditingHelper.DefaultCursor = Cursors.IBeam;
						break;
					case "Hand":
						EditingHelper.DefaultCursor = Cursors.Hand;
						break;
					case "Arrow":
						EditingHelper.DefaultCursor = Cursors.Arrow;
						break;
					case "Cross":
						EditingHelper.DefaultCursor = Cursors.Cross;
						break;
				}
			}
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			// Must not be run more than once.
			if (IsDisposed)
			{
				return;
			}

			if (disposing)
			{
				var idleQueue = PropertyTable?.GetValue<IFwMainWnd>(FwUtilsConstants.window)?.IdleQueue;
				if (idleQueue != null)
				{
					idleQueue.Remove(RemoveRootBoxSelectionOnIdle);
					idleQueue.Remove(FireSelectionChanged);
					idleQueue.Remove(UpdateSelectedRow);
				}
				Subscriber.Unsubscribe("SaveScrollPosition", SaveScrollPosition);
				Subscriber.Unsubscribe("RestoreScrollPosition", RestoreScrollPosition);
				if (m_bv != null && !m_bv.IsDisposed)
				{
					m_bv.SpecialCache?.RemoveNotification(this);
				}
			}

			base.Dispose(disposing);

			if (disposing)
			{
				components?.Dispose();
				if (m_bv != null && !m_bv.IsDisposed)
				{
					m_bv.Dispose();
				}
			}
			m_xbvvc = null;
			_configParamsElement = null;
			m_bv = null;
		}

		#endregion Construction, Initialization and disposal

		#region Other methods

		/// <summary>
		/// Get from the specified node a list of strings, as used in filter bar and bulk edit bar,
		/// where the node is expected to have a [stringList] first child element which is interpreted
		/// in the context of our string table.
		/// </summary>
		internal string[] GetStringList(XElement spec)
		{
			var stringList = XmlUtils.GetFirstNonCommentChild(spec);
			if (stringList == null || stringList.Name != "stringList")
			{
				return null;
			}
			return StringTable.Table.GetStringsFromStringListNode(stringList);
		}

		/// <summary>
		/// a look up table for getting the correct version of strings that the user will see.
		/// </summary>
		public virtual bool ColumnSortedFromEnd(int icol)
		{
			return m_bv.ColumnActiveAndSortedFromEnd(icol);
		}

		/// <summary>
		/// The scroll range can't be stored in the AutoScrollMinSize like we'd like,
		/// because setting that turns on the view's own scroll bar.
		///
		/// This is the requested maximum range of the scrollbar.
		/// MSDN says of AutoScrollMinSize "A Size that determines the minimum size of the virtual area through which the user can scroll."
		/// </summary>
		public override Size ScrollMinSize
		{
			set
			{
				Debug.Assert(!IsVertical, "Unexpected vertical XmlBrowseViewBase");
				if (m_bv?.ScrollBar != null)
				{
					m_bv.ScrollBar.Maximum = value.Height;
				}
				// A new rootbox height will lead to a new MeanRowHeight, so ScrollBar.LargeChange and .SmallChange need
				// to be updated.
				SetScrollBarParameters(m_bv.ScrollBar);
			}
		}

		/// <summary>
		/// Zero if no rows or null RootBox.
		/// </summary>
		public int MeanRowHeight => RootBox == null ? 0 : RowCount == 0 ? 0 : RootBox.Height / RowCount;

		/// <summary>
		/// The amount of content that the user can scroll through within the content display area.
		/// This height, the maximum value that a user can reach through the UI, is different than the ScrollBar.Maximum
		/// amount (see http://msdn.microsoft.com/en-us/library/vstudio/system.windows.forms.scrollbar.maximum).
		/// Upper boundary on ScrollPosition.
		/// </summary>
		public int ScrollPositionMaxUserReachable
		{
			get
			{
				Debug.Assert(!IsVertical, "Unexpected vertical XmlBrowseViewBase");
				var contentHeight = RootBox.Height;
				var desiredMaxUserReachable = contentHeight - ClientHeight;
				if (desiredMaxUserReachable < 0)
				{
					desiredMaxUserReachable = 0;
				}
				return desiredMaxUserReachable;
			}
		}

		/// <summary>
		/// Because we turn AutoScroll off to suppress the scroll bars, we need our own
		/// private representation of the actual scroll position.
		/// The setter has to behave in the same bizarre way as AutoScrollPosition,
		/// that setting it to (x,y) results in the new value being (-x, -y).
		/// </summary>
		public override Point ScrollPosition
		{
			get => _scrollPosition;

			set
			{
				if (_scrollPosition == value)
				{
					return;
				}
				Debug.Assert(!IsVertical, "Unexpected vertical XmlBrowseViewBase");
				var newValueForY = value.Y;
				const int minValueForY = 0;
				var maxValueForY = ScrollPositionMaxUserReachable;
				if (newValueForY < minValueForY)
				{
					newValueForY = minValueForY;
				}
				// Don't scroll so far down so you can't see any rows.
				if (newValueForY > maxValueForY)
				{
					newValueForY = maxValueForY;
				}
				_scrollPosition.Y = -newValueForY;
				Debug.Assert(m_bv.ScrollBar.Maximum >= 0, "ScrollBar.Maximum is unexpectedly a negative value");
				// The assignment to 'Value' can (and was LT-3091) throw an exception
				Debug.Assert(m_bv.ScrollBar.Minimum <= minValueForY, "minValue setting could allow attempt to set out of bounds");
				Debug.Assert(m_bv.ScrollBar.Maximum >= maxValueForY, "maxValue setting could allow attempt to set out of bounds");
				// to minimize recursive calls, don't set the scroll bar unless it's wrong.
				if (m_bv.ScrollBar.Value != newValueForY)
				{
					m_bv.ScrollBar.Value = newValueForY;
				}
				// Achieve the scroll by just invalidating. We'd like to optimize this sometime...
				Invalidate();
			}
		}

		/// <returns>
		/// Desired scrollbar LargeChange value
		/// </returns>
		public int DesiredScrollBarLargeChange
		{
			get
			{
				var desiredLargeChange = ClientHeight - MeanRowHeight;
				// Two reasons to make this 1 rather than 0:
				// 1. Don't want a click in the large change area to produce no change at all.
				// 2. scroll range gets set to ScrollPositionMaxUserReachable + (our return value) - 1.
				// Scroll range should never be LESS than ScrollPositionMaxUserReachable
				// (See asserts in ScrollPosition setter.)
				// This typically only happens during premature window layout while ClientHeight is very small.
				// But a zero here can trigger asserts and possibly crashes (LT-14544)
				if (desiredLargeChange <= 0)
				{
					desiredLargeChange = 1;
				}
				return desiredLargeChange;
			}
		}

		/// <summary>
		/// Set a controlling scrollbar's parameters using information this object owns.
		/// </summary>
		public void SetScrollBarParameters(ScrollBar scrollBar)
		{
			scrollBar.Minimum = 0;
			scrollBar.SmallChange = MeanRowHeight;
			scrollBar.LargeChange = DesiredScrollBarLargeChange;
		}

		/// <summary>
		/// True if the control is being scrolled and should have its ScrollMinSize
		/// adjusted and its AutoScrollPosition modified. An XmlBrowseViewBase
		/// scrolls, but using a separate scroll bar.
		/// </summary>
		public override bool DoingScrolling => true;

		/// <summary>
		/// Determines whether automatic vertical scrolling to show the selection should
		/// occur. XmlBrowseViews want this behavior, even though they don't have an auto
		/// scroll bar.
		/// </summary>
		protected override bool DoAutoVScroll => true;

		protected internal ISilDataAccessManaged SpecialCache { get; protected set; }

		/// <summary>
		/// Called when [prepare to refresh].
		/// </summary>
		public void PrepareToRefresh()
		{
			SaveScrollPosition(null);
		}

		/// <summary>
		/// Creates a new selection restorer.
		/// </summary>
		protected override SelectionRestorer CreateSelectionRestorer()
		{
			return new XmlBrowseViewSelectionRestorer(this);
		}

		/// <summary>
		/// Called through Publisher.
		///
		/// Save the current scroll position for later restoration, in a form that will survive
		/// having the view contents replaced by a lazy box (that is, it's not good enough to
		/// just save AutoScrollPosition.y, we need enough information to create a selection
		/// at the top of the screen and get a corresponding selection back there.
		/// This class implements this by figuring out the index of the record at the top of
		/// the screen and saving that.
		/// </summary>
		public void SaveScrollPosition(object newValue)
		{
			if (!IsHandleCreated || RootBox == null)
			{
				// JohnT: really nasty things can happen if we create the root box as part of
				// HoldGraphics here (see for example LT-2100). And there's no need: if we
				// haven't even made our root box we can't have a meaningful scroll position to
				// save.
				m_iTopOfScreenObjectForScrollPosition = -1; // in case we can't figure one.
				return;
			}
			try
			{
				using (new HoldGraphics(this))
				{
					GetCoordRects(out var rcSrcRoot, out var rcDstRoot);
					var sel = RootBox.MakeSelAt(1, 0, rcSrcRoot, rcDstRoot, false);
					m_iTopOfScreenObjectForScrollPosition = -1; // in case we can't figure one.
					if (sel == null)
					{
						return;
					}
					// This gets us the index of the object at the top of the screen in the list of
					// objects we are browsing.
					sel.PropInfo(false, sel.CLevels(false) - 1, out _, out _, out m_iTopOfScreenObjectForScrollPosition, out _, out _);
					// Get a selection of that whole object. This is just in case there might be a pixel or two difference
					// between the top of an IP and the top of the rectangle that encloses the whole object.
					var rgvsli = new SelLevInfo[1];
					rgvsli[0].ihvo = m_iTopOfScreenObjectForScrollPosition;
					rgvsli[0].tag = MainTag;
					sel = RootBox.MakeTextSelInObj(0, 1, rgvsli, 0, null, false, false, false, true, false);
					if (sel == null)
					{
						m_iTopOfScreenObjectForScrollPosition = -1; // in case we can't figure one.
						return;
					}

					//sel = RootBox.MakeSelInObj(0, 1, rgvsli, 0, false);
					// Get its position, specifically, we save the distance from the top of the client area to the top of the object.
					// Often this will be negative because the top of the TOS object is just above the top of the client area.
					sel.Location(m_graphicsManager.VwGraphics, rcSrcRoot, rcDstRoot, out var rcPrimary, out _, out _, out _);
					m_dyTopOfScreenOffset = rcPrimary.top;
				}
			}
			catch
			{
				m_iTopOfScreenObjectForScrollPosition = -1; // in case we can't figure one.
			}
		}

		/// <summary>
		/// Called through mediator by reflection. (Maybe?)
		/// This routine attempts to restore the scroll position previously saved by OnSaveScrollPosition.
		/// Specifically, it attempts to scroll to a position such that the top of the object at index
		/// m_iTopOfScreenObjectForScrollPosition is m_dyTopOfScreenOffset pixels below the top of the
		/// client area (or above, if m_dyTopOfScreenOffset is negative).
		/// </summary>
		public void RestoreScrollPosition(object newValue)
		{
			RestoreScrollPosition(m_iTopOfScreenObjectForScrollPosition);
		}

		/// <summary>
		/// Called by BulkEditBar if Preview makes the highlighted row scroll off the screen.
		/// It attempts to scroll to a position such that the top of the object at index
		/// irow is m_dyTopOfScreenOffset pixels below the top of the
		/// client area (or above, if m_dyTopOfScreenOffset is negative).
		/// </summary>
		internal void RestoreScrollPosition(int indexOfHighlightedRow)
		{
			if (indexOfHighlightedRow < 0 || indexOfHighlightedRow > SpecialCache.get_VecSize(_hvoRoot, MainTag))
			{
				// we weren't able to save a scroll position for some reason, or the position we saved is
				// out of range following whatever changed, so we can't restore.
				return;
			}
			// Get a selection of the whole target object. Do this OUTSIDE the HoldGraphics/GetCoordRects block,
			// since it may change the scroll position as it expands lazy boxes, modifying the dest rect.
			try
			{
				var rgvsli = new SelLevInfo[1];
				rgvsli[0].ihvo = indexOfHighlightedRow;
				rgvsli[0].tag = MainTag;
				var sel = RootBox.MakeTextSelInObj(0, 1, rgvsli, 0, null, false, false, false, true, false);
				if (sel == null)
				{
					// Just ignore it if we couldn't make a selection.
					Debug.WriteLine("restore scroll position failed");
					return;
				}
				using (new HoldGraphics(this))
				{
					GetCoordRects(out var rcSrcRoot, out var rcDstRoot);
					// Get its position, specifically, we save the distance from the top of the client area to the top of the object.
					// Often this will be negative because the top of the TOS object is just above the top of the client area.
					sel.Location(m_graphicsManager.VwGraphics, rcSrcRoot, rcDstRoot, out var rcPrimary, out _, out _, out _);
					var dyCurrentOffset = rcPrimary.top;
					var scrollDistance = dyCurrentOffset - m_dyTopOfScreenOffset; // positive means move window contents down.
					var currentAsp = ScrollPosition; // negative!!
					ScrollPosition = new Point(-currentAsp.X, -currentAsp.Y + scrollDistance);
				}
			}
			catch
			{
				// Just ignore it if we couldn't make a selection or something else goes wrong.
				Debug.WriteLine("restore scroll position failed");
			}
		}

		/// <summary>
		/// Gets the name of the corresponding property.
		/// </summary>
		public string GetCorrespondingPropertyName(string property)
		{
			return $"{m_id}_{property}";
		}

		/// <summary>
		/// Cause the behavior to switch to the current setting of ReadOnlyBrowse.
		/// Override if the behavior should be different than this.
		/// </summary>
		public virtual void SetSelectedRowHighlighting()
		{
			switch (XmlUtils.GetOptionalAttributeValue(_configParamsElement, "selectionStyle", string.Empty))
			{
				case "all":
					SelectedRowHighlighting = SelectionHighlighting.all;
					break;
				case "border":
					SelectedRowHighlighting = SelectionHighlighting.border;
					break;
				case "none":
					SelectedRowHighlighting = SelectionHighlighting.none;
					break;
				default:
					SelectedRowHighlighting = ReadOnlySelect ? SelectionHighlighting.all : SelectionHighlighting.border;
					break;
			}
		}

		/// <summary>
		/// Get the HVO of the object at the specified index.
		/// </summary>
		public int HvoAt(int index)
		{
			return SpecialCache.get_VecItem(_hvoRoot, MainTag, index);
		}

		internal Rectangle LocationOfSelectedRow()
		{
			var sel = GetRowSelection(m_selectedIndex);
			using (new HoldGraphics(this))
			{
				GetCoordRects(out var rcSrcRoot, out var rcDstRoot);
				sel.Location(m_graphicsManager.VwGraphics, rcSrcRoot, rcDstRoot, out var rcPrimary, out _, out _, out _);

				return new Rectangle(rcPrimary.left, rcPrimary.top, rcPrimary.right - rcPrimary.left, rcPrimary.bottom - rcPrimary.top);
			}
		}

		/// <summary>
		/// Gets the row selection.
		/// </summary>
		protected IVwSelection GetRowSelection(int index)
		{
			var rgvsli = new SelLevInfo[1];
			rgvsli[0].ihvo = index;
			rgvsli[0].cpropPrevious = 0;
			rgvsli[0].tag = MainTag;
			IVwSelection selRow = null;
			try
			{
				selRow = RootBox.MakeTextSelInObj(0, 1, rgvsli, 0, null, false, false, false, true, false);
			}
			catch
			{
				// This can happen if the only columns specified are custom fields which have
				// been deleted. We don't want to crash -- see LT-6449.
			}
			return selRow;
		}

		/// <summary>
		/// Does the select and scroll.
		/// </summary>
		protected virtual void DoSelectAndScroll(int hvo, int index)
		{
			m_iSelIndex = 0;
			m_ydSelBottom = 0;
			m_ydSelScrollPos = 0;
			m_ydSelTop = 0;
			if (RootBox == null)
			{
				return;
			}
			var selRow = GetRowSelection(index);
			if (selRow != null)
			{
				MakeSelectionVisible(selRow, true, true, true);
			}
			// Make default insertion point (MouseDown already does it's own).
			if (m_fHandlingMouseUp)
			{
				return;
			}
			var vwSel = RootBox.Selection;
			var fWantNewIp = true;
			if (vwSel != null)
			{
				try
				{
					// If selection is already on the right row don't move it; might interfere with editing.
					// Sometimes (e.g., in Words RDE) we get RecordNavigation during editing.
					var clev = vwSel.CLevels(false); // anchor
					// The first argument below was true, which seems wrong.  See LT-8506, which
					// tends to prove that the argument must match that used in vwSel.CLevels() above.
					vwSel.PropInfo(false, clev - 1, out _, out _, out var ihvo, out _, out _);

					// If it's multi-line also don't change it.
					clev = vwSel.CLevels(true); // end
					vwSel.PropInfo(true, clev - 1, out _, out _, out var ihvoEnd, out _, out _);
					fWantNewIp = (ihvo == ihvoEnd && ihvo != SelectedIndex);
				}
				catch
				{
					fWantNewIp = true;
				}
			}

			if (fWantNewIp)
			{
				SetDefaultInsertionPointInRow(index);
			}
			// Calling Focus causes problems, e.g., LT-3746, when something calls this and the index
			// is not really changing. It can move focus from the detail pane to the browse
			// pane undesirably. Usually, we move it back as we change record and make an
			// initial selection in the detail pane, but if the selection isn't changing
			// the detail pane doesn't get updated. So leave the decision to the caller.
		}

		/// <summary>
		/// Set an insertion point somewhere in the given row,
		/// and install the selection if it exists.
		/// </summary>
		/// <param name="index">record index</param>
		/// <returns>true if selection was installed.</returns>
		private bool SetDefaultInsertionPointInRow(int index)
		{
			var rgvsli = new SelLevInfo[1];
			rgvsli[0].ihvo = index;
			rgvsli[0].cpropPrevious = 0;
			rgvsli[0].tag = MainTag;
			IVwSelection vwselNew = null;
			var isEditable = XmlUtils.GetOptionalBooleanAttributeValue(_configParamsElement, "editable", true);
			var fInstalledNewSelection = false;
			if (isEditable) //hack to get around bug XW-38
			{
				try
				{
					// Try to get an IP in an editable area.
					vwselNew = RootBox.MakeTextSelInObj(0,
						1, rgvsli, 0, null, //1, rgvsli,
						true, // fInitial
						true, // fEdit
						false, // fRange
						false, // fWholeObj
						false); // fInstall
								// if we find an editable selection, make sure it's in the same record.
					if (vwselNew != null)
					{
						var clev = vwselNew.CLevels(false); // anchor
						vwselNew.PropInfo(false, clev - 1, out _, out _, out var ihvo, out _, out _);
						if (ihvo == index)
						{
							// install the selection this time.
							vwselNew.Install();
							fInstalledNewSelection = true;
						}
					}
				}
				catch (ObjectDisposedException ode)
				{
					throw; // We should die, if we get into this one.
				}
				catch
				{
					fInstalledNewSelection = false;
				}
			}
			if (!fInstalledNewSelection)
			{
				// Try something else.
				try
				{
					vwselNew = RootBox.MakeTextSelInObj(0,
						1, rgvsli, 0, null,
						fInitial: true,
						fEdit: false,
						fRange: false,
						fWholeObj: false,
						fInstall: true);
					if (vwselNew != null)
						fInstalledNewSelection = true;
				}
				catch
				{
					// Not much we can do to handle errors, but don't let the program die just
					// because the display hasn't yet been laid out, so selections can't fully be
					// created and displayed.
					// Or (LT-20118) the display is laid out, but the previously-selected item has been deleted.
				}
			}
			if (vwselNew != null && fInstalledNewSelection)
			{
				MakeSelectionVisible(vwselNew, true, true, true);
			}
			else
			{
				Debug.WriteLine("XmlBrowseViewBase::SetDefaultInsertionPointInRow: Caught exception while trying to scroll a non-editable object into view.");
			}
			return fInstalledNewSelection;
		}

		/// <summary>
		/// Get the widths of the columns as VwLengths (for the view tables).
		/// </summary>
		public virtual VwLength[] GetColWidthInfo()
		{
			m_bv.GetColWidthInfo(out var rglength, out _);
			return rglength;
		}

		/// <summary>
		/// Can't find a .NET definition of this constant. One 'notch' on a mouse wheel is supposed to produce this
		/// value for MouseEventArgs.Delta. Supposedly it might change in future versions.
		/// </summary>
		const int WHEEL_DATA = 120;
		/// <summary>
		/// See if we can interpret a mouse wheel movement in the XmlBrowse view as a vertical
		/// scroll in the separate scroll bar maintained by the browse viewer to scroll this.
		/// Return true if the event was reinterpreted as a vertical scroll.
		/// </summary>
		private bool DoMouseWheelVScroll(MouseEventArgs e)
		{
			var scrollBar = m_bv?.ScrollBar;
			if (scrollBar == null || scrollBar.Maximum < scrollBar.LargeChange)
			{
				return false;
			}
			// Supposedly an e.Delta of one WHEEL_DATA is a unit of movement, and MouseWheelScrollLines tells how many lines
			// to scroll for one unit (or one rotation?? doc isn't clear), and SmallChange tells how far one line is...
			var newVal = scrollBar.Value - e.Delta * SystemInformation.MouseWheelScrollLines * scrollBar.SmallChange / WHEEL_DATA;
			newVal = Math.Max(0, newVal);
			newVal = Math.Min(newVal, scrollBar.Maximum - scrollBar.LargeChange);
			scrollBar.Value = newVal;
			return true;
		}

		/// <summary>
		/// When we get a mouse wheel event for windows other than the scrolling controller
		/// then pass on the message to the scrolling controller.
		/// </summary>
		protected override void OnMouseWheel(MouseEventArgs e)
		{
			if (DoMouseWheelVScroll(e))
			{
				// Note: the return here does not seem to have any effect in suppressing horizontal
				// scrolling by the mouse wheel. See the version of OnMouseWheel in BrowseViewScroller
				// for the code which succeeds in doing that.
				// Enhance JohnT: might it work (and make things clearer) to do all the mouse wheel
				// processing in that class?
				return; // skip the normal processing, which tends to scroll horizontally.
			}
			base.OnMouseWheel(e);
		}

		#endregion Other methods

		#region Overrides of RootSite
		/// <summary>
		/// Make the root box.
		/// </summary>
		public override void MakeRoot()
		{
			if (m_cache == null || DesignMode)
			{
				return;
			}

			base.MakeRoot();

			// Only change it if it is null or different.
			// Otherwise, it does an unneeded disposal/creation of the layout cache.
			if (Vc.Cache == null || Vc.Cache != m_cache)
			{
				Vc.Cache = m_cache;
			}
			SetSelectedRowHighlighting();
			ReadOnlyView = ReadOnlySelect;

			// This is where the 'Decorator' SDA is added.
			// This SDA can handle fake stuff, if the SDA overrides are all implemented properly.
			SpecialCache = m_bv.SpecialCache;
			RootBox.DataAccess = SpecialCache;

			RootObjectHvo = _hvoRoot;
			m_bv.SpecialCache.AddNotification(this);
			// Don't try to draw until we get OnSize and do layout.
			m_dxdLayoutWidth = kForceLayout;
		}

		/// <summary>
		/// Overridden to fix TE-4146
		/// </summary>
		protected override void OnLayout(LayoutEventArgs levent)
		{
			var oldWidth = m_dxdLayoutWidth;
			base.OnLayout(levent);
			// If being laid out for the first time, synchronize column widths.
			if (m_dxdLayoutWidth > 0 && m_dxdLayoutWidth != oldWidth)
			{
				m_bv.AdjustColumnWidths(false);
			}
		}

		/// <summary>
		/// Call Draw() which does all the real painting
		/// </summary>
		protected override void OnPaint(PaintEventArgs e)
		{
			// let the VC know when we're actually doing an OnPaint().
			Vc.InOnPaint = true;
			base.OnPaint(e);
			Vc.InOnPaint = false;
		}

		/// <summary>
		/// Notifies us that the selection changed. When next idle, we want to delete any unhelpful
		/// selection.
		/// </summary>
		protected override void HandleSelectionChange(IVwRootBox prootb, IVwSelection vwselNew)
		{
			base.HandleSelectionChange(prootb, vwselNew);
			PropertyTable.GetValue<IFwMainWnd>(FwUtilsConstants.window).IdleQueue.Add(IdleQueuePriority.Medium, RemoveRootBoxSelectionOnIdle);
		}

		private bool RemoveRootBoxSelectionOnIdle(object parameter)
		{
			if (IsDisposed)
			{
				throw new InvalidOperationException("Thou shalt not call methods after I am disposed!");
			}

			// This is a good time to check that we don't have a useless IP selection.
			// Right after we make it is too soon, because the current row's editing properties
			// aren't set until we paint it.
			var sel = RootBox?.Selection;
			if (sel != null && !sel.IsRange)
			{
				// an insertion point where you can't edit is just confusing.
				// Also, sometimes, trying to make an editable one we end up with one on another row.
				// We don't want that either.
				var idxFromSel = GetRowIndexFromSelection(sel, true);
				if (m_fSelectedRowHighlighting != SelectionHighlighting.none && idxFromSel != m_selectedIndex)
				{
					RootBox.DestroySelection();
				}
				else if (!sel.IsEditable)
				{
					RootBox.DestroySelection();
					if (idxFromSel == m_selectedIndex)
					{
						SetDefaultInsertionPointInRow(idxFromSel);
					}
				}
			}
			return true;
		}

		/// <remarks>
		/// The ScrollBar.Maximum is equal to the maxUserReachable + ScrollBar.LargeChange - 1, which
		/// in this implementation is also equal to RootBox.Height - MeanRowHeight - 1 in cases
		/// where RootBox.Height > ClientHeight.
		///
		/// Because ScrollBar.LargeChange.get is partly bounded by ScrollBar.Maximum, we can't reliably set and use
		/// ScrollBar.LargeChange before setting ScrollBar.Maximum. But we can know the value of what ScrollBar.LargeChange
		/// should be from DesiredScrollBarLargeChange.
		/// </remarks>
		protected override Size ScrollRange
		{
			get
			{
				var desiredMaxUserReachable = ScrollPositionMaxUserReachable;
				// http://msdn.microsoft.com/en-us/library/vstudio/system.windows.forms.scrollbar.maximum
				var desiredMaxScrollbarHeight = desiredMaxUserReachable + DesiredScrollBarLargeChange - 1;
				return new Size(Width, desiredMaxScrollbarHeight);
			}
		}
		#endregion

		#region Overrides of SimpleRootSite

		/// <summary>
		/// Initialize a FLEx component with the basic interfaces.
		/// </summary>
		/// <param name="flexComponentParameters">Parameter object that contains the required three interfaces.</param>
		public override void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
		{
			base.InitializeFlexComponent(flexComponentParameters);

			Subscriber.Subscribe("SaveScrollPosition", SaveScrollPosition);
			Subscriber.Subscribe("RestoreScrollPosition", RestoreScrollPosition);
			SetSelectedRowHighlighting();
		}

		#endregion

		#region Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			components = new Container();
		}
		#endregion

		/// <summary>
		/// Save the location information for the current selection being made visible.
		/// </summary>
		/// <param name="rcIdeal">rectangle of selection</param>
		/// <param name="ydTop">current scroll position</param>
		protected override void SaveSelectionInfo(Rectangle rcIdeal, int ydTop)
		{
			m_ydSelTop = rcIdeal.Top;
			m_ydSelBottom = rcIdeal.Bottom;
			// The other two values are for validation.
			m_iSelIndex = SelectedIndex;
			m_ydSelScrollPos = ydTop;
		}

		#region IVwNotifyChange Members

		ICmObjectRepository m_repo;
		/// <summary />
		public void PropChanged(int hvo, int tag, int ivMin, int cvIns, int cvDel)
		{
			if (tag == MainTag)
			{
				// something has changed in the list we're displaying. make sure
				// our checkboxes are up to date, before we process OnPaint
				if (Vc != null && Vc.HasSelectColumn)
				{
					// there can be a circular dependency between checked items in a browse viewer
					// and the record list managing that list.
					// The record list can depend upon BulkEdit settings for loading its list
					// (e.g. ListItemsClass), so the bulk edit bar must load before the RecordList.
					// But, the BrowseViewer and BulkEditBar (Delete tab) also needs to know when
					// RecordList has been loaded, so that they can manage checkbox behavior for
					// the actual items being displayed.
					m_bv?.UpdateCheckedItems();
				}
				if (SelectedObject != m_hvoOldSel)
				{
					PropertyTable.GetValue<IFwMainWnd>(FwUtilsConstants.window).IdleQueue.Add(IdleQueuePriority.Medium, FireSelectionChanged);
				}
			}
			else if (RootBox != null && hvo > 0 && SelectedObject > 0)
			{
				// Check whether the changed object is either the selected object or owned
				// by the selected object.  If so, do a fake PropChanged that causes the whole
				// row to be regenerated. This ensures updating of virtual properties of the
				// object in other columns (e.g., when user opinion of this object changes,
				// the count of approved analyses should change).
				// This might become unnecessary if we can find a better way to generate
				// PropChanged when virtuals change.
				// See FWR-661.
				if (m_repo == null)
				{
					m_repo = Cache.ServiceLocator.GetInstance<ICmObjectRepository>();
				}
				if (!m_repo.TryGetObject(SelectedObject, out var objSel))
				{
					return;
				}
				if (!m_repo.TryGetObject(hvo, out var obj))
				{
					return;
				}
				if (obj == objSel || obj.OwnerOfClass(objSel.ClassID) == objSel)
				{
					// Reconstruct the current row (by pretending to replace the object),
					// preserving the selection if any (otherwise, the selection disappears
					// after each letter typed in a browse view...FWR-690).
					PropertyTable.GetValue<IFwMainWnd>(FwUtilsConstants.window).IdleQueue.Add(IdleQueuePriority.Medium, UpdateSelectedRow);
				}
			}
		}

		private bool UpdateSelectedRow(object args)
		{
			if (IsDisposed)
			{
				throw new InvalidOperationException("Thou shalt not call methods after I am disposed!");
			}
			if (RootBox == null)
			{
				return true; // presumably we've been disposed; this happens (at least) in tests where a later test may simulate idle events.
			}
			if (SelectedIndex < 0)
			{
				return true; // no current row to update
			}
			SelectionHelper helper = null;
			if (RootBox.Selection != null)
			{
				helper = SelectionHelper.Create(this);
			}
			RootBox.PropChanged(_hvoRoot, MainTag, SelectedIndex, 1, 1);
			helper?.MakeBest(false);
			return true; // we did it.
		}

		#endregion

		/// <summary>
		/// After we have our true size, make sure the interesting row is visible.
		/// </summary>
		public void PostLayoutInit()
		{
			if (RootBox == null || SelectedIndex < 0)
			{
				return;
			}
			MakeSelectionVisible(GetRowSelection(SelectedIndex), true, true, true);
		}

		/// <summary>
		/// Clear dangerous data values out of any of your decorated SDAs that require it.
		/// </summary>
		public void ClearValues()
		{
			var sda = SpecialCache;
			while (sda != null)
			{
				var cv = sda as IClearValues;
				cv?.ClearValues();
				var decorator = sda as DomainDataByFlidDecoratorBase;
				if (decorator == null)
				{
					break;
				}
				sda = decorator.BaseSda;
			}
		}

		/// <summary>
		/// SelectionRestorer used in the XmlBrowseViewBase to more accurately scroll the selection
		/// to the correct location.
		/// </summary>
		private sealed class XmlBrowseViewSelectionRestorer : SelectionRestorer
		{
			/// <summary />
			public XmlBrowseViewSelectionRestorer(XmlBrowseViewBase browseView) : base(browseView)
			{
			}

			/// <summary>
			/// Performs application-defined tasks associated with freeing, releasing, or resetting
			/// unmanaged resources.
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
					((XmlBrowseViewBase)MySimpleRootSite)?.RestoreScrollPosition(null);
				}
			}
		}
	}
}
