// Copyright (c) 2004-2013 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// File: TeScrInitializerTests.cs
// Responsibility: TE team

using System;
using System.Collections.Generic;

using NUnit.Framework;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.FwKernelInterfaces;
using SIL.FieldWorks.FDO;
using SIL.Utils;
using SIL.FieldWorks.Common.ScriptureUtils;
using SIL.FieldWorks.FDO.FDOTests;
using SILUBS.SharedScrUtils;

namespace SIL.FieldWorks.TE
{
	#region TestTeScrInitializer class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// TestTeScrInitializer class exposes aspects of <see cref="TeScrInitializer"/> class
	/// for testing.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	internal class TestTeScrInitializer : TeScrInitializer
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Init the base class TeScrInitializer for testing.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public TestTeScrInitializer(FdoCache cache) : base(cache)
		{
		}
	}
	#endregion

	#region TeScrInitializerTests class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// TeScrInitializerTests is a collection of tests for static methods of the
	/// <see cref="TeScrInitializer"/> class
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class TeScrInitializerTests : ScrInMemoryFdoTestBase
	{
		private TestTeScrInitializer m_scrInitializer;

		#region Setup, Teardown

		/// <summary>
		///
		/// </summary>
		public override void TestSetup()
		{
			base.TestSetup();
			m_scr.ResourcesOC.Clear(); // Make sure we don't think we've already done the fix
			m_scrInitializer = new TestTeScrInitializer(Cache);
		}

		public override void TestTearDown()
		{
			m_scrInitializer = null;

			base.TestTearDown();
		}
		#endregion

		#region Create ScrBookRefs tests
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test TeScrInitializer.CreateScrBookRefs method.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void CreateScrBookRefs()
		{
			ReflectionHelper.CallMethod(typeof(TeScrBookRefsInit), "SetNamesAndAbbreviations",
				new DummyProgressDlg(), Cache);

			IFdoOwningSequence<IScrBookRef> books =
				Cache.ServiceLocator.GetInstance<IScrRefSystemRepository>().Singleton.BooksOS;

			// Make sure the right number of books was generated.
			Assert.AreEqual(66, books.Count);

			ILgWritingSystemFactory wsf = Cache.WritingSystemFactory;
			int wsEnglish = wsf.GetWsFromStr("en");
			int wsSpanish = wsf.GetWsFromStr("es");

			// Check English Genesis
			IScrBookRef genesis = books[0];
			Assert.AreEqual("Genesis",
				genesis.BookName.get_String(wsEnglish).Text);
			Assert.AreEqual("Gen",
				genesis.BookAbbrev.get_String(wsEnglish).Text);
			Assert.IsNull(genesis.BookNameAlt.get_String(wsEnglish).Text);

			// Check Spanish Matthew
			IScrBookRef mateo = books[39];
			Assert.AreEqual("Mateo",
				mateo.BookName.get_String(wsSpanish).Text);
			Assert.AreEqual("Mt",
				mateo.BookAbbrev.get_String(wsSpanish).Text);
			Assert.IsNull(mateo.BookNameAlt.get_String(wsSpanish).Text);

			// Check English 2 Corinthians
			IScrBookRef iiCor = books[46];
			Assert.AreEqual("2 Corinthians",
				iiCor.BookName.get_String(wsEnglish).Text);
			Assert.AreEqual("2Cor",
				iiCor.BookAbbrev.get_String(wsEnglish).Text);
			Assert.AreEqual("II Corinthians",
				iiCor.BookNameAlt.get_String(wsEnglish).Text);

			// Check Spanish Revelation
			IScrBookRef apocalipsis = books[65];
			Assert.AreEqual("Apocalipsis",
				apocalipsis.BookName.get_String(wsSpanish).Text);
			Assert.AreEqual("Ap",
				apocalipsis.BookAbbrev.get_String(wsSpanish).Text);
			Assert.IsNull(apocalipsis.BookNameAlt.get_String(wsSpanish).Text);

			MultilingScrBooks mlsb = new MultilingScrBooks(m_scr.ScrProjMetaDataProvider);

			foreach (IScrBookRef brf in books)
			{
				Assert.IsTrue(!String.IsNullOrEmpty(brf.BookName.get_String(wsEnglish).Text));
				Assert.IsTrue(!String.IsNullOrEmpty(brf.BookAbbrev.get_String(wsEnglish).Text));
			}
		}
		#endregion

		#region Remove RTL marks from Scripture properties tests
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that the TeScrInitializer removes the RTL marks from verse bridge, etc.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void RemoveRTLMarksFromVerseBridgeEtc()
		{
			m_scr.Bridge = "\u200f-\u200f";
			m_scr.ChapterVerseSepr = "\u200f:\u200f";
			m_scr.VerseSepr = "\u200f,\u200f";
			m_scr.RefSepr = "\u200f;\u200f";
			ReflectionHelper.CallMethod(m_scrInitializer, "RemoveRtlMarksFromScrProperties");
			Assert.AreEqual("-", m_scr.Bridge);
			Assert.AreEqual(":", m_scr.ChapterVerseSepr);
			Assert.AreEqual(",", m_scr.VerseSepr);
			Assert.AreEqual(";", m_scr.RefSepr);
		}
		#endregion

		#region FixOrcsWithoutProps tests
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test <see cref="TeScrInitializer.FixOrcsWithoutProps()"/> method when the project
		/// has no footnotes.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FixOrcsWithoutProps_NoFootnotesInProject()
		{
			IScrBook exodus = CreateExodusData();

			TeScrInitializer scrInit = new TestTeScrInitializer(Cache);
			Assert.IsNull(ReflectionHelper.GetResult(scrInit, "FixOrcsWithoutProps"));

			VerifyResourceForFixedOrphans();
			Assert.AreEqual(0, exodus.FootnotesOS.Count);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test <see cref="TeScrInitializer.FixOrcsWithoutProps()"/> method when the
		/// project has footnotes, but none are orphaned.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FixOrcsWithoutProps_AllFootnotesAreOkay()
		{
			IScrBook exodus = CreateExodusData();
			CreateFootnote(exodus, 0, 0, 0, 4, ScrStyleNames.NormalFootnoteParagraph, false);
			CreateFootnote(exodus, 2, 0, 1, 7, ScrStyleNames.NormalFootnoteParagraph, false);

			TeScrInitializer scrInit = new TestTeScrInitializer(Cache);
			Assert.IsNull(ReflectionHelper.GetResult(scrInit, "FixOrcsWithoutProps"));

			VerifyResourceForFixedOrphans();
			Assert.AreEqual(2, exodus.FootnotesOS.Count);
		}

#if WANTTESTPORT // (TE) FWR-774 - Need different testing strategy
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test <see cref="TeScrInitializer.FixOrcsWithoutProps()"/> method when the
		/// project has orphaned footnotes interspersed with non-orphaned footnotes such that all
		/// orphans are in the correct order.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FixOrcsWithoutProps_OrphanedFootnotesInOrder()
		{
			IScrBook exodus = CreateExodusData();
			IScrTxtPara para = AddParaToMockedSectionContent(exodus.SectionsOS[2], ScrStyleNames.NormalParagraph);
			AddVerse(para, 2, 3, "ORC is here, you see, my friend.");
			CreateFootnote(exodus, 0, 0, 0, 4, ScrStyleNames.NormalFootnoteParagraph, false);
			CreateFootnote(exodus, 1, 0, 1, 7, ScrStyleNames.NormalFootnoteParagraph, true);
			CreateFootnote(exodus, 2, 0, 2, 7, ScrStyleNames.CrossRefFootnoteParagraph, false);
			CreateFootnote(exodus, 2, 1, 3, 13, ScrStyleNames.NormalFootnoteParagraph, true);

			TeScrInitializer scrInit = new TestTeScrInitializer(Cache);
			List <string> report = (List<string>)ReflectionHelper.GetResult(scrInit, "FixOrcsWithoutProps");

			VerifyNoOrphanedFootnotes();
			VerifyResourceForFixedOrphans();

			Assert.AreEqual(2, report.Count);
			Assert.AreEqual("EXO 1:1 - Connected footnote to marker in the vernacular text", report[0]);
			Assert.AreEqual("EXO 2:3 - Connected footnote to marker in the vernacular text", report[1]);

			Assert.AreEqual(4, exodus.FootnotesOS.Count);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test <see cref="TeScrInitializer.FixOrcsWithoutProps()"/> method when the
		/// project has two orphaned footnotes in the same paragraph.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FixOrcsWithoutProps_TwoOrphanedFootnotesInParagraph()
		{
			IScrBook exodus = CreateExodusData();
			IScrTxtPara para = AddParaToMockedSectionContent(exodus.SectionsOS[2], ScrStyleNames.NormalParagraph);
			AddVerse(para, 2, 3, "ORC is here, you see, my friend.");
			CreateFootnote(exodus, 1, 0, 0, 7, ScrStyleNames.NormalFootnoteParagraph, true);
			CreateFootnote(exodus, 1, 0, 1, 19, ScrStyleNames.CrossRefFootnoteParagraph, true);

			TeScrInitializer scrInit = new TestTeScrInitializer(Cache);
			List<string> report = (List<string>)ReflectionHelper.GetResult(scrInit, "FixOrcsWithoutProps");

			VerifyNoOrphanedFootnotes();
			VerifyResourceForFixedOrphans();

			Assert.AreEqual(2, report.Count);
			Assert.AreEqual("EXO 1:1 - Deleted corrupted footnote marker or picture anchor", report[0]);
			Assert.AreEqual("EXO 1:2 - Deleted corrupted footnote marker or picture anchor", report[1]);

			Assert.AreEqual(0, exodus.FootnotesOS.Count);
		}
#endif

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test <see cref="TeScrInitializer.FixOrcsWithoutProps()"/> method when the
		/// project has an orphaned footnote and a valid picture ORC in the same book.
		/// TE-8769
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FixOrcsWithoutProps_OrphanedFootnoteAndValidPicture()
		{
			IScrBook exodus = CreateExodusData();
			IScrTxtPara para = AddParaToMockedSectionContent(exodus.SectionsOS[2], ScrStyleNames.NormalParagraph);
			AddVerse(para, 2, 3, "ORC is here, you see, my friend.");

			// Update section 2's paragraph contents to include the picture
			ITsStrBldr tsStrBldr = para.Contents.GetBldr();
			string filename;
			if (MiscUtils.IsUnix)
				filename = "/tmp/junk.jpg";
			else
				filename = "c:\\junk.jpg";
			ICmPicture pict = Cache.ServiceLocator.GetInstance<ICmPictureFactory>().Create(filename,
				TsStringUtils.MakeString("Test picture", Cache.DefaultVernWs), CmFolderTags.LocalPictures);
			Assert.IsNotNull(pict);
			pict.InsertORCAt(tsStrBldr, 11);
			para.Contents = tsStrBldr.GetString();
			// We need a valid footnote after the picture ORC
			CreateFootnote(exodus, 2, para.IndexInOwner, 0, 15, ScrStyleNames.CrossRefFootnoteParagraph, false);

			// Update section 1's paragraph contents to include an orphaned footnote marker
			CreateFootnote(exodus, 1, 0, 0, 19, ScrStyleNames.CrossRefFootnoteParagraph, true);

			TeScrInitializer scrInit = new TestTeScrInitializer(Cache);
			List<string> report = (List<string>)ReflectionHelper.GetResult(scrInit, "FixOrcsWithoutProps");

			VerifyResourceForFixedOrphans();

			Assert.AreEqual(1, report.Count);
			Assert.AreEqual("EXO 1:2 - Deleted corrupted footnote marker or picture anchor", report[0]);

			Assert.AreEqual(1, exodus.FootnotesOS.Count);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test <see cref="TeScrInitializer.FixOrcsWithoutProps()"/> method when the
		/// project has an ORC that has props, but the object it's pointing to is missing.
		/// This scenario is really outside the scope of what this method was intended to
		/// handle, but we're testing it because the implementation needs to deal with it. We
		/// hope this is no longer possible for this to happen, but if it does, we'll treat it
		/// like any other orphaned ORC.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FixOrcsWithoutProps_OrcForMissingObject()
		{
			IScrBook exodus = CreateExodusData();
			IScrTxtPara para = AddParaToMockedSectionContent(exodus.SectionsOS[2], ScrStyleNames.NormalParagraph);
			AddVerse(para, 2, 3, "ORC is here, you see, my friend.");

			// Update the paragraph contents to include the picture
			ITsStrBldr tsStrBldr = para.Contents.GetBldr();
			TsStringUtils.InsertOrcIntoPara(Guid.NewGuid(), FwObjDataTypes.kodtOwnNameGuidHot,
				tsStrBldr, 11, 11, Cache.DefaultVernWs);
			para.Contents = tsStrBldr.GetString();

			TeScrInitializer scrInit = new TestTeScrInitializer(Cache);
			List<string> report = (List<string>)ReflectionHelper.GetResult(scrInit, "FixOrcsWithoutProps");

			VerifyResourceForFixedOrphans();

			Assert.AreEqual(1, report.Count);
			Assert.AreEqual("EXO 2:3 - Deleted corrupted footnote marker or picture anchor", report[0]);

			Assert.AreEqual(0, exodus.FootnotesOS.Count);
		}

#if WANTTESTPORT // (TE) FWR-774 - Need different testing strategy
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test <see cref="TeScrInitializer.FixOrcsWithoutProps()"/> method when the
		/// project has orphaned footnotes int the book title, introduction and section headings
		/// (still in the correct order).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FixOrcsWithoutProps_OrphanedFootnotesInTitleIntroAndHeading()
		{
			IScrBook exodus = CreateExodusData();
			CreateFootnote(exodus, exodus.TitleOA, 0, 0, 0, ScrStyleNames.NormalFootnoteParagraph, true);
			CreateFootnote(exodus, 0, 0, 1, 9, ScrStyleNames.NormalFootnoteParagraph, true);
			CreateFootnote(exodus, 1, 0, 2, 7, ScrStyleNames.NormalFootnoteParagraph, false);
			CreateFootnote(exodus, exodus.SectionsOS[2].HeadingOA, 0, 3, 7, ScrStyleNames.NormalFootnoteParagraph, true);
			CreateFootnote(exodus, 2, 0, 4, 13, ScrStyleNames.NormalFootnoteParagraph, false);

			TeScrInitializer scrInit = new TestTeScrInitializer(Cache);
			List<string> report = (List<string>)ReflectionHelper.GetResult(scrInit, "FixOrcsWithoutProps");

			VerifyNoOrphanedFootnotes();
			VerifyResourceForFixedOrphans();

			Assert.AreEqual(3, report.Count);
			Assert.AreEqual("EXO Title - Connected footnote to marker in the vernacular text", report[0]);
			Assert.AreEqual("EXO Intro Section 1, Contents - Connected footnote to marker in the vernacular text", report[1]);
			Assert.AreEqual("EXO 1:6-7 Section Heading - Connected footnote to marker in the vernacular text", report[2]);

			Assert.AreEqual(5, exodus.FootnotesOS.Count);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test <see cref="TeScrInitializer.FixOrcsWithoutProps()"/> method when the
		/// project has orphaned footnotes interspersed with non-orphaned footnotes such that
		/// some orphans occur in the sequence of footnotes after their correct place.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FixOrcsWithoutProps_OrphanedFootnotesOutOfOrder()
		{
			IScrBook exodus = CreateExodusData();
			CreateFootnote(exodus, 0, 0, 0, 4, ScrStyleNames.NormalFootnoteParagraph, false);
			CreateFootnote(exodus, 1, 0, 1, 7, ScrStyleNames.NormalFootnoteParagraph, true);
			CreateFootnote(exodus, 2, 0, 1 /* this causes this footnote to get inserted before the preceding one*/,
				7, ScrStyleNames.CrossRefFootnoteParagraph, false);
			CreateFootnote(exodus, 2, 0, 3, 14, ScrStyleNames.NormalFootnoteParagraph, true);

			TeScrInitializer scrInit = new TestTeScrInitializer(Cache);
			List<string> report = (List<string>)ReflectionHelper.GetResult(scrInit, "FixOrcsWithoutProps");

			VerifyResourceForFixedOrphans();

			Assert.AreEqual(3, report.Count);
			Assert.AreEqual("EXO 1:1 - Deleted corrupted footnote marker or picture anchor", report[0]);
			// Note that the last ORC gets hooked up to the first orpaned footnote, rather than to
			// its original footnote, leaving the last one as an orphan still.
			Assert.AreEqual("EXO 1:7 - Connected footnote to marker in the vernacular text", report[1]);
			Assert.AreEqual("EXO - Footnote 4 has no corresponding marker in the vernacular text", report[2]);

			Assert.AreEqual(4, exodus.FootnotesOS.Count);
		}
#endif

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Test <see cref="TeScrInitializer.FixOrcsWithoutProps()"/> method when the
		/// project has an orphaned footnote and no orphaned ORCs in the data.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void FixOrcsWithoutProps_OrphanedFootnotesWithNoOrcs()
		{
			IScrBook exodus = CreateExodusData();
			CreateFootnote(exodus, 0, 0, 0, 4, ScrStyleNames.NormalFootnoteParagraph, false);
			IScrFootnote footnote = Cache.ServiceLocator.GetInstance<IScrFootnoteFactory>().Create();
			exodus.FootnotesOS.Add(footnote);
			//IStTxtPara para = (IStTxtPara)footnote.ParagraphsOS.Append(new StTxtPara());
			//para.Contents = TsStringUtils.MakeTss("Poor orphaned footnote 1")

			TeScrInitializer scrInit = new TestTeScrInitializer(Cache);
			List<string> report = (List<string>)ReflectionHelper.GetResult(scrInit, "FixOrcsWithoutProps");

			VerifyResourceForFixedOrphans();

			Assert.AreEqual(1, report.Count);
			Assert.AreEqual("EXO - Footnote 2 has no corresponding marker in the vernacular text", report[0]);

			Assert.AreEqual(2, exodus.FootnotesOS.Count);
		}

		#region Helper methods for FixOrcsWithoutProps tests
			/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates a test footnote in the given location.
		/// </summary>
		/// <param name="book">The Scripture book.</param>
		/// <param name="iSection">The index of the section in the book.</param>
		/// <param name="iPara">The index of the Content paragraph where the footnote is to be
		/// inserted.</param>
		/// <param name="iFootnote">The index of the footnote to create.</param>
		/// <param name="ichOrc">The charaacter offset in the paragraph where the ORC (marker)
		/// is to be inserted.</param>
		/// <param name="sStyle">The style name (which determines whether it is a general
		/// footnote or a cross-reference).</param>
		/// <param name="fMakeOrphan">Flag indicating whether to make this footnote into an
		/// "orphan" be clearing the properties of the ORC.</param>
		/// ------------------------------------------------------------------------------------
		private void CreateFootnote(IScrBook book, int iSection, int iPara, int iFootnote,
			int ichOrc, string sStyle, bool fMakeOrphan)
		{
			CreateFootnote(book, book.SectionsOS[iSection].ContentOA, iPara, iFootnote,
				ichOrc, sStyle, fMakeOrphan);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates a test footnote in the given location.
		/// </summary>
		/// <param name="book">The Scripture book.</param>
		/// <param name="text">The StText containing iPara.</param>
		/// <param name="iPara">The index of the Content paragraph where the footnote is to be
		/// inserted.</param>
		/// <param name="iFootnote">The index of the footnote to create.</param>
		/// <param name="ichOrc">The charaacter offset in the paragraph where the ORC (marker)
		/// is to be inserted.</param>
		/// <param name="sStyle">The style name (which determines whether it is a general
		/// footnote or a cross-reference).</param>
		/// <param name="fMakeOrphan">Flag indicating whether to make this footnote into an
		/// "orphan" by clearing the properties of the ORC.</param>
		/// ------------------------------------------------------------------------------------
		private void CreateFootnote(IScrBook book, IStText text, int iPara, int iFootnote,
			int ichOrc, string sStyle, bool fMakeOrphan)
		{
			IScrTxtPara scrPara = (IScrTxtPara)text.ParagraphsOS[iPara];
			IStFootnote footnote = InsertTestFootnote(book, scrPara, iFootnote, ichOrc);
			IStTxtPara fnPara = AddParaToMockedText(footnote, sStyle);
			AddRunToMockedPara(fnPara, "Footnote " + Guid.NewGuid(), Cache.DefaultVernWs);

			if (fMakeOrphan)
			{
				ITsStrBldr bldr = scrPara.Contents.GetBldr();
				bldr.SetProperties(ichOrc, ichOrc + 1, TsStringUtils.PropsForWs(Cache.DefaultVernWs));
				scrPara.Contents = bldr.GetString();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Verifies that a CmResource was added to indicate that orphaned footnotes have been
		/// cleaned up for this project.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void VerifyResourceForFixedOrphans()
		{
			bool fResourceWasInserted = false;
			foreach (ICmResource resource in m_scr.ResourcesOC)
			{
				fResourceWasInserted |= (resource.Name == CmResourceTags.ksFixedOrphanedFootnotes &&
				resource.Version == CmResourceTags.kguidFixedOrphanedFootnotes);
			}
			Assert.IsTrue(fResourceWasInserted);
		}
		#endregion
		#endregion
	}
	#endregion
}
