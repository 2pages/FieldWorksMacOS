// Copyright (c) 2007-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using LanguageExplorer.MGA;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.Windows.Forms;

namespace LanguageExplorer.Controls
{
	/// <summary />
	internal class MasterListDlg : Form
	{
		protected ILcmOwningCollection<IFsFeatDefn> m_featureList;
		protected IPropertyTable m_propertyTable;
		protected LcmCache m_cache;
		protected IHelpTopicProvider m_helpTopicProvider;
		protected IFsFeatureSystem m_featureSystem;
		protected bool m_skipEvents;
		protected string m_sClassName;
		protected int iCheckedCount;
		protected string m_sWindowKeyLocation;
		protected string m_sWindowKeySize;
		protected Label label1;
		protected Label label2;
		protected GlossListTreeView m_tvMasterList;
		protected RichTextBox m_rtbDescription;
		protected Label label3;
		protected Button m_btnOK;
		protected Button m_btnCancel;
		protected Button m_bnHelp;
		protected PictureBox pictureBox1;
		protected LinkLabel linkLabel1;
		protected ImageList m_imageList;
		protected ImageList m_imageListPictures;
		protected System.ComponentModel.IContainer components;
		protected string s_helpTopic = "khtpInsertInflectionFeature";
		protected HelpProvider helpProvider;

		public MasterListDlg()
		{
			var treeView = new GlossListTreeView();
			InitDlg("FsClosedFeature", treeView);
		}

		internal MasterListDlg(string className, GlossListTreeView treeView)
		{
			InitDlg(className, treeView);
		}

		private void InitDlg(string className, GlossListTreeView treeView)
		{
			m_sClassName = className;
			m_tvMasterList = treeView;
			InitializeComponent();
			AccessibleName = GetType().Name;
			m_tvMasterList.TerminalsUseCheckBoxes = true;
			iCheckedCount = 0;
			pictureBox1.Image = m_imageListPictures.Images[0];
			m_btnOK.Enabled = false; // Disable until we are able to support interaction with the DB list of POSes.
			m_rtbDescription.ReadOnly = true;  // Don't allow any editing
			DoExtraInit();
		}

		protected virtual void DoExtraInit()
		{
			// needs to be overriden
		}

		/// <summary>
		/// Overridden to defeat the standard .NET behavior of adjusting size by
		/// screen resolution. That is bad for this dialog because we remember the size,
		/// and if we remember the enlarged size, it just keeps growing.
		/// If we defeat it, it may look a bit small the first time at high resolution,
		/// but at least it will stay the size the user sets.
		/// </summary>
		protected override void OnLoad(EventArgs e)
		{
			var size = Size;
			base.OnLoad(e);
			if (Size != size)
			{
				Size = size;
			}
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
				m_tvMasterList?.Dispose();
			}
			m_cache = null;
			SelectedFeatDefn = null;
			m_featureList = null;
			m_tvMasterList = null;

			base.Dispose(disposing);
		}

		public IFsFeatDefn SelectedFeatDefn { get; protected set; }

		///  <summary />
		public void SetDlginfo(IFsFeatureSystem featSys, IPropertyTable propertyTable)
		{
			// default to inflection features
			SetDlginfo(featSys, propertyTable, "masterInflFeatListDlg", Path.Combine(FwDirectoryFinder.CodeDirectory, "Language Explorer", "MGA", "GlossLists", "EticGlossList.xml"));
		}

		///  <summary />
		public void SetDlginfo(IFsFeatureSystem featSys, IPropertyTable propertyTable, string sWindowKey, string eticGlossListXmlPathname)
		{
			m_featureSystem = featSys;
			m_featureList = featSys.FeaturesOC;
			m_propertyTable = propertyTable;
			if (m_propertyTable != null)
			{
				m_sWindowKeyLocation = sWindowKey + "Location";
				m_sWindowKeySize = sWindowKey + "Size";
				ResetWindowLocationAndSize();
				m_helpTopicProvider = m_propertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider);
				helpProvider = new HelpProvider
				{
					HelpNamespace = m_helpTopicProvider.HelpFile
				};
				helpProvider.SetHelpKeyword(this, m_helpTopicProvider.GetHelpString(s_helpTopic));
				helpProvider.SetHelpNavigator(this, HelpNavigator.Topic);
			}
			m_cache = featSys.Cache;
			LoadMasterFeatures(eticGlossListXmlPathname);
			m_tvMasterList.Cache = m_cache;
		}

		private void ResetWindowLocationAndSize()
		{
			// Get location to the stored values, if any.
			if (!m_propertyTable.TryGetValue(m_sWindowKeyLocation, out Point dlgLocation) || !m_propertyTable.TryGetValue(m_sWindowKeySize, out Size dlgSize))
			{
				return;
			}
			var rect = new Rectangle(dlgLocation, dlgSize);
			ScreenHelper.EnsureVisibleRect(ref rect);
			DesktopBounds = rect;
			StartPosition = FormStartPosition.Manual;
		}

		private void LoadMasterFeatures(string sXmlFile)
		{
			m_tvMasterList.LoadGlossListTreeFromXml(sXmlFile, "en");
			// walk tree and set InDatabase info and use ToString() and change color, etc.
			AdjustNodes(m_tvMasterList.Nodes);
		}

		private void AdjustNodes(TreeNodeCollection treeNodes)
		{
			foreach (TreeNode node in treeNodes)
			{
				AdjustNode(node);
			}
		}

		private void AdjustNode(TreeNode treeNode)
		{
			var mi = (MasterItem)treeNode.Tag;
			mi.DetermineInDatabase(m_cache);
			treeNode.Text = mi.ToString();
			if (mi.InDatabase && treeNode.Nodes.Count == 0)
			{
				try
				{
					m_skipEvents = true;
					treeNode.Checked = true;
					treeNode.ImageIndex = (int)MGAImageKind.checkedBox;
					treeNode.SelectedImageIndex = treeNode.ImageIndex;
					treeNode.ForeColor = Color.Gray;
				}
				finally
				{
					m_skipEvents = false;
				}
			}
			var list = treeNode.Nodes;
			if (list.Count < 1)
			{
				return;
			}
			if (!mi.KindCanBeInDatabase() || mi.InDatabase)
			{
				AdjustNodes(treeNode.Nodes);
			}
			DoFinalAdjustment(treeNode);
		}
		protected virtual void DoFinalAdjustment(TreeNode treeNode)
		{
			// default is to do nothing
		}
		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MasterListDlg));
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			// seems to be crucial that the following is commented off
			//this.m_tvMasterList = new LanguageExplorer.MGA.GlossListTreeView();
			this.m_imageList = new System.Windows.Forms.ImageList(this.components);
			this.m_rtbDescription = new System.Windows.Forms.RichTextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.m_btnOK = new System.Windows.Forms.Button();
			this.m_btnCancel = new System.Windows.Forms.Button();
			this.m_bnHelp = new System.Windows.Forms.Button();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.m_imageListPictures = new System.Windows.Forms.ImageList(this.components);
			((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
			this.SuspendLayout();
			//
			// label1
			//
			resources.ApplyResources(this.label1, "label1");
			this.label1.Name = "label1";
			//
			// label2
			//
			resources.ApplyResources(this.label2, "label2");
			this.label2.Name = "label2";
			//
			// m_tvMasterList
			//
			resources.ApplyResources(this.m_tvMasterList, "m_tvMasterList");
			this.m_tvMasterList.FullRowSelect = true;
			this.m_tvMasterList.HideSelection = false;
			this.m_tvMasterList.Name = "m_tvMasterList";
			this.m_tvMasterList.TerminalsUseCheckBoxes = false;
			this.m_tvMasterList.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.m_tvMasterList_AfterCheck);
			this.m_tvMasterList.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.m_tvMasterList_AfterSelect);
			this.m_tvMasterList.BeforeCheck += new System.Windows.Forms.TreeViewCancelEventHandler(this.m_tvMasterList_BeforeCheck);
			//
			// m_imageList
			//
			this.m_imageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("m_imageList.ImageStream")));
			this.m_imageList.TransparentColor = System.Drawing.Color.Transparent;
			this.m_imageList.Images.SetKeyName(0, "");
			this.m_imageList.Images.SetKeyName(1, "");
			this.m_imageList.Images.SetKeyName(2, "");
			//
			// m_rtbDescription
			//
			resources.ApplyResources(this.m_rtbDescription, "m_rtbDescription");
			this.m_rtbDescription.Name = "m_rtbDescription";
			//
			// label3
			//
			resources.ApplyResources(this.label3, "label3");
			this.label3.Name = "label3";
			//
			// m_btnOK
			//
			resources.ApplyResources(this.m_btnOK, "m_btnOK");
			this.m_btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
			this.m_btnOK.Name = "m_btnOK";
			//
			// m_btnCancel
			//
			resources.ApplyResources(this.m_btnCancel, "m_btnCancel");
			this.m_btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.m_btnCancel.Name = "m_btnCancel";
			//
			// m_bnHelp
			//
			resources.ApplyResources(this.m_bnHelp, "m_bnHelp");
			this.m_bnHelp.Name = "m_bnHelp";
			this.m_bnHelp.Click += new System.EventHandler(this.m_bnHelp_Click);
			//
			// pictureBox1
			//
			resources.ApplyResources(this.pictureBox1, "pictureBox1");
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.TabStop = false;
			//
			// linkLabel1
			//
			resources.ApplyResources(this.linkLabel1, "linkLabel1");
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = LanguageExplorerControls.ksLinkText;
			this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
			//
			// m_imageListPictures
			//
			this.m_imageListPictures.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("m_imageListPictures.ImageStream")));
			this.m_imageListPictures.TransparentColor = System.Drawing.Color.Magenta;
			this.m_imageListPictures.Images.SetKeyName(0, "");
			//
			// MasterListDlg
			//
			this.AcceptButton = this.m_btnOK;
			resources.ApplyResources(this, "$this");
			this.CancelButton = this.m_btnCancel;
			this.ControlBox = false;
			this.Controls.Add(this.linkLabel1);
			this.Controls.Add(this.pictureBox1);
			this.Controls.Add(this.m_bnHelp);
			this.Controls.Add(this.m_btnCancel);
			this.Controls.Add(this.m_btnOK);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.m_rtbDescription);
			this.Controls.Add(this.m_tvMasterList);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.label1);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "MasterListDlg";
			this.ShowInTaskbar = false;
			this.Closing += new System.ComponentModel.CancelEventHandler(this.MasterListDlg_Closing);
			((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
			this.ResumeLayout(false);

		}
		#endregion

		void m_bnHelp_Click(object sender, EventArgs e)
		{
			ShowHelp.ShowHelpTopic(m_helpTopicProvider, "UserHelpFile", s_helpTopic);
		}

		protected void m_tvMasterList_AfterSelect(object sender, TreeViewEventArgs e)
		{
			var mi = (MasterItem)e.Node.Tag;
			mi.ResetDescription(m_rtbDescription);
			ResetOKBtnEnable();
		}
		/// <summary>
		/// Cancel, if it is already in the database.
		/// </summary>
		protected void m_tvMasterList_BeforeCheck(object sender, TreeViewCancelEventArgs e)
		{
			if (m_skipEvents)
			{
				return;
			}
			var selMC = (MasterItem)e.Node.Tag;
			e.Cancel = selMC.InDatabase;
			if (selMC.InDatabase || !selMC.KindCanBeInDatabase())
			{
				return;
			}
			if (e.Node.Checked)
			{
				iCheckedCount--;
			}
			else
			{
				iCheckedCount++;
			}
		}

		protected void m_tvMasterList_AfterCheck(object sender, TreeViewEventArgs e)
		{
			if (m_skipEvents)
			{
				return;
			}

			ResetOKBtnEnable();
		}

		private void ResetOKBtnEnable()
		{
			if (m_tvMasterList.TerminalsUseCheckBoxes)
			{
				m_btnOK.Enabled = iCheckedCount != 0;
			}
			else
			{
				var selNode = m_tvMasterList.SelectedNode;
				m_btnOK.Enabled = selNode != null && HasChosenItemNotInDatabase(selNode);
			}
		}

		private static bool HasChosenItemNotInDatabase(TreeNode node)
		{
			return node.Checked && node.Tag is MasterItem masterItem && !masterItem.InDatabase;
		}

		/// <summary>
		/// If OK, then add relevant inflection features to DB.
		/// </summary>
		private void MasterListDlg_Closing(object sender, CancelEventArgs e)
		{
			switch (DialogResult)
			{
				default:
					SelectedFeatDefn = null;
					break;
				case DialogResult.OK:
					{
						using (new WaitCursor(this))
						{
							if (m_tvMasterList.TerminalsUseCheckBoxes)
							{
								UpdateAllCheckedItems(m_tvMasterList.Nodes);
							}
							else
							{
								if (m_tvMasterList.SelectedNode.Tag is MasterItem masterItem)
								{
									masterItem.AddToDatabase(m_cache);
									SelectedFeatDefn = masterItem.FeatureDefn;
								}
							}
						}
						break;
					}
				case DialogResult.Yes:
					{
						// Closing via the hotlink.
						// Do nothing special, except avoid setting m_selFeatDefn to null, as in the default case.
						break;
					}
			}
			if (m_propertyTable != null)
			{
				m_propertyTable.SetProperty(m_sWindowKeyLocation, Location, true, true);
				m_propertyTable.SetProperty(m_sWindowKeySize, Size, true, true);
			}
		}

		private void UpdateAllCheckedItems(TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes)
			{
				if (node.Nodes.Count > 0)
				{
					UpdateAllCheckedItems(node.Nodes);
				}
				else
				{
					if (!node.Checked)
					{
						continue;
					}
					var mi = (MasterItem)node.Tag;
					if (mi.InDatabase)
					{
						continue;
					}
					mi.AddToDatabase(m_cache);
					SelectedFeatDefn = mi.FeatureDefn;
				}
			}
		}
		protected virtual void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			// should be overriden, but just in case...
			DialogResult = DialogResult.Yes;
			Close();
		}
	}
}