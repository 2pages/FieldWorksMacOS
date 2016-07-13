// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

using SIL.FieldWorks.Common.Controls;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.Common.Framework;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.FDO.Infrastructure;
using SIL.FieldWorks.FdoUi;
using SIL.Utils;
using SIL.FieldWorks.FDO;
using XCore;
using SIL.FieldWorks.Common.Widgets;

namespace SIL.FieldWorks.XWorks.LexEd
{
	public partial class FindExampleSentenceDlg : Form, IFwGuiControl
	{
		FdoCache m_cache;
		Mediator m_mediator;
		XmlNode m_configurationNode;
		ILexExampleSentence m_les;
		ILexSense m_owningSense;
		ConcOccurrenceBrowseView m_rbv;
		XmlView m_previewPane;
		string m_helpTopic = "khtpFindExampleSentence";
		RecordClerk m_clerk;

		public FindExampleSentenceDlg()
		{
			InitializeComponent();
		}

		#region IFWDisposable Members

		public void CheckDisposed()
		{
			if (IsDisposed)
				throw new ObjectDisposedException(String.Format("'{0}' in use after being disposed.", GetType().Name));
		}

		#endregion

		#region IFwGuiControl Members

		public void Init(Mediator mediator, XmlNode configurationNode, ICmObject sourceObject)
		{
			CheckDisposed();

			m_cache = sourceObject.Cache;

			// Find the sense we want examples for, which depends on the kind of source object.
			if (sourceObject is ILexExampleSentence)
			{
				m_les = sourceObject as ILexExampleSentence;
				m_owningSense = (ILexSense)m_les.Owner;
			}
			else if (sourceObject is ILexSense)
			{
				m_owningSense = sourceObject as ILexSense;
			}
			else
			{
				throw new ArgumentException("Invalid object type for sourceObject.");
			}

			m_mediator = mediator;
			m_configurationNode = configurationNode;

			helpProvider.SetHelpNavigator(this, HelpNavigator.Topic);
			helpProvider.SetShowHelp(this, true);
			if (m_mediator.HelpTopicProvider != null)
			{
				helpProvider.HelpNamespace = m_mediator.HelpTopicProvider.HelpFile;
				helpProvider.SetHelpKeyword(this, m_mediator.HelpTopicProvider.GetHelpString(m_helpTopic));
				btnHelp.Enabled = true;
			}

			AddConfigurableControls();
		}

		public void Launch()
		{
			CheckDisposed();
			ShowDialog((Form)m_mediator.PropertyTable.GetValue("window"));
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
			m_previewPane = new XmlView(0, "publicationNew", null, false);
			m_previewPane.Cache = m_cache;
			m_previewPane.StyleSheet = FontHeightAdjuster.StyleSheetFromMediator(m_mediator);

			BasicPaneBarContainer pbc = new BasicPaneBarContainer();
			pbc.Init(m_mediator, m_previewPane);
			pbc.Dock = DockStyle.Fill;
			pbc.PaneBar.Text = LexEdStrings.ksFindExampleSentenceDlgPreviewPaneTitle;
			panel2.Controls.Add(pbc);
			if (m_previewPane.RootBox == null)
				m_previewPane.MakeRoot();

			// 2. load the browse view. (upper pane)
			XmlNode xnBrowseViewControlParameters = this.BrowseViewControlParameters;

			// First create our Clerk, since we can't set it's OwningObject via the configuration/mediator/PropertyTable info.
			m_clerk = RecordClerkFactory.CreateClerk(m_mediator, xnBrowseViewControlParameters, true);
			m_clerk.OwningObject = m_owningSense;

			m_rbv = DynamicLoader.CreateObject(xnBrowseViewControlParameters.ParentNode.SelectSingleNode("dynamicloaderinfo")) as ConcOccurrenceBrowseView;
			m_rbv.Init(m_mediator, xnBrowseViewControlParameters, m_previewPane, m_clerk.VirtualListPublisher);
			m_rbv.CheckBoxChanged += new CheckBoxChangedEventHandler(m_rbv_CheckBoxChanged);
			// add it to our controls.
			BasicPaneBarContainer pbc1 = new BasicPaneBarContainer();
			pbc1.Init(m_mediator, m_rbv);
			pbc1.BorderStyle = BorderStyle.FixedSingle;
			pbc1.Dock = DockStyle.Fill;
			pbc1.PaneBar.Text = LexEdStrings.ksFindExampleSentenceDlgBrowseViewPaneTitle;
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

			UndoableUnitOfWorkHelper.Do(LexEdStrings.ksUndoAddExamples, LexEdStrings.ksRedoAddExamples,
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
			ShowHelp.ShowHelpTopic(m_mediator.HelpTopicProvider, m_helpTopic);
		}
	}

	internal class ConcOccurrenceBrowseView : RecordBrowseView
	{
		int m_hvoSelectedOccurrence; // dummy HVO for occurrence, only understood by ConcSda
		XmlView m_previewPane;
		private ISilDataAccess m_decoratedSda; // typically a ConcSda, understands the segment property of the fake HVO.

		internal void Init(Mediator mediator, XmlNode xnBrowseViewControlParameters, XmlView pubView, ISilDataAccess sda)
		{
			m_previewPane = pubView;
			m_decoratedSda = sda;
			base.Init(mediator, xnBrowseViewControlParameters);
		}

		public override void OnSelectionChanged(object sender, FwObjectSelectionEventArgs e)
		{
			PreviewCurrentSelection(e.Hvo);
			base.OnSelectionChanged(sender, e);
		}

		protected override void ShowRecord()
		{
			if (!m_fullyInitialized || m_suppressShowRecord)
				return;
			if (Clerk == null || Clerk.CurrentObjectHvo == 0)
				return;
			PreviewCurrentSelection(Clerk.CurrentObjectHvo);
			base.ShowRecord();
		}

		private void PreviewCurrentSelection(int hvoOccurrence)
		{
			if (m_hvoSelectedOccurrence == hvoOccurrence)
				return;
			m_hvoSelectedOccurrence = hvoOccurrence;
			m_previewPane.RootObjectHvo = m_decoratedSda.get_ObjectProp(m_hvoSelectedOccurrence, ConcDecorator.kflidSegment);
		}
	}
}
