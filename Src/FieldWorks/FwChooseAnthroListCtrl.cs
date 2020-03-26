// Copyright (c) 2019-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Windows.Forms;
using SIL.FieldWorks.FwCoreDlgs;

namespace SIL.FieldWorks
{
	/// <summary>
	/// This control is used during the new language project wizard to select the anthropology category
	/// </summary>
	internal sealed partial class FwChooseAnthroListCtrl : UserControl
	{
		private FwChooseAnthroListModel _model;

		/// <summary>
		/// Model can be null to allow for designer to work
		/// </summary>
		internal FwChooseAnthroListCtrl(FwChooseAnthroListModel model = null)
		{
			InitializeComponent();
			_model = model;
		}

		private void FrameDescriptionClick(object sender, MouseEventArgs e)
		{
			m_radioFRAME.Checked = true;
		}

		private void OcmDescriptionClick(object sender, MouseEventArgs e)
		{
			m_radioOCM.Checked = true;
		}

		private void CustomDescriptionClick(object sender, MouseEventArgs e)
		{
			m_radioCustom.Checked = true;
		}

		private void Custom_CheckedChanged(object sender, System.EventArgs e)
		{
			if (m_radioCustom.Checked)
			{
				_model.CurrentList = ListChoice.UserDef;
			}
		}

		private void OCM_CheckedChanged(object sender, System.EventArgs e)
		{
			if (m_radioOCM.Checked)
			{
				_model.CurrentList = ListChoice.OCM;
			}
		}

		private void Frame_CheckedChanged(object sender, System.EventArgs e)
		{
			if (m_radioFRAME.Checked)
			{
				_model.CurrentList = ListChoice.FRAME;
			}
		}
	}
}
