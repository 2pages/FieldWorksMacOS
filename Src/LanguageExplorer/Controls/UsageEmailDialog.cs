// Copyright (c) 2006-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using SIL.LCModel.Utils;
using SIL.PlatformUtilities;

namespace LanguageExplorer.Controls
{
	/// <summary />
	internal sealed class UsageEmailDialog : Form
	{
		private TabControl tabControl1;
		private TabPage tabPage1;
		private PictureBox pictureBox1;
		private RichTextBox richTextBox2;
		private Button btnSend;
		private LinkLabel btnNope;

		private RichTextBox m_topLineText;

		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		private UsageEmailDialog()
		{
			InitializeComponent();
			AccessibleName = GetType().Name;
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
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

		/// <summary />
		internal string EmailAddress { set; get; } = string.Empty;

		/// <summary>
		/// the  e-mail subject
		/// </summary>
		internal string EmailSubject { set; get; } = "Automated Usage Report";

		/// <summary />
		internal string Body { set; get; } = string.Empty;

		/// <summary />
		internal string TopLineText
		{
			set => m_topLineText.Text = value;
			get => m_topLineText.Text;
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UsageEmailDialog));
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.m_topLineText = new System.Windows.Forms.RichTextBox();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.richTextBox2 = new System.Windows.Forms.RichTextBox();
			this.btnSend = new System.Windows.Forms.Button();
			this.btnNope = new System.Windows.Forms.LinkLabel();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
			this.SuspendLayout();
			//
			// tabControl1
			//
			this.tabControl1.Controls.Add(this.tabPage1);
			resources.ApplyResources(this.tabControl1, "tabControl1");
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			//
			// tabPage1
			//
			this.tabPage1.BackColor = System.Drawing.SystemColors.Window;
			this.tabPage1.Controls.Add(this.m_topLineText);
			this.tabPage1.Controls.Add(this.pictureBox1);
			this.tabPage1.Controls.Add(this.richTextBox2);
			resources.ApplyResources(this.tabPage1, "tabPage1");
			this.tabPage1.Name = "tabPage1";
			//
			// m_topLineText
			//
			this.m_topLineText.BorderStyle = System.Windows.Forms.BorderStyle.None;
			resources.ApplyResources(this.m_topLineText, "m_topLineText");
			this.m_topLineText.Name = "m_topLineText";
			//
			// pictureBox1
			//
			resources.ApplyResources(this.pictureBox1, "pictureBox1");
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.TabStop = false;
			//
			// richTextBox2
			//
			this.richTextBox2.BorderStyle = System.Windows.Forms.BorderStyle.None;
			resources.ApplyResources(this.richTextBox2, "richTextBox2");
			this.richTextBox2.Name = "richTextBox2";
			//
			// btnSend
			//
			resources.ApplyResources(this.btnSend, "btnSend");
			this.btnSend.Name = "btnSend";
			this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
			//
			// btnNope
			//
			resources.ApplyResources(this.btnNope, "btnNope");
			this.btnNope.Name = "btnNope";
			this.btnNope.TabStop = true;
			this.btnNope.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.btnNope_LinkClicked);
			//
			// UsageEmailDialog
			//
			this.AcceptButton = this.btnSend;
			resources.ApplyResources(this, "$this");
			this.CancelButton = this.btnNope;
			this.ControlBox = false;
			this.Controls.Add(this.btnNope);
			this.Controls.Add(this.btnSend);
			this.Controls.Add(this.tabControl1);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MinimizeBox = false;
			this.Name = "UsageEmailDialog";
			this.TopMost = true;
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
			this.ResumeLayout(false);

		}
		#endregion

		private void btnSend_Click(object sender, System.EventArgs e)
		{
			try
			{
				var body = Body.Replace(System.Environment.NewLine, "%0A").Replace("\"", "%22").Replace("&", "%26");
				using (var p = new Process())
				{
					p.StartInfo.FileName = $"mailto:{EmailAddress}?subject={EmailSubject}&body={body}";
					p.Start();
				}
			}
			catch (Exception)
			{
				//swallow it
			}
			DialogResult = DialogResult.OK;
			Close();
		}

		private void btnNope_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			DialogResult = DialogResult.No;
			Close();
		}

		/// <summary>
		/// call this each time the application is launched if you have launch count-based reporting
		/// </summary>
		internal static void IncrementLaunchCount(RegistryKey applicationKey)
		{
			var launchCount = int.Parse((string)applicationKey.GetValue("launches", "0")) + 1;
			applicationKey.SetValue("launches", launchCount.ToString());
		}

		/// <summary>
		/// if you call this every time the application starts, it will send reports on the
		/// specified launch number.  It will get version number and name out of the application.
		/// </summary>
		/// <param name="applicationName">Name of the application.</param>
		/// <param name="applicationKey">The application registry key.</param>
		/// <param name="emailAddress">The e-mail address.</param>
		/// <param name="topMessage">The message at the top of the e-mail.</param>
		/// <param name="addStats">True to add crash and application runtime statistics to the
		/// report.</param>
		/// <param name="launchNumber">The needed launch count to show the dialog and ask for
		/// an e-mail.</param>
		/// <param name="assembly">The assembly to use for getting version information (can be
		/// <c>null</c>).</param>
		internal static void DoTrivialUsageReport(string applicationName, RegistryKey applicationKey,
			string emailAddress, string topMessage, bool addStats, int launchNumber, Assembly assembly = null)
		{
			var launchCount = int.Parse((string)applicationKey.GetValue("launches", "0"));
			if (launchNumber == launchCount)
			{
				// Set the Application label to the name of the app
				if (assembly == null)
				{
					assembly = Assembly.GetEntryAssembly();
				}
				var version = Application.ProductVersion;
				if (assembly != null)
				{
					var attributes = assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false);
					version = attributes.Length > 0 ? ((AssemblyFileVersionAttribute)attributes[0]).Version : Application.ProductVersion;
				}
				using (var d = new UsageEmailDialog())
				{
					d.TopLineText = topMessage;
					d.EmailAddress = emailAddress;
					d.EmailSubject = $"{applicationName} {version} Report {launchCount} Launches";
					var bldr = new StringBuilder();
					bldr.AppendFormat("<report app='{0}' version='{1}' linux='{2}'>", applicationName, version, Platform.IsUnix);
					bldr.AppendFormat("<stat type='launches' value='{0}'/>", launchCount);
					if (launchCount > 1)
					{
						var val = (int)applicationKey.GetValue("NumberOfSeriousCrashes", 0);
						bldr.AppendFormat("<stat type='NumberOfSeriousCrashes' value='{0}'/>", val);
						val = (int)applicationKey.GetValue("NumberOfAnnoyingCrashes", 0);
						bldr.AppendFormat("<stat type='NumberOfAnnoyingCrashes' value='{0}'/>", val);
						var csec = (int)applicationKey.GetValue("TotalAppRuntime", 0);
						var cmin = csec / 60;
						var sRuntime = $"{cmin / 60}:{cmin % 60:d2}:{csec % 60:d2}";
						bldr.AppendFormat("<stat type='TotalAppRuntime' value='{0}'/>", sRuntime);
					}
					bldr.AppendFormat("</report>");
					d.Body = bldr.ToString();
					d.ShowDialog();
				}
			}
		}
	}
}