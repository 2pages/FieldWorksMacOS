// Copyright (c) 2003-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
<<<<<<< HEAD
using System.Windows.Forms;
||||||| f013144d5
using SIL.FieldWorks.Common.RootSites;
using SIL.FieldWorks.Resources;
using SIL.LCModel.Utils;
using SIL.FieldWorks.Common.FwUtils;
=======
using SIL.FieldWorks.Common.RootSites;
using SIL.FieldWorks.Resources;
using SIL.FieldWorks.Common.FwUtils;
>>>>>>> develop
using ECInterfaces;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Resources;
using SilEncConverters40;

namespace SIL.FieldWorks.FwCoreDlgs
{
	/// <summary>
	/// The dialog for adding/configuring encoding converters
	/// </summary>
	internal class AddCnvtrDlg : Form
	{
		#region Constants
		/// <summary>Index of the tab for encoding converters properties</summary>
		private const int kECProperties = 0;
		/// <summary>Index of the tab for encoding converters test</summary>
		private const int kECTest = 1;
		/// <summary>Index of the tab for encoding converters advanced features</summary>
		private const int kECAdvanced = 2;
		#endregion

		#region Member variables
		private TabPage propertiesTab;
		private TabPage testTab;
		private TabPage advancedTab;
		private Button btnAdd;
		private Button btnCopy;
		private Button btnDelete;
		private ListBox availableCnvtrsListBox;

		private EncConverters m_encConverters;
		private IHelpTopicProvider m_helpTopicProvider;
		private IApp m_app;
		/// <summary>properties tab</summary>
		internal CnvtrPropertiesCtrl m_cnvtrPropertiesCtrl;
		/// <summary>advanced tab</summary>
		private AdvancedEncProps m_advancedEncProps;
		/// <summary>test tab</summary>
		private ConverterTrial _converterTrial;
		/// <summary>Encoding converters which have not yet been fully defined</summary>
		private Dictionary<string, EncoderInfo> m_undefinedConverters = new Dictionary<string, EncoderInfo>();
		private bool m_fOnlyUnicode;
		// internal because a test thinks it has to have direct access.
		internal bool m_outsideDlgChangedCnvtrs;
		private bool m_currentlyAdding;
		private bool m_fDiscardingChanges;
		private bool m_fClosingDialog;
		private bool m_transduceDialogOpen;
		private bool m_currentlyLoading;
		private bool m_suppressListBoxIndexChanged;
		private bool m_suppressAutosave;
		private string m_toSelect;
		private string m_sConverterToAdd;
		private ISet<string> m_WSInUse;
		/// <summary>For testing</summary>
		internal string m_msg;

		/// <summary>
		/// Stores the name of the EC to be deleted if it is renamed
		/// </summary>
		private string m_oldConverter;
		private Label label1;
		private TabControl m_addCnvtrTabCtrl;
		/// <summary>Required designer variable</summary>
		private IContainer m_components = null;
		#endregion

		#region Constructors

		/// <summary />
		/// <param name="helpTopicProvider">help topic provider for the Help button</param>
		/// <param name="app">The app.</param>
		/// <param name="encConverters">The enc converters.</param>
		/// <param name="wsInUse">The ws in use.</param>
		/// <param name="onlyUnicodeCnvtrs">if set to <c>true</c> [only unicode CNVTRS].</param>
		internal AddCnvtrDlg(IHelpTopicProvider helpTopicProvider, IApp app, EncConverters encConverters, ISet<string> wsInUse, bool onlyUnicodeCnvtrs)
			: this(helpTopicProvider, app, encConverters, null, wsInUse, onlyUnicodeCnvtrs)
		{
		}

		/// <summary />
		/// <param name="helpTopicProvider">The help topic provider.</param>
		/// <param name="app">The app.</param>
		/// <param name="encConverters">The enc converters.</param>
		/// <param name="selectConv">Converter to be selected</param>
		/// <param name="wsInUse">The ws in use.</param>
		/// <param name="onlyUnicodeCnvtrs">If true, show and create only Unicode converters (both to and to/from).</param>
		internal AddCnvtrDlg(IHelpTopicProvider helpTopicProvider, IApp app, EncConverters encConverters, string selectConv, ISet<string> wsInUse, bool onlyUnicodeCnvtrs)
		{
			// Set members
			AccessibleName = GetType().Name;
			m_helpTopicProvider = helpTopicProvider;
			m_app = app;
			m_fOnlyUnicode = onlyUnicodeCnvtrs;
			m_toSelect = selectConv;

			// Take care of null values
			m_encConverters = encConverters ?? new EncConverters();

			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();

			// Things that must happen after InitializeComponent() follow:

			InitWSInUse(wsInUse);

			// LT-6927: this is ugly, but could be added back into generated
			// parts after test tab conversion problems are resolved -- CameronB
			if (!m_fOnlyUnicode)
			{
				m_addCnvtrTabCtrl.Controls.Add(testTab);
				m_addCnvtrTabCtrl.Controls.Add(advancedTab);
			}

			m_cnvtrPropertiesCtrl.Application = app;
			m_cnvtrPropertiesCtrl.Converters = m_encConverters;
			m_cnvtrPropertiesCtrl.UndefinedConverters = m_undefinedConverters;
			_converterTrial.Converters = m_encConverters;
			m_advancedEncProps.Converters = m_encConverters;

			if (m_fOnlyUnicode) // Not really encoding converters in this case.
			{
				m_cnvtrPropertiesCtrl.OnlyUnicode = true;
				Text = AddConverterDlgStrings.kstidSetupProcessor;
				label1.Text = AddConverterDlgStrings.kstidAvailableProcessors;
			}
		}
		#endregion

		#region Dispose
<<<<<<< HEAD
||||||| f013144d5
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Check to see if the object has been disposed.
		/// All public Properties and Methods should call this
		/// before doing anything else.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void CheckDisposed()
		{
			if (IsDisposed)
				throw new ObjectDisposedException(String.Format("'{0}' in use after being disposed.", GetType().Name));
		}
=======
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Check to see if the object has been disposed.
		/// All public Properties and Methods should call this
		/// before doing anything else.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void CheckDisposed()
		{
			if (IsDisposed)
				throw new ObjectDisposedException($"'{GetType().Name}' in use after being disposed.");
		}
>>>>>>> develop

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
<<<<<<< HEAD
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
||||||| f013144d5
			System.Diagnostics.Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			// Must not be run more than once.
=======
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			// Must not be run more than once.
>>>>>>> develop
			if (IsDisposed)
<<<<<<< HEAD
||||||| f013144d5
				return;

			if( disposing )
=======
				return;

			if(disposing)
>>>>>>> develop
			{
<<<<<<< HEAD
				// No need to run it more than once.
				return;
			}

			if ( disposing )
			{
				m_components?.Dispose();
||||||| f013144d5
				if(m_components != null)
				{
					m_components.Dispose();
				}
=======
				m_components?.Dispose();
>>>>>>> develop
			}
			base.Dispose(disposing);
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddCnvtrDlg));
			System.Windows.Forms.Button btnClose;
			System.Windows.Forms.HelpProvider helpProvider1;
			this.label1 = new System.Windows.Forms.Label();
			this.m_addCnvtrTabCtrl = new System.Windows.Forms.TabControl();
			this.propertiesTab = new System.Windows.Forms.TabPage();
			this.m_cnvtrPropertiesCtrl = new SIL.FieldWorks.FwCoreDlgs.CnvtrPropertiesCtrl();
			this.testTab = new System.Windows.Forms.TabPage();
			this._converterTrial = new SIL.FieldWorks.FwCoreDlgs.ConverterTrial();
			this.advancedTab = new System.Windows.Forms.TabPage();
			this.m_advancedEncProps = new SIL.FieldWorks.FwCoreDlgs.AdvancedEncProps();
			this.availableCnvtrsListBox = new System.Windows.Forms.ListBox();
			this.btnAdd = new System.Windows.Forms.Button();
			this.btnCopy = new System.Windows.Forms.Button();
			this.btnDelete = new System.Windows.Forms.Button();
			btnHelp = new System.Windows.Forms.Button();
			btnClose = new System.Windows.Forms.Button();
			helpProvider1 = new System.Windows.Forms.HelpProvider();
			this.m_addCnvtrTabCtrl.SuspendLayout();
			this.propertiesTab.SuspendLayout();
			this.testTab.SuspendLayout();
			this.advancedTab.SuspendLayout();
			this.SuspendLayout();
			//
			// btnHelp
			//
			resources.ApplyResources(btnHelp, "btnHelp");
			btnHelp.Name = "btnHelp";
			helpProvider1.SetShowHelp(btnHelp, ((bool)(resources.GetObject("btnHelp.ShowHelp"))));
			btnHelp.Click += new System.EventHandler(this.btnHelp_Click);
			//
			// btnClose
			//
			resources.ApplyResources(btnClose, "btnClose");
			helpProvider1.SetHelpString(btnClose, resources.GetString("btnClose.HelpString"));
			btnClose.Name = "btnClose";
			helpProvider1.SetShowHelp(btnClose, ((bool)(resources.GetObject("btnClose.ShowHelp"))));
			btnClose.Click += new System.EventHandler(this.btnClose_Click);
			//
			// label1
			//
			resources.ApplyResources(this.label1, "label1");
			this.label1.Name = "label1";
			helpProvider1.SetShowHelp(this.label1, ((bool)(resources.GetObject("label1.ShowHelp"))));
			//
			// m_addCnvtrTabCtrl
			//
			resources.ApplyResources(this.m_addCnvtrTabCtrl, "m_addCnvtrTabCtrl");
			this.m_addCnvtrTabCtrl.Controls.Add(this.propertiesTab);
			this.m_addCnvtrTabCtrl.Name = "m_addCnvtrTabCtrl";
			this.m_addCnvtrTabCtrl.SelectedIndex = 0;
			helpProvider1.SetShowHelp(this.m_addCnvtrTabCtrl, ((bool)(resources.GetObject("m_addCnvtrTabCtrl.ShowHelp"))));
			this.m_addCnvtrTabCtrl.SelectedIndexChanged += new System.EventHandler(this.AddCnvtrTabCtrl_SelectedIndexChanged);
			//
			// propertiesTab
			//
			this.propertiesTab.Controls.Add(this.m_cnvtrPropertiesCtrl);
			resources.ApplyResources(this.propertiesTab, "propertiesTab");
			this.propertiesTab.Name = "propertiesTab";
			helpProvider1.SetShowHelp(this.propertiesTab, ((bool)(resources.GetObject("propertiesTab.ShowHelp"))));
			this.propertiesTab.Tag = "ktagProperties";
			this.propertiesTab.UseVisualStyleBackColor = true;
			//
			// m_cnvtrPropertiesCtrl
			//
			this.m_cnvtrPropertiesCtrl.BackColor = System.Drawing.Color.Transparent;
			this.m_cnvtrPropertiesCtrl.ConverterChanged = true;
			resources.ApplyResources(this.m_cnvtrPropertiesCtrl, "m_cnvtrPropertiesCtrl");
			this.m_cnvtrPropertiesCtrl.Name = "m_cnvtrPropertiesCtrl";
			this.m_cnvtrPropertiesCtrl.OnlyUnicode = false;
			helpProvider1.SetShowHelp(this.m_cnvtrPropertiesCtrl, ((bool)(resources.GetObject("m_cnvtrPropertiesCtrl.ShowHelp"))));
			//
			// testTab
			//
			this.testTab.Controls.Add(this._converterTrial);
			resources.ApplyResources(this.testTab, "testTab");
			this.testTab.Name = "testTab";
			helpProvider1.SetShowHelp(this.testTab, ((bool)(resources.GetObject("testTab.ShowHelp"))));
			this.testTab.Tag = "ktagTest";
			this.testTab.UseVisualStyleBackColor = true;
			//
			// m_converterTest
			//
			this._converterTrial.Converters = null;
			resources.ApplyResources(this._converterTrial, "_converterTrial");
			this._converterTrial.Name = "_converterTrial";
			helpProvider1.SetShowHelp(this._converterTrial, ((bool)(resources.GetObject("_converterTrial.ShowHelp"))));
			//
			// advancedTab
			//
			this.advancedTab.Controls.Add(this.m_advancedEncProps);
			resources.ApplyResources(this.advancedTab, "advancedTab");
			this.advancedTab.Name = "advancedTab";
			helpProvider1.SetShowHelp(this.advancedTab, ((bool)(resources.GetObject("advancedTab.ShowHelp"))));
			this.advancedTab.Tag = "ktagAdvanced";
			this.advancedTab.UseVisualStyleBackColor = true;
			//
			// m_advancedEncProps
			//
			this.m_advancedEncProps.Converters = null;
			resources.ApplyResources(this.m_advancedEncProps, "m_advancedEncProps");
			this.m_advancedEncProps.Name = "m_advancedEncProps";
			helpProvider1.SetShowHelp(this.m_advancedEncProps, ((bool)(resources.GetObject("m_advancedEncProps.ShowHelp"))));
			//
			// availableCnvtrsListBox
			//
			this.availableCnvtrsListBox.FormattingEnabled = true;
			helpProvider1.SetHelpString(this.availableCnvtrsListBox, resources.GetString("availableCnvtrsListBox.HelpString"));
			resources.ApplyResources(this.availableCnvtrsListBox, "availableCnvtrsListBox");
			this.availableCnvtrsListBox.Name = "availableCnvtrsListBox";
			helpProvider1.SetShowHelp(this.availableCnvtrsListBox, ((bool)(resources.GetObject("availableCnvtrsListBox.ShowHelp"))));
			this.availableCnvtrsListBox.Sorted = true;
			this.availableCnvtrsListBox.SelectedIndexChanged += new System.EventHandler(this.availableCnvtrsListBox_SelectedIndexChanged);
			//
			// btnAdd
			//
			resources.ApplyResources(this.btnAdd, "btnAdd");
			this.btnAdd.Name = "btnAdd";
			helpProvider1.SetShowHelp(this.btnAdd, ((bool)(resources.GetObject("btnAdd.ShowHelp"))));
			this.btnAdd.UseVisualStyleBackColor = true;
			this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
			//
			// btnCopy
			//
			resources.ApplyResources(this.btnCopy, "btnCopy");
			this.btnCopy.Name = "btnCopy";
			helpProvider1.SetShowHelp(this.btnCopy, ((bool)(resources.GetObject("btnCopy.ShowHelp"))));
			this.btnCopy.UseVisualStyleBackColor = true;
			this.btnCopy.Click += new System.EventHandler(this.btnCopy_Click);
			//
			// btnDelete
			//
			resources.ApplyResources(this.btnDelete, "btnDelete");
			this.btnDelete.Name = "btnDelete";
			helpProvider1.SetShowHelp(this.btnDelete, ((bool)(resources.GetObject("btnDelete.ShowHelp"))));
			this.btnDelete.UseVisualStyleBackColor = true;
			this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
			//
			// AddCnvtrDlg
			//
			resources.ApplyResources(this, "$this");
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ControlBox = false;
			this.Controls.Add(this.btnDelete);
			this.Controls.Add(this.btnCopy);
			this.Controls.Add(this.btnAdd);
			this.Controls.Add(btnClose);
			this.Controls.Add(this.availableCnvtrsListBox);
			this.Controls.Add(btnHelp);
			this.Controls.Add(this.m_addCnvtrTabCtrl);
			this.Controls.Add(this.label1);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "AddCnvtrDlg";
			helpProvider1.SetShowHelp(this, ((bool)(resources.GetObject("$this.ShowHelp"))));
			this.ShowInTaskbar = false;
			this.Load += new System.EventHandler(this.AddCnvtrDlg_Load);
			this.m_addCnvtrTabCtrl.ResumeLayout(false);
			this.propertiesTab.ResumeLayout(false);
			this.testTab.ResumeLayout(false);
			this.advancedTab.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}
		#endregion

		#region Event handlers for buttons
		/// <summary>
		/// Handles the Click event of the btnAdd control.
		/// </summary>
		private void btnAdd_Click(object sender, EventArgs e)
		{
			if (AutoSave())
			{
				m_currentlyAdding = true;
				JumpToHomeTab();
				m_suppressAutosave = true;
				m_suppressListBoxIndexChanged = true;
				try
				{
					SetFieldsForAdd();
					SetUnchanged();
					SetStates();
					RefreshTabs();
				}
				finally
				{
					m_suppressAutosave = false;
					m_suppressListBoxIndexChanged = false;
					m_currentlyAdding = false;
				}
				m_cnvtrPropertiesCtrl.EnableEntirePane(true); // just to make sure
			}
			m_currentlyAdding = false;
		}

		/// <summary>
		/// Handles the Click event of the btnCopy control.
		/// </summary>
		private void btnCopy_Click(object sender, EventArgs e)
		{
			var goToNextIndex = SelectedConverterIndex + 1;
			if (AutoSave())
			{
				JumpToHomeTab();
				SetFieldsForCopy();
				if (!AbortInstallDueToOverwrite())
				{
					SetUnchanged();
					InstallConverter();
					SelectedConverterIndex = goToNextIndex;
				}
			}
		}

		/// <summary>
		/// Handles the Click event of the btnDelete control.
		/// </summary>
		private void btnDelete_Click(object sender, EventArgs e)
		{
			var goToNextIndex = SelectedConverterIndex;// +1; //no, because the current EC is deleted
			RemoveConverter(SelectedConverter);
			SelectedConverterIndex = goToNextIndex;
			SetStates();
		}

		/// <summary>
		/// Handles the Click event of the btnClose control.
		/// </summary>
		private void btnClose_Click(object sender, EventArgs e)
		{
			m_fClosingDialog = true;
			if (m_undefinedConverters.Count > 0)
			{
				// loop through all the encoding converters that are not fully defined.
				for (; ;)
				{
					using (IEnumerator<KeyValuePair<string, EncoderInfo>> enumerator = m_undefinedConverters.GetEnumerator())
					{
						if (!enumerator.MoveNext())
						{
							break;
						}
						SelectedConverter = enumerator.Current.Value.Name;
					}
					if (!AutoSave())
					{
						m_fClosingDialog = false;
						return; // Let user correct problem with converter.
					}
				}
			}

			var newConv = ConverterName;
			if (AutoSave())
			{
				SelectedConverter = newConv;
				SetUnchanged();
				DialogResult = DialogResult.OK;
			}
			SetStates();
			m_fClosingDialog = false;
		}

		/// <summary>
		/// Open the appropriate Help file for selected tab.
		/// </summary>
		private void btnHelp_Click(object sender, EventArgs e)
		{
			var helpTopicKey = new StringBuilder("khtpEC");
			if (m_fOnlyUnicode)
			{
				helpTopicKey.Append("Process");
			}
			switch (m_addCnvtrTabCtrl.SelectedIndex)
			{
				case kECProperties:
					helpTopicKey.Append("Properties");
					break;
				case kECTest:
					helpTopicKey.Append("Test");
					break;
				case kECAdvanced:
					helpTopicKey.Append("Advanced");
					break;
			}
			ShowHelp.ShowHelpTopic(m_helpTopicProvider, helpTopicKey.ToString());
		}
		#endregion

		/// <summary>
		/// Handles the Load event of the AddCnvtrDlg control.
		/// </summary>
<<<<<<< HEAD
		private void AddCnvtrDlg_Load(object sender, EventArgs e)
||||||| f013144d5
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">The <see cref="T:System.EventArgs"/> instance containing the event
		/// data.</param>
		/// ------------------------------------------------------------------------------------
		private void AddCnvtrDlg_Load(object sender, System.EventArgs e)
=======
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">The <see cref="T:System.EventArgs"/> instance containing the event
		/// data.</param>
		/// ------------------------------------------------------------------------------------
		private void AddCnvtrDlg_Load(object sender, EventArgs e)
>>>>>>> develop
		{
			m_currentlyLoading = true;
			m_cnvtrPropertiesCtrl.ConverterListChanged += cnvtrPropertiesCtrl_ConverterListChanged;
			m_cnvtrPropertiesCtrl.ConverterSaved += cnvtrPropertiesCtrl_ConverterSaved;
			m_cnvtrPropertiesCtrl.ConverterFileChanged += cnvtrPropertiesCtrl_ConverterFileChanged;
			RefreshListBox();
			SelectedConverterZeroDefault = m_toSelect;
			SetStates();
			m_currentlyLoading = false;
		}

		/// <summary>
		/// Reloads contents of the Available Converters ListBox
		/// </summary>
		internal void RefreshListBox()
		{
			if (m_outsideDlgChangedCnvtrs)
			{
				// after the dialog is closed, we have to "reboot" the list
				// of encoding converters to detect the new converters
				m_encConverters = new EncConverters();
				m_cnvtrPropertiesCtrl.Converters = m_encConverters;
				_converterTrial.Converters = m_encConverters;
				m_advancedEncProps.Converters = m_encConverters;
				m_outsideDlgChangedCnvtrs = false;
			}

			// then we will perform the standard "refresh" of the listbox
			availableCnvtrsListBox.Items.Clear();
			foreach (string convName in m_encConverters.Keys)
			{
				if (m_fOnlyUnicode)
				{
					var conv = m_encConverters[convName];
					// Only Unicode-to-Unicode converters are relevant.
					if (conv.ConversionType == ConvType.Unicode_to_Unicode || conv.ConversionType == ConvType.Unicode_to_from_Unicode)
					{
						availableCnvtrsListBox.Items.Add(convName);
					}
				}
				else
				{
					availableCnvtrsListBox.Items.Add(convName);
				}
			}

			// Now add the converters that haven't been validated.
<<<<<<< HEAD
			foreach (var info in m_undefinedConverters.Values)
			{
				availableCnvtrsListBox.Items.Add(info.Name);
			}
||||||| f013144d5
			foreach (EncoderInfo info in m_undefinedConverters.Values)
				availableCnvtrsListBox.Items.Add(info.m_name);
=======
			foreach (var info in m_undefinedConverters.Values)
				availableCnvtrsListBox.Items.Add(info.m_name);
>>>>>>> develop
		}

		/// <summary>
		/// Handles the ConverterListChanged event of the cnvtrPropertiesCtrl control.
		/// </summary>
		private void cnvtrPropertiesCtrl_ConverterListChanged(object sender, EventArgs e)
		{
			RefreshListBox();
		}

		/// <summary>
		/// Handles the ConverterSaved event of the cnvtrPropertiesCtrl control.
		/// </summary>
		private void cnvtrPropertiesCtrl_ConverterSaved(object sender, EventArgs e)
		{
			SelectedConverter = ConverterName;
		}

		/// <summary>
		/// Handles the ConverterFileChanged event of the cnvtrPropertiesCtrl control.
		/// </summary>
		private void cnvtrPropertiesCtrl_ConverterFileChanged(object sender, EventArgs e)
		{
			// Update the states of the buttons with the change.
			SetStates();
		}

		/// <summary>
		/// Does the same as SelectedConverter, but selects index 0 by default.
		/// </summary>
		private string SelectedConverterZeroDefault
		{
			set
			{
				SelectedConverter = value;
				if (SelectedConverterIndex == -1)
				{
					SelectedConverterIndex = 0;
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether the selected converter is installed.
		/// </summary>
<<<<<<< HEAD
		private bool IsConverterInstalled => m_encConverters.ContainsKey(SelectedConverter);
||||||| f013144d5
		/// ------------------------------------------------------------------------------------
		private bool IsConverterInstalled
		{
			get { return m_encConverters.ContainsKey(SelectedConverter); }
		}
=======
		/// ------------------------------------------------------------------------------------
		private bool IsConverterInstalled => m_encConverters.ContainsKey(SelectedConverter);
>>>>>>> develop

		/// <summary>
		/// Gets or sets the name of the currently selected converter, if any.
		/// NOTE: This is also used to "return" the name of the user selected EC to a parent dialog.
		/// </summary>
		internal string SelectedConverter
		{
			set
			{
<<<<<<< HEAD
				if (string.IsNullOrEmpty(value) || !m_encConverters.ContainsKey(value.Trim()) && !m_undefinedConverters.ContainsKey(value.Trim()))
||||||| f013144d5
				if (String.IsNullOrEmpty(value) ||
					(!m_encConverters.ContainsKey(value.Trim()) &&
					!m_undefinedConverters.ContainsKey(value.Trim())))
=======
				if (string.IsNullOrEmpty(value) ||
					!m_encConverters.ContainsKey(value.Trim()) &&
					!m_undefinedConverters.ContainsKey(value.Trim()))
>>>>>>> develop
				{
					SelectedConverterIndex = -1;
				}
				else if (SelectedConverter.Trim() != value.Trim())
<<<<<<< HEAD
				{
					// most of the time, this will be correct. As a matter of cost reduction, we should test that first
					for (var i = 0; i < availableCnvtrsListBox.Items.Count; ++i)
||||||| f013144d5
				{ // most of the time, this will be correct. As a matter of cost reduction, we should test that first
					for (int i = 0; i < availableCnvtrsListBox.Items.Count; ++i)
=======
				{
					for (var i = 0; i < availableCnvtrsListBox.Items.Count; ++i)
>>>>>>> develop
					{
						if (availableCnvtrsListBox.Items[i].ToString() == value)
						{
							SelectedConverterIndex = i;
							break;
						}
					}
				}
			}
			get => availableCnvtrsListBox.Text;
		}

		/// <summary>
		/// Gets or sets the index of the currently selected converter, if any.
		/// Keep in mind, this changes because the listbox is set to autosort.
		/// </summary>
		internal int SelectedConverterIndex
		{
			set
			{
				// makes input and output behave similarly (and forces validity check)
				// either user wants a deselect or we must deselect (if no ECs in list)
				if (value == -1 || availableCnvtrsListBox.Items.Count == 0)
				{
					// deselect, if not already deselected
					if (availableCnvtrsListBox.SelectedIndex != -1)
					{
						m_suppressAutosave = true;
						availableCnvtrsListBox.SetSelected(availableCnvtrsListBox.SelectedIndex, false);
						m_suppressAutosave = false;
					}
				}
				else
				{
					if (value > availableCnvtrsListBox.Items.Count - 1) // index too high
					{
						availableCnvtrsListBox.SelectedIndex = availableCnvtrsListBox.Items.Count - 1;
					}
					else if (value < -1) // index too low
					{
						availableCnvtrsListBox.SelectedIndex = 0;
					}
					else
					{
						availableCnvtrsListBox.SelectedIndex = value;
					}
				}
			}
			get => availableCnvtrsListBox.SelectedIndex;
		}

		/// <summary>
		/// Gets and Sets txtName in the CnvtrPropertiesCtrl as a specified string value.
		/// </summary>
		internal string ConverterName
		{
<<<<<<< HEAD
			get => m_cnvtrPropertiesCtrl.txtName.Text.Trim();
			set => m_cnvtrPropertiesCtrl.txtName.Text = value;
||||||| f013144d5
			set { m_cnvtrPropertiesCtrl.txtName.Text = value; }
			get { return m_cnvtrPropertiesCtrl.txtName.Text.Trim(); }
=======
			set => m_cnvtrPropertiesCtrl.txtName.Text = value;
			get => m_cnvtrPropertiesCtrl.txtName.Text.Trim();
>>>>>>> develop
		}

		/// <summary>
		/// Handles the SelectedIndexChanged event of the availableCnvtrsListBox control.
		/// </summary>
		private void availableCnvtrsListBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (!m_suppressListBoxIndexChanged)
			{
				m_suppressListBoxIndexChanged = true;
				//We shouldn't load the next converter if the autosave fails
				var shouldLoadConv = true;
				if (!m_currentlyLoading)
				{
					var returnToName = SelectedConverter;
					shouldLoadConv = AutoSave();
					if (shouldLoadConv)
					{
						SelectedConverter = returnToName;
					}
				}
				if (shouldLoadConv)
				{
					RefreshTabs();
				}
				SetStates();
				m_suppressListBoxIndexChanged = false;
			}
		}

		/// <summary>
		/// Selects the first tab of the tab control on this dialog.
		/// </summary>
		private void JumpToHomeTab()
		{
			if (m_addCnvtrTabCtrl.SelectedIndex != 0)
			{
<<<<<<< HEAD
				m_addCnvtrTabCtrl.SelectedIndex = 0;
||||||| f013144d5
				// Loading newly selected EC
				m_cnvtrPropertiesCtrl.SelectMapping((string)availableCnvtrsListBox.SelectedItem);

				bool fValidEncConverter = m_encConverters.ContainsKey(ConverterName);
				if (fValidEncConverter)
				{
					m_converterTest.SelectMapping((string)availableCnvtrsListBox.SelectedItem);
					m_advancedEncProps.SelectMapping((string)availableCnvtrsListBox.SelectedItem);
				}

				SetUnchanged();
				// make sure copy is disabled
				SetStates();
=======
				// Loading newly selected EC
				m_cnvtrPropertiesCtrl.SelectMapping((string)availableCnvtrsListBox.SelectedItem);

				var fValidEncConverter = m_encConverters.ContainsKey(ConverterName);
				if (fValidEncConverter)
				{
					m_converterTest.SelectMapping((string)availableCnvtrsListBox.SelectedItem);
					m_advancedEncProps.SelectMapping((string)availableCnvtrsListBox.SelectedItem);
				}

				SetUnchanged();
				// make sure copy is disabled
				SetStates();
>>>>>>> develop
			}
		}

		/// <summary>
		/// Setup the dialog for the currently selected encoding converter.
		/// </summary>
		private void RefreshTabs()
		{
			// Check if newly selected EC is valid
			if (availableCnvtrsListBox.SelectedItem == null)
			{
				return;
			}
			// Loading newly selected EC
			m_cnvtrPropertiesCtrl.SelectMapping((string)availableCnvtrsListBox.SelectedItem);
			var fValidEncConverter = m_encConverters.ContainsKey(ConverterName);
			if (fValidEncConverter)
			{
				_converterTrial.SelectMapping((string)availableCnvtrsListBox.SelectedItem);
				m_advancedEncProps.SelectMapping((string)availableCnvtrsListBox.SelectedItem);
			}
			SetUnchanged();
			// make sure copy is disabled
			SetStates();
		}

		/// <summary>
		/// Handles the SelectedIndexChanged event of the AddCnvtrTabCtrl control.
		/// </summary>
		private void AddCnvtrTabCtrl_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (m_currentlyLoading || m_addCnvtrTabCtrl.SelectedIndex == 0)
			{
<<<<<<< HEAD
				return;
			}
			var returnToIndex = SelectedConverterIndex;
			var returnToName = ConverterName;
			if (AutoSave())
			{
				// reselect (because it forgets otherwise)
				if (!string.IsNullOrEmpty(returnToName))
||||||| f013144d5
				int returnToIndex = SelectedConverterIndex;
				string returnToName = ConverterName;
				if (AutoSave())
=======
				var returnToIndex = SelectedConverterIndex;
				var returnToName = ConverterName;
				if (AutoSave())
>>>>>>> develop
				{
<<<<<<< HEAD
					SelectedConverter = returnToName;
				}
				else if (returnToIndex != -1)
				{
					SelectedConverterIndex = returnToIndex;
||||||| f013144d5
					// reselect (because it forgets otherwise)
					if (!String.IsNullOrEmpty(returnToName))
						SelectedConverter = returnToName;
					else if (returnToIndex != -1)
						SelectedConverterIndex = returnToIndex;
					else
						SelectedConverterIndex = 0;
=======
					// reselect (because it forgets otherwise)
					if (!string.IsNullOrEmpty(returnToName))
						SelectedConverter = returnToName;
					else if (returnToIndex != -1)
						SelectedConverterIndex = returnToIndex;
					else
						SelectedConverterIndex = 0;
>>>>>>> develop
				}
				else
				{
					SelectedConverterIndex = 0;
				}
			}
			else
			{
				m_addCnvtrTabCtrl.SelectedIndex = 0;
			}
		}

		#region add, copy, delete and autosave logic

		/// <summary>
		/// Sets the fields in the encoding converter for an Add.
		/// </summary>
		private void SetFieldsForAdd()
		{
			//set defaults
			ConverterName = string.Empty;
			m_cnvtrPropertiesCtrl.cboConverter.SelectedIndex = (int)ConverterType.ktypeTecKitTec;
			m_cnvtrPropertiesCtrl.txtMapFile.Text = string.Empty;

			// if we're currently adding and the user has not indicated that they want to discard
			// their changes...
			if (m_currentlyAdding && !m_fDiscardingChanges)
			{
				// Add a converter with a unique name that is selected so that the user can
				// easily change it.
				SelectedConverterIndex = GetNewConverterName(out m_sConverterToAdd);
				ConverterName = m_sConverterToAdd;
<<<<<<< HEAD
				m_undefinedConverters.Add(ConverterName, new EncoderInfo(ConverterName, ConverterType.ktypeTecKitTec, string.Empty, ConvType.Legacy_to_from_Unicode));
||||||| f013144d5
				m_undefinedConverters.Add(ConverterName, new EncoderInfo(ConverterName,
					ConverterType.ktypeTecKitTec, String.Empty, ConvType.Legacy_to_from_Unicode));
=======
				m_undefinedConverters.Add(ConverterName, new EncoderInfo(ConverterName,
					ConverterType.ktypeTecKitTec, string.Empty, ConvType.Legacy_to_from_Unicode));
>>>>>>> develop
				m_cnvtrPropertiesCtrl.txtName.Focus();
			}
		}

		/// <summary>
		/// Gets a unique name for the new converter.
		/// </summary>
		private int GetNewConverterName(out string strNewConvName)
		{
			// Get a unique name for the encoding converter
			for (var iConverter = 1; ; iConverter++)
			{
				strNewConvName = AddConverterDlgStrings.kstidNewConverterName + iConverter;
				if (!availableCnvtrsListBox.Items.Contains(strNewConvName))
				{
					availableCnvtrsListBox.Items.Add(strNewConvName);
					break;
				}
			}

			return availableCnvtrsListBox.Items.IndexOf(strNewConvName);
		}

		/// <summary>
		/// Sets the fields in the encoding converter for a Copy.
		/// </summary>
		private void SetFieldsForCopy()
		{
			var nameField = ConverterName;
			var nameFieldArray = nameField.ToCharArray();
			string newName; // name of the copied converter
			var copy = AddConverterDlgStrings.kstidCopy;

			//First we must figure out what newName will be
			if (nameField.Length >= 10 && string.Compare(" - " + copy + "(", 0, nameField, nameField.Length - 10, 8) == 0) // we're going to make the Xth copy
			{
<<<<<<< HEAD
				var nameStripped = nameField.Remove(nameField.Length - 3);
				var copyCount = nameFieldArray[nameField.Length - 2] - '0' + 1;
||||||| f013144d5
				string nameStripped = nameField.Remove(nameField.Length - 3);
				int copyCount = (int)nameFieldArray[nameField.Length - 2] - (int)'0' + 1;

=======
				var nameStripped = nameField.Remove(nameField.Length - 3);
				var copyCount = (int)nameFieldArray[nameField.Length - 2] - (int)'0' + 1;

>>>>>>> develop
				newName = nameStripped;
				newName += "(" + copyCount + ")";

				if (copyCount == 10)
				{
					ShowMessage(AddConverterDlgStrings.kstidNumerousCopiesMsg, AddConverterDlgStrings.kstidNumerousCopiesMade, MessageBoxButtons.OK);
				}
			}
			else if (nameField.Length >= 7 && string.Compare(" - " + copy, 0, nameField, nameField.Length - 7, 7) == 0)
			{
				// we're going to make the second copy
				newName = nameField;
				newName += "(2)";
			}
			else
			{
				// we're dealing with the original
				newName = nameField;
				newName += " - " + copy;
			}

			ConverterName = newName;
		}

<<<<<<< HEAD
||||||| f013144d5
		//private void SetFieldsBlank()
		//{
		//    ConverterName = String.Empty;
		//    cnvtrPropertiesCtrl.cboConverter.SelectedIndex = -1;
		//    cnvtrPropertiesCtrl.txtMapFile.Text = String.Empty;
		//    cnvtrPropertiesCtrl.cboSpec.SelectedIndex = -1;
		//    cnvtrPropertiesCtrl.cboConversion.SelectedIndex = -1;
		//}

		/// ------------------------------------------------------------------------------------
=======
		/// ------------------------------------------------------------------------------------
>>>>>>> develop
		/// <summary>
		/// Remove the encoding converter.
		/// </summary>
		internal void RemoveConverter(string converterToRemove)
		{
			// if the converter doesn't exist in the list of installed converters
			if (!m_encConverters.ContainsKey(converterToRemove))
			{
				// try to remove it from the invalid list of converters.
				m_undefinedConverters.Remove(converterToRemove);
				m_cnvtrPropertiesCtrl.RaiseListChanged();
				return;
			}

			// not sure if this will ever be hit, but let's still check
<<<<<<< HEAD
			if (string.IsNullOrEmpty(converterToRemove))
			{
||||||| f013144d5
			if (String.IsNullOrEmpty(converterToRemove))
=======
			if (string.IsNullOrEmpty(converterToRemove))
>>>>>>> develop
				return;
			}
			if (m_WSInUse == null || !m_WSInUse.Contains(converterToRemove))
			{
				m_encConverters.Remove(converterToRemove);
				m_cnvtrPropertiesCtrl.RaiseListChanged();
			}
			else // we did not remove the converter..it is probably in use somewhere.. go check :o)
			{
				ShowMessage(ResourceHelper.GetResourceString("kstidEncodingConverterInUseError"), ResourceHelper.GetResourceString("kstidEncodingConverterInUseErrorCaption"), MessageBoxButtons.OK);
			}
			SetUnchanged();
			SetStates();
		}

		/// <summary>
		/// Autosave the converter. If enough changes have been made to justify spending
		/// the time to save (actually it's an install), then we'll save
		/// </summary>
		/// <returns><c>false</c> if we must give the user a chance to change something</returns>
		internal bool AutoSave()
		{
			if (m_suppressAutosave || (CnvtrTypeComboItem)m_cnvtrPropertiesCtrl.cboConverter.SelectedItem == null)
			{
				return true;
			}

			m_suppressAutosave = true;

			try
			{
				// no changes made
				if (!m_cnvtrPropertiesCtrl.ConverterChanged)
				{
					return true;
				}
				// we should check the validity of all the fields
				switch (((CnvtrTypeComboItem)m_cnvtrPropertiesCtrl.cboConverter.SelectedItem).Type)
				{
					case ConverterType.ktypeRegEx:
						if (m_cnvtrPropertiesCtrl.m_specs == null ||    // LT-7098 m_specs can be null
							!m_cnvtrPropertiesCtrl.m_specs.Contains("->")) // invalid field
						{
							return UserDesiresDiscard(AddConverterDlgStrings.kstidNoFindReplaceSymbolSpecified, AddConverterDlgStrings.kstidInvalidRegularExpression);
						}
						if (m_cnvtrPropertiesCtrl.m_specs.Substring(0, 2) == "->") // no 'find' term to search for
						{
							ShowMessage(AddConverterDlgStrings.kstidFindReplaceWarningMsg, 								AddConverterDlgStrings.FindReplaceWarning, MessageBoxButtons.OK);
						}
						break;
					case ConverterType.ktypeCodePage:
						if (m_cnvtrPropertiesCtrl.cboSpec.SelectedIndex == -1)
						{
							return UserDesiresDiscard(AddConverterDlgStrings.kstidNoCodePage, AddConverterDlgStrings.kstidInvalidCodePage);
						}
						break;
					case ConverterType.ktypeIcuConvert:
					case ConverterType.ktypeIcuTransduce:
						if (m_cnvtrPropertiesCtrl.cboSpec.SelectedIndex == -1)
						{
							return UserDesiresDiscard(AddConverterDlgStrings.kstidInvalidMappingFileNameMsg, AddConverterDlgStrings.kstidInvalidMappingName);
						}
						break;
					default:
<<<<<<< HEAD
						if (string.IsNullOrEmpty(m_cnvtrPropertiesCtrl.m_specs) ||  // LT-7098 m_specs can be null
							string.IsNullOrEmpty(m_cnvtrPropertiesCtrl.m_specs.Trim())) // null field
||||||| f013144d5
						if (String.IsNullOrEmpty(m_cnvtrPropertiesCtrl.m_specs) ||	// LT-7098 m_specs can be null
							String.IsNullOrEmpty(m_cnvtrPropertiesCtrl.m_specs.Trim())) // null field
=======
						if (string.IsNullOrEmpty(m_cnvtrPropertiesCtrl.m_specs) ||	// LT-7098 m_specs can be null
							string.IsNullOrEmpty(m_cnvtrPropertiesCtrl.m_specs.Trim())) // null field
>>>>>>> develop
						{
							return UserDesiresDiscard(AddConverterDlgStrings.kstidInvalidMappingFileMsg, AddConverterDlgStrings.kstidInvalidMappingFile);
						}
						if (!File.Exists(m_cnvtrPropertiesCtrl.m_specs.Trim())) // file in m_spec does not exist
						{
							return UserDesiresDiscard(AddConverterDlgStrings.kstidNoMapFileFound, AddConverterResources.kstrMapFileNotFoundTitle);
						}
						break;
				}

<<<<<<< HEAD
				if (m_cnvtrPropertiesCtrl.cboConverter.SelectedIndex == -1 || m_cnvtrPropertiesCtrl.cboConversion.SelectedIndex == -1)
||||||| f013144d5
				if (m_cnvtrPropertiesCtrl.cboConverter.SelectedIndex == -1 ||
					m_cnvtrPropertiesCtrl.cboConversion.SelectedIndex == -1)
					return false; // all fields must be filled out (not sure if this ever occurs anymore)

				if (String.IsNullOrEmpty(ConverterName)) // no name provided
=======
				if (m_cnvtrPropertiesCtrl.cboConverter.SelectedIndex == -1 ||
					m_cnvtrPropertiesCtrl.cboConversion.SelectedIndex == -1)
				{
					MessageBoxUtils.Show(this, AddConverterDlgStrings.kstrErrorInProperties, AddConverterDlgStrings.kstrUnspecifiedSaveError);
					return true; // all fields must be filled out (not sure if this ever occurs anymore)
				}

				if (string.IsNullOrEmpty(ConverterName)) // no name provided
>>>>>>> develop
				{
					return false; // all fields must be filled out (not sure if this ever occurs anymore)
				}
				if (string.IsNullOrEmpty(ConverterName)) // no name provided
				{
					return UserDesiresDiscard(AddConverterDlgStrings.kstidNoNameMsg, AddConverterDlgStrings.kstidNoName);
				}

				// This begins the actual "save" operation
				if (m_oldConverter != ConverterName)
				{
					// uhg! They changed the converter name. So we're going to check if that name is acceptable,
					// then we'll remove the old converter before we perform the install
					if (AbortInstallDueToOverwrite())
					{
						return false;
					}
					RemoveConverter(m_oldConverter);
				}
<<<<<<< HEAD
				var installState = InstallConverter(); // save changes made
||||||| f013144d5
				bool installState = InstallConverter(); // save changes made

=======
				var installState = InstallConverter(); // save changes made

>>>>>>> develop
				SetUnchanged();
				return installState;
			}
			catch (Exception e)
			{
				ShowMessage(string.Format(AddConverterDlgStrings.kstrUnhandledConverterException, e.Message),
					AddConverterDlgStrings.kstrUnspecifiedSaveError, MessageBoxButtons.OK);
				// return true to allow closing the dialog when we encounter an unexpected error
				return true;
			}
			finally
			{
				m_suppressAutosave = false;
			}
		}

		/// <summary>
		/// Set that the fields have not been changed by the user since the last load/save.
		/// </summary>
		internal void SetUnchanged()
		{
			m_oldConverter = ConverterName; // store this name, so we can delete the EC if we rename it
			m_cnvtrPropertiesCtrl.ConverterChanged = false;
			if (SelectedConverterIndex != -1)
			{
				m_currentlyAdding = false;
			}
		}
		#endregion

		/// <summary>
		/// Informs this control of the writing systems which should not be deleted.
		/// </summary>
		internal void InitWSInUse(ISet<string> wsInUse)
		{
			m_WSInUse = wsInUse;
		}

		/// <summary>
		/// Check if an overwrite could occur and if the user wants to abort the overwrite.
		/// </summary>
		/// <returns>True if we should not perform the install.</returns>
		private bool AbortInstallDueToOverwrite()
		{
			// Overwrite Existing EC: I am hesitant to put this in with any other method,
			// because we always want the user's permission to overwrite... also, if they
			// alter the txtName or cboConverter, warnings are excessive if you put this
			// into the autosave validity check, but are lacking if you put it into
			// InstallConverter()  -- CameronB
			if (m_encConverters.ContainsKey(ConverterName))
			{
				if (ShowMessage(AddConverterDlgStrings.kstidExistingConvMsg, AddConverterResources.kstrOverwriteTitle, MessageBoxButtons.OKCancel) == DialogResult.Cancel)
				{
					m_suppressListBoxIndexChanged = true;
					SelectedConverter = m_oldConverter;
					m_suppressListBoxIndexChanged = false;
					// If we return true: the user does not want to perform an overwrite
					// of an existing converter. SO... DO NOT perform the install!!!
					return true;
				}
			}
			// If we return false: either an overwrite is not possible, or the user wants
			// to proceed with the overwrite... So go ahead and install.
			return false;
		}

		/// <summary>
		/// Try to install ("add" or "save") the converter.
		/// </summary>
		/// <returns>True if the converter was installed, false otherwise</returns>
		internal bool InstallConverter()
		{
<<<<<<< HEAD
			if (string.IsNullOrEmpty(ConverterName))
			{
||||||| f013144d5
			CheckDisposed();

			if (String.IsNullOrEmpty(ConverterName))
=======
			CheckDisposed();

			if (string.IsNullOrEmpty(ConverterName))
>>>>>>> develop
				return false; // do not add a null converter, even if the warning has been suppressed
			}
			// We MUST remove it if it already exists (otherwise, we may get duplicates in rare cases)
			// duplicates could occur because the "key" is the name + Converter Type
			RemoveConverter(ConverterName);

			var ct = ((CnvtrDataComboItem)m_cnvtrPropertiesCtrl.cboConversion.SelectedItem).Type;
			var impType = ((CnvtrTypeComboItem)m_cnvtrPropertiesCtrl.cboConverter.SelectedItem).ImplementType;
			var processType = ProcessTypeFlags.DontKnow;
			switch (((CnvtrTypeComboItem)m_cnvtrPropertiesCtrl.cboConverter.SelectedItem).Type)
			{
				case ConverterType.ktypeCC:
				case ConverterType.ktypeTecKitTec:
				case ConverterType.ktypeTecKitMap:
					switch (ct)
					{
						case ConvType.Legacy_to_from_Legacy:
						case ConvType.Legacy_to_Legacy:
							processType = ProcessTypeFlags.NonUnicodeEncodingConversion;
							break;
						case ConvType.Unicode_to_from_Unicode:
						case ConvType.Unicode_to_Unicode:
							processType = ProcessTypeFlags.Transliteration;
							break;
						default:
							processType = ProcessTypeFlags.UnicodeEncodingConversion;
							break;
					}
					break;
				case ConverterType.ktypeCodePage:
					processType = ProcessTypeFlags.CodePageConversion;
					break;
				case ConverterType.ktypeIcuConvert:
					processType = ProcessTypeFlags.ICUConverter;
					break;
				case ConverterType.ktypeIcuTransduce:
					processType = ProcessTypeFlags.ICUTransliteration;
					break;
			}
			try
			{
				m_encConverters.AddConversionMap(ConverterName, m_cnvtrPropertiesCtrl.m_specs.Trim(), ct, impType, string.Empty, string.Empty, processType);
			}
			catch (ECException exception)
			{
				// Catch an invalid character in the EC name, or other improper install message
				return UserDesiresDiscard(exception.Message, AddConverterResources.kstrEcExceptionTitle);
			}
			catch (System.Runtime.InteropServices.COMException comEx)
			{
				// Possibly an ICU related COM exception.  Now if we only knew what to do with it...
				// When we get the U_FILE_ACCESS_ERROR, it seems like we need to restart the ICU
				// functions, but to do that now would be of great risk.  The only way we currently have
				// is to restart the application.  Hmmmm???
				// Also seems like the converter is 'lost' when this happens .. hmmm???
				Debug.WriteLine("=====COMException in AddCnvtrDlg.cs: " + comEx.Message);
<<<<<<< HEAD
				MessageBox.Show(string.Format(AddConverterDlgStrings.kstidICUErrorText,
					Environment.NewLine, m_app.ApplicationName), AddConverterDlgStrings.kstidICUErrorTitle,
||||||| f013144d5
				MessageBox.Show(String.Format(AddConverterDlgStrings.kstidICUErrorText,
					Environment.NewLine, m_app.ApplicationName), AddConverterDlgStrings.kstidICUErrorTitle,
=======
				MessageBox.Show(string.Format(AddConverterDlgStrings.kstidICUErrorText,
					Environment.NewLine, m_app?.ApplicationName), AddConverterDlgStrings.kstidICUErrorTitle,
>>>>>>> develop
					MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
			catch (Exception ex)
			{
				var sb = new StringBuilder(ex.Message);
				sb.Append(Environment.NewLine);
				sb.Append(FwCoreDlgs.kstidErrorAccessingEncConverters);
				MessageBox.Show(this, sb.ToString(), ResourceHelper.GetResourceString("kstidCannotModifyWS"));
				return true;
			}

			// Remove the item from the temporary list since it is now a valid converter.
			m_undefinedConverters.Remove(ConverterName);

			m_cnvtrPropertiesCtrl.RaiseListChanged();
			SelectedConverter = ConverterName;
			return true;
		}

		/// <summary>
		/// Notify the user about a problem with the current encoding converter (unless the user
		/// has clicked Add or changed the name).
		/// </summary>
		/// <param name="sMessage">Reason why allowing the user to discard</param>
		/// <param name="sTitle">Title</param>
		/// <returns><c>true</c> if the user wants to discard the changes, <c>false</c>
		/// otherwise</returns>
		private bool UserDesiresDiscard(string sMessage, string sTitle)
		{
			m_fDiscardingChanges = true;

			// This is very ugly, but here we want to suppress an error dialog if the user
			// has clicked Add, changed the name, (may or may not have looked through the
			// Converter Type list) and then clicked More... --CameronB
			if (m_currentlyAdding && m_oldConverter != ConverterName && sTitle == AddConverterDlgStrings.kstidInvalidMappingFile && m_transduceDialogOpen)
			{
				// discard all changes made and go to the currently selected item
				m_suppressAutosave = true;
				SetFieldsForAdd();
				SetUnchanged();
				m_suppressAutosave = false;
				RefreshTabs();
				m_fDiscardingChanges = false;
				return true;
			}

			// If selected converter is not defined, and the user did not select a different converter,
			// attempt to add another converter or close the dialog (that is, if they are trying to
			// go to the Test or Advanced tab)...
			if (m_undefinedConverters.ContainsKey(SelectedConverter) && !m_suppressListBoxIndexChanged && !m_currentlyAdding && !m_fClosingDialog)
			{
				// don't offer the option to cancel.
<<<<<<< HEAD
				ShowMessage(string.Format(AddConverterDlgStrings.kstidInvalidConverterNotify, sMessage), sTitle, MessageBoxButtons.OK);
||||||| f013144d5
				ShowMessage(String.Format(AddConverterDlgStrings.kstidInvalidConverterNotify, sMessage),
					sTitle, MessageBoxButtons.OK);
=======
				ShowMessage(string.Format(AddConverterDlgStrings.kstidInvalidConverterNotify, sMessage),
					sTitle, MessageBoxButtons.OK);
>>>>>>> develop
			}
			else
			{
<<<<<<< HEAD
				var result = ShowMessage(string.Format(AddConverterDlgStrings.kstidDiscardChangesConfirm, sMessage), sTitle, MessageBoxButtons.OKCancel);
||||||| f013144d5
				DialogResult result = ShowMessage(
					String.Format(AddConverterDlgStrings.kstidDiscardChangesConfirm, sMessage),
					sTitle, MessageBoxButtons.OKCancel);

=======
				var result = ShowMessage(
					string.Format(AddConverterDlgStrings.kstidDiscardChangesConfirm, sMessage),
					sTitle, MessageBoxButtons.OKCancel);

>>>>>>> develop
				if (result == DialogResult.Cancel)
				{
					// If the user made a change to an existing, installed converter that
					// made it invalid, we don't want to remove it when they click "Cancel".
					if (m_undefinedConverters.ContainsKey(m_oldConverter))
					{
						// The user wants to cancel this invalid encoding converter.
						// Remove it from the list and continue.
						RemoveConverter(m_oldConverter);
					}

					// discard all changes made and go to the currently selected item
					m_suppressAutosave = true;
					SetFieldsForAdd();
					SetUnchanged();
					m_suppressAutosave = false;
					RefreshTabs();
					m_fDiscardingChanges = false;
					return true;
				}
<<<<<<< HEAD
				// DialogResult.OK
				if (m_currentlyAdding)
				{
					SelectedConverterIndex = -1; // let them keep working
				}
				else
				{
					SelectedConverterZeroDefault = m_oldConverter;
				}
||||||| f013144d5
				else // DialogResult.OK
				{
					if (m_currentlyAdding)
						SelectedConverterIndex = -1; // let them keep working
					else
						SelectedConverterZeroDefault = m_oldConverter;
				}
=======
				// DialogResult.OK
				if (m_currentlyAdding)
					SelectedConverterIndex = -1; // let them keep working
				else
					SelectedConverterZeroDefault = m_oldConverter;
>>>>>>> develop
			}

			m_fDiscardingChanges = false;
			return false;
		}

		/// <summary>
		/// Override this for testing without UI
		/// </summary>
		protected virtual DialogResult ShowMessage(string sMessage, string sTitle, MessageBoxButtons buttons)
		{
			Debug.WriteLine("MESSAGE: " + sMessage);
			return MessageBoxUtils.Show(this, sMessage, sTitle, buttons);
		}

		/// <summary>
		/// Launches the add transduce processor dialog.
		/// </summary>
		internal void launchAddTransduceProcessorDlg()
		{
			m_transduceDialogOpen = true;
			m_suppressAutosave = true;
			m_cnvtrPropertiesCtrl.EnableEntirePane(false);
			// save the current converter
			var selectedConverter = ConverterName;

			try
			{
				// call the v2.2 interface to "AutoConfigure" a converter
<<<<<<< HEAD
				var strFriendlyName = selectedConverter;
				var aEC = new EncConverters();
				aEC.AutoConfigure(ConvType.Unknown, ref strFriendlyName);
||||||| f013144d5
				string strFriendlyName = selectedConverter;
				EncConverters aEC = new EncConverters();
				aEC.AutoConfigure(ConvType.Unknown, ref strFriendlyName);

=======
				var strFriendlyName = selectedConverter;
				var encConverters = new EncConverters();
				encConverters.AutoConfigure(ConvType.Unknown, ref strFriendlyName);

>>>>>>> develop
				m_outsideDlgChangedCnvtrs = true;
<<<<<<< HEAD
				if (!string.IsNullOrEmpty(strFriendlyName) && strFriendlyName != selectedConverter)
||||||| f013144d5

				if (!String.IsNullOrEmpty(strFriendlyName) && strFriendlyName != selectedConverter)
=======

				if (!string.IsNullOrEmpty(strFriendlyName) && strFriendlyName != selectedConverter)
>>>>>>> develop
				{
					m_undefinedConverters.Remove(selectedConverter);
					RefreshListBox();
					SelectedConverter = strFriendlyName;
				}
				else
				{
					SelectedConverter = selectedConverter;
				}

				RefreshTabs();
				SetStates();
			}
			finally
			{
				m_transduceDialogOpen = false;
				m_suppressAutosave = false;
			}
		}

		/// <summary>
		/// Sets the enabled states for the buttons.
		/// </summary>
		private void SetStates()
		{
			// Set button states
			btnCopy.Enabled = SelectedConverterIndex != -1 && m_cnvtrPropertiesCtrl.m_supportedConverter && !m_undefinedConverters.ContainsKey(SelectedConverter);
			btnDelete.Enabled = SelectedConverterIndex != -1;

			// Set pane states
			m_cnvtrPropertiesCtrl.SetStates(availableCnvtrsListBox.Items.Count != 0, IsConverterInstalled);
		}
	}
<<<<<<< HEAD
}
||||||| f013144d5

	#region Encoder information
	internal class EncoderInfo
	{
		/// <summary>The name of the encoding converter.</summary>
		public string m_name = string.Empty;
		/// <summary>The converter method, e.g. CC table, TecKit, etc.</summary>
		public ConverterType m_method;
		/// <summary>Name of the file containing the conversion table, etc.</summary>
		public string m_fileName = string.Empty;
		/// <summary>Type of conversion, e.g. from legacy to Unicode.</summary>
		public ConvType m_fromToType;

		/// --------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="T:EncoderInfo"/> class.
		/// </summary>
		/// <param name="name">The name of the encoding converter.</param>
		/// <param name="method">The method, e.g. CC table, TecKit, etc.</param>
		/// <param name="fileName">Name of the file containing the conversion table, etc.</param>
		/// <param name="fromToType">Type of conversion, e.g. from legacy to Unicode.</param>
		/// --------------------------------------------------------------------------------
		public EncoderInfo(string name, ConverterType method, string fileName, ConvType fromToType)
		{
			m_name = name;
			m_method = method;
			m_fileName = fileName;
			m_fromToType = fromToType;
		}
	}
	#endregion
}
=======

	#region Encoder information
	internal class EncoderInfo
	{
		/// <summary>The name of the encoding converter.</summary>
		public string m_name;
		/// <summary>The converter method, e.g. CC table, TecKit, etc.</summary>
		public ConverterType m_method;
		/// <summary>Name of the file containing the conversion table, etc.</summary>
		public string m_fileName;
		/// <summary>Type of conversion, e.g. from legacy to Unicode.</summary>
		public ConvType m_fromToType;

		/// --------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="T:EncoderInfo"/> class.
		/// </summary>
		/// <param name="name">The name of the encoding converter.</param>
		/// <param name="method">The method, e.g. CC table, TecKit, etc.</param>
		/// <param name="fileName">Name of the file containing the conversion table, etc.</param>
		/// <param name="fromToType">Type of conversion, e.g. from legacy to Unicode.</param>
		/// --------------------------------------------------------------------------------
		public EncoderInfo(string name, ConverterType method, string fileName, ConvType fromToType)
		{
			m_name = name;
			m_method = method;
			m_fileName = fileName;
			m_fromToType = fromToType;
		}
	}
	#endregion
}
>>>>>>> develop
