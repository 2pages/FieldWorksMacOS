// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using NUnit.Framework;
using SIL.LCModel;
using SIL.LCModel.Core.Scripture;

namespace ParatextImport
{
	/// <summary />
	[TestFixture]
	public class DifferenceTests : ScrInMemoryLcmTestBase
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests cloning differences - basic.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void Clone()
		{
			IScrTxtPara[] paras = DiffTestHelper.CreateDummyParas(2, Cache);
			Difference diff = new Difference(
				new ScrReference(1, 1, 1, ScrVers.English),
				new ScrReference(1, 1, 30, ScrVers.English),
				paras[0], 1, 99, paras[1], 11, 88,
				DifferenceType.PictureDifference,
				null, null, "Whatever", "Whateverelse", "Esperanto", "Latvian",
				null, null);
			//diff.SectionsCurr = new int[] {6, 7, 8};

			Difference clonedDiff = diff.Clone();

<<<<<<< HEAD:Src/ParatextImportTests/DifferenceTests.cs
			Assert.AreEqual(1001001, (int)clonedDiff.RefStart);
			Assert.AreEqual(1001030, (int)clonedDiff.RefEnd);
||||||| f013144d5:Src/ParatextImport/ParatextImportTests/DifferenceTests.cs
			Assert.AreEqual(1001001, clonedDiff.RefStart);
			Assert.AreEqual(1001030, clonedDiff.RefEnd);
=======
			Assert.That((int)clonedDiff.RefStart, Is.EqualTo(1001001));
			Assert.That((int)clonedDiff.RefEnd, Is.EqualTo(1001030));
>>>>>>> develop:Src/ParatextImport/ParatextImportTests/DifferenceTests.cs
			Assert.AreSame(paras[0], clonedDiff.ParaCurr);
			Assert.AreEqual(1, clonedDiff.IchMinCurr);
			Assert.AreEqual(99, clonedDiff.IchLimCurr);
			Assert.AreSame(paras[1], clonedDiff.ParaRev);
			Assert.AreEqual(11, clonedDiff.IchMinRev);
			Assert.AreEqual(88, clonedDiff.IchLimRev);
			//Assert.AreEqual(987654321, clonedDiff.hvoAddedSection);
			Assert.AreEqual(DifferenceType.PictureDifference, clonedDiff.DiffType);
			Assert.That(clonedDiff.SubDiffsForParas, Is.Null);
			Assert.That(clonedDiff.SubDiffsForORCs, Is.Null);
			Assert.AreEqual("Whatever", clonedDiff.StyleNameCurr);
			Assert.AreEqual("Whateverelse", clonedDiff.StyleNameRev);
			Assert.AreEqual("Esperanto", clonedDiff.WsNameCurr);
			Assert.AreEqual("Latvian", clonedDiff.WsNameRev);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
<<<<<<< HEAD:Src/ParatextImportTests/DifferenceTests.cs
||||||| f013144d5:Src/ParatextImport/ParatextImportTests/DifferenceTests.cs
		/// Tests cloning differences when Difference contains sections.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		[Ignore("Enable when added sections are stored in array")]
		public void Clone_WithSections()
		{
			// wish we had a simpler constructor
			//Difference diffA = new Difference(
			//    new ScrReference(1, 1, 1, ScrVers.English), new ScrReference(1, 1, 30, ScrVers.English),
			//    DifferenceType.SectionAddedToCurrent,
			//    new int[] {6, 7, 8},
			//    4712, 11);
			////diff.SectionsCurr = new int[] {6, 7, 8};

			//Difference clonedDiff = diffA.Clone();

			//Assert.AreEqual(1001001, clonedDiff.RefStart);
			//Assert.AreEqual(1001030, clonedDiff.RefEnd);
			//Assert.AreEqual(DifferenceType.SectionAddedToCurrent, (DifferenceType)clonedDiff.DiffType);
			//Assert.AreEqual(6, clonedDiff.SectionsCurr[0]);
			//Assert.AreEqual(7, clonedDiff.SectionsCurr[1]);
			//Assert.AreEqual(8, clonedDiff.SectionsCurr[2]);
			//Assert.AreEqual(0, clonedDiff.ParaCurr);
			//Assert.AreEqual(0, clonedDiff.IchMinCurr);
			//Assert.AreEqual(0, clonedDiff.IchLimCurr);
			//Assert.AreEqual(4712, clonedDiff.ParaRev);
			//Assert.AreEqual(11, clonedDiff.IchMinRev);
			//Assert.AreEqual(11, clonedDiff.IchLimRev);
			//Assert.IsNull(clonedDiff.SubDifferences);
			//Assert.IsNull(clonedDiff.StyleNameCurr);
			//Assert.IsNull(clonedDiff.StyleNameRev);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
=======
		/// Tests cloning differences when Difference contains sections.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		[Ignore("Enable when added sections are stored in array")]
		public void Clone_WithSections()
		{
			// wish we had a simpler constructor
			//Difference diffA = new Difference(
			//    new ScrReference(1, 1, 1, ScrVers.English), new ScrReference(1, 1, 30, ScrVers.English),
			//    DifferenceType.SectionAddedToCurrent,
			//    new int[] {6, 7, 8},
			//    4712, 11);
			////diff.SectionsCurr = new int[] {6, 7, 8};

			//Difference clonedDiff = diffA.Clone();

			//Assert.AreEqual(1001001, clonedDiff.RefStart);
			//Assert.AreEqual(1001030, clonedDiff.RefEnd);
			//Assert.AreEqual(DifferenceType.SectionAddedToCurrent, (DifferenceType)clonedDiff.DiffType);
			//Assert.AreEqual(6, clonedDiff.SectionsCurr[0]);
			//Assert.AreEqual(7, clonedDiff.SectionsCurr[1]);
			//Assert.AreEqual(8, clonedDiff.SectionsCurr[2]);
			//Assert.AreEqual(0, clonedDiff.ParaCurr);
			//Assert.AreEqual(0, clonedDiff.IchMinCurr);
			//Assert.AreEqual(0, clonedDiff.IchLimCurr);
			//Assert.AreEqual(4712, clonedDiff.ParaRev);
			//Assert.AreEqual(11, clonedDiff.IchMinRev);
			//Assert.AreEqual(11, clonedDiff.IchLimRev);
			//Assert.That(clonedDiff.SubDifferences, Is.Null);
			//Assert.That(clonedDiff.StyleNameCurr, Is.Null);
			//Assert.That(clonedDiff.StyleNameRev, Is.Null);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
>>>>>>> develop:Src/ParatextImport/ParatextImportTests/DifferenceTests.cs
		/// Tests cloning differences when Difference contains multiple SubDifferences
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void Clone_WithSubDiffs()
		{
			IScrTxtPara[] paras = DiffTestHelper.CreateDummyParas(8, Cache);
			Difference subSubDiff = new Difference(
				new ScrReference(1, 1, 3, ScrVers.English), new ScrReference(1, 1, 30, ScrVers.English),
				paras[0], 0, 99, paras[1], 11, 88,
				DifferenceType.PictureDifference, null, null,
				"Whatever", "Whateverelse", "Esperanto", "Latvian",
				null, null);
			Difference subDiff1 = new Difference(
				new ScrReference(1, 1, 2, ScrVers.English), new ScrReference(1, 1, 30, ScrVers.English),
				paras[2], 0, 99, paras[3], 11, 88,
				DifferenceType.PictureDifference, new List<Difference>(new Difference[] { subSubDiff }),
				new List<Difference>(new Difference[] { subSubDiff }), "Whatever", "Whateverelse", "Esperanto", "Latvian",
				null, null);
			Difference subDiff2 = new Difference(
				new ScrReference(1, 1, 4, ScrVers.English), new ScrReference(1, 1, 30, ScrVers.English),
				paras[4], 0, 99, paras[5], 11, 88,
				DifferenceType.PictureDifference, null, null,
				"Whatever", "Whateverelse", "Esperanto", "Latvian",
				null, null);
			Difference diff = new Difference(
				new ScrReference(1, 1, 1, ScrVers.English), new ScrReference(1, 1, 30, ScrVers.English),
				paras[6], 0, 99, paras[7], 11, 88,
				DifferenceType.PictureDifference, new List<Difference>(new Difference[] { subDiff1, subDiff2 }),
				new List<Difference>(new Difference[] { subDiff1, subDiff2 }), "Whatever", "Whateverelse", "Esperanto", "Latvian",
				null, null);

			Difference clonedDiff = diff.Clone();

			Assert.AreEqual(2, clonedDiff.SubDiffsForORCs.Count);
			Assert.AreEqual(1, clonedDiff.SubDiffsForORCs[0].SubDiffsForORCs.Count);
			Assert.That(clonedDiff.SubDiffsForORCs[1].SubDiffsForORCs, Is.Null);
			Assert.That(clonedDiff.SubDiffsForORCs[0].SubDiffsForORCs[0].SubDiffsForORCs, Is.Null);

			Assert.AreEqual(2, clonedDiff.SubDiffsForParas.Count);
			Assert.AreEqual(1, clonedDiff.SubDiffsForParas[0].SubDiffsForParas.Count);
			Assert.That(clonedDiff.SubDiffsForParas[1].SubDiffsForParas, Is.Null);
			Assert.That(clonedDiff.SubDiffsForParas[0].SubDiffsForParas[0].SubDiffsForParas, Is.Null);
		}
	}
}
