// Copyright (c) 2008-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using LanguageExplorer.Areas.TextsAndWords.Discourse;
using NUnit.Framework;
using SIL.LCModel;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainServices;
using SIL.Xml;

namespace LanguageExplorerTests.Areas.TextsAndWords.Discourse
{
	public class DiscourseTestHelper
	{
		internal Dictionary<IStTxtPara, AnalysisOccurrence[]> m_allOccurrences;
		internal IText m_text;
		internal IStText m_stText;

		#region Factories/Repositories

		private readonly ILcmServiceLocator m_servLoc;
		private readonly IWfiAnalysisFactory m_wAnalysisFact;
		private readonly IWfiGlossFactory m_wGlossFact;
		private readonly IConstChartRowFactory m_rowFact;
		private readonly IConstChartWordGroupFactory m_wordGrpFact;
		private readonly IConstChartTagFactory m_ccTagFact;
		private readonly IConstChartMovedTextMarkerFactory m_mtmFact;
		private readonly IConstChartClauseMarkerFactory m_clauseMrkrFact;
		private readonly IConstituentChartCellPartRepository m_partRepo; // for checking newly created CellPart hvos
		private readonly IConstChartRowRepository m_rowRepo; // for checking newly created ChartRow hvos

		#endregion
		private ICmPossibility m_template;
		private List<ICmPossibility> m_allColumns;

		public DiscourseTestHelper(LcmCache cache)
		{
			Cache = cache;

			#region Load Factories and Repositories

			m_servLoc = Cache.ServiceLocator;
			m_wAnalysisFact = m_servLoc.GetInstance<IWfiAnalysisFactory>();
			m_wGlossFact = m_servLoc.GetInstance<IWfiGlossFactory>();
			m_rowFact = m_servLoc.GetInstance<IConstChartRowFactory>();
			m_wordGrpFact = m_servLoc.GetInstance<IConstChartWordGroupFactory>();
			m_ccTagFact = m_servLoc.GetInstance<IConstChartTagFactory>();
			m_mtmFact = m_servLoc.GetInstance<IConstChartMovedTextMarkerFactory>();
			m_clauseMrkrFact = m_servLoc.GetInstance<IConstChartClauseMarkerFactory>();
			m_partRepo = m_servLoc.GetInstance<IConstituentChartCellPartRepository>();
			m_rowRepo = m_servLoc.GetInstance<IConstChartRowRepository>();

			#endregion

			m_text = m_servLoc.GetInstance<ITextFactory>().Create();
			m_stText = m_servLoc.GetInstance<IStTextFactory>().Create();
			m_text.ContentsOA = m_stText;
			m_allOccurrences = new Dictionary<IStTxtPara, AnalysisOccurrence[]>();
			FirstPara = MakeParagraph();

		}

		internal LcmCache Cache { get; }

		internal IStTxtPara FirstPara { get; }

		internal IDsConstChart Chart { get; set; }

		internal TestCCLogic Logic { get; set; }

		/// <summary>
		/// Make and parse a new paragraph and append it to the current text.
		/// In this version the test specifies the text (so it can know how many
		/// words it has.
		/// </summary>
		internal IStTxtPara MakeParagraphSpecificContent(string content)
		{
			var para0 = m_servLoc.GetInstance<IStTxtParaFactory>().Create();
			m_stText.ParagraphsOS.Add(para0);
			var tsstring = TsStringUtils.MakeString(content, Cache.DefaultVernWs);
			para0.Contents = tsstring;
			ParseTestParagraphWithSpecificContent(para0);
			return para0;
		}

		private void ParseTestParagraphWithSpecificContent(IStTxtPara paraToParse)
		{
			using (var pp = new ParagraphParser(Cache))
			{
				pp.Parse(paraToParse);
			}
			GlossParagraph(paraToParse);
			var temp = new List<AnalysisOccurrence>();
			foreach (var seg in paraToParse.SegmentsOS)
			{
				var formMax = seg.AnalysesRS.Count;
				for (var i = 0; i < formMax; i++)
				{
					temp.Add(new AnalysisOccurrence(seg, i));
				}
			}
			m_allOccurrences[paraToParse] = temp.ToArray();
		}

		/// <summary>
		/// Make and parse a new unique paragraph and append it to the current text.
		/// </summary>
		internal IStTxtPara MakeParagraph()
		{
			return MakeParagraphForGivenText(m_stText);
		}

		/// <summary>
		/// Make and parse a new unique paragraph and append it to the given text.
		/// </summary>
		internal IStTxtPara MakeParagraphForGivenText(IStText testText)
		{
			var para0 = m_servLoc.GetInstance<IStTxtParaFactory>().Create();
			testText.ParagraphsOS.Add(para0);
			var cPara = testText.ParagraphsOS.Count;
			var paraNum = cPara == 1 ? "one" : cPara.ToString();
			var tsstring = TsStringUtils.MakeString("this is paragraph " + paraNum + ". It is for our constituent chart database tests.", Cache.DefaultVernWs);
			para0.Contents = tsstring;
			ParseTestParagraph(para0);
			return para0;
		}

		/// <summary>
		/// Create a text other than the normal one and add it to the LanguageProject.
		/// </summary>
		internal IStText CreateANewText()
		{
			var servLoc = Cache.ServiceLocator;
			var mainTextObj = servLoc.GetInstance<ITextFactory>().Create();
			var strangeText = Cache.ServiceLocator.GetInstance<IStTextFactory>().Create();
			mainTextObj.ContentsOA = strangeText;
			return strangeText;
		}

		/// <summary>
		/// Creates a chart, adds it to the LangProject, sets a template, and adds it to the logic
		/// </summary>
		internal IDsConstChart SetupAChart()
		{
			// Note: do this AFTER creating the template, which may also create the DiscourseData object.
			Assert.IsNotNull(Cache.LangProject, "No LangProject in the cache!");
			var data = Cache.LangProject.DiscourseDataOA;
			Assert.IsNotNull(data, "No DiscourseData object!");
			Chart = Cache.ServiceLocator.GetInstance<IDsConstChartFactory>().Create(data, m_stText, m_template);
			Logic.Chart = Chart;
			Logic.Ribbon.CacheRibbonItems(new List<AnalysisOccurrence>());
			Cache.LangProject.GetDefaultChartMarkers();
			return Chart;
		}

		public ICmPossibility MakeTemplate(out List<ICmPossibility> allCols)
		{
			// The exact organization of columns is not of great
			// importance for the current tests (still less the names), but we do want there
			// to be a hierarchy, since that is a common problem, and naming them conventionally
			// may make debugging easier. Currently this is the same set of columns as
			// m_logic.CreateDefaultColumns, but we make it explicit here so most of the test
			// code is unaffected by changes to the default.
			var doc = new XmlDocument();
			doc.LoadXml(
				"<template name=\"default\">"
				+ "<column name=\"prenuclear\">"
				+ "<column name=\"prenuc1\"/>"
				+ "<column name=\"prenuc2\"/>"
				+ "</column>"
				+ "<column name=\"nucleus\">"
				+ "<column name=\"Subject\"/>"
				+ "<column name=\"verb\"/>"
				+ "<column name=\"object\"/>"
				+ "</column>"
				+ "<column name=\"postnuc\"/>"
				+ "</template>");
			m_template = Cache.LangProject.CreateChartTemplate(doc.DocumentElement);
			m_allColumns = Logic.AllColumns(m_template);
			allCols = m_allColumns;
			return m_template;
		}

		private void ParseTestParagraph(IStTxtPara paraToParse)
		{
			// Seg:  0                    1
			// Index:0    1  2         3  0  1  2   3   4           5     6        7
			//       this is paragraph x. It is for our constituent chart database tests. (where 'x' is the number of the paragraph)
			using (var pp = new ParagraphParser(Cache))
			{
				pp.Parse(paraToParse);
			}
			GlossParagraph(paraToParse);
			const int formMax = 12;
			var coords = new[,] { { 0, 0 }, { 0, 1 }, { 0, 2 }, { 0, 3 }, { 1, 0 }, { 1, 1 }, { 1, 2 }, { 1, 3 }, { 1, 4 }, { 1, 5 }, { 1, 6 }, { 1, 7 }};
			var temp = new AnalysisOccurrence[formMax];
			for (var i = 0; i < formMax; i++)
			{
				temp[i] = new AnalysisOccurrence(paraToParse.SegmentsOS[coords[i, 0]], coords[i, 1]);
			}
			m_allOccurrences[paraToParse] = temp;
		}

		private void GlossParagraph(IStTxtPara paraToParse)
		{
			var ich = 0;
			var wsForm = Cache.DefaultVernWs;
			var wsGloss = Cache.DefaultAnalWs;
			foreach (var seg in paraToParse.SegmentsOS)
			{
				for (var i = 0; i < seg.AnalysesRS.Count; i++)
				{
					var xform = seg.AnalysesRS[i];
					var word = xform.GetForm(wsForm).Text;
					ich += word.Length;
					if (!xform.HasWordform)
					{
						continue;
					}
					var wordform = xform.Wordform;
					var analysis = m_wAnalysisFact.Create(wordform, m_wGlossFact);
					ich++; // past space or dot
					var tssString = TsStringUtils.MakeString(word + "Gloss"+ ich, wsGloss);
					var gloss = analysis.MeaningsOC.FirstOrDefault();
					gloss.Form.set_String(wsGloss, tssString);
					seg.AnalysesRS[i] = gloss;
				}
			}
		}

		/// <summary>
		/// Cache all but the first nUsedAnalyses occurrences as the value of the analysisList.
		/// This one param version assumes first paragraph.
		/// </summary>
		/// <param name="nUsedAnalyses">-1 is magic for "All used"</param>
		/// <returns>The occurrences for the 1st paragraph</returns>
		internal AnalysisOccurrence[] MakeAnalysesUsedN(int nUsedAnalyses)
		{
			return MakeAnalysesUsedN(nUsedAnalyses, FirstPara);
		}

		/// <summary>
		/// Cache all but the first nUsedAnalyses occurrences with wordforms as the value of
		/// the OccurenceListId. Might be any paragraph.
		/// </summary>
		/// <param name="nUsedAnalyses">-1 is magic for "All used"</param>
		/// <param name="para"></param>
		/// <returns>The occurrences for the paragraph</returns>
		internal AnalysisOccurrence[] MakeAnalysesUsedN(int nUsedAnalyses, IStTxtPara para)
		{
			var allParaWords = m_allOccurrences[para].Where(point => point.HasWordform).ToList();
			if (nUsedAnalyses < 0)
			{
				nUsedAnalyses = allParaWords.Count; // nUsedAnalyses = -1 is magic for "all used"
			}
			var unusedWords = SubArray(allParaWords.ToArray(), nUsedAnalyses, allParaWords.Count);
			var sda = Logic.Ribbon.Decorator;
			var cachedAnalyses = sda.VecProp(m_stText.Hvo, Logic.Ribbon.OccurenceListId);
			if (cachedAnalyses.Length > 0)
			{
				// This is a nice theoretical problem we need to solve later, but it never happens in the tests.
				// I'll put in an assert just in case.
				Assert.Fail("I don't think any present tests go through here.");
			}
			else
			{
				var tempList = new List<AnalysisOccurrence>();
				tempList.AddRange(unusedWords);
				Logic.Ribbon.CacheRibbonItems(tempList);
			}
			return allParaWords.ToArray();
		}

		#region DefaultChartMarkers

		internal ICmPossibilityList MakeDefaultChartMarkers()
		{
			const string xml = "<list>"
							   + " <item name=\"Group1\" abbr=\"G1\">"
							   + " <item name=\"Group1.1\" abbr=\"G1.1\">"
							   + " <item name=\"Item1\" abbr=\"I1\"/>"
							   + " </item>"
							   + " </item>"
							   + " <item name=\"Group2\" abbr=\"G2\">"
							   + " <item name=\"Item2\" abbr=\"I2\"/>"
							   + " <item name=\"Item3\" abbr=\"I3\"/>"
							   + " </item>"
							   + "</list>";
			return MakeChartMarkers(xml);
		}

		internal ICmPossibilityList MakeChartMarkers(string xml)
		{
			var result = m_servLoc.GetInstance<ICmPossibilityListFactory>().Create();
			Cache.LangProject.DiscourseDataOA.ChartMarkersOA = result;
			var doc = new XmlDocument();
			doc.LoadXml(xml);
			MakeListXml(result, doc.DocumentElement);
			return result;
		}

		private void MakeListXml(ICmPossibilityList list, XmlElement root)
		{
			foreach (XmlNode item in root)
			{
				var poss = m_servLoc.GetInstance<ICmPossibilityFactory>().Create();
				list.PossibilitiesOS.Add(poss);
				InitItem(item, poss);

			}
		}

		private void InitItem(XmlNode item, ICmPossibility poss)
		{
			poss.Name.AnalysisDefaultWritingSystem = TsStringUtils.MakeString(XmlUtils.GetMandatoryAttributeValue(item, "name"), Cache.DefaultAnalWs);
			var abbr = XmlUtils.GetOptionalAttributeValue(item, "abbr");
			if (string.IsNullOrEmpty(abbr))
			{
				abbr = poss.Name.AnalysisDefaultWritingSystem.Text;
			}
			poss.Abbreviation.AnalysisDefaultWritingSystem = TsStringUtils.MakeString(abbr, Cache.DefaultAnalWs);
			foreach (XmlNode subItem in item.ChildNodes)
			{
				var poss2 = m_servLoc.GetInstance<ICmPossibilityFactory>().Create();
				poss.SubPossibilitiesOS.Add(poss2);
				InitItem(subItem, poss2);
			}
		}

		#endregion // DefaultChartMarkers

		/// <summary>
		/// Make a typical first row (we don't care about the label).
		/// If we DO care about the label and there will be other rows, use MakeRow() or MakeRow1a().
		/// </summary>
		/// <returns></returns>
		internal IConstChartRow MakeFirstRow()
		{
			return MakeRow(Chart, FirstRowLabel);
		}

		internal static string FirstRowLabel => "1";

		/// <summary>
		/// Make a typical second row (we might care about the label).
		/// Must be called after MakeRow1a.
		/// </summary>
		internal IConstChartRow MakeSecondRow()
		{
			return MakeRow(Chart, SecondRowLabel);
		}

		internal static string SecondRowLabel => "1b";

		/// <summary>
		/// Make a typical first row where there will be others
		/// </summary>
		internal IConstChartRow MakeRow1a()
		{
			return MakeRow(Chart, FirstClauseRowLabel);
		}

		internal static string FirstClauseRowLabel => "1a";

		internal IConstChartRow MakeRow(string lineNo)
		{
			return MakeRow(Chart, lineNo);
		}

		/// <summary>
		/// The LCM factory now inserts the row in a particular spot in the chart.
		/// This method assumes you want to put it at the end.
		/// </summary>
		internal IConstChartRow MakeRow(IDsConstChart chart, string lineNo)
		{
			return m_rowFact.Create(chart, chart.RowsOS.Count, TsStringUtils.MakeString(lineNo, Logic.WsLineNumber));
		}

		/// <summary>
		/// Make a chart WordGroup object for the specified column that groups the specified words
		/// The LCM factory now inserts the item in a particular spot in the row.
		/// This method assumes you want to put it at the end of the row.
		/// </summary>
		internal IConstChartWordGroup MakeWordGroup(IConstChartRow row, int icol, AnalysisOccurrence begPoint, AnalysisOccurrence endPoint)
		{
			Assert.Less(icol, m_allColumns.Count, "Invalid column index");
			return m_wordGrpFact.Create(row, row.CellsOS.Count, m_allColumns[icol], begPoint, endPoint);
		}

		/// <summary>
		/// Make a chart WordGroup object for the specified column that groups the specified words
		/// and append to the specified row. Assumes all IAnalysis objects are in the 1st paragraph.
		/// </summary>
		internal object MakeWordGroup(IConstChartRow row, int icol, IAnalysis[] analyses)
		{
			var begPoint = FindAnalysisInPara(analyses[0].Hvo, true);
			var endPoint = FindAnalysisInPara(analyses[analyses.Length - 1].Hvo, false);
			return MakeWordGroup(row, icol, begPoint, endPoint);
		}

		/// <summary>
		/// Returns an AnalysisOccurrence corresponding to the word
		/// </summary>
		/// <param name="para"></param>
		/// <param name="hvoAnalysisToFind"></param>
		/// <param name="fAtBeginning">if true, we are finding the first </param>
		/// <returns></returns>
		private AnalysisOccurrence FindAnalysisInPara(IStTxtPara para, int hvoAnalysisToFind, bool fAtBeginning)
		{
			var paraOccurrences = m_allOccurrences[para];
			var max = paraOccurrences.Length - 1;
			var start = fAtBeginning ? 0 : max;
			var incr = fAtBeginning ? 1 : -1;
			for (var i = start; i >= 0 && i <= max; i += incr)
			{
				if (paraOccurrences[i].Analysis.Hvo == hvoAnalysisToFind)
				{
					return paraOccurrences[i];
				}
			}
			return null; // Failure!
		}

		/// <summary>
		/// Returns an AnalysisOccurrence corresponding to the word (assuming first paragraph)
		/// </summary>
		/// <param name="hvoAnalysisToFind"></param>
		/// <param name="fAtBeginning">if true, we are finding the first </param>
		/// <returns></returns>
		private AnalysisOccurrence FindAnalysisInPara(int hvoAnalysisToFind, bool fAtBeginning)
		{
			return FindAnalysisInPara(FirstPara, hvoAnalysisToFind, fAtBeginning);
		}

		/// <summary>
		/// Makes a ChartTag object and appends it to the row
		/// </summary>
		internal IConstChartTag MakeChartMarker(IConstChartRow row, int icol, ICmPossibility marker)
		{
			Assert.Less(icol, m_allColumns.Count, "Invalid column index");
			Assert.IsNotNull(marker, "Invalid marker.");
			return m_ccTagFact.Create(row, row.CellsOS.Count, m_allColumns[icol], marker);
		}

		/// <summary>
		/// Makes a MissingText chart object and appends it to the row
		/// </summary>
		internal IConstChartTag MakeMissingMarker(IConstChartRow row, int icol)
		{
			Assert.Less(icol, m_allColumns.Count, "Invalid column index");
			return m_ccTagFact.CreateMissingMarker(row, row.CellsOS.Count, m_allColumns[icol]);
		}

		/// <summary>
		/// Makes a ChartMovedTextMarker object and appends it to the row
		/// </summary>
		internal IConstChartMovedTextMarker MakeMovedTextMarker(IConstChartRow row, int icol, IConstChartWordGroup target, bool fPreposed)
		{
			Assert.Less(icol, m_allColumns.Count, "Invalid column index");
			Assert.IsNotNull(target, "Can't make a MovedTextMarker with no target WordGroup");
			return m_mtmFact.Create(row, row.CellsOS.Count, m_allColumns[icol], fPreposed, target);
		}

		/// <summary>
		/// Make a dependent clause marker at the end of the specified row in the specified column
		/// for the specified clauses (rows) of the specified type. Caller supplies a marker.
		/// </summary>
		internal IConstChartClauseMarker MakeDependentClauseMarker(IConstChartRow row, int icol, IConstChartRow[] depClauses, ClauseTypes depType)
		{
			Assert.IsTrue(depType == ClauseTypes.Dependent || depType == ClauseTypes.Song || depType == ClauseTypes.Speech, "Invalid dependent type.");

			// Set ClauseType and begin/end group booleans in destination clauses
			foreach (var rowDst in depClauses)
			{
				rowDst.ClauseType = depType;
				if (rowDst == depClauses[0])
				{
					rowDst.StartDependentClauseGroup = true;
				}
				if (rowDst == depClauses[depClauses.Length - 1])
				{
					rowDst.EndDependentClauseGroup = true;
				}
			}
			// Create marker
			Assert.Less(icol, m_allColumns.Count, "Invalid column index");
			return m_clauseMrkrFact.Create(row, row.CellsOS.Count, m_allColumns[icol], depClauses);
		}

		/// <summary>
		/// Get some arbitrary chart marker.
		/// </summary>
		internal ICmPossibility GetAMarker()
		{
			return Cache.LangProject.DiscourseDataOA.ChartMarkersOA.PossibilitiesOS[1].SubPossibilitiesOS[0];
		}
		/// <summary>
		/// Get some arbitrary chart marker (different from GetAMarker()).
		/// </summary>
		internal ICmPossibility GetAnotherMarker()
		{
			return Cache.LangProject.DiscourseDataOA.ChartMarkersOA.PossibilitiesOS[1].SubPossibilitiesOS[1];
		}

		/// <summary>
		/// Copy a subset out of an array of objects.
		/// </summary>
		internal static T[] SubArray<T>(T[] input, int start, int count1)
		{
			var count = Math.Min(count1, input.Length - start);
			var result = new T[count];
			for (var i = 0; i < count; i++)
			{
				result[i] = input[start + i];
			}
			return result;
		}

		internal void VerifyFirstRow(int ccellParts)
		{
			VerifyRow(0, FirstRowLabel, ccellParts);
		}

		internal void VerifySecondRow(int ccellParts)
		{
			VerifyRow(1, SecondRowLabel, ccellParts);
		}

		/// <summary>
		/// Verify that the specified row of the chart exists, has the expected row-number comment,
		/// and the expected number of cell parts.
		/// </summary>
		internal void VerifyRow(int index, string rowNumber, int ccellParts)
		{
			var crows = Chart.RowsOS.Count;
			Assert.IsTrue(index <= crows);
			var row = Chart.RowsOS[index];
			Assert.IsNotNull(row, "Invalid Row object!");
			Assert.AreEqual(rowNumber, row.Label.Text, "Row has wrong number!");
			Assert.AreEqual(ccellParts, row.CellsOS.Count, "Row has wrong number of cell parts.");
		}

		/// <summary>
		/// Verify that the specified row of the chart exists and has the expected CellParts in Cells.
		/// </summary>
		internal void VerifyRowCells(int index, IConstituentChartCellPart[] cellParts)
		{
			var crows = Chart.RowsOS.Count;
			Assert.IsTrue(index < crows, "Invalid row index.");
			var row = Chart.RowsOS[index];
			Assert.IsNotNull(row, "Invalid Row object!");
			var ccellParts = row.CellsOS.Count;
			Assert.IsNotNull(row.Label.Text, "Row has no number!");
			Assert.AreEqual(cellParts.Length, row.CellsOS.Count);
			for (var i = 0; i < ccellParts; i++)
			{
				Assert.AreEqual(cellParts[i].Hvo, row.CellsOS[i].Hvo, string.Format("Wrong CellPart at index i={0}", i));
			}
		}

		/// <summary>
		/// Verify that the specified row of the chart exists and has the expected property values.
		/// </summary>
		/// <param name="index">row index</param>
		/// <param name="ct">ClauseType</param>
		/// <param name="ep">EndParagraph</param>
		/// <param name="es">EndSentence</param>
		/// <param name="sdcg">StartDependentClauseGroup</param>
		/// <param name="edcg">EndDependentClauseGroup</param>
		internal void VerifyRowDetails(int index, ClauseTypes ct, bool ep, bool es, bool sdcg, bool edcg)
		{
			var crows = Chart.RowsOS.Count;
			Assert.IsTrue(index < crows, "Invalid row index.");
			var row = Chart.RowsOS[index];
			Assert.IsNotNull(row, "Invalid Row object!");
			Assert.AreEqual(ep, row.EndParagraph, "EndParagraph property is wrong");
			Assert.AreEqual(es, row.EndSentence, "EndSentence property is wrong");
			Assert.AreEqual(sdcg, row.StartDependentClauseGroup, "StartDependentClauseGroup property is wrong");
			Assert.AreEqual(edcg, row.EndDependentClauseGroup, "EndDependentClauseGroup property is wrong");
		}

		/// <summary>
		/// Verify that there is a row with the specified index that has a cell part with the specified
		/// index which belongs to the specified column and references (through Cells) the specified words
		/// </summary>
		internal void VerifyWordGroup(int irow, int icellPart, ICmPossibility column, List<AnalysisOccurrence> words)
		{
			var cellPart = VerifyCellPartBasic(irow, icellPart, column);
			var wordGroup = cellPart as IConstChartWordGroup;
			Assert.IsNotNull(wordGroup, "Not a valid CCWordGroup cell part!");
			var cellWords = wordGroup.GetOccurrences();
			Assert.AreEqual(words, cellWords, "WordGroup has the wrong words");
		}

		private IConstituentChartCellPart VerifyCellPartBasic(int irow, int icellPart, ICmPossibility column)
		{
			Assert.IsNotNull(column, "Cell Part must be assigned to some column!");
			var crows = Chart.RowsOS.Count;
			Assert.IsTrue(irow < crows);
			var row = Chart.RowsOS[irow];
			Assert.IsNotNull(row, "Invalid row object!");
			var ccellParts = row.CellsOS.Count;
			Assert.IsTrue(icellPart < ccellParts);
			var cellPart = row.CellsOS[icellPart];
			Assert.IsNotNull(cellPart.ColumnRA, "Invalid column object!");
			Assert.AreEqual(column.Hvo, cellPart.ColumnRA.Hvo);
			return cellPart;
		}

		/// <summary>
		/// Verify that there is a row with the specified index that has a cell part (subclass ConstChartTag)
		/// with the specified index which belongs to the specified column and points to the specified
		/// marker possibility.
		/// </summary>
		internal void VerifyMarkerCellPart(int irow, int icellpart, ICmPossibility column, ICmPossibility marker)
		{
			Assert.IsNotNull(marker, "CCTag must have a CmPossibility");
			var cellPart = VerifyCellPartBasic(irow, icellpart, column) as IConstChartTag;
			Assert.IsNotNull(cellPart, "Cell part should be a ConstChartTag!");
			Assert.IsNotNull(cellPart.TagRA, "ConstChartTag is not assigned a possibility");
			Assert.AreEqual(marker.Hvo, cellPart.TagRA.Hvo);
		}

		/// <summary>
		/// Verify that there is a row with the specified index that has a cell part (subclass ConstChartTag)
		/// with the specified index which belongs to the specified column and has a null Tag.
		/// </summary>
		internal void VerifyMissingMarker(int irow, int icellPart, ICmPossibility column)
		{
			var cellPart = VerifyCellPartBasic(irow, icellPart, column) as IConstChartTag;
			Assert.IsNotNull(cellPart, "Cell part should be a ConstChartTag!");
			Assert.IsNull(cellPart.TagRA, "Missing Marker should not be assigned a Tag possibility!");
		}

		/// <summary>
		/// Verify that there is a row with the specified index that has a cell part
		/// (subclass ConstChartMovedTextMarker) with the specified index which belongs to the
		/// specified column and points to the specified WordGroup object in the right direction.
		/// </summary>
		internal void VerifyMovedTextMarker(int irow, int icellPart, ICmPossibility column, IConstChartWordGroup wordGroup, bool fPrepose)
		{
			Assert.IsNotNull(wordGroup, "CCMTMarker must refer to a wordgroup");
			var cellPart = VerifyCellPartBasic(irow, icellPart, column) as IConstChartMovedTextMarker;
			Assert.IsNotNull(cellPart, "Cell part should be a ConstChartMovedTextMarker!");
			Assert.IsNotNull(cellPart.WordGroupRA, "MovedText Marker does not refer to a word group");
			Assert.AreEqual(wordGroup.Hvo, cellPart.WordGroupRA.Hvo);
			Assert.AreEqual(fPrepose, cellPart.Preposed, "MTMarker is not pointing the right direction!");
		}

		/// <summary>
		/// Verify that there is a row with the specified index that has a cell part
		/// (subclass ConstChartClauseMarker) with the specified index which belongs to the
		/// specified column and points to the specified array of ConstChartRows.
		/// </summary>
		internal void VerifyDependentClauseMarker(int irow, int icellPart, ICmPossibility column, IConstChartRow[] depClauses)
		{
			Assert.IsNotNull(depClauses, "CCClauseMarker must refer to some rows");
			var cellPart = VerifyCellPartBasic(irow, icellPart, column) as IConstChartClauseMarker;
			Assert.IsNotNull(cellPart, "Cell part should be a ConstChartClauseMarker!");
			Assert.IsNotNull(cellPart.DependentClausesRS, "Clause Marker does not refer to any rows");
			Assert.AreEqual(depClauses.Length, cellPart.DependentClausesRS.Count, "Clause marker points to wrong number of rows");
			for (var i = 0; i < depClauses.Length; i++ )
			{
				Assert.AreEqual(depClauses[i].Hvo, cellPart.DependentClausesRS[i].Hvo, $"Clause array doesn't match at index {i}");
			}
		}

		/// <summary>
		/// Checks that the row label on the ConstChartRow object is as expected.
		/// </summary>
		internal void VerifyRowNumber(string label, IConstChartRow row, string msg)
		{
			var expected = TsStringUtils.MakeString(label, Logic.WsLineNumber).Text;
			var actual = row.Label.Text;
			Assert.AreEqual(expected, actual, msg);
		}

		/// <summary>
		/// Checks that the chart contains the ChartRows specified.
		/// </summary>
		public void VerifyChartRows(IDsConstChart chart, IConstChartRow[] chartRows)
		{
			Assert.AreEqual(chart.RowsOS.Count, chartRows.Length, "Chart has wrong number of rows");
			for (var i = 0; i < chartRows.Length; i++)
			{
				Assert.AreEqual(chartRows[i].Hvo, chart.RowsOS[i].Hvo, $"Chart has unexpected ChartRow object at index = {i}");
			}
		}

		/// <summary>
		/// Checks that the Hvos specified have been marked as deleted.
		/// </summary>
		/// <param name="hvos"></param>
		/// <param name="message">should contain {0} for hvo</param>
		public void VerifyDeletedHvos(int[] hvos, string message)
		{
			foreach (var hvoDel in hvos)
			{
				Assert.AreEqual((int)SpecialHVOValues.kHvoObjectDeleted, hvoDel, string.Format(message, hvoDel));
			}
		}

		/// <summary>
		/// Checks that the ConstituentChartCellPart repository now contains the Hvo specified.
		/// </summary>
		public IConstituentChartCellPart VerifyCreatedCellPart(int hvo, string message)
		{
			try
			{
				return m_partRepo.GetObject(hvo);
			}
			catch
			{
				Assert.Fail($"{message}. Hvo {hvo} isn't in the cellPart Repo!");
			}
			return null;
		}

		/// <summary>
		/// Checks that the ConstituentChartRow repository now contains the Hvo specified.
		/// </summary>
		public IConstChartRow VerifyCreatedRow(int hvo, string message)
		{
			try
			{
				return m_rowRepo.GetObject(hvo);
			}
			catch
			{
				Assert.Fail($"The hvo for {message} doesn't seem to be in the Row Repository!");
			}
			return null;
		}

		/// <summary>
		/// Verifies that the specified number of AnalysisOccurrences have been removed from
		/// the start of the original list.
		/// </summary>
		internal void AssertUsedAnalyses(MockRibbon mrib, AnalysisOccurrence[] allParaOccurrences, int removedAnalyses)
		{
			var allWords = allParaOccurrences.Where(point => point.HasWordform).ToList();
			var remainderAnalyses = SubArray(allWords.ToArray(), removedAnalyses, allWords.Count - removedAnalyses);

			var dummyHvoVec = mrib.Decorator.VecProp(m_stText.Hvo, mrib.OccurenceListId);
			var cdummyHvos = dummyHvoVec.Length;
			Assert.AreEqual(remainderAnalyses.Length, cdummyHvos);

			var ribbonAnalyses = LoadRibbonAnalyses(mrib, dummyHvoVec);
			for (var i = 0; i < cdummyHvos; i++)
			{
				Assert.AreEqual(remainderAnalyses[i].Analysis.Hvo, ribbonAnalyses[i].Hvo);
			}
		}

		private static IAnalysis[] LoadRibbonAnalyses(IInterlinRibbon mrib, int[] ribbonHvos)
		{
			var chvos = ribbonHvos.Length;
			var result = new IAnalysis[chvos];
			for (var i = 0; i < chvos; i++)
			{
				result[i] = ((InterlinRibbonDecorator)mrib.Decorator).OccurrenceFromHvo(ribbonHvos[i]).Analysis;
			}
			return result;
		}
	}
}