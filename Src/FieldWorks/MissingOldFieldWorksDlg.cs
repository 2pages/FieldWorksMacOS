// Copyright (c) 2010-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using SIL.LCModel.DomainServices.BackupRestore;

namespace SIL.FieldWorks
{
	/// <summary>
	/// This dialog is popped up when the user tries to restore/migrate an old project, but the
	/// old version of FieldWorks (or its special SQL Server instance) is not installed.
	/// </summary>
	internal sealed partial class MissingOldFieldWorksDlg : Form
	{
		/// <summary />
		private MissingOldFieldWorksDlg()
		{
			InitializeComponent();
		}

		/// <summary />
		internal MissingOldFieldWorksDlg(RestoreProjectSettings settings, bool fHaveFw60, bool fHaveSqlSvr) : this()
		{
			Debug.Assert(!fHaveFw60 || !fHaveSqlSvr);
			if (fHaveFw60)
			{
				m_labelFwDownload.Visible = false;
				m_lnkFw60.Visible = false;
			}
			if (fHaveSqlSvr)
			{
				m_labelSqlDownload.Visible = false;
				m_lnkSqlSvr.Visible = false;
			}
			m_lblBackupFile.Text = settings.Backup.File;

			if (!IsWindows7OrEarlier())
			{
				// No point in downloading SqlServer 2005 and dependencies; hide the relevant links
				m_labelSqlDownload.Visible = false;
				m_labelFwDownload.Visible = false;
				m_lnkSqlSvr.Visible = false;
				m_lnkFw60.Visible = false;
				m_labelAfterDownload.Visible = false;
				m_clickDownloadPicture.Visible = false;
				m_label6OrEarlier.Text = Properties.Resources.kstidCantMigrateWrongOS;
				m_label6OrEarlier.Height *= 4;
				m_btnOK.Enabled = false;
			}
		}

		private static bool IsWindows7OrEarlier()
		{
			// Windows 7 is 6.1; Windows 8 is 6.2
			var os = Environment.OSVersion;
			return os.Platform == PlatformID.Win32NT && os.Version.Major <= 6 && (os.Version.Major != 6 || os.Version.Minor <= 1);
		}

		/// <summary>
		/// Shrink the dialog box if necessary.
		/// </summary>
		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			int kDiff;
			if (m_labelAfterDownload.Visible)
			{
				kDiff = 2 * (m_lnkSqlSvr.Location.Y - m_labelSqlDownload.Location.Y);
			}
			else
			{
				kDiff = m_labelAfterDownload.Bottom - m_label6OrEarlier.Bottom;
				MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height - kDiff);
				Height = Height - kDiff;
				MaximumSize = new Size(MaximumSize.Width, MaximumSize.Height - kDiff);
				return;
			}
			if (!m_lnkFw60.Visible)
			{
				MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height - kDiff);
				Height = Height - kDiff;
				MaximumSize = new Size(MaximumSize.Width, MaximumSize.Height - kDiff);
			}
			if (!m_lnkSqlSvr.Visible)
			{
				MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height - kDiff);
				Height = Height - kDiff;
				MaximumSize = new Size(MaximumSize.Width, MaximumSize.Height - kDiff);
			}
		}

		// REVIEW-Linux: does this work on Linux?
		private void m_lnkFw60_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			using (Process.Start("http://downloads.sil.org/FieldWorks/OldSQLMigration/FW6Lite.exe"))
			{
			}
		}

		private void m_lnkSqlSvr_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			using (Process.Start("http://downloads.sil.org/FieldWorks/OldSQLMigration/SQL4FW.exe"))
			{
			}
		}
	}
}