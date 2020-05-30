// Copyright (c) 2003-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LanguageExplorer.Controls;
using LanguageExplorer.Filters;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.RootSites;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.FieldWorks.FwCoreDlgs;
using SIL.FieldWorks.FwCoreDlgs.Controls;
using SIL.FieldWorks.Resources;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;
using SIL.LCModel.Utils;
using SIL.PlatformUtilities;
using SIL.Windows.Forms;

namespace LanguageExplorer.Impls
{
	/// <summary>
	/// Find/Replace dialog
	/// </summary>
	internal class FwFindReplaceDlg : Form, IMessageFilter
	{
		#region Constants
		private const string kPersistenceLabel = "FindReplace_";

		#endregion

		#region Events
		/// <summary>Handler for MatchNotFound events.</summary>
		protected delegate bool MatchNotFoundHandler(object sender, string defaultMsg, MatchType type);

		/// <summary>Fired when a match is not found.</summary>
		protected event MatchNotFoundHandler MatchNotFound;
		#endregion

		#region Data members
		/// <summary>all the search settings</summary>
		private IVwPattern m_vwFindPattern;
		/// <summary>Environment that keeps track of where we're finding</summary>
		protected FindCollectorEnv m_findEnvironment;
		/// <summary>The rootsite where the find operation will be performed</summary>
		protected IVwRootSite m_vwRootsite;
		/// <summary />
		protected LcmCache m_cache;
		private IApp m_app;
		private bool m_cacheMadeLocally = false;
		/// <summary />
		private IVwSelection m_vwSelectionForPattern;
		/// <summary />
		protected ITsString m_prevSearchText;
		/// <summary />
		private SearchKiller m_searchKiller = new SearchKiller();
		private bool m_messageFilterInstalled;
		private string m_sMoreButtonText;
		private int m_heightDlgMore;
		private int m_heightTabControlMore;
		private int m_heightDlgLess;
		private int m_heightTabControlLess;
		private bool m_fLastDirectionForward;
		private TabPage tabFind;
		private TabPage tabReplace;
		private TabControl tabControls;
		/// <summary />
		protected CheckBox chkMatchDiacritics;
		/// <summary />
		protected CheckBox chkMatchWS;
		/// <summary />
		protected CheckBox chkMatchCase;
		/// <summary />
		protected CheckBox chkMatchWholeWord;
		/// <summary>Panel containing advanced controls</summary>
		protected Panel panelSearchOptions;
		/// <summary />
		protected FwTextBox fweditFindText;
		/// <summary />
		protected FwTextBox fweditReplaceText;
		private IContainer components = null;
		/// <summary />
		protected Label lblFindFormatText;
		/// <summary />
		protected Label lblReplaceFormatText;
		private bool m_initialActivate;
		private bool m_inReplace;
		private bool m_inFind;
		private bool m_inGetSpecs;
		/// <summary>The OK button is usually hidden. It is visible in Flex after clicking the
		/// Setup button of the Find/Replace tab of the Bulk Edit bar.</summary>
		private Button m_okButton;
		/// <summary></summary>
		protected CheckBox chkUseRegularExpressions;
		private IHelpTopicProvider m_helpTopicProvider;
		private IPropertyTable m_propertyTable;
		private string s_helpTopic;
		private HelpProvider helpProvider;
		// Used by EnableControls to remember what was enabled when things were disabled for
		// the duration of an operation, in order to put them right afterwards.
		private Dictionary<Control, bool> m_enableStates;
		private Button btnRegexMenuFind;
		private Button btnRegexMenuReplace;
		private RegexHelperContextMenu _regexContextContextMenuFind;
		/// <summary />
		protected MenuItem mnuWritingSystem;
		/// <summary />
		protected MenuItem mnuStyle;
		private Button btnFormat;
		/// <summary>The close button</summary>
		/// <remarks>TE-4839: Changed the text from Cancel to Close according to TE Analyst (2007-06-22).</remarks>
		protected Button btnClose;
		private Button btnFindNext;
		private Button btnMore;
		private Button btnReplace;
		private Button btnReplaceAll;
		private Panel panelBasic;
		/// <summary />
		protected Label lblReplaceFormat;
		private Label lblReplaceText;
		/// <summary />
		private ContextMenu mnuFormat;
		private Label lblSearchOptions;
		/// <summary />
		protected Label lblFindFormat;
		private RegexHelperContextMenu _regexContextContextMenuReplace;

		#endregion

		#region Construction, initialization, destruction
		/// <summary />
		public FwFindReplaceDlg()
		{
			m_inFind = false;
			AccessibleName = GetType().Name;
			InitializeComponent();
			helpProvider = new HelpProvider();
			helpProvider.SetHelpNavigator(this, HelpNavigator.Topic);
			LastTextBoxInFocus = fweditFindText;
			// Init of member variables related to dialog height removed from here and moved
			// to OnLayout. This allows them to correctly remember the adjusted dialog size
			// that occurs when screen resolution is not set to 96dpi
			m_sMoreButtonText = btnMore.Text;
			btnMore.Image = ResourceHelper.MoreButtonDoubleArrowIcon;
			btnFormat.Image = ResourceHelper.ButtonMenuArrowIcon;
			panelSearchOptions.Visible = false;
			fweditFindText.TextChanged += HandleTextChanged;
			fweditReplaceText.TextChanged += HandleTextChanged;
			m_searchKiller.Control = this;  // used for redrawing
			m_searchKiller.StopControl = btnClose;  // need to know the stop button
		}

		/// <summary>
		/// Set the initial values for the dialog controls, assuming that the find and replace
		/// edit boxes use the default vernacular writing system.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="vwPattern">Find/replace values</param>
		/// <param name="rootSite">view</param>
		/// <param name="fReplace"><c>true</c> to initially display replace dialog page</param>
		/// <param name="fOverlays">ignored for now</param>
		/// <param name="owner">The main window that owns the rootsite</param>
		/// <param name="helpTopicProvider">help topic provider allows the dialog box class
		/// to specify the appropriate help topic path for this dialog</param>
		/// <param name="app">The application</param>
		/// <returns>
		/// true if the dialog was initialized properly, otherwise false.
		/// False indicates some problem and the find/replace dialog should not be
		/// shown at this time.
		/// </returns>
		public bool SetDialogValues(LcmCache cache, IVwPattern vwPattern, IVwRootSite rootSite, bool fReplace, bool fOverlays, Form owner, IHelpTopicProvider helpTopicProvider, IApp app)
		{
			return SetDialogValues(cache, vwPattern, rootSite, fReplace, fOverlays, owner, helpTopicProvider, app, cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem.Handle);
		}

		/// <summary>
		/// Sets the initial values for the dialog controls, prior to displaying the dialog.
		/// This method should be called after creating, but prior to calling DoModeless. This
		/// overload is meant to be called from managed code.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="vwPattern">Find/replace values</param>
		/// <param name="rootSite">view</param>
		/// <param name="fReplace"><c>true</c> to initially display replace dialog page</param>
		/// <param name="fOverlays">ignored for now</param>
		/// <param name="owner">The main window that owns the rootsite</param>
		/// <param name="helpTopicProvider">help topic provider allows the dialog box class
		/// to specify the appropriate help topic path for this dialog</param>
		/// <param name="app">The application</param>
		/// <param name="wsEdit">writing system for the find and replace edit boxes</param>
		/// <exception cref="ArgumentNullException"></exception>
		/// <returns>
		/// true if the dialog was initialized properly, otherwise false.
		/// False indicates some problem and the find/replace dialog should not be
		/// shown at this time.
		/// </returns>
		/// <remarks>ENHANCE JohnT: it may need more arguments, for example, the name of the
		/// kind of object we can restrict the search to, a list of fields.</remarks>
		public bool SetDialogValues(LcmCache cache, IVwPattern vwPattern, IVwRootSite rootSite, bool fReplace, bool fOverlays, Form owner, IHelpTopicProvider helpTopicProvider, IApp app, int wsEdit)
		{
			Guard.AgainstNull(vwPattern, nameof(vwPattern));
			Guard.AgainstNull(cache, nameof(cache));

			fweditFindText.controlID = "Find";
			fweditReplaceText.controlID = "Replace";
			m_vwFindPattern = vwPattern;
			m_cache = cache;
			m_helpTopicProvider = helpTopicProvider;
			m_app = app;
			SetOwner(rootSite, owner);
			var readOnly = rootSite is SimpleRootSite simpleRootSite && simpleRootSite.ReadOnlyView;
			if (readOnly)
			{
				if (tabControls.Controls.Contains(tabReplace))
				{
					tabControls.Controls.Remove(tabReplace);
				}
			}
			else
			{
				if (!tabControls.Controls.Contains(tabReplace))
				{
					tabControls.Controls.Add(tabReplace);
				}
			}
			var wsf = m_cache.WritingSystemFactory;
			fweditFindText.WritingSystemFactory = fweditReplaceText.WritingSystemFactory = wsf;
			var defVernWs = m_cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem;
			FindText = TsStringUtils.EmptyString(defVernWs.Handle);
			ReplaceText = TsStringUtils.EmptyString(defVernWs.Handle);
			// Make sure each of the edit boxes has a reasonable writing system assigned.
			// (See LT-5130 for what can happen otherwise.)
			fweditFindText.WritingSystemCode = wsEdit;
			fweditReplaceText.WritingSystemCode = wsEdit;
			FindText = EnsureValidWs(wsEdit, vwPattern.Pattern);
			ReplaceText = EnsureValidWs(wsEdit, vwPattern.ReplaceWith);
			tabControls.SelectedTab = fReplace ? tabReplace : tabFind;
			tabControls_SelectedIndexChanged(null, new EventArgs());
			if (m_helpTopicProvider != null) // Will be null when running tests
			{
				helpProvider.HelpNamespace = FwDirectoryFinder.CodeDirectory + m_helpTopicProvider.GetHelpString("UserHelpFile");
			}
			SetCheckboxStates(vwPattern);
			_regexContextContextMenuFind?.Dispose();
			_regexContextContextMenuFind = new RegexHelperContextMenu(fweditFindText, m_helpTopicProvider);
			_regexContextContextMenuReplace?.Dispose();
			_regexContextContextMenuReplace = new RegexHelperContextMenu(fweditReplaceText, m_helpTopicProvider, false);
			EnableRegexMenuReplaceButton();
			// get the current selection text (if available) to fill in the find pattern.
			IVwSelection sel = null;
			if (rootSite?.RootBox != null)
			{
				sel = rootSite.RootBox.Selection;
			}
			if (sel == null)
			{
				// Set the TSS of the edit box to an empty string if it isn't set.
				if (FindText == null)
				{
					FindText = TsStringUtils.MakeString(string.Empty, m_cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem.Handle);
				}
			}
			else
			{
				// Get the selected text as the initial contents of the find box. Make a new TS String without
				// any character style so the character style from the selection will not be used. Also, if the
				// selection ends with a paragraph end sequence (CR/LF) then remove it.
				sel.GetFirstParaString(out var tssSel, " ", out _);
				if (tssSel == null)
				{
					// Not able to get ITsString from selection (e.g. if it is a picture)...
					SetFormatLabels();
					return true;
				}
				var bldr = tssSel.GetBldr();
				bldr.SetStrPropValue(0, bldr.Length, (int)FwTextPropType.ktptNamedStyle, null);
				// Unfortunately, the TsString returned by sel.GetFirstParaString() can have an
				// empty TsTextProps for at least the first run.  If that happens, we blow up
				// when we try to get the string later.
				for (var irun = 0; irun < bldr.RunCount; ++irun)
				{
					var ttp = bldr.get_Properties(irun);
					if (ttp.GetIntPropValues((int)FwTextPropType.ktptWs, out _) <= 0)
					{
						bldr.FetchRunInfo(irun, out var tri);
						bldr.SetIntPropValues(tri.ichMin, tri.ichLim, (int)FwTextPropType.ktptWs, 0, m_cache.DefaultAnalWs);
					}
				}
				RemoveEndOfPara(bldr);
				// We don't want to copy a multi-line selection into the find box.
				// Currently treating it as don't copy anything; another plausible option would be to copy the first line.
				var text = bldr.Text;
				if (text != null && (text.IndexOf('\r') >= 0 || text.IndexOf('\n') > 0))
				{
					bldr.Replace(0, bldr.Length, "", null);
				}
				// Set the TSS of the edit box if there is any text to set, or if there is no
				// TSS for the box, or if there is no text in the find box AND the selection is not a user prompt.
				// If the current selection is an IP AND we have a previous find text, we want to use that
				// instead of the current selection (TE-5127 and TE-5126).
				if (bldr.Length == 0 && vwPattern.Pattern != null)
				{
					FindText = vwPattern.Pattern;
				}
				else if ((bldr.Length != 0 || FindText == null || FindText.Length == 0) && tssSel.get_Properties(0).GetIntPropValues(SimpleRootSite.ktptUserPrompt, out _) != 1)
				{
					FindText = bldr.GetString();
				}
				if (FindText != null)
				{
					// Set the replace text box properties to be the same as the find text box.
					// The best we can do is take the properties of the first run which should
					// be fine for most cases.
					var props = FindText.get_Properties(0);
					var replaceBldr = TsStringUtils.MakeStrBldr();
					replaceBldr.Replace(0, 0, "", props);
					ReplaceText = replaceBldr.GetString();
				}
			}
			SetFormatLabels();
			return true;
		}

		private void EnableRegexMenuReplaceButton()
		{
			btnRegexMenuReplace.Visible = (tabControls.SelectedTab != tabFind && !DisableReplacePatternMatching);
			btnRegexMenuReplace.Enabled = chkUseRegularExpressions.Checked;
		}

		/// <summary>
		/// Set initial values, assuming default vernacular writing system for the find
		/// and replace edit boxes.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="vwPattern">Find/replace values</param>
		/// <param name="stylesheet">to use in text boxes</param>
		/// <param name="owner">The main window that owns the rootsite</param>
		/// <param name="helpTopicProvider">help topic provider allows the dialog box class
		/// to specify the appropriate help topic path for this dialog</param>
		/// <param name="app">The application</param>
		/// <returns>
		/// true if the dialog was initialized properly, otherwise false.
		/// False indicates some problem and the find/replace dialog should not be
		/// shown at this time.
		/// </returns>
		public bool SetDialogValues(LcmCache cache, IVwPattern vwPattern, IVwStylesheet stylesheet, Form owner, IHelpTopicProvider helpTopicProvider, IApp app)
		{
			return SetDialogValues(cache, vwPattern, stylesheet, owner, helpTopicProvider, app, cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem.Handle);
		}

		/// <summary>
		/// Sets the initial values for the dialog controls, prior to displaying the dialog.
		/// This method should be called after creating, but prior to calling DoModal. This
		/// overload is meant to be called from the Setup button of the Find/Replace tab
		/// of the Bulk Edit bar. Instead of having a root site and controls that allow the
		/// find/replace to be actually done, it just serves to edit the pattern.
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="vwPattern">Find/replace values</param>
		/// <param name="stylesheet">to use in text boxes</param>
		/// <param name="owner">The main window that owns the rootsite</param>
		/// <param name="helpTopicProvider">help topic provider allows the dialog box class
		/// to specify the appropriate help topic path for this dialog</param>
		/// <param name="app">The application</param>
		/// <param name="wsEdit">writing system used in the find/replace text boxes</param>
		/// <returns>
		/// true if the dialog was initialized properly, otherwise false.
		/// False indicates some problem and the find/replace dialog should not be
		/// shown at this time.
		/// </returns>
		/// <remarks>ENHANCE JohnT: it may need more arguments, for example, the name of the
		/// kind of object we can restrict the search to, a list of fields.</remarks>
		public bool SetDialogValues(LcmCache cache, IVwPattern vwPattern, IVwStylesheet stylesheet, Form owner, IHelpTopicProvider helpTopicProvider, IApp app, int wsEdit)
		{
			// Must set the stylesheet for the FwEdit boxes before calling SetDialogValues since
			// that call can reset the text in those boxes.
			fweditFindText.StyleSheet = fweditReplaceText.StyleSheet = stylesheet;
			// For now pass a null writing system string since it isn't used at all.
			if (!SetDialogValues(cache, vwPattern, null, true, false, owner, helpTopicProvider, app, wsEdit))
			{
				return false;
			}
			FindText = vwPattern.Pattern;
			// Reconfigure the dialog for this special purpose. The Find/Replace buttons go away,
			// we have an OK button which is the default.
			btnReplace.Hide();
			btnFindNext.Hide();
			btnReplaceAll.Hide();
			m_okButton.Show();
			m_inGetSpecs = true; // disables showing Replace buttons
			tabControls.TabPages.Remove(tabFind);
			AcceptButton = m_okButton;
			return true;
		}

		/// <summary>
		/// Sets the checkbox states.
		/// </summary>
		private void SetCheckboxStates(IVwPattern vwPattern)
		{
			// Set initial checkbox states
			chkMatchWS.Checked = vwPattern.MatchOldWritingSystem;
			chkMatchDiacritics.Checked = vwPattern.MatchDiacritics;
			chkMatchCase.Checked = vwPattern.MatchCase;
			chkMatchWholeWord.Checked = vwPattern.MatchWholeWord;
			if (chkUseRegularExpressions.Enabled)
			{
				chkUseRegularExpressions.Checked = vwPattern.UseRegularExpressions;
			}
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

			LastTextBoxInFocus = null;

			if (disposing)
			{
				components?.Dispose();
				if (m_cacheMadeLocally)
				{
					m_cache?.Dispose();
				}
				_regexContextContextMenuFind?.Dispose();
				_regexContextContextMenuReplace?.Dispose();
				if (m_messageFilterInstalled)
				{
					Application.RemoveMessageFilter(this);
					m_messageFilterInstalled = false;
				}
				m_findEnvironment?.Dispose();
			}
			m_helpTopicProvider = null;
			m_searchKiller = null;
			m_prevSearchText = null;
			m_vwRootsite = null;
			m_vwFindPattern = null;
			m_cache = null;
			_regexContextContextMenuReplace = null;
			_regexContextContextMenuFind = null;
			m_findEnvironment = null;
			base.Dispose(disposing);
		}
		#endregion // Construction, initialization, destruction

		#region Other methods
		/// <summary>
		/// Removes any end of paragraph marker from the string builder.
		/// </summary>
		private void RemoveEndOfPara(ITsStrBldr bldr)
		{
			var endOfPara = Environment.NewLine;
			// Remove any end of paragraph marker from the string builder.
			if (bldr.Length < endOfPara.Length)
			{
				return;
			}
			if (bldr.Text.EndsWith(endOfPara))
			{
				bldr.Replace(bldr.Length - endOfPara.Length, bldr.Length, null, null);
			}
		}

		/// <summary>
		/// Call this after SetDialogValues on startup to restore settings and have them
		/// saved on close.
		/// </summary>
		public void RestoreAndPersistSettingsIn(IPropertyTable propertyTable)
		{
			if (propertyTable == null)
			{
				return;
			}
			m_propertyTable = propertyTable;
		}

		/// <summary>
		/// If this is set true, the 'use regular expression' check is disabled in the Replace tab.
		/// </summary>
		public bool DisableReplacePatternMatching { get; set; }

		/// <summary />
		protected override void OnLayout(LayoutEventArgs levent)
		{
			// We want to save and adjust these sizes AFTER our handle is created
			// (which is one way to ensure that it is AFTER .NET has adjusted the
			// dialog size if our screen resolution is not 96 DPI), but only ONCE,
			// because after the first time we have changed the values we are basing
			// things on.
			if (IsHandleCreated && m_heightDlgLess == 0)
			{
				m_heightDlgMore = Height;
				m_heightTabControlMore = tabControls.Height;
				m_heightDlgLess = Height - panelSearchOptions.Height;
				m_heightTabControlLess = tabControls.Height - panelSearchOptions.Height;
				Height = m_heightDlgLess;
				tabControls.Height = m_heightTabControlLess;
				if (m_propertyTable != null)
				{
					// Now we have our natural size, we can properly adjust our location etc.
					var locWnd = m_propertyTable.GetValue<object>(kPersistenceLabel + "DlgLocation");
					var showMore = m_propertyTable.GetValue<object>(kPersistenceLabel + "ShowMore");
					if (showMore != null && "true" == (string)showMore)
					{
						btnMore_Click(this, new EventArgs());
					}
					if (locWnd != null)
					{
						var rect = new Rectangle((Point)locWnd, this.Size);
						ScreenHelper.EnsureVisibleRect(ref rect);
						DesktopBounds = rect;
						StartPosition = FormStartPosition.Manual;
					}
				}
			}
			base.OnLayout(levent);
		}

		/// <summary>
		/// Change the main window which owns this dialog. Since this dialog attempts to stay
		/// alive as long as the app is alive (or, as long as there is a main window open),
		/// the app should call this to re-assign an owner any time the existing owner is
		/// closing.
		/// </summary>
		/// <param name="rootSite">view</param>
		/// <param name="newOwner">The main window that owns the rootsite</param>
		public void SetOwner(IVwRootSite rootSite, Form newOwner)
		{
			m_vwRootsite = rootSite;
			if (m_vwRootsite != null && rootSite.RootBox != null)
			{
				fweditFindText.StyleSheet = fweditReplaceText.StyleSheet = rootSite.RootBox.Stylesheet;
			}
			if (newOwner != null && Owner != newOwner)
			{
				Owner = newOwner;
				m_vwSelectionForPattern = null;
				m_findEnvironment = null;
			}
		}

		/// <summary>
		/// Check that the ws in the ITsString is still valid.  If it isn't, set it to the given
		/// default value.
		/// </summary>
		private ITsString EnsureValidWs(int wsEdit, ITsString tss)
		{
			if (tss != null)
			{
				var tsb = tss.GetBldr();
				if (m_cache.LanguageWritingSystemFactoryAccessor.GetStrFromWs(tsb.get_Properties(0).GetIntPropValues((int)FwTextPropType.ktptWs, out var nVar)) == null)
				{
					tsb.SetIntPropValues(0, tsb.Length, (int)FwTextPropType.ktptWs, nVar, wsEdit);
					return tsb.GetString();
				}
			}
			return tss;
		}
		#endregion

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.Windows.Forms.Button btnHelp;
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FwFindReplaceDlg));
			System.Windows.Forms.Label lblFindText;
			this.tabControls = new System.Windows.Forms.TabControl();
			this.tabFind = new System.Windows.Forms.TabPage();
			this.tabReplace = new System.Windows.Forms.TabPage();
			this.panelSearchOptions = new System.Windows.Forms.Panel();
			this.chkUseRegularExpressions = new System.Windows.Forms.CheckBox();
			this.btnFormat = new System.Windows.Forms.Button();
			this.chkMatchCase = new System.Windows.Forms.CheckBox();
			this.chkMatchDiacritics = new System.Windows.Forms.CheckBox();
			this.chkMatchWholeWord = new System.Windows.Forms.CheckBox();
			this.chkMatchWS = new System.Windows.Forms.CheckBox();
			this.lblSearchOptions = new System.Windows.Forms.Label();
			this.panelBasic = new System.Windows.Forms.Panel();
			this.btnRegexMenuReplace = new System.Windows.Forms.Button();
			this.btnRegexMenuFind = new System.Windows.Forms.Button();
			this.lblReplaceFormat = new System.Windows.Forms.Label();
			this.lblReplaceFormatText = new System.Windows.Forms.Label();
			this.lblFindFormatText = new System.Windows.Forms.Label();
			this.lblFindFormat = new System.Windows.Forms.Label();
			this.btnClose = new System.Windows.Forms.Button();
			this.btnFindNext = new System.Windows.Forms.Button();
			this.btnMore = new System.Windows.Forms.Button();
			this.lblReplaceText = new System.Windows.Forms.Label();
			this.btnReplace = new System.Windows.Forms.Button();
			this.btnReplaceAll = new System.Windows.Forms.Button();
			this.m_okButton = new System.Windows.Forms.Button();
			this.fweditReplaceText = new SIL.FieldWorks.FwCoreDlgs.Controls.FwTextBox();
			this.fweditFindText = new SIL.FieldWorks.FwCoreDlgs.Controls.FwTextBox();
			this.mnuFormat = new System.Windows.Forms.ContextMenu();
			this.mnuWritingSystem = new System.Windows.Forms.MenuItem();
			this.mnuStyle = new System.Windows.Forms.MenuItem();
			btnHelp = new System.Windows.Forms.Button();
			lblFindText = new System.Windows.Forms.Label();
			this.tabControls.SuspendLayout();
			this.tabReplace.SuspendLayout();
			this.panelSearchOptions.SuspendLayout();
			this.panelBasic.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.fweditReplaceText)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.fweditFindText)).BeginInit();
			this.SuspendLayout();
			//
			// btnHelp
			//
			resources.ApplyResources(btnHelp, "btnHelp");
			btnHelp.Name = "btnHelp";
			btnHelp.Click += new System.EventHandler(this.btnHelp_Click);
			//
			// lblFindText
			//
			resources.ApplyResources(lblFindText, "lblFindText");
			lblFindText.Name = "lblFindText";
			//
			// tabControls
			//
			this.tabControls.Controls.Add(this.tabFind);
			this.tabControls.Controls.Add(this.tabReplace);
			resources.ApplyResources(this.tabControls, "tabControls");
			this.tabControls.Name = "tabControls";
			this.tabControls.SelectedIndex = 0;
			this.tabControls.SelectedIndexChanged += new System.EventHandler(this.tabControls_SelectedIndexChanged);
			//
			// tabFind
			//
			resources.ApplyResources(this.tabFind, "tabFind");
			this.tabFind.Name = "tabFind";
			this.tabFind.UseVisualStyleBackColor = true;
			//
			// tabReplace
			//
			this.tabReplace.Controls.Add(this.panelSearchOptions);
			this.tabReplace.Controls.Add(this.panelBasic);
			resources.ApplyResources(this.tabReplace, "tabReplace");
			this.tabReplace.Name = "tabReplace";
			this.tabReplace.UseVisualStyleBackColor = true;
			//
			// panelSearchOptions
			//
			this.panelSearchOptions.Controls.Add(this.chkUseRegularExpressions);
			this.panelSearchOptions.Controls.Add(this.btnFormat);
			this.panelSearchOptions.Controls.Add(this.chkMatchCase);
			this.panelSearchOptions.Controls.Add(this.chkMatchDiacritics);
			this.panelSearchOptions.Controls.Add(this.chkMatchWholeWord);
			this.panelSearchOptions.Controls.Add(this.chkMatchWS);
			this.panelSearchOptions.Controls.Add(this.lblSearchOptions);
			resources.ApplyResources(this.panelSearchOptions, "panelSearchOptions");
			this.panelSearchOptions.Name = "panelSearchOptions";
			this.panelSearchOptions.Paint += new System.Windows.Forms.PaintEventHandler(this.panel2_Paint);
			//
			// chkUseRegularExpressions
			//
			resources.ApplyResources(this.chkUseRegularExpressions, "chkUseRegularExpressions");
			this.chkUseRegularExpressions.Name = "chkUseRegularExpressions";
			this.chkUseRegularExpressions.CheckedChanged += new System.EventHandler(this.chkUseRegularExpressions_CheckedChanged);
			//
			// btnFormat
			//
			resources.ApplyResources(this.btnFormat, "btnFormat");
			this.btnFormat.Name = "btnFormat";
			this.btnFormat.Click += new System.EventHandler(this.btnFormat_Click);
			//
			// chkMatchCase
			//
			resources.ApplyResources(this.chkMatchCase, "chkMatchCase");
			this.chkMatchCase.Name = "chkMatchCase";
			//
			// chkMatchDiacritics
			//
			resources.ApplyResources(this.chkMatchDiacritics, "chkMatchDiacritics");
			this.chkMatchDiacritics.Name = "chkMatchDiacritics";
			//
			// chkMatchWholeWord
			//
			resources.ApplyResources(this.chkMatchWholeWord, "chkMatchWholeWord");
			this.chkMatchWholeWord.Name = "chkMatchWholeWord";
			//
			// chkMatchWS
			//
			resources.ApplyResources(this.chkMatchWS, "chkMatchWS");
			this.chkMatchWS.Name = "chkMatchWS";
			this.chkMatchWS.CheckedChanged += new System.EventHandler(this.chkMatchWS_CheckedChanged);
			//
			// lblSearchOptions
			//
			resources.ApplyResources(this.lblSearchOptions, "lblSearchOptions");
			this.lblSearchOptions.Name = "lblSearchOptions";
			//
			// panelBasic
			//
			this.panelBasic.Controls.Add(this.btnRegexMenuReplace);
			this.panelBasic.Controls.Add(this.btnRegexMenuFind);
			this.panelBasic.Controls.Add(this.lblReplaceFormat);
			this.panelBasic.Controls.Add(this.lblReplaceFormatText);
			this.panelBasic.Controls.Add(this.lblFindFormatText);
			this.panelBasic.Controls.Add(this.lblFindFormat);
			this.panelBasic.Controls.Add(this.btnClose);
			this.panelBasic.Controls.Add(btnHelp);
			this.panelBasic.Controls.Add(this.btnFindNext);
			this.panelBasic.Controls.Add(lblFindText);
			this.panelBasic.Controls.Add(this.btnMore);
			this.panelBasic.Controls.Add(this.lblReplaceText);
			this.panelBasic.Controls.Add(this.btnReplace);
			this.panelBasic.Controls.Add(this.btnReplaceAll);
			this.panelBasic.Controls.Add(this.m_okButton);
			this.panelBasic.Controls.Add(this.fweditReplaceText);
			this.panelBasic.Controls.Add(this.fweditFindText);
			resources.ApplyResources(this.panelBasic, "panelBasic");
			this.panelBasic.Name = "panelBasic";
			//
			// btnRegexMenuReplace
			//
			resources.ApplyResources(this.btnRegexMenuReplace, "btnRegexMenuReplace");
			this.btnRegexMenuReplace.Name = "btnRegexMenuReplace";
			this.btnRegexMenuReplace.Click += new System.EventHandler(this.btnRegexMenuReplace_Click);
			//
			// btnRegexMenuFind
			//
			resources.ApplyResources(this.btnRegexMenuFind, "btnRegexMenuFind");
			this.btnRegexMenuFind.Name = "btnRegexMenuFind";
			this.btnRegexMenuFind.Click += new System.EventHandler(this.btnRegexMenuFind_Click);
			//
			// lblReplaceFormat
			//
			resources.ApplyResources(this.lblReplaceFormat, "lblReplaceFormat");
			this.lblReplaceFormat.Name = "lblReplaceFormat";
			//
			// lblReplaceFormatText
			//
			resources.ApplyResources(this.lblReplaceFormatText, "lblReplaceFormatText");
			this.lblReplaceFormatText.Name = "lblReplaceFormatText";
			//
			// lblFindFormatText
			//
			resources.ApplyResources(this.lblFindFormatText, "lblFindFormatText");
			this.lblFindFormatText.Name = "lblFindFormatText";
			//
			// lblFindFormat
			//
			resources.ApplyResources(this.lblFindFormat, "lblFindFormat");
			this.lblFindFormat.Name = "lblFindFormat";
			//
			// btnClose
			//
			this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			resources.ApplyResources(this.btnClose, "btnClose");
			this.btnClose.Name = "btnClose";
			this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
			//
			// btnFindNext
			//
			this.btnFindNext.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			resources.ApplyResources(this.btnFindNext, "btnFindNext");
			this.btnFindNext.Name = "btnFindNext";
			this.btnFindNext.Click += new System.EventHandler(this.OnFindNext);
			//
			// btnMore
			//
			resources.ApplyResources(this.btnMore, "btnMore");
			this.btnMore.Name = "btnMore";
			this.btnMore.Click += new System.EventHandler(this.btnMore_Click);
			//
			// lblReplaceText
			//
			resources.ApplyResources(this.lblReplaceText, "lblReplaceText");
			this.lblReplaceText.Name = "lblReplaceText";
			//
			// btnReplace
			//
			resources.ApplyResources(this.btnReplace, "btnReplace");
			this.btnReplace.Name = "btnReplace";
			this.btnReplace.Click += new System.EventHandler(this.OnReplace);
			//
			// btnReplaceAll
			//
			resources.ApplyResources(this.btnReplaceAll, "btnReplaceAll");
			this.btnReplaceAll.Name = "btnReplaceAll";
			this.btnReplaceAll.Click += new System.EventHandler(this.OnReplaceAll);
			//
			// m_okButton
			//
			resources.ApplyResources(this.m_okButton, "m_okButton");
			this.m_okButton.Name = "m_okButton";
			this.m_okButton.Click += new System.EventHandler(this.m_okButton_Click);
			//
			// fweditReplaceText
			//
			this.fweditReplaceText.AcceptsReturn = false;
			this.fweditReplaceText.AdjustStringHeight = true;
			resources.ApplyResources(this.fweditReplaceText, "fweditReplaceText");
			this.fweditReplaceText.BackColor = System.Drawing.SystemColors.Window;
			this.fweditReplaceText.controlID = null;
			this.fweditReplaceText.HasBorder = true;
			this.fweditReplaceText.Name = "fweditReplaceText";
			this.fweditReplaceText.SuppressEnter = false;
			this.fweditReplaceText.WordWrap = false;
			this.fweditReplaceText.Leave += new System.EventHandler(this.FwTextBox_Leave);
			this.fweditReplaceText.Enter += new System.EventHandler(this.FwTextBox_Enter);
			//
			// fweditFindText
			//
			this.fweditFindText.AcceptsReturn = false;
			this.fweditFindText.AdjustStringHeight = true;
			resources.ApplyResources(this.fweditFindText, "fweditFindText");
			this.fweditFindText.BackColor = System.Drawing.SystemColors.Window;
			this.fweditFindText.controlID = null;
			this.fweditFindText.HasBorder = true;
			this.fweditFindText.Name = "fweditFindText";
			this.fweditFindText.SuppressEnter = false;
			this.fweditFindText.WordWrap = false;
			this.fweditFindText.Leave += new System.EventHandler(this.FwTextBox_Leave);
			this.fweditFindText.Enter += new System.EventHandler(this.FwTextBox_Enter);
			//
			// mnuFormat
			//
			this.mnuFormat.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
			this.mnuWritingSystem,
			this.mnuStyle});
			//
			// mnuWritingSystem
			//
			this.mnuWritingSystem.Index = 0;
			resources.ApplyResources(this.mnuWritingSystem, "mnuWritingSystem");
			//
			// mnuStyle
			//
			this.mnuStyle.Index = 1;
			resources.ApplyResources(this.mnuStyle, "mnuStyle");
			//
			// FwFindReplaceDlg
			//
			this.AcceptButton = this.btnFindNext;
			resources.ApplyResources(this, "$this");
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this.btnClose;
			this.Controls.Add(this.tabControls);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.KeyPreview = true;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FwFindReplaceDlg";
			this.ShowInTaskbar = false;
			this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
			this.tabControls.ResumeLayout(false);
			this.tabReplace.ResumeLayout(false);
			this.panelSearchOptions.ResumeLayout(false);
			this.panelSearchOptions.PerformLayout();
			this.panelBasic.ResumeLayout(false);
			this.panelBasic.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.fweditReplaceText)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.fweditFindText)).EndInit();
			this.ResumeLayout(false);

		}
		#endregion

		#region Event handlers
		/// <summary>
		/// Handles the CheckedChanged event of the chkMatchWS control.
		/// </summary>
		private void chkMatchWS_CheckedChanged(object sender, EventArgs e)
		{
			SetFormatLabels();
		}

		/// <summary>
		/// Change Close button back to Cancel whenever we re-open the dialog
		/// </summary>
		protected override void OnVisibleChanged(EventArgs e)
		{
			if (Visible)
			{   // this means we were hidden and now will be visible
				var resources = new System.Resources.ResourceManager(typeof(FwFindReplaceDlg));
				btnClose.Text = resources.GetString("btnClose.Text");

				// set the "initial activate" state to true. This will allow the OnActivated
				// message handler to set the focus to the find text box.
				m_initialActivate = true;
			}
			base.OnVisibleChanged(e);
		}

		/// <summary>
		/// Open the help window when the help button is pressed.
		/// </summary>
		private void btnHelp_Click(object sender, System.EventArgs e)
		{
			SetHelpTopicId();
			ShowHelp.ShowHelpTopic(m_helpTopicProvider, s_helpTopic);
		}

		/// <summary>
		/// Method to handle the Click event of the Format button. Displays the format menu.
		/// </summary>
		private void btnFormat_Click(object sender, EventArgs e)
		{
			mnuFormat.MenuItems.Clear();
			PopulateWritingSystemMenu();
			PopulateStyleMenu();
			mnuFormat.MenuItems.Add(mnuWritingSystem);
			mnuFormat.MenuItems.Add(mnuStyle);
			mnuFormat.Show(btnFormat, new Point(0, btnFormat.Height));
		}

		/// <summary>
		/// This gets called whenever the user selects a writing system
		/// </summary>
		private void WritingSystemMenu_Click(object sender, EventArgs e)
		{
			if (sender is MenuItem menuItem)
			{
				if (menuItem.Parent == mnuWritingSystem)
				{
					ApplyWS(LastTextBoxInFocus, (int)menuItem.Tag);
				}
			}
		}

		/// <summary>
		/// This gets called whenever the user selects a style from the style menu
		/// </summary>
		private void StyleMenu_Click(object sender, EventArgs e)
		{
			if (sender is MenuItem menuItem)
			{
				if (menuItem.Parent == mnuStyle)
				{
					ApplyStyle(LastTextBoxInFocus, menuItem.Text);
				}
			}
		}

		/// <summary>
		/// Handle the Find Next button click event
		/// </summary>
		protected void OnFindNext(object sender, EventArgs e)
		{
			if (DataUpdateMonitor.IsUpdateInProgress())
			{
				return;
			}
			FindNext();
			// After a find next, focus the find box and select the text in it.
			fweditFindText.Select();
			fweditFindText.SelectAll();
			if (Platform.IsMono)
			{
				RemoveWaitCursor(this);
			}
		}

		/// <summary>
		/// Remove the wait cursor, which is left behind on several controls when the
		/// DataUpdateMonitor object is disposed.  This is a patch over a bug in Mono
		/// as far as I can tell. It fixes FWNX-659.
		/// </summary>
		/// <remarks>
		/// The strange thing is that the cursor on all these controls seems to already
		/// be set to Cursors.Default, but this fix works.
		/// Method is only used on Linux.
		/// </remarks>
		private void RemoveWaitCursor(Control ctl)
		{
			Debug.Assert(Platform.IsMono, "This method is only needed with Mono on Linux");
			foreach (var c in ctl.Controls)
			{
				if (c is Control control)
				{
					RemoveWaitCursor(control);
				}
			}
			ctl.Cursor = Cursors.Default;
		}

		/// <summary>
		/// Handle the Replace button click event. The first time the user presses the
		/// "Replace" button, we just find the next match; the second time we actually do the
		/// replace and then go on to find the next match.
		/// </summary>
		private void OnReplace(object sender, EventArgs e)
		{
			if (DataUpdateMonitor.IsUpdateInProgress())
			{
				return;  // discard event
			}
			var rootSite = ActiveView;
			if (rootSite == null)
			{
				return;
			}
			using (var undoTaskHelper = new UndoTaskHelper(rootSite, "kstidUndoReplace"))
			{
				// Replace the selection with the replacement text, but don't allow deletion
				// of objects, such as footnotes in the replaced text.
				DoReplace(CurrentSelection);
				undoTaskHelper.RollBack = false;
			}
			// After a replace, focus the find box and select the text in it.
			fweditFindText.Select();
			fweditFindText.SelectAll();
		}

		/// <summary>
		/// Handle the Replace All button click event.
		/// </summary>
		private void OnReplaceAll(object sender, EventArgs e)
		{
			if (DataUpdateMonitor.IsUpdateInProgress())
			{
				return;  // discard event
			}
			ResourceHelper.MakeUndoRedoLabels("kstidUndoReplace", out var undo, out var redo);
			using (new DataUpdateMonitor(this, "ReplaceAll"))
			using (new WaitCursor(this, true))
			using (var undoHelper = new UndoableUnitOfWorkHelper(m_cache.ServiceLocator.GetInstance<IActionHandler>(), undo, redo))
			{
				DoReplaceAll();
				undoHelper.RollBack = false;
			}
			// After a replace, focus the find box and select the text in it.
			fweditFindText.Select();
			fweditFindText.SelectAll();
		}

		/// <summary>
		/// Does the replace all.
		/// </summary>
		protected void DoReplaceAll()
		{
			var replaceCount = 0;
			m_app?.EnableMainWindows(false);
			var rootSite = ActiveView;
			if (rootSite == null)
			{
				return;
			}
			PrepareToFind();
			m_inReplace = true;
			// suppress the standard message; ReplaceAll shows its own based on how many matches we find (although the text is the same for 0 found)
			if (MatchNotFound == null)
			{
				MatchNotFound += SuppressAllMatchNotFoundMessages;
			}
			try
			{
				var start = DateTime.Now;
				// Do the replace all
				SetupFindPattern();
				if (PatternIsValid())
				{
					m_searchKiller.AbortRequest = false;
					m_searchKiller.Control = this;  // used for redrawing
					m_searchKiller.StopControl = btnClose;  // need to know the stop button
					m_vwFindPattern.ReplaceWith = ReplaceText;
					var initialSelection = CurrentSelection;
					InitializeFindEnvironment(rootSite);
					// TODO Enhance (Hasso) 2015.08: place CurrentSelection in a variable?
					// Starting at the beginning is a workaround for LT-16537 (starting position is not adjusted after each replacement).
					for (FindFromAndWrap(SelectAtBeginning(), true); IsReplacePossible(CurrentSelection); FindFromAndWrap(CurrentSelection, false))
					{
						DoReplacement(CurrentSelection, m_vwFindPattern.ReplacementText, m_vwFindPattern.MatchOldWritingSystem, (FindText.Length == 0));
						replaceCount++;
					}
					// Reset the selection to what the user had selected before the ReplaceAll.
					// TODO Enhance (Hasso) 2015.08: adjust the selection to reflect length changes in the text (easier once LT-16537 is fixed)
					SelectionHelper.Create(initialSelection, rootSite).SetSelection(rootSite, true, true, VwScrollSelOpts.kssoDefault);
					Debug.WriteLine("Replace all took " + (DateTime.Now - start));
				}
			}
			finally
			{
				PostpareToFind(replaceCount > 0);
				m_app?.EnableMainWindows(true);
				m_inReplace = false;
			}

			// Display a dialog box if the replace all finished or was stopped
			if (replaceCount > 0)
			{
				var fShowMsg = true;
				var msg = string.Format(m_searchKiller.AbortRequest ? FwCoreDlgs.kstidReplaceAllStopped : FwCoreDlgs.kstidReplaceAllDone, replaceCount);
				if (MatchNotFound != null)
				{
					fShowMsg = MatchNotFound(this, msg, MatchType.ReplaceAllFinished);
				}
				if (!fShowMsg)
				{
					return;
				}
				try
				{
					if (MiscUtils.IsUnix)
					{
						// Get a wait cursor to display when waiting for the messagebox
						// to show. See FWNX-660.
						tabReplace.UseWaitCursor = true;
						tabReplace.Parent.UseWaitCursor = true;
					}
					MessageBox.Show(Owner, msg, m_app.ApplicationName);
				}
				finally
				{
					if (MiscUtils.IsUnix)
					{
						tabReplace.Parent.UseWaitCursor = false;
						tabReplace.UseWaitCursor = false;
					}
				}
			}
			else if (replaceCount == 0)
			{
				InternalMatchNotFound(true);
			}
		}

		/// <summary>
		/// Suppress any messages about matches not found.
		/// </summary>
		private static bool SuppressAllMatchNotFoundMessages(object sender, string defaultMsg, MatchType type)
		{
			return false;
		}

		/// <summary>
		/// Stops a find/replace
		/// </summary>
		private void OnStop(object sender, EventArgs e)
		{
			m_searchKiller.AbortRequest = true;
		}

		/// <summary>
		/// Close the Find/Replace dialog
		/// </summary>
		private void btnClose_Click(object sender, EventArgs e)
		{
			Hide();
		}

		/// <summary>
		/// Handle a click on the OK button.
		/// </summary>
		private void m_okButton_Click(object sender, EventArgs e)
		{
			if (!CanFindNext())
			{
				DialogResult = DialogResult.Cancel;
				Close();
				return;
			}
			SaveDialogValues(); // This needs to be done before the Regex is checked because it sets up m_vwFindPattern
			// LT-3310 - make sure if it's a regular expression that it is valid.
			// The following technique is what was added to the filtering code, so
			// it makes some sense to follow that pattern here.  This allows us to
			// look for an ICU RegEx error code first and handle it here in the dlg.
			if (PatternIsValid())
			{
				DialogResult = DialogResult.OK;
				Close();
			}
		}

		/// <summary>
		/// Verifies the pattern is valid. If user wants to use a regex, this validates the
		/// regular expression.
		/// </summary>
		/// <returns><c>true</c> if regular expression is valid or if we don't use regular
		/// expressions, <c>false</c> if regEx is invalid.</returns>
		private bool PatternIsValid()
		{
			if (chkUseRegularExpressions.Checked)
			{
				IMatcher testMatcher = new RegExpMatcher(m_vwFindPattern);
				if (!testMatcher.IsValid())
				{
					DisplayInvalidRegExMessage(testMatcher.ErrorMessage());
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Displays a message box that the regular expression is invalid.
		/// </summary>
		protected virtual void DisplayInvalidRegExMessage(string errorMessage)
		{
			MessageBox.Show(this, string.Format(FwCoreDlgs.kstidErrorInRegEx, errorMessage), FwCoreDlgs.kstidErrorInRegExHeader, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}

		/// <summary>
		/// Handle the event when the check on "Use Regular Expressions" changes.
		/// </summary>
		private void chkUseRegularExpressions_CheckedChanged(object sender, EventArgs e)
		{
			if (chkUseRegularExpressions.Checked)
			{
				chkMatchDiacritics.Enabled = false;
				chkMatchDiacritics.Checked = false;
				chkMatchWholeWord.Enabled = false;
				chkMatchWholeWord.Checked = false;
				chkMatchWS.Enabled = false;
				chkMatchWS.Checked = false;
				btnRegexMenuFind.Enabled = btnRegexMenuReplace.Enabled = true;
			}
			else
			{
				chkMatchDiacritics.Enabled = true;
				chkMatchWholeWord.Enabled = true;
				chkMatchWS.Enabled = true;
				btnRegexMenuFind.Enabled = btnRegexMenuReplace.Enabled = false;
			}
		}

		/// <summary>
		/// Instead of closing, just try to hide.
		/// </summary>
		protected override void OnClosing(CancelEventArgs e)
		{
			// Save location.
			if (m_propertyTable != null)
			{
				var propertyName = kPersistenceLabel + "DlgLocation";
				m_propertyTable.SetProperty(propertyName, Location, true, true);
				propertyName = kPersistenceLabel + "ShowMore";
				m_propertyTable.SetProperty(propertyName, Height == m_heightDlgMore ? "true" : "false", true, true);
			}
			base.OnClosing(e);
			// If no other handler of this event tried to intervene, the dialog itself will
			// prevent closing and just hide itself.
			if (e.Cancel == false && !m_inGetSpecs)
			{
				e.Cancel = true;
				Hide();
			}
		}

		/// <summary>
		/// When the user switches between Find and Replace tabs, we need to transfer ownership
		/// of the panels that hold the controls and hide/show/change controls as appropriate.
		/// </summary>
		private void tabControls_SelectedIndexChanged(object sender, System.EventArgs e)
		{
			if (tabControls.SelectedTab == tabFind)
			{
				// If a replace is in progress, don't allow a change to the find tab
				if (m_inReplace)
				{
					tabControls.SelectedTab = tabReplace;
					return;
				}
				SuspendLayout();
				tabReplace.Controls.Clear();
				tabFind.Controls.Add(panelSearchOptions);
				tabFind.Controls.Add(panelBasic);
				btnReplace.Hide();
				btnReplaceAll.Hide();
				btnMore.Location = btnReplaceAll.Location;
				chkUseRegularExpressions.Enabled = true;
				fweditReplaceText.Hide();
				lblReplaceText.Hide();
				fweditFindText.Select();
			}
			else
			{
				// if a find is in progress, don't allow a switch to the replace tab
				if (m_inFind)
				{
					tabControls.SelectedTab = tabFind;
					return;
				}
				SuspendLayout();
				tabFind.Controls.Clear();
				tabReplace.Controls.Add(panelSearchOptions);
				tabReplace.Controls.Add(panelBasic);
				if (!m_inGetSpecs)
				{
					btnReplace.Show();
					btnReplaceAll.Show();
				}
				// Move More button beside Replace button
				btnMore.Location = new Point(btnReplace.Location.X - btnReplace.Width - 6, btnReplace.Location.Y);
				fweditReplaceText.Show();
				lblReplaceText.Show();
				fweditFindText.Select();
				if (DisableReplacePatternMatching && (ModifierKeys & Keys.Shift) != Keys.Shift)
				{
					if (chkUseRegularExpressions.Checked)
					{
						chkUseRegularExpressions.Checked = false;
					}
					chkUseRegularExpressions.Enabled = false;
				}
			}
			SetHelpTopicId();
			SetFormatLabels();
			EnableRegexMenuReplaceButton();
			ResumeLayout();
		}

		private void SetHelpTopicId()
		{
			// there were two help topics. Now there is just one.
			// NOTE: since this is a common dialog, don't change the help topic ids! Instead,
			// change the string that your help topic provider returns for those two help
			// topic ids (i.e. change LexTextDll\HelpTopicPaths.resx or TeResources\HelpTopicPaths.resx
			// See C:\fw\Src\FwResources\HelpTopicPaths.resx
			if (tabControls.SelectedTab == tabFind)
			{
				s_helpTopic = "khtpFind"; // default
				if (m_app?.ActiveMainWindow is IFindAndReplaceContext mainWindow)
				{
					s_helpTopic = mainWindow.FindTabHelpId ?? s_helpTopic;
				}
			}
			else
			{
				s_helpTopic = "khtpReplace";
			}
			if (tabControls != null && tabControls.TabCount <= 1 && s_helpTopic != "khtpFindNotebook")   //find/replace help topic for lexicon
			{
				s_helpTopic = "khtpLexFind";
			}
			if (Text == string.Format(FwCoreDlgs.khtpBulkReplaceTitle))
			{
				s_helpTopic = "khtpBulkReplace";
			}
			if (m_helpTopicProvider != null) // It will be null if we are running under the test program
			{
				helpProvider.SetHelpKeyword(this, m_helpTopicProvider.GetHelpString(s_helpTopic));
			}
		}

		/// <summary>
		/// Show more options.
		/// </summary>
		private void btnMore_Click(object sender, EventArgs e)
		{
			btnMore.Text = FwCoreDlgs.kstidFindLessButtonText;
			btnMore.Image = ResourceHelper.LessButtonDoubleArrowIcon;
			btnMore.Click -= btnMore_Click;
			btnMore.Click += btnLess_Click;
			tabControls.Height = m_heightTabControlMore;
			Height = m_heightDlgMore;
			panelSearchOptions.Visible = true;
		}

		/// <summary>
		/// Show fewer options.
		/// </summary>
		public void btnLess_Click(object sender, EventArgs e)
		{
			btnMore.Text = m_sMoreButtonText;
			btnMore.Image = ResourceHelper.MoreButtonDoubleArrowIcon;
			btnMore.Click += btnMore_Click;
			btnMore.Click -= btnLess_Click;
			tabControls.Height = m_heightTabControlLess;
			Height = m_heightDlgLess;
			panelSearchOptions.Visible = false;
		}

		/// <summary>
		/// Show the Regex Helper context menu for Find
		/// </summary>
		private void btnRegexMenuFind_Click(object sender, EventArgs e)
		{
			_regexContextContextMenuFind.Show(btnRegexMenuFind, new System.Drawing.Point(btnRegexMenuFind.Width, 0));
		}

		/// <summary>
		/// Show the Regex Helper context menu for Replace
		/// </summary>
		private void btnRegexMenuReplace_Click(object sender, EventArgs e)
		{
			_regexContextContextMenuReplace.Show(btnRegexMenuReplace, new System.Drawing.Point(btnRegexMenuFind.Width, 0));
		}

		/// <summary>
		/// Draws an etched line on the dialog to separate the Search Options from the
		/// basic controls.
		/// </summary>
		private void panel2_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
		{
			const int dxMargin = 10;
			var left = lblSearchOptions.Right;
			LineDrawing.Draw(e.Graphics, left, (lblSearchOptions.Top + lblSearchOptions.Bottom) / 2, tabControls.Right - left - dxMargin, LineTypes.Etched);
		}

		/// <summary>
		/// Handle special keystrokes in the dialog.
		/// </summary>
		protected override void OnKeyDown(KeyEventArgs e)
		{
			// Handle the F3 (Find Next shortcut) when the dialog is active (but not in spec-only
			// mode, since that mode can't actually perform a Find!)
			if (!m_inGetSpecs && e.KeyCode == Keys.F3)
			{
				OnFindNext(null, EventArgs.Empty);
			}
			else
			{
				base.OnKeyDown(e);
			}
		}

		/// <summary>
		/// Activate dialog.
		/// </summary>
		protected override void OnActivated(EventArgs e)
		{
			if (!m_messageFilterInstalled && !DesignMode)
			{
				m_messageFilterInstalled = true;
				Application.AddMessageFilter(this);
			}
			base.OnActivated(e);
			// TODO (TimS): possibly make ShowRangeSelAfterLostFocus an interface method so
			// other apps (i.e. DN) can do this.
			if (m_vwRootsite is SimpleRootSite simpleRootSite && !simpleRootSite.IsDisposed)
			{
				simpleRootSite.ShowRangeSelAfterLostFocus = true;
			}
			if (m_initialActivate)
			{
				fweditFindText.Select();
				m_initialActivate = false;
			}
		}

		/// <summary>
		/// Remove the message filter when the dialog loses focus
		/// (Don't mistake this for onLoseFocus...this dialog never loses focus, it never
		/// has it, only it's sub-controls do.)
		/// </summary>
		protected override void OnDeactivate(EventArgs e)
		{
			if (m_messageFilterInstalled && !DesignMode)
			{
				Application.RemoveMessageFilter(this);
				m_messageFilterInstalled = false;
			}
			base.OnDeactivate(e);
			// TODO (TimS): possibly make ShowRangeSelAfterLostFocus an interface method so
			// other apps (i.e. DN) can do this.
			if (m_vwRootsite is SimpleRootSite simpleRootSite && !simpleRootSite.IsDisposed)
			{
				simpleRootSite.ShowRangeSelAfterLostFocus = false;
			}
		}

		/// <summary>
		/// When the focus arrives on a TSS edit control, the other edit control needs to have
		/// the selection removed from it. Also, the entered box needs to have all of the text
		/// selected.
		/// </summary>
		private void FwTextBox_Enter(object sender, EventArgs e)
		{
			if (tabControls.SelectedTab == tabReplace)
			{
				if (sender == fweditFindText)
				{
					fweditReplaceText.RemoveSelection();
				}
				else
				{
					fweditFindText.RemoveSelection();
				}
			}
			((FwTextBox)sender).SelectAll();
		}

		/// <summary>
		/// Needed to keep track of the last Tss edit control to have focus, for the purpose of
		/// setting styles, etc.
		/// </summary>
		private void FwTextBox_Leave(object sender, EventArgs e)
		{
			LastTextBoxInFocus = (FwTextBox)sender;
		}

		/// <summary>
		/// Handle a text changed event in an FW edit box. The style labels need to be
		/// updated when the text changes.
		/// </summary>
		private void HandleTextChanged(object sender, EventArgs e)
		{
			SetFormatLabels();
		}

		/// <summary>
		/// If enable is false, save the current enable state of the control and disable it.
		/// If it is true, restore the previous enable state of each control.
		/// </summary>
		private void AdjustControlState(Control ctrl, bool enable)
		{
			if (m_enableStates == null)
			{
				return;
			}
			if (enable)
			{
				var reallyEnable = true;
				if (m_enableStates.ContainsKey(ctrl))
				{
					reallyEnable = m_enableStates[ctrl];
				}
				ctrl.Enabled = reallyEnable;
			}
			else
			{
				m_enableStates[ctrl] = ctrl.Enabled; // Remember it's previous enabled state.
				ctrl.Enabled = false;
			}
		}
		#endregion

		#region Methods where the work is actually done.
		/// <summary>
		/// Enables or disables all the controls except the close/stop/cancel button on the
		/// find/replace dialog.
		/// </summary>
		private void EnableControls(bool newEnableStatus)
		{
			if (!newEnableStatus)
			{
				if (m_enableStates == null)
				{
					m_enableStates = new Dictionary<Control, bool>();
				}
				else
				{
					return; // already disabled, don't remember disabled state.
				}
			}
			foreach (Control ctrl in panelBasic.Controls)
			{
				if (ctrl != btnClose)
				{
					AdjustControlState(ctrl, newEnableStatus);
				}
			}
			foreach (Control ctrl in panelSearchOptions.Controls)
			{
				AdjustControlState(ctrl, newEnableStatus);
			}
			if (newEnableStatus)
			{
				m_enableStates.Clear();
				m_enableStates = null; // So we know to save on next disable.
			}
		}

		/// <summary>
		/// Replace the existing selection with the string in the replace box, then find the next occurrance, if any.
		/// </summary>
		protected void DoReplace(IVwSelection sel)
		{
			SetupFindPattern();
			if (IsReplacePossible(sel))
			{
				// See if we are just trying to replace formatting.
				var fEmptySearchPattern = (FindText.Length == 0);
				m_vwFindPattern.ReplaceWith = ReplaceText;
				DoReplacement(sel, m_vwFindPattern.ReplacementText, m_vwFindPattern.MatchOldWritingSystem, fEmptySearchPattern);
			}
			else
			{
				// REVIEW(TimS): Should we beep or something to tell the user that the replace
				// could not happen (TE-8289)?
			}
			btnClose.Text = FwCoreDlgs.kstidClose;
			FindNext();
		}

		/// <summary>
		/// Find the next match.
		/// </summary>
		public void FindNext()
		{
			Find(true);
		}

		/// <summary>
		/// Find the previous match.
		/// </summary>
		public void FindPrevious()
		{
			Find(false);
		}

		/// <summary>
		/// Executes a replace.
		/// </summary>
		public void Replace()
		{
			OnReplace(null, new EventArgs());
		}

		private IVwRootSite ActiveView
		{
			get
			{
				if (m_vwRootsite != null)
				{
					if (!(m_vwRootsite is Control))
					{
						return m_vwRootsite; // Maybe in testing? Just playing safe.
					}
					if (!((Control)m_vwRootsite).IsDisposed)
					{
						return m_vwRootsite;
					}
				}
				// if the current one is null or disposed, see if we can get one from the main window
				if (Owner != null && !Owner.IsDisposed)
				{
					try
					{
						if (ReflectionHelper.GetProperty(Owner, "ActiveView") is IVwRootSite newSite)
						{
							m_vwRootsite = newSite;
						}
					}
					catch (Exception)
					{
					}
				}
				if (m_vwRootsite != null)
				{
					if (!(m_vwRootsite is Control))
					{
						return m_vwRootsite; // Maybe in testing? Just playing safe.
					}
					if (!((Control)m_vwRootsite).IsDisposed)
					{
						return m_vwRootsite;
					}
				}
				return null; // If we can't find anything better than a disposed control, return null.
			}
		}

		/// <summary>
		/// Find the next match based on the current pattern settings
		/// </summary>
		private void Find(bool fSearchForward)
		{
			// If no find was done before, show the dialog or focus it.
			if (!btnFindNext.Enabled)
			{
				if (!Visible)
				{
					Show();
				}
				else
				{
					Focus();
				}
				return;
			}
			var rootSite = ActiveView;
			if (rootSite == null)
			{
				return;
			}
			if (m_fLastDirectionForward != fSearchForward)
			{
				// Changing search direction. Reset current selection (resets the search limit)
				m_vwSelectionForPattern = null;
				m_fLastDirectionForward = fSearchForward;
			}
			SetupFindPattern();
			// Get the selection from the root box in order to compare it with the one from
			// the pattern.
			var vwselRootb = rootSite.RootBox.Selection;
			// If the pattern's selection is different from the current selection in the
			// rootbox or if a new search has been started then set things up to begin
			// searching at the current selection.
			var fFirstTry = (m_vwSelectionForPattern == null || m_vwSelectionForPattern != vwselRootb);
			if (fFirstTry)
			{
				InitializeFindEnvironment(rootSite, fSearchForward);
			}
			Debug.Assert(m_findEnvironment != null);
			if (vwselRootb == null)
			{
				vwselRootb = rootSite.RootBox.MakeSimpleSel(true, true, false, true);
			}
			// Find the pattern.
			if (vwselRootb != null)
			{
				// Even though the find doesn't technically update any data, we don't
				// want the user to be able to change the data underneath us while the
				// find is happening.
				using (new DataUpdateMonitor(this, "Find"))
				{
					// Change the cancel button to a stop
					PrepareToFind();
					m_inFind = true;
					try
					{
						if (PatternIsValid())
						{
							m_searchKiller.AbortRequest = false;
							FindFromAndWrap(vwselRootb, fFirstTry);
						}
					}
					finally
					{
						PostpareToFind(false);
						m_inFind = false;
					}
				}
			}
		}

		private void InitializeFindEnvironment(IVwRootSite rootSite, bool fSearchForward = true)
		{
			rootSite.RootBox.GetRootObject(out var hvoRoot, out var vc, out var frag, out _);
			m_findEnvironment?.Dispose();
			m_findEnvironment = fSearchForward
				? new FindCollectorEnv(vc, DataAccess, hvoRoot, frag, m_vwFindPattern, m_searchKiller)
				: new ReverseFindCollectorEnv(vc, DataAccess, hvoRoot, frag, m_vwFindPattern, m_searchKiller);
		}

		/// <summary>
		/// Prepares to find: change the Close button to a Stop button; disable all other controls.
		/// </summary>
		private void PrepareToFind()
		{
			// Change the close button into the 'Stop' button
			btnClose.Tag = btnClose.Text;
			btnClose.Text = FwCoreDlgs.kstidStop;
			btnClose.Click -= btnClose_Click;
			btnClose.Click += OnStop;
			// Disable controls
			EnableControls(false);
			btnClose.Focus();
		}

		/// <summary>
		/// Postpares to find: reset controls to how they were before the find; remove NoMatchFound from the MatchNotFound handler
		/// </summary>
		/// <param name="fMakeCloseBtnSayClose">True to change the the close button to say "Close";
		/// otherwise it will go back to whatever it was"</param>
		private void PostpareToFind(bool fMakeCloseBtnSayClose)
		{
			// Enable controls
			EnableControls(true);
			// Restore the close button
			btnClose.Text = fMakeCloseBtnSayClose ? FwCoreDlgs.kstidClose : (string)btnClose.Tag;
			btnClose.Click += btnClose_Click;
			btnClose.Click -= OnStop;
			MatchNotFound -= SuppressAllMatchNotFoundMessages;
		}

		/// <summary>
		/// Setup and save the find pattern, clear the selection for the pattern.  This is done at the start of a NEW search only.
		/// </summary>
		private void SetupFindPattern()
		{
			if (m_prevSearchText == null || !m_prevSearchText.Equals(FindText))
			{
				m_vwSelectionForPattern = null;
				m_prevSearchText = FindText;
				SaveDialogValues(); // set up m_vwFindPattern
			}
		}

		/// <summary>
		/// Save the values set in the dialog in the pattern.
		/// </summary>
		private void SaveDialogValues()
		{
			m_vwFindPattern.Pattern = FindText;
			m_vwFindPattern.MatchOldWritingSystem = chkMatchWS.Checked;
			m_vwFindPattern.MatchDiacritics = chkMatchDiacritics.Checked;
			m_vwFindPattern.MatchWholeWord = chkMatchWholeWord.Checked;
			m_vwFindPattern.MatchCase = chkMatchCase.Checked;
			m_vwFindPattern.UseRegularExpressions = chkUseRegularExpressions.Checked;
			m_vwFindPattern.ReplaceWith = ReplaceText;
			ResultReplaceText = ReplaceText;
			SimpleStringMatcher.SetupPatternCollating(m_vwFindPattern, m_cache);
		}

		/// <summary>
		/// Attempts to find a pattern match in the view starting from the specified selection, wrapping around if we reach the end of the view.
		/// </summary>
		/// <param name="sel">Starting point for the search</param>
		/// <param name="fFirstTry">true if this is the first try finding this pattern</param>
		private void FindFromAndWrap(IVwSelection sel, bool fFirstTry)
		{
			FindFrom(sel);
			if (!m_findEnvironment.FoundMatch)
			{
				AttemptWrap(fFirstTry);
			}
		}

		/// <summary>
		/// Attempts to find a pattern match in the view starting from the specified selection.
		/// </summary>
		/// <param name="sel">Starting position</param>
		private void FindFrom(IVwSelection sel)
		{
			LocationInfo startLocation = null;
			var rootSite = ActiveView;
			if (rootSite == null)
			{
				return;
			}
			if (sel != null)
			{
				startLocation = new LocationInfo(SelectionHelper.Create(sel, rootSite));
			}
			var locationInfo = m_findEnvironment.FindNext(startLocation);
			if (locationInfo != null)
			{
				var selHelper = SelectionHelper.Create(rootSite);
				selHelper.SetLevelInfo(SelLimitType.Anchor, locationInfo.m_location);
				selHelper.SetLevelInfo(SelLimitType.End, locationInfo.m_location);
				selHelper.IchAnchor = locationInfo.m_ichMin;
				selHelper.IchEnd = locationInfo.m_ichLim;
				selHelper.SetNumberOfPreviousProps(SelLimitType.Anchor, locationInfo.m_cpropPrev);
				selHelper.SetNumberOfPreviousProps(SelLimitType.End, locationInfo.m_cpropPrev);
				selHelper.SetTextPropId(SelLimitType.Anchor, locationInfo.m_tag);
				selHelper.SetTextPropId(SelLimitType.End, locationInfo.m_tag);
				m_vwSelectionForPattern = selHelper.SetSelection(rootSite, true, true, VwScrollSelOpts.kssoDefault);
				Debug.Assert(m_vwSelectionForPattern != null, "We need a selection after a find!");
				rootSite.RootBox.Activate(VwSelectionState.vssOutOfFocus);
			}
		}

		/// <summary>
		/// Attempts to wrap and continue searching if we hit the bottom of the view.
		/// </summary>
		private void AttemptWrap(bool fFirstTry)
		{
			Debug.Assert(m_findEnvironment != null);
			m_findEnvironment.HasWrapped = true;
			// Have we gone full circle and reached the point where we started?
			if (m_findEnvironment.StoppedAtLimit)
			{
				InternalMatchNotFound(false);
			}
			else
			{
				// Wrap around to start searching at the top or bottom of the view.
				FindFrom(null);
				// If, after wrapping around to begin searching from the top, we hit the
				// starting point, then display the same message as if we went full circle.
				if (!m_findEnvironment.FoundMatch)
				{
					InternalMatchNotFound(fFirstTry);
				}
			}
		}

		/// <summary>
		/// Checks for a subscriber to the MatchNotFound event and displays the appropriate not
		/// found message if the subscriber says it's ok, or if there is no subscriber.
		/// </summary>
		private void InternalMatchNotFound(bool fFirstTry)
		{
			m_vwSelectionForPattern = null;
			var fShowMsg = true;
			var defaultMsg = fFirstTry ? FwCoreDlgs.kstidNoMatchMsg : FwCoreDlgs.kstidNoMoreMatchesMsg;
			if (MatchNotFound != null)
			{
				fShowMsg = MatchNotFound(this, defaultMsg, fFirstTry ? MatchType.NoMatchFound : MatchType.NoMoreMatchesFound);
			}
			if (fShowMsg && !m_searchKiller.AbortRequest)
			{
				// Show message that entire document was searched only if the search was not aborted (TE-3567).
				Enabled = false;
				MessageBox.Show(Owner, defaultMsg, m_app.ApplicationName);
				Enabled = true;
			}
		}

		/// <summary>
		/// Moves the selection to the beginning of the root site and returns the selection information.
		/// </summary>
		private IVwSelection SelectAtBeginning()
		{
			var rootSite = ActiveView;
			if (rootSite == null)
			{
				return null;
			}
			rootSite.RootBox.Activate(VwSelectionState.vssOutOfFocus);
			var selHelper = SelectionHelper.Create(rootSite);
			selHelper.IchAnchor = 0;
			selHelper.IchEnd = 0;
			selHelper.SetNumberOfPreviousProps(SelLimitType.Anchor, 0);
			selHelper.SetNumberOfPreviousProps(SelLimitType.End, 0);
			m_vwSelectionForPattern = selHelper.SetSelection(rootSite, true, true, VwScrollSelOpts.kssoDefault);
			var rootBox = rootSite.RootBox;
			return rootBox?.Selection;
		}

		/// <summary>
		/// Gets the current selection from the root site.
		/// </summary>
		public IVwSelection CurrentSelection => ActiveView?.RootBox?.Selection;

		/// <summary>
		/// Determine if the current selection can be replaced with the replace text.
		/// </summary>
		/// <param name="vwsel">current selection to check</param>
		/// <returns>true if the selection can be replaced, else false</returns>
		private bool IsReplacePossible(IVwSelection vwsel)
		{
			// If there is no selection then replace is impossible.
			return vwsel != null && m_vwFindPattern.MatchWhole(vwsel) && vwsel.CanFormatChar;
		}

		/// <summary>
		/// Perform a single instance of a text replace
		/// </summary>
		/// <param name="sel">The current (new) selection in the view where we just did a find.
		/// Presumably, this matches the Find Text string.</param>
		/// <param name="tssReplace">The tss string that the user entered in the Replace box
		/// </param>
		/// <param name="fUseWS"></param>
		/// <param name="fEmptySearch"></param>
		private void DoReplacement(IVwSelection sel, ITsString tssReplace, bool fUseWS, bool fEmptySearch)
		{
			// Get the properties we will apply, except for the writing system/ows and/or sStyleName.
			sel.GetFirstParaString(out var tssSel, " ", out var fGotItAll);
			if (!fGotItAll)
			{
				return; // desperate defensive programming.
			}
			// Get ORCs from selection so that they can be appended after the text has been replaced.
			var stringBldr = tssSel.GetBldr();
			ReplaceString(stringBldr, tssSel, 0, tssSel.Length, tssReplace, 0, fEmptySearch, fUseWS);
			// finally - do the replacement
			sel.ReplaceWithTsString(stringBldr.GetString().get_NormalizedForm(FwNormalizationMode.knmNFD));
		}

		/// <summary>
		/// Replaces the string.
		/// </summary>
		/// <param name="tsbBuilder">The string builder for the text to be replaced.</param>
		/// <param name="tssInput">The input string to be replaced.</param>
		/// <param name="ichMinInput">The start in the input string.</param>
		/// <param name="ichLimInput">The lim in the input string.</param>
		/// <param name="tssReplace">The replacement text. This should come from VwPattern.ReplacementText,
		/// NOT VwPattern.ReplaceWith. The former includes any ORCs that need to be saved from the input, as well as
		/// properly handling $1, $2 etc. in regular expressions.</param>
		/// <param name="delta">length difference between tssInput and tsbBuilder from previous
		/// replacements.</param>
		/// <param name="fEmptySearch"><c>true</c> if search text is empty (irun.e. we're searching
		/// for a style or Writing System)</param>
		/// <param name="fUseWs">if set to <c>true</c> use the writing system used in the
		/// replace string of the Find/Replace dialog.</param>
		/// <returns>Change in length of the string.</returns>
		private static int ReplaceString(ITsStrBldr tsbBuilder, ITsString tssInput, int ichMinInput, int ichLimInput, ITsString tssReplace, int delta, bool fEmptySearch, bool fUseWs)
		{
			var initialLength = tsbBuilder.Length;
			var replaceRunCount = tssReplace.RunCount;
			// Determine whether to replace the sStyleName. We do this if any of the runs of
			// the replacement string have the sStyleName set (to something other than
			// Default Paragraph Characters).
			var fUseStyle = false;
			// ENHANCE (EberhardB): If we're not doing a RegEx search we could store these flags
			// since they don't change.
			TsRunInfo runInfo;
			for (var irunReplace = 0; irunReplace < replaceRunCount; irunReplace++)
			{
				var textProps = tssReplace.FetchRunInfo(irunReplace, out runInfo);
				var sStyleName = textProps.GetStrPropValue((int)FwTextPropType.ktptNamedStyle);
				if (!string.IsNullOrEmpty(sStyleName))
				{
					fUseStyle = true;
				}
			}
			var iRunInput = tssInput.get_RunAt(ichMinInput);
			var selProps = tssInput.get_Properties(iRunInput);
			var propsBldr = selProps.GetBldr();
			// Make a string builder to accumulate the real replacement string.
			// Copy the runs of the replacement string, adjusting the properties.
			// Make a string builder to accumulate the real replacement string.
			var stringBldr = TsStringUtils.MakeStrBldr();
			// Copy the runs of the replacement string, adjusting the properties.
			for (var irun = 0; irun < replaceRunCount; irun++)
			{
				var ttpReplaceRun = tssReplace.FetchRunInfo(irun, out runInfo);
				if (TsStringUtils.GetGuidFromRun(tssReplace, irun) != Guid.Empty)
				{
					// If the run was a footnote or picture ORC, then just use the run
					// properties as they are.
				}
				else if (fUseWs || fUseStyle)
				{
					// Copy only writing system/old writing system, char sStyleName and/or
					// tag info into the builder.
					if (fUseWs)
					{
						int ttv;
						var ws = ttpReplaceRun.GetIntPropValues((int)FwTextPropType.ktptWs, out ttv);
						propsBldr.SetIntPropValues((int)FwTextPropType.ktptWs, ttv, ws);
					}
					if (fUseStyle)
					{
						var sStyleName = ttpReplaceRun.GetStrPropValue((int)FwTextPropType.ktptNamedStyle);
						propsBldr.SetStrPropValue((int)FwTextPropType.ktptNamedStyle, sStyleName == LcmStyleSheet.kstrDefaultCharStyle ? null : sStyleName);
					}
					ttpReplaceRun = propsBldr.GetTextProps();
				}
				else
				{
					// Its not a footnote so copy all props exactly from (the first run of the) matched text.
					ttpReplaceRun = selProps;
				}
				// Insert modified run into string builder.
				if (fEmptySearch && tssReplace.Length == 0)
				{
					// We are just replacing an ws/ows/sStyleName/tags. The text remains unchanged.
					// ENHANCE (SharonC): Rework this when we get patterns properly implemented.
					var runText = tssInput.get_RunText(iRunInput);
					if (runText.Length > ichLimInput - ichMinInput)
					{
						runText = runText.Substring(0, ichLimInput - ichMinInput);
					}
					stringBldr.Replace(0, 0, runText, ttpReplaceRun);
				}
				else
				{
					stringBldr.Replace(runInfo.ichMin, runInfo.ichMin, tssReplace.get_RunText(irun), ttpReplaceRun);
				}
			}
			tsbBuilder.ReplaceTsString(delta + ichMinInput, delta + ichLimInput, stringBldr.GetString());
			var finalLength = tsbBuilder.Length;
			return finalLength - initialLength;
		}

		#endregion

		#region Protected properties
		/// <summary>
		/// The data access for the find and replace dialog.
		/// </summary>
		private ISilDataAccess DataAccess => ActiveView?.RootBox.DataAccess;
		#endregion

		#region Protected helper methods
		/// <summary>
		/// Given an FwTextBox, we get the name of the WS of the current selection. If the
		/// selection spans multiple writing systems, we return an empty string.
		/// </summary>
		/// <param name="fwtextbox">An FwTextBox (either the Find or Replace box)</param>
		/// <returns>Empty string if there is more than one writing system contained in the
		/// selection or if the TsString doesn't have a writing system property (if that's
		/// even possible). Otherwise, the UI name of the writing system.</returns>
		private CoreWritingSystemDefinition GetCurrentWS(FwTextBox fwtextbox)
		{
			var hvoWs = SelectionHelper.GetWsOfEntireSelection(fwtextbox.Selection);
			return hvoWs == 0 ? null : m_cache.ServiceLocator.WritingSystemManager.Get(hvoWs);
		}

		/// <summary>
		/// Fill the Writing Systems menu with an alphabetized list of all writing systems
		/// defined in this language project. The writing system of the current selection
		/// (if there is exactly one) will be checked; otherwise, nothing will be checked.
		/// </summary>
		internal void PopulateWritingSystemMenu()
		{
			// First clear any items added previously
			mnuWritingSystem.MenuItems.Clear();
			EventHandler clickEvent = WritingSystemMenu_Click;
			// Convert from Set to List, since the Set can't sort.
			var writingSystems = m_cache.ServiceLocator.WritingSystems.AllWritingSystems.ToList();
			writingSystems.Sort((x, y) => x.DisplayLabel.CompareTo(y.DisplayLabel));
			var sCurrentWs = GetCurrentWS(LastTextBoxInFocus);
			foreach (var ws in writingSystems)
			{
				mnuWritingSystem.MenuItems.Add(new MenuItem(ws.DisplayLabel, clickEvent) { Checked = sCurrentWs == ws, Tag = ws.Handle });
			}
		}

		/// <summary>
		/// Fill the Style menu the "No style" item, plus a an alphabetized list of all
		/// character styles in stylesheet of the last Fw Edit Box to have focus. The style of
		/// the current selection (if there is exactly one) will be checked. If the selection
		/// contains no style, then "No style" will be checked. If the selection covers multiple
		/// styles, nothing will be checked.
		/// </summary>
		public void PopulateStyleMenu()
		{
			// TODO: Convert this method to use StyleListHelper.
			// First clear any items added previously
			mnuStyle.MenuItems.Clear();
			var clickEvent = new EventHandler(StyleMenu_Click);
			var sSelectedStyle = LastTextBoxInFocus.SelectedStyle;
			var mnuItem = new MenuItem(FwCoreDlgs.kstidNoStyle, clickEvent) { Checked = (sSelectedStyle == string.Empty) };
			mnuStyle.MenuItems.Add(mnuItem);
			mnuItem = new MenuItem(StyleUtils.DefaultParaCharsStyleName, clickEvent)
			{
				Checked = (sSelectedStyle == LcmStyleSheet.kstrDefaultCharStyle)
			};
			mnuStyle.MenuItems.Add(mnuItem);
			var count = 0;
			if (LastTextBoxInFocus.StyleSheet != null)
			{
				count = LastTextBoxInFocus.StyleSheet.CStyles;
			}
			var styleNames = new List<string>(count / 2);
			for (var i = 0; i < count; i++)
			{
				var styleName = LastTextBoxInFocus.StyleSheet.get_NthStyleName(i);
				if (LastTextBoxInFocus.StyleSheet.GetType(styleName) == 1) // character style
				{
					var context = (ContextValues)LastTextBoxInFocus.StyleSheet.GetContext(styleName);

					// Exclude Internal and InternalMappable style contexts
					if (context != ContextValues.Internal && context != ContextValues.InternalMappable)
					{
						styleNames.Add(styleName);
					}
				}
			}
			styleNames.Sort();
			foreach (var s in styleNames)
			{
				mnuItem = new MenuItem(s, clickEvent) { Checked = (sSelectedStyle == s) };
				mnuStyle.MenuItems.Add(mnuItem);
			}
		}

		/// <summary>
		/// Applies the specified style to the current selection of the Tss string in the
		/// specified Tss edit control
		/// </summary>
		/// <param name="fwTextBox">The Tss edit control whose selection should have the
		/// specified style applied to it.</param>
		/// <param name="sStyle">The name of the style to apply</param>
		public virtual void ApplyStyle(FwTextBox fwTextBox, string sStyle)
		{
			// Apply the specified style to the current selection
			if (sStyle.ToLowerInvariant() == FwCoreDlgs.kstidNoStyle.ToLowerInvariant())
			{
				sStyle = null;
			}
			else if (sStyle.ToLowerInvariant() == StyleUtils.DefaultParaCharsStyleName.ToLowerInvariant())
			{
				sStyle = LcmStyleSheet.kstrDefaultCharStyle;
			}
			fwTextBox.ApplyStyle(sStyle);
			SetFormatLabels();

			fwTextBox.Select();
		}

		/// <summary>
		/// Applies the specified writing system to the current selection of the Tss string in
		/// the specified Tss edit control
		/// </summary>
		/// <param name="fwTextBox">The Tss edit control whose selection should have the
		/// specified style applied to it.</param>
		/// <param name="hvoWs">The ID of the writing system to apply</param>
		public void ApplyWS(FwTextBox fwTextBox, int hvoWs)
		{
			fwTextBox.ApplyWS(hvoWs);
			if (chkMatchWS.Enabled)
			{
				chkMatchWS.Checked = true;
			}
			SetFormatLabels();
			fwTextBox.Select();
		}

		/// <summary>
		/// Updates visibility and values of format labels used to show selected styles in
		/// find and replace text boxes.
		/// </summary>
		private void SetFormatLabels()
		{
			if (tabControls.SelectedTab == tabFind)
			{
				lblReplaceFormat.Hide();
				lblReplaceFormatText.Hide();
				SetFormatLabels(fweditFindText, lblFindFormat, lblFindFormatText);
			}
			else
			{
				SetFormatLabels(fweditFindText, lblFindFormat, lblFindFormatText);
				SetFormatLabels(fweditReplaceText, lblReplaceFormat, lblReplaceFormatText);
			}
			btnFindNext.Enabled = btnReplace.Enabled = btnReplaceAll.Enabled = CanFindNext();
		}

		private bool CanFindNext()
		{
			return (fweditFindText.Text != string.Empty || lblFindFormatText.Text != string.Empty);
		}

		/// <summary>
		/// Updates visibility and content of labels depending on char styles in passed TsString.
		/// </summary>
		private void SetFormatLabels(FwTextBox textBox, Label format, Label formatText)
		{
			var tss = textBox.Tss;
			var currentWs = GetCurrentWS(textBox);
			// Check for writing systems and styles that are applied to the tss
			var fShowLabels = false;
			var prevWs = -1;
			var prevStyleName = string.Empty;
			var multipleWs = false;
			var multipleStyles = false;
			for (var i = 0; i < tss.RunCount; i++)
			{
				var ttp = tss.get_Properties(i);
				// check for writing systems
				var ws = ttp.GetIntPropValues((int)FwTextPropType.ktptWs, out _);
				if (prevWs != ws && prevWs != -1)
				{
					multipleWs = true;
				}
				prevWs = ws;
				// check for styles
				var charStyle = ttp.GetStrPropValue((int)FwTextPropType.ktptNamedStyle);
				if (charStyle != prevStyleName && prevStyleName != string.Empty)
				{
					multipleStyles = true;
				}
				prevStyleName = charStyle;
			}
			switch (prevStyleName)
			{
				case null:
					prevStyleName = string.Empty;
					break;
				case LcmStyleSheet.kstrDefaultCharStyle:
					prevStyleName = StyleUtils.DefaultParaCharsStyleName;
					break;
			}
			Debug.Assert(prevWs > 0, "We should always have a writing system");
			// no more than 1 style and only 1 writing system
			if (!multipleStyles && !multipleWs)
			{
				// Not displaying anything
				if (prevStyleName == string.Empty && !chkMatchWS.Checked)
				{
					formatText.Text = string.Empty;
				}
				// Just have one style
				else if (prevStyleName != string.Empty)
				{
					fShowLabels = true;
					formatText.Text = !chkMatchWS.Checked ? prevStyleName : string.Format(FwCoreDlgs.kstidOneStyleOneWS, prevStyleName, currentWs == null ? string.Empty : currentWs.DisplayLabel);
				}
				// No style (WS displayed)
				else if (chkMatchWS.Checked)
				{
					fShowLabels = true;
					formatText.Text = currentWs == null ? string.Empty : currentWs.DisplayLabel;
				}
			}
			// multiple styles or multiple writing systems (displayed)
			else if (multipleStyles || multipleWs && chkMatchWS.Checked)
			{
				fShowLabels = true;
				// multiple styles
				if (multipleStyles)
				{
					// only one writing system or multiple writing systems (not displayed)
					if (!multipleWs || !chkMatchWS.Checked)
					{
						formatText.Text = !chkMatchWS.Checked ? FwCoreDlgs.kstidMultipleStyles : string.Format(FwCoreDlgs.kstidMultipleStylesOneWS, currentWs == null ? string.Empty : currentWs.DisplayLabel);
					}
					// multiple writing systems (displayed)
					else
					{
						formatText.Text = FwCoreDlgs.kstidMultipleStylesMultipleWS;
					}
				}
				// Multiple writing systems and no more than 1 style
				else
				{
					formatText.Text = prevStyleName == string.Empty ? FwCoreDlgs.kstidMultipleWritingSystems : string.Format(FwCoreDlgs.kstidOneStyleMultipleWS, prevStyleName);
				}
			}
			// multiple writing systems (not displayed) and one style
			else if (!chkMatchWS.Checked && prevStyleName != string.Empty)
			{
				formatText.Text = prevStyleName;
				fShowLabels = true;
			}
			format.Visible = fShowLabels;
			formatText.Visible = fShowLabels;
		}
		#endregion

		#region Public properties
		/// <summary>
		/// Returns the text to find
		/// </summary>
		public ITsString FindText
		{
			get => fweditFindText.Tss;
			set
			{
				fweditFindText.Tss = TsStringUtils.GetCleanTsString(value);
				HandleTextChanged(fweditFindText, null);
			}
		}

		/// <summary>
		/// Returns the text to replace
		/// </summary>
		public ITsString ReplaceText
		{
			get => fweditReplaceText.Tss;
			set
			{
				fweditReplaceText.Tss = TsStringUtils.GetCleanTsString(value);
				HandleTextChanged(fweditReplaceText, null);
			}
		}

		/// <summary>
		/// Returns the text to replace after OK has closed the dialog and ReplaceText will crash.
		/// </summary>
		public ITsString ResultReplaceText { get; private set; }

		/// <summary>
		/// Returns a reference to the last Tss edit control to have focus. Needed for applying
		/// styles and writing systems.
		/// </summary>
		public FwTextBox LastTextBoxInFocus { get; private set; }
		#endregion

		#region IMessageFilter Members
		/// <summary>
		/// Provide tabbing with the view controls and handle the ESC key to close the find dialog
		/// </summary>
		public bool PreFilterMessage(ref Message m)
		{
			if (m.Msg == (int)Win32.WinMsgs.WM_CHAR)
			{
				// Handle TAB and Shift-TAB
				if (m.WParam == (IntPtr)Win32.VirtualKeycodes.VK_TAB)
				{
					SelectNextControl(ActiveControl, ModifierKeys != Keys.Shift, true, true, true);
					return true;
				}
			}
			return false;
		}
		#endregion

		/// <summary>
		/// Implements a search killer
		/// </summary>
		private sealed class SearchKiller : IVwSearchKiller
		{
			private bool stopButtonDown;

			#region IVwSearchKiller Members

			/// <summary>Owning control</summary>
			public Control Control { get; set; }

			/// <summary>Stop button control</summary>
			public Control StopControl { get; set; }

			/// <summary>
			/// Get/set the abort status
			/// </summary>
			public bool AbortRequest { get; set; }

			/// <summary>
			/// Process any pending window messages
			/// </summary>
			public void FlushMessages()
			{
				Control?.Update();
				if (Platform.IsMono)
				{
					return;
				}
				// Currently (Aug 2010) Mono Winforms on X11 doesn't support PeekMessage with filtering.
				// Process keystrokes and lbutton events so the user can stop the dlg work.
				// This should allow the dlg to be stopped mid stream with out the risk
				// of the DoEvents call.
				// The reason this change works is due to the 'polling' type of design of the
				// calling code.  This method is called frequently during the 'action' of the
				// dlg.
				while (PeekMessage(Win32.WinMsgs.WM_KEYDOWN, Win32.WinMsgs.WM_KEYUP, out var msg) || PeekMessage(Win32.WinMsgs.WM_LBUTTONDOWN, Win32.WinMsgs.WM_LBUTTONUP, out msg))
				{
					switch (msg.message)
					{
						case (int)Win32.WinMsgs.WM_LBUTTONDOWN:
							stopButtonDown = StopControl != null && msg.hwnd == StopControl.Handle;
							break;
						case (int)Win32.WinMsgs.WM_LBUTTONUP:
						{
							if (StopControl != null && msg.hwnd == StopControl.Handle && stopButtonDown)
							{
								((Button)StopControl).PerformClick();
								stopButtonDown = false;
							}
							break;
						}
						default:
						{
							if (msg.message == (int)Win32.WinMsgs.WM_KEYDOWN && msg.wParam == (IntPtr)Win32.VirtualKeycodes.VK_ESCAPE && StopControl != null && msg.hwnd == StopControl.Handle)
							{
								((Button)StopControl).PerformClick();
							}
							break;
						}
					}
					if (!Win32.IsDialogMessage(Control.Handle, ref msg))
					{
						Win32.TranslateMessage(ref msg);
						Win32.DispatchMessage(ref msg);
					}
				}
			}

			// Currently (Aug 2010) Mono Winforms on X11 doesn't support PeekMessage with filtering.
			/// <summary>
			/// Peeks at the pending messages and if it finds any message in the given range, that
			/// message is removed from the stack and passed back to be handled immediately.
			/// </summary>
			/// <param name="min">The minimum message to handle.</param>
			/// <param name="max">The maximum message to handle.</param>
			/// <param name="msg">The message found, if any.</param>
			/// <returns><c>true</c> if a matching message is found; <c>false</c> otherwise.</returns>
			private bool PeekMessage(Win32.WinMsgs min, Win32.WinMsgs max, out Win32.MSG msg)
			{
				msg = new Win32.MSG();
				return Win32.PeekMessage(ref msg, Control.Handle, (uint)min, (uint)max, (uint)Win32.PeekFlags.PM_REMOVE);
			}
			#endregion
		}
	}
}