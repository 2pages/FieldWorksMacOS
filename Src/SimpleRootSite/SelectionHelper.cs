// Copyright (c) 2002-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel.Utils;
using System.Runtime.InteropServices;
using SIL.Code;
using SIL.LCModel.Core.KernelInterfaces;

namespace SIL.FieldWorks.Common.RootSites
{
	/// <summary>
	/// Wrapper for all the information that a text selection contains.
	/// </summary>
	[Serializable]
	public class SelectionHelper
	{
		#region Data members
		private SelInfo[] m_selInfo = new SelInfo[2];
		private int m_iTop = -1;
		private int m_ihvoEnd = -1;
		private bool m_fEndSet;
		/// <summary>Used for testing: holds SelectionHelper mock</summary>
		public static SelectionHelper s_mockedSelectionHelper = null;
		/// <summary>The distance the IP is from the top of the view</summary>
		[NonSerialized]
		protected int m_dyIPTop;
		[NonSerialized]
		private IVwRootSite m_rootSite;
		[NonSerialized]
		private IVwSelection m_vwSel;

		#endregion

		#region Construction and Initialization

		/// <summary>
		/// The default constructor must be followed by a call to SetSelection before it will
		/// really be useful
		/// </summary>
		public SelectionHelper()
		{
		}

		/// <summary>
		/// Create a selection helper based on an existing selection
		/// </summary>
		protected SelectionHelper(IVwSelection vwSel, IVwRootSite rootSite)
		{
			m_vwSel = vwSel;
			if (vwSel != null)
			{
				m_fEndSet = vwSel.IsRange;
			}
			RootSite = rootSite;
		}

		/// <summary>
		/// Copy constructor
		/// </summary>
		/// <param name="src">The source object</param>
		public SelectionHelper(SelectionHelper src)
		{
			m_selInfo[0] = new SelInfo(src.SelectionInfo[0]);
			m_selInfo[1] = new SelInfo(src.SelectionInfo[1]);
			m_iTop = src.m_iTop;
			m_ihvoEnd = src.m_ihvoEnd;
			m_vwSel = src.m_vwSel;
			RootSite = src.RootSite;
			m_fEndSet = src.m_fEndSet;
			m_dyIPTop = src.m_dyIPTop;
		}

		/// <summary>
		/// Create a SelectionHelper with the information about the current selection.
		/// </summary>
		/// <param name="rootSite">The root site</param>
		/// <returns>A new <see cref="SelectionHelper"/> object</returns>
		public static SelectionHelper Create(IVwRootSite rootSite)
		{
			return GetSelectionInfo(null, rootSite);
		}

		/// <summary>
		/// Create a SelectionHelper with the information about the current selection.
		/// </summary>
		/// <param name="vwSel">The selection to create a SelectionHelper object from or
		/// null to create it from the given RootSite</param>
		/// <param name="rootSite">The root site</param>
		/// <returns>A new <see cref="SelectionHelper"/> object</returns>
		public static SelectionHelper Create(IVwSelection vwSel, IVwRootSite rootSite)
		{
			return GetSelectionInfo(vwSel, rootSite);
		}

		/// <summary>
		/// Gets all information about a selection by calling <c>IVwSelection.AllTextSelInfo</c>.
		/// </summary>
		/// <param name="vwSel">The selection to get info for, or <c>null</c> to get current
		/// selection.</param>
		/// <param name="rootSite">The root site</param>
		/// <returns>A new <see cref="SelectionHelper"/> object</returns>
		public static SelectionHelper GetSelectionInfo(IVwSelection vwSel, IVwRootSite rootSite)
		{
			if (s_mockedSelectionHelper != null)
			{
				return s_mockedSelectionHelper;
			}
			if (vwSel == null || !vwSel.IsValid)
			{
				vwSel = rootSite?.RootBox?.Selection;
				if (vwSel == null || !vwSel.IsValid)
				{
					return null;
				}
			}
			var helper = new SelectionHelper(vwSel, rootSite);
			return !helper.GetSelEndInfo(false) ? null : !helper.GetSelEndInfo(true) ? null : helper;
		}

		/// <summary>
		/// Get information about the selection
		/// </summary>
		/// <param name="fEnd"><c>true</c> to get information about the end of the selection,
		/// otherwise <c>false</c>.</param>
		/// <returns><c>true</c> if information retrieved, otherwise <c>false</c>.</returns>
		private bool GetSelEndInfo(bool fEnd)
		{
			var i = fEnd ? 1 : 0;
			var cvsli = m_vwSel.CLevels(fEnd) - 1;
			if (cvsli < 0)
			{
				cvsli = 0;
			}
			if (m_selInfo[i] == null)
			{
				m_selInfo[i] = new SelInfo();
			}
			using (var prgvsli = MarshalEx.ArrayToNative<SelLevInfo>(cvsli))
			{
				m_vwSel.AllSelEndInfo(fEnd, out m_selInfo[i].ihvoRoot, cvsli, prgvsli,
					out m_selInfo[i].tagTextProp, out m_selInfo[i].cpropPrevious, out m_selInfo[i].ich,
					out m_selInfo[i].ws, out m_selInfo[i].fAssocPrev, out m_selInfo[i].ttpSelProps);
				m_selInfo[i].rgvsli = MarshalEx.NativeToArray<SelLevInfo>(prgvsli, cvsli);
			}

			if (fEnd)
			{
				m_fEndSet = true;
			}
			return true;
		}
		#endregion

		#region Methods to get selection properties

		/// <summary>
		/// Determines whether the specified flid is located in the level info for the selection
		/// </summary>
		/// <param name="flid">The flid.</param>
		/// <param name="limitType">Type of the limit.</param>
		/// <returns>true if the specified flid is found, false otherwise</returns>
		public bool IsFlidInLevelInfo(int flid, SelLimitType limitType)
		{
			var info = GetLevelInfo(limitType);
			for (var i = 0; i < info.Length; i++)
			{
				if (info[i].tag == flid)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Returns the selection level info for the specified tag found in the level info for
		/// this selection.
		/// NOTE: This version only searches the anchor for the specified tag.
		/// </summary>
		/// <param name="tag">The field tag to search for
		/// (i.e. BaseStText.StTextTags.kflidParagraphs)</param>
		/// <returns>The level info for the specified tag</returns>
		/// <exception cref="Exception">Thrown if the specified tag is not found in the level
		/// info for the Anchor of the selection</exception>
		public SelLevInfo GetLevelInfoForTag(int tag)
		{
			return GetLevelInfoForTag(tag, SelLimitType.Anchor);
		}

		/// <summary>
		/// Returns the selection level info for the specified tag found in the level info for
		/// this selection.
		/// </summary>
		/// <param name="tag">The field tag to search for
		/// (i.e. BaseStText.StTextTags.kflidParagraphs)</param>
		/// <param name="limitType">The limit of the selection to search for the specified tag
		/// </param>
		/// <returns>The level info for the specified tag</returns>
		/// <exception cref="Exception">Thrown if the specified tag is not found in the level
		/// info of the selection</exception>
		public SelLevInfo GetLevelInfoForTag(int tag, SelLimitType limitType)
		{
			return GetLevelInfoForTag(tag, limitType, out var selLevInfo) ? selLevInfo : throw new Exception("No selection level had the requested tag.");
		}

		/// <summary>
		/// Retrieves the selection level info for the specified tag found in the level info for
		/// this selection.
		/// NOTE: This version only searches the anchor for the specified tag.
		/// </summary>
		/// <param name="tag">The field tag to search for
		/// (i.e. BaseStText.StTextTags.kflidParagraphs)</param>
		/// <param name="selLevInfo">The level info for the specified tag, if found; undefined
		/// otherwise</param>
		/// <returns><c>true</c>if the specified tag is found; <c>false</c> otherwise</returns>
		public bool GetLevelInfoForTag(int tag, out SelLevInfo selLevInfo)
		{
			return GetLevelInfoForTag(tag, SelLimitType.Anchor, out selLevInfo);
		}

		/// <summary>
		/// Retrieves the selection level info for the specified tag found in the level info for
		/// this selection.
		/// </summary>
		/// <param name="tag">The field tag to search for
		/// (i.e. BaseStText.StTextTags.kflidParagraphs)</param>
		/// <param name="limitType">The limit of the selection to search for the specified tag
		/// </param>
		/// <param name="selLevInfo">The level info for the specified tag, if found; undefined
		/// otherwise</param>
		/// <returns><c>true</c>if the specified tag is found; <c>false</c> otherwise</returns>
		public bool GetLevelInfoForTag(int tag, SelLimitType limitType, out SelLevInfo selLevInfo)
		{
			var info = GetLevelInfo(limitType);
			for (var i = 0; i < info.Length; i++)
			{
				if (info[i].tag == tag)
				{
					selLevInfo = info[i];
					return true;
				}
			}
			selLevInfo = new SelLevInfo();
			return false;
		}

		/// <summary>
		/// Returns the level number of the specified tag for this selection.
		/// NOTE: This version only searches the anchor for the specified tag.
		/// </summary>
		/// <param name="tag">The field tag to search for
		/// (i.e. BaseStText.StTextTags.kflidParagraphs)</param>
		/// <returns>The level number of the specified tag for this selection, or -1 if it
		/// could not be found</returns>
		public int GetLevelForTag(int tag)
		{
			return GetLevelForTag(tag, SelLimitType.Anchor);
		}

		/// <summary>
		/// Returns the level number of the specified tag for this selection.
		/// </summary>
		/// <param name="tag">The field tag to search for
		/// (i.e. BaseStText.StTextTags.kflidParagraphs)</param>
		/// <param name="limitType">The limit of the selection to search for the specified tag
		/// </param>
		/// <returns>The level number of the specified tag for this selection, or -1 if it
		/// could not be found</returns>
		public int GetLevelForTag(int tag, SelLimitType limitType)
		{
			var info = GetLevelInfo(limitType);
			for (var i = 0; i < info.Length; i++)
			{
				if (info[i].tag == tag)
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>
		/// Gets the selection props for the current selection
		/// </summary>
		/// <param name="vttp">Returned array of ITsTextProps in the selection</param>
		/// <param name="vvps">Returned array of IVwPropertyStores in the selection</param>
		public void GetCurrSelectionProps(out ITsTextProps[] vttp, out IVwPropertyStore[] vvps)
		{
			GetSelectionProps(Selection, out vttp, out vvps, out _);
		}

		/// <summary>
		/// Get the location of the selection
		/// TODO: Make this work in PrintLayout view
		/// </summary>
		/// <returns>point on the screen of the anchor of the selection</returns>
		public Point GetLocation()
		{
			// TODO: what if selection anchor is off the screen?
			RootSite.GetGraphics(m_rootSite.RootBox, out var viewGraphics, out var rectangleSource, out var rectangleDestination);
			Rect rectanglePrimary;
			try
			{
				m_vwSel.Location(viewGraphics, rectangleSource, rectangleDestination, out rectanglePrimary, out _, out _, out _);

			}
			finally
			{
				RootSite.ReleaseGraphics(m_rootSite.RootBox, viewGraphics);
			}
			return new Point(rectanglePrimary.left, rectanglePrimary.top);
		}

		/// <summary>
		/// Gets the selection props for the specified IVwSelection
		/// </summary>
		/// <param name="vwSel">The view Selection</param>
		/// <param name="vttp">Returned array of ITsTextProps in the selection</param>
		/// <param name="vvps">Returned array of IVwPropertyStores in the selection</param>
		/// <param name="cttp">Returned count of TsTxtProps (this is basically just the number
		/// of runs in the selection)</param>
		public static void GetSelectionProps(IVwSelection vwSel, out ITsTextProps[] vttp, out IVwPropertyStore[] vvps, out int cttp)
		{
			Guard.AgainstNull(vwSel, nameof(vwSel));
			vttp = null;
			vvps = null;
			cttp = 0;
			if (!vwSel.IsValid)
			{
				return;
			}
			// The first call to GetSelectionProps gets the count of properties
			vwSel.GetSelectionProps(0, ArrayPtr.Null, ArrayPtr.Null, out cttp);
			if (cttp == 0)
			{
				return;
			}
			using (var pvTtp = MarshalEx.ArrayToNative<ITsTextProps>(cttp))
			using (var pvVps = MarshalEx.ArrayToNative<IVwPropertyStore>(cttp))
			{
				vwSel.GetSelectionProps(cttp, pvTtp, pvVps, out cttp);
				vttp = MarshalEx.NativeToArray<ITsTextProps>(pvTtp, cttp);
				vvps = MarshalEx.NativeToArray< IVwPropertyStore>(pvVps, cttp);
			}
		}

		/// <summary>
		/// This is like GetSelectionProps, except that the vvpsSoft items include all
		/// formatting MINUS the hard formatting in the text properties (ie, those derived by
		/// the view constructor and styles).
		/// </summary>
		/// <param name="vwSel">The view Selection</param>
		/// <param name="vttp">Returned array of ITsTextProps in the selection</param>
		/// <param name="vvpsSoft">Returned array of IVwPropertyStores in the selection</param>
		/// <param name="cttp">Returned count of properties</param>
		public static void GetHardAndSoftCharProps(IVwSelection vwSel, out ITsTextProps[] vttp, out IVwPropertyStore[] vvpsSoft, out int cttp)
		{
			vttp = null;
			vvpsSoft = null;
			GetSelectionProps(vwSel, out vttp, out vvpsSoft, out cttp);
			if (cttp == 0)
			{
				return;
			}
			using (var pvTtp = MarshalEx.ArrayToNative<ITsTextProps>(cttp))
			using (var pvVps = MarshalEx.ArrayToNative<IVwPropertyStore>(cttp))
			{
				vwSel.GetHardAndSoftCharProps(cttp, pvTtp, pvVps, out cttp);
				vttp = MarshalEx.NativeToArray<ITsTextProps>(pvTtp, cttp);
				vvpsSoft = MarshalEx.NativeToArray<IVwPropertyStore>(pvVps, cttp);
			}
		}

		/// <summary>
		/// Gets the paragraph properties for the specified IVwSelection
		/// </summary>
		/// <param name="vwSel">The view Selection</param>
		/// <param name="vvps">Returned array of IVwPropertyStores in the selection</param>
		/// <param name="cvps">Returned count of IVwPropertyStores</param>
		public static void GetParaProps(IVwSelection vwSel, out IVwPropertyStore[] vvps, out int cvps)
		{
			vvps = null;
			vwSel.GetParaProps(0, ArrayPtr.Null, out cvps);
			using (var arrayPtr = MarshalEx.ArrayToNative<IVwPropertyStore>(cvps))
			{
				vwSel.GetParaProps(cvps, arrayPtr, out cvps);
				vvps = MarshalEx.NativeToArray<IVwPropertyStore>(arrayPtr, cvps);
			}
		}

		/// <summary>
		/// Get the TsString of the property where the given selection limit is located. This
		/// gets the entire TSS of the paragraph, not just the selected portion.
		/// </summary>
		/// <param name="limit">the part of the selection where tss is to be retrieved (top,
		/// bottom, end, anchor)</param>
		/// <returns>
		/// the TsString containing the given limit of this Selection, or null if the selection
		/// is not in a paragraph.
		/// </returns>
		public virtual ITsString GetTss(SelLimitType limit)
		{
			try
			{
				Selection.TextSelInfo(IsEnd(limit), out var tss, out _, out _, out _, out _, out _);
				return tss;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Retrieve the language writing system used by the given selection. If the selection
		/// has no writing system or contains more than one writing system, zero is returned.
		/// </summary>
		/// <param name="vwsel">The selection</param>
		/// <returns>The writing system of the selection, or zero if selection is null or
		/// has no writing system or there are multiple writing systems in the selection.
		/// </returns>
		/// <remarks>ENHANCE JohnT (DaveO): This should this be a COM method for IVwSelection
		/// </remarks>
		public static int GetWsOfEntireSelection(IVwSelection vwsel)
		{
			return GetWsOfSelection(vwsel, false);
		}

		/// <summary>
		/// Retrieve the first language writing system used by the given selection.
		/// </summary>
		/// <param name="vwsel">The selection</param>
		/// <returns>The first writing system in the selection, or zero if selection is null or
		/// has no writing system</returns>
		/// <remarks>ENHANCE JohnT: Should this be a COM method for IVwSelection?</remarks>
		public static int GetFirstWsOfSelection(IVwSelection vwsel)
		{
			return GetWsOfSelection(vwsel, true);
		}

		/// <summary>
		/// This is the implementation for GetWsOfEntireSelection and GetFirstWsOfSelection. It
		/// retrieves the language writing system used by the given selection.
		/// </summary>
		/// <param name="vwsel">The selection</param>
		/// <param name="fStopAtFirstWs"><c>true</c> if caller only cares about the first ws
		/// found; <c>false</c> if the caller wants a ws that represents the whole selection
		/// (if any)</param>
		/// <returns>The first writing system in the selection, or 0 if we can't find a writing
		/// system or, if <paramref name="fStopAtFirstWs"/> is <c>false</c>, selection contains
		/// more then one writing system.</returns>
		/// <remarks>ENHANCE JohnT: Should this be a COM method for IVwSelection?</remarks>
		private static int GetWsOfSelection(IVwSelection vwsel, bool fStopAtFirstWs)
		{
			if (vwsel == null)
			{
				return 0;
			}
			try
			{
				var wsSaveFirst = -1;
				GetSelectionProps(vwsel, out var vttp, out _, out var cttp);
				if (cttp == 0)
				{
					return 0;
				}
				foreach (var ttp in vttp)
				{
					var ws = ttp.GetIntPropValues((int)FwTextPropType.ktptWs, out _);
					if (ws != -1)
					{
						if (fStopAtFirstWs)
						{
							return ws;
						}
						// This will set wsSave the first time we find a ws in a run
						if (wsSaveFirst == -1)
						{
							wsSaveFirst = ws;
						}
						else if (wsSaveFirst != ws)
						{
							// Multiple writing systems selected
							return 0;
						}
					}
				}
				// On the off chance we fund no writing systems at all, just return zero. It
				// should be safe because we can't have an editable selection where we would
				// really use the information.
				return wsSaveFirst != -1 ? wsSaveFirst : 0;
			}
			catch (ExternalException objException)
			{
				var msg = string.Format("{1} {2}{0}{0}{3}{0}{4}{0}{0}{5}{6}",
					Environment.NewLine,
					objException.ErrorCode,
					objException.GetBaseException().Source,
					objException.Message,
					objException.TargetSite,
					objException.StackTrace,
					objException.Source);
				Debug.Assert(false, msg);
				return 0;
			}
		}

		/// <summary>
		/// Determine if selection is editable
		/// </summary>
		/// <param name="sel">Selection</param>
		/// <returns><c>true</c> if selection is editable, otherwise <c>false</c>.</returns>
		public static bool IsEditable(IVwSelection sel)
		{
			return sel.CanFormatChar;
		}

		/// <summary>
		/// Determine if text that belongs to ttp and vps is editable.
		/// </summary>
		public static bool IsEditable(ITsTextProps ttp, IVwPropertyStore vps)
		{
			var nVal = -1;
			if (ttp != null)
			{
				nVal = ttp.GetIntPropValues((int)FwTextPropType.ktptEditable, out _);
			}
			if (nVal == -1 && vps != null)
			{
				nVal = vps.get_IntProperty((int)FwTextPropType.ktptEditable);
			}
			return nVal != (int)TptEditable.ktptNotEditable && nVal != (int)TptEditable.ktptSemiEditable;
		}
		#endregion

		#region ReduceSelectionToIp methods

		/// <summary>
		/// Reduce a range selection to a simple insertion point, specifying which limit of
		/// the range selection to use as the position for the new IP.
		/// </summary>
		/// <param name="rootSite">The root site</param>
		/// <param name="limit">Specify Top to place the IP at the top-most limit of the
		/// selection. Specify Bottom to place the IP at the bottom-most limit of the selection.
		/// Specify Anchor to place the IP at the point where the user initiated the selection.
		/// Specify End to place the IP at the point where the user completed the selection. Be
		/// aware the user may select text in either direction, thus the end of the selection\
		/// could be visually before the anchor. For a simple insertion point or a selection
		/// entirely within a single StText, this parameter doesn't actually make any
		/// difference.</param>
		/// <param name="fMakeVisible">Indicates whether to scroll the IP into view.</param>
		public static SelectionHelper ReduceSelectionToIp(IVwRootSite rootSite, SelLimitType limit, bool fMakeVisible)
		{
			return ReduceSelectionToIp(rootSite, limit, fMakeVisible, true);
		}

		/// <summary>
		/// Reduce a range selection to a simple insertion point, specifying which limit of
		/// the range selection to use as the position for the new IP.
		/// </summary>
		/// <param name="rootSite">The root site</param>
		/// <param name="limit">Specify Top to place the IP at the top-most limit of the
		/// selection. Specify Bottom to place the IP at the bottom-most limit of the selection.
		/// Specify Anchor to place the IP at the point where the user initiated the selection.
		/// Specify End to place the IP at the point where the user completed the selection. Be
		/// aware the user may select text in either direction, thus the end of the selection\
		/// could be visually before the anchor. For a simple insertion point or a selection
		/// entirely within a single StText, this parameter doesn't actually make any
		/// difference.</param>
		/// <param name="fMakeVisible">Indicates whether to scroll the IP into view.</param>
		/// <param name="fInstall">True to install the created selection, false otherwise</param>
		public static SelectionHelper ReduceSelectionToIp(IVwRootSite rootSite, SelLimitType limit, bool fMakeVisible, bool fInstall)
		{
			return (Create(rootSite))?.ReduceSelectionToIp(limit, fMakeVisible, fInstall);
		}

		/// <summary>
		/// Returns the contrary limit
		/// </summary>
		private SelLimitType ContraryLimit(SelLimitType limit)
		{
			switch(limit)
			{
				case SelLimitType.Anchor:
					return SelLimitType.End;
				case SelLimitType.End:
					return SelLimitType.Anchor;
				case SelLimitType.Top:
					return SelLimitType.Bottom;
				case SelLimitType.Bottom:
					return SelLimitType.Top;
			}
			return SelLimitType.Anchor;
		}

		/// <summary>
		/// Reduces this selection to an insertion point at the specified limit.
		/// Will not install or make visible.
		/// </summary>
		/// <param name="limit">The current selection limit to reduce to</param>
		public virtual void ReduceToIp(SelLimitType limit)
		{
			ReduceToIp(limit, false, false);
		}

		/// <summary>
		/// This is the workhorse that actually reduces a range selection to a simple insertion
		/// point, given the specified index to indicate the limit where the IP is to be
		/// created.
		/// </summary>
		/// <param name="limit">The current selection limit to reduce to</param>
		/// <param name="fMakeVisible">Indicates whether to scroll the IP into view.</param>
		/// <param name="fInstall">True to install the created selection, false otherwise</param>
		/// <returns>The selection that was created</returns>
		public virtual IVwSelection ReduceToIp(SelLimitType limit, bool fMakeVisible, bool fInstall)
		{
			var newLimit = ContraryLimit(limit);
			// set the information for the IP for the other limit
			SetTextPropId(newLimit, GetTextPropId(limit));
			SetNumberOfPreviousProps(newLimit, GetNumberOfPreviousProps(limit));
			SetIch(newLimit, GetIch(limit));
			m_fEndSet = false;
			SetIhvoRoot(newLimit, GetIhvoRoot(limit));
			SetAssocPrev(newLimit, GetAssocPrev(limit));
			SetWritingSystem(newLimit, GetWritingSystem(limit));
			SetNumberOfLevels(newLimit, GetNumberOfLevels(limit));
			SetLevelInfo(newLimit, GetLevelInfo(limit));
			return SetSelection(m_rootSite, fInstall, fMakeVisible);
		}

		/// <summary>
		/// Gets a (new) selection helper that represents an insertion point at the specified
		/// limit of this selection
		/// </summary>
		/// <param name="limit">Specify Top to place the IP at the top-most limit of the
		/// selection. Specify Bottom to place the IP at the bottom-most limit of the selection.
		/// Specify Anchor to place the IP at the point where the user initiated the selection.
		/// Specify End to place the IP at the point where the user completed the selection. Be
		/// aware the user may select text in either direction, thus the end of the selection\
		/// could be visually before the anchor. For a simple insertion point or a selection
		/// entirely within a single StText, this parameter doesn't actually make any
		/// difference.</param>
		/// <param name="fMakeVisible">Indicates whether to scroll the IP into view.</param>
		/// <param name="fInstall">True to install the created selection, false otherwise</param>
		public virtual SelectionHelper ReduceSelectionToIp(SelLimitType limit, bool fMakeVisible, bool fInstall)
		{
			var textSelHelper = new SelectionHelper(this);
			return textSelHelper.ReduceToIp(limit, fMakeVisible, fInstall) == null ? null : textSelHelper;
		}
		#endregion

		#region Methods to create and set selections

		/// <summary>
		/// Sets the selection by calling <c>IVwRootBox.MakeRangeSelection</c>.
		/// </summary>
		/// <param name="fInstall">Makes the selection the current selection</param>
		/// <param name="fMakeVisible">Determines whether or not to make the selection visible.
		/// </param>
		/// <returns>The selection</returns>
		public virtual IVwSelection SetSelection(bool fInstall, bool fMakeVisible)
		{
			return SetSelection(m_rootSite, fInstall, fMakeVisible);
		}

		/// <summary>
		/// Sets the selection by calling <c>IVwRootBox.MakeRangeSelection</c>.
		/// </summary>
		/// <param name="rootSite">The root site</param>
		/// <param name="fInstall">Makes the selection the current selection</param>
		/// <param name="fMakeVisible">Determines whether or not to make the selection visible.
		/// </param>
		/// <returns>The selection</returns>
		public virtual IVwSelection SetSelection(IVwRootSite rootSite, bool fInstall, bool fMakeVisible)
		{
			return SetSelection(rootSite, fInstall, fMakeVisible, VwScrollSelOpts.kssoDefault);
		}

		/// <summary>
		/// Sets the selection by calling <c>IVwRootBox.MakeRangeSelection</c>.
		/// </summary>
		/// <param name="rootSite">The root site</param>
		/// <param name="fInstall">Makes the selection the current selection</param>
		/// <param name="fMakeVisible">Determines whether or not to make the selection visible.
		/// </param>
		/// <param name="scrollOption">Where to scroll the selection</param>
		/// <returns>The selection, null if it could not return a valid one.</returns>
		public virtual IVwSelection SetSelection(IVwRootSite rootSite, bool fInstall, bool fMakeVisible, VwScrollSelOpts scrollOption)
		{
			if (rootSite?.RootBox == null)
			{
				return null;
			}
			m_rootSite = rootSite;
			try
			{
				var sel = MakeRangeSelection(rootSite.RootBox, fInstall);
				if (sel == null)
				{
					return null;
				}
				if (fInstall && !sel.IsValid)
				{
					// We rarely expect to have an invalid selection after we install a new selection,
					// but it's possible for selection side-effects to have invalidated it
					// (e.g. highlighting a row in a browse view cf. LT-5033.)
					sel = MakeRangeSelection(rootSite, true);
				}
				m_vwSel = sel.IsValid ? sel : null;
				if (fMakeVisible && m_vwSel != null)
				{
					rootSite.ScrollSelectionIntoView(m_vwSel, scrollOption);
				}
				return m_vwSel;
			}
			catch (COMException)
			{
				return null;
			}
			catch(Exception)
			{
				Debug.Assert(false, "Exceptional condition encountered while making selection.");
// ReSharper disable HeuristicUnreachableCode
				return null; //Um, asserts don't happen in release you overzealous Heuristic.
// ReSharper restore HeuristicUnreachableCode
			}
		}

		/// <summary>
		/// Make a range selection based upon our saved selection info.
		/// NOTE: Installing the selection may trigger side effects that will invalidate the selection.
		/// Callers should check to make sure the selection is still valid before using it.
		/// </summary>
		/// <param name="rootSite"></param>
		/// <param name="fInstall"></param>
		/// <exception cref="Exception">overload throws if unable to make an end selection</exception>
		/// <returns>a range selection (could become invalid as a side-effect of installing.)</returns>
		private IVwSelection MakeRangeSelection(IVwRootSite rootSite, bool fInstall)
		{
			return MakeRangeSelection(rootSite.RootBox, fInstall);
		}

		/// <summary>
		/// Make a range selection based upon our saved selection info.
		/// NOTE: Installing the selection may trigger side effects that will invalidate the selection.
		/// Callers should check to make sure the selection is still valid before using it.
		/// </summary>
		/// <param name="rootBox"></param>
		/// <param name="fInstall"></param>
		/// <exception cref="Exception">throws if unable to make an end selection</exception>
		/// <returns>a range selection (could become invalid as a side-effect of installing.)</returns>
		public IVwSelection MakeRangeSelection(IVwRootBox rootBox, bool fInstall)
		{
			const int iAnchor = 0;
			var iEnd = 1;
			if (!m_fEndSet)
			{
				// No end information set, so use iAnchor as end
				iEnd = iAnchor;
			}
			if (m_selInfo[iEnd] == null)
			{
				m_selInfo[iEnd] = new SelInfo(m_selInfo[iAnchor]);
			}
			// we want to pass fInstall=false to MakeTextSelection so that it doesn't notify
			// the RootSite of the selection change.
			IVwSelection vwSelAnchor;
			try
			{
				vwSelAnchor = rootBox.MakeTextSelection(
					m_selInfo[iAnchor].ihvoRoot, m_selInfo[iAnchor].rgvsli.Length,
					m_selInfo[iAnchor].rgvsli, m_selInfo[iAnchor].tagTextProp,
					m_selInfo[iAnchor].cpropPrevious, m_selInfo[iAnchor].ich, m_selInfo[iAnchor].ich,
					m_selInfo[iAnchor].ws, m_selInfo[iAnchor].fAssocPrev, m_selInfo[iAnchor].ihvoEnd,
					null, false);
			}
			catch (Exception e)
			{
				Debug.Assert(m_selInfo[iEnd].rgvsli.Length > 0 || m_selInfo[iAnchor].rgvsli.Length == 0, "Making the anchor selection failed, this is probably an empty editable field.");
				throw;
			}
			IVwSelection vwSelEnd;
			try
			{
				vwSelEnd = rootBox.MakeTextSelection(
					m_selInfo[iEnd].ihvoRoot, m_selInfo[iEnd].rgvsli.Length,
					m_selInfo[iEnd].rgvsli, m_selInfo[iEnd].tagTextProp,
					m_selInfo[iEnd].cpropPrevious, m_selInfo[iEnd].ich, m_selInfo[iEnd].ich,
					m_selInfo[iEnd].ws, m_selInfo[iEnd].fAssocPrev, m_selInfo[iEnd].ihvoEnd,
					null, false);

			}
			catch (Exception)
			{
				Debug.Assert(m_selInfo[iEnd].rgvsli.Length > 0 || m_selInfo[iAnchor].rgvsli.Length == 0, "The anchor has rgvsli but the end does not; since making the end selection failed, this is probably a mistake.");
				throw;
			}
			return rootBox.MakeRangeSelection(vwSelAnchor, vwSelEnd, fInstall);
		}

		/// <summary>
		/// Make an insertion point based upon our saved anchor selection info. It will not be
		/// set until after the unit of work.
		/// </summary>
		public virtual void SetIPAfterUOW()
		{
			if (RootSite == null)
			{
				throw new InvalidOperationException("This version of SetIPAfterUOW should only be called when the SelectionHelper has been initialized with a rootsite.");
			}
			SetIPAfterUOW(RootSite);
		}

		/// <summary>
		/// Make an insertion point based upon our saved anchor selection info. It will not be
		/// set until after the unit of work.
		/// </summary>
		/// <param name="rootSite"></param>
		public virtual void SetIPAfterUOW(IVwRootSite rootSite)
		{
			const int iAnchor = 0;
			rootSite.RequestSelectionAtEndOfUow(rootSite.RootBox,
				m_selInfo[iAnchor].ihvoRoot, m_selInfo[iAnchor].rgvsli.Length,
				m_selInfo[iAnchor].rgvsli, m_selInfo[iAnchor].tagTextProp,
				m_selInfo[iAnchor].cpropPrevious, m_selInfo[iAnchor].ich,
				m_selInfo[iAnchor].ws, m_selInfo[iAnchor].fAssocPrev,
				m_selInfo[iAnchor].ttpSelProps);
		}

		/// <summary>
		/// Sets the selection
		/// </summary>
		/// <param name="rootSite">The root site</param>
		/// <returns>The selection</returns>
		public virtual IVwSelection SetSelection(IVwRootSite rootSite)
		{
			return SetSelection(rootSite, true, true, VwScrollSelOpts.kssoDefault);
		}

		/// <summary>
		/// Attempt to find a complete word that the selection corresponds to.
		/// If it is a range, this is the word at its start.
		/// </summary>
		public ITsString SelectedWord
		{
			get
			{
				try
				{
					var ip = this;
					if (IsRange)
					{
						ip = ReduceSelectionToIp(SelLimitType.Top, false, false);
						if (ip == null) // Save an exception being thrown by checking explicitly.
						{
							return null;
						}
						ip.SetSelection(RootSite, false, false);
					}
					var selWord = ip.Selection.GrowToWord();
					if (selWord == null || !selWord.IsRange)
					{
						return null;
					}
					var wordHelper = SelectionHelper.Create(selWord, RootSite);
					var bldr = wordHelper.GetTss(SelLimitType.Anchor).GetBldr();
					var ichMin = Math.Min(wordHelper.IchAnchor, wordHelper.IchEnd);
					var ichLim = Math.Max(wordHelper.IchAnchor, wordHelper.IchEnd);
					if (ichLim < bldr.Length)
					{
						bldr.ReplaceTsString(ichLim, bldr.Length, null);
					}
					if (ichMin > 0)
					{
						bldr.ReplaceTsString(0, ichMin, null);
					}
					return bldr.GetString();
				}
				catch(Exception)
				{
					// If anything goes wrong, perhaps because we have some bizarre sort
					// of selection such as a picture, just give up getting a selected word.
					return null;
				}
			}
		}

		/// <summary>
		/// Sets and install the selection in the previously supplied rootsite.
		/// </summary>
		/// <param name="fMakeVisible">Indicates whether to scroll the selection into view
		/// </param>
		/// <returns>The selection</returns>
		public virtual IVwSelection SetSelection(bool fMakeVisible)
		{
			return SetSelection(m_rootSite, true, fMakeVisible, VwScrollSelOpts.kssoDefault);
		}

		/// <summary>
		/// The requested anchor and endpoint may be beyond the end of the string. Try to make
		/// a selection as near the end of the string as possible.
		/// </summary>
		/// <param name="fMakeVisible">Indicates whether to scroll the selection into view
		/// </param>
		/// <returns>The selection</returns>
		public virtual IVwSelection MakeBest(bool fMakeVisible)
		{
			return MakeBest(m_rootSite, fMakeVisible);
		}

		/// <summary>
		/// The requested anchor and endpoint may be beyond the end of the string. Try to make
		/// a selection as near the end of the string as possible.
		/// </summary>
		/// <param name="fMakeVisible">Indicates whether to scroll the selection into view
		/// </param>
		/// <param name="rootsite">The rootsite that will try take the selection</param>
		/// <returns>The selection</returns>
		public virtual IVwSelection MakeBest(IVwRootSite rootsite, bool fMakeVisible)
		{
			// Try setting original selection
			var vwsel = SetSelection(rootsite, true, fMakeVisible, VwScrollSelOpts.kssoDefault);
			if (vwsel != null)
			{
				return vwsel;
			}
			// Otherwise try endpoint = anchor (if the endpoint is set)
			if (m_fEndSet)
			{
				try
				{
					if (m_selInfo[1] == null || m_selInfo[0] < m_selInfo[1])
					{
						m_selInfo[1] = m_selInfo[0];
					}
					else
					{
						m_selInfo[0] = m_selInfo[1];
					}
				}
				catch (ArgumentException)
				{
					// comparison failed due to selection points being at different text levels,
					// e.g., section heading and section content. Assume first selection point
					// is top
					m_selInfo[1] = m_selInfo[0];
				}
				vwsel = SetSelection(rootsite, true, fMakeVisible);
				if (vwsel != null)
				{
					return vwsel;
				}
			}
			// If we can't find a selection try to create a selection at the end of the
			// current paragraph.
			IchAnchor = 0;
			var sel = SetSelection(rootsite, false, false);
			if (sel == null)
			{
				return null;
			}
			sel.TextSelInfo(false, out var tss, out _, out _, out _, out _, out _);
			if (tss != null)
			{
				IchAnchor = tss.Length;
			}
			return SetSelection(rootsite, true, fMakeVisible, VwScrollSelOpts.kssoDefault);
		}

		/// <summary>
		/// Makes a selection then scrolls the window so the IP is at the same vertical
		/// position it was when this selection helper object was created. This method is
		/// used mainly after reconstructing a view. After reconstruction, the desire is to
		/// not only have the IP back in the data where it was before reconstruction, but to
		/// have the same number of pixels between the IP and the top of the view.
		/// This method does it's best to do this.
		/// </summary>
		/// <returns>True if a selection could be made (regardless of its accuracy.
		/// Otherwise, false.</returns>
		public virtual bool RestoreSelectionAndScrollPos()
		{
			if (RootSite == null)
			{
				return false;
			}
			// Try to restore the selection as best as possible.
			if (MakeBest(true) == null)
			{
				return false;
			}
			RestoreScrollPos();
			return true;
		}

		/// <summary>
		/// Scrolls the selection to its original scroll position.
		/// </summary>
		public void RestoreScrollPos()
		{
			var site = RootSite as IRootSite;
			if (!Selection.IsValid)
			{
				SetSelection(false, false);
				if (Selection == null || !Selection.IsValid)
				{
					return; // apparently we can't successfully make an equivalent selection.
				}
			}
			site?.ScrollSelectionToLocation(Selection, m_dyIPTop);
		}

		/// <summary>
		/// Removes the selection level for the specified tag.
		/// </summary>
		public void RemoveLevel(int tag)
		{
			RemoveLevel(tag, SelLimitType.Anchor);
			RemoveLevel(tag, SelLimitType.End);
		}

		/// <summary>
		/// Removes the selection level for the specified tag.
		/// </summary>
		/// <param name="tag">The tag.</param>
		/// <param name="type">Anchor or End</param>
		private void RemoveLevel(int tag, SelLimitType type)
		{
			RemoveLevelAt(GetLevelForTag(tag, type), type);
		}

		private void RemoveLevelAt(int iLevel, SelLimitType type)
		{
			if (iLevel < 0)
			{
				return;
			}
			var levInfo = GetLevelInfo(type);
			var temp = new List<SelLevInfo>(levInfo);
			temp.RemoveAt(iLevel);
			SetLevelInfo(type, temp.ToArray());
		}

		/// <summary>
		/// Remove the specified level from the SelLevInfo for both ends.
		/// </summary>
		public void RemoveLevelAt(int ilev)
		{
			RemoveLevelAt(ilev, SelLimitType.Anchor);
			RemoveLevelAt(ilev, SelLimitType.End);
		}

		/// <summary>
		/// Appends a selection level for the specified tag.
		/// </summary>
		/// <param name="iLev">Index at which the level is to be inserted.</param>
		/// <param name="tag">The tag.</param>
		/// <param name="ihvo">The index of the object to insert in the appended level</param>
		/// <param name="ws">HVO of the writing system, if the property is a Multitext</param>
		public void InsertLevel(int iLev, int tag, int ihvo, int ws)
		{
			InsertLevel(iLev, tag, ihvo, ws, SelLimitType.Anchor);
			InsertLevel(iLev, tag, ihvo, ws, SelLimitType.End);
		}

		/// <summary>
		/// Appends a selection level for the specified tag.
		/// </summary>
		/// <param name="iLev">Index at which the level is to be inserted.</param>
		/// <param name="tag">The tag.</param>
		/// <param name="ihvo">The index of the object to insert in the appended level</param>
		/// <param name="type">Anchor or End</param>
		/// <param name="ws">HVO of the writing system, if the property is a Multitext</param>
		private void InsertLevel(int iLev, int tag, int ihvo, int ws, SelLimitType type)
		{
			var levInfo = GetLevelInfo(type);
			var temp = new List<SelLevInfo>(levInfo);
			var level = new SelLevInfo
			{
				tag = tag,
				ihvo = ihvo,
				cpropPrevious = 0,
				ws = ws
			};
			temp.Insert(iLev, level);
			SetLevelInfo(type, temp.ToArray());
		}

		/// <summary>
		/// Appends a selection level for the specified tag.
		/// </summary>
		/// <param name="tag">The tag.</param>
		/// <param name="ihvo">The index of the object to insert in the appended level</param>
		/// <param name="ws">HVO of the writing system, if the property is a Multitext</param>
		public void AppendLevel(int tag, int ihvo, int ws)
		{
			AppendLevel(tag, ihvo, ws, SelLimitType.Anchor);
			AppendLevel(tag, ihvo, ws, SelLimitType.End);
		}

		/// <summary>
		/// Appends a selection level for the specified tag.
		/// </summary>
		/// <param name="tag">The tag.</param>
		/// <param name="ihvo">The index of the object to insert in the appended level</param>
		/// <param name="type">Anchor or End</param>
		/// <param name="ws">HVO of the writing system, if the property is a Multitext</param>
		private void AppendLevel(int tag, int ihvo, int ws, SelLimitType type)
		{
			InsertLevel(GetNumberOfLevels(type), tag, ihvo, ws, type);
		}

		/// <summary>
		/// Updates the internal scroll location for this selection to be up-to-date,
		/// if possible.
		/// </summary>
		public void UpdateScrollLocation()
		{
			try
			{
				if (m_rootSite != null && m_rootSite is SimpleRootSite rootSite)
				{
					m_dyIPTop = rootSite.IPDistanceFromWindowTop(m_vwSel ?? m_rootSite.RootBox.Selection);
				}
			}
			catch
			{
				// ignore and go on with life...
			}
		}
		#endregion

		#region Private Properties
		/// <summary>
		/// The selection helper stores two sets of selection levels: one for the anchor
		/// (index 0) and one for the end (index 1). This property returns either 0 or 1
		/// (corresponding either to the anchor or the end), depending on whether the selection
		/// was made top-down or bottom-up. If it is bottom-up (i.e., if the end of the
		/// selection is higher in the view than the anchor), then the top index is the index of
		/// the end, so this property returns 1; otherwise, it returns 0. If there is no
		/// selection at all, then this arbitrarily returns 0;
		/// </summary>
		private int TopIndex
		{
			get
			{
				if (m_vwSel == null)
				{
					return 0;
				}
				if (m_iTop < 0)
				{
					m_iTop = m_vwSel.EndBeforeAnchor ? 1 : 0;
				}
				return m_iTop;
			}
		}

		/// <summary>
		/// The selection helper stores two sets of selection levels: one for the anchor
		/// (index 0) and one for the end (index 1). This property returns either 0 or 1
		/// (corresponding either to the anchor or the end), depending on whether the selection
		/// was made top-down or bottom-up. If it is bottom-up (i.e., if the end of the
		/// selection is higher in the view than the anchor), then the bottom index is the index
		/// of the end, so this property returns 0; otherwise, it returns 1. If there is no
		/// selection at all, then this arbitrarily returns 1;
		/// </summary>
		private int BottomIndex => TopIndex == 1 ? 0 : 1;

		/// <summary>
		/// Gets the information about the selection
		/// </summary>
		protected internal SelInfo[] SelectionInfo => m_selInfo;

		/// <summary>
		/// Returns the index used for the appropriate limit type
		/// </summary>
		/// <param name="type">Limit type</param>
		/// <returns>Index</returns>
		private int GetIndex(SelLimitType type)
		{
			int i;
			switch (type)
			{
				case SelLimitType.Anchor:
					i = 0;
					break;
				case SelLimitType.End:
					i = 1;
					break;
				case SelLimitType.Top:
					i = TopIndex;
					break;
				case SelLimitType.Bottom:
					i = BottomIndex;
					break;
				default:
					throw new ArgumentOutOfRangeException("Got unexpected SelLimitType");
			}
			if (m_selInfo[i] == null)
			{
				m_selInfo[i] = new SelInfo(m_selInfo[i == 0 ? 1 : 0]);
			}
			return i;
		}

		/// <summary>
		/// Determines whether the specified type is the end.
		/// </summary>
		/// <param name="type">Limit type.</param>
		/// <returns>
		/// 	<c>true</c> if the specified type is the end; otherwise, <c>false</c>.
		/// </returns>
		private bool IsEnd(SelLimitType type)
		{
			return (GetIndex(type) == 1);
		}
		#endregion

		#region Properties

		/// <summary>
		/// Gets whether or not the selection is a range selection
		/// </summary>
		public virtual bool IsRange
		{
			get
			{
				// Its possible that the selection extents were changed and the SelectionHelper
				// wasn't updated to reflect those changes. (Example is to double-click a word
				// in TE. This creates a range selection without updating the selection
				// information in the SelectionHelper)
				if (Selection != null)
				{
					return Selection.IsRange;
				}
				var anchorLev = GetLevelInfo(SelLimitType.Anchor);
				var endLev = GetLevelInfo(SelLimitType.End);
				return IchAnchor != IchEnd || anchorLev[0] != endLev[0] || GetNumberOfLevels(SelLimitType.Anchor) != GetNumberOfLevels(SelLimitType.End);
			}
		}

		/// <summary>
		/// Gets whether or not the selection is valid
		/// </summary>
		public virtual bool IsValid => (Selection != null && Selection.IsValid);

		/// <summary>
		/// Gets a value indicating whether this selection is visible.
		/// </summary>
		/// <value>
		/// 	<c>true</c> if this instance is visible; otherwise, <c>false</c>. Also
		/// <c>false</c> if no rootsite is set or root site isn't derived from SimpleRootSite.
		/// </value>
		public bool IsVisible => RootSite is SimpleRootSite && ((SimpleRootSite)RootSite).IsSelectionVisible(Selection);

		/// <summary>
		/// Gets the selection that this <see cref="SelectionHelper"/> represents.
		/// </summary>
		public virtual IVwSelection Selection => m_vwSel;

		/// <summary>
		/// Gets or sets the rootsite
		/// </summary>
		public virtual IVwRootSite RootSite
		{
			get => m_rootSite;
			set
			{
				if (m_rootSite is Control control)
				{
					control.Disposed -= OnRootSiteDisposed;
				}
				m_rootSite = value;
				if (m_rootSite is Control rootSite)
				{
					rootSite.Disposed += OnRootSiteDisposed;
				}
				UpdateScrollLocation();
			}
		}

		/// <summary>
		/// Called when the root site gets disposed. We can't use m_rootSite any more.
		/// </summary>
		/// <param name="sender">The sender.</param>
		/// <param name="e">The <see cref="T:System.EventArgs"/> instance containing the event
		/// data.</param>
		private void OnRootSiteDisposed(object sender, EventArgs e)
		{
			m_rootSite = null;
		}

		/// <summary>
		/// Gets the number of levels needed to traverse the view objects to reach the
		/// given limit of the selection.
		/// </summary>
		public virtual int GetNumberOfLevels(SelLimitType type)
		{
			return GetSelInfo(type).rgvsli.Length;
		}

		/// <summary>
		/// Sets the number of levels needed to traverse the view objects to reach the
		/// given limit of the selection.
		/// </summary>
		public virtual void SetNumberOfLevels(SelLimitType type, int value)
		{
			var iType = GetIndex(type);
			m_selInfo[iType].rgvsli = new SelLevInfo[value];
			for (var i = 0; i < value; i++)
			{
				m_selInfo[iType].rgvsli[i] = new SelLevInfo();
			}
		}

		/// <summary>
		/// Gets or sets the number of levels needed to traverse the view objects to reach the
		/// selected object(s).
		/// </summary>
		public virtual int NumberOfLevels
		{
			get => GetNumberOfLevels(SelLimitType.Anchor);
			set => SetNumberOfLevels(SelLimitType.Anchor, value);
		}

		/// <summary>
		/// Gets the internal selection info for the requested end of the selection.
		/// </summary>
		/// <param name="type">Anchor or End</param>
		private SelInfo GetSelInfo(SelLimitType type)
		{
			return m_selInfo[GetIndex(type)];
		}

		/// <summary>
		/// Gets the index of the root object for the given limit of the selection. This
		/// is 0 for views that don't display multiple root objects).
		/// </summary>
		/// <param name="type">Anchor or End</param>
		public virtual int GetIhvoRoot(SelLimitType type)
		{
			return GetSelInfo(type).ihvoRoot;
		}

		/// <summary>
		/// Sets the index of the root object for the given limit of the selection. This
		/// is 0 for views that don't display multiple root objects).
		/// </summary>
		public virtual void SetIhvoRoot(SelLimitType type, int value)
		{
			GetSelInfo(type).ihvoRoot = value;
		}

		/// <summary>
		/// Gets or sets the index of the root object (for views that display multiple root
		/// objects). Default is 0.
		/// </summary>
		public virtual int IhvoRoot
		{
			get => GetIhvoRoot(SelLimitType.Anchor);
			set => SetIhvoRoot(SelLimitType.Anchor, value);
		}

		/// <summary>
		/// Gets or sets the tag/flid of the of the property containing the actual text in
		/// which the selection occurs (this is the lowest level of the selection -- it is NOT
		/// contained in the SelLevelInfo array). Default is 0, so it must be set explicitly
		/// unless the SelectionHelper is being created from an existing selection.
		/// </summary>
		public int TextPropId
		{
			get => GetTextPropId(SelLimitType.Anchor);
			set => SetTextPropId(SelLimitType.Anchor, value);
		}

		/// <summary>
		/// Gets the number of previous elements for the given limit of the selection
		/// </summary>
		/// <param name="type">Anchor or End</param>
		public virtual int GetNumberOfPreviousProps(SelLimitType type)
		{
			return GetSelInfo(type).cpropPrevious;
		}

		/// <summary>
		/// Gets the text property that occurs at the indicated end of the selection.
		/// </summary>
		public virtual int GetTextPropId(SelLimitType type)
		{
			return GetSelInfo(type).tagTextProp;
		}

		/// <summary>
		/// Sets the text property that occurs at the indicated end of the selection.
		/// </summary>
		/// <param name="type">Anchor or End</param>
		/// <param name="tagTextProp">Text property</param>
		public void SetTextPropId(SelLimitType type, int tagTextProp)
		{
			GetSelInfo(type).tagTextProp = tagTextProp;
		}

		/// <summary>
		/// Sets the number of previous elements for the given limit of the selection
		/// </summary>
		public virtual void SetNumberOfPreviousProps(SelLimitType type, int value)
		{
			GetSelInfo(type).cpropPrevious = value;
		}

		/// <summary>
		/// Gets or sets the number of previous elements for the given limit of the selection
		/// </summary>
		public virtual int NumberOfPreviousProps
		{
			get => GetNumberOfPreviousProps(SelLimitType.Anchor);
			set => SetNumberOfPreviousProps(SelLimitType.Anchor, value);
		}

		/// <summary>
		/// Gets the 0-based index of the character for the given limit of the selection.
		/// </summary>
		public virtual int GetIch(SelLimitType type)
		{
			return GetSelInfo(type).ich;
		}

		/// <summary>
		/// Sets the 0-based index of the character for the given limit of the selection.
		/// If appropriate records that the end of the selection has been set explicitly.
		/// </summary>
		public virtual void SetIch(SelLimitType type, int value)
		{
			if (type == SelLimitType.End)
			{
				m_fEndSet = true;
			}
			GetSelInfo(type).ich = value;
			// Force recalculation of top/bottom of selection
			m_iTop = -1;
		}

		/// <summary>
		/// Gets or sets the 0-based index of the character at which the selection begins (or
		/// before which the insertion point is to be placed if IchAnchor == IchEnd)
		/// </summary>
		/// <remarks>Note that if IchAnchor==IchEnd, setting IchAnchor will effectively move
		/// the end as well. Set IchEnd (and thereby m_fEndSet) if you intend to make a range
		/// selection!</remarks>
		public virtual int IchAnchor
		{
			get => GetIch(SelLimitType.Anchor);
			set => SetIch(SelLimitType.Anchor, value);
		}

		/// <summary>
		/// Gets or sets the character location to end the selection. Should be un-set or set
		/// equal to IchAnchor for a simple insertion point.
		/// </summary>
		public virtual int IchEnd
		{
			get => GetIch(SelLimitType.End);
			set => SetIch(SelLimitType.End, value);
		}

		/// <summary>
		/// Get the writing system associated with the insertion point.
		/// <p>Note: If you need the writing system for the selection, you should
		/// use <see cref="GetFirstWsOfSelection"/>.</p>
		/// </summary>
		/// <param name="type">Which end of the selection</param>
		/// <returns>Writing system</returns>
		public virtual int GetWritingSystem(SelLimitType type)
		{
			return GetSelInfo(type).ws;
		}

		/// <summary>
		/// Set the writing system associated with the insertion point.
		/// <p>Note: If you need the writing system for the selection, you should
		/// use <see cref="GetFirstWsOfSelection"/>.</p>
		/// </summary>
		/// <param name="type">Which end of the selection</param>
		/// <param name="value">Writing system</param>
		public virtual void SetWritingSystem(SelLimitType type, int value)
		{
			GetSelInfo(type).ws = value;
		}

		/// <summary>
		/// Gets or sets the writing system associated with the insertion point.
		/// <p>Note: If you need the writing system for the selection, you should
		/// use <see cref="GetFirstWsOfSelection"/>.</p>
		/// </summary>
		public virtual int Ws
		{
			get => GetWritingSystem(SelLimitType.Anchor);
			set => SetWritingSystem(SelLimitType.Anchor, value);
		}

		/// <summary>
		/// Get the text props associated with the given end of the selection.
		/// </summary>
		/// <param name="type">Which end of the selection</param>
		/// <returns>Text props associated with the given end of the selection</returns>
		public virtual ITsTextProps GetSelProps(SelLimitType type)
		{
			return GetSelInfo(type).ttpSelProps;
		}

		/// <summary>
		/// Set the text props associated with the given end of the selection.
		/// </summary>
		/// <param name="type">Which end of the selection</param>
		/// <param name="value">Properties to set for the selection</param>
		public virtual void SetSelProps(SelLimitType type, ITsTextProps value)
		{
			GetSelInfo(type).ttpSelProps = value;
		}

		/// <summary>
		/// Gets or sets the text properties associated with the insertion point. If selection
		/// is a range, we're not sure you should necessarily even be doing this. This is
		/// probably relevant only for insertion points.
		/// </summary>
		public virtual ITsTextProps SelProps
		{
			get => GetSelProps(SelLimitType.End);
			set => SetSelProps(SelLimitType.End, value);
		}

		/// <summary>
		/// Get the text props for the text immediately before top of the selection.
		/// </summary>
		/// <returns>Text props associated with the character immediately before the top of the
		/// selection, or null if there is no preceding character</returns>
		public ITsTextProps PropsBefore
		{
			get
			{
				var ichTop = GetIch(SelLimitType.Top);
				return ichTop <= 0 ? null : (GetTss(SelLimitType.Top))?.get_PropertiesAt(ichTop - 1);
			}
		}

		/// <summary>
		/// Get the text props for the text immediately after the bottom of the selection.
		/// </summary>
		/// <returns>Text props associated with the charcter immediately after the bottom of the
		/// selection, or null if there is no following character</returns>
		public ITsTextProps PropsAfter
		{
			get
			{
				var tss = GetTss(SelLimitType.Bottom);
				var ichBottom = GetIch(SelLimitType.Bottom);
				return tss == null || ichBottom >= tss.Length ? null : tss.get_PropertiesAt(ichBottom);
			}
		}

		/// <summary>
		/// Indicates whether of not the insertion point should be associated with the
		/// characters immediately preceding it in the view (default) or not.
		/// </summary>
		/// <param name="type">Which end of the selection</param>
		/// <returns><c>true</c> to associate IP with preceding characters, otherwise
		/// <c>false</c></returns>
		public virtual bool GetAssocPrev(SelLimitType type)
		{
			return GetSelInfo(type).fAssocPrev;
		}

		/// <summary>
		/// Indicates whether of not the insertion point should be associated with the
		/// characters immediately preceding it in the view (default) or not.
		/// </summary>
		/// <param name="type">Which end of the selection</param>
		/// <param name="value"><c>true</c> to associate IP with preceding characters, otherwise
		/// <c>false</c></param>
		public virtual void SetAssocPrev(SelLimitType type, bool value)
		{
			GetSelInfo(type).fAssocPrev = value;
		}

		/// <summary>
		/// Indicates whether of not the insertion point should be associated with the
		/// characters immediately preceding it in the view (default) or not.
		/// </summary>
		public virtual bool AssocPrev
		{
			get => GetAssocPrev(SelLimitType.Anchor);
			set => SetAssocPrev(SelLimitType.Anchor, value);
		}

		/// <summary>
		/// Gets the array of VwSelLevInfo. Array elements should indicate the chain of
		/// objects that needs to be traversed to get from the root object to object where the
		/// selection is to be made. The tag for item n should be the flid in which the
		/// children of the root object are owned. The 0th element of this array must have
		/// its tag value set to BaseStText.StTextTags.kflidParagraphs. This is set
		/// automatically whenever the array is resized using Cvsli.
		/// </summary>
		/// <param name="type">type</param>
		/// <returns>The level info</returns>
		public virtual SelLevInfo[] GetLevelInfo(SelLimitType type)
		{
			return GetSelInfo(type).rgvsli;
		}

		/// <summary>
		/// Sets the array of SelLevInfo.
		/// </summary>
		/// <param name="type">type</param>
		/// <param name="value">The level info</param>
		public virtual void SetLevelInfo(SelLimitType type, SelLevInfo[] value)
		{
			GetSelInfo(type).rgvsli = value;
		}

		/// <summary>
		/// Gets the array of VwSelLevInfo. Array elements should indicate the chain of
		/// objects that needs to be traversed to get from the root object to object where the
		/// selection is to be made. The tag for item n should be the flid in which the
		/// children of the root object are owned. The 0th element of this array must have
		/// its tag value set to BaseStText.StTextTags.kflidParagraphs. This is set
		/// automatically whenever the array is resized using Cvsli.
		/// </summary>
		public virtual SelLevInfo[] LevelInfo => GetLevelInfo(SelLimitType.Anchor);
		#endregion
	}
}
