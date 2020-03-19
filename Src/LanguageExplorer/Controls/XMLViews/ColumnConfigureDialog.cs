// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using SIL.FieldWorks.Common.Controls;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainServices;
using SIL.Xml;

namespace LanguageExplorer.Controls.XMLViews
{
	/// <summary />
	public class ColumnConfigureDialog : Form
	{
		private const string s_helpTopic = "khtpConfigureColumns";
		private Label label1;
		private Label label2;
		private ColumnHeader FieldColumn;
		private ColumnHeader InfoColumn;
		private Button okButton;
		private Button cancelButton;
		private Button helpButton;
		internal Button addButton;
		private Button removeButton;
		internal Button moveUpButton;
		internal Button moveDownButton;
		internal FwOverrideComboBox wsCombo;
		private Label label3;
		internal ListView currentList;
		private List<XElement> m_possibleColumns;
		private readonly LcmCache m_cache;
		private readonly IHelpTopicProvider m_helpTopicProvider;
		private bool m_fUpdatingWsCombo; // true during UpdateWsCombo
		private WsComboContent m_wccCurrent = WsComboContent.kwccNone;
		internal ListView optionsList;
		private HelpProvider helpProvider;
		private IContainer components;
		private ColumnHeader columnHeader1;
		private PictureBox blkEditIcon;
		private Label blkEditText;
		private ImageList imageList1;
		private ImageList imageList2;
		private bool showBulkEditIcons;

		/// <summary>
		/// Construct a column configure dialog. It is passed a list of XmlNodes that
		/// specify the possible columns, and another list, a subset of the first,
		/// of the ones currently displayed.
		/// </summary>
		public ColumnConfigureDialog(List<XElement> possibleColumns, List<XElement> currentColumns, IPropertyTable propertyTable)
		{
			m_possibleColumns = possibleColumns;
			CurrentSpecs = currentColumns;
			m_cache = propertyTable.GetValue<LcmCache>(FwUtils.cache);
			InitializeComponent();
			m_helpTopicProvider = propertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider);
			if (m_helpTopicProvider != null)
			{
				helpProvider.HelpNamespace = m_helpTopicProvider.HelpFile;
				helpProvider.SetHelpNavigator(this, HelpNavigator.Topic);
				helpProvider.SetHelpKeyword(this, m_helpTopicProvider.GetHelpString(s_helpTopic));
				helpProvider.SetShowHelp(this, true);
			}
			// Call FinishInitialization to do setup that might depend on previously setting
			// the root object (which is done by the caller after exiting the constructor.
		}

		/// <summary>
		/// Do dialog initialization that might depend on previously setting the root object.
		/// </summary>
		public void FinishInitialization()
		{
			InitCurrentList();
			InitChoicesList();
			InitWsCombo(WsComboContent.kwccNone);
			EnableControls();
		}

		/// <summary>
		/// Gets the current specs.
		/// </summary>
		public List<XElement> CurrentSpecs { get; }

		/// <summary>
		/// Gets or sets a value indicating whether [show bulk edit icons].
		/// </summary>
		/// <value><c>true</c> if [show bulk edit icons]; otherwise, <c>false</c>.</value>
		public bool ShowBulkEditIcons
		{
			get => showBulkEditIcons;
			set
			{
				showBulkEditIcons = value;
				blkEditIcon.Visible = blkEditText.Visible = showBulkEditIcons;
				currentList.SmallImageList = imageList1;
				optionsList.SmallImageList = imageList1;
				Refresh();
			}
		}

		/// <summary>
		/// Gets or sets the root object hvo.
		/// </summary>
		public int RootObjectHvo { get; set; }

		private void InitWsCombo(WsComboContent contentToDisplay, string wsLabel = "")
		{
			if (m_wccCurrent == contentToDisplay)
			{
				// We may have the correct content up, but we still need to make sure we've selected the correct thing
				SelectWsLabel(wsLabel);
				return;
			}
			wsCombo.Items.Clear();
			AddWritingSystemsToCombo(m_cache, wsCombo.Items, contentToDisplay);
			m_wccCurrent = contentToDisplay;
			SelectWsLabel(wsLabel);
		}

		private void SelectWsLabel(string wsLabel)
		{
			if (wsLabel == string.Empty)
			{
				return;
			}
			var itemToSelect = wsLabel;
			switch (wsLabel)
			{
				case "analysis vernacular":
				case "vernacular analysis":
					itemToSelect = wsLabel.Split(' ')[0];
					break;
			}
			foreach (WsComboItem item in wsCombo.Items)
			{
				if (item.Id == itemToSelect)
				{
					wsCombo.SelectedItem = item;
					break;
				}
			}
		}

		/// <summary>
		/// Initialize the combo box for the set of reversal index writing systems.
		/// </summary>
		private void InitWsComboForReversalIndexes()
		{
			if (m_wccCurrent == WsComboContent.kwccReversalIndexes)
			{
				return;
			}
			wsCombo.Items.Clear();
			using (var ie = m_cache.LanguageProject.LexDbOA.ReversalIndexesOC.GetEnumerator())
			{
				var rgWs = new CoreWritingSystemDefinition[m_cache.LanguageProject.LexDbOA.ReversalIndexesOC.Count];
				for (var i = 0; i < rgWs.Length; ++i)
				{
					if (!ie.MoveNext())
					{
						throw new Exception("The IEnumerator failed to move to an existing Reversal Index???");
					}
					var ri = ie.Current;
					rgWs[i] = m_cache.ServiceLocator.WritingSystemManager.Get(ri.WritingSystem);
				}
				var fSort = wsCombo.Sorted;
				wsCombo.Sorted = true;
				AddWritingSystemsToCombo(m_cache, wsCombo.Items, rgWs);
				wsCombo.Sorted = fSort;
				m_wccCurrent = WsComboContent.kwccReversalIndexes;
				wsCombo.Enabled = true;
				var sDefaultRevWsName = GetDefaultReversalWsName();
				var idx = -1;
				for (var i = 0; i < wsCombo.Items.Count; ++i)
				{
					if (wsCombo.Items[i] is WsComboItem item)
					{
						if (item.ToString() == sDefaultRevWsName)
						{
							idx = i;
							break;
						}
					}
				}
				Debug.Assert(idx >= 0);
				wsCombo.SelectedIndex = idx >= 0 ? idx : 0;
			}
		}

		private void InitWsComboForReversalIndex()
		{
			if (m_wccCurrent == WsComboContent.kwccReversalIndex)
			{
				return;
			}
			wsCombo.Items.Clear();
			var ri = m_cache.ServiceLocator.GetInstance<IReversalIndexRepository>().GetObject(RootObjectHvo);
			var sLang = m_cache.ServiceLocator.WritingSystemManager.Get(ri.WritingSystem).Language;
			var fSort = wsCombo.Sorted;
			foreach (var ws in WritingSystemServices.GetReversalIndexWritingSystems(m_cache, ri.Hvo, false).Where(ws => ws.Language == sLang))
			{
				wsCombo.Items.Add(new WsComboItem(ws.DisplayLabel, ws.Id));
			}
			wsCombo.Sorted = fSort;
			m_wccCurrent = WsComboContent.kwccReversalIndex;
			wsCombo.Enabled = true;
			wsCombo.SelectedIndex = 0;
		}

		/// <summary>
		/// Initialize the combo box for the standard set of writing systems.
		/// </summary>
		public static void AddWritingSystemsToCombo(LcmCache cache, ComboBox.ObjectCollection items, WsComboContent contentToAdd)
		{
			AddWritingSystemsToCombo(cache, items, contentToAdd, false, false);
		}

		/// <summary>
		/// Initialize the combo box for the standard set of writing systems.
		/// </summary>
		public static void AddWritingSystemsToCombo(LcmCache cache, ComboBox.ObjectCollection items, WsComboContent contentToAdd, bool skipDefaults)
		{
			AddWritingSystemsToCombo(cache, items, contentToAdd, skipDefaults, false);
		}

		/// <summary>
		/// Initialize the combo box for the standard set of writing systems.
		/// </summary>
		/// <param name="cache"></param>
		/// <param name="items"></param>
		/// <param name="contentToAdd"></param>
		/// <param name="skipDefaults">true if we do NOT want to see the default items, but only
		/// the actual writing systems</param>
		/// <param name="allowMultiple">true to allow values that generate multiple writing
		/// systems, like "all analysis". Used by ConfigureFieldDlg for Dictionary views. Also
		/// adds all reasonable single generic items not already included by skipDefaults.
		/// Ignored if skipDefaults is true.</param>
		/// <remarks>This is static because ConfigureInterlinDialog uses it</remarks>
		public static void AddWritingSystemsToCombo(LcmCache cache, ComboBox.ObjectCollection items, WsComboContent contentToAdd, bool skipDefaults, bool allowMultiple)
		{
			var sAllAnal = XMLViewsStrings.ksAllAnal;
			var sAllAnalVern = XMLViewsStrings.ksAllAnalVern;
			var sAllPron = XMLViewsStrings.ksAllPron;
			var sAllVern = XMLViewsStrings.ksAllVern;
			var sAllVernAnal = XMLViewsStrings.ksAllVernAnal;
			var sBestAnal = XMLViewsStrings.ksBestAnal;
			var sBestAnalVern = XMLViewsStrings.ksBestAnalVern;
			var sBestVern = XMLViewsStrings.ksBestVern;
			var sBestVernAnal = XMLViewsStrings.ksBestVernAnal;
			var sDefaultAnal = XMLViewsStrings.ksDefaultAnal;
			var sDefaultPron = XMLViewsStrings.ksDefaultPron;
			var sDefaultVern = XMLViewsStrings.ksDefaultVern;
			var sVernacularInPara = XMLViewsStrings.ksVernacularInParagraph;
			switch (contentToAdd)
			{
				case WsComboContent.kwccNone:
					break;
				case WsComboContent.kwccVernAndAnal:
					if (!skipDefaults)
					{
						items.Add(new WsComboItem(sDefaultVern, "vernacular"));
						items.Add(new WsComboItem(sDefaultAnal, "analysis"));
						if (allowMultiple)
						{
							items.Add(new WsComboItem(sAllVern, "all vernacular"));
							items.Add(new WsComboItem(sAllAnal, "all analysis"));
							items.Add(new WsComboItem(sAllVernAnal, "vernacular analysis"));
							items.Add(new WsComboItem(sAllAnalVern, "analysis vernacular"));
							items.Add(new WsComboItem(sBestVernAnal, "best vernoranal"));
							items.Add(new WsComboItem(sBestAnalVern, "best analorvern"));
						}
					}
					AddWritingSystemsToCombo(cache, items, cache.ServiceLocator.WritingSystems.CurrentVernacularWritingSystems);
					AddWritingSystemsToCombo(cache, items, cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems);
					break;
				case WsComboContent.kwccAnalAndVern:
					if (!skipDefaults)
					{
						items.Add(new WsComboItem(sDefaultAnal, "analysis"));
						items.Add(new WsComboItem(sDefaultVern, "vernacular"));
						if (allowMultiple)
						{
							items.Add(new WsComboItem(sAllAnal, "all analysis"));
							items.Add(new WsComboItem(sAllVern, "all vernacular"));
							items.Add(new WsComboItem(sAllAnalVern, "analysis vernacular"));
							items.Add(new WsComboItem(sAllVernAnal, "vernacular analysis"));
							items.Add(new WsComboItem(sBestAnalVern, "best analorvern"));
							items.Add(new WsComboItem(sBestVernAnal, "best vernoranal"));
						}
					}
					AddWritingSystemsToCombo(cache, items, cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems);
					AddWritingSystemsToCombo(cache, items, cache.ServiceLocator.WritingSystems.CurrentVernacularWritingSystems);
					break;
				case WsComboContent.kwccBestAnalOrVern:
					if (!skipDefaults)
					{
						items.Add(new WsComboItem(sBestAnalVern, "best analorvern"));
						if (allowMultiple)
						{
							items.Add(new WsComboItem(sDefaultAnal, "analysis"));
							items.Add(new WsComboItem(sAllAnal, "all analysis"));
							items.Add(new WsComboItem(sAllAnalVern, "analysis vernacular"));
							items.Add(new WsComboItem(sDefaultVern, "vernacular"));
							items.Add(new WsComboItem(sAllVern, "all vernacular"));
							items.Add(new WsComboItem(sAllVernAnal, "vernacular analysis"));
							items.Add(new WsComboItem(sBestVernAnal, "best vernoranal"));
						}
					}
					AddWritingSystemsToCombo(cache, items, cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems);
					AddWritingSystemsToCombo(cache, items, cache.ServiceLocator.WritingSystems.CurrentVernacularWritingSystems);
					break;
				case WsComboContent.kwccBestAnalysis:
					if (!skipDefaults)
					{
						items.Add(new WsComboItem(sBestAnal, "best analysis"));
						if (allowMultiple)
						{
							items.Add(new WsComboItem(sDefaultAnal, "analysis"));
							items.Add(new WsComboItem(sAllAnal, "all analysis"));
						}
					}
					AddWritingSystemsToCombo(cache, items, cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems);
					break;
				case WsComboContent.kwccBestVernacular:
					if (!skipDefaults)
					{
						items.Add(new WsComboItem(sBestVern, "best vernacular"));
						if (allowMultiple)
						{
							items.Add(new WsComboItem(sDefaultVern, "vernacular"));
							items.Add(new WsComboItem(sAllVern, "all vernacular"));
						}
					}
					AddWritingSystemsToCombo(cache, items, cache.ServiceLocator.WritingSystems.CurrentVernacularWritingSystems);
					break;
				case WsComboContent.kwccBestVernOrAnal:
					if (!skipDefaults)
					{
						items.Add(new WsComboItem(sBestVernAnal, "best vernoranal"));
						if (allowMultiple)
						{
							items.Add(new WsComboItem(sDefaultVern, "vernacular"));
							items.Add(new WsComboItem(sAllVern, "all vernacular"));
							items.Add(new WsComboItem(sAllVernAnal, "vernacular analysis"));
							items.Add(new WsComboItem(sDefaultAnal, "analysis"));
							items.Add(new WsComboItem(sAllAnal, "all analysis"));
							items.Add(new WsComboItem(sAllAnalVern, "analysis vernacular"));
							items.Add(new WsComboItem(sBestAnalVern, "best analorvern"));
						}
					}
					AddWritingSystemsToCombo(cache, items, cache.ServiceLocator.WritingSystems.CurrentVernacularWritingSystems);
					AddWritingSystemsToCombo(cache, items, cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems);
					break;
				case WsComboContent.kwccAnalysis:
					if (!skipDefaults)
					{
						items.Add(new WsComboItem(sDefaultAnal, "analysis"));
						if (allowMultiple)
						{
							items.Add(new WsComboItem(sBestAnal, "best analysis"));
							items.Add(new WsComboItem(sAllAnal, "all analysis"));
						}
					}
					AddWritingSystemsToCombo(cache, items, cache.ServiceLocator.WritingSystems.CurrentAnalysisWritingSystems);
					break;
				case WsComboContent.kwccVernacularInParagraph:
					items.Add(new WsComboItem(sVernacularInPara, "vern in para"));
					AddWritingSystemsToCombo(cache, items, cache.ServiceLocator.WritingSystems.CurrentVernacularWritingSystems);
					break;
				case WsComboContent.kwccVernacular:
					if (!skipDefaults)
					{
						items.Add(new WsComboItem(sDefaultVern, "vernacular"));
						if (allowMultiple)
						{
							items.Add(new WsComboItem(sBestVern, "best vernacular"));
							items.Add(new WsComboItem(sAllVern, "all vernacular"));
						}
					}
					AddWritingSystemsToCombo(cache, items, cache.ServiceLocator.WritingSystems.CurrentVernacularWritingSystems);
					break;
				case WsComboContent.kwccPronunciation:
					if (!skipDefaults)
					{
						items.Add(new WsComboItem(sDefaultPron, "pronunciation"));
						if (allowMultiple)
						{
							items.Add(new WsComboItem(sAllPron, "all pronunciation"));
						}
					}
					AddWritingSystemsToCombo(cache, items, cache.ServiceLocator.WritingSystems.CurrentPronunciationWritingSystems);
					break;
				default:
					throw new NotSupportedException($"AddWritingSystemsToCombo does not know how to add {contentToAdd} content.");
			}
		}

		/// <summary>
		/// Adds the writing systems to combo.
		/// </summary>
		public static void AddWritingSystemsToCombo(LcmCache cache, ComboBox.ObjectCollection items, IEnumerable<CoreWritingSystemDefinition> wss)
		{
			foreach (var ws in wss)
			{
				items.Add(new WsComboItem(ws.DisplayLabel, ws.Id));
			}
		}

		private void InitChoicesList()
		{
			// LT-12253 It's just possible that AddCurrentItem() will delete a column
			// (e.g. if the user previously deleted a ws that it references).
			// So don't use foreach here!
			for (var i = 0; i < CurrentSpecs.Count; i++)
			{
				var node = CurrentSpecs[i];
				var item = AddCurrentItem(node);
				if (item == null)
				{
					i--;
				}
			}
		}

		/// <summary>
		/// Creates the ListViewItem for the current Xml node.
		/// </summary>
		/// <returns>The ListViewItem or null. If null is returned, the caller should delete this
		/// column from the current list.</returns>
		private ListViewItem MakeCurrentItem(XElement node)
		{
			var cols = new string[2];
			var label = StringTable.Table.LocalizeAttributeValue(XmlUtils.GetOptionalAttributeValue(node, "label", null)) ?? XmlUtils.GetMandatoryAttributeValue(node, "label");
			cols[0] = label;
			var wsParam = XmlViewsUtils.FindWsParam(node);
			var dispCategory = TranslateWsParamToLocalizedDisplayCategory(wsParam);
			cols[1] = dispCategory;
			// Failure to translate (Empty string result) means either:
			//    1) wsParam is a specific Writing System... look up how to display it.
			// or 2) the user deleted the Writing System... try to revert to a default ws
			//       unless there is a column for that already, in which case return null
			//       so we can delete this column.
			if (string.IsNullOrEmpty(dispCategory) && !string.IsNullOrEmpty(wsParam))
			{
				// Display the language name, not its ICU locale.
				CoreWritingSystemDefinition ws;
				if (m_cache.ServiceLocator.WritingSystemManager.TryGet(wsParam, out ws))
				{
					cols[1] = ws.DisplayLabel;
				}
				else
				{
					// Probably this ws was deleted. See LT-12253.
					if (!TryToRevertToDefaultWs(node, out var newColName, out var newWsDispCat))
					{
						// Caller should delete this node from the current list of columns.
						return null;
					}
					cols[0] = newColName;
					cols[1] = newWsDispCat;
				}
			}
			var itemWithToolTip = new ListViewItem(cols)
			{
				ToolTipText = cols[1]
			};
			if (XmlUtils.GetOptionalAttributeValue(node, "bulkEdit") != null || XmlUtils.GetOptionalAttributeValue(node, "transduce") != null)
			{
				itemWithToolTip.ImageIndex = 0;
			}
			return itemWithToolTip;
		}

		private string TranslateWsParamToLocalizedDisplayCategory(string wsParam)
		{
			var result = string.Empty; // if the switch doesn't match wsParam, this will be returned.
			switch (wsParam)
			{
				case "analysis":
					result = XMLViewsStrings.ksDefaultAnal;
					break;
				case "vernacular":
					result = XMLViewsStrings.ksDefaultVern;
					break;
				case "pronunciation":
					result = XMLViewsStrings.ksDefaultPron;
					break;
				case "best vernoranal":
					result = XMLViewsStrings.ksBestVernAnal;
					break;
				case "best analorvern":
					result = XMLViewsStrings.ksBestAnalVern;
					break;
				case "best analysis":
					result = XMLViewsStrings.ksBestAnal;
					break;
				case "best vernacular":
					result = XMLViewsStrings.ksBestVern;
					break;
				case "reversal":
					{
						// Get the language for this reversal index.
						string sWsName = null;
						if (RootObjectHvo > 0)
						{
							var servLoc = m_cache.ServiceLocator;
							var ri = servLoc.GetInstance<IReversalIndexRepository>().GetObject(RootObjectHvo);
							//var ws = servLoc.WritingSystemManager.Get(ri.WritingSystem);
							//sWsName = ws.DisplayLabel;
							sWsName = ri.ShortName;
						}
						if (string.IsNullOrEmpty(sWsName))
						{
							sWsName = GetDefaultReversalWsName();
						}
						if (!string.IsNullOrEmpty(sWsName))
						{
							result = sWsName;
						}
					}
					break;
				//case "reversal index": // ??? is this case used? Nope.
				//    break;
				case "analysis vernacular":
					result = XMLViewsStrings.ksDefaultAnal;
					break;
				case "vernacular analysis":
					result = XMLViewsStrings.ksDefaultVern;
					break;
			}
			return result;
		}

		private bool TryToRevertToDefaultWs(XElement node, out string newColName, out string newWsDispCat)
		{
			newColName = string.Empty;
			newWsDispCat = string.Empty;
			var origWs = XmlUtils.GetOptionalAttributeValue(node, "originalWs");
			if (origWs == null)
			{
				return false;
			}
			var origDisplayCategory = TranslateWsParamToLocalizedDisplayCategory(origWs);
			var origLabel = XmlUtils.GetOptionalAttributeValue(node, "originalLabel");
			if (string.IsNullOrEmpty(origLabel))
			{
				return false; // trash this bizarre column!
			}
			if (CurrentColumnsContainsOriginalDefault(origLabel, origWs))
			{
				return false;
			}
			var dispName = UpdateNodeToReflectDefaultWs(node, origWs);
			if (!string.IsNullOrEmpty(dispName))
			{
				newColName = dispName;
			}
			newWsDispCat = origDisplayCategory;
			return true;
		}

		private string UpdateNodeToReflectDefaultWs(XElement node, string origWs)
		{
			var result = string.Empty;
			if (!node.HasAttributes)
			{
				return result;
			}
			const string wsAttrName = "ws";
			if (XmlUtils.GetOptionalAttributeValue(node, wsAttrName) == null)
			{
				return result;
			}
			XmlUtils.SetAttribute(node, wsAttrName, StringServices.WsParamLabel + origWs);
			// reset 'label' attribute to 'originalLabel'
			const string origLabelAttrName = "originalLabel";
			var origLabel = XmlUtils.GetOptionalAttributeValue(node, origLabelAttrName);
			if (origLabel == null)
			{
				return result;
			}
			result = origLabel;
			const string origWsAttrName = "originalWs";
			XmlUtils.SetAttribute(node, "label", origLabel);
			// remove 'originalLabel' and 'originalWs' attributes
			node.Attribute(origLabelAttrName).Remove();
			node.Attribute(origWsAttrName).Remove();
			return result;
		}

		private bool CurrentColumnsContainsOriginalDefault(string label, string origWs)
		{
			// Search through m_currentColumns for one that has the same label attribute
			// and original writing system.
			foreach (var col in CurrentSpecs)
			{
				var colLabel = XmlUtils.GetOptionalAttributeValue(col, "label");
				if (label != colLabel)
				{
					continue;
				}
				var wsParam = XmlViewsUtils.FindWsParam(col);
				if (wsParam == origWs)
				{
					return true;
				}
			}
			return false;
		}

		private string GetDefaultReversalWsName()
		{
			IReversalIndex ri = null;
			var rgriCurrent = m_cache.LangProject.LexDbOA.CurrentReversalIndices;
			if (rgriCurrent.Count > 0)
			{
				ri = rgriCurrent[0];
			}
			else if (m_cache.LangProject.LexDbOA.ReversalIndexesOC.Count > 0)
			{
				ri = m_cache.LangProject.LexDbOA.ReversalIndexesOC.ToArray()[0];
			}
			return ri != null ? m_cache.ServiceLocator.WritingSystemManager.Get(ri.WritingSystem).DisplayLabel : null;
		}

		private ListViewItem AddCurrentItem(XElement node)
		{
			var item = MakeCurrentItem(node);
			// Should only occur if user deleted this ws
			if (item == null)
			{
				if (CurrentSpecs.Contains(node))
				{
					CurrentSpecs.Remove(node);
				}
			}
			else
			{
				currentList.Items.Add(item);
			}
			return item;
		}

		private void InitCurrentList()
		{
			IComparer<XElement> columnSorter = new ColumnSorter();
			var firstIndex = 0;
			var count = m_possibleColumns.Count;
			if (m_possibleColumns.Count > 0 && m_possibleColumns[0].Parent != null)
			{
				// The parent columns element may specify that the first few items are to be left in place and not sorted.
				var leadingUnsortedColumns = XmlUtils.GetOptionalIntegerValue(m_possibleColumns[0].Parent, "leadingUnsortedColumns", 0);
				firstIndex += leadingUnsortedColumns;
				count -= leadingUnsortedColumns;
			}
			m_possibleColumns.Sort(firstIndex, count, columnSorter); // Sort the list before it's displayed
			SafelyMakeOptionsList(optionsList);
		}

		private void SafelyMakeOptionsList(ListView optionsList)
		{
			foreach (var listItem in m_possibleColumns.Select(MakeCurrentItem).Where(listItem => listItem != null))
			{
				optionsList.Items.Add(listItem);
			}
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
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ColumnConfigureDialog));
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.currentList = new System.Windows.Forms.ListView();
			this.FieldColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.InfoColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.okButton = new System.Windows.Forms.Button();
			this.cancelButton = new System.Windows.Forms.Button();
			this.helpButton = new System.Windows.Forms.Button();
			this.addButton = new System.Windows.Forms.Button();
			this.removeButton = new System.Windows.Forms.Button();
			this.moveUpButton = new System.Windows.Forms.Button();
			this.imageList2 = new System.Windows.Forms.ImageList(this.components);
			this.moveDownButton = new System.Windows.Forms.Button();
			this.wsCombo = new SIL.FieldWorks.Common.Controls.FwOverrideComboBox();
			this.label3 = new System.Windows.Forms.Label();
			this.optionsList = new System.Windows.Forms.ListView();
			this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.helpProvider = new System.Windows.Forms.HelpProvider();
			this.imageList1 = new System.Windows.Forms.ImageList(this.components);
			this.blkEditIcon = new System.Windows.Forms.PictureBox();
			this.blkEditText = new System.Windows.Forms.Label();
			((System.ComponentModel.ISupportInitialize)(this.blkEditIcon)).BeginInit();
			this.SuspendLayout();
			//
			// label1
			//
			resources.ApplyResources(this.label1, "label1");
			this.label1.Name = "label1";
			this.helpProvider.SetShowHelp(this.label1, ((bool)(resources.GetObject("label1.ShowHelp"))));
			//
			// label2
			//
			resources.ApplyResources(this.label2, "label2");
			this.label2.Name = "label2";
			this.helpProvider.SetShowHelp(this.label2, ((bool)(resources.GetObject("label2.ShowHelp"))));
			//
			// currentList
			//
			resources.ApplyResources(this.currentList, "currentList");
			this.currentList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
			this.FieldColumn,
			this.InfoColumn});
			this.currentList.FullRowSelect = true;
			this.currentList.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
			this.currentList.HideSelection = false;
			this.currentList.MultiSelect = false;
			this.currentList.Name = "currentList";
			this.currentList.ShowItemToolTips = true;
			this.currentList.UseCompatibleStateImageBehavior = false;
			this.currentList.View = System.Windows.Forms.View.Details;
			this.currentList.SelectedIndexChanged += new System.EventHandler(this.currentList_SelectedIndexChanged);
			this.currentList.DoubleClick += new System.EventHandler(this.removeButton_Click);
			//
			// FieldColumn
			//
			resources.ApplyResources(this.FieldColumn, "FieldColumn");
			//
			// InfoColumn
			//
			resources.ApplyResources(this.InfoColumn, "InfoColumn");
			//
			// okButton
			//
			resources.ApplyResources(this.okButton, "okButton");
			this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
			this.okButton.Name = "okButton";
			//
			// cancelButton
			//
			resources.ApplyResources(this.cancelButton, "cancelButton");
			this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.cancelButton.Name = "cancelButton";
			//
			// helpButton
			//
			resources.ApplyResources(this.helpButton, "helpButton");
			this.helpButton.Name = "helpButton";
			this.helpButton.Click += new System.EventHandler(this.helpButton_Click);
			//
			// addButton
			//
			resources.ApplyResources(this.addButton, "addButton");
			this.addButton.Name = "addButton";
			this.addButton.Click += new System.EventHandler(this.addButton_Click);
			//
			// removeButton
			//
			resources.ApplyResources(this.removeButton, "removeButton");
			this.removeButton.Name = "removeButton";
			this.removeButton.Click += new System.EventHandler(this.removeButton_Click);
			//
			// moveUpButton
			//
			resources.ApplyResources(this.moveUpButton, "moveUpButton");
			this.moveUpButton.ImageList = this.imageList2;
			this.moveUpButton.Name = "moveUpButton";
			this.moveUpButton.Click += new System.EventHandler(this.moveUpButton_Click);
			//
			// imageList2
			//
			this.imageList2.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList2.ImageStream")));
			this.imageList2.TransparentColor = System.Drawing.Color.Magenta;
			this.imageList2.Images.SetKeyName(0, "LargeUpArrow.bmp");
			this.imageList2.Images.SetKeyName(1, "LargeDownArrow.bmp");
			//
			// moveDownButton
			//
			resources.ApplyResources(this.moveDownButton, "moveDownButton");
			this.moveDownButton.ImageList = this.imageList2;
			this.moveDownButton.Name = "moveDownButton";
			this.moveDownButton.Click += new System.EventHandler(this.moveDownButton_Click);
			//
			// wsCombo
			//
			this.wsCombo.AllowSpaceInEditBox = false;
			resources.ApplyResources(this.wsCombo, "wsCombo");
			this.wsCombo.Name = "wsCombo";
			this.wsCombo.SelectedIndexChanged += new System.EventHandler(this.wsCombo_SelectedIndexChanged);
			//
			// label3
			//
			resources.ApplyResources(this.label3, "label3");
			this.label3.Name = "label3";
			//
			// optionsList
			//
			resources.ApplyResources(this.optionsList, "optionsList");
			this.optionsList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
			this.columnHeader1});
			this.optionsList.FullRowSelect = true;
			this.optionsList.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
			this.optionsList.HideSelection = false;
			this.optionsList.MultiSelect = false;
			this.optionsList.Name = "optionsList";
			this.optionsList.UseCompatibleStateImageBehavior = false;
			this.optionsList.View = System.Windows.Forms.View.Details;
			this.optionsList.SelectedIndexChanged += new System.EventHandler(this.optionsList_SelectedIndexChanged_1);
			this.optionsList.DoubleClick += new System.EventHandler(this.addButton_Click);
			//
			// columnHeader1
			//
			resources.ApplyResources(this.columnHeader1, "columnHeader1");
			//
			// imageList1
			//
			this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
			this.imageList1.TransparentColor = System.Drawing.Color.Magenta;
			this.imageList1.Images.SetKeyName(0, "");
			//
			// blkEditIcon
			//
			resources.ApplyResources(this.blkEditIcon, "blkEditIcon");
			this.blkEditIcon.Name = "blkEditIcon";
			this.blkEditIcon.TabStop = false;
			//
			// blkEditText
			//
			resources.ApplyResources(this.blkEditText, "blkEditText");
			this.blkEditText.Name = "blkEditText";
			//
			// ColumnConfigureDialog
			//
			this.AcceptButton = this.okButton;
			resources.ApplyResources(this, "$this");
			this.CancelButton = this.cancelButton;
			this.Controls.Add(this.blkEditText);
			this.Controls.Add(this.blkEditIcon);
			this.Controls.Add(this.optionsList);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.wsCombo);
			this.Controls.Add(this.moveDownButton);
			this.Controls.Add(this.moveUpButton);
			this.Controls.Add(this.removeButton);
			this.Controls.Add(this.addButton);
			this.Controls.Add(this.helpButton);
			this.Controls.Add(this.cancelButton);
			this.Controls.Add(this.okButton);
			this.Controls.Add(this.currentList);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.label1);
			this.helpProvider.SetHelpNavigator(this, ((System.Windows.Forms.HelpNavigator)(resources.GetObject("$this.HelpNavigator"))));
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "ColumnConfigureDialog";
			this.helpProvider.SetShowHelp(this, ((bool)(resources.GetObject("$this.ShowHelp"))));
			this.ShowInTaskbar = false;
			this.TransparencyKey = System.Drawing.Color.Fuchsia;
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ColumnConfigureDialog_FormClosing);
			((System.ComponentModel.ISupportInitialize)(this.blkEditIcon)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}
		#endregion

		private void ColumnConfigureDialog_FormClosing(object sender, FormClosingEventArgs e)
		{
			// We only need to validate the choices if the user clicked OK
			if (DialogResult != DialogResult.OK)
			{
				return;
			}
			if (HasDuplicateColumns())
			{
				ShowDuplicatesWarning(GetDuplicateColumns());
				e.Cancel = true;
			}
		}

		private static void ShowDuplicatesWarning(List<string> duplicateColumnLabels)
		{
			MessageBox.Show(string.Format(XMLViewsStrings.ksDuplicateColumnMsg, string.Join(", ", duplicateColumnLabels.ToArray())), XMLViewsStrings.ksDuplicateColumn, MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}

		private bool HasDuplicateColumns()
		{
			return (GetDuplicateColumns().Count > 0);
		}

		private List<string> GetDuplicateColumns()
		{
			var duplicateColumnLabels = new List<string>();
			for (var i = 0; i < CurrentSpecs.Count; i++)
			{
				var label = GetColumnLabel(i);
				var wsParam = XmlViewsUtils.FindWsParam(CurrentSpecs[i]);
				// This tries to interpret the ws parameter into an int.  Sometimes the parameter cannot be interpreted without an object,
				// such as when the ws is a magic string that will change the actual ws depending on the contents of the object.
				// In these cases, we give -50 as a known constant to check for and will just compare the string version of the
				// ws parameter.  This can can possibly throw an exception, so we'll enclose it in a try block.
				var ws = -50;
				var wsMagic = 0;
				try
				{
					if (!XmlViewsUtils.GetWsRequiresObject(wsParam))
					{
						ws = WritingSystemServices.InterpretWsLabel(m_cache, wsParam, null, 0, 0, null, out wsMagic);
					}
				}
				catch { }
				for (var j = 0; j < CurrentSpecs.Count; j++)
				{
					// No need to check against our own node
					if (j == i)
					{
						continue;
					}
					var sameSpec = false;
					var otherLabel = GetColumnLabel(j);
					if (label != otherLabel)
					{
						continue;
					}
					var otherWsParam = XmlViewsUtils.FindWsParam(CurrentSpecs[j]);
					// If the ws is not -50, then we know to compare against integer ws codes, not string labels
					if (ws != -50)
					{
						if (ws == WritingSystemServices.InterpretWsLabel(m_cache, otherWsParam, null, 0, 0, null, out var wsOtherMagic) && wsMagic == wsOtherMagic)
						{
							sameSpec = true;
						}
					}
					else
					{
						if (wsParam == otherWsParam)
						{
							sameSpec = true;
						}
					}
					if (sameSpec) // Found a duplicate column.
					{
						if (!duplicateColumnLabels.Contains(label)) // Don't add the same label twice!
						{
							duplicateColumnLabels.Add(label);
						}
					}
				}
			}
			return duplicateColumnLabels;
		}

		private string GetColumnLabel(int columnIndex)
		{
			return (StringTable.Table.LocalizeAttributeValue(XmlUtils.GetOptionalAttributeValue(CurrentSpecs[columnIndex], "originalLabel", null))
					?? StringTable.Table.LocalizeAttributeValue(XmlUtils.GetOptionalAttributeValue(CurrentSpecs[columnIndex], "label", null)))
					?? XmlUtils.GetMandatoryAttributeValue(CurrentSpecs[columnIndex], "label");
		}

		private void addButton_Click(object sender, EventArgs e)
		{
			var columnBeingAdded = m_possibleColumns[optionsList.SelectedIndices[0]];
			CurrentSpecs.Add(columnBeingAdded);
			var index = CurrentListIndex;
			if (index >= 0)
			{
				currentList.Items[index].Selected = false;
			}
			var currentItem = AddCurrentItem(columnBeingAdded);
			currentItem.Selected = true;
			//When adding the columnBeingAdded, try to adjust the label so that it is unique. This happens when
			//the column is already one that exists in the list of currentColumns.
			while (ColumnHasWsParam(columnBeingAdded) && ColumnHasAsDuplicate(columnBeingAdded) && (wsCombo.SelectedIndex < wsCombo.Items.Count) && wsCombo.Items.Count > 0)
			{
				if (wsCombo.SelectedIndex.Equals(wsCombo.Items.Count - 1))
				{
					wsCombo.SelectedIndex = 0;
					UpdateWsAndLabelOfCurrentColumn();
					break;
				}
				wsCombo.SelectedIndex++;
			}
			//Warn the user if the column being added has a duplicate in the currentColumns list and it was not
			//possible to create a unique label for it.
			if (ColumnHasAsDuplicate(columnBeingAdded))
			{
				ShowDuplicatesWarning(GetDuplicateColumns());
			}
			// Select the item in the ws combo box by its name (see MakeCurrentItem method for details of item construction)
			wsCombo.SelectedItem = currentItem.SubItems[1];
			currentList.Focus();
		}

		//Some fields such as Sense have no $ws attribute. Other fields such as Form do have this attribute.
		//This information is used to determine if a unique column label should be created.
		private static bool ColumnHasWsParam(XElement columnBeingAdded)
		{
			return !string.IsNullOrEmpty(XmlViewsUtils.FindWsParam(columnBeingAdded));
		}

		private bool ColumnHasAsDuplicate(XElement colSpec)
		{
			return GetDuplicateColumns().Contains(colSpec.Attribute("label").Value);
		}

		private void removeButton_Click(object sender, EventArgs e)
		{
			var index = CurrentListIndex;
			if (index < 0 || currentList.Items.Count == 1)
			{
				return;
			}
			currentList.Items.RemoveAt(index);
			CurrentSpecs.RemoveAt(index);
			// Select the next logical item
			if (index < currentList.Items.Count)
			{
				currentList.Items[index].Selected = true;
			}
			else
			{
				currentList.Items[currentList.Items.Count - 1].Selected = true;
			}
			currentList.Select();
		}

		private void moveUpButton_Click(object sender, EventArgs e)
		{
			var index = CurrentListIndex;
			if (index <= 0)
			{
				return; // should be disabled, but play safe.
			}
			var itemMove = CurrentSpecs[index];
			CurrentSpecs[index] = CurrentSpecs[index - 1];
			CurrentSpecs[index - 1] = itemMove;
			var listItemMove = currentList.Items[index];
			currentList.Items.RemoveAt(index);
			currentList.Items.Insert(index - 1, listItemMove);
			listItemMove.Selected = true;
		}

		private void moveDownButton_Click(object sender, EventArgs e)
		{
			var index = CurrentListIndex;
			if (index < 0 || index >= CurrentSpecs.Count - 1)
			{
				return; // should be disabled, but play safe.
			}
			var itemMove = CurrentSpecs[index];
			CurrentSpecs[index] = CurrentSpecs[index + 1];
			CurrentSpecs[index + 1] = itemMove;
			var listItemMove = currentList.Items[index];
			currentList.Items.RemoveAt(index);
			currentList.Items.Insert(index + 1, listItemMove);
			listItemMove.Selected = true;
		}

		private void currentList_SelectedIndexChanged(object sender, System.EventArgs e)
		{
			EnableControls();
		}

		private void EnableControls()
		{
			addButton.Enabled = optionsList.SelectedIndices.Count > 0 && optionsList.SelectedIndices[0] >= 0;
			var index = CurrentListIndex;
			removeButton.Enabled = index >= 0 && currentList.Items.Count > 1;
			moveUpButton.Enabled = index > 0;
			moveDownButton.Enabled = index >= 0 && index < currentList.Items.Count - 1;
			if (index >= 0)
			{
				// The ordering of these next two statements is critical.  We need to enable the combobox by default because we have
				// a valid selection on the current list.  However, if UpdateWsComboValue finds out that the ws for that particular
				// field is not configurable, it will disable it.
				wsCombo.Enabled = true;
				UpdateWsComboValue();
			}
			else
			{
				wsCombo.Enabled = false;
			}
		}

		private int CurrentListIndex => currentList.SelectedIndices.Count == 0 ? -1 : currentList.SelectedIndices[0];

		private void UpdateWsComboValue()
		{
			try
			{
				m_fUpdatingWsCombo = true;
				var index = CurrentListIndex;
				if (index < 0 || index >= CurrentSpecs.Count)
				{
					return;
				}
				var node = CurrentSpecs[index];
				var wsLabel = XmlViewsUtils.FindWsParam(node);
				if (wsLabel == string.Empty)
				{
					wsCombo.SelectedIndex = -1;
					wsCombo.Enabled = false;
					wsLabel = XmlUtils.GetOptionalAttributeValue(node, "ws");
				}
				if (string.IsNullOrEmpty(wsLabel))
				{
					return;
				}
				var wsForOptions = XmlUtils.GetOptionalAttributeValue(node, "originalWs", wsLabel);
				switch (wsForOptions)
				{
					case "reversal":
						Debug.Assert(RootObjectHvo != 0);
						var clid = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(RootObjectHvo).ClassID;
						switch (clid)
						{
							case ReversalIndexTags.kClassId:
								InitWsComboForReversalIndex();
								break;
							default:
								InitWsComboForReversalIndexes();
								break;
						}
						break;
					case "vern in para":
						InitWsCombo(WsComboContent.kwccVernacularInParagraph, wsLabel);
						break;
					case "analysis":
						InitWsCombo(WsComboContent.kwccAnalysis, wsLabel);
						break;
					case "vernacular":
						InitWsCombo(WsComboContent.kwccVernacular, wsLabel);
						break;
					case "pronunciation":
						InitWsCombo(WsComboContent.kwccPronunciation, wsLabel);
						break;
					case "best vernoranal":
						InitWsCombo(WsComboContent.kwccBestVernOrAnal, wsLabel);
						break;
					case "best analorvern":
						InitWsCombo(WsComboContent.kwccBestAnalOrVern, wsLabel);
						break;
					case "best analysis":
						InitWsCombo(WsComboContent.kwccBestAnalysis, wsLabel);
						break;
					case "best vernacular":
						InitWsCombo(WsComboContent.kwccBestVernacular, wsLabel);
						break;
					case "analysis vernacular":
						InitWsCombo(WsComboContent.kwccAnalAndVern, wsLabel);
						break;
					case "vernacular analysis":
						InitWsCombo(WsComboContent.kwccVernAndAnal, wsLabel);
						break;
					default:
						// There something going on that we don't know how to handle.
						// As a last ditch option, we show all vernacular and analysis systems.
						Debug.Assert(false, "A writing system was specified in the column spec that this method does not understand.");
						InitWsCombo(WsComboContent.kwccVernAndAnal, wsLabel);
						break;
				}
			}
			finally
			{
				m_fUpdatingWsCombo = false;
			}
		}


		/// <summary>
		/// given the magic writing system, we'll choose the appropriate ComboContent.
		/// if the given writing system is not magic, we'll use defaultMagicName provided.
		/// </summary>
		public static WsComboContent ChooseComboContent(LcmCache cache, int wsForOptions, string defaultMagicName)
		{
			var magicName = string.Empty;
			if (wsForOptions < 0)
			{
				magicName = WritingSystemServices.GetMagicWsNameFromId(wsForOptions);
			}
			if (magicName == string.Empty)
			{
				magicName = defaultMagicName;
			}
			return ChooseComboContent(magicName);
		}

		/// <summary>
		/// Chooses the content of the combo.
		/// </summary>
		/// <param name="wsForOptions">The ws for options.</param>
		public static WsComboContent ChooseComboContent(string wsForOptions)
		{
			switch (wsForOptions)
			{
				case "analysis": return WsComboContent.kwccAnalysis;
				case "vernacular": return WsComboContent.kwccVernacular;
				case "pronunciation": return WsComboContent.kwccPronunciation;
				case "best vernoranal": return WsComboContent.kwccBestVernOrAnal;
				case "best analorvern": return WsComboContent.kwccBestAnalOrVern;
				case "best analysis": return WsComboContent.kwccBestAnalysis;
				case "best vernacular": return WsComboContent.kwccBestVernacular;
				case "all analysis": return WsComboContent.kwccAnalysis;
				case "all vernacular": return WsComboContent.kwccVernacular;
				// The next two are needed to fix LT-6647.
				case "analysis vernacular": return WsComboContent.kwccAnalAndVern;
				case "vernacular analysis": return WsComboContent.kwccVernAndAnal;
				case "vern in para": return WsComboContent.kwccVernacularInParagraph;
				default:
					// There something going on that we don't know how to handle.
					// As a last ditch option, we show all vernacular and analysis systems.
					Debug.Assert(false, "A writing system was specified in the column spec that this method does not understand.");
					return WsComboContent.kwccVernAndAnal;
			}
		}

		private void optionsList_SelectedIndexChanged_1(object sender, EventArgs e)
		{
			EnableControls();
		}

		private void wsCombo_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (m_fUpdatingWsCombo)
			{
				return;
			}
			UpdateWsAndLabelOfCurrentColumn();
		}

		private void UpdateWsAndLabelOfCurrentColumn()
		{
			if (!(wsCombo.SelectedItem is WsComboItem))
			{
				return;
			}
			var index = CurrentListIndex;
			if (index < 0)
			{
				return;
			}
			var wsId = ((WsComboItem)wsCombo.SelectedItem).Id;
			var current = CurrentSpecs[index];
			var sWsOrig = XmlViewsUtils.FindWsParam(current);
			if (string.IsNullOrEmpty(sWsOrig))
			{
				sWsOrig = XmlUtils.GetOptionalAttributeValue(current, "ws");
			}
			var replacement = XmlViewsUtils.CopyReplacingParamDefault(current, "ws", wsId);
			var originalWs = XmlUtils.GetOptionalAttributeValue(replacement, "originalWs");
			if (originalWs == null)
			{
				// We store in the XML (which will be persisted as the spec of the column)
				// the original writing system code. This allows us to more easily
				// generate a label if it is changed again: we know both the original label
				// (to possibly append an abbreviation to) and the original writing system (so
				// we know whether to mark it at all).
				XmlUtils.SetAttribute(replacement, "originalWs", !string.IsNullOrEmpty(sWsOrig) ? sWsOrig : currentList.Items[index].SubItems[1].Text);
			}
			GenerateColumnLabel(replacement, m_cache);
			var xa = replacement.Attribute("label");
			xa.Value = XmlUtils.GetMandatoryAttributeValue(replacement, "label");
			var listItem = MakeCurrentItem(replacement);
			if (listItem == null) // The user deleted this ws and there was already one with the default ws.
			{
				Debug.Assert(false, "Did the user delete the ws?!");
				currentList.Items.RemoveAt(index);
				return;
			}
			CurrentSpecs[index] = replacement;
			currentList.Items.RemoveAt(index);
			currentList.Items.Insert(index, listItem);
			currentList.Items[index].Selected = true;
		}

		/// <summary>
		/// Generates a column label given an XML node of the column spec.  The purpose of this
		/// method is to append an abbreviation of the writing system of the column to the end
		/// of the normal label if the writing system is different from the normal one.  This
		/// method assumes that both the originalWs and the ws attributes have been set on the
		/// column already.
		/// </summary>
		public static void GenerateColumnLabel(XElement colSpec, LcmCache cache)
		{
			var newWs = XmlViewsUtils.FindWsParam(colSpec);
			var originalWs = XmlUtils.GetOptionalAttributeValue(colSpec, "originalWs");
			var originalLabel = XmlUtils.GetOptionalAttributeValue(colSpec, "originalLabel");
			if (originalLabel == null)
			{
				// We store in the XML (which will be persisted as the spec of the column)
				// the original label. This allows us to more easily
				// generate a label if it is changed again: we know both the original label
				// (to possibly append an abbreviation to) and the original writing system (so
				// we know whether to mark it at all).
				originalLabel = XmlUtils.GetMandatoryAttributeValue(colSpec, "label");
				XmlUtils.SetAttribute(colSpec, "originalLabel", originalLabel);
			}
			var label = originalLabel;
			if (!string.IsNullOrEmpty(label))
			{
				label = StringTable.Table.LocalizeAttributeValue(label);
			}
			// Note that there's no reason to try and make a new label if originalWs isn't defined.  If this is the
			// case, then it means that the ws was never changed, so we don't need to put the new ws in the label
			if (!string.IsNullOrEmpty(originalWs) && (newWs != originalWs))
			{
				var extra = string.Empty;
				switch (newWs)
				{
					case "vernacular":
						extra = "ver";
						break;
					case "analysis":
						extra = "an";
						break;
					default:
						// Try to use the abbreviation of the language name, not its ICU locale
						// name.
						CoreWritingSystemDefinition ws;
						if (cache.ServiceLocator.WritingSystemManager.TryGet(newWs, out ws))
						{
							extra = ws.Abbreviation;
						}
						if (string.IsNullOrEmpty(extra))
						{
							extra = newWs;  // but if all else fails...
						}
						break;
				}
				if (!string.IsNullOrEmpty(extra))
				{
					label += " (" + extra + ")";
				}
			}
			XmlUtils.SetAttribute(colSpec, "label", label);
		}

		private void helpButton_Click(object sender, EventArgs e)
		{
			ShowHelp.ShowHelpTopic(m_helpTopicProvider, s_helpTopic);
		}

		// Class to sort the columns before they are displayed
		private sealed class ColumnSorter : IComparer<XElement>
		{
			#region IComparer<T> Members

			public int Compare(XElement x, XElement y)
			{
				var xVal = StringTable.Table.LocalizeAttributeValue(XmlUtils.GetOptionalAttributeValue(x, "label", null)) ?? XmlUtils.GetMandatoryAttributeValue(x, "label");
				var yVal = StringTable.Table.LocalizeAttributeValue(XmlUtils.GetOptionalAttributeValue(y, "label", null)) ?? XmlUtils.GetMandatoryAttributeValue(y, "label");
				return xVal.CompareTo(yVal);
			}

			#endregion
		}
	}
}