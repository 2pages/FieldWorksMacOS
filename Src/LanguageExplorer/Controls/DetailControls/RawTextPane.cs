// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using LanguageExplorer.LcmUi;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.RootSites;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;
using SIL.Xml;

namespace LanguageExplorer.Controls.DetailControls
{
	/// <summary>
	/// RawTextPane displays an StText using the standard VC, except that if it is empty altogether,
	/// we display a message. (Eventually.)
	/// </summary>
	internal sealed class RawTextPane : RootSite, IInterlinearTabControl, IHandleBookmark
	{
		XElement _configurationParameters;
		private ShowSpaceDecorator _showSpaceDa;
		private bool _clickInsertsZws; // true for the special mode where click inserts a zero-width space
		private bool _isCurrentTabForInterlineMaster;
		private bool _showInvisibleSpaces;
		private bool _clickInvisibleSpace;
		private IStText _rootObj;
		private Cursor _invisibleSpaceCursor;

		internal MajorFlexComponentParameters MyMajorFlexComponentParameters { get; set; }

		internal RawTextPane()
			: base(null)
		{
			BackColor = Color.FromKnownColor(KnownColor.Window);
			DoSpellCheck = true;
			AcceptsTab = false;
		}

		internal int RootHvo { get; private set; }

		internal RawTextVc Vc { get; private set; }

		internal XElement ConfigurationParameters
		{
			set => _configurationParameters = value;
		}

		internal bool IsCurrentTabForInterlineMaster
		{
			get => _isCurrentTabForInterlineMaster;
			set
			{
				if (_isCurrentTabForInterlineMaster == value)
				{
					// Same value, so skip the work.
					return;
				}
				_isCurrentTabForInterlineMaster = value;
				if (_isCurrentTabForInterlineMaster)
				{
					// Set Check on two space menus.
					var currentMenuItem = (ToolStripMenuItem)MyMajorFlexComponentParameters.UiWidgetController.InsertMenuDictionary[Command.ClickInvisibleSpace];
					currentMenuItem.Checked = _clickInvisibleSpace;
					currentMenuItem = (ToolStripMenuItem)MyMajorFlexComponentParameters.UiWidgetController.ViewMenuDictionary[Command.ShowInvisibleSpaces];
					currentMenuItem.Checked = _showInvisibleSpaces;
					// Add handler stuff.
					var userController = new UserControlUiWidgetParameterObject(this);
					userController.MenuItemsForUserControl[MainMenu.View].Add(Command.ShowInvisibleSpaces, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(ShowInvisibleSpaces_Click, () => CanShowInvisibleSpaces));
					userController.MenuItemsForUserControl[MainMenu.Insert].Add(Command.CmdGuessWordBreaks, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CmdGuessWordBreaks_Click, () => CanCmdGuessWordBreaks));
					userController.MenuItemsForUserControl[MainMenu.Insert].Add(Command.ClickInvisibleSpace, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(ClickInvisibleSpace_Click, () => CanClickInvisibleSpace));
					MyMajorFlexComponentParameters.UiWidgetController.AddHandlers(userController);
				}
				else
				{
					// remove handler stuff.
					MyMajorFlexComponentParameters.UiWidgetController.RemoveUserControlHandlers(this);
				}
			}
		}

		#region IDisposable override

		/// <inheritdoc />
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
				var sharedEventHandlers = MyMajorFlexComponentParameters.SharedEventHandlers;
				var jumpHandler = sharedEventHandlers.GetEventHandler(Command.CmdJumpToTool);
				// Dispose managed resources here.
				DisposeContextMenuStrip(sharedEventHandlers, jumpHandler);
			}

			// Dispose unmanaged resources here, whether disposing is true or false.
			MyMajorFlexComponentParameters = null;
			ContextMenuStrip = null;
			MyRecordList = null;
			Vc = null;
			_configurationParameters = null;
		}

		private void DisposeContextMenuStrip(ISharedEventHandlers sharedEventHandlers, EventHandler jumpHandler)
		{
			if (ContextMenuStrip == null)
			{
				return;
			}
			var currentIndex = 0;
			var currentMenuItem = ContextMenuStrip.Items[currentIndex++];
			currentMenuItem.Click -= sharedEventHandlers.GetEventHandler(Command.CmdCut);
			currentMenuItem = ContextMenuStrip.Items[currentIndex++];
			currentMenuItem.Click -= sharedEventHandlers.GetEventHandler(Command.CmdCopy);
			currentMenuItem = ContextMenuStrip.Items[currentIndex++];
			currentMenuItem.Click -= sharedEventHandlers.GetEventHandler(Command.CmdPaste);
			currentIndex++;
			currentMenuItem = ContextMenuStrip.Items[currentIndex++];
			currentMenuItem.Click -= CmdLexiconLookup_Click;
			currentMenuItem = ContextMenuStrip.Items[currentIndex++];
			currentMenuItem.Click -= jumpHandler;
			currentMenuItem = ContextMenuStrip.Items[currentIndex++];
			currentMenuItem.Click -= jumpHandler;
			ContextMenuStrip.Dispose();
		}
		#endregion IDisposable override

		#region implemention of IChangeRootObject

		public void SetRoot(int hvo)
		{
			if (hvo != RootHvo || Vc == null)
			{
				RootHvo = hvo;
				SetupVc();
				ChangeOrMakeRoot(RootHvo, Vc, (int)StTextFrags.kfrText, m_styleSheet);
			}
			BringToFront();
			if (RootHvo == 0)
			{
				return;
			}
			// if editable, parse the text to make sure annotations are in a valid initial state
			// with respect to the text so AnnotatedTextEditingHelper can make the right changes
			// to annotations effected by MonitorTextsEdits being true;
			if (Vc == null || !Vc.Editable)
			{
				return;
			}
			if (RootObject.HasParagraphNeedingParse())
			{
				NonUndoableUnitOfWorkHelper.Do(Cache.ActionHandlerAccessor, () =>
				{
					RootObject.LoadParagraphAnnotationsAndGenerateEntryGuessesIfNeeded(false);
				});
			}
		}

		/// <summary>
		/// We can't set the style for Scripture...that has to follow some very specific rules implemented in TE.
		/// </summary>
		public override bool CanApplyStyle => base.CanApplyStyle && !ScriptureServices.ScriptureIsResponsibleFor(_rootObj);

		#endregion

		/// <summary>
		/// This is the record list, if any, that determines the text for our control.
		/// </summary>
		internal IRecordList MyRecordList { get; set; }

		internal IStText RootObject
		{
			get
			{
				if (_rootObj != null && _rootObj.Hvo == RootHvo)
				{
					return _rootObj;
				}
				_rootObj = RootHvo != 0 ? Cache.ServiceLocator.GetInstance<IStTextRepository>().GetObject(RootHvo) : null;
				return _rootObj;
			}
		}

		internal int LastFoundAnnotationHvo { get; } = 0;

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (_clickInvisibleSpace)
			{
				if (InsertInvisibleSpace(e))
				{
					return;
				}
			}
			base.OnMouseDown(e);
		}

		// Insert an invisible space at the place clicked. Return true to suppress normal MouseDown processing.
		private bool InsertInvisibleSpace(MouseEventArgs e)
		{
			var sel = GetSelectionAtViewPoint(e.Location, false);
			if (sel == null)
			{
				return false;
			}
			if (e.Button == MouseButtons.Right || (ModifierKeys & Keys.Shift) == Keys.Shift)
			{
				return false; // don't interfere with right clicks or shift+clicks.
			}
			var helper = SelectionHelper.Create(sel, this);
			var text = helper.GetTss(SelLimitType.Anchor).Text;
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			// We test for space (rather than zwsp) because when in this mode, the option to make the ZWS's visible
			// is always on, which means they are spaces in the string we retrieve.
			// If we don't want to suppress inserting one next to a regular space, we'll need to check the character properties
			// to distinguish the magic spaces from regular ones.
			var ich = helper.GetIch(SelLimitType.Anchor);
			if (ich > 0 && ich <= text.Length && text[ich - 1] == ' ')
			{
				return false; // don't insert second ZWS following existing one (or normal space).
			}
			if (ich < text.Length && text[ich] == ' ')
			{
				return false; // don't insert second ZWS before existing one (or normal space).
			}
			var ws = helper.GetSelProps(SelLimitType.Anchor).GetIntPropValues((int)FwTextPropType.ktptWs, out _);
			if (ws != 0)
			{
				UndoableUnitOfWorkHelper.Do(LanguageExplorerResources.ksUndoInsertInvisibleSpace, LanguageExplorerResources.ksRedoInsertInvisibleSpace, Cache.ActionHandlerAccessor,
					() => sel.ReplaceWithTsString(TsStringUtils.MakeString(AnalysisOccurrence.KstrZws, ws)));
			}
			helper.SetIch(SelLimitType.Anchor, ich + 1);
			helper.SetIch(SelLimitType.End, ich + 1);
			helper.SetSelection(true, true);
			return true; // we already made an appropriate selection.
		}

		protected override void OnKeyPress(KeyPressEventArgs e)
		{
			if (e.KeyChar == (int)Keys.Escape)
			{
				TurnOffClickInvisibleSpace();
			}
			base.OnKeyPress(e);
			Cursor.Current = Cursors.IBeam;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (!_clickInvisibleSpace)
			{
				return;
			}
			if (_invisibleSpaceCursor == null)
			{
				_invisibleSpaceCursor = new Cursor(GetType(), "InvisibleSpaceCursor.cur");
			}
			Cursor = _invisibleSpaceCursor;
		}

		protected override void OnLostFocus(EventArgs e)
		{
			TurnOffClickInvisibleSpace();
			base.OnLostFocus(e);
		}

		/// <summary>
		/// handle the message to see if the menu item should be enabled
		/// </summary>
		private Tuple<bool, bool> CanShowInvisibleSpaces
		{
			get
			{
				var isTextPresent = RootBox?.Selection != null;
				if (isTextPresent) //well, the rootbox is at least there, test it for text.
				{
					RootBox.Selection.TextSelInfo(true, out var tss, out var ichLim, out _, out _, out _, out _);
					if (ichLim == 0 && tss.Length == 0) //nope, no text.
					{
						isTextPresent = false;
					}
				}
				return new Tuple<bool, bool>(Visible, isTextPresent);
			}
		}

		private void ShowInvisibleSpaces_Click(object sender, EventArgs e)
		{
			var senderAsMenuItem = (ToolStripMenuItem)sender;
			if (senderAsMenuItem.Checked == _showInvisibleSpaces)
			{
				// Nothing to do.
				return;
			}
			var newVal = senderAsMenuItem.Checked;
			if (newVal != _showSpaceDa.ShowSpaces)
			{
				_showSpaceDa.ShowSpaces = newVal;
				var saveSelection = SelectionHelper.Create(this);
				RootBox.Reconstruct();
				saveSelection.SetSelection(true);
			}
			if (!newVal && _clickInvisibleSpace)
			{
				TurnOffClickInvisibleSpace();
				// Set Checked for the other the menu and run its event handler.
				var clickInvisibleSpace = (ToolStripMenuItem)MyMajorFlexComponentParameters.UiWidgetController.ViewMenuDictionary[Command.ClickInvisibleSpace];
				clickInvisibleSpace.Checked = false;
				clickInvisibleSpace.PerformClick();
			}
		}

		/// <summary>
		/// handle the message to see if the menu item should be enabled
		/// </summary>
		private Tuple<bool, bool> CanClickInvisibleSpace
		{
			get
			{
				var isTextPresent = RootBox?.Selection != null;
				if (isTextPresent) //well, the rootbox is at least there, test it for text.
				{
					RootBox.Selection.TextSelInfo(true, out var tss, out var ichLim, out _, out _, out _, out _);
					if (ichLim == 0 && tss.Length == 0) //nope, no text.
					{
						isTextPresent = false;
					}
				}
				return new Tuple<bool, bool>(Visible, isTextPresent);
			}
		}

		private void ClickInvisibleSpace_Click(object sender, EventArgs e)
		{
			var senderAsMenuItem = (ToolStripMenuItem)sender;
			var newVal = senderAsMenuItem.Checked;
			if (newVal == _clickInvisibleSpace || newVal == _clickInsertsZws)
			{
				// Nothing to do.
				return;
			}
			_clickInsertsZws = newVal;
			if (newVal && !_showInvisibleSpaces)
			{
				TurnOnShowInvisibleSpaces();
				// Set Checked for the other the menu and run its event handler.
				var showInvisibleSpacesMenu = (ToolStripMenuItem)MyMajorFlexComponentParameters.UiWidgetController.ViewMenuDictionary[Command.ShowInvisibleSpaces];
				showInvisibleSpacesMenu.Checked = true;
				showInvisibleSpacesMenu.PerformClick();
			}
		}

		/// <summary>
		/// Handle "WritingSystemHvo" message.
		/// </summary>
		protected override void ReallyHandleWritingSystemHvo_Changed(object newValue)
		{
			var wsBefore = 0;
			if (RootObject != null && RootBox != null && RootBox.Selection.IsValid)
			{
				// We want to know below whether a base class changed the ws or not.
				wsBefore = SelectionHelper.GetWsOfEntireSelection(RootBox.Selection);
			}

			base.ReallyHandleWritingSystemHvo_Changed(newValue);

			if (RootObject == null || RootBox == null || !RootBox.Selection.IsValid)
			{
				return;
			}
			var ws = SelectionHelper.GetWsOfEntireSelection(RootBox.Selection);
			if (ws == wsBefore)
			{
				// No change, so bail out.
				return;
			}
			if (!GetSelectedWordPos(RootBox.Selection, out var hvo, out var tag, out ws, out _, out _) || tag != StTxtParaTags.kflidContents)
			{
				return;
			}
			// Force this paragraph to recognize it might need reparsing.
			var para = m_cache.ServiceLocator.GetInstance<IStTxtParaRepository>().GetObject(hvo);
			if (Cache.ActionHandlerAccessor.CurrentDepth > 0)
			{
				para.ParseIsCurrent = false;
			}
			else
			{
				NonUndoableUnitOfWorkHelper.Do(Cache.ActionHandlerAccessor, () => para.ParseIsCurrent = false);
			}
		}

		private void TurnOnShowInvisibleSpaces()
		{
			_showInvisibleSpaces = true;
			PropertyTable.SetProperty("ShowInvisibleSpaces", true, true);
		}

		private void TurnOffClickInvisibleSpace()
		{
			_clickInvisibleSpace = false;
			PropertyTable.SetProperty("ClickInvisibleSpace", false, true);
		}

		#region Overrides of RootSite
		/// <summary>
		/// Make the root box.
		/// </summary>
		public override void MakeRoot()
		{
			if (m_cache == null || DesignMode || RootHvo == 0)
			{
				return;
			}

			base.MakeRoot();

			var wsFirstPara = GetWsOfFirstWordOfFirstTextPara();
			Vc = new RawTextVc(RootBox, m_cache, wsFirstPara);
			SetupVc();
			_showSpaceDa = new ShowSpaceDecorator(m_cache.GetManagedSilDataAccess())
			{
				ShowSpaces = _showInvisibleSpaces
			};
			RootBox.DataAccess = _showSpaceDa;
			RootBox.SetRootObject(RootHvo, Vc, (int)StTextFrags.kfrText, m_styleSheet);
		}

		/// <summary>
		/// Returns WS of first character of first paragraph of _hvoRoot text.
		/// It defaults to DefaultVernacularWs in case of a problem.
		/// </summary>
		private int GetWsOfFirstWordOfFirstTextPara()
		{
			Debug.Assert(RootHvo > 0, "No StText Hvo!");
			var wsFirstPara = Cache.DefaultVernWs;
			var txt = Cache.ServiceLocator.GetInstance<IStTextRepository>().GetObject(RootHvo);
			if (txt.ParagraphsOS == null || txt.ParagraphsOS.Count == 0)
			{
				return wsFirstPara;
			}
			return ((IStTxtPara)txt.ParagraphsOS[0]).Contents.get_WritingSystem(0);
		}

		private void SetupVc()
		{
			if (Vc == null || RootHvo == 0)
			{
				return;
			}
			var wsFirstPara = GetWsOfFirstWordOfFirstTextPara();
			if (wsFirstPara == -1)
			{
				// The paragraph's first character has no valid writing system...this seems to be possible
				// when it consists entirely of a picture. Rather than crashing, presume the default.
				wsFirstPara = Cache.DefaultVernWs;
			}
			Vc.SetupVernWsForText(wsFirstPara);
			var stText = Cache.ServiceLocator.GetInstance<IStTextRepository>().GetObject(RootHvo);
			if (_configurationParameters == null)
			{
				return;
			}
			Vc.Editable = XmlUtils.GetOptionalBooleanAttributeValue(_configurationParameters, "editable", true);
			Vc.Editable &= !ScriptureServices.ScriptureIsResponsibleFor(stText);
		}

		protected override void HandleSelectionChange(IVwRootBox rootb, IVwSelection vwselNew)
		{
			base.HandleSelectionChange(rootb, vwselNew);

			// JohnT: it's remotely possible that the base, in calling commit, made this
			// selection no longer useable.
			if (!vwselNew.IsValid)
			{
				return;
			}
			// 'wordform' may be null.
			GetSelectedWordform(vwselNew, out var wordform);
			Publisher.Publish(new PublisherParameterObject(LanguageExplorerConstants.TextSelectedWord, wordform));
			var helper = SelectionHelper.Create(vwselNew, this);
			if (helper != null && helper.GetTextPropId(SelLimitType.Anchor) == RawTextVc.kTagUserPrompt)
			{
				vwselNew.ExtendToStringBoundaries();
				EditingHelper.SetKeyboardForSelection(vwselNew);
			}
		}

		protected override void OnLayout(LayoutEventArgs levent)
		{
			if (Parent == null && string.IsNullOrEmpty(levent.AffectedProperty))
			{
				// width is meaningless, no point in doing extra work
				return;
			}
			// In a tab page this panel occupies the whole thing, so layout is wasted until
			// our size is adjusted to match.
			if (Parent is TabPage && (Parent.Width - Parent.Padding.Horizontal) != this.Width)
			{
				return;
			}
			base.OnLayout(levent);
		}

		/// <summary>
		/// The user has attempted to delete something which the system does not inherently
		/// know how to delete. The dpt argument indicates the type of problem.
		/// </summary>
		public override VwDelProbResponse OnProblemDeletion(IVwSelection sel, VwDelProbType dpt)
		{
			switch (dpt)
			{
				case VwDelProbType.kdptBsAtStartPara:
				case VwDelProbType.kdptDelAtEndPara:
				case VwDelProbType.kdptNone:
					return VwDelProbResponse.kdprDone;
				case VwDelProbType.kdptBsReadOnly:
				case VwDelProbType.kdptComplexRange:
				case VwDelProbType.kdptDelReadOnly:
				case VwDelProbType.kdptReadOnly:
					return VwDelProbResponse.kdprFail;
			}
			return VwDelProbResponse.kdprAbort;
		}

		/// <summary>
		/// Draw to the given clip rectangle.  This is overridden to *NOT* write the
		/// default message for an uninitialized rootsite.
		/// </summary>
		protected override void Draw(PaintEventArgs e)
		{
			if (RootBox != null && (m_dxdLayoutWidth > 0) && !DesignMode)
			{
				base.Draw(e);
			}
			else
			{
				e.Graphics.FillRectangle(SystemBrushes.Window, ClientRectangle);
			}
		}

		public void HandleKeyDownAndKeyPress(Keys key)
		{
			var kea = new KeyEventArgs(key);
			if (EditingHelper.HandleOnKeyDown(kea))
			{
				return;
			}
			OnKeyDown(kea);
			// for some reason OnKeyPress does not handle Delete key
			// In FLEX, OnKeyPress does not even get called for Delete key.
			if (key != Keys.Delete)
			{
				OnKeyPress(new KeyPressEventArgs((char)kea.KeyValue));
			}
		}

		/// <summary>
		/// Handle a right mouse up, invoking an appropriate context menu.
		/// </summary>
		protected override bool DoContextMenu(IVwSelection sel, Point pt, Rectangle rcSrcRoot, Rectangle rcDstRoot)
		{
			// Allow base method to handle spell check problems, if any.
			if (base.DoContextMenu(sel, pt, rcSrcRoot, rcDstRoot))
			{
				return true;
			}
			if (sel == null)
			{
				return false;
			}
			if (!GetSelectedWordform(RootBox.Selection, out _))
			{
				return false;
			}
			var sharedEventHandlers = MyMajorFlexComponentParameters.SharedEventHandlers;
			var jumpHandler = sharedEventHandlers.GetEventHandler(Command.CmdJumpToTool);
			DisposeContextMenuStrip(sharedEventHandlers, jumpHandler);
			// Start: <menu id="mnuIText_RawText">;
			ContextMenuStrip = new ContextMenuStrip
			{
				Name = ContextMenuName.mnuIText_RawText.ToString()
			};
			var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>(7);
			// <item command="CmdCut" />
			ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, ContextMenuStrip, sharedEventHandlers.GetEventHandler(Command.CmdCut), LanguageExplorerResources.Cut);
			// <item command="CmdCopy" />
			ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, ContextMenuStrip, sharedEventHandlers.GetEventHandler(Command.CmdCopy), LanguageExplorerResources.Copy);
			// <item command="CmdPaste" />
			ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, ContextMenuStrip, sharedEventHandlers.GetEventHandler(Command.CmdPaste), LanguageExplorerResources.Paste);
			// <item label="-" translate="do not translate" />
			ToolStripMenuItemFactory.CreateToolStripSeparatorForContextMenuStrip(ContextMenuStrip);
			// <item command="CmdLexiconLookup" />
			var menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, ContextMenuStrip, CmdLexiconLookup_Click, LanguageExplorerResources.Find_in_Dictionary);
			menu.Enabled = CanCmdLexiconLookup;
			// <item command="CmdWordformJumpToAnalyses" defaultVisible="false" />
			var activeRecordList = MyMajorFlexComponentParameters.FlexComponentParameters.PropertyTable.GetValue<IRecordListRepository>(LanguageExplorerConstants.RecordListRepository).ActiveRecordList;
			menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, ContextMenuStrip, jumpHandler, LanguageExplorerResources.Show_in_Word_Analyses);
			menu.Tag = new List<object> { MyMajorFlexComponentParameters.FlexComponentParameters.Publisher, LanguageExplorerConstants.AnalysesMachineName, activeRecordList };
			// <item command="CmdWordformJumpToConcordance" defaultVisible="false" />
			menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, ContextMenuStrip, jumpHandler, LanguageExplorerResources.Show_Wordform_in_Concordance);
			menu.Tag = new List<object> { MyMajorFlexComponentParameters.FlexComponentParameters.Publisher, LanguageExplorerConstants.ConcordanceMachineName, activeRecordList };

			// End: <menu id="mnuDataTree_Delete_Adhoc_Morpheme">
			return true;
		}

		#endregion Overrides of RootSite

		public void MakeTextSelectionAndScrollToView(int ichMin, int ichLim, int ws, int ipara)
		{
			MakeTextSelectionAndScrollToView(ichMin, ichLim, ws, ipara, -1);// end in same prop
		}

		private void MakeTextSelectionAndScrollToView(int ichMin, int ichLim, int ws, int ipara, int ihvoEnd)
		{
			var rgsli = new SelLevInfo[1];
			// entry 0 says which StTextPara
			rgsli[0].ihvo = ipara;
			rgsli[0].tag = StTextTags.kflidParagraphs;
			// entry 1 says to use the Contents of the Text.
			try
			{
				RootBox.MakeTextSelection(0, rgsli.Length, rgsli, StTxtParaTags.kflidContents, 0, ichMin, ichLim, ws, false, ihvoEnd, null, true);
				// Don't steal the focus from another window.  See FWR-1795.
				if (ParentForm == Form.ActiveForm)
				{
					Focus();
				}
				// Scroll this selection into View.
				var sel = RootBox.Selection;
				ScrollSelectionIntoView(sel, VwScrollSelOpts.kssoDefault);
				Update();
			}
			catch (Exception)
			{
			}
		}

		#region IHandleBookMark

		public void SelectBookmark(IStTextBookmark bookmark)
		{
			MakeTextSelectionAndScrollToView(bookmark.BeginCharOffset, bookmark.EndCharOffset, 0, bookmark.IndexOfParagraph);
		}

		#endregion

		private bool CanCmdLexiconLookup
		{
			get
			{
				var enabled = false;
				var sel = RootBox?.Selection;
				if (sel != null && sel.IsValid)
				{
					// We just need to see if it's possible
					enabled = GetSelectedWordPos(sel, out _, out _, out _, out _, out _);
				}
				return enabled;
			}
		}

		private void CmdLexiconLookup_Click(object sender, EventArgs e)
		{
			if (GetSelectedWordPos(RootBox.Selection, out var hvo, out var tag, out var ws, out var ichMin, out var ichLim))
			{
				LexEntryUi.DisplayOrCreateEntry(m_cache, hvo, tag, ws, ichMin, ichLim, this, new FlexComponentParameters(PropertyTable, Publisher, Subscriber), PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider), FwUtilsConstants.UserHelpFile);
			}
		}

		private static bool GetSelectedWordPos(IVwSelection sel, out int hvo, out int tag, out int ws, out int ichMin, out int ichLim)
		{
			IVwSelection wordsel = null;
			if (sel != null)
			{
				var sel2 = sel.EndBeforeAnchor ? sel.EndPoint(true) : sel.EndPoint(false);
				wordsel = sel2?.GrowToWord();
			}
			if (wordsel == null)
			{
				hvo = tag = ws = 0;
				ichMin = ichLim = -1;
				return false;
			}
			wordsel.TextSelInfo(false, out _, out ichMin, out _, out hvo, out tag, out ws);
			wordsel.TextSelInfo(true, out _, out ichLim, out _, out hvo, out tag, out ws);
			return ichLim > 0;
		}

		private bool GetSelectedWordform(IVwSelection sel, out IWfiWordform wordform)
		{
			wordform = null;
			if (!GetSelectedWordPos(sel, out var hvo, out var tag, out _, out var ichMin, out var ichLim))
			{
				return false;
			}
			if (tag != StTxtParaTags.kflidContents)
			{
				return false;
			}
			var para = m_cache.ServiceLocator.GetInstance<IStTxtParaRepository>().GetObject(hvo);
			if (!para.ParseIsCurrent)
			{
				ReparseParaInUowIfNeeded(para);
			}
			var anal = FindClosestWagParsed(para, ichMin, ichLim);
			if (!para.ParseIsCurrent)
			{
				// Something is wrong! The attempt to find the word detected an inconsistency.
				// Fix the paragraph and try again.
				ReparseParaInUowIfNeeded(para);
				anal = FindClosestWagParsed(para, ichMin, ichLim);
			}
			if (anal != null && anal.HasWordform)
			{
				wordform = anal.Wordform;
				return true;
			}
			return false;
		}

		private void ReparseParaInUowIfNeeded(IStTxtPara para)
		{
			if (Cache.ActionHandlerAccessor.CurrentDepth > 0)
			{
				ReparseParagraph(para);
			}
			else
			{
				NonUndoableUnitOfWorkHelper.Do(Cache.ActionHandlerAccessor, () => ReparseParagraph(para));
			}
		}

		private static void ReparseParagraph(IStTxtPara para)
		{
			using (var parser = new ParagraphParser(para))
			{
				parser.Parse(para);
			}
		}

		private static IAnalysis FindClosestWagParsed(IStTxtPara para, int ichMin, int ichLim)
		{
			IAnalysis anal = null;
			foreach (var seg in para.SegmentsOS)
			{
				if (seg.BeginOffset > ichMin || seg.EndOffset < ichLim)
				{
					continue;
				}
				var occurrence = seg.FindWagform(ichMin - seg.BeginOffset, ichLim - seg.BeginOffset, out _);
				if (occurrence != null)
				{
					anal = occurrence.Analysis;
				}
				break;
			}
			return anal;
		}

		private static void Swap(ref int first, ref int second)
		{
			var temp = first;
			first = second;
			second = temp;
		}

		private Tuple<bool, bool> CanCmdGuessWordBreaks
		{
			get
			{
				var isTextPresent = RootBox?.Selection != null;
				if (isTextPresent) //well, the rootbox is at least there, test it for text.
				{
					RootBox.Selection.TextSelInfo(true, out var tss, out var ichLim, out _, out _, out _, out _);
					if (ichLim == 0 && tss.Length == 0) //nope, no text.
					{
						isTextPresent = false;
					}
				}
				return new Tuple<bool, bool>(Visible, isTextPresent);
			}
		}

		/// <summary>
		/// Guess where we can break words.
		/// </summary>
		private void CmdGuessWordBreaks_Click(object sender, EventArgs e)
		{
			var sel = RootBox.Selection;
			sel.TextSelInfo(false, out _, out var ichMin, out _, out var hvoStart, out _, out _);
			sel.TextSelInfo(true, out _, out var ichLim, out _, out var hvoEnd, out _, out _);
			if (sel.EndBeforeAnchor)
			{
				Swap(ref ichMin, ref ichLim);
				Swap(ref hvoStart, ref hvoEnd);
			}
			var guesser = new WordBreakGuesser(m_cache, hvoStart);
			if (hvoStart == hvoEnd)
			{
				if (ichMin == ichLim)
				{
					ichMin = 0;
					ichLim = -1; // do the whole paragraph for an IP.
				}
				guesser.Guess(ichMin, ichLim, hvoStart);
			}
			else
			{
				guesser.Guess(ichMin, -1, hvoStart);
				var fProcessing = false;
				var sda = m_cache.MainCacheAccessor;
				var hvoStText = RootHvo;
				var cpara = sda.get_VecSize(hvoStText, StTextTags.kflidParagraphs);
				for (var i = 0; i < cpara; i++)
				{
					var hvoPara = sda.get_VecItem(hvoStText, StTextTags.kflidParagraphs, i);
					if (hvoPara == hvoStart)
					{
						fProcessing = true;
					}
					else if (hvoPara == hvoEnd)
					{
						break;
					}
					else if (fProcessing)
					{
						guesser.Guess(0, -1, hvoPara);
					}
				}
				guesser.Guess(0, ichLim, hvoEnd);
			}
			TurnOnShowInvisibleSpaces();
		}

		#region Overrides of SimpleRootSite

		/// <summary>
		/// Initialize a FLEx component with the basic interfaces.
		/// </summary>
		/// <param name="flexComponentParameters">Parameter object that contains the required three interfaces.</param>
		public override void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
		{
			base.InitializeFlexComponent(flexComponentParameters);
			m_styleSheet = FwUtils.StyleSheetFromPropertyTable(PropertyTable);
			_showInvisibleSpaces = PropertyTable.GetValue<bool>("ShowInvisibleSpaces");
			_clickInvisibleSpace = PropertyTable.GetValue<bool>("ClickInvisibleSpace");
		}

		#endregion
	}
}