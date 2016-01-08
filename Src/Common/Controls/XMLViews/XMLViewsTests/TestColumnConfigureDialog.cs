// Copyright (c) 2010-2013 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// Original author: MarkS 2010-06-29 TestColumnConfigureDialog.cs
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.Controls;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.FDOTests;
using SIL.FieldWorks.Test.TestUtils;
using SIL.Utils;

namespace XMLViewsTests
{
	/// <summary></summary>
	[TestFixture]
	public class TestColumnConfigureDialog : BaseTest
	{
		private IPublisher m_publisher;
		private ISubscriber m_subscriber;
		private IPropertyTable m_propertyTable;
		private FdoCache m_cache;

		[SetUp]
		public void SetUp()
		{
			PubSubSystemFactory.CreatePubSubSystem(out m_publisher, out m_subscriber);
			m_propertyTable = PropertyTableFactory.CreatePropertyTable(m_publisher);
			var st = StringTable.Table; // Make sure it is loaded.
			m_cache = FdoCache.CreateCacheWithNewBlankLangProj(
				new TestProjectId(FDOBackendProviderType.kMemoryOnly, null), "en", "en", "en", new DummyFdoUI(), FwDirectoryFinder.FdoDirectories, new FdoSettings());
			m_propertyTable.SetProperty("cache", m_cache, true, true);
		}

		[TearDown]
		public void TearDown()
		{
			m_propertyTable.Dispose();
			m_propertyTable = null;
			m_publisher = null;
			m_subscriber = null;
			m_cache.Dispose();
			m_cache = null;
		}

		#region AfterMovingItemArrowsAreNotImproperlyDisabled
		private ColumnConfigureDialog CreateColumnConfigureDialog()
		{
			// Create window and populate currentColumns with a few items.

			string currentColumns_data = "<root><column layout=\"EntryHeadwordForEntry\" label=\"Headword\" ws=\"$ws=vernacular\" width=\"72000\" sortmethod=\"FullSortKey\" cansortbylength=\"true\" visibility=\"always\" /><column layout=\"LexemeFormForEntry\" label=\"Lexeme Form\" common=\"true\" width=\"72000\" ws=\"$ws=vernacular\" sortmethod=\"MorphSortKey\" cansortbylength=\"true\" visibility=\"always\" transduce=\"LexEntry.LexemeForm.Form\" transduceCreateClass=\"MoStemAllomorph\" /><column layout=\"GlossesForSense\" label=\"Glosses\" multipara=\"true\" width=\"72000\" ws=\"$ws=analysis\" transduce=\"LexSense.Gloss\" cansortbylength=\"true\" visibility=\"always\" /><column layout=\"GrammaticalInfoFullForSense\" headerlabel=\"Grammatical Info.\" chooserFilter=\"external\" label=\"Grammatical Info. (Full)\" multipara=\"true\" width=\"72000\" visibility=\"always\"><dynamicloaderinfo assemblyPath=\"FdoUi.dll\" class=\"SIL.FieldWorks.FdoUi.PosFilter\" /></column></root>";
			var currentColumns_document = XDocument.Parse(currentColumns_data);
			var currentColumns = currentColumns_document.Root.Elements().ToList();

			var possibleColumns = new List<XElement>();

			var window = new ColumnConfigureDialog(possibleColumns, currentColumns, m_propertyTable);
			window.FinishInitialization();

			return window;
		}

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
	}
}
