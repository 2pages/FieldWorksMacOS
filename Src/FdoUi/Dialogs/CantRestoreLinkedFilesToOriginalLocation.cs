// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;

namespace SIL.FieldWorks.FdoUi.Dialogs
{
	/// <summary>
	///
	/// </summary>
	public partial class CantRestoreLinkedFilesToOriginalLocation : Form
	{
		private readonly IHelpTopicProvider m_helpTopicProvider;

		/// <summary>
		///
		/// </summary>
		public CantRestoreLinkedFilesToOriginalLocation(IHelpTopicProvider helpTopicProvider)
		{
			m_helpTopicProvider = helpTopicProvider;
			InitializeComponent();
		}

		/// <summary>
		///
		/// </summary>
		public bool fRestoreLinkedFilesToProjectFolder
		{
			get { return radio_Thanks.Checked; }
		}

		/// <summary>
		///
		/// </summary>
		public bool fDoNotRestoreLinkedFiles
		{
			get { return radio_NoThanks.Checked; }
		}

		private void button_OK_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.OK;
			Close();

		}

		private void button_Cancel_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.Cancel;
			Close();
		}

		private void button_Help_Click(object sender, EventArgs e)
		{
			ShowHelp.ShowHelpTopic(m_helpTopicProvider, "khtp-LinkedFilesFolder");
		}
	}
}
