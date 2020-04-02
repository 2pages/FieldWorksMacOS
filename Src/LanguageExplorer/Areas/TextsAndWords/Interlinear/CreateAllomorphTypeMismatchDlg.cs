// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Diagnostics;
using System.Windows.Forms;

namespace LanguageExplorer.Areas.TextsAndWords.Interlinear
{
	/// <summary />
	public class CreateAllomorphTypeMismatchDlg : Form
	{
		private Label label1;
		private Button m_btnYes;
		private Button m_btnNo;
		private Button m_btnCreateNew;
		private PictureBox pictureBox1;
		private Label m_lblMessage1_Warning;
		private Label m_lblMessage2_Question;
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		/// <summary>
		/// Set the text of the top message.
		/// </summary>
		public string Warning
		{
			set => m_lblMessage1_Warning.Text = value;
		}

		/// <summary>
		/// Set the text of the bottom message.
		/// </summary>
		public string Question
		{
			set => m_lblMessage2_Question.Text = value;
		}

		public CreateAllomorphTypeMismatchDlg()
		{
			InitializeComponent();
			AccessibleName = GetType().Name;
			pictureBox1.Image = System.Drawing.SystemIcons.Warning.ToBitmap();
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****************** Missing Dispose() call for " + GetType().Name + ". ******************");
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CreateAllomorphTypeMismatchDlg));
			this.label1 = new System.Windows.Forms.Label();
			this.m_lblMessage1_Warning = new System.Windows.Forms.Label();
			this.m_lblMessage2_Question = new System.Windows.Forms.Label();
			this.m_btnYes = new System.Windows.Forms.Button();
			this.m_btnNo = new System.Windows.Forms.Button();
			this.m_btnCreateNew = new System.Windows.Forms.Button();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
			this.SuspendLayout();
			//
			// label1
			//
			resources.ApplyResources(this.label1, "label1");
			this.label1.Name = "label1";
			//
			// m_lblMessage1_Warning
			//
			resources.ApplyResources(this.m_lblMessage1_Warning, "m_lblMessage1_Warning");
			this.m_lblMessage1_Warning.Name = "m_lblMessage1_Warning";
			//
			// m_lblMessage2_Question
			//
			resources.ApplyResources(this.m_lblMessage2_Question, "m_lblMessage2_Question");
			this.m_lblMessage2_Question.Name = "m_lblMessage2_Question";
			//
			// m_btnYes
			//
			resources.ApplyResources(this.m_btnYes, "m_btnYes");
			this.m_btnYes.DialogResult = System.Windows.Forms.DialogResult.Yes;
			this.m_btnYes.Name = "m_btnYes";
			//
			// m_btnNo
			//
			resources.ApplyResources(this.m_btnNo, "m_btnNo");
			this.m_btnNo.DialogResult = System.Windows.Forms.DialogResult.No;
			this.m_btnNo.Name = "m_btnNo";
			//
			// m_btnCreateNew
			//
			resources.ApplyResources(this.m_btnCreateNew, "m_btnCreateNew");
			this.m_btnCreateNew.DialogResult = System.Windows.Forms.DialogResult.Retry;
			this.m_btnCreateNew.Name = "m_btnCreateNew";
			//
			// pictureBox1
			//
			resources.ApplyResources(this.pictureBox1, "pictureBox1");
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.TabStop = false;
			//
			// CreateAllomorphTypeMismatchDlg
			//
			this.AcceptButton = this.m_btnYes;
			resources.ApplyResources(this, "$this");
			this.CancelButton = this.m_btnNo;
			this.Controls.Add(this.pictureBox1);
			this.Controls.Add(this.m_btnCreateNew);
			this.Controls.Add(this.m_btnNo);
			this.Controls.Add(this.m_btnYes);
			this.Controls.Add(this.m_lblMessage2_Question);
			this.Controls.Add(this.m_lblMessage1_Warning);
			this.Controls.Add(this.label1);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "CreateAllomorphTypeMismatchDlg";
			((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
			this.ResumeLayout(false);

		}
		#endregion
	}
}