// Copyright (c) 2010-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.FwCoreDlgs.FileDialog;
using SIL.FieldWorks.Resources;
using SIL.LCModel.DomainServices.BackupRestore;
using SIL.LCModel.Utils;
using SIL.Reporting;

namespace SIL.FieldWorks.FwCoreDlgs.BackupRestore
{
	/// <summary />
	internal sealed partial class RestoreProjectDlg : Form
	{
		/// <summary>
		/// Performs the requested actions and handles any IO or zip error by reporting them to
		/// the user. (Intended for operations that deal directly with a backup zip file.
		/// </summary>
		/// <param name="parentWindow">The parent window to use when reporting an error (can be
		/// null).</param>
		/// <param name="zipFilename">The backup zip filename.</param>
		/// <param name="action">The action to perform.</param>
		/// <returns>
		/// 	<c>true</c> if successful (no exception caught); <c>false</c> otherwise
		/// </returns>
		internal static bool HandleRestoreFileErrors(IWin32Window parentWindow, string zipFilename, Action action)
		{
			try
			{
				action();
			}
			catch (Exception error)
			{
				if (error is IOException || error is InvalidBackupFileException ||
					error is UnauthorizedAccessException)
				{
					Logger.WriteError(error);
					MessageBoxUtils.Show(parentWindow, error.Message, "FLEx", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return false;
				}
				throw;
			}
			return true;
		}

		#region Data members
		private readonly RestoreProjectPresenter m_presenter;
		private readonly string m_fmtUseOriginalName;
		private IOpenFileDialog m_openFileDlg;

		#endregion

		#region Constructors

		/// <summary />
		private RestoreProjectDlg()
		{
			InitializeComponent();
		}

		/// <summary />
		/// <param name="backupFileSettings">Specific backup file settings to use (dialog
		/// controls to select a backup file will be disabled)</param>
		/// <param name="helpTopicProvider">The help topic provider.</param>
		internal RestoreProjectDlg(BackupFileSettings backupFileSettings, IHelpTopicProvider helpTopicProvider)
			: this(helpTopicProvider)
		{
			m_lblBackupZipFile.Text = backupFileSettings.File;
			m_presenter = new RestoreProjectPresenter(this);
			BackupFileSettings = backupFileSettings;
			m_rdoDefaultFolder.Enabled = m_btnBrowse.Enabled = false;
			m_rdoAnotherLocation.Checked = true;
			SetOriginalNameFromSettings();
		}

		/// <summary />
		internal RestoreProjectDlg(string defaultProjectName, IHelpTopicProvider helpTopicProvider)
			: this(helpTopicProvider)
		{
			m_presenter = new RestoreProjectPresenter(this, defaultProjectName);
			m_rdoDefaultFolder_CheckedChanged(null, null);
			PopulateProjectList(m_presenter.DefaultProjectName);
		}

		/// <summary />
		private RestoreProjectDlg(IHelpTopicProvider helpTopicProvider)
			: this()
		{
			HelpTopicProvider = helpTopicProvider;
			m_lblOtherBackupIncludes.Text = string.Empty;
			m_lblDefaultBackupIncludes.Text = string.Empty;
			m_lblBackupZipFile.Text = string.Empty;
			m_lblBackupProjectName.Text = string.Empty;
			m_lblBackupDate.Text = string.Empty;
			m_lblBackupComment.Text = string.Empty;
			m_fmtUseOriginalName = m_rdoUseOriginalName.Text;
			m_rdoUseOriginalName.Text = string.Format(m_fmtUseOriginalName, string.Empty);
			Settings = new RestoreProjectSettings(FwDirectoryFinder.ProjectsDirectory);
			m_txtOtherProjectName.KeyPress += m_txtOtherProjectName_KeyPress;
			m_txtOtherProjectName.TextChanged += m_txtOtherProjectName_TextChanged;
			GetIllegalProjectNameChars();
		}

		private static void GetIllegalProjectNameChars()
		{
			MiscUtils.GetInvalidProjectNameChars(MiscUtils.FilenameFilterStrength.kFilterProjName).ToCharArray();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the restore settings.
		/// </summary>
		internal RestoreProjectSettings Settings { get; }

		/// <summary>
		/// Path of the zip file that the user chose which contains a FieldWorks project backup.
		/// </summary>
		private string BackupZipFile
		{
			get => m_lblBackupZipFile.Text;
			set
			{
				if (m_lblBackupZipFile.Text != value)
				{
					m_lblBackupZipFile.Text = value;
					OnBackupVersionChosen();
				}
			}
		}

		/// <summary>
		/// This is the name of the project to restore as.
		/// </summary>
		private string TargetProjectName
		{
			get => m_rdoUseOriginalName.Checked ? Settings.Backup.ProjectName : m_txtOtherProjectName.Text.Normalize();
			set
			{
				if (value == Settings.Backup.ProjectName)
				{
					m_rdoUseOriginalName.Checked = true;
				}
				else
				{
					m_rdoRestoreToName.Checked = true;
					m_txtOtherProjectName.Text = value;
				}
				Settings.ProjectName = value;
			}
		}

		///<summary>
		/// Whether or not user wants field visibilities, columns, dictionary layout, interlinear etc
		/// settings in the backup zipfile restored.
		///</summary>
		private bool ConfigurationSettings => m_configurationSettings.Checked;

		///<summary>
		/// Whether or not user wants externally linked files (pictures, media and other) files in the backup zipfile restored.
		///</summary>
		private bool LinkedFiles => m_linkedFiles.Checked;

		///<summary>
		/// Whether or not user wants files in the SupportingFiles folder in the backup zipfile restored.
		///</summary>
		private bool SupportingFiles => m_supportingFiles.Checked;

		///<summary>
		/// Whether or not user wants spell checking additions in the backup.
		///</summary>
		private bool SpellCheckAdditions => m_spellCheckAdditions.Checked;

		/// <summary>
		/// Sets the backup file settings and updates the dialog controls accordingly
		/// </summary>
		private BackupFileSettings BackupFileSettings
		{
			set
			{
				Settings.Backup = value;
				var suggestedNewProjName = value.ProjectName; // TODO: m_presenter.GetSuggestedNewProjectName(value);
				SetDialogControlsFromBackupSettings(value, suggestedNewProjName);
			}
		}

		///<summary>
		/// Gets the HelpTopicProvider (for use by OverwriteExistingProject dialog).
		///</summary>
		internal IHelpTopicProvider HelpTopicProvider { get; }

		#endregion

		#region Event handlers
		private void m_rdoRestoreToName_CheckedChanged(object sender, EventArgs e)
		{
			m_txtOtherProjectName.Enabled = m_rdoRestoreToName.Checked;
			m_presenter.EmptyProjectName = false;
			if (m_txtOtherProjectName.Enabled && m_txtOtherProjectName.Text == String.Empty)
			{
				m_txtOtherProjectName.Text = m_presenter.GetSuggestedNewProjectName();
			}
		}

		private void m_btnBrowse_Click(object sender, EventArgs e)
		{
			if (m_openFileDlg == null)
			{
				m_openFileDlg = new OpenFileDialogAdapter();
			}
			m_openFileDlg.CheckFileExists = true;
			m_openFileDlg.InitialDirectory = FwDirectoryFinder.DefaultBackupDirectory;
			m_openFileDlg.RestoreDirectory = true;
			m_openFileDlg.Title = FwCoreDlgs.ksFindBackupFileDialogTitle;
			m_openFileDlg.ValidateNames = true;
			m_openFileDlg.Multiselect = false;
			m_openFileDlg.Filter = ResourceHelper.BuildFileFilter(FileFilterType.FieldWorksAllBackupFiles, FileFilterType.XML);
			if (m_openFileDlg.ShowDialog(this) == DialogResult.OK)
			{
				//In the presentation layer:
				//1) Verify that the file selected for restore is a valid FieldWorks backup file
				//and take appropriate action.
				//1a) if not then inform the user they need to select another file.
				//1b) if it is valid then we need to set the various other controls in this dialog to be active
				//and give the user the option of selecting things they can restore optionally. If something like SupportingFiles
				//was not included in the backup then we grey out that control and uncheck it.
				BackupZipFile = m_openFileDlg.FileName;
				m_openFileDlg.InitialDirectory = Path.GetDirectoryName(m_openFileDlg.FileName);
			}
		}

		/// <summary>
		/// Called when a backup version is chosen either by browsing to a zip file.
		/// </summary>
		private void OnBackupVersionChosen()
		{
			m_rdoUseOriginalName.Text = string.Format(m_fmtUseOriginalName, string.Empty);
			m_txtOtherProjectName.Text = string.Empty;
			Settings.Backup = null;
			if (string.IsNullOrEmpty(BackupZipFile))
			{
				EnableDisableDlgControlsForRestore(false);
				return;
			}

			if (HandleRestoreFileErrors(this, BackupZipFile, () => BackupFileSettings = new BackupFileSettings(BackupZipFile, true)))
			{
				SetOriginalNameFromSettings();
			}
			else
			{
				EnableDisableDlgControlsForRestore(false);
			}
		}

		/// <summary>
		/// Sets the original name label from the backup settings.
		/// </summary>
		private void SetOriginalNameFromSettings()
		{
			m_txtOtherProjectName.Text = string.Empty;
			m_rdoUseOriginalName.Text = string.Format(m_fmtUseOriginalName, Settings.Backup.ProjectName);
		}

		/// <summary>
		/// Enables or disables controls.
		/// </summary>
		private void EnableDisableDlgControlsForRestore(bool enable)
		{
			if (!enable)
			{
				m_rdoUseOriginalName.Text = string.Format(m_fmtUseOriginalName, string.Empty);
				m_txtOtherProjectName.Text = string.Empty;
			}
			m_btnOk.Enabled = enable;
			m_gbRestoreAs.Enabled = enable;
			m_gbAlsoRestore.Enabled = enable;
		}

		/// <summary>
		/// Sets the dialog controls from backup settings.
		/// </summary>
		private void SetDialogControlsFromBackupSettings(BackupFileSettings settings, String suggestedNewProjectName)
		{
			EnableDisableDlgControlsForRestore(true);
			TargetProjectName = suggestedNewProjectName;
			m_configurationSettings.Checked = settings.IncludeConfigurationSettings;
			m_configurationSettings.Enabled = settings.IncludeConfigurationSettings;
			// If the settings file does not contain enough information for restoring the
			// linked files, then just disable the option. (FWR-2245)
			m_linkedFiles.Checked = settings.LinkedFilesAvailable;
			m_linkedFiles.Enabled = settings.LinkedFilesAvailable;
			m_supportingFiles.Checked = settings.IncludeSupportingFiles;
			m_supportingFiles.Enabled = settings.IncludeSupportingFiles;
			m_spellCheckAdditions.Checked = settings.IncludeSpellCheckAdditions;
			m_spellCheckAdditions.Enabled = settings.IncludeSpellCheckAdditions;
			if (m_rdoDefaultFolder.Checked)
			{
				m_lblDefaultBackupIncludes.Text = m_presenter.IncludesFiles(settings);
			}
			else
			{
				m_lblBackupProjectName.Text = settings.ProjectName;
				m_lblBackupDate.Text = settings.BackupTime.ToString();
				m_lblBackupComment.Text = settings.Comment;
				m_lblOtherBackupIncludes.Text = m_presenter.IncludesFiles(settings);
			}
			SetOriginalNameFromSettings();
		}



		private void m_btnHelp_Click(object sender, EventArgs e)
		{
			ShowHelp.ShowHelpTopic(HelpTopicProvider, "khtpRestoreProjectDlg");
		}

		private void m_btnOk_Click(object sender, EventArgs e)
		{
			UpdateSettingsFromControls();

			using (new WaitCursor(this))
			{
				if (m_presenter.IsOkayToRestoreProject())
				{
					//If the project restored was the one currently running we need
					//to restart FW with that project.
					DialogResult = DialogResult.OK;
					Close();
				}
				else
				{
					if (m_presenter.NewProjectNameAlreadyExists)
					{
						m_txtOtherProjectName.Select();
						m_txtOtherProjectName.SelectAll();
					}
				}
			}
		}

		internal void EnableOKBtn(bool enable)
		{
			m_btnOk.Enabled = enable;
		}

		/// <summary>
		/// Handles the CheckedChanged event of the m_rdoDefaultFolder control.
		/// </summary>
		private void m_rdoDefaultFolder_CheckedChanged(object sender, EventArgs e)
		{
			if (m_rdoDefaultFolder.Checked)
			{
				m_pnlDefaultBackupFolder.Visible = true;
				m_pnlAnotherLocation.Visible = false;
				m_pnlDefaultBackupFolder.Dock = DockStyle.Fill;
				m_pnlAnotherLocation.Dock = DockStyle.None;
				m_btnBrowse.Enabled = false;
				if (m_lstVersions.SelectedItems.Count > 0)
				{
					BackupFileSettings = (BackupFileSettings)m_lstVersions.SelectedItems[0].Tag;
				}
				else
				{
					EnableDisableDlgControlsForRestore(false);
				}
			}
			else
			{
				m_pnlDefaultBackupFolder.Visible = false;
				m_pnlAnotherLocation.Visible = true;
				m_pnlDefaultBackupFolder.Dock = DockStyle.None;
				m_pnlAnotherLocation.Dock = DockStyle.Fill;
				m_btnBrowse.Enabled = true;
				OnBackupVersionChosen();
			}
		}

		/// <summary>
		/// Handles the SelectedIndexChanged event of the m_cboProjects control.
		/// </summary>
		private void m_cboProjects_SelectedIndexChanged(object sender, EventArgs e)
		{
			var selectedProject = (string)m_cboProjects.SelectedItem;
			if (selectedProject == null)
			{
				return; // Hopefully this is just temporary from clearing out the list.
			}
			m_lstVersions.BeginUpdate();
			m_lstVersions.Items.Clear();
			foreach (var backupDate in m_presenter.BackupRepository.GetAvailableVersions(selectedProject))
			{
				// We have to ensure that at least the first one is valid because we're going to make it
				// the default
				var backupFile = m_presenter.BackupRepository.GetBackupFile(selectedProject, backupDate, (m_lstVersions.Items.Count == 0));
				if (backupFile != null)
				{
					var newItem = new ListViewItem(new[] { backupDate.ToString(), backupFile.Comment })
					{
						Tag = backupFile
					};
					m_lstVersions.Items.Add(newItem);
				}
			}

			m_lstVersions.EndUpdate();

			// ENHANCE: If there are no available versions for the selected project, we should
			// probably say so.
			if (m_lstVersions.Items.Count > 0)
			{
				m_lstVersions.SelectedIndices.Add(0);
			}
		}

		/// <summary>
		/// Handles the SelectedIndexChanged event of the m_lstVersions control.
		/// </summary>
		private void m_lstVersions_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (m_lstVersions.SelectedIndices.Count == 0)
			{
				EnableDisableDlgControlsForRestore(false);
				return; // Hopefully this is just temporary from clearing out the list.
			}

			try
			{
				BackupFileSettings = (BackupFileSettings)m_lstVersions.SelectedItems[0].Tag;
			}
			catch (Exception error)
			{
				// When the user selects a backup file which is invalid as a FieldWorks backup
				// checks are done on the file when creating the BackupFileSettings and
				// an exception is thrown which needs to be handled.
				if (error is IOException || error is InvalidBackupFileException || error is UnauthorizedAccessException)
				{
					Logger.WriteError(error);
					MessageBox.Show(null, error.Message, ResourceHelper.GetResourceString("ksRestoreFailed"), MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
				throw;
			}
		}

		/// <summary>
		/// Adjust column widths when the listview resizes.
		/// </summary>
		private void m_lstVersions_SizeChanged(object sender, EventArgs e)
		{
			colComment.Width = m_lstVersions.ClientSize.Width - colDate.Width;
		}

		/// <summary>
		/// Handles the TextChanged event for the Other Project name text box.
		/// </summary>
		private void m_txtOtherProjectName_TextChanged(object sender, EventArgs e)
		{
			UpdateSettingsFromControls();
		}

		/// <summary>
		/// Routine to eliminate illegal characters from being entered as part of a Project filename.
		/// Note that we allow backups with existing illegal (Unicode) characters to be restored under the same name (or [name]-01).
		/// TODO (Hasso) 2019.05: prevent pasting illegal characters (Ctrl-V and right-click)--must use TextChanged or similar (LT-19712)
		/// </summary>
		private void m_txtOtherProjectName_KeyPress(object sender, KeyPressEventArgs e)
		{
			switch ((int)e.KeyChar)
			{
				case (int)Keys.Back: // Backspace
				case 26: // Ctrl-Z (undo)
				case 25: // Ctrl-Y (redo)
				case 24: // Ctrl-X (cut)
				case 22: // Ctrl-V (paste)
				case 3: // Ctrl-C (copy)
				case 1: // Ctrl-A (select all)
					return;
			}
			var character = e.KeyChar.ToString();
			if (!FwNewLangProjectModel.CheckForSafeProjectName(ref character, out var errorMessage))
			{
				MessageBox.Show(errorMessage, FwCoreDlgs.FwProjProperties_PickDifferentProjName, MessageBoxButtons.OK, MessageBoxIcon.Error);
				e.Handled = true; // This will cause the character NOT to be entered.
			}
		}

		#endregion

		#region Private helper methods

		/// <summary>
		/// Updates the settings from controls.
		/// </summary>
		private void UpdateSettingsFromControls()
		{
			Settings.ProjectName = TargetProjectName;
			m_presenter.EmptyProjectName = Settings.ProjectName.Length == 0;

			//What to restore
			Settings.IncludeConfigurationSettings = ConfigurationSettings;
			Settings.IncludeLinkedFiles = LinkedFiles;
			Settings.IncludeSupportingFiles = SupportingFiles;
			Settings.IncludeSpellCheckAdditions = SpellCheckAdditions;
		}

		/// <summary>
		/// Populates the project list.
		/// </summary>
		private void PopulateProjectList(string sDefaultProjectName)
		{
			m_cboProjects.BeginUpdate();
			m_cboProjects.Items.Clear();
			foreach (var projectName in m_presenter.BackupRepository.AvailableProjectNames)
			{
				m_cboProjects.Items.Add(projectName);
			}
			m_cboProjects.EndUpdate();
			m_cboProjects.SelectedItem = sDefaultProjectName;

			if (m_cboProjects.Items.Count == 0)
			{
				var nada = new Label
				{
					Text = Properties.Resources.kstidNoProjectBackupsFound,
					Left = m_cboProjects.Left,
					Top = m_lblBackupZipFile.Top,
					AutoSize = true
				};
				m_cboProjects.Parent.Controls.Add(nada);
				m_cboProjects.Visible = false;
			}
		}
		#endregion
	}
}
