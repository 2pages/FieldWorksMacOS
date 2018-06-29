// Copyright (c) 2009-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

#if RANDYTODO
	// TODO: This really needs to be refactored with MasterCategoryListDlg.cs
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.XPath;
using LanguageExplorer.Areas;
using LanguageExplorer.Controls.XMLViews;
using SIL.LCModel.Core.Cellar;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Application;
using SIL.LCModel.Infrastructure;
using SIL.Windows.Forms;

namespace LanguageExplorer.Controls.LexText
{
	/// <summary>
	/// This dialog shows the list of all defined phonological features.
	/// It allows a user to select values for one or more phonological features.
	/// It can be given a feature structure indicating current feature/value pairs.
	/// A user may choose the "n/a" option to remove a feature/value pair from an existing feature structure.
	/// If returns a feature structure with the selected feature/value pairs.
	/// </summary>
	public class PhonologicalFeatureChooserDlg : Form
	{
		private IPropertyTable m_propertyTable;
		private IPublisher m_publisher;
		private LcmCache m_cache;
		private IPhRegularRule m_rule;
		private IPhSimpleContextNC m_ctxt;
		private PhonologicalFeaturePublisher m_sda;
		private FwLinkArgs m_link;
		// The dialog can be initialized with an existing feature structure,
		// or just with an owning object and flid in which to create one.
		// Where to put a new feature structure if needed. Owning flid may be atomic
		// or collection. Used only if m_fs is initially null.
		int m_hvoOwner;
		int m_owningFlid;
		private Panel m_listPanel;
		private Button m_btnOK;
		private Button m_btnCancel;
		private Button m_bnHelp;
		private PictureBox pictureBox1;
		private LinkLabel linkLabel1;
		private Label labelPrompt;

		private FwComboBox m_valuesCombo;

		private string m_helpTopic = "khtpChoose-PhonFeats";
		private BrowseViewer m_bvList;
		private HelpProvider m_helpProvider;

		public PhonologicalFeatureChooserDlg()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			AccessibleName = GetType().Name;

			m_valuesCombo = new FwComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				AdjustStringHeight = false,
				BackColor = SystemColors.Window,
				Padding = new Padding(0, 1, 0, 0)
			};
			m_valuesCombo.SelectedIndexChanged += m_valuesCombo_SelectedIndexChanged;

			Resize += PhonologicalFeatureChooserDlg_Resize;
		}

		void PhonologicalFeatureChooserDlg_Resize(object sender, EventArgs e)
		{
			PositionValuesCombo();
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
			PopulateValuesCombo();
			PositionValuesCombo();
		}

		/// <summary>
		/// Updates the feature constraints in the context.
		/// </summary>
		private void UpdateFeatureConstraints()
		{
			var featureHvos = m_bvList.AllItems;
			foreach (var hvoClosedFeature in featureHvos)
			{
				var feat = m_cache.ServiceLocator.GetInstance<IFsFeatDefnRepository>().GetObject(hvoClosedFeature);
				var str = m_sda.get_UnicodeProp(feat.Hvo, PhonologicalFeaturePublisher.PolarityFlid);
				if (string.IsNullOrEmpty(str))
				{
					var removedConstr = RemoveFeatureConstraint(m_ctxt.PlusConstrRS, feat) ?? RemoveFeatureConstraint(m_ctxt.MinusConstrRS, feat);
					if (removedConstr != null)
					{
						if (!m_rule.FeatureConstraints.Contains(removedConstr))
						{
							m_cache.LangProject.PhonologicalDataOA.FeatConstraintsOS.Remove(removedConstr);
						}
					}
				}
				else
				{
					var var = GetFeatureConstraint(m_rule.FeatureConstraints, feat);
					if (var == null)
					{
						var = m_cache.ServiceLocator.GetInstance<IPhFeatureConstraintFactory>().Create();
						m_cache.LangProject.PhonologicalDataOA.FeatConstraintsOS.Add(var);
						var.FeatureRA = feat;
					}

					if (str == LexTextControls.ksFeatConstrAgree)
					{
						if (!m_ctxt.PlusConstrRS.Contains(var))
						{
							m_ctxt.PlusConstrRS.Add(var);
							RemoveFeatureConstraint(m_ctxt.MinusConstrRS, feat);
						}
					}
					else if (str == LexTextControls.ksFeatConstrDisagree)
					{
						if (!m_ctxt.MinusConstrRS.Contains(var))
						{
							m_ctxt.MinusConstrRS.Add(var);
							RemoveFeatureConstraint(m_ctxt.PlusConstrRS, feat);
						}
					}
				}
			}
		}

		private IPhFeatureConstraint RemoveFeatureConstraint(ILcmReferenceSequence<IPhFeatureConstraint> featConstrs, IFsFeatDefn feat)
		{
			var constrToRemove = GetFeatureConstraint(featConstrs, feat);
			if (constrToRemove != null)
			{
				featConstrs.Remove(constrToRemove);
			}
			return constrToRemove;
		}

		private IPhFeatureConstraint GetFeatureConstraint(IEnumerable<IPhFeatureConstraint> featConstrs, IFsFeatDefn feat)
		{
			return featConstrs.FirstOrDefault(curConstr => curConstr.FeatureRA == feat);
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			// Must not be run more than once.
			if (IsDisposed)
			{
				return;
			}

			if (disposing)
			{
				m_helpProvider?.Dispose();
			}
			m_cache = null;
			FS = null;
			m_ctxt = null;
			m_cache = null;
			m_bvList = null;
			m_valuesCombo = null;

			base.Dispose( disposing );
		}

		/// <summary>
		/// Init the dialog with an existing context.
		/// </summary>
		public void SetDlgInfo(LcmCache cache, IPropertyTable propertyTable, IPublisher publisher, IPhRegularRule rule, IPhSimpleContextNC ctxt)
		{
			SetDlgInfo(cache, propertyTable, publisher, ctxt.FeatureStructureRA.Hvo, PhNCFeaturesTags.kflidFeatures, ((IPhNCFeatures)ctxt.FeatureStructureRA).FeaturesOA, rule, ctxt);
		}

		public void SetDlgInfo(LcmCache cache, IPropertyTable propertyTable, IPublisher publisher, IPhRegularRule rule)
		{
			SetDlgInfo(cache, propertyTable, publisher, 0, 0, null, rule, null);
		}

		/// <summary>
		/// Init the dialog with an existing FS.
		/// </summary>
		public void SetDlgInfo(LcmCache cache, IPropertyTable propertyTable, IPublisher publisher, IFsFeatStruc fs)
		{
			SetDlgInfo(cache, propertyTable, publisher, fs.Owner.Hvo, fs.OwningFlid, fs, null, null);
		}

		/// <summary>
		/// Init the dialog with a PhPhoneme (or PhNCFeatures) and flid that does not yet contain a feature structure.
		/// </summary>
		public void SetDlgInfo(LcmCache cache, IPropertyTable propertyTable, IPublisher publisher, ICmObject cobj, int owningFlid)
		{
			SetDlgInfo(cache, propertyTable, publisher, cobj.Hvo, owningFlid, null, null, null);
		}

		public void SetDlgInfo(LcmCache cache, IPropertyTable propertyTable, IPublisher publisher)
		{
			SetDlgInfo(cache, propertyTable, publisher, 0, 0, null, null, null);
		}

		private void SetDlgInfo(LcmCache cache, IPropertyTable propertyTable, IPublisher publisher, int hvoOwner, int owningFlid, IFsFeatStruc fs, IPhRegularRule rule, IPhSimpleContextNC ctxt)
		{
			FS = fs;
			m_owningFlid = owningFlid;
			m_hvoOwner = hvoOwner;
			m_rule = rule;
			m_ctxt = ctxt;
			m_propertyTable = propertyTable;
			m_publisher = publisher;
			if (m_propertyTable != null)
			{
				// Reset window location.
				// Get location to the stored values, if any.
				Point dlgLocation;
				Size dlgSize;
				if (m_propertyTable.TryGetValue("phonFeatListDlgLocation", out dlgLocation) && m_propertyTable.TryGetValue("phonFeatListDlgSize", out dlgSize))
				{
					var rect = new Rectangle(dlgLocation, dlgSize);
					ScreenHelper.EnsureVisibleRect(ref rect);
					DesktopBounds = rect;
					StartPosition = FormStartPosition.Manual;
				}

				var helpTopicProvider = (m_propertyTable.GetValue<IHelpTopicProvider>("HelpTopicProvider"));
				if (helpTopicProvider != null) // Will be null when running tests
				{
					m_helpProvider.HelpNamespace = helpTopicProvider.HelpFile;
					m_helpProvider.SetHelpKeyword(this, helpTopicProvider.GetHelpString(m_helpTopic));
					m_helpProvider.SetHelpNavigator(this, HelpNavigator.Topic);
				}
			}
			m_cache = cache;
			m_valuesCombo.WritingSystemFactory = m_cache.LanguageWritingSystemFactoryAccessor;
			m_valuesCombo.StyleSheet = FwUtils.StyleSheetFromPropertyTable(m_propertyTable);

			LoadPhonFeats(FS);
			BuildInitialBrowseView();
		}

		private bool ContainsFeature(IEnumerable<IPhFeatureConstraint> vars, IFsFeatDefn feat)
		{
			return vars.Any(var => var.FeatureRA == feat);
		}

		/// <summary>
		/// Sets the help topic ID for the window.  This is used in both the Help button and when the user hits F1
		/// </summary>
		public void SetHelpTopic(string helpTopic)
		{
			m_helpTopic = helpTopic;
			m_helpProvider.SetHelpKeyword(this, m_propertyTable.GetValue<IHelpTopicProvider>("HelpTopicProvider").GetHelpString(helpTopic));
		}

		public void HandleJump()
		{
			if (m_link != null)
			{
				LinkHandler.PublishFollowLinkMessage(m_publisher, m_link);
			}
		}

		void m_bvList_SelectionChanged(object sender, FwObjectSelectionEventArgs e)
		{
			PopulateValuesCombo();
			PositionValuesCombo();
		}

		private void PositionValuesCombo()
		{
			if (m_bvList == null)
			{
				return;
			}
			var hprops = m_bvList.Scroller.HorizontalScroll;
			var iValueLocationHorizontalOffset = hprops.Value;
			var valueLocation = m_bvList.LocationOfCellInSelectedRow("Value");
			m_valuesCombo.Location = new Point(valueLocation.Left + m_listPanel.Left + 2 - iValueLocationHorizontalOffset, valueLocation.Top + m_listPanel.Top - 3);
			m_valuesCombo.Size = new Size(valueLocation.Width + 1, valueLocation.Height + 4);
			if (!Controls.Contains(m_valuesCombo))
			{
				Controls.Add(m_valuesCombo);
			}
			if (IsValuesComboBoxVisible(hprops))
			{
				m_valuesCombo.Visible = true;
				m_valuesCombo.BringToFront();
			}
			else
			{
				m_valuesCombo.Visible = false;
				m_valuesCombo.SendToBack();
			}
		}
		private bool IsValuesComboBoxVisible(HScrollProperties hprops)
		{
			var iVerticalScrollBarWidth = (m_bvList.ScrollBar.Visible) ? SystemInformation.VerticalScrollBarWidth : 0;
			var iHorizontalScrollBarHeight = (hprops.Visible) ? SystemInformation.HorizontalScrollBarHeight : 0;

			if (m_valuesCombo.Top < (m_listPanel.Top + m_bvList.BrowseView.Top))
			{
				return false;  // too high
			}
			if (m_valuesCombo.Bottom > (m_listPanel.Bottom - iHorizontalScrollBarHeight))
			{
				return false; // too low
			}
			if (m_valuesCombo.Right > (m_listPanel.Right - iVerticalScrollBarWidth + 1))
			{
				return false; // too far to the right
			}
			return m_valuesCombo.Left >= m_listPanel.Left;
		}
		private void PopulateValuesCombo()
		{
			var selIndex = m_bvList.SelectedIndex;
			if (selIndex < 0)
			{
				if (Controls.Contains(m_valuesCombo))
				{
					Controls.Remove(m_valuesCombo);
				}
				return;
			}
			var hvoSel = m_bvList.AllItems[selIndex];
			var feat = m_cache.ServiceLocator.GetInstance<IFsClosedFeatureRepository>().GetObject(hvoSel);
			m_valuesCombo.Items.Clear();
			var valHvo = m_sda.get_ObjectProp(hvoSel, PhonologicalFeaturePublisher.ValueFlid);
			var comboSelectedIndex = -1;
			var index = 0;
			var sortedVaues = feat.ValuesOC.OrderBy(v => v.Abbreviation.BestAnalysisAlternative.Text);
			foreach (var val in sortedVaues)
			{
				m_valuesCombo.Items.Add(val.Abbreviation.BestAnalysisAlternative);
				// try to set the selected item
				if (valHvo == val.Hvo)
				{
					comboSelectedIndex = index;
				}
				++index;
			}
			if (ShowFeatureConstraintValues)
			{
				m_valuesCombo.Items.Add(LexTextControls.ksFeatConstrAgree);
				index++;
				m_valuesCombo.Items.Add(LexTextControls.ksFeatConstrDisagree);
				index++;

				var str = m_sda.get_UnicodeProp(hvoSel, PhonologicalFeaturePublisher.PolarityFlid);
				for (var i = feat.ValuesOC.Count; i < m_valuesCombo.Items.Count; i++)
				{
					var comboStr = m_valuesCombo.Items[i] as string;
					if (str == comboStr || (string.IsNullOrEmpty(str) && comboStr == LexTextControls.ks_DontCare_))
					{
						comboSelectedIndex = i;
						break;
					}
				}
			}
			if (comboSelectedIndex < 0)
			{
				comboSelectedIndex = feat.ValuesOC.Count + (ShowFeatureConstraintValues ? 2 : 0);
				Debug.Assert(comboSelectedIndex == index);
			}
			m_valuesCombo.Items.Add(ShowIgnoreInsteadOfDontCare ? LexTextControls.ks_Ignore_ : LexTextControls.ks_DontCare_);
			m_valuesCombo.SelectedIndex = comboSelectedIndex;
			if (!Controls.Contains(m_valuesCombo))
			{
				Controls.Add(m_valuesCombo);
			}
		}

		void m_valuesCombo_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (m_valuesCombo.SelectedIndex == -1)
			{
				return;
			}

			// make sure the dummy value reflects the selected value
			var selectedRowIndex = m_bvList.SelectedIndex;
			var hvoSel = m_bvList.AllItems[selectedRowIndex];
			var feat = m_cache.ServiceLocator.GetInstance<IFsClosedFeatureRepository>().GetObject(hvoSel);
			var selectedValueIndex = m_valuesCombo.SelectedIndex;
			// values[selectedValueIndex] is the selected value
			if (selectedValueIndex >= feat.ValuesOC.Count)
			{
				// is unspecified or is a feature constraint
				if (ShowFeatureConstraintValues)
				{
					// make sure the dummy value reflects the selected value
					var str = m_valuesCombo.SelectedItem as string;
					if (str != null)
					{
						if (str == LexTextControls.ks_DontCare_)
						{
							str = string.Empty;
						}

						m_sda.SetUnicode(hvoSel, PhonologicalFeaturePublisher.PolarityFlid, str, str.Length);
					}
				}
				m_sda.SetObjProp(feat.Hvo, PhonologicalFeaturePublisher.ValueFlid, 0);
			}
			else
			{
				var sortedVaues = feat.ValuesOC.OrderBy(v => v.Abbreviation.BestAnalysisAlternative.Text);
				var val = sortedVaues.ElementAt(selectedValueIndex);
				m_sda.SetObjProp(feat.Hvo, PhonologicalFeaturePublisher.ValueFlid, val.Hvo);
			}
		}

		private void BuildInitialBrowseView()
		{
			var configurationParameters = m_propertyTable.GetValue<XElement>("WindowConfiguration");
			var toolNode = configurationParameters.XPathSelectElement("controls/parameters/guicontrol[@id='PhonologicalFeaturesFlatList']/parameters");

			m_listPanel.SuspendLayout();
			var sortedFeatureHvos = m_cache.LangProject.PhFeatureSystemOA.FeaturesOC.OrderBy(s => s.Name.BestAnalysisAlternative.Text).Select(s => s.Hvo);
			var featureHvos = sortedFeatureHvos.ToArray();
			m_sda.CacheVecProp(m_cache.LangProject.Hvo, featureHvos);
#if RANDYTODO
			// TODO: call Init Flex Comp after creating BrowseViewer.
			// TODO: Call FinishInitialization on m_bvList and feed it PhonologicalFeaturePublisher.ListFlid for the 'madeUpFieldIdentifier' parameter.
#endif
			m_bvList = new BrowseViewer(toolNode, m_cache.LangProject.Hvo, m_cache, null, m_sda);
			m_bvList.SelectionChanged += m_bvList_SelectionChanged;
			m_bvList.ScrollBar.ValueChanged += ScrollBar_ValueChanged;
			m_bvList.Scroller.Scroll += ScrollBar_Scroll;
			m_bvList.ColumnsChanged += BrowseViewer_ColumnsChanged;
			m_bvList.Resize += m_bvList_Resize;
			m_bvList.TabStop = true;
			m_bvList.StyleSheet = FwUtils.StyleSheetFromPropertyTable(m_propertyTable);
			m_bvList.Dock = DockStyle.Fill;
			m_bvList.BackColor = SystemColors.Window;
			m_listPanel.Controls.Add(m_bvList);
			m_listPanel.ResumeLayout(false);
		}

		private void ScrollBar_Scroll(object sender, ScrollEventArgs e)
		{
			PositionValuesCombo();
		}

		private void BrowseViewer_ColumnsChanged(object sender, EventArgs e)
		{
			PositionValuesCombo();
		}

		private void ScrollBar_ValueChanged(object sender, EventArgs e)
		{
			PositionValuesCombo();
		}

		private void m_bvList_Resize(object sender, EventArgs e)
		{
			PositionValuesCombo();
		}

		/// <summary>
		/// Load the tree items if the starting point is a feature structure.
		/// </summary>
		private void LoadPhonFeats(IFsFeatStruc fs)
		{
			m_sda = new PhonologicalFeaturePublisher(m_cache.GetManagedSilDataAccess(), m_cache);
			foreach (IFsClosedFeature feat in m_cache.LangProject.PhFeatureSystemOA.FeaturesOC)
			{
				if (fs != null)
				{
					var closedValue = fs.GetValue(feat);
					if (closedValue != null)
					{
						m_sda.SetObjProp(feat.Hvo, PhonologicalFeaturePublisher.ValueFlid, closedValue.ValueRA.Hvo);
					}
					else
					{
						if (m_ctxt != null && ShowFeatureConstraintValues)
						{
							string str;
							if (ContainsFeature(m_ctxt.PlusConstrRS, feat))
							{
								str = LexTextControls.ksFeatConstrAgree;
							}
							else if (ContainsFeature(m_ctxt.MinusConstrRS, feat))
							{
								str = LexTextControls.ksFeatConstrDisagree;
							}
							else
							{
								str = string.Empty;
							}
							if (!string.IsNullOrEmpty(str))
							{
								m_sda.SetUnicode(feat.Hvo, PhonologicalFeaturePublisher.PolarityFlid, str, str.Length);
								continue;
							}
						}
						// set the value to zero so nothing shows
						m_sda.SetObjProp(feat.Hvo, PhonologicalFeaturePublisher.ValueFlid, 0);
					}
				}
				else
				{
					// set the value to zero so nothing shows
					m_sda.SetObjProp(feat.Hvo, PhonologicalFeaturePublisher.ValueFlid, 0);
				}
			}
		}

		/// <summary>
		/// Get the simple context.
		/// </summary>
		public IPhSimpleContextNC Context
		{
			get
			{
				return m_ctxt;
			}

			set
			{
				m_ctxt = value;
				FS = ((IPhNCFeatures)m_ctxt.FeatureStructureRA).FeaturesOA;
			}
		}



		/// <summary>
		/// Get Feature Structure resulting from dialog operation
		/// </summary>
		public IFsFeatStruc FS { get; set; }

		/// <summary>
		/// Get/Set prompt text
		/// </summary>
		public string Prompt
		{
			get
			{
				return labelPrompt.Text;
			}
			set
			{
				var s1 = value ?? LexTextControls.ksPhonologicalFeatures;
				labelPrompt.Text = s1;
			}
		}

		/// <summary>
		/// Get/Set whether to include feature constraint values (agree/disagree) in value combos
		/// </summary>
		public bool ShowFeatureConstraintValues { get; set; }
		/// <summary>
		/// Get/Set whether to include feature constraint values (agree/disagree) in value combos
		/// </summary>
		public bool ShowIgnoreInsteadOfDontCare { get; set; }
		/// <summary>
		/// Get/Set dialog title text
		/// </summary>
		public string Title
		{
			get
			{
				return Text;
			}
			set
			{
				Text = value;
			}
		}
		/// <summary>
		/// Get/Set link text
		/// </summary>
		public string LinkText
		{
			get
			{
				return linkLabel1.Text;
			}
			set
			{
				linkLabel1.Text = value ?? LexTextControls.ksPhonologicalFeaturesAdd;
			}
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PhonologicalFeatureChooserDlg));
			this.labelPrompt = new System.Windows.Forms.Label();
			this.m_btnOK = new System.Windows.Forms.Button();
			this.m_btnCancel = new System.Windows.Forms.Button();
			this.m_bnHelp = new System.Windows.Forms.Button();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.m_listPanel = new System.Windows.Forms.Panel();
			this.m_helpProvider = new HelpProvider();
			((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
			this.SuspendLayout();
			//
			// labelPrompt
			//
			resources.ApplyResources(this.labelPrompt, "labelPrompt");
			this.labelPrompt.Name = "labelPrompt";
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
			this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
			//
			// m_listPanel
			//
			resources.ApplyResources(this.m_listPanel, "m_listPanel");
			this.m_listPanel.Name = "m_listPanel";
			//
			// PhonologicalFeatureChooserDlg
			//
			this.AcceptButton = this.m_btnOK;
			resources.ApplyResources(this, "$this");
			this.CancelButton = this.m_btnCancel;
			this.Controls.Add(this.m_listPanel);
			this.Controls.Add(this.linkLabel1);
			this.Controls.Add(this.pictureBox1);
			this.Controls.Add(this.m_bnHelp);
			this.Controls.Add(this.m_btnCancel);
			this.Controls.Add(this.m_btnOK);
			this.Controls.Add(this.labelPrompt);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "PhonologicalFeatureChooserDlg";
			this.ShowInTaskbar = false;
			this.Closing += new System.ComponentModel.CancelEventHandler(this.PhonologicalFeatureChooserDlg_Closing);
			((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
			this.ResumeLayout(false);

		}
		#endregion

		/// <summary>
		/// If OK, then make FS have the selected feature value(s).
		/// </summary>
		private void PhonologicalFeatureChooserDlg_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (DialogResult == DialogResult.OK)
			{
				using (new WaitCursor(this))
				{
					UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW(LexTextControls.ksUndoSelectionOfPhonologicalFeatures,
						LexTextControls.ksRedoSelectionOfPhonologicalFeatures, m_cache.ActionHandlerAccessor, () =>
					{
						if (FS == null)
						{
							// Didn't have one to begin with. See whether we want to create one.
							if (m_hvoOwner != 0 && CheckFeatureStructure())
							{
								// The last argument is meaningless since we expect this property to be owning
								// or collection.
								var hvoFs = m_cache.DomainDataByFlid.MakeNewObject(FsFeatStrucTags.kClassId, m_hvoOwner, m_owningFlid, -2);
								FS = m_cache.ServiceLocator.GetInstance<IFsFeatStrucRepository>().GetObject(hvoFs);
								UpdateFeatureStructure();
							}
						}
						else
						{
							// clean out any extant features in the feature structure
							FS.FeatureSpecsOC.Clear();
							UpdateFeatureStructure();
						}
					});
				}
			}

			if (m_propertyTable != null)
			{
				m_propertyTable.SetProperty("phonFeatListDlgLocation", Location, true, true);
				m_propertyTable.SetProperty("phonFeatListDlgSize", Size, true, true);
			}
		}

		/// <summary>
		/// Answer true if there is some feature with a real value.
		/// </summary>
		private bool CheckFeatureStructure()
		{
			var featureHvos = m_bvList.AllItems;
			foreach (var hvoClosedFeature in featureHvos)
			{
				var valHvo = m_sda.get_ObjectProp(hvoClosedFeature, PhonologicalFeaturePublisher.ValueFlid);
				if (valHvo > 0)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Makes the feature structure reflect the values chosen
		/// </summary>
		/// <remarks>Is internal for Unit Testing</remarks>
		internal void UpdateFeatureStructure()
		{
			var featureHvos = m_bvList.AllItems;
			foreach (var hvoClosedFeature in featureHvos)
			{
				var valHvo = m_sda.get_ObjectProp(hvoClosedFeature, PhonologicalFeaturePublisher.ValueFlid);
				if (valHvo > 0)
				{
					var feat = m_cache.ServiceLocator.GetInstance<IFsClosedFeatureRepository>().GetObject(hvoClosedFeature);
					var val = m_cache.ServiceLocator.GetInstance<IFsSymFeatValRepository>().GetObject(valHvo);
					var closedVal = FS.GetOrCreateValue(feat);
					closedVal.FeatureRA = feat;
					closedVal.ValueRA = val;
				}
			}

			if (ShowFeatureConstraintValues)
			{
				UpdateFeatureConstraints();
			}
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			var guid = m_cache.LangProject.PhFeatureSystemOA.Guid;
			m_link = new FwLinkArgs(AreaServices.PhonologicalFeaturesAdvancedEditMachineName, guid);
			m_btnCancel.PerformClick();
			DialogResult = DialogResult.Ignore;
		}

		private void m_bnHelp_Click(object sender, EventArgs e)
		{
			if (m_propertyTable.GetValue<string>(AreaServices.ToolChoice).Substring(0, 7) == "natural")
			{
				m_helpTopic = "khtpChoose-Phonemes";
			}
			ShowHelp.ShowHelpTopic(m_propertyTable.GetValue<IHelpTopicProvider>("HelpTopicProvider"), m_helpTopic);
		}

		private sealed class PhonologicalFeaturePublisher : ObjectListPublisher
		{
			private const int ListFlid = 89999988;
			public const int ValueFlid = 89999977;
			public const int PolarityFlid = 89999966;

			Dictionary<int, string> m_unicodeProps;
			Dictionary<int, int> m_objProps;

			private LcmCache m_cache;

			public PhonologicalFeaturePublisher(ISilDataAccessManaged domainDataByFlid, LcmCache cache)
				: base(domainDataByFlid, ListFlid)
			{
				m_cache = cache;
				m_objProps = new Dictionary<int, int>();
				m_unicodeProps = new Dictionary<int, string>();
				SetOverrideMdc(new PhonologicalFeatureMdc((IFwMetaDataCacheManaged)MetaDataCache));
			}

			public override int get_ObjectProp(int hvo, int tag)
			{
				if (tag == ValueFlid)
				{
					int valHvo;
					if (!m_objProps.TryGetValue(hvo, out valHvo))
					{
						valHvo = 0;
					}
					return valHvo;
				}
				return base.get_ObjectProp(hvo, tag);
			}

			public override void SetObjProp(int hvo, int tag, int hvoObj)
			{
				if (tag == ValueFlid)
				{
					m_objProps[hvo] = hvoObj;
				}
				else
				{
					base.SetObjProp(hvo, tag, hvoObj);
				}
			}
			public override string get_UnicodeProp(int hvo, int tag)
			{
				if (tag == PolarityFlid)
				{
					string str;
					if (!m_unicodeProps.TryGetValue(hvo, out str))
					{
						str = string.Empty;
					}
					return str;
				}
				return base.get_UnicodeProp(hvo, tag);
			}
			/// <summary>
			/// Called when processing the where clause of the area configuration XML
			/// </summary>
			public override ITsString get_StringProp(int hvo, int tag)
			{
				if (tag == PolarityFlid)
				{
					string str;
					if (!m_unicodeProps.TryGetValue(hvo, out str))
					{
						str = string.Empty;
					}
					return TsStringUtils.MakeString(str, m_cache.DefaultUserWs);
				}
				return base.get_StringProp(hvo, tag);
			}

			public override void SetUnicode(int hvo, int tag, string rgch, int cch)
			{
				if (tag == PolarityFlid)
				{
					m_unicodeProps[hvo] = rgch.Substring(0, cch);
				}
				else
				{
					base.SetUnicode(hvo, tag, rgch, cch);
				}
			}

			private sealed class PhonologicalFeatureMdc : LcmMetaDataCacheDecoratorBase
			{
				public PhonologicalFeatureMdc(IFwMetaDataCacheManaged mdc)
					: base(mdc)
				{
				}

				public override void AddVirtualProp(string bstrClass, string bstrField, int luFlid, int type)
				{
					throw new NotSupportedException();
				}

				public override int GetFieldId(string bstrClassName, string bstrFieldName, bool fIncludeBaseClasses)
				{
					if (bstrClassName == "FsClosedFeature" && bstrFieldName == "DummyPolarity")
					{
						return PolarityFlid;
					}

					if (bstrClassName == "FsClosedFeature" && bstrFieldName == "DummyValue")
					{
						return ValueFlid;
					}
					return base.GetFieldId(bstrClassName, bstrFieldName, fIncludeBaseClasses);
				}
				public override int GetFieldType(int luFlid)
				{
					return luFlid == PolarityFlid ? (int)CellarPropertyType.Unicode : base.GetFieldType(luFlid);
				}
			}
		}
	}
}
