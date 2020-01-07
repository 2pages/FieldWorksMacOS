// Copyright (c) 2011-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Drawing;
using System.Windows.Forms;

namespace LanguageExplorer.Areas
{
	/// <summary>
	/// This class implements the simplest node control for XmlDocConfigureDlg.
	/// </summary>
	public partial class ConfigParentNode : UserControl
	{
		///<summary>delegate for passing on the clicked event</summary>
		public delegate void ConfigureNowClickedHandler(object sender, LinkLabelLinkClickedEventArgs e);

		///<summary>
		/// Event handler for clicking the "Configure Now" link.
		///</summary>
		public event ConfigureNowClickedHandler ConfigureNowClicked;

		/// <summary />
		public ConfigParentNode()
		{
			InitializeComponent();
			m_lblMoreDetail.Location = new Point(m_tbMoreDetail.Location.X, m_tbMoreDetail.Location.Y);
			m_lnkConfigureNow.Location = new Point(m_tbMoreDetail.Location.X, m_tbMoreDetail.Location.Y + m_tbMoreDetail.Height);
			m_tbMoreDetail.Visible = false;
		}

		private void m_lnkConfigureNow_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			ConfigureNowClicked?.Invoke(sender, e);
		}

		///<summary>
		/// Set the label for the control.
		///</summary>
		public void SetDetails(string sMoreDetail, bool fEnabled, bool fShowLink)
		{
			m_lblMoreDetail.Visible = true;
			m_lblMoreDetail.Text = sMoreDetail;
			if (m_lblMoreDetail.Location.X + m_lblMoreDetail.PreferredWidth + Padding.Left + Padding.Right > Width)
			{
				m_lblMoreDetail.Visible = false;
				m_lblMoreDetail.Enabled = false;
				m_tbMoreDetail.Text = sMoreDetail;
				m_tbMoreDetail.Visible = true;
				m_tbMoreDetail.Enabled = fEnabled;
			}
			else
			{
				m_lblMoreDetail.Enabled = fEnabled;
				m_tbMoreDetail.Visible = false;
				m_tbMoreDetail.Enabled = false;
			}
			if (fShowLink)
			{
				m_lnkConfigureNow.Visible = true;
				m_lnkConfigureNow.Enabled = fEnabled;
				m_lnkConfigureNow.Location = m_lblMoreDetail.Visible
					? new Point(m_lblMoreDetail.Location.X, m_lblMoreDetail.Location.Y + m_lblMoreDetail.Height + m_lblMoreDetail.Padding.Bottom + 1)
					: new Point(m_tbMoreDetail.Location.X, m_tbMoreDetail.Location.Y + m_tbMoreDetail.Height + m_tbMoreDetail.Padding.Bottom + 1);
				Height = m_lnkConfigureNow.Location.Y + m_lnkConfigureNow.Height + m_lnkConfigureNow.Padding.Bottom + 1;
			}
			else
			{
				m_lnkConfigureNow.Visible = false;
				m_lnkConfigureNow.Enabled = false;
				if (m_lblMoreDetail.Visible)
				{
					Height = m_lblMoreDetail.Location.Y + m_lblMoreDetail.Height + m_lblMoreDetail.Padding.Bottom + 1;
				}
				else
				{
					Height = m_tbMoreDetail.Location.Y + m_tbMoreDetail.Height + m_tbMoreDetail.Padding.Bottom + 1;
				}
			}
		}
	}
}