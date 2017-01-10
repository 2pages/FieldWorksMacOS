// Copyright (c) 2010-2013 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// Original author: MarkS 2010-06-29 TestColumnConfigureDialog.cs

using System.Collections.Generic;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using SIL.FieldWorks.Common.Controls;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.FDOTests;
using SIL.FieldWorks.Test.TestUtils;
using SIL.Utils;
using XCore;

namespace XMLViewsTests
{
	/// <summary></summary>
	[TestFixture]
	public class TestColumnConfigureDialog : BaseTest
	{
		private Mediator m_mediator;
		private FdoCache m_cache;

		[SetUp]
		public void SetUp()
		{
			m_mediator = new Mediator();
			m_mediator.StringTbl = new StringTable("../../DistFiles/Language Explorer/Configuration");
			m_cache = FdoCache.CreateCacheWithNewBlankLangProj(
				new TestProjectId(FDOBackendProviderType.kMemoryOnly, null), "en", "en", "en", new DummyFdoUI(), FwDirectoryFinder.FdoDirectories, new FdoSettings());
			m_mediator.PropertyTable.SetProperty("cache", m_cache);
		}

		[TearDown]
		public void TearDown()
		{
			m_mediator.Dispose();
			m_cache.Dispose();
		}

		#region AfterMovingItemArrowsAreNotImproperlyDisabled

		/// <summary>
		/// FWNX-313: flex configure column arrows inappropriately disabling
		/// After moving a current columns item up or down, the up and down buttons
		/// should not both be disabled.
		/// <seealso cref="AfterMovingItemArrowsAreNotImproperlyDisabled_OnUp"/>
		/// </summary>
		[Test]
		public void AfterMovingItemArrowsAreNotImproperlyDisabled_OnDown()
		{
			using (var window = CreateColumnConfigureDialog())
			{
				window.Show();
				window.currentList.Items[1].Selected = true;
				window.moveDownButton.PerformClick();
				Assert.True(window.moveUpButton.Enabled,
					"Up button should not be disabled after moving an item down.");
			}
		}

		/// <see cref="AfterMovingItemArrowsAreNotImproperlyDisabled_OnDown"/>
		[Test]
		public void AfterMovingItemArrowsAreNotImproperlyDisabled_OnUp()
		{
			using (var window = CreateColumnConfigureDialog())
			{
				window.Show();
				window.currentList.Items[1].Selected = true;
				window.moveUpButton.PerformClick();
				Assert.True(window.moveDownButton.Enabled,
					"Down button should not be disabled after moving an item up.");
			}
		}
		#endregion

		#region WsCombo selection tests
		/// <summary/>
		[Test]
		[Category("ByHand")]
		public void AnalysisVernacularWsSetsWsComboToAnalysis()
		{
			using (var window = CreateColumnConfigureDialog("<root></root>", "<root><column layout=\"EntryHeadwordForEntry\" label=\"Headword\" ws=\"$ws=analysis vernacular\" cansortbylength=\"true\" visibility=\"always\" /></root>"))
			{
				window.Show();
				window.optionsList.Items[0].Selected = true;
				window.addButton.PerformClick();
				Assert.AreEqual(((WsComboItem)window.wsCombo.SelectedItem).Id, "analysis", "Default analysis should be selected for 'analysis vernacular' ws");
			}
		}
		#endregion

		private ColumnConfigureDialog CreateColumnConfigureDialog()
		{
			// Create window and populate currentColumns with a few items.

			var currentColumns_data = "<root><column layout=\"EntryHeadwordForEntry\" label=\"Headword\" ws=\"$ws=vernacular\" width=\"72000\" sortmethod=\"FullSortKey\" cansortbylength=\"true\" visibility=\"always\" /><column layout=\"LexemeFormForEntry\" label=\"Lexeme Form\" common=\"true\" width=\"72000\" ws=\"$ws=vernacular\" sortmethod=\"MorphSortKey\" cansortbylength=\"true\" visibility=\"always\" transduce=\"LexEntry.LexemeForm.Form\" transduceCreateClass=\"MoStemAllomorph\" /><column layout=\"GlossesForSense\" label=\"Glosses\" multipara=\"true\" width=\"72000\" ws=\"$ws=analysis\" transduce=\"LexSense.Gloss\" cansortbylength=\"true\" visibility=\"always\" /><column layout=\"GrammaticalInfoFullForSense\" headerlabel=\"Grammatical Info.\" chooserFilter=\"external\" label=\"Grammatical Info. (Full)\" multipara=\"true\" width=\"72000\" visibility=\"always\"><dynamicloaderinfo assemblyPath=\"FdoUi.dll\" class=\"SIL.FieldWorks.FdoUi.PosFilter\" /></column></root>";
			return CreateColumnConfigureDialog(currentColumns_data, "<root></root>");
		}

		private ColumnConfigureDialog CreateColumnConfigureDialog(string currentColumnsData, string possibleColumnsData)
		{
			// Create window and populate currentColumns with a few items.
			var currentColumns_document = new XmlDocument();
			currentColumns_document.LoadXml(currentColumnsData);
			List<XmlNode> currentColumns = currentColumns_document.FirstChild.ChildNodes.Cast<XmlNode>().ToList();

			var possibleColumns_document = new XmlDocument();
			possibleColumns_document.LoadXml(possibleColumnsData);
			List<XmlNode> possibleColumns = possibleColumns_document.FirstChild.ChildNodes.Cast<XmlNode>().ToList();

			ColumnConfigureDialog window = new ColumnConfigureDialog(possibleColumns, currentColumns, m_mediator,
				m_mediator.StringTbl);
			window.FinishInitialization();

			return window;
		}
	}
}
