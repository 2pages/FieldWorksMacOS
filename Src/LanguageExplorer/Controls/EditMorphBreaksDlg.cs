// Copyright (c) 2007-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.FwCoreDlgs.Controls;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;

namespace LanguageExplorer.Controls
{
	/// <summary>
	/// The EditMorphBreaks dialog allows the user to edit the morph breaks in a word.  It is
	/// better for this purpose than just using the combobox's edit box because it has a much
	/// bigger edit box, and it displays some helpful (?) information to assist in marking the
	/// morpheme types.
	/// </summary>
	internal sealed class EditMorphBreaksDlg : Form
	{
		private Button m_btnOk;
		private Button m_btnCancel;
		private FwTextBox m_txtMorphs;
		private Container m_components;
		private Label m_lblWord;
		private GroupBox m_groupBox2BreakCharacters;
		private GroupBox m_groupBox1Examples;
		private Label m_lblHelp2Example1;
		private Label m_lblHelp2Example2;
		private Label m_lblBreakPrefixExample;
		private Label m_lblBreakSuffixExample;
		private Label m_lblBreakInfixExample;
		private Label m_lblBreakSimulfixExample;
		private Label m_lblBreakEncliticExample;
		private Label m_lblBreakProcliticExample;
		private Label m_lblBreakSuprafixExample;
		private Label m_lblBreakInfixLabel;
		private Label m_lblBreakSuffixLabel;
		private Label m_lblBreakPrefixLabel;
		private Label m_lblBreakSimulfixLabel;
		private Label m_lblBreakEncliticLabel;
		private Label m_lblBreakProcliticLabel;
		private Label m_lblBreakSuprafixtLabel;
		private Label m_lblBreakStemExample;
		private Label m_lblBreakBoundStemExample;
		private Label m_lblBreakStemLabel;
		private Label m_lblBreakBoundStemLabel;
		private Label m_label1;
		private Button m_buttonHelp;
		private string m_sMorphs;
		private const string ksHelpTopic = "khtpEditMorphBreaks";
		private Button m_morphBreakHelper;
		private readonly HelpProvider m_helpProvider;
		private MorphBreakHelperContextMenu _morphBreakContextContextMenu;
		private readonly IHelpTopicProvider m_helpTopicProvider;

		internal EditMorphBreaksDlg(IHelpTopicProvider helpTopicProvider)
		{
			InitializeComponent();
			m_helpTopicProvider = helpTopicProvider;
			AccessibleNameCreator.AddNames(this);
			AccessibleName = GetType().Name;
			if (!Application.RenderWithVisualStyles)
			{
				m_txtMorphs.BorderStyle = BorderStyle.FixedSingle;
			}
			m_helpProvider = new HelpProvider {HelpNamespace = helpTopicProvider.HelpFile};
			m_helpProvider.SetHelpKeyword(this, helpTopicProvider.GetHelpString(ksHelpTopic));
			m_helpProvider.SetHelpNavigator(this, HelpNavigator.Topic);
		}

		/// <summary>
		/// This sets the original wordform and morph-broken word into the dialog.
		/// </summary>
		internal void Initialize(ITsString tssWord, string sMorphs, ILgWritingSystemFactory wsf, LcmCache cache, IVwStylesheet stylesheet)
		{
			Debug.Assert(tssWord != null);
			Debug.Assert(wsf != null);
			var ttp = tssWord.get_Properties(0);
			Debug.Assert(ttp != null);
			var ws = ttp.GetIntPropValues((int)FwTextPropType.ktptWs, out _);
			Debug.Assert(ws != 0);
			var wsVern = wsf.get_EngineOrNull(ws);
			Debug.Assert(wsVern != null);
			m_txtMorphs.WritingSystemFactory = wsf;
			m_txtMorphs.WritingSystemCode = ws;
			m_txtMorphs.Text = sMorphs;
			m_sMorphs = sMorphs;
			// Fix the help strings to use the actual MorphType markers.
			var morphTypeRepo = cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>();
			morphTypeRepo.GetMajorMorphTypes(out var mmtStem, out var mmtPrefix, out var mmtSuffix, out var mmtInfix, out var mmtBoundStem, out var mmtProclitic, out var mmtEnclitic, out var mmtSimulfix, out var mmtSuprafix);
			// Format the labels according to the MoMorphType Prefix/Postfix values.
			var sExample1 = StringTable.Table.GetString("EditMorphBreaks-Example1", StringTable.DialogStrings);
			var sExample2 = StringTable.Table.GetString("EditMorphBreaks-Example2", StringTable.DialogStrings);
			var sStemExample = StringTable.Table.GetString("EditMorphBreaks-stemExample", StringTable.DialogStrings);
			var sAffixExample = StringTable.Table.GetString("EditMorphBreaks-affixExample", StringTable.DialogStrings);
			m_lblHelp2Example1.Text = string.Format(sExample1, mmtStem.Prefix ?? string.Empty, mmtStem.Postfix ?? string.Empty);
			m_lblHelp2Example2.Text = string.Format(sExample2, mmtSuffix.Prefix ?? string.Empty, mmtSuffix.Postfix ?? string.Empty);
			m_lblBreakStemExample.Text = string.Format(sStemExample, mmtStem.Prefix ?? string.Empty, mmtStem.Postfix ?? string.Empty);
			m_lblBreakBoundStemExample.Text = string.Format(sStemExample, mmtBoundStem.Prefix ?? string.Empty, mmtBoundStem.Postfix ?? string.Empty);
			m_lblBreakPrefixExample.Text = string.Format(sAffixExample, mmtPrefix.Prefix == null ? string.Empty : " " + mmtPrefix.Prefix, mmtPrefix.Postfix == null ? string.Empty : mmtPrefix.Postfix + " ");
			m_lblBreakSuffixExample.Text = string.Format(sAffixExample, mmtSuffix.Prefix == null ? string.Empty : " " + mmtSuffix.Prefix, mmtSuffix.Postfix == null ? string.Empty : mmtSuffix.Postfix + " ");
			m_lblBreakInfixExample.Text = string.Format(sAffixExample, mmtInfix.Prefix == null ? string.Empty : " " + mmtInfix.Prefix, mmtInfix.Postfix == null ? string.Empty : mmtInfix.Postfix + " ");
			m_lblBreakProcliticExample.Text = string.Format(sAffixExample, mmtProclitic.Prefix == null ? string.Empty : " " + mmtProclitic.Prefix, mmtProclitic.Postfix == null ? string.Empty : mmtProclitic.Postfix + " ");
			m_lblBreakEncliticExample.Text = string.Format(sAffixExample, mmtEnclitic.Prefix == null ? string.Empty : " " + mmtEnclitic.Prefix, mmtEnclitic.Postfix == null ? string.Empty : mmtEnclitic.Postfix + " ");
			m_lblBreakSimulfixExample.Text = string.Format(sAffixExample, mmtSimulfix.Prefix == null ? string.Empty : " " + mmtSimulfix.Prefix, mmtSimulfix.Postfix == null ? string.Empty : mmtSimulfix.Postfix + " ");
			m_lblBreakSuprafixExample.Text = string.Format(sAffixExample, mmtSuprafix.Prefix == null ? string.Empty : " " + mmtSuprafix.Prefix, mmtSuprafix.Postfix == null ? string.Empty : mmtSuprafix.Postfix + " ");
			_morphBreakContextContextMenu = new MorphBreakHelperContextMenu(m_txtMorphs, m_helpTopicProvider, cache);
			m_txtMorphs.AdjustForStyleSheet(this, null, stylesheet);
			m_morphBreakHelper.Height = m_txtMorphs.Height;
		}

		/// <summary>
		/// Retrieve the morph-broken word.
		/// </summary>
		/// <returns>string containing the morph-broken word</returns>
		internal string GetMorphs()
		{
			return m_sMorphs;
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			Debug.WriteLineIf(!disposing, "****************** Missing Dispose() call for " + GetType().Name + ". ******************");
			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}

			if ( disposing )
			{
				m_components?.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditMorphBreaksDlg));
			this.m_btnOk = new System.Windows.Forms.Button();
			this.m_btnCancel = new System.Windows.Forms.Button();
			this.m_txtMorphs = new SIL.FieldWorks.FwCoreDlgs.Controls.FwTextBox();
			this.m_lblWord = new System.Windows.Forms.Label();
			this.m_groupBox2BreakCharacters = new System.Windows.Forms.GroupBox();
			this.m_lblBreakSuprafixtLabel = new System.Windows.Forms.Label();
			this.m_lblBreakSimulfixLabel = new System.Windows.Forms.Label();
			this.m_lblBreakBoundStemLabel = new System.Windows.Forms.Label();
			this.m_lblBreakEncliticLabel = new System.Windows.Forms.Label();
			this.m_lblBreakProcliticLabel = new System.Windows.Forms.Label();
			this.m_lblBreakInfixLabel = new System.Windows.Forms.Label();
			this.m_lblBreakSuprafixExample = new System.Windows.Forms.Label();
			this.m_lblBreakSimulfixExample = new System.Windows.Forms.Label();
			this.m_lblBreakEncliticExample = new System.Windows.Forms.Label();
			this.m_lblBreakProcliticExample = new System.Windows.Forms.Label();
			this.m_lblBreakBoundStemExample = new System.Windows.Forms.Label();
			this.m_lblBreakInfixExample = new System.Windows.Forms.Label();
			this.m_lblBreakSuffixExample = new System.Windows.Forms.Label();
			this.m_lblBreakPrefixExample = new System.Windows.Forms.Label();
			this.m_lblBreakStemExample = new System.Windows.Forms.Label();
			this.m_lblBreakStemLabel = new System.Windows.Forms.Label();
			this.m_lblBreakSuffixLabel = new System.Windows.Forms.Label();
			this.m_lblBreakPrefixLabel = new System.Windows.Forms.Label();
			this.m_groupBox1Examples = new System.Windows.Forms.GroupBox();
			this.m_lblHelp2Example1 = new System.Windows.Forms.Label();
			this.m_lblHelp2Example2 = new System.Windows.Forms.Label();
			this.m_label1 = new System.Windows.Forms.Label();
			this.m_buttonHelp = new System.Windows.Forms.Button();
			this.m_morphBreakHelper = new System.Windows.Forms.Button();
			((System.ComponentModel.ISupportInitialize)(this.m_txtMorphs)).BeginInit();
			this.m_groupBox2BreakCharacters.SuspendLayout();
			this.m_groupBox1Examples.SuspendLayout();
			this.SuspendLayout();
			//
			// m_btnOk
			//
			resources.ApplyResources(this.m_btnOk, "m_btnOk");
			this.m_btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
			this.m_btnOk.Name = "m_btnOk";
			this.m_btnOk.Click += new System.EventHandler(this.MBtnOkClick);
			//
			// m_btnCancel
			//
			resources.ApplyResources(this.m_btnCancel, "m_btnCancel");
			this.m_btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.m_btnCancel.Name = "m_btnCancel";
			this.m_btnCancel.Click += new System.EventHandler(this.MBtnCancelClick);
			//
			// m_txtMorphs
			//
			this.m_txtMorphs.AcceptsReturn = false;
			this.m_txtMorphs.AdjustStringHeight = true;
			resources.ApplyResources(this.m_txtMorphs, "m_txtMorphs");
			this.m_txtMorphs.BackColor = System.Drawing.SystemColors.Window;
			this.m_txtMorphs.controlID = null;
			this.m_txtMorphs.HasBorder = true;
			this.m_txtMorphs.Name = "m_txtMorphs";
			this.m_txtMorphs.SuppressEnter = false;
			this.m_txtMorphs.WordWrap = false;
			//
			// m_lblWord
			//
			resources.ApplyResources(this.m_lblWord, "m_lblWord");
			this.m_lblWord.Name = "m_lblWord";
			//
			// m_groupBox2BreakCharacters
			//
			resources.ApplyResources(this.m_groupBox2BreakCharacters, "m_groupBox2BreakCharacters");
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakSuprafixtLabel);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakSimulfixLabel);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakBoundStemLabel);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakEncliticLabel);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakProcliticLabel);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakInfixLabel);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakSuprafixExample);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakSimulfixExample);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakEncliticExample);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakProcliticExample);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakBoundStemExample);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakInfixExample);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakSuffixExample);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakPrefixExample);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakStemExample);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakStemLabel);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakSuffixLabel);
			this.m_groupBox2BreakCharacters.Controls.Add(this.m_lblBreakPrefixLabel);
			this.m_groupBox2BreakCharacters.ForeColor = System.Drawing.SystemColors.ControlText;
			this.m_groupBox2BreakCharacters.Name = "m_groupBox2BreakCharacters";
			this.m_groupBox2BreakCharacters.TabStop = false;
			//
			// m_lblBreakSuprafixtLabel
			//
			resources.ApplyResources(this.m_lblBreakSuprafixtLabel, "m_lblBreakSuprafixtLabel");
			this.m_lblBreakSuprafixtLabel.Name = "m_lblBreakSuprafixtLabel";
			//
			// m_lblBreakSimulfixLabel
			//
			resources.ApplyResources(this.m_lblBreakSimulfixLabel, "m_lblBreakSimulfixLabel");
			this.m_lblBreakSimulfixLabel.Name = "m_lblBreakSimulfixLabel";
			//
			// m_lblBreakBoundStemLabel
			//
			resources.ApplyResources(this.m_lblBreakBoundStemLabel, "m_lblBreakBoundStemLabel");
			this.m_lblBreakBoundStemLabel.Name = "m_lblBreakBoundStemLabel";
			//
			// m_lblBreakEncliticLabel
			//
			resources.ApplyResources(this.m_lblBreakEncliticLabel, "m_lblBreakEncliticLabel");
			this.m_lblBreakEncliticLabel.Name = "m_lblBreakEncliticLabel";
			//
			// m_lblBreakProcliticLabel
			//
			resources.ApplyResources(this.m_lblBreakProcliticLabel, "m_lblBreakProcliticLabel");
			this.m_lblBreakProcliticLabel.Name = "m_lblBreakProcliticLabel";
			//
			// m_lblBreakInfixLabel
			//
			resources.ApplyResources(this.m_lblBreakInfixLabel, "m_lblBreakInfixLabel");
			this.m_lblBreakInfixLabel.Name = "m_lblBreakInfixLabel";
			//
			// m_lblBreakSuprafixExample
			//
			resources.ApplyResources(this.m_lblBreakSuprafixExample, "m_lblBreakSuprafixExample");
			this.m_lblBreakSuprafixExample.Name = "m_lblBreakSuprafixExample";
			//
			// m_lblBreakSimulfixExample
			//
			resources.ApplyResources(this.m_lblBreakSimulfixExample, "m_lblBreakSimulfixExample");
			this.m_lblBreakSimulfixExample.Name = "m_lblBreakSimulfixExample";
			//
			// m_lblBreakEncliticExample
			//
			resources.ApplyResources(this.m_lblBreakEncliticExample, "m_lblBreakEncliticExample");
			this.m_lblBreakEncliticExample.Name = "m_lblBreakEncliticExample";
			//
			// m_lblBreakProcliticExample
			//
			resources.ApplyResources(this.m_lblBreakProcliticExample, "m_lblBreakProcliticExample");
			this.m_lblBreakProcliticExample.Name = "m_lblBreakProcliticExample";
			//
			// m_lblBreakBoundStemExample
			//
			resources.ApplyResources(this.m_lblBreakBoundStemExample, "m_lblBreakBoundStemExample");
			this.m_lblBreakBoundStemExample.Name = "m_lblBreakBoundStemExample";
			//
			// m_lblBreakInfixExample
			//
			resources.ApplyResources(this.m_lblBreakInfixExample, "m_lblBreakInfixExample");
			this.m_lblBreakInfixExample.Name = "m_lblBreakInfixExample";
			//
			// m_lblBreakSuffixExample
			//
			resources.ApplyResources(this.m_lblBreakSuffixExample, "m_lblBreakSuffixExample");
			this.m_lblBreakSuffixExample.Name = "m_lblBreakSuffixExample";
			//
			// m_lblBreakPrefixExample
			//
			resources.ApplyResources(this.m_lblBreakPrefixExample, "m_lblBreakPrefixExample");
			this.m_lblBreakPrefixExample.Name = "m_lblBreakPrefixExample";
			//
			// m_lblBreakStemExample
			//
			resources.ApplyResources(this.m_lblBreakStemExample, "m_lblBreakStemExample");
			this.m_lblBreakStemExample.Name = "m_lblBreakStemExample";
			//
			// m_lblBreakStemLabel
			//
			resources.ApplyResources(this.m_lblBreakStemLabel, "m_lblBreakStemLabel");
			this.m_lblBreakStemLabel.Name = "m_lblBreakStemLabel";
			//
			// m_lblBreakSuffixLabel
			//
			resources.ApplyResources(this.m_lblBreakSuffixLabel, "m_lblBreakSuffixLabel");
			this.m_lblBreakSuffixLabel.Name = "m_lblBreakSuffixLabel";
			//
			// m_lblBreakPrefixLabel
			//
			resources.ApplyResources(this.m_lblBreakPrefixLabel, "m_lblBreakPrefixLabel");
			this.m_lblBreakPrefixLabel.Name = "m_lblBreakPrefixLabel";
			//
			// m_groupBox1Examples
			//
			this.m_groupBox1Examples.Controls.Add(this.m_lblHelp2Example1);
			this.m_groupBox1Examples.Controls.Add(this.m_lblHelp2Example2);
			resources.ApplyResources(this.m_groupBox1Examples, "m_groupBox1Examples");
			this.m_groupBox1Examples.Name = "m_groupBox1Examples";
			this.m_groupBox1Examples.TabStop = false;
			//
			// m_lblHelp2Example1
			//
			resources.ApplyResources(this.m_lblHelp2Example1, "m_lblHelp2Example1");
			this.m_lblHelp2Example1.Name = "m_lblHelp2Example1";
			//
			// m_lblHelp2Example2
			//
			resources.ApplyResources(this.m_lblHelp2Example2, "m_lblHelp2Example2");
			this.m_lblHelp2Example2.Name = "m_lblHelp2Example2";
			//
			// m_label1
			//
			resources.ApplyResources(this.m_label1, "m_label1");
			this.m_label1.Name = "m_label1";
			//
			// m_buttonHelp
			//
			resources.ApplyResources(this.m_buttonHelp, "m_buttonHelp");
			this.m_buttonHelp.Name = "m_buttonHelp";
			this.m_buttonHelp.Click += new System.EventHandler(this.ButtonHelpClick);
			//
			// m_morphBreakHelper
			//
			resources.ApplyResources(this.m_morphBreakHelper, "m_morphBreakHelper");
			this.m_morphBreakHelper.Name = "m_morphBreakHelper";
			this.m_morphBreakHelper.Click += new System.EventHandler(this.MorphBreakHelperClick);
			//
			// EditMorphBreaksDlg
			//
			this.AcceptButton = this.m_btnOk;
			resources.ApplyResources(this, "$this");
			this.CancelButton = this.m_btnCancel;
			this.Controls.Add(this.m_morphBreakHelper);
			this.Controls.Add(this.m_buttonHelp);
			this.Controls.Add(this.m_label1);
			this.Controls.Add(this.m_groupBox1Examples);
			this.Controls.Add(this.m_groupBox2BreakCharacters);
			this.Controls.Add(this.m_lblWord);
			this.Controls.Add(this.m_txtMorphs);
			this.Controls.Add(this.m_btnCancel);
			this.Controls.Add(this.m_btnOk);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "EditMorphBreaksDlg";
			this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
			((System.ComponentModel.ISupportInitialize)(this.m_txtMorphs)).EndInit();
			this.m_groupBox2BreakCharacters.ResumeLayout(false);
			this.m_groupBox1Examples.ResumeLayout(false);
			this.ResumeLayout(false);

		}
		#endregion

		private void MorphBreakHelperClick(object sender, EventArgs e)
		{
			_morphBreakContextContextMenu.Show(m_morphBreakHelper, new System.Drawing.Point(m_morphBreakHelper.Width,0));
		}

		private void MBtnOkClick(object sender, EventArgs e)
		{
			DialogResult =  DialogResult.OK;
			m_sMorphs = m_txtMorphs.Text;
			Close();
		}

		private void MBtnCancelClick(object sender, EventArgs e)
		{
			DialogResult =  DialogResult.Cancel;
			Close();
		}

		private void ButtonHelpClick(object sender, EventArgs e)
		{
			ShowHelp.ShowHelpTopic(m_helpTopicProvider, ksHelpTopic);
		}
	}
}