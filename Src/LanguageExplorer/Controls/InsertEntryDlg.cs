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
using System.Xml.Linq;
using LanguageExplorer.Controls.DetailControls;
using LanguageExplorer.Controls.MGA;
using LanguageExplorer.Controls.XMLViews;
using Microsoft.Win32;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.RootSites;
using SIL.FieldWorks.FwCoreDlgs;
using SIL.FieldWorks.FwCoreDlgs.Controls;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainImpl;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;
using SIL.PlatformUtilities;
using SIL.Windows.Forms;
using SimpleMonitor = SIL.Collections.SimpleMonitor;

namespace LanguageExplorer.Controls
{
	/// <summary />
	internal class InsertEntryDlg : Form, IFlexComponent
	{
		#region Data members

		private LcmCache m_cache;
		private ILexEntry m_entry;
		private IMoMorphType m_morphType;
		private ILexEntryType m_complexType;
		private bool m_fComplexForm;
		private bool m_fNewlyCreated;
		private string m_oldForm = "";
		private SimpleMonitor m_updateTextMonitor;
		private ListBox.ObjectCollection m_MGAGlossListBoxItems;
		private Button m_btnOK;
		private Button m_btnCancel;
		private Label m_formLabel;
		private FwTextBox m_tbLexicalForm;  // text box used if one vernacular ws
		private FwTextBox m_tbGloss; // text box used if one analysis ws
		private LabeledMultiStringControl msLexicalForm; // multistring text box used for multiple vernacular ws
		private LabeledMultiStringControl msGloss; // multistring text box used for multiple analysis ws.
		private Button m_btnHelp;
		private Label m_morphTypeLabel;
		private FwOverrideComboBox m_cbMorphType;
		private FwOverrideComboBox m_cbComplexFormType;
		protected GroupBox m_matchingEntriesGroupBox;
		private MatchingObjectsBrowser m_matchingObjectsBrowser;
		private ToolTip m_toolTipSlotCombo;
		private MSAGroupBox m_msaGroupBox;
		private IContainer components;
		private string s_helpTopic = "khtpInsertEntry";
		private LinkLabel m_linkSimilarEntry;
		private ImageList m_imageList;
		private Label m_labelArrow;
		private readonly HelpProvider m_helpProvider;
		protected SearchingAnimation m_searchAnimation;
		/// <summary>
		/// Remember how much we adjusted the height for the lexical form and gloss
		/// text boxes.
		/// </summary>
		private int m_delta;
		private GroupBox m_propsGroupBox;
		private Label m_complexTypeLabel;
		// These are used to identify the <Not Complex> and <Unknown Complex Form>
		// entries in the combobox list.
		int m_idxNotComplex;
		private const string UnSpecifiedComplex = "Unspecified Complex Form";
		private GroupBox m_glossGroupBox;
		private LinkLabel m_lnkAssistant;
		private bool m_fLexicalFormInitialFocus = true;
		private bool m_fInitialized;
		#endregion // Data members

		#region Properties

		/// <summary>
		/// Registry key for settings for this Dialog.
		/// </summary>
		internal RegistryKey SettingsKey
		{
			get
			{
				using (var regKey = FwRegistryHelper.FieldWorksRegistryKey)
				{
					return regKey.CreateSubKey("LingCmnDlgs");
				}
			}
		}

		private string Form
		{
			get
			{
				string sForm = null;
				if (msLexicalForm == null)
				{
					sForm = m_tbLexicalForm.Text;
				}
				else
				{
					var tss = msLexicalForm.Value(m_cache.DefaultVernWs);
					if (tss != null)
					{
						sForm = tss.Text;
					}
				}
				return TrimOrGetEmptyString(sForm);
			}
			set
			{
				if (msLexicalForm == null)
				{
					m_tbLexicalForm.Text = value.Trim();
				}
				else
				{
					msLexicalForm.SetValue(m_cache.DefaultVernWs, value.Trim());
				}
			}
		}

		private ITsString TssForm
		{
			// REVIEW: trim?
			get => msLexicalForm == null ? m_tbLexicalForm.Tss : msLexicalForm.Value(m_cache.DefaultVernWs);
			set
			{
				if (msLexicalForm == null)
				{
					m_tbLexicalForm.Tss = value;
				}
				else
				{
					var wsForm = TsStringUtils.GetWsAtOffset(value, 0);
					var fVern = m_cache.ServiceLocator.WritingSystems.CurrentVernacularWritingSystems.Contains(wsForm);
					msLexicalForm.SetValue(fVern ? wsForm : m_cache.DefaultVernWs, value);
				}
			}
		}

		private string BestForm
		{
			get
			{
				if (msLexicalForm == null)
				{
					return Form;
				}
				for (var i = 0; i < msLexicalForm.NumberOfWritingSystems; ++i)
				{
					var tss = msLexicalForm.ValueAndWs(i, out _);
					if (tss != null && tss.Length > 0)
					{
						return tss.Text.Trim();
					}
				}
				return string.Empty;
			}
			set
			{
				if (msLexicalForm == null)
				{
					Form = value;
				}
				else
				{
					for (var i = 0; i < msLexicalForm.NumberOfWritingSystems; ++i)
					{
						var tss = msLexicalForm.ValueAndWs(i, out var ws);
						if (tss != null && tss.Length > 0)
						{
							msLexicalForm.SetValue(ws, value);
							return;
						}
					}
					Form = value;
				}
			}
		}

		private ITsString BestTssForm
		{
			get
			{
				if (msLexicalForm == null)
				{
					return TssForm;
				}
				for (var i = 0; i < msLexicalForm.NumberOfWritingSystems; ++i)
				{
					var tss = msLexicalForm.ValueAndWs(i, out _);
					if (tss != null && tss.Length > 0)
					{
						return tss;
					}
				}
				return null;
			}
		}

		private ITsString SelectedOrBestGlossTss
		{
			get
			{
				ITsString tssBestGloss;
				// FWNX-260: added 'msGloss.RootBox.Selection == null'
				if (msGloss == null || msGloss.RootBox.Selection == null)
				{
					tssBestGloss = m_tbGloss.Tss;
				}
				else
				{
					var wsGloss = WritingSystemServices.kwsFirstAnal;
					// if there is a selection in the MultiStringControl
					// use the anchor ws from that selection.
					var tsi = new TextSelInfo(msGloss.RootBox);
					if (tsi.Selection != null)
					{
						wsGloss = tsi.WsAltAnchor;
					}
					tssBestGloss = msGloss.Value(wsGloss);
				}
				if (tssBestGloss != null && tssBestGloss.Length > 0)
				{
					return tssBestGloss;
				}
				return null;
			}
		}

		private string Gloss
		{
			get
			{
				string glossAnalysis = null;
				if (msGloss == null)
				{
					glossAnalysis = m_tbGloss.Text;
				}
				else
				{
					var tssAnal = msGloss.Value(m_cache.DefaultAnalWs);
					if (tssAnal != null)
					{
						glossAnalysis = tssAnal.Text;
					}
				}
				return TrimOrGetEmptyString(glossAnalysis);
			}
			set
			{
				if (msGloss == null)
				{
					m_tbGloss.Text = value.Trim();
				}
				else
				{
					msGloss.SetValue(m_cache.DefaultAnalWs, value.Trim());
				}
			}
		}

		private static string TrimOrGetEmptyString(string s)
		{
			return string.IsNullOrEmpty(s) ? string.Empty : s.Trim();
		}

		internal ITsString TssGloss
		{
			// REVIEW: trim?
			get => msGloss == null ? m_tbGloss.Tss : msGloss.Value(m_cache.DefaultAnalWs);
			set
			{
				if (msGloss == null)
				{
					m_tbGloss.Tss = value;
				}
				else
				{
					var wsGloss = TsStringUtils.GetWsAtOffset(value, 0);
					msGloss.SetValue(m_cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems.Any(ws => ws.Handle == wsGloss) ? wsGloss : m_cache.DefaultAnalWs, value);
				}
			}
		}

		/// <summary>
		/// Used to initialize other WSs of the gloss line during startup.
		/// Only works for WSs that are displayed (current analysis WSs).
		/// </summary>
		internal void SetInitialGloss(int ws, ITsString tss)
		{
			if (msGloss == null)
			{
				if (ws == m_cache.ServiceLocator.WritingSystems.DefaultAnalysisWritingSystem.Handle)
				{
					m_tbGloss.Tss = tss;
				}
			}
			else
			{
				msGloss.SetValue(ws, tss);
			}
		}

		internal IPartOfSpeech POS
		{
			set => m_msaGroupBox.StemPOS = value;
		}

		internal MsaType MsaType
		{
			get => m_msaGroupBox?.MSAType ?? MsaType.kStem;
			set
			{
				if (m_msaGroupBox != null)
				{
					m_msaGroupBox.MSAType = value;
				}
			}
		}

		internal IMoInflAffixSlot Slot
		{
			get => m_msaGroupBox?.Slot;
			set
			{
				if (m_msaGroupBox != null)
				{
					m_msaGroupBox.Slot = value;
				}
			}
		}

		#endregion // Properties

		#region Construction and Initialization

		/// <summary>
		/// Constructor.
		/// </summary>
		internal InsertEntryDlg()
		{
			InitializeComponent();
			AccessibleName = GetType().Name;
			// Figure out where to locate the dlg.
			using (var regKey = SettingsKey)
			{
				var obj = regKey.GetValue("InsertX");
				if (obj != null)
				{
					var x = (int)obj;
					var y = (int)regKey.GetValue("InsertY");
					var width = (int)regKey.GetValue("InsertWidth", Width);
					var height = (int)regKey.GetValue("InsertHeight", Height);
					var rect = new Rectangle(x, y, width, height);
					ScreenHelper.EnsureVisibleRect(ref rect);
					DesktopBounds = rect;
					StartPosition = FormStartPosition.Manual;
				}
			}
			m_helpProvider = new HelpProvider();
			m_helpProvider.SetHelpNavigator(this, HelpNavigator.Topic);
			m_updateTextMonitor = new SimpleMonitor();
			m_searchAnimation = new SearchingAnimation();
			AdjustWidthForLinkLabelGroupBox();
		}

		/// <summary>
		/// Adjust the width of the group box containing the LinkLabel to allow longer
		/// translated labels to be visible (if possible).
		/// </summary>
		private void AdjustWidthForLinkLabelGroupBox()
		{
			var maxWidth = m_matchingEntriesGroupBox.Width;
			var needWidth = m_lnkAssistant.Location.X + m_lnkAssistant.Width + 2;
			if (needWidth > m_glossGroupBox.Width || m_glossGroupBox.Width > maxWidth)
			{
				m_glossGroupBox.Width = Math.Min(needWidth, maxWidth);
			}
		}

		protected override void OnSizeChanged(EventArgs e)
		{
			base.OnSizeChanged(e);
			AdjustWidthForLinkLabelGroupBox();
		}

		/// <summary>
		/// Overridden to defeat the standard .NET behavior of adjusting size by
		/// screen resolution. That is bad for this dialog because we remember the size,
		/// and if we remember the enlarged size, it just keeps growing.
		/// If we defeat it, it may look a bit small the first time at high resolution,
		/// but at least it will stay the size the user sets.
		/// </summary>
		protected override void OnLoad(EventArgs e)
		{
			var size = Size;
			base.OnLoad(e);
			if (Size != size)
			{
				Size = size;
			}
			// Setting the initial focus fixes LT-20117 (as well as FWNX-783 and LT-4719)
			SetInitialFocus();
		}

		/// <summary>
		/// Set the initial focus to either the lexical form or the gloss.
		/// </summary>
		private void SetInitialFocus()
		{
			if (m_fLexicalFormInitialFocus)
			{
				if (msLexicalForm == null)
				{
					m_tbLexicalForm.Select();
				}
				else
				{
					msLexicalForm.Select();
				}
			}
			else
			{
				if (msGloss == null)
				{
					m_tbGloss.Select();
				}
				else
				{
					msGloss.Select();
				}
			}
		}

		/// <summary />
		protected override void WndProc(ref Message m)
		{
			if (Platform.IsMono)
			{
				// FWNX-520: fix some focus issues.
				// By the time this message is processed, the popup form (PopupTree) may need to be the
				// active window, so ignore WM_ACTIVATE.
				if (m.Msg == 0x6 /*WM_ACTIVATE*/ && System.Windows.Forms.Form.ActiveForm == this)
				{
					return;
				}
			}
			base.WndProc(ref m);
		}

		/// <summary>
		/// Initialize the dialog.
		/// </summary>
		/// <remarks>All other variations of SetDlgInfo should eventually call this one.</remarks>
		protected void SetDlgInfo(LcmCache cache, IMoMorphType morphType)
		{
			SetDlgInfo(cache, morphType, 0, MorphTypeFilterType.Any);
		}

		protected void SetDlgInfo(LcmCache cache, IMoMorphType morphType, int wsVern, MorphTypeFilterType filter)
		{
			try
			{
				IVwStylesheet stylesheet = FwUtils.StyleSheetFromPropertyTable(PropertyTable);
				m_matchingObjectsBrowser.Initialize(cache, stylesheet, XDocument.Parse(LanguageExplorerControls.MatchingEntriesGuiControlParameters).Root, SearchEngine.Get(PropertyTable, "InsertEntrySearchEngine", () => new InsertEntrySearchEngine(cache)));
				m_cache = cache;
				m_fNewlyCreated = false;
				m_oldForm = string.Empty;
				// Set fonts for the two edit boxes.
				if (stylesheet != null)
				{
					m_tbLexicalForm.StyleSheet = stylesheet;
					m_tbGloss.StyleSheet = stylesheet;
				}
				// Set writing system factory and code for the two edit boxes.
				var wsContainer = cache.ServiceLocator.WritingSystems;
				var defAnalWs = wsContainer.DefaultAnalysisWritingSystem;
				var defVernWs = wsContainer.DefaultVernacularWritingSystem;
				m_tbLexicalForm.WritingSystemFactory = cache.WritingSystemFactory;
				m_tbGloss.WritingSystemFactory = cache.WritingSystemFactory;
				m_tbLexicalForm.AdjustStringHeight = false;
				m_tbGloss.AdjustStringHeight = false;
				if (wsVern <= 0)
				{
					wsVern = defVernWs.Handle;
				}
				// initialize to empty TsStrings
				//we need to use the wsVern so that tbLexicalForm is sized correctly for the font size.
				//In Interlinear text the baseline can be in any of the vernacular writing systems, not just
				//the defaultVernacularWritingSystem.
				var tssForm = TsStringUtils.EmptyString(wsVern);
				var tssGloss = TsStringUtils.EmptyString(defAnalWs.Handle);
				using (m_updateTextMonitor.Enter())
				{
					m_tbLexicalForm.WritingSystemCode = wsVern;
					m_tbGloss.WritingSystemCode = defAnalWs.Handle;

					TssForm = tssForm;
					TssGloss = tssGloss;
				}
				// start building index
				m_matchingObjectsBrowser.SearchAsync(BuildSearchFieldArray(tssForm, tssGloss));
				((ISupportInitialize)(m_tbLexicalForm)).EndInit();
				((ISupportInitialize)(m_tbGloss)).EndInit();
				if (WritingSystemServices.GetWritingSystemList(m_cache, WritingSystemServices.kwsVerns, false).Count > 1)
				{
					msLexicalForm = ReplaceTextBoxWithMultiStringBox(m_tbLexicalForm, WritingSystemServices.kwsVerns, stylesheet);
					msLexicalForm.TextChanged += tbLexicalForm_TextChanged;
				}
				else
				{
					// See if we need to adjust the height of the lexical form
					AdjustTextBoxAndDialogHeight(m_tbLexicalForm);
				}
				// JohnT addition: if multiple analysis writing systems, replace tbGloss with msGloss
				if (WritingSystemServices.GetWritingSystemList(m_cache, WritingSystemServices.kwsAnals, false).Count > 1)
				{
					msGloss = ReplaceTextBoxWithMultiStringBox(m_tbGloss, WritingSystemServices.kwsAnals, stylesheet);
					m_lnkAssistant.Top = msGloss.Bottom - m_lnkAssistant.Height;
					msGloss.TextChanged += tbGloss_TextChanged;
				}
				else
				{
					// See if we need to adjust the height of the gloss
					AdjustTextBoxAndDialogHeight(m_tbGloss);
				}
				m_msaGroupBox.Initialize(cache, new FlexComponentParameters(PropertyTable, Publisher, Subscriber), m_lnkAssistant, this);
				// See if we need to adjust the height of the MSA group box.
				var oldHeight = m_msaGroupBox.Height;
				var newHeight = Math.Max(m_msaGroupBox.PreferredHeight, oldHeight);
				GrowDialogAndAdjustControls(newHeight - oldHeight, m_msaGroupBox);
				m_msaGroupBox.AdjustInternalControlsAndGrow();
				Text = GetTitle();
				m_lnkAssistant.Enabled = false;
				// Set font for the combobox.
				m_cbMorphType.Font = new Font(defAnalWs.DefaultFontName, 12);
				// Populate morph type combo.
				// first Fill ComplexFormType combo, since cbMorphType controls
				// whether it gets enabled and which index is selected.
				m_cbComplexFormType.Font = new Font(defAnalWs.DefaultFontName, 12);
				var rgComplexTypes = new List<ICmPossibility>(m_cache.LangProject.LexDbOA.ComplexEntryTypesOA.ReallyReallyAllPossibilities.ToArray());
				rgComplexTypes.Sort();
				m_idxNotComplex = m_cbComplexFormType.Items.Count;
				m_cbComplexFormType.Items.Add(new DummyEntryType(LanguageExplorerControls.ksNotApplicable, false));
				foreach (var type in rgComplexTypes.Cast<ILexEntryType>())
				{
					m_cbComplexFormType.Items.Add(type);
				}
				m_cbComplexFormType.SelectedIndex = 0;
				m_cbComplexFormType.Visible = true;
				m_cbComplexFormType.Enabled = true;
				// Convert from Set to List, since the Set can't sort.
				var al = new List<IMoMorphType>();
				foreach (var mType in m_cache.LanguageProject.LexDbOA.MorphTypesOA.ReallyReallyAllPossibilities.Cast<IMoMorphType>())
				{
					switch (filter)
					{
						case MorphTypeFilterType.Prefix:
							if (mType.IsPrefixishType)
							{
								al.Add(mType);
							}
							break;
						case MorphTypeFilterType.Suffix:
							if (mType.IsSuffixishType)
							{
								al.Add(mType);
							}
							break;
						case MorphTypeFilterType.Any:
							al.Add(mType);
							break;
					}
				}
				al.Sort();
				for (var i = 0; i < al.Count; ++i)
				{
					m_cbMorphType.Items.Add(al[i]);
					if (al[i] == morphType)
					{
						m_cbMorphType.SelectedIndex = i;
					}
				}
				m_morphType = morphType; // Is this still needed?
				m_msaGroupBox.MorphTypePreference = m_morphType;
				// Now position the searching animation
				/*
					* This position put the animation over the Glossing Assistant button. LT-9146
				m_searchAnimation.Top = groupBox2.Top - m_searchAnimation.Height - 5;
				m_searchAnimation.Left = groupBox2.Right - m_searchAnimation.Width - 10;
					*/
				/* This position puts the animation over the top left corner, but will that
					* look okay with right-to-left?
				m_searchAnimation.Top = groupBox2.Top + 40;
				m_searchAnimation.Left = groupBox2.Left + 10;
					*/
				// This position puts the animation close to the middle of the list.
				m_searchAnimation.Top = m_matchingEntriesGroupBox.Top + (m_matchingEntriesGroupBox.Height / 2) - (m_searchAnimation.Height / 2);
				m_searchAnimation.Left = m_matchingEntriesGroupBox.Left + (m_matchingEntriesGroupBox.Width / 2) - (m_searchAnimation.Width / 2);
			}
			catch (Exception e)
			{
				MessageBox.Show(e.ToString());
				MessageBox.Show(e.StackTrace);
			}
		}

		private LabeledMultiStringControl ReplaceTextBoxWithMultiStringBox(FwTextBox tb, int wsType, IVwStylesheet stylesheet)
		{
			tb.Hide();
			var ms = new LabeledMultiStringControl(m_cache, wsType, stylesheet)
			{
				Location = tb.Location,
				Width = tb.Width,
				Anchor = tb.Anchor
			};
			var oldHeight = tb.Parent.Height;
			FontHeightAdjuster.GrowDialogAndAdjustControls(tb.Parent, ms.Height - tb.Height, ms);
			tb.Parent.Controls.Add(ms);
			// Grow the dialog and move all lower controls down to make room.
			GrowDialogAndAdjustControls(tb.Parent.Height - oldHeight, tb.Parent);
			ms.TabIndex = tb.TabIndex;  // assume the same tab order as the single ws control
			return ms;
		}

		private void AdjustTextBoxAndDialogHeight(FwTextBox tb)
		{
			var oldHeight = tb.Parent.Height;
			var tbNewHeight = Math.Max(tb.PreferredHeight, tb.Height);
			FontHeightAdjuster.GrowDialogAndAdjustControls(tb.Parent, tbNewHeight - tb.Height, tb);
			tb.Height = tbNewHeight;
			GrowDialogAndAdjustControls(tb.Parent.Height - oldHeight, tb.Parent);
		}

		// Grow the dialog's height by delta.
		// Adjust any controls that need it.
		private void GrowDialogAndAdjustControls(int delta, Control grower)
		{
			if (delta == 0)
			{
				return;
			}
			m_delta += delta;
			FontHeightAdjuster.GrowDialogAndAdjustControls(this, delta, grower);
		}

		/// <summary>
		/// Initialize an InsertEntryDlg from something like an "Insert Major Entry menu".
		/// </summary>
		internal void SetDlgInfo(LcmCache cache, ITsString tssForm)
		{
			var helpTopicProvider = PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider);
			if (helpTopicProvider != null)
			{
				m_helpProvider.HelpNamespace = helpTopicProvider.HelpFile;
				m_helpProvider.SetHelpKeyword(this, helpTopicProvider.GetHelpString(s_helpTopic));
			}
			m_btnHelp.Enabled = (helpTopicProvider != null);
			var morphComponents = MorphServices.BuildMorphComponents(cache, tssForm, MoMorphTypeTags.kguidMorphStem);
			var morphType = morphComponents.MorphType;
			var wsContainer = cache.ServiceLocator.WritingSystems;
			var wsForm = TsStringUtils.GetWsAtOffset(tssForm, 0);
			var fVern = wsContainer.CurrentVernacularWritingSystems.Contains(wsForm);
			var wsVern = fVern ? wsForm : wsContainer.DefaultVernacularWritingSystem.Handle;
			SetDlgInfo(cache, morphType, wsVern, MorphTypeFilterType.Any);
			if (fVern)
			{
				TssForm = tssForm;
				TssGloss = TsStringUtils.MakeString("", wsContainer.DefaultAnalysisWritingSystem.Handle);
				// The lexical form is already set, so shift focus to the gloss when
				// the form is activated.
				m_fLexicalFormInitialFocus = false;
			}
			else
			{
				TssForm = TsStringUtils.MakeString("", wsContainer.DefaultVernacularWritingSystem.Handle);
				TssGloss = tssForm;
				// The gloss is already set, so shift the focus to the lexical form
				// when the form is activated.
				m_fLexicalFormInitialFocus = true;
			}
			if (tssForm.Length > 0)
			{
				UpdateMatches();
			}
		}

		/// <summary>
		/// Initialize an InsertEntryDlg from something like an "Insert Major Entry menu".
		/// </summary>
		internal void SetDlgInfo(LcmCache cache, IPersistenceProvider persistProvider)
		{
			Guard.AgainstNull(persistProvider, nameof(persistProvider));

			SetDlgInfo(cache);
		}

		/// <summary>
		/// Initialize the dialog.
		/// </summary>
		internal void SetDlgInfo(LcmCache cache, IMoMorphType morphType, MsaType msaType, IMoInflAffixSlot slot, MorphTypeFilterType filter)
		{
			SetDlgInfo(cache, morphType, 0, filter);
			m_msaGroupBox.MSAType = msaType;
			Slot = slot;
		}

		/// <summary>
		/// Initialize an InsertEntryDlg from something like an "Insert Major Entry menu".
		/// </summary>
		protected void SetDlgInfo(LcmCache cache)
		{
			SetDlgInfo(cache, cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>().GetObject(MoMorphTypeTags.kguidMorphStem));
		}

		/// <summary>
		/// Disable these two controls (for use when creating an entry for a particular slot)
		/// </summary>
		internal void DisableAffixTypeMainPosAndSlot()
		{
			m_msaGroupBox.DisableAffixTypeMainPosAndSlot();
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

			if (disposing)
			{
				components?.Dispose();
			}
			m_cache = null;

			base.Dispose(disposing);
		}

		#endregion Construction and Initialization

		#region Other methods

		/// <summary>
		/// Get the results from the dlg.
		/// </summary>
		internal void GetDialogInfo(out ILexEntry entry, out bool newlyCreated)
		{
			entry = m_entry;
			newlyCreated = m_fNewlyCreated;
		}

		private static string GetTitle()
		{
			return StringTable.Table.GetStringWithXPath("CreateEntry", "/group[@id=\"DialogTitles\"]/");
		}

		protected virtual void UpdateMatches()
		{
			var tssForm = TssForm;
			var vernWs = TsStringUtils.GetWsAtOffset(tssForm, 0);
			var form = MorphServices.EnsureNoMarkers(tssForm.Text, m_cache);
			tssForm = TsStringUtils.MakeString(form, vernWs);
			var tssGloss = SelectedOrBestGlossTss;
			if (!Controls.Contains(m_searchAnimation))
			{
				Controls.Add(m_searchAnimation);
				m_searchAnimation.BringToFront();
			}
			m_matchingObjectsBrowser.SearchAsync(BuildSearchFieldArray(tssForm, tssGloss));
		}

		private SearchField[] BuildSearchFieldArray(ITsString tssForm, ITsString tssGloss)
		{
			var fields = new List<SearchField>();
			if (m_matchingObjectsBrowser.IsVisibleColumn("EntryHeadword") || m_matchingObjectsBrowser.IsVisibleColumn("CitationForm"))
			{
				fields.Add(new SearchField(LexEntryTags.kflidCitationForm, tssForm));
			}
			if (m_matchingObjectsBrowser.IsVisibleColumn("EntryHeadword") || m_matchingObjectsBrowser.IsVisibleColumn("LexemeForm"))
			{
				fields.Add(new SearchField(LexEntryTags.kflidLexemeForm, tssForm));
			}
			if (m_matchingObjectsBrowser.IsVisibleColumn("Allomorphs"))
			{
				fields.Add(new SearchField(LexEntryTags.kflidAlternateForms, tssForm));
			}
			if (tssGloss != null && m_matchingObjectsBrowser.IsVisibleColumn("Glosses"))
			{
				fields.Add(new SearchField(LexSenseTags.kflidGloss, tssGloss));
			}
			return fields.ToArray();
		}

		/// <summary>
		/// Set the class and morph type.
		/// </summary>
		private void SetMorphType(IMoMorphType mmt)
		{
			if (!m_cbMorphType.Items.Contains(mmt))
			{
				return;
			}
			m_morphType = mmt;
			m_msaGroupBox.MorphTypePreference = mmt;
			using (m_updateTextMonitor.Enter())
			{
				m_cbMorphType.SelectedItem = mmt;
			}
			EnableComplexFormTypeCombo();
		}

		private void UseExistingEntry()
		{
			DialogResult = DialogResult.Yes;
			Close();
		}

		/// <summary>
		/// Changes the text of "Use Similar Entry" link to indicate that in this context it will lead
		/// to adding an allomorph to the similar entry (unless it already has an appropriate one, of course).
		/// </summary>
		internal void ChangeUseSimilarToCreateAllomorph()
		{
			m_linkSimilarEntry.Text = LanguageExplorerControls.ksAddAllomorphToSimilarEntry;
		}

		/// <summary>
		/// Sets the help topic ID for the window.  This is used in both the Help button and when the user hits F1
		/// </summary>
		internal void SetHelpTopic(string helpTopic)
		{
			s_helpTopic = helpTopic;
			var helpTopicProvider = PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider);
			if (helpTopicProvider != null)
			{
				m_helpProvider.SetHelpKeyword(this, helpTopicProvider.GetHelpString(s_helpTopic));
				m_btnHelp.Enabled = true;
			}
		}

		#endregion Other methods

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(InsertEntryDlg));
			this.m_btnOK = new System.Windows.Forms.Button();
			this.m_btnCancel = new System.Windows.Forms.Button();
			this.m_formLabel = new System.Windows.Forms.Label();
			this.m_tbLexicalForm = new SIL.FieldWorks.FwCoreDlgs.Controls.FwTextBox();
			this.m_tbGloss = new SIL.FieldWorks.FwCoreDlgs.Controls.FwTextBox();
			this.m_btnHelp = new System.Windows.Forms.Button();
			this.m_morphTypeLabel = new System.Windows.Forms.Label();
			this.m_cbMorphType = new SIL.FieldWorks.FwCoreDlgs.FwOverrideComboBox();
			this.m_cbComplexFormType = new SIL.FieldWorks.FwCoreDlgs.FwOverrideComboBox();
			this.m_matchingEntriesGroupBox = new System.Windows.Forms.GroupBox();
			this.m_labelArrow = new System.Windows.Forms.Label();
			this.m_imageList = new System.Windows.Forms.ImageList(this.components);
			this.m_linkSimilarEntry = new System.Windows.Forms.LinkLabel();
			this.m_matchingObjectsBrowser = new LanguageExplorer.Controls.XMLViews.MatchingObjectsBrowser();
			this.m_toolTipSlotCombo = new System.Windows.Forms.ToolTip(this.components);
			this.m_msaGroupBox = new MSAGroupBox();
			this.m_propsGroupBox = new System.Windows.Forms.GroupBox();
			this.m_complexTypeLabel = new System.Windows.Forms.Label();
			this.m_glossGroupBox = new System.Windows.Forms.GroupBox();
			this.m_lnkAssistant = new System.Windows.Forms.LinkLabel();
			((System.ComponentModel.ISupportInitialize)(this.m_tbLexicalForm)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.m_tbGloss)).BeginInit();
			this.m_matchingEntriesGroupBox.SuspendLayout();
			this.m_propsGroupBox.SuspendLayout();
			this.m_glossGroupBox.SuspendLayout();
			this.SuspendLayout();
			//
			// m_btnOK
			//
			resources.ApplyResources(this.m_btnOK, "m_btnOK");
			this.m_btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
			this.m_btnOK.Name = "m_btnOK";
			//
			// m_btnCancel
			//
			resources.ApplyResources(this.m_btnCancel, "m_btnCancel");
			this.m_btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.m_btnCancel.Name = "m_btnCancel";
			//
			// m_formLabel
			//
			resources.ApplyResources(this.m_formLabel, "m_formLabel");
			this.m_formLabel.Name = "m_formLabel";
			//
			// m_tbLexicalForm
			//
			this.m_tbLexicalForm.AdjustStringHeight = true;
			this.m_tbLexicalForm.BackColor = System.Drawing.SystemColors.Window;
			this.m_tbLexicalForm.controlID = null;
			resources.ApplyResources(this.m_tbLexicalForm, "m_tbLexicalForm");
			this.m_tbLexicalForm.HasBorder = true;
			this.m_tbLexicalForm.Name = "m_tbLexicalForm";
			this.m_tbLexicalForm.SelectionLength = 0;
			this.m_tbLexicalForm.SelectionStart = 0;
			this.m_tbLexicalForm.TextChanged += new System.EventHandler(this.tbLexicalForm_TextChanged);
			//
			// m_tbGloss
			//
			this.m_tbGloss.AdjustStringHeight = true;
			this.m_tbGloss.BackColor = System.Drawing.SystemColors.Window;
			this.m_tbGloss.controlID = null;
			resources.ApplyResources(this.m_tbGloss, "m_tbGloss");
			this.m_tbGloss.HasBorder = true;
			this.m_tbGloss.Name = "m_tbGloss";
			this.m_tbGloss.SelectionLength = 0;
			this.m_tbGloss.SelectionStart = 0;
			this.m_tbGloss.TextChanged += new System.EventHandler(this.tbGloss_TextChanged);
			//
			// m_btnHelp
			//
			resources.ApplyResources(this.m_btnHelp, "m_btnHelp");
			this.m_btnHelp.Name = "m_btnHelp";
			this.m_btnHelp.Click += new System.EventHandler(this.btnHelp_Click);
			//
			// m_morphTypeLabel
			//
			resources.ApplyResources(this.m_morphTypeLabel, "m_morphTypeLabel");
			this.m_morphTypeLabel.Name = "m_morphTypeLabel";
			//
			// m_cbMorphType
			//
			this.m_cbMorphType.AllowSpaceInEditBox = false;
			this.m_cbMorphType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			resources.ApplyResources(this.m_cbMorphType, "m_cbMorphType");
			this.m_cbMorphType.Name = "m_cbMorphType";
			this.m_cbMorphType.SelectedIndexChanged += new System.EventHandler(this.cbMorphType_SelectedIndexChanged);
			//
			// m_cbComplexFormType
			//
			this.m_cbComplexFormType.AllowSpaceInEditBox = false;
			this.m_cbComplexFormType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			resources.ApplyResources(this.m_cbComplexFormType, "m_cbComplexFormType");
			this.m_cbComplexFormType.Name = "m_cbComplexFormType";
			this.m_cbComplexFormType.SelectedIndexChanged += new System.EventHandler(this.cbComplexFormType_SelectedIndexChanged);
			//
			// m_matchingEntriesGroupBox
			//
			resources.ApplyResources(this.m_matchingEntriesGroupBox, "m_matchingEntriesGroupBox");
			this.m_matchingEntriesGroupBox.Controls.Add(this.m_labelArrow);
			this.m_matchingEntriesGroupBox.Controls.Add(this.m_linkSimilarEntry);
			this.m_matchingEntriesGroupBox.Controls.Add(this.m_matchingObjectsBrowser);
			this.m_matchingEntriesGroupBox.Name = "m_matchingEntriesGroupBox";
			this.m_matchingEntriesGroupBox.TabStop = false;
			//
			// m_labelArrow
			//
			resources.ApplyResources(this.m_labelArrow, "m_labelArrow");
			this.m_labelArrow.ImageList = this.m_imageList;
			this.m_labelArrow.Name = "m_labelArrow";
			this.m_labelArrow.Click += new System.EventHandler(this.btnSimilarEntry_Click);
			//
			// m_imageList
			//
			this.m_imageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("m_imageList.ImageStream")));
			this.m_imageList.TransparentColor = System.Drawing.Color.Fuchsia;
			this.m_imageList.Images.SetKeyName(0, "GoToArrow.bmp");
			//
			// m_linkSimilarEntry
			//
			resources.ApplyResources(this.m_linkSimilarEntry, "m_linkSimilarEntry");
			this.m_linkSimilarEntry.Name = "m_linkSimilarEntry";
			this.m_linkSimilarEntry.TabStop = true;
			this.m_linkSimilarEntry.Click += new System.EventHandler(this.btnSimilarEntry_Click);
			//
			// m_matchingObjectsBrowser
			//
			resources.ApplyResources(this.m_matchingObjectsBrowser, "m_matchingObjectsBrowser");
			this.m_matchingObjectsBrowser.Name = "m_matchingObjectsBrowser";
			this.m_matchingObjectsBrowser.TabStop = false;
			this.m_matchingObjectsBrowser.SelectionChanged += new FwSelectionChangedEventHandler(this.m_matchingObjectsBrowser_SelectionChanged);
			this.m_matchingObjectsBrowser.SelectionMade += new FwSelectionChangedEventHandler(this.m_matchingObjectsBrowser_SelectionMade);
			this.m_matchingObjectsBrowser.SearchCompleted += new EventHandler(this.m_matchingObjectsBrowser_SearchCompleted);
			this.m_matchingObjectsBrowser.ColumnsChanged += new EventHandler(this.m_matchingObjectsBrowser_ColumnsChanged);
			//
			// m_toolTipSlotCombo
			//
			this.m_toolTipSlotCombo.AutoPopDelay = 5000;
			this.m_toolTipSlotCombo.InitialDelay = 250;
			this.m_toolTipSlotCombo.ReshowDelay = 100;
			this.m_toolTipSlotCombo.ShowAlways = true;
			//
			// m_msaGroupBox
			//
			resources.ApplyResources(this.m_msaGroupBox, "m_msaGroupBox");
			this.m_msaGroupBox.MSAType = MsaType.kNotSet;
			this.m_msaGroupBox.Name = "m_msaGroupBox";
			this.m_msaGroupBox.Slot = null;
			//
			// m_propsGroupBox
			//
			this.m_propsGroupBox.Controls.Add(this.m_complexTypeLabel);
			this.m_propsGroupBox.Controls.Add(this.m_morphTypeLabel);
			this.m_propsGroupBox.Controls.Add(this.m_cbMorphType);
			this.m_propsGroupBox.Controls.Add(this.m_cbComplexFormType);
			this.m_propsGroupBox.Controls.Add(this.m_tbLexicalForm);
			this.m_propsGroupBox.Controls.Add(this.m_formLabel);
			resources.ApplyResources(this.m_propsGroupBox, "m_propsGroupBox");
			this.m_propsGroupBox.Name = "m_propsGroupBox";
			this.m_propsGroupBox.TabStop = false;
			//
			// m_complexTypeLabel
			//
			resources.ApplyResources(this.m_complexTypeLabel, "m_complexTypeLabel");
			this.m_complexTypeLabel.Name = "m_complexTypeLabel";
			//
			// m_glossGroupBox
			//
			this.m_glossGroupBox.Controls.Add(this.m_lnkAssistant);
			this.m_glossGroupBox.Controls.Add(this.m_tbGloss);
			resources.ApplyResources(this.m_glossGroupBox, "m_glossGroupBox");
			this.m_glossGroupBox.Name = "m_glossGroupBox";
			this.m_glossGroupBox.TabStop = false;
			//
			// m_lnkAssistant
			//
			resources.ApplyResources(this.m_lnkAssistant, "m_lnkAssistant");
			this.m_lnkAssistant.Name = "m_lnkAssistant";
			this.m_lnkAssistant.TabStop = true;
			this.m_lnkAssistant.VisitedLinkColor = System.Drawing.Color.Blue;
			this.m_lnkAssistant.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkAssistant_LinkClicked);
			//
			// InsertEntryDlg
			//
			this.AcceptButton = this.m_btnOK;
			resources.ApplyResources(this, "$this");
			this.CancelButton = this.m_btnCancel;
			this.Controls.Add(this.m_glossGroupBox);
			this.Controls.Add(this.m_propsGroupBox);
			this.Controls.Add(this.m_msaGroupBox);
			this.Controls.Add(this.m_matchingEntriesGroupBox);
			this.Controls.Add(this.m_btnHelp);
			this.Controls.Add(this.m_btnCancel);
			this.Controls.Add(this.m_btnOK);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "InsertEntryDlg";
			this.ShowInTaskbar = false;
			this.Load += new System.EventHandler(this.InsertEntryDlg_Load);
			this.Closing += new System.ComponentModel.CancelEventHandler(this.InsertEntryDlg_Closing);
			((System.ComponentModel.ISupportInitialize)(this.m_tbLexicalForm)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.m_tbGloss)).EndInit();
			this.m_matchingEntriesGroupBox.ResumeLayout(false);
			this.m_matchingEntriesGroupBox.PerformLayout();
			this.m_propsGroupBox.ResumeLayout(false);
			this.m_glossGroupBox.ResumeLayout(false);
			this.m_glossGroupBox.PerformLayout();
			this.ResumeLayout(false);

		}
		#endregion

		#region Event Handlers

		private void InsertEntryDlg_Closing(object sender, CancelEventArgs e)
		{
			switch (DialogResult)
			{
				default:
					{
						Debug.Assert(false, "Unexpected DialogResult.");
						break;
					}
				case DialogResult.Yes:
					{
						// Exiting via existing entry selection.
						DialogResult = DialogResult.OK;
						m_fNewlyCreated = false;
						m_entry = (ILexEntry)m_matchingObjectsBrowser.SelectedObject;
						break;
					}
				case DialogResult.Cancel:
					{
						break;
					}
				case DialogResult.OK:
					{
						// In the beginning, Larry specified the gloss to not be required.
						// Then, Andy changed it to be required.
						// As of LT-518, it is again not required.
						// I'll leave it in, but blocked, in case it changes again. :-)
						//&& tbGloss.Text.Length > 0
						// As of LT-832, categories are all optional.
						if (!LexFormNotEmpty())
						{
							e.Cancel = true;
							MessageBox.Show(this, LanguageExplorerControls.ksFillInLexForm, LanguageExplorerControls.ksMissingInformation, MessageBoxButtons.OK, MessageBoxIcon.Information);
							return;
						}
						if (!CheckMorphType())
						{
							e.Cancel = true;
							MessageBox.Show(this, LanguageExplorerControls.ksInvalidLexForm, LanguageExplorerControls.ksMissingInformation, MessageBoxButtons.OK, MessageBoxIcon.Information);
							return;
						}
						if (CircumfixProblem())
						{
							e.Cancel = true;
							MessageBox.Show(this, LanguageExplorerControls.ksCompleteCircumfix, LanguageExplorerControls.ksMissingInformation, MessageBoxButtons.OK, MessageBoxIcon.Information);
							return;
						}
						CreateNewEntry();
						break;
					}
			}
			// Save location.
			using (var regKey = SettingsKey)
			{
				regKey.SetValue("InsertX", Location.X);
				regKey.SetValue("InsertY", Location.Y);
				regKey.SetValue("InsertWidth", Width);
				// We want to save the default height, without the growing we did
				// to make room for multiple gloss writing systems or a large font
				// in the lexical form box.
				regKey.SetValue("InsertHeight", Height - m_delta);
			}
		}

		private bool CheckMorphType()
		{
			var form = BestForm;
			var originalForm = form;
			var mmt = MorphServices.FindMorphType(m_cache, ref form, out _);
			bool result;
			switch (m_morphType.Guid.ToString())
			{
				// these cases are not handled by FindMorphType
				case MoMorphTypeTags.kMorphCircumfix:
				case MoMorphTypeTags.kMorphPhrase:
				case MoMorphTypeTags.kMorphDiscontiguousPhrase:
				case MoMorphTypeTags.kMorphStem:
				case MoMorphTypeTags.kMorphRoot:
				case MoMorphTypeTags.kMorphParticle:
				case MoMorphTypeTags.kMorphClitic:
					result = mmt.Guid == MoMorphTypeTags.kguidMorphStem || mmt.Guid == MoMorphTypeTags.kguidMorphPhrase;
					break;
				case MoMorphTypeTags.kMorphBoundRoot:
					result = mmt.Guid == MoMorphTypeTags.kguidMorphBoundStem;
					break;
				case MoMorphTypeTags.kMorphSuffixingInterfix:
					result = mmt.Guid == MoMorphTypeTags.kguidMorphSuffix;
					break;
				case MoMorphTypeTags.kMorphPrefixingInterfix:
					result = mmt.Guid == MoMorphTypeTags.kguidMorphPrefix;
					break;
				case MoMorphTypeTags.kMorphInfixingInterfix:
					result = mmt.Guid == MoMorphTypeTags.kguidMorphInfix;
					break;
				default:
					result = mmt.Equals(m_morphType);
					break;
			}
			if (result)
			{
				return true; // all is well.
			}
			// Pathologically the user may have changed the markers so that we cannot distinguish things that
			// are normally distinct (e.g., LT-12378).
			return mmt.Prefix + form + mmt.Postfix == originalForm;
		}

		/// <summary>
		/// Answer true if we are trying to create a circumfix and the data is not in a state that allows that.
		/// </summary>
		private bool CircumfixProblem()
		{
			if (m_morphType.Guid != MoMorphTypeTags.kguidMorphCircumfix)
			{
				return false; // not a circumfix at all.
			}
			if (msLexicalForm == null)
			{
				var tss = TssForm;
				if (!StringServices.GetCircumfixLeftAndRightParts(m_cache, tss, out _, out _))
				{
					return true;
				}
			}
			else // multiple WSS to check.
			{
				// Check all other writing systems.
				for (var i = 0; i < msLexicalForm.NumberOfWritingSystems; i++)
				{
					var tss = msLexicalForm.ValueAndWs(i, out _);
					if (tss?.Text != null)
					{
						if (!StringServices.GetCircumfixLeftAndRightParts(m_cache, tss, out _, out _))
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		private bool LexFormNotEmpty()
		{
			return BestForm.Length > 0;
		}

		/// <summary>
		/// Create a new entry based upon the state of the Dialog.
		/// </summary>
		public void CreateNewEntry()
		{
			var okToClose = LexFormNotEmpty();
			if (!okToClose)
			{
				throw new ArgumentException("lexical form field should not be empty.");
			}
			using (new WaitCursor(this))
			{
				ILexEntry newEntry = null;
				UndoableUnitOfWorkHelper.Do(LanguageExplorerControls.ksUndoCreateEntry, LanguageExplorerControls.ksRedoCreateEntry, m_cache.ServiceLocator.GetInstance<IActionHandler>(),
					() => { newEntry = CreateNewEntryInternal(); });
				m_entry = newEntry;
				m_fNewlyCreated = true;
			}
		}

		private ILexEntry CreateNewEntryInternal()
		{
			var entryComponents = BuildEntryComponentsDTO();
			var newEntry = m_cache.ServiceLocator.GetInstance<ILexEntryFactory>().Create(entryComponents);
			if (m_fComplexForm)
			{
				var ler = m_cache.ServiceLocator.GetInstance<ILexEntryRefFactory>().Create();
				newEntry.EntryRefsOS.Add(ler);
				if (m_complexType != null)
				{
					ler.ComplexEntryTypesRS.Add(m_complexType);
				}
				ler.RefType = LexEntryRefTags.krtComplexForm;
			}
			return newEntry;
		}

		private LexEntryComponents BuildEntryComponentsDTO()
		{
			var entryComponents = new LexEntryComponents();
			entryComponents.MorphType = m_morphType;
			CollectValuesFromMultiStringControl(msLexicalForm, entryComponents.LexemeFormAlternatives, BestTssForm);
			CollectValuesFromMultiStringControl(msGloss, entryComponents.GlossAlternatives, TsStringUtils.MakeString(Gloss, m_cache.DefaultAnalWs));
			entryComponents.MSA = m_msaGroupBox.SandboxMSA;
			if (m_MGAGlossListBoxItems != null)
			{
				foreach (GlossListBoxItem xn in m_MGAGlossListBoxItems)
				{
					entryComponents.GlossFeatures.Add(xn.XmlNode);
				}
			}
			return entryComponents;
		}

		private static void CollectValuesFromMultiStringControl(LabeledMultiStringControl lmsControl, IList<ITsString> alternativesCollector, ITsString defaultIfNoMultiString)
		{
			if (lmsControl == null)
			{
				alternativesCollector.Add(defaultIfNoMultiString);
			}
			else
			{
				// Save all the writing systems.
				for (var i = 0; i < lmsControl.NumberOfWritingSystems; i++)
				{
					var tss = lmsControl.ValueAndWs(i, out var ws);
					if (tss?.Text != null)
					{
						// In the case of copied text, sometimes the string had the wrong ws attached to it. (LT-11950)
						alternativesCollector.Add(TsStringUtils.MakeString(tss.Text, ws));
					}
				}
			}
		}

		/// <summary>
		/// This is triggered also if msGloss has been created, and its text has changed.
		/// </summary>
		private void tbGloss_TextChanged(object sender, EventArgs e)
		{
			if (m_updateTextMonitor.Busy)
			{
				return;
			}
			UpdateMatches();
		}

		private void tbLexicalForm_TextChanged(object sender, EventArgs e)
		{
			if (m_updateTextMonitor.Busy)
			{
				return;
			}
			//TODO?
			Debug.Assert(BestForm != null);
			if (BestForm == string.Empty)
			{
				// Set it back to stem, since there are no characters.
				SetMorphType(m_cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>().GetObject(MoMorphTypeTags.kguidMorphStem));
				m_oldForm = BestForm;
				UpdateMatches();
				return;
			}
			var newForm = BestForm;
			var mmt = MorphServices.GetTypeIfMatchesPrefix(m_cache, newForm, out var sAdjusted);
			if (mmt != null)
			{
				if (newForm != sAdjusted)
				{
					using (m_updateTextMonitor.Enter())
					{
						BestForm = sAdjusted;
						if (msLexicalForm == null)
						{
							m_tbLexicalForm.SelectionLength = 0;
							m_tbLexicalForm.SelectionStart = newForm.Length;
						}
						// TODO: how do we handle multiple writing systems?
					}
				}
			}
			else if (newForm.Length == 1)
			{
				mmt = m_cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>().GetObject(MoMorphTypeTags.kguidMorphStem);
			}
			else // Longer than one character.
			{
				try
				{
					mmt = MorphServices.FindMorphType(m_cache, ref newForm, out _);
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.Message, LanguageExplorerControls.ksInvalidForm, MessageBoxButtons.OK);
					using (m_updateTextMonitor.Enter())
					{
						BestForm = m_oldForm;
						UpdateMatches();
					}
					return;
				}
			}
			if (mmt != null && mmt != m_morphType)
			{
				SetMorphType(mmt);
			}
			m_oldForm = BestForm;
			UpdateMatches();
		}

		private void cbMorphType_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (m_updateTextMonitor.Busy)
			{
				return;
			}
			m_morphType = (IMoMorphType)m_cbMorphType.SelectedItem;
			m_msaGroupBox.MorphTypePreference = m_morphType;
			if (m_morphType.Guid != MoMorphTypeTags.kguidMorphCircumfix)
			{
				// since circumfixes can be a combination of prefix, infix, and suffix, just leave it as is
				using (m_updateTextMonitor.Enter())
				{
					BestForm = m_morphType.FormWithMarkers(BestForm);
				}
			}
			EnableComplexFormTypeCombo();
		}

		private void EnableComplexFormTypeCombo()
		{
			switch (m_morphType.Guid.ToString())
			{
				case MoMorphTypeTags.kMorphBoundRoot:
				case MoMorphTypeTags.kMorphRoot:
					m_cbComplexFormType.SelectedIndex = 0;
					m_cbComplexFormType.Enabled = false;
					break;
				case MoMorphTypeTags.kMorphDiscontiguousPhrase:
				case MoMorphTypeTags.kMorphPhrase:
					m_cbComplexFormType.Enabled = true;
					// default to "Unspecified Complex Form" if found, else set to "0" for "phrase"
					if (m_cbComplexFormType.SelectedIndex == m_idxNotComplex)
					{
						var unSpecCompFormIndex = m_cbComplexFormType.FindStringExact(UnSpecifiedComplex);
						m_cbComplexFormType.SelectedIndex = unSpecCompFormIndex != -1 ? unSpecCompFormIndex : 0;
					}
					break;
				default:
					m_cbComplexFormType.SelectedIndex = 0;
					m_cbComplexFormType.Enabled = true;
					break;
			}
		}

		private void cbComplexFormType_SelectedIndexChanged(object sender, EventArgs e)
		{
			m_complexType = m_cbComplexFormType.SelectedItem as ILexEntryType;
			m_fComplexForm = m_complexType != null;
			if (!m_fComplexForm)
			{
				var dum = (DummyEntryType)m_cbComplexFormType.SelectedItem;
				m_fComplexForm = dum.IsComplexForm;
			}
		}

		private void InsertEntryDlg_Load(object sender, EventArgs e)
		{
			var tss = BestTssForm;
			if (tss != null && tss.Length > 0)
			{
				var text = tss.Text;
				var ws = TsStringUtils.GetWsAtOffset(tss, 0);
				if (text == "-")
				{
					// is either prefix or suffix
					var wsEng = m_cache.WritingSystemFactory.GetWsFromStr("en");
					if ("prefix" == m_morphType.Name.get_String(wsEng).Text)
					{
						// is prefix so set cursor to beginning (before the hyphen)
						if (msLexicalForm == null)
						{
							m_tbLexicalForm.Select(0, 0);
						}
						else
						{
							msLexicalForm.Select(ws, 0, 0);
						}
					}
					else
					{
						// is not prefix, so set cursor to end (after the hyphen)
						if (msLexicalForm == null)
						{
							m_tbLexicalForm.Select(1, 0);
						}
						else
						{
							msLexicalForm.Select(ws, 1, 0);
						}
					}
				}
				else
				{
					if (msLexicalForm == null)
					{
						m_tbLexicalForm.Select(text.Length, 0);
					}
					else
					{
						msLexicalForm.Select(ws, text.Length, 0);
					}
				}
			}
			else
			{
				if (msLexicalForm == null)
				{
					m_tbLexicalForm.Select();
				}
				else
				{
					msLexicalForm.Select();
				}
			}
		}

		private void m_matchingObjectsBrowser_SelectionChanged(object sender, FwObjectSelectionEventArgs e)
		{
			CheckIfGoto();
		}

		private void m_matchingObjectsBrowser_SelectionMade(object sender, FwObjectSelectionEventArgs e)
		{
			DialogResult = DialogResult.Yes;
			Close();
		}

		private void m_matchingObjectsBrowser_SearchCompleted(object sender, EventArgs e)
		{
			CheckIfGoto();
			if (Controls.Contains(m_searchAnimation))
			{
				Controls.Remove(m_searchAnimation);
			}
		}

		private void m_matchingObjectsBrowser_ColumnsChanged(object sender, EventArgs e)
		{
			UpdateMatches();
		}

		private void CheckIfGoto()
		{
			var fEnable = m_matchingObjectsBrowser.SelectedObject != null;
			m_linkSimilarEntry.TabStop = fEnable;
			m_labelArrow.Enabled = m_linkSimilarEntry.Enabled = fEnable;
		}

		private void btnSimilarEntry_Click(object sender, EventArgs e)
		{
			UseExistingEntry();
		}

		private void lnkAssistant_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			switch (MsaType)
			{
				case MsaType.kInfl:
					// Get a wait cursor by setting the LinkLabel to use a wait cursor. See FWNX-700.
					// Need to use a wait cursor while creating dialog, but not when showing it.
					using (new WaitCursor(m_lnkAssistant))
					using (var dlg = new MGAHtmlHelpDialog(m_cache, PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider), m_tbLexicalForm.Text))
					{
						if (dlg.ShowDialog() == DialogResult.OK)
						{
							Gloss = dlg.Result;
							m_MGAGlossListBoxItems = dlg.Items;
						}
					}
					break;
				case MsaType.kDeriv:
					MessageBox.Show(LanguageExplorerControls.ksNoAssistForDerivAffixes, LanguageExplorerControls.ksNotice, MessageBoxButtons.OK, MessageBoxIcon.Information);
					break;
			}
		}

		private void btnHelp_Click(object sender, EventArgs e)
		{
			ShowHelp.ShowHelpTopic(PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider), "FLExHelpFile", s_helpTopic);
		}

		#endregion Event Handlers

		#region Implementation of IPropertyTableProvider
		/// <summary>
		/// Placement in the IPropertyTableProvider interface lets FwApp call IPropertyTable.DoStuff.
		/// </summary>
		public IPropertyTable PropertyTable { get; private set; }
		#endregion

		#region Implementation of IPublisherProvider
		/// <summary>
		/// Get the IPublisher.
		/// </summary>
		public IPublisher Publisher { get; private set; }
		#endregion

		#region Implementation of ISubscriberProvider
		/// <summary>
		/// Get the ISubscriber.
		/// </summary>
		public ISubscriber Subscriber { get; private set; }
		#endregion

		#region Implementation of IFlexComponent

		/// <summary>
		/// Initialize a FLEx component with the basic interfaces.
		/// </summary>
		/// <param name="flexComponentParameters">Parameter object that contains the required three interfaces.</param>
		public void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
		{
			FlexComponentParameters.CheckInitializationValues(flexComponentParameters, new FlexComponentParameters(PropertyTable, Publisher, Subscriber));

			PropertyTable = flexComponentParameters.PropertyTable;
			Publisher = flexComponentParameters.Publisher;
			Subscriber = flexComponentParameters.Subscriber;

			m_matchingObjectsBrowser.InitializeFlexComponent(flexComponentParameters);
		}
		#endregion

		/// <summary>
		/// This class allows a dummy LexEntryType replacement for "&lt;Unknown&gt;".
		/// </summary>
		private sealed class DummyEntryType
		{
			private readonly string m_sName;

			internal DummyEntryType(string sName, bool fIsComplexForm)
			{
				m_sName = sName;
				IsComplexForm = fIsComplexForm;
			}

			internal bool IsComplexForm { get; }

			public override string ToString()
			{
				return m_sName;
			}
		}

		/// <summary>
		/// This is the search engine for InsertEntryDlg.
		/// </summary>
		private sealed class InsertEntrySearchEngine : SearchEngine
		{
			private readonly Virtuals m_virtuals;

			internal InsertEntrySearchEngine(LcmCache cache)
				: base(cache, SearchType.Prefix)
			{
				m_virtuals = Cache.ServiceLocator.GetInstance<Virtuals>();
			}

			protected override IEnumerable<ITsString> GetStrings(SearchField field, ICmObject obj)
			{
				var entry = (ILexEntry)obj;
				var ws = field.String.get_WritingSystemAt(0);
				switch (field.Flid)
				{
					case LexEntryTags.kflidCitationForm:
						var cf = entry.CitationForm.StringOrNull(ws);
						if (cf != null && cf.Length > 0)
						{
							yield return cf;
						}
						break;
					case LexEntryTags.kflidLexemeForm:
						var lexemeForm = entry.LexemeFormOA;
						var formOfLexemeForm = lexemeForm?.Form.StringOrNull(ws);
						if (formOfLexemeForm != null && formOfLexemeForm.Length > 0)
						{
							yield return formOfLexemeForm;
						}
						break;
					case LexEntryTags.kflidAlternateForms:
						foreach (var form in entry.AlternateFormsOS)
						{
							var af = form.Form.StringOrNull(ws);
							if (af != null && af.Length > 0)
							{
								yield return af;
							}
						}
						break;
					case LexSenseTags.kflidGloss:
						foreach (var sense in entry.SensesOS)
						{
							var gloss = sense.Gloss.StringOrNull(ws);
							if (gloss != null && gloss.Length > 0)
							{
								yield return gloss;
							}
						}
						break;
					default:
						throw new ArgumentException("Unrecognized field.", "field");
				}
			}

			protected override IList<ICmObject> GetSearchableObjects()
			{
				return Cache.ServiceLocator.GetInstance<ILexEntryRepository>().AllInstances().Cast<ICmObject>().ToArray();
			}

			protected override bool IsIndexResetRequired(int hvo, int flid)
			{
				if (flid == m_virtuals.LexDbEntries)
				{
					return true;
				}
				switch (flid)
				{
					case LexEntryTags.kflidCitationForm:
					case LexEntryTags.kflidLexemeForm:
					case LexEntryTags.kflidAlternateForms:
					case LexEntryTags.kflidSenses:
					case MoFormTags.kflidForm:
					case LexSenseTags.kflidSenses:
					case LexSenseTags.kflidGloss:
						return true;
				}
				return false;
			}

			protected override bool IsFieldMultiString(SearchField field)
			{
				switch (field.Flid)
				{
					case LexEntryTags.kflidCitationForm:
					case LexEntryTags.kflidLexemeForm:
					case LexEntryTags.kflidAlternateForms:
					case LexSenseTags.kflidGloss:
						return true;
				}
				throw new ArgumentException("Unrecognized field.", "field");
			}

			/// <inheritdoc />
			protected override void Dispose(bool disposing)
			{
				Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + " ******");
				base.Dispose(disposing);
			}
		}
	}
}
