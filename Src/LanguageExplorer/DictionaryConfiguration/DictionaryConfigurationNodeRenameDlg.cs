// Copyright (c) 2014-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Windows.Forms;

namespace LanguageExplorer.DictionaryConfiguration
{
	public partial class DictionaryConfigurationNodeRenameDlg : Form
	{
		/// <summary>
		/// Insert value into description in dialog.
		/// </summary>
		public string DisplayLabel
		{
			set { description.Text = description.Text.Replace("%s", value); }
		}

		public string NewSuffix
		{
			get { return newSuffix.Text; }
			set { newSuffix.Text = value; }
		}

		public DictionaryConfigurationNodeRenameDlg()
		{
			InitializeComponent();
		}
	}
}