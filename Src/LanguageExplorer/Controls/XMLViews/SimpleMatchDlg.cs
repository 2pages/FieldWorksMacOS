// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using LanguageExplorer.Filters;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.FieldWorks.FwCoreDlgs.Controls;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;

namespace LanguageExplorer.Controls.XMLViews
{
	/// <summary>
	/// SimpleMatchDlg allows the user to type a target string and indicate whether it
	/// should match the whole, start, end, or anywhere in the target. It knows how to
	/// make a matcher consistent with what the user entered.
	/// </summary>
	public class SimpleMatchDlg : Form
	{
		private RadioButton m_anywhereButton;
		private RadioButton m_atStartButton;
		private RadioButton m_atEndButton;
		private RadioButton m_wholeItemButton;
		private FwTextBox m_textBox;
		private Button m_cancelButton;
		private Button m_okButton;
		private RadioButton m_regExButton;
		private Label label1;
		private Button buttonHelp;
		private Container components = null;
		private const string s_helpTopic = "khtpFilterFor";
		private Button regexHelper;
		private HelpProvider helpProvider;
		private CheckBox m_MatchCasecheckBox;
		private RegexHelperContextMenu _regexContextContextMenu;
		private CheckBox m_MatchDiacriticscheckBox;
		private IHelpTopicProvider m_helpTopicProvider;
		private IVwPattern m_ivwpattern;
		private LcmCache m_cache;

		/// <summary />
		public SimpleMatchDlg(ILgWritingSystemFactory wsf, IHelpTopicProvider helpTopicProvider, int ws, IVwStylesheet ss, LcmCache cache)
		{
			InitializeComponent();
			m_cache = cache;
			// We do this outside the designer-controlled code because it does funny things
			// to FwTextBoxes, owing to the need for a writing system factory, and some
			// properties it should not persist but I can't persuade it not to.
			m_textBox = new FwTextBox
			{
				WritingSystemFactory = wsf,
				WritingSystemCode = ws,
				StyleSheet = ss,
				Location = new System.Drawing.Point(8, 24),
				Name = "m_textBox",
				Size = new System.Drawing.Size(450, 32),
				TabIndex = 0,
				Text = string.Empty
			};
			// set ASAP.
			// before setting text, otherwise it gets confused about height needed.
			Controls.Add(m_textBox);
			AccessibleName = "SimpleMatchDlg";
			m_helpTopicProvider = helpTopicProvider;
			_regexContextContextMenu = new RegexHelperContextMenu(m_textBox, m_helpTopicProvider);
			m_ivwpattern = VwPatternClass.Create();
			helpProvider = new HelpProvider { HelpNamespace = m_helpTopicProvider.HelpFile };
			helpProvider.SetHelpKeyword(this, m_helpTopicProvider.GetHelpString(s_helpTopic));
			helpProvider.SetHelpNavigator(this, HelpNavigator.Topic);
			foreach (Control control in Controls)
			{
				control.Click += control_Click;
			}
		}

		private void control_Click(object sender, EventArgs e)
		{
			m_textBox.Select();
		}

		/// <summary>
		/// Initialize the dialog, based on the old matcher, if any, and if recognized.
		/// </summary>
		internal void SetDlgValues(IMatcher matcher, IVwStylesheet stylesheet)
		{
			switch (matcher)
			{
				// Figure out which kind to check
				case AnywhereMatcher _:
					m_anywhereButton.Checked = true;
					break;
				case EndMatcher _:
					m_atEndButton.Checked = true;
					break;
				case BeginMatcher _:
					m_atStartButton.Checked = true;
					break;
				case RegExpMatcher _:
					m_regExButton.Checked = true;
					break;
				case ExactMatcher _:
					m_wholeItemButton.Checked = true;
					break;
			}
			// Now get the attributes
			if (matcher is SimpleStringMatcher simpleStringMatcher)
			{
				m_textBox.Tss = simpleStringMatcher.Pattern.Pattern;
				m_MatchCasecheckBox.Checked = simpleStringMatcher.Pattern.MatchCase;
				m_MatchDiacriticscheckBox.Checked = simpleStringMatcher.Pattern.MatchDiacritics;
			}
			m_textBox.AdjustForStyleSheet(this, null, stylesheet);
		}

		/// <summary>
		/// Gets the resulting matcher.
		/// </summary>
		internal IMatcher ResultingMatcher
		{
			get
			{
				m_ivwpattern.Pattern = m_textBox.Tss;
				m_ivwpattern.MatchCase = m_MatchCasecheckBox.Checked;
				m_ivwpattern.MatchDiacritics = m_MatchDiacriticscheckBox.Checked;
				// Default values because we don't set these here
				m_ivwpattern.MatchOldWritingSystem = false;
				m_ivwpattern.MatchWholeWord = false;
				m_ivwpattern.UseRegularExpressions = false;
				m_ivwpattern.IcuLocale = m_textBox.WritingSystemFactory.GetStrFromWs(m_textBox.WritingSystemCode);
				SimpleStringMatcher.SetupPatternCollating(m_ivwpattern, m_cache);
				if (m_anywhereButton.Checked)
				{
					return new AnywhereMatcher(m_ivwpattern);
				}
				if (m_atEndButton.Checked)
				{
					return new EndMatcher(m_ivwpattern);
				}
				if (m_atStartButton.Checked)
				{
					return new BeginMatcher(m_ivwpattern);
				}
				if (m_regExButton.Checked)
				{
					return new RegExpMatcher(m_ivwpattern);
				}
				return new ExactMatcher(m_ivwpattern);
			}
		}

		/// <summary>
		/// Make sure the search text is valid for the type of IMatcher that is created.
		/// Use the new IsValid method on the IMatcher interface to make sure it's valid
		/// and allow it to continue, otherwise show the errormessage associated with the
		/// error and don't allow it to be selected.
		/// </summary>
		private void m_okButton_Click(object sender, EventArgs e)
		{
			var testMatcher = ResultingMatcher;
			if (testMatcher.IsValid())
			{
				DialogResult = DialogResult.OK;
				return;
			}
			var errMsg = string.Format(XMLViewsStrings.ksFilterErrorMsg, testMatcher.ErrorMessage());
			MessageBox.Show(this, errMsg, XMLViewsStrings.ksFilterError, MessageBoxButtons.OK, MessageBoxIcon.Error);
			// If the matcher can make the search text valid - let it
			if (testMatcher.CanMakeValid())
			{
				m_textBox.Tss = testMatcher.MakeValid();
			}
		}

		/// <summary>
		/// The text the user typed...should eventually be TsString.
		/// </summary>
		public string Pattern
		{
			get
			{
				if (m_textBox.Text.Length == 0) // case where nothing was entered
				{
					return string.Empty;
				}
				var pattern = m_textBox.Text;
				if (m_anywhereButton.Checked)
				{
					return pattern;
				}
				if (m_atEndButton.Checked)
				{
					return pattern + "#";
				}
				if (m_atStartButton.Checked)
				{
					return "#" + pattern;
				}
				if (m_regExButton.Checked)
				{
					return "/" + pattern + "/";
				}
				return "#" + pattern + "#";
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

			if (disposing)
			{
				components?.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SimpleMatchDlg));
			this.m_anywhereButton = new System.Windows.Forms.RadioButton();
			this.m_atStartButton = new System.Windows.Forms.RadioButton();
			this.m_atEndButton = new System.Windows.Forms.RadioButton();
			this.m_wholeItemButton = new System.Windows.Forms.RadioButton();
			this.m_okButton = new System.Windows.Forms.Button();
			this.m_cancelButton = new System.Windows.Forms.Button();
			this.m_regExButton = new System.Windows.Forms.RadioButton();
			this.label1 = new System.Windows.Forms.Label();
			this.buttonHelp = new System.Windows.Forms.Button();
			this.regexHelper = new System.Windows.Forms.Button();
			this.m_MatchCasecheckBox = new System.Windows.Forms.CheckBox();
			this.m_MatchDiacriticscheckBox = new System.Windows.Forms.CheckBox();
			this.SuspendLayout();
			//
			// m_anywhereButton
			//
			this.m_anywhereButton.Checked = true;
			resources.ApplyResources(this.m_anywhereButton, "m_anywhereButton");
			this.m_anywhereButton.Name = "m_anywhereButton";
			this.m_anywhereButton.TabStop = true;
			//
			// m_atStartButton
			//
			resources.ApplyResources(this.m_atStartButton, "m_atStartButton");
			this.m_atStartButton.Name = "m_atStartButton";
			//
			// m_atEndButton
			//
			resources.ApplyResources(this.m_atEndButton, "m_atEndButton");
			this.m_atEndButton.Name = "m_atEndButton";
			//
			// m_wholeItemButton
			//
			resources.ApplyResources(this.m_wholeItemButton, "m_wholeItemButton");
			this.m_wholeItemButton.Name = "m_wholeItemButton";
			//
			// m_okButton
			//
			resources.ApplyResources(this.m_okButton, "m_okButton");
			this.m_okButton.Name = "m_okButton";
			this.m_okButton.Click += new System.EventHandler(this.m_okButton_Click);
			//
			// m_cancelButton
			//
			this.m_cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			resources.ApplyResources(this.m_cancelButton, "m_cancelButton");
			this.m_cancelButton.Name = "m_cancelButton";
			//
			// m_regExButton
			//
			resources.ApplyResources(this.m_regExButton, "m_regExButton");
			this.m_regExButton.Name = "m_regExButton";
			this.m_regExButton.CheckedChanged += new System.EventHandler(this.m_regExButton_CheckedChanged);
			//
			// label1
			//
			resources.ApplyResources(this.label1, "label1");
			this.label1.Name = "label1";
			//
			// buttonHelp
			//
			resources.ApplyResources(this.buttonHelp, "buttonHelp");
			this.buttonHelp.Name = "buttonHelp";
			this.buttonHelp.Click += new System.EventHandler(this.buttonHelp_Click);
			//
			// regexHelper
			//
			resources.ApplyResources(this.regexHelper, "regexHelper");
			this.regexHelper.Name = "regexHelper";
			this.regexHelper.Click += new System.EventHandler(this.regexHelper_Click);
			//
			// m_MatchCasecheckBox
			//
			resources.ApplyResources(this.m_MatchCasecheckBox, "m_MatchCasecheckBox");
			this.m_MatchCasecheckBox.Name = "m_MatchCasecheckBox";
			//
			// m_MatchDiacriticscheckBox
			//
			resources.ApplyResources(this.m_MatchDiacriticscheckBox, "m_MatchDiacriticscheckBox");
			this.m_MatchDiacriticscheckBox.Name = "m_MatchDiacriticscheckBox";
			this.m_MatchDiacriticscheckBox.UseVisualStyleBackColor = true;
			//
			// SimpleMatchDlg
			//
			this.AcceptButton = this.m_okButton;
			resources.ApplyResources(this, "$this");
			this.CancelButton = this.m_cancelButton;
			this.Controls.Add(this.m_MatchDiacriticscheckBox);
			this.Controls.Add(this.m_MatchCasecheckBox);
			this.Controls.Add(this.regexHelper);
			this.Controls.Add(this.buttonHelp);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.m_regExButton);
			this.Controls.Add(this.m_cancelButton);
			this.Controls.Add(this.m_okButton);
			this.Controls.Add(this.m_wholeItemButton);
			this.Controls.Add(this.m_atEndButton);
			this.Controls.Add(this.m_atStartButton);
			this.Controls.Add(this.m_anywhereButton);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.Name = "SimpleMatchDlg";
			this.ResumeLayout(false);
			this.PerformLayout();

		}
		#endregion

		private void buttonHelp_Click(object sender, System.EventArgs e)
		{
			ShowHelp.ShowHelpTopic(m_helpTopicProvider, s_helpTopic);
		}

		private void m_regExButton_CheckedChanged(object sender, EventArgs e)
		{
			regexHelper.Enabled = m_regExButton.Checked;
		}

		private void regexHelper_Click(object sender, EventArgs e)
		{
			_regexContextContextMenu.Show(regexHelper, new System.Drawing.Point(regexHelper.Width, 0));
		}
	}
}