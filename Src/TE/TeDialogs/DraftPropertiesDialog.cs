// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwKernelInterfaces;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Infrastructure;
using SIL.FieldWorks.Common.ViewsInterfaces;

namespace SIL.FieldWorks.TE
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Dialog to present the Protected status and Description of an ScrDraft for editing.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public partial class DraftPropertiesDialog : Form
	{
		private IScrDraft m_draft;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructor.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public DraftPropertiesDialog()
		{
			InitializeComponent();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initialize the window
		/// </summary>
		/// <param name="draft">The draft.</param>
		/// ------------------------------------------------------------------------------------
		public void SetDialogInfo(IScrDraft draft)
		{
			m_draft = draft;
			m_cbProtected.Checked = m_draft.Protected;
			m_tbDescription.Text = m_draft.Description;
			pictVersionType.Image =
				m_imageListTypeIcons.Images[m_draft.Type == ScrDraftType.ImportedVersion ? 0 : 1];
			lblCreatedDate.Text = m_draft.DateCreated.ToString();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Save results when closed.
		/// </summary>
		/// <param name="e">The <see cref="T:System.EventArgs"/> that contains the event data.</param>
		/// ------------------------------------------------------------------------------------
		protected override void OnClosed(EventArgs e)
		{
			if (DialogResult == DialogResult.OK)
			{
				using (UndoableUnitOfWorkHelper undoHelper = new UndoableUnitOfWorkHelper(
					m_draft.Cache.ServiceLocator.GetInstance<IActionHandler>(),
					TeResourceHelper.GetResourceString("ksUndoDraftProperties"),
					TeResourceHelper.GetResourceString("ksRedoDraftProperties")))
				{
					m_draft.Protected = m_cbProtected.Checked;
					m_draft.Description = m_tbDescription.Text;

					undoHelper.RollBack = false;
				}
			}
			base.OnClosed(e);
		}
	}
}