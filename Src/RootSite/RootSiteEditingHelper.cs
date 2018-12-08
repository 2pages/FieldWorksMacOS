// Copyright (c) 2005-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.FieldWorks.Resources;
using SIL.LCModel;
using SIL.LCModel.Application;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.SpellChecking;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;
using SIL.LCModel.Utils;

namespace SIL.FieldWorks.Common.RootSites
{
	/// <summary />
	public class RootSiteEditingHelper : EditingHelper
	{
		#region Constants
		/// <summary>maximum number of spelling suggestions that will appear in the
		/// root level of the context menu. Additional suggestions will appear in a submenu.
		/// </summary>
		internal static int kMaxSpellingSuggestionsInRootMenu = 7;
		#endregion

		#region Member variables
		/// <summary>The LCM cache</summary>
		protected LcmCache m_cache;
		private SpellCheckHelper m_spellCheckHelper;
		private int m_undoCountBeforeMerge;
		#endregion

		#region Delegates
		/// <summary>Delegate for when a footnote is found in HandleFootnoteAnchorIconSelected</summary>
		protected delegate void FootnoteAnchorFoundDelegate(int hvo, int flid, int ws, int ich);
		#endregion

		/// <summary />
		/// <param name="cache">The LCM Cache</param>
		/// <param name="callbacks">implementation of <see cref="IEditingCallbacks"/></param>
		public RootSiteEditingHelper(LcmCache cache, IEditingCallbacks callbacks)
			: base(callbacks)
		{
			Cache = cache;
		}

		#region Properties
		/// <summary>
		/// Setter, normally only used if the client view's cache was not yet set at the time
		/// of creating the editing helper.
		/// </summary>
		public LcmCache Cache
		{
			get { return m_cache; }
			internal set
			{
				m_cache = value;
			}
		}

		/// <summary>
		/// Gets whether the current selection is in back translation data.
		/// </summary>
		public virtual bool IsBackTranslation
		{
			get
			{
				var helper = CurrentSelection;
				return helper != null && helper.TextPropId == SegmentTags.kflidFreeTranslation;
			}
		}

		/// <summary>
		/// Gets the writing system factory
		/// </summary>
		protected override ILgWritingSystemFactory WritingSystemFactory => m_cache == null ? base.WritingSystemFactory : m_cache.WritingSystemFactory;
		#endregion

		#region Spelling stuff

		/// <summary>
		/// Gets (creating if necessary) the SpellCheckHelper
		/// </summary>
		private SpellCheckHelper SpellCheckHelper => m_spellCheckHelper ?? (m_spellCheckHelper = new SpellCheckHelper(Cache));

		/// <summary>
		/// Gets a value indicating what the status of the spell checking system is for the
		/// sake of the the context menu that's being shown in the ShowContextMenu method.
		/// This property is only valid when the context menu popped-up in that method is
		/// in the process of being shown. This property is used for the SimpleRootSites who
		/// need to handle the update messages for those spelling options.
		/// </summary>
		public SpellCheckStatus SpellCheckingStatus { get; } = SpellCheckStatus.Disabled;

#if RANDYTODO
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Displays the specified context menu for the specified rootsite at the specified
		/// mouse position. This will also determine whether or not to include the spelling
		/// correction options.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void ShowContextMenu(Point mousePos, ITMAdapter tmAdapter,
			RootSite rootsite, string contextMenuName, string addToDictMenuName,
			string insertBeforeMenuName, string changeMultipleMenuName, bool fShowSpellingOptions)
		{
			m_spellCheckStatus = SpellCheckStatus.Disabled;
			List<string> menuItemNames = null;

			if (fShowSpellingOptions)
			{
				Debug.Assert(rootsite.RootBox == Callbacks.EditedRootBox);
				menuItemNames = MakeSpellCheckMenuOptions(mousePos, rootsite, tmAdapter,
					contextMenuName, addToDictMenuName, insertBeforeMenuName,
					changeMultipleMenuName);

				m_spellCheckStatus = (menuItemNames == null ?
					SpellCheckStatus.WordInDictionary : SpellCheckStatus.Enabled);
			}

			Point pt = rootsite.PointToScreen(mousePos);
			tmAdapter.PopupMenu(contextMenuName, pt.X, pt.Y, menuItemNames);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make spell checking menu options using the DotNetBar adapter.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private List<string> MakeSpellCheckMenuOptions(Point mousePos, RootSite rootsite,
			ITMAdapter tmAdapter, string menuName, string addToDictMenuName,
			string changeMultipleMenuName, string insertBeforeMenuName)
		{
			int hvoObj, tag, wsAlt, wsText;
			string word;
			ISpellEngine dict;
			bool nonSpellingError;
			ICollection<SpellCorrectMenuItem> suggestions = SpellCheckHelper.GetSuggestions(mousePos, rootsite,
				out hvoObj, out tag, out wsAlt, out wsText, out word, out dict, out nonSpellingError);

			IVwRootBox rootb = rootsite.RootBox;
			// These two menu items are disabled for non-spelling errors. In addition, for addToDict, we need
			// to set the tag to an AddToDictMenuItem which can actually do the work.
			UpdateItemProps(tmAdapter, addToDictMenuName, nonSpellingError, new AddToDictMenuItem(dict, word, rootb,
				hvoObj, tag, wsAlt, wsText, RootSiteStrings.ksAddToDictionary, Cache));
			// any non-null value of tag will indicate the item should be enabled, tested in TeMainWnd.UpdateSpellingMenus.
			UpdateItemProps(tmAdapter, changeMultipleMenuName, nonSpellingError, "ok to change");

			if (suggestions == null)
				return null;

			// Make the menu options.
			List<string> menuItemNames = new List<string>();
			TMItemProperties itemProps;
			if (suggestions.Count == 0)
			{
				itemProps = new TMItemProperties();
				itemProps.Name = "noSpellingSuggestion";
				itemProps.Text = RootSiteStrings.ksNoSuggestions;
				itemProps.Enabled = false;
				menuItemNames.Add(itemProps.Name);
				tmAdapter.AddContextMenuItem(itemProps, menuName, insertBeforeMenuName);
			}

			int cSuggestions = 0;
			string additionalSuggestionsMenuName = "additionalSpellSuggestion";

			foreach (SpellCorrectMenuItem scmi in suggestions)
			{
				itemProps = new TMItemProperties();
				itemProps.Name = "spellSuggestion" + scmi.Text;
				itemProps.Text = scmi.Text;
				itemProps.Message = "SpellingSuggestionChosen";
				itemProps.CommandId = "CmdSpellingSuggestionChosen";
				itemProps.Tag = scmi;
				itemProps.Font = (wsText == 0) ? null : GetFontForNormalStyle(wsText,
					rootb.Stylesheet, rootb.DataAccess.WritingSystemFactory);

				if (cSuggestions++ == kMaxSpellingSuggestionsInRootMenu)
				{
					TMItemProperties tmpItemProps = new TMItemProperties();
					tmpItemProps.Name = additionalSuggestionsMenuName;
					tmpItemProps.Text = RootSiteStrings.ksAdditionalSuggestions;
					menuItemNames.Add(tmpItemProps.Name);
					tmAdapter.AddContextMenuItem(tmpItemProps, menuName, insertBeforeMenuName);
					insertBeforeMenuName = null;
				}

				if (insertBeforeMenuName != null)
				{
					menuItemNames.Add(itemProps.Name);
					tmAdapter.AddContextMenuItem(itemProps, menuName, insertBeforeMenuName);
				}
				else
				{
					tmAdapter.AddContextMenuItem(itemProps, menuName,
						additionalSuggestionsMenuName, null);
				}
			}

			return menuItemNames;
		}

		void UpdateItemProps(ITMAdapter tmAdapter, string menuName, bool nonSpellingError, object tag)
		{
			TMItemProperties itemProps = tmAdapter.GetItemProperties(menuName);
			if (itemProps != null)
			{
				if (nonSpellingError)
				{
					itemProps.Tag = null; // disable
				}
				else
				{
					itemProps.Tag = tag;
				}
				itemProps.Update = true;
				tmAdapter.SetItemProperties(menuName, itemProps);
			}
		}
#endif

		void itemAdd_Click(object sender, EventArgs e)
		{
			(sender as AddToDictMenuItem).AddWordToDictionary();
		}

		/// <summary>
		/// Return the dictionary which should be used for the specified writing system.
		/// </summary>
		public ISpellEngine GetDictionary(int ws)
		{
			return SpellingHelper.GetSpellChecker(ws, WritingSystemFactory);
		}

		/// <summary>
		/// If there is a mis-spelled word at the specified point, display the spell-check menu and return true.
		/// Otherwise, return false.
		/// </summary>
		/// <param name="pt">The screen position for which the context menu is requested</param>
		/// <param name="rootsite">The focused rootsite</param>
		internal bool DoSpellCheckContextMenu(Point pt, RootSite rootsite)
		{
			return SpellCheckHelper.ShowContextMenu(pt, rootsite);
		}
		#endregion

		#region Navigation
		/// <summary>
		/// Gets the flid of the next property to be displayed for the current object
		/// </summary>
		/// <param name="flid">The flid of the current (i.e., selected) property</param>
		/// <returns>the flid of the next property to be displayed</returns>
		/// <remarks>ENHANCE: This approach will not work if the same flids are ever displayed
		/// at multiple levels (or in different frags) with different following flids</remarks>
		protected virtual int GetNextFlid(int flid)
		{
			return -1;
		}
		#endregion

		#region Embedded object handling
		/// <summary>
		/// Gets a string representation of the object suitable to put on the clipboard.
		/// </summary>
		/// <param name="cache">LCM cache representing the DB connection to use</param>
		/// <param name="guid">The guid of the object in the DB</param>
		public string TextRepOfObj(LcmCache cache, Guid guid)
		{
			ICmObject obj;
			if (cache.ServiceLocator.ObjectRepository.TryGetObject(guid, out obj) && obj is IEmbeddedObject)
			{
				return ((IEmbeddedObject)obj).TextRepresentation;
			}
			return null;
		}

		/// <summary>
		/// Create a new object, given a text representation (e.g., from the clipboard).
		/// </summary>
		/// <param name="cache">LCM cache representing the DB connection to use</param>
		/// <param name="sTextRep">Text representation of object</param>
		/// <param name="selDst">Provided for information in case it's needed to generate
		/// the new object (E.g., footnotes might need it to generate the proper sequence
		/// letter)</param>
		/// <param name="kodt">The object data type to use for embedding the new object
		/// </param>
		public virtual Guid MakeObjFromText(LcmCache cache, string sTextRep, IVwSelection selDst, out int kodt)
		{
			// Keep trying different types of objects until one of them recognizes the string
			try
			{
				// try to make picture
				var pict = cache.ServiceLocator.GetInstance<ICmPictureFactory>().Create(sTextRep, CmFolderTags.DefaultPictureFolder);
				kodt = (int)FwObjDataTypes.kodtGuidMoveableObjDisp;
				return pict.Guid;
			}
			catch
			{
			}

			// Wasn't a picture, try creating a BT footnote copy
			try
			{
				if (IsBackTranslation)
				{
					kodt = (int)FwObjDataTypes.kodtNameGuidHot;
					var helper = SelectionHelper.Create(selDst, null);
					var repo = cache.ServiceLocator.GetInstance<IStFootnoteRepository>();
					SelLevInfo info;
					ISegment segment;
					if (helper.GetLevelInfoForTag(StTxtParaTags.kflidSegments, out info))
					{
						segment = m_cache.ServiceLocator.GetInstance<ISegmentRepository>().GetObject(info.hvo);
					}
					else
					{
						SelLevInfo paraLevInfo;
						helper.GetLevelInfoForTag(StTextTags.kflidParagraphs, out paraLevInfo);
						var para = cache.ServiceLocator.GetInstance<IStTxtParaRepository>().GetObject(paraLevInfo.hvo);
						segment = para.GetSegmentForOffsetInFreeTranslation(helper.GetIch(SelectionHelper.SelLimitType.Top), helper.Ws);
					}
					var tssVernSegment = segment.BaselineText;
					foreach (var guid in tssVernSegment.GetAllEmbeddedObjectGuids(FwObjDataTypes.kodtOwnNameGuidHot))
					{
						IStFootnote footnote;
						if (repo.TryGetObject(guid, out footnote) && footnote.TextRepresentation == sTextRep)
						{
							// Now we know the footnote being pasted corresponds to one in the proper place in the
							// vernacular. Next we have to make sure it's not already somewhere else in the BT.
							var tssBtSegment = segment.FreeTranslation.get_String(helper.Ws);
							if (tssBtSegment.GetAllEmbeddedObjectGuids(FwObjDataTypes.kodtNameGuidHot).Any(guidBtFn => repo.TryGetObject(guidBtFn, out footnote) && footnote.TextRepresentation == sTextRep))
							{
								DisplayMessage(ResourceHelper.GetResourceString("kstidFootnoteCallerAlreadyInBt"));
								return Guid.Empty;
							}
							return guid;
						}
					}
					DisplayMessage(ResourceHelper.GetResourceString("kstidFootnoteNotInVernacular"));
					return Guid.Empty;
				}
			}
			catch
			{
			}

			throw new ArgumentException("Unexpected object representation string: " + sTextRep, "stextRep");
		}

		/// <summary>
		/// Overridable method to display a simple application message to the user.
		/// </summary>
		public virtual void DisplayMessage(string message)
		{
			MessageBox.Show(Control, message, Application.ProductName, MessageBoxButtons.OK);
		}

		/// <summary>
		/// Gets text properties to apply to a picture caption.
		/// </summary>
		public virtual ITsTextProps CaptionProps
		{
			get
			{
				throw new NotSupportedException();
			}
		}
		#endregion

		#region Overridden methods & properties
		/// <summary>
		/// Called from SimpleRootSite when the selection changes on its rootbox.
		/// </summary>
		public sealed override void SelectionChanged()
		{
			if (m_cache != null)
			{
				if (m_cache.ActionHandlerAccessor.CurrentDepth > 0 ||
					m_cache.ActionHandlerAccessor.SuppressSelections)
				{
					// Make sure that between the time this method is called and the time we actually
					// fire the deferred event, we don't use an out-of-date cached selection.
					ClearCurrentSelection();

					((IActionHandlerExtensions)m_cache.ActionHandlerAccessor).DoAtEndOfPropChanged(HandleSelectionChange);
					return;
				}
			}
			base.SelectionChanged();
		}

		/// <summary>
		/// Handles the selection changed.
		/// </summary>
		private void HandleSelectionChange()
		{
			if (IsDisposed)
			{
				return; // there is now no way for Dispose() to remove the task that requests this.
			}
			// At the end of a unit-of-work, the rootbox may or may not have a selection. If it
			// does, it will (probably) be the same one that was passed to the last call to
			// SelectionChanged, and we can respond to the change. If not, then a selection will
			// probably get created by RequestSelectionAtEndOfUOW, so we'll handle the change
			// in response to that. There's also a chance we'll never get a selection, but that
			// might be okay, too, in some views.
			// Also, need to use Invoke, since this may be running on a progress thread.
			Control.Invoke(() =>
			{
				var rootb = EditedRootBox;
				RootSite site = null;
				if (rootb != null && rootb.Selection != null)
				{
					if (rootb.Selection.RootBox != null && rootb.Selection.RootBox.Site != null)
					{
						site = rootb.Selection.RootBox.Site as RootSite;
						if (site != null)
						{
							site.InSelectionChanged = true;
						}
					}
					HandleSelectionChange(rootb, rootb.Selection);

					if (site != null)
					{
						site.InSelectionChanged = false;
					}
				}
				else
				{
					ClearCurrentSelection();
				}
			});
		}

		/// <summary>
		/// Another case of something we can currently only do in the LCM-aware subclass.
		/// </summary>
		protected override void MergeLastTwoUnitsOfWork()
		{
			((IActionHandlerExtensions)Cache.ActionHandlerAccessor).DoAtEndOfPropChanged(InvokeMergeLastTwoUnitsOfWork);
			m_undoCountBeforeMerge = Cache.ActionHandlerAccessor.UndoableSequenceCount;
		}

		/// <summary>
		/// Merges the last two units of work (when they are completely done and everyone has
		/// been notified of the property changes).
		/// </summary>
		private void InvokeMergeLastTwoUnitsOfWork()
		{
			// complex selection that started this may not have produced any changes
			if (Cache.ActionHandlerAccessor.UndoableSequenceCount > m_undoCountBeforeMerge)
			{
				((IActionHandlerExtensions)Cache.ActionHandlerAccessor).MergeLastTwoUnitsOfWork();
			}
		}

		/// <summary>
		/// Allows an application to set a cursor for the given selection.
		/// </summary>
		protected override bool SetCustomCursor(IVwSelection sel)
		{
			if (IsFootnoteAnchorIconSelected(sel))
			{
				Control.Cursor = Cursors.Hand;
				return true;
			}

			return base.SetCustomCursor(sel);
		}

		/// <summary>
		/// Determines if the current selection is for a footnote anchor icon.
		/// </summary>
		/// <returns>
		/// True if current selection points to a footnote anchor icon, false otherwise
		/// </returns>
		public bool IsFootnoteAnchorIconSelected()
		{
			return CurrentSelection != null && IsFootnoteAnchorIconSelected(CurrentSelection.Selection);
		}

		/// <summary>
		/// Determines if the given selection is for a footnote anchor icon.
		/// </summary>
		/// <returns>
		/// True if current selection points to a footnote anchor icon, false otherwise
		/// </returns>
		protected bool IsFootnoteAnchorIconSelected(IVwSelection sel)
		{
			var found = false;
			HandleFootnoteAnchorIconSelected(sel, (hvo, flid, ws, ich) => { found = true; });
			return found;
		}

		/// <summary>
		/// Determines if the given selection is for a footnote anchor icon and returns the
		/// GUID of the footnote.
		/// </summary>
		/// <returns>
		/// GUID of footnote if selection points to a footnote anchor icon, Guid.Empty otherwise
		/// </returns>
		protected Guid GetGuidForSelectedFootnoteAnchorIcon(IVwSelection sel)
		{
			var footnote = Guid.Empty;
			HandleFootnoteAnchorIconSelected(sel, (hvo, flid, ws, ich) =>
			{
				var footnoteLocation = (ws <= 0) ? Cache.DomainDataByFlid.get_StringProp(hvo, flid) : Cache.DomainDataByFlid.get_MultiStringAlt(hvo, flid, ws);
				footnote = TsStringUtils.GetGuidFromRun(footnoteLocation, footnoteLocation.get_RunAt(ich));
			});
			return footnote;
		}

		/// <summary>
		/// Performs the delegated action if the given selection is for a footnote anchor icon.
		/// ENHANCE: This code currently assumes that any iconic representation of an ORC is
		/// for a footnote anchor. When we support showing picture anchors (or anything else)
		/// iconically, this will have to be changed to account for that.
		/// </summary>
		protected static void HandleFootnoteAnchorIconSelected(IVwSelection sel, FootnoteAnchorFoundDelegate action)
		{
			if (sel == null)
			{
				throw new ArgumentNullException(nameof(sel));
			}
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			if (sel.SelType == VwSelType.kstPicture)
			{
				// See if this is a ORC-replacement picture, in which case we treat it
				// as a clickable object rather than a picture.
				var ichAnchor = sel.get_ParagraphOffset(false);
				var ichEnd = sel.get_ParagraphOffset(true);
				if (ichAnchor >= 0 && ichAnchor < ichEnd)
				{
					var selHelperOrc = SelectionHelper.Create(sel, sel.RootBox.Site);
					SelLevInfo info;
					bool found = false;
					switch (selHelperOrc.TextPropId)
					{
						case StTxtParaTags.kflidContents:
							found = selHelperOrc.GetLevelInfoForTag(StTextTags.kflidParagraphs, out info);
							break;
						case CmTranslationTags.kflidTranslation:
							found = (selHelperOrc.GetLevelInfoForTag(-1, out info) && selHelperOrc.Ws > 0);
							break;
						case SegmentTags.kflidFreeTranslation:
							if (selHelperOrc.GetLevelInfoForTag(StTxtParaTags.kflidSegments, out info) && selHelperOrc.Ws > 0)
							{
								// adjust anchor offset to be a segment offset - need to subtract off the beginning offset
								// for the segment.
								var selHelperStartOfSeg = new SelectionHelper(selHelperOrc);
								selHelperStartOfSeg.IchAnchor = selHelperStartOfSeg.IchEnd = 0;
								var selSegStart = selHelperStartOfSeg.SetSelection(selHelperOrc.RootSite, false, false);
								ichAnchor -= selSegStart.get_ParagraphOffset(false);
								found = true;
							}
							break;
						default:
							// Ignore everything else because it doesn't have footnotes.
							return;
					}
					if (found)
						action(info.hvo, selHelperOrc.TextPropId, selHelperOrc.Ws, ichAnchor);
				}
			}
		}

		/// <summary>
		/// Gets a selection helper that represents an insertion point at the specified limit of
		/// the current selection. Use this version instead of calling ReduceSelectionToIp
		/// directly on the selection helper if you want it to work for selections which might
		/// include iconic representations of ORC characters.
		/// </summary>
		/// <returns>A selection helper representing an IP, or <c>null</c> if the given
		/// selection cannot be reduced to an IP</returns>
		public SelectionHelper GetSelectionReducedToIp(SelectionHelper.SelLimitType selLimit)
		{
			var selHelper = CurrentSelection.ReduceSelectionToIp(SelectionHelper.SelLimitType.Top, false, false);
			if (selHelper == null)
			{
				HandleFootnoteAnchorIconSelected(CurrentSelection.Selection, (hvo, flid, ws, ich) =>
				{
					selHelper = new SelectionHelper(CurrentSelection);
					selHelper.IchAnchor = selHelper.IchEnd = ich;
					var sel = selHelper.SetSelection(false, false);
					if (sel == null)
					{
						selHelper = null;
					}
				});
			}
			return selHelper;
		}

		/// <summary>
		/// Remove character formatting.
		/// </summary>
		public override void RemoveCharFormatting(bool removeAllStyles)
		{
			string undo, redo;
			ResourceHelper.MakeUndoRedoLabels("kstidUndoStyleChanges", out undo, out redo);
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW(undo, redo, Cache.ServiceLocator.GetInstance<IActionHandler>(), () => CallBaseRemoveCharFormatting(removeAllStyles));
		}

		/// <summary>
		/// Needed to be able to call the base implementation from within an anonymous method.
		/// </summary>
		private void CallBaseRemoveCharFormatting(bool removeAllStyles)
		{
			base.RemoveCharFormatting(removeAllStyles);
		}

		/// <summary>
		/// Changes the writing system.
		/// </summary>
		/// <param name="sel">The selection.</param>
		/// <param name="props">The properties specifying the new writing system.</param>
		/// <param name="numProps">The number of ITsTextProps.</param>
		protected override void ChangeWritingSystem(IVwSelection sel, ITsTextProps[] props, int numProps)
		{
			Debug.Assert(numProps > 0);
			ITsTextProps ttp = null;
			for (var i = 0; i < numProps; ++i)
			{
				ttp = props[i];
				if (ttp != null)
				{
					break;
				}
			}
			if (ttp != null)
			{
				if (sel.IsRange)
				{
					int var;
					var hvoWs = ttp.GetIntPropValues((int)FwTextPropType.ktptWs, out var);
					var wsName = Cache.ServiceLocator.WritingSystemManager.Get(hvoWs).DisplayLabel;
					string undo, redo;
					ResourceHelper.MakeUndoRedoLabels("kstidUndoWritingSystemChanges", out undo, out redo);
					undo = string.Format(undo, wsName);
					redo = string.Format(redo, wsName);
					UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW(undo, redo, Cache.ServiceLocator.GetInstance<IActionHandler>(), () => CallBaseChangeWritingSystem(sel, props, numProps));
				}
				else
				{
					// insertion point, won't make any actual data change, better not to make a UOW
					// (e.g., see LT-12697)
					CallBaseChangeWritingSystem(sel, props, numProps);
				}
			}
		}

		/// <summary>
		/// Needed to be able to call the base implementation from within an anonymous method.
		/// </summary>
		private void CallBaseChangeWritingSystem(IVwSelection sel, ITsTextProps[] props, int numProps)
		{
			base.ChangeWritingSystem(sel, props, numProps);
		}

		/// <summary>
		/// Change the character style.
		/// </summary>
		protected override void ChangeCharacterStyle(IVwSelection sel, ITsTextProps[] props, int numProps)
		{
			Debug.Assert(numProps > 0);
			ITsTextProps ttp = null;
			for (var i = 0; i < numProps; ++i)
			{
				ttp = props[i];
				if (ttp != null)
				{
					break;
				}
			}
			if (ttp != null)
			{
				var style = ttp.GetStrPropValue((int)FwTextPropType.ktptNamedStyle);
				string undo, redo;
				ResourceHelper.MakeUndoRedoLabels("kstidUndoStyleChanges", out undo, out redo);
				undo = string.Format(undo, style);
				redo = string.Format(redo, style);
				UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW(undo, redo, Cache.ServiceLocator.GetInstance<IActionHandler>(),
					() => CallBaseChangeCharacterStyle(sel, props, numProps));
			}
		}

		/// <summary>
		/// Needed to be able to call the base implementation from within an anonymous method.
		/// </summary>
		private void CallBaseChangeCharacterStyle(IVwSelection sel, ITsTextProps[] props, int numProps)
		{
			base.ChangeCharacterStyle(sel, props, numProps);
		}

		/// <summary>
		/// Change the paragraph style.
		/// </summary>
		protected override void ChangeParagraphStyle(ISilDataAccess sda, ITsTextProps ttp, int hvoPara)
		{
			if (ttp != null)
			{
				var style = ttp.GetStrPropValue((int)FwTextPropType.ktptNamedStyle);
				string undo, redo;
				ResourceHelper.MakeUndoRedoLabels("kstidUndoStyleChanges", out undo, out redo);
				undo = string.Format(undo, style);
				redo = string.Format(redo, style);
				UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW(undo, redo, Cache.ServiceLocator.GetInstance<IActionHandler>(),
					() => CallBaseChangeParagraphStyle(sda, ttp, hvoPara));
			}

		}

		/// <summary>
		/// Needed to be able to call the base implementation from within an anonymous method.
		/// </summary>
		private void CallBaseChangeParagraphStyle(ISilDataAccess sda, ITsTextProps ttp, int hvoPara)
		{
			base.ChangeParagraphStyle(sda, ttp, hvoPara);
		}

		/// <summary>
		/// Determines whether the given tag represents paragraph-level information
		/// </summary>
		protected override bool IsParagraphLevelTag(int tag)
		{
			return tag == StTextTags.kflidParagraphs || tag == CmPictureTags.kflidCaption;
		}

		/// <summary>
		/// Return true if this is a property the mdc can't tell us about.
		/// This is overridden in RootSite, where we can cast it to IFwMetaDataCacheManaged and really find out.
		/// </summary>
		protected override bool IsUnknownProp(IFwMetaDataCache mdc, int tag)
		{
			return !(mdc is IFwMetaDataCacheManaged && ((IFwMetaDataCacheManaged)mdc).FieldExists(tag));
		}

		/// <summary>
		/// At the end of the (presumed active) UOW, restore the requested selection (at its
		/// original scroll position).
		/// </summary>
		protected override void RestoreSelectionAtEndUow(IVwSelection sel, SelectionHelper helper)
		{
			new RequestSelectionByHelper((IActionHandlerExtensions)Cache.ActionHandlerAccessor, sel, helper);
		}

		/// <summary>
		/// The default tag/flid containing the contents of ordinary paragraphs
		/// </summary>
		protected override int ParagraphContentsTag => StTxtParaTags.kflidContents;

		/// <summary>
		/// The default tag/flid containing the properties of ordinary paragraphs
		/// </summary>
		protected override int ParagraphPropertiesTag => StParaTags.kflidStyleRules;

		/// <summary>
		/// Examines the given flid to see if it is a type that requires special handling, as
		/// opposed to just getting an array of style props for each paragraph in the property
		/// represented by that flid.
		/// </summary>
		/// <param name="flidParaOwner">The flid in which the paragraph is owned</param>
		/// <param name="vqttp">array of text props representing the paragraphs in</param>
		/// <returns><c>true</c> if handled; <c>false</c> otherwise.</returns>
		protected override bool HandleSpecialParagraphType(int flidParaOwner, out ITsTextProps[] vqttp)
		{
			if (flidParaOwner == CmPictureTags.kflidCaption)
			{
				vqttp = new[] { CaptionProps };
				return true;
			}
			vqttp = null;
			return false;
		}

		/// <summary>
		/// Gets the default "Normal" paragraph style name. This base implementation just returns
		/// a hardcoded string. It will probably never be used, so it doesn't matter.
		/// </summary>
		protected override string DefaultNormalParagraphStyleName => StyleServices.NormalStyleName;

		/// <summary>
		/// Overridden version of HandleKeyDown to get an undo task around key strokes that
		/// would change the database.
		/// </summary>
		/// <param name="e">key pressed</param>
		/// <param name="ss">whether control and/or shift is pressed along with key pressed</param>
		protected override void HandleKeyDown(KeyEventArgs e, VwShiftStatus ss)
		{
			base.HandleKeyDown(e, ss);

			string stUndo;
			string stRedo;
			ResourceHelper.MakeUndoRedoLabels("kstidUndoTyping", out stUndo, out stRedo);
			if (e.KeyValue == (char)Keys.Return)
			{
				stUndo = string.Format(stUndo, "LSEP");
				stRedo = string.Format(stRedo, "LSEP");
			}

			// The action handler can be null if we're using SimpleRootSite as a
			// textbox that is not connected to a BEP.
			var actionHandler = EditedRootBox.DataAccess.GetActionHandler();
			var undoHelper = (actionHandler != null && actionHandler.CurrentDepth == 0) ? new UndoTaskHelper(EditedRootBox.Site, stUndo, stRedo) : null;

			try
			{
				if (e.Shift && e.KeyValue == (int)Keys.Return)
				{
					CallOnTyping("\u2028", Keys.None); // type a line separator
				}
				if (undoHelper != null)
				{
					undoHelper.RollBack = false;
				}
			}
			finally
			{
				undoHelper?.Dispose();
			}
		}

		/// <summary>
		/// Overridden to wrap handling of a key-press in an appropriate undo task.
		/// </summary>
		public override void OnKeyPress(KeyPressEventArgs e, Keys modifiers)
		{
			// if we aren't editing a root box, we aren't editing, so ignore key presses.
			// See FWR -975.
			if (EditedRootBox == null)
			{
				return;
			}
			// Try get a useful undo/redo message
			string stUndo, stRedo;
			if (e.KeyChar == (char)Keys.Back || e.KeyChar == (char)127)
			{
				ResourceHelper.MakeUndoRedoLabels("kstidUndoDelete", out stUndo, out stRedo);
			}
			else
			{
				if (e.KeyChar == (char)Keys.Return && modifiers == Keys.Shift)
				{
					// Handle case for inserting line break
					ResourceHelper.MakeUndoRedoLabels("kstidHardLineBreak", out stUndo, out stRedo);
				}
				else
				{
					ResourceHelper.MakeUndoRedoLabels("kstidUndoTyping", out stUndo, out stRedo);
					if (e.KeyChar == (char)Keys.Return)
					{
						stUndo = string.Format(stUndo, "CR");
						stRedo = string.Format(stRedo, "CR");
					}
					else
					{
						stUndo = string.Format(stUndo, e.KeyChar);
						stRedo = string.Format(stRedo, e.KeyChar);
					}
				}
			}

			// The action handler can be null if we're using SimpleRootSite as a
			// textbox that is not connected to a BEP.
			var actionHandler = EditedRootBox.DataAccess.GetActionHandler();
			var undoHelper = (actionHandler != null && actionHandler.CurrentDepth == 0) ? new UndoTaskHelper(EditedRootBox.Site, stUndo, stRedo) : null;
			try
			{
				base.OnKeyPress(e, modifiers);

				if (undoHelper != null)
				{
					undoHelper.RollBack = false;
				}
			}
			finally
			{
				undoHelper?.Dispose();
			}
		}

		/// <summary>
		/// Call DeleteSelection, wrapping it in an Undo task.
		/// Overridden to make a proper UOW in RootSite.
		/// </summary>
		protected override void DeleteSelectionTask(string undoLabel, string redoLabel)
		{
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW(undoLabel, redoLabel, Cache.ActionHandlerAccessor, DeleteSelection);
		}

		#endregion

		/// <summary>
		/// Applies the given URL as a hotlink to the currently selected text, if any, or
		/// inserts a link to the URL.
		/// </summary>
		public void ConvertSelToLink(string clip, LcmStyleSheet stylesheet)
		{
			var hyperlinkStyle = stylesheet.FindStyle(StyleServices.Hyperlink);
			if (m_callbacks?.EditedRootBox == null || hyperlinkStyle == null)
			{
				return;
			}
			var sel = m_callbacks.EditedRootBox.Selection;
			var sda = m_callbacks.EditedRootBox.DataAccess;
			var actionHandler = sda.GetActionHandler();
			if (actionHandler == null)
			{
				return; // no way we can do it.
			}
			ITsString tssLink;
			bool fGotItAll;
			sel.GetFirstParaString(out tssLink, " ", out fGotItAll);
			var tsb = tssLink.GetBldr();
			if (sel.IsRange)
			{
				// Use the text of the selection as the text of the link
				var ich = tssLink.Text.IndexOf(Environment.NewLine);
				if (!fGotItAll || ich >= 0)
				{
					tsb.ReplaceTsString(ich, tsb.Length, null);
					var ichTop = sel.get_ParagraphOffset(sel.EndBeforeAnchor);
					var helper = SelectionHelper.Create(sel, EditedRootBox.Site);
					helper.IchAnchor = ichTop;
					helper.IchEnd = ich;
					sel = helper.Selection;
				}
			}
			if (!sel.IsRange)
			{
				tssLink = TsStringUtils.MakeString(clip, sda.WritingSystemFactory.UserWs);
				tsb = tssLink.GetBldr();
			}

			using (var undoTaskHelper = new UndoTaskHelper(actionHandler, EditedRootBox.Site, RootSiteStrings.ksUndoInsertLink, RootSiteStrings.ksRedoInsertLink))
			{
				if (m_cache != null && m_cache.ProjectId != null)
				{
					clip = FwLinkArgs.FixSilfwUrlForCurrentProject(clip, m_cache.ProjectId.Name);
				}
				var filename = StringServices.MarkTextInBldrAsHyperlink(tsb, 0, tsb.Length, clip, hyperlinkStyle, m_cache.LanguageProject.LinkedFilesRootDir);
				if (FileUtils.IsFilePathValid(filename))
				{
					Debug.Assert(m_cache.LangProject.FilePathsInTsStringsOA != null, "Somehow migration #30 did not add the FilePathsInTsStrings CmFolder. Fix by modifying FindOrCreateFolder()");
					DomainObjectServices.FindOrCreateFile(m_cache.LangProject.FilePathsInTsStringsOA, filename);
				}

				tssLink = tsb.GetString();
				sel.ReplaceWithTsString(tssLink);
				undoTaskHelper.RollBack = false;
				// Arrange that immediate further typing won't extend link.
				sel = Callbacks.EditedRootBox.Selection; // may have been changed.
				if (sel == null)
				{
					return;
				}
				var pb = tssLink.get_PropertiesAt(0).GetBldr();
				pb.SetStrPropValue((int)FwTextPropType.ktptObjData, null);
				pb.SetStrPropValue((int)FwTextPropType.ktptNamedStyle, null);
				sel.SetTypingProps(pb.GetTextProps());
			}
		}

		/// <summary>
		/// Indicate whether we can insert an LinkedFiles link. Currently this is the same as
		/// the method called, but in case we think of more criteria...
		/// </summary>
		public bool CanInsertLinkToFile()
		{
			return m_callbacks.EditedRootBox.DataAccess.GetActionHandler() != null && IsSelectionInOneFormattableProp();
		}

		/// <summary>
		/// Determine whether we can paste a URL at the current location.
		/// Requires a suitable selection and a URL in the clipboard.
		/// A file name is acceptable as a URL.
		/// </summary>
		public bool CanPasteUrl()
		{
			return IsSelectionInOneFormattableProp() && (ClipboardContainsString() && !string.IsNullOrEmpty(GetClipboardAsString()));
		}

		/// <summary>
		/// Paste the contents of the clipboard as a hot link.
		/// </summary>
		public bool PasteUrl(LcmStyleSheet stylesheet)
		{
			if (!CanPasteUrl())
			{
				return false;
			}
			ConvertSelToLink(GetClipboardAsString(), stylesheet);
			return true;
		}
	}
}