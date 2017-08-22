// Copyright (c) 2015-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using LanguageExplorer.Controls.PaneBar;
using LanguageExplorer.Works;
using SIL.LCModel.Core.Text;
using SIL.FieldWorks.Common.Controls;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel.Infrastructure;
using SIL.FieldWorks.FdoUi;
using SIL.LCModel;
using SIL.FieldWorks.Common.Widgets;

namespace LanguageExplorer.Areas.Lexicon.Tools.Edit
{
#if RANDYTODO
// TODO: Used by this:
/*
<guicontrol id="findExampleSentences">
	<dynamicloaderinfo assemblyPath="LanguageExplorer.dll" class="LanguageExplorer.Areas.Lexicon.Tools.Edit.FindExampleSentenceDlg"/>
	<parameters id="senseConcordanceControls">
		<control id="ConcOccurrenceList">
			<dynamicloaderinfo assemblyPath="LanguageExplorer.dll" class="LanguageExplorer.Areas.Lexicon.Tools.Edit.ConcOccurrenceBrowseView"/>
			<parameters id="ConcOccurrenceList" selectColumn="true" defaultChecked="false" forceReloadListOnInitOrChangeRoot="true" editable="false"
					clerk="OccurrencesOfSense" filterBar="true" ShowOwnerShortname="true">
				<include path="../Words/reusableBrowseControlConfiguration.xml" query="reusableControls/control[@id='concordanceColumns']/columns"/>
			</parameters>
		</control>
		<control id="SegmentPreviewControl">
			<dynamicloaderinfo assemblyPath="xWorks.dll" class="LanguageExplorer.Works.RecordDocXmlView"/>
			<parameters id="SegmentPreviewControl" clerk="OccurrencesOfSense" treeBarAvailability="NotMyBusiness" layout="publicationNew" editable="false"/>
		</control>
	</parameters>
	</guicontrol>
*/
#endif
	/// <summary />
	internal partial class FindExampleSentenceDlg : Form, IFwGuiControl
	{
		LcmCache m_cache;
		XmlNode m_configurationNode;
		ILexExampleSentence m_les;
		ILexSense m_owningSense;
		ConcOccurrenceBrowseView m_rbv;
		XmlView m_previewPane;
		string m_helpTopic = "khtpFindExampleSentence";
		RecordClerk m_clerk;

		/// <summary />
		public FindExampleSentenceDlg()
		{
			InitializeComponent();
		}

		#region Implementation of IPropertyTableProvider

		/// <summary>
		/// Placement in the IPropertyTableProvider interface lets FwApp call IPropertyTable.DoStuff.
		/// </summary>
		public IPropertyTable PropertyTable { get; private set; }

		#endregion

		#region Implementation of IPublisherProvider

		/// <summary>
		/// Get the IPublisher.
		/// </summary>
		public IPublisher Publisher { get; private set; }

		#endregion

		#region Implementation of ISubscriberProvider

		/// <summary>
		/// Get the ISubscriber.
		/// </summary>
		public ISubscriber Subscriber { get; private set; }

		/// <summary>
		/// Initialize a FLEx component with the basic interfaces.
		/// </summary>
		/// <param name="flexComponentParameters">Parameter object that contains the required three interfaces.</param>
		public void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
		{
			FlexComponentCheckingService.CheckInitializationValues(flexComponentParameters, new FlexComponentParameters(PropertyTable, Publisher, Subscriber));

			PropertyTable = flexComponentParameters.PropertyTable;
			Publisher = flexComponentParameters.Publisher;
			Subscriber = flexComponentParameters.Subscriber;
		}

		#endregion

		/// <summary />
		public void CheckDisposed()
		{
			if (IsDisposed)
				throw new ObjectDisposedException(String.Format("'{0}' in use after being disposed.", GetType().Name));
		}

		#region IFwGuiControl Members

		/// <summary />
		public void Init(XmlNode configurationNode, ICmObject sourceObject)
		{
			CheckDisposed();

			m_cache = sourceObject.Cache;

			// Find the sense we want examples for, which depends on the kind of source object.
			if (sourceObject is ILexExampleSentence)
			{
				m_les = sourceObject as ILexExampleSentence;
				if (m_les.Owner is ILexSense)
				{
					m_owningSense = (ILexSense)m_les.Owner;
				}
				else if (m_les.Owner is ILexExtendedNote)
				{
					m_owningSense = (ILexSense)m_les.Owner.Owner;
				}
			}
			else if (sourceObject is ILexSense)
			{
				m_owningSense = sourceObject as ILexSense;
			}
			else if (sourceObject is ILexExtendedNote)
			{
				m_owningSense = sourceObject.Owner as ILexSense;
			}
			else
			{
				throw new ArgumentException("Invalid object type for sourceObject.");
			}

			m_configurationNode = configurationNode;

			helpProvider.SetHelpNavigator(this, HelpNavigator.Topic);
			helpProvider.SetShowHelp(this, true);
			var helpToicProvider = PropertyTable.GetValue<IHelpTopicProvider>("HelpTopicProvider");
			if (helpToicProvider != null)
			{
				helpProvider.HelpNamespace = helpToicProvider.HelpFile;
				helpProvider.SetHelpKeyword(this, helpToicProvider.GetHelpString(m_helpTopic));
				btnHelp.Enabled = true;
			}

			AddConfigurableControls();
		}

		/// <summary />
		public void Launch()
		{
			CheckDisposed();
			ShowDialog(PropertyTable.GetValue<Form>("window"));
		}

		#endregion

		XmlNode BrowseViewControlParameters
		{
			get
			{
				return m_configurationNode.SelectSingleNode(
					String.Format("control/parameters[@id='{0}']", "ConcOccurrenceList"));
			}
		}

		private void AddConfigurableControls()
		{
			// Load the controls.

			// 1. Initialize the preview pane (lower pane)
			m_previewPane = new XmlView(0, "publicationNew", false)
			{
				Cache = m_cache,
				StyleSheet = FontHeightAdjuster.StyleSheetFromPropertyTable(PropertyTable)
			};

			BasicPaneBarContainer pbc = new BasicPaneBarContainer();
			pbc.Init(PropertyTable, m_previewPane, new PaneBar());
			pbc.Dock = DockStyle.Fill;
			pbc.PaneBar.Text = LanguageExplorerResources.ksFindExampleSentenceDlgPreviewPaneTitle;
			panel2.Controls.Add(pbc);
			if (m_previewPane.RootBox == null)
				m_previewPane.MakeRoot();

			// 2. load the browse view. (upper pane)
			XmlNode xnBrowseViewControlParameters = this.BrowseViewControlParameters;

#if RANDYTODO
			// First create our Clerk, since we can't set it's OwningObject via the configuration/mediator/PropertyTable info.
			m_clerk = RecordClerkFactory.CreateClerk(PropertyTable, Publisher, Subscriber, true);
			m_clerk.OwningObject = m_owningSense;
#endif

			m_rbv = DynamicLoader.CreateObject(xnBrowseViewControlParameters.ParentNode.SelectSingleNode("dynamicloaderinfo")) as ConcOccurrenceBrowseView;
			m_rbv.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
			m_rbv.Init(m_previewPane, m_clerk.VirtualListPublisher);
			m_rbv.CheckBoxChanged += m_rbv_CheckBoxChanged;
			// add it to our controls.
			BasicPaneBarContainer pbc1 = new BasicPaneBarContainer();
			pbc1.Init(PropertyTable, m_rbv, new PaneBar());
			pbc1.BorderStyle = BorderStyle.FixedSingle;
			pbc1.Dock = DockStyle.Fill;
			pbc1.PaneBar.Text = LanguageExplorerResources.ksFindExampleSentenceDlgBrowseViewPaneTitle;
			panel1.Controls.Add(pbc1);

			CheckAddBtnEnabling();
		}

		void m_rbv_CheckBoxChanged(object sender, CheckBoxChangedEventArgs e)
		{
			CheckAddBtnEnabling();
		}

		private void CheckAddBtnEnabling()
		{
			btnAdd.Enabled = m_rbv.CheckedItems.Count > 0;
		}
		private void btnAdd_Click(object sender, EventArgs e)
		{
			// Get the checked occurrences;
			List<int> occurrences = m_rbv.CheckedItems;
			if (occurrences == null || occurrences.Count == 0)
			{
				// do nothing.
				return;
			}
			List<int> uniqueSegments =
				(from fake in occurrences
				 select m_clerk.VirtualListPublisher.get_ObjectProp(fake, ConcDecorator.kflidSegment)).Distinct().ToList
					();
			int insertIndex = m_owningSense.ExamplesOS.Count; // by default, insert at the end.
			if (m_les != null)
			{
				// we were given a LexExampleSentence, so set our insertion index after the given one.
				insertIndex = m_owningSense.ExamplesOS.IndexOf(m_les) + 1;
			}

			UndoableUnitOfWorkHelper.Do(LanguageExplorerResources.ksUndoAddExamples, LanguageExplorerResources.ksRedoAddExamples,
				m_cache.ActionHandlerAccessor,
				() =>
					{
						int cNewExamples = 0;
						ILexExampleSentence newLexExample = null;
						foreach (int segHvo in uniqueSegments)
						{
							var seg = m_cache.ServiceLocator.GetObject(segHvo) as ISegment;
							if (cNewExamples == 0 && m_les != null &&
								m_les.Example.BestVernacularAlternative.Text == "***" &&
								(m_les.TranslationsOC == null || m_les.TranslationsOC.Count == 0) &&
								m_les.Reference.Length == 0)
							{
								// we were given an empty LexExampleSentence, so use this one for our first new Example.
								newLexExample = m_les;
							}
							else
							{
								// create a new example sentence.
								newLexExample =
									m_cache.ServiceLocator.GetInstance<ILexExampleSentenceFactory>().Create();
								m_owningSense.ExamplesOS.Insert(insertIndex + cNewExamples, newLexExample);
								cNewExamples++;
							}
							// copy the segment string into the new LexExampleSentence
							// Enhance: bold the relevant occurrence(s).
							// LT-11388 Make sure baseline text gets copied into correct ws
							var baseWs = GetBestVernWsForNewExample(seg);
							newLexExample.Example.set_String(baseWs, seg.BaselineText);
							if (seg.FreeTranslation.AvailableWritingSystemIds.Length > 0)
							{
								var trans = m_cache.ServiceLocator.GetInstance<ICmTranslationFactory>().Create(newLexExample,
									m_cache.ServiceLocator.GetInstance<ICmPossibilityRepository>().GetObject(
										CmPossibilityTags.kguidTranFreeTranslation));
								trans.Translation.CopyAlternatives(seg.FreeTranslation);
							}
							if (seg.LiteralTranslation.AvailableWritingSystemIds.Length > 0)
							{
								var trans = m_cache.ServiceLocator.GetInstance<ICmTranslationFactory>().Create(newLexExample,
									m_cache.ServiceLocator.GetInstance<ICmPossibilityRepository>().GetObject(
										CmPossibilityTags.kguidTranLiteralTranslation));
								trans.Translation.CopyAlternatives(seg.LiteralTranslation);
							}
						   // copy the reference.
							ITsString tssRef = seg.Paragraph.Reference(seg, seg.BeginOffset);
							// convert the plain reference string into a link.
							ITsStrBldr tsb = tssRef.GetBldr();
							FwLinkArgs fwl = new FwLinkArgs("interlinearEdit", seg.Owner.Owner.Guid);
							// It's not clear how to focus in on something smaller than the text when following
							// a link.
							//fwl.PropertyTableEntries.Add(new Property("LinkSegmentGuid", seg.Guid.ToString()));
							tsb.SetStrPropValue(0, tsb.Length, (int)FwTextPropType.ktptObjData,
								(char)FwObjDataTypes.kodtExternalPathName + fwl.ToString());
							tsb.SetStrPropValue(0, tsb.Length, (int)FwTextPropType.ktptNamedStyle,
								"Hyperlink");
							newLexExample.Reference = tsb.GetString();
						}
					});
		}

		private int GetBestVernWsForNewExample(ISegment seg)
		{
			var baseWs = seg.BaselineText.get_WritingSystem(0);
			if (baseWs < 1)
				return m_cache.DefaultVernWs;

			var possibleWss = m_cache.ServiceLocator.WritingSystems.VernacularWritingSystems;
			var wsObj = m_cache.ServiceLocator.WritingSystemManager.Get(baseWs);
			return possibleWss.Contains(wsObj) ? baseWs : m_cache.DefaultVernWs;
		}

		private void btnHelp_Click(object sender, EventArgs e)
		{
			ShowHelp.ShowHelpTopic(PropertyTable.GetValue<IHelpTopicProvider>("HelpTopicProvider"), m_helpTopic);
		}
	}
}
