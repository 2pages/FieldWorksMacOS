// Copyright (c) 2008-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using LanguageExplorer.Areas.TextsAndWords.Interlinear;
using LanguageExplorer.Controls;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;

namespace LanguageExplorer.Areas.TextsAndWords.Discourse
{
	/// <summary>
	/// DiscourseExporter is an IVwEnv implementation which exports discourse data to an XmlWriter.
	/// Make one of these by creating an XmlTextWriter.
	/// Refactoring is probably in order to share more code with InterlinearExporter,
	/// or move common code down to CollectorEnv. This has been postponed in the interests
	/// of being able to release FW 5.2.1 without requiring changes to DLLs other than Discourse.
	/// </summary>
	internal sealed class DiscourseExporter : CollectorEnv, IDisposable
	{
		private readonly XmlWriter m_writer;
		private readonly LcmCache m_cache;
		private readonly IVwViewConstructor m_vc;
		private readonly HashSet<int> m_usedWritingSystems = new HashSet<int>();
		private int m_wsGloss;
		private readonly List<string> m_glossesInCellCollector = new List<string>();
		private readonly List<int> m_frags = new List<int>();
		private readonly IDsConstChart m_chart;
		private readonly IConstChartRowRepository m_rowRepo;
		private enum TitleStage
		{
			ktsStart,
			ktsGotFirstRowGroups,
			ktsGotNotesHeaderCell,
			ktsStartedSecondHeaderRow,
			ktsFinishedHeaders
		}
		// 0 = start, 1 = got first level titles, 2 = opened notes cell, 3 = opened first cell in in 2nd row,
		// 4 = ended that got first real row (and later).
		private TitleStage m_titleStage = TitleStage.ktsStart;
		private bool m_fNextCellReversed;
		private readonly int m_wsLineNumber; // ws to use for line numbers.

		public DiscourseExporter(LcmCache cache, XmlWriter writer, int hvoRoot, IVwViewConstructor vc, int wsLineNumber)
			: base(null, cache.MainCacheAccessor, hvoRoot)
		{
			m_cache = cache;
			m_writer = writer;
			m_vc = vc;
			m_wsLineNumber = wsLineNumber;
			m_chart = m_cache.ServiceLocator.GetInstance<IDsConstChartRepository>().GetObject(hvoRoot);
			m_rowRepo = m_cache.ServiceLocator.GetInstance<IConstChartRowRepository>();
		}

		#region Disposable stuff

		~DiscourseExporter()
		{
			Dispose(false);
		}

		private bool IsDisposed { get; set; }

		/// <inheritdoc />
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary />
		private void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}

			if (disposing)
			{
				// dispose managed objects
				m_writer.Dispose();
			}

			IsDisposed = true;
		}
		#endregion

		public void ExportDisplay()
		{
			m_writer.WriteStartElement("chart");
			m_writer.WriteStartElement("row"); // first header
			m_writer.WriteAttributeString("type", "title1");
			m_vc.Display(this, OpenObject, ConstChartVc.kfragPrintChart);
			m_writer.WriteEndElement();
			WriteLanguages();
		}

		/// <summary>
		/// Write out the languages element. This should be used in InterlinearExporter, too.
		/// </summary>
		private void WriteLanguages()
		{
			m_writer.WriteStartElement("languages");
			foreach (var ws in m_usedWritingSystems.Select(wsActual => m_cache.ServiceLocator.WritingSystemManager.Get(wsActual)))
			{
				m_writer.WriteStartElement("language");
				// we don't have enough context at this point to get all the possible writing system
				// information we may encounter in the word bundles.
				var wsId = ws.Id;
				m_writer.WriteAttributeString("lang", wsId);
				var fontName = ws.DefaultFontName;
				m_writer.WriteAttributeString("font", fontName);
				if (m_cache.ServiceLocator.WritingSystems.VernacularWritingSystems.Contains(ws))
				{
					m_writer.WriteAttributeString("vernacular", "true");
				}
				if (ws.RightToLeftScript)
				{
					m_writer.WriteAttributeString("RightToLeft", "true");
				}
				m_writer.WriteEndElement();
			}
			m_writer.WriteEndElement(); // languages
		}

		public override void AddStringProp(int tag, IVwViewConstructor vwvc)
		{
			switch (tag)
			{
				case ConstChartRowTags.kflidNotes:
					WriteStringProp(tag, "note", 0);
					break;
				case ConstChartRowTags.kflidLabel:
					switch (TopFragment)
					{
						case ConstChartVc.kfragChartRow:
							WriteStringProp(tag, "rownum", 0);
							break;
						case ConstChartVc.kfragComment:
							WriteStringProp(tag, "clauseMkr", 0, "target", DataAccess.get_StringProp(OpenObject, tag).Text);
							break;
					}
					break;
			}
		}

		private int TopFragment => m_frags.Any() ? m_frags[m_frags.Count - 1] : 0;

		public override void AddStringAltMember(int tag, int ws, IVwViewConstructor vwvc)
		{
			switch (tag)
			{
				case WfiWordformTags.kflidForm:
					if (m_frags.Contains(ConstChartVc.kfragMovedTextCellPart))
					{
						WriteStringProp(tag, "word", ws, "moved", "true");
					}
					else
					{
						WriteStringProp(tag, "word", ws);
					}
					break;
				case WfiGlossTags.kflidForm:
					m_wsGloss = ws;
					m_glossesInCellCollector.Add(DataAccess.get_MultiStringAlt(OpenObject, tag, m_wsGloss).Text ?? string.Empty);
					break;
				case ConstChartTagTags.kflidTag:
					WriteStringProp(tag, "lit", ws); // missing marker.
					break;
				case CmPossibilityTags.kflidName:
				case CmPossibilityTags.kflidAbbreviation:
					WriteStringProp(tag, "listRef", ws);
					break;
				default:
					Debug.Assert(false, "Unknown field in AddStringAltMember!");
					break;
			}
		}

		private void WriteStringProp(int tag, string elementTag, int alt, string extraAttr = null, string extraAttrVal = null)
		{
			var ws = alt;
			var tss = ws == 0 ? DataAccess.get_StringProp(OpenObject, tag) : DataAccess.get_MultiStringAlt(OpenObject, tag, alt);
			if (elementTag == "word")
			{
				WriteWordForm(elementTag, tss, ws, extraAttr);
			}
			else
			{
				WriteStringVal(elementTag, ws, tss, extraAttr, extraAttrVal);
			}
		}

		private void WriteWordForm(string elementTag, ITsString tss, int ws, string extraAttr)
		{
			WriteStringVal(elementTag, ws, tss, extraAttr, (extraAttr == null) ? null : "true");
		}

		private void WriteStringVal(string elementTag, int ws, ITsString tss, string extraAttr, string extraAttrVal)
		{
			m_writer.WriteStartElement(elementTag);
			if (extraAttr != null)
			{
				m_writer.WriteAttributeString(extraAttr, extraAttrVal);
			}
			WriteLangAndContent(ws, tss);
			m_writer.WriteEndElement();
		}

		private static int GetWsFromTsString(ITsString tss)
		{
			return tss.get_PropertiesAt(0).GetIntPropValues((int)FwTextPropType.ktptWs, out _);
		}

		public override void set_IntProperty(int tpt, int tpv, int nValue)
		{
			if (tpt == (int)FwTextPropType.ktptAlign && nValue == (int)FwTextAlign.ktalTrailing)
			{
				m_fNextCellReversed = true;
			}
			base.set_IntProperty(tpt, tpv, nValue);
		}

		/// <summary>
		/// Write a lang attribute identifying the string, then its content as the body of
		/// an element.
		/// </summary>
		private void WriteLangAndContent(int ws1, ITsString tss)
		{
			var ws = ws1;
			if (ws == 0)
			{
				ws = GetWsFromTsString(tss);
			}
			UpdateWsList(ws);
			var icuCode = m_cache.WritingSystemFactory.GetStrFromWs(ws);
			m_writer.WriteAttributeString("lang", icuCode);
			m_writer.WriteString(GetText(tss));
		}

		private void UpdateWsList(int ws)
		{
			// only add valid actual ws
			if (ws > 0)
			{
				m_usedWritingSystems.Add(ws);
			}
		}

		static string GetText(ITsString tss)
		{
			var result = tss.Text;
			return result?.Normalize() ?? string.Empty;
		}

		public override void AddObj(int hvoItem, IVwViewConstructor vc, int frag)
		{
			m_frags.Add(frag);
			base.AddObj(hvoItem, vc, frag);
			m_frags.RemoveAt(m_frags.Count - 1);
		}

		public override void AddString(ITsString tss)
		{
			if (TopFragment == ConstChartVc.kfragMTMarker)
			{
				WriteMTMarker(tss);
				return;
			}
			// Ignore directionality markers on export. Also skip empty strings and single spaces...
			// we handle extra space with the stylesheet.
			var text = tss.Text;
			if (text == "\x200F" || text == "\x200E" || string.IsNullOrEmpty(text) || text == " ")
			{
				return;
			}
			if ((m_vc as InterlinVc)?.IsDoingRealWordForm == true)
			{
				var ws = GetWsFromTsString(tss);
				WriteWordForm("word", tss, ws, m_frags.Contains(ConstChartVc.kfragMovedTextCellPart) ? "moved" : null);
			}
			else if (text == "***")
			{
				m_glossesInCellCollector.Add(tss.Text);
			}
			else
			{
				m_writer.WriteStartElement("lit");
				MarkNeedsSpace(tss.Text);
				WriteLangAndContent(GetWsFromTsString(tss), tss);
				m_writer.WriteEndElement();
				base.AddString(tss);
			}
		}

		private void WriteMTMarker(ITsString tss)
		{
			var ws = GetWsFromTsString(tss);
			var newTss = TsStringUtils.MakeString(tss == ((ConstChartVc)m_vc).m_sMovedTextBefore ? "Preposed" : "Postposed", ws);
			var hvoTarget = DataAccess.get_ObjectProp(OpenObject, ConstChartMovedTextMarkerTags.kflidWordGroup); // the CCWordGroup we refer to
			if (ConstituentChartLogic.HasPreviousMovedItemOnLine(m_chart, hvoTarget))
			{
				WriteStringVal("moveMkr", ws, newTss, "targetFirstOnLine", "false");
			}
			else
			{
				WriteStringVal("moveMkr", ws, newTss, null, null);
			}
		}

		/// <summary>
		/// Add attributes to an element just started to indicate whether white space is
		/// needed before or after the specified literal.
		/// The current implementation is simplistic, based on the few separators actually
		/// used in the discourse chart.
		/// The default is nothing added, indicating that white space IS needed.
		/// </summary>
		private void MarkNeedsSpace(string lit)
		{
			if (lit.StartsWith("]") || lit.StartsWith(")"))
			{
				m_writer.WriteAttributeString("noSpaceBefore", "true");
			}
			if (lit.EndsWith("[") || lit.EndsWith("(") || lit.EndsWith("-"))
			{
				m_writer.WriteAttributeString("noSpaceAfter", "true");
			}
		}

		public override void AddObjProp(int tag, IVwViewConstructor vc, int frag)
		{
			m_frags.Add(frag);
			base.AddObjProp(tag, vc, frag);
			m_frags.RemoveAt(m_frags.Count - 1);
		}

		/// <summary>
		/// Here we build the main structure of the chart as a collection of cells. We have to be a bit tricky about
		/// generating the header.
		/// </summary>
		public override void OpenTableCell(int nRowSpan, int nColSpan)
		{
			switch (m_titleStage)
			{
				case TitleStage.ktsStart when m_frags.Any() && m_frags[m_frags.Count - 1] == ConstChartVc.kfragColumnGroupHeader:
					// got the first group header
					m_titleStage = TitleStage.ktsGotFirstRowGroups;
					break;
				case TitleStage.ktsGotFirstRowGroups when !m_frags.Any():
					// got the column groups, no longer in that, next thing is the notes header
					m_titleStage = TitleStage.ktsGotNotesHeaderCell;
					break;
				case TitleStage.ktsGotNotesHeaderCell:
					// got the one last cell on the very first row, now starting the second row, close first and make a new row.
					m_writer.WriteEndElement();  // terminate the first header row.
					m_writer.WriteStartElement("row"); // second row headers
					m_writer.WriteAttributeString("type", "title2");
					m_titleStage = TitleStage.ktsStartedSecondHeaderRow;
					break;
			}
			m_writer.WriteStartElement("cell");
			if (m_fNextCellReversed)
			{
				m_fNextCellReversed = false;
				m_writer.WriteAttributeString("reversed", "true");
			}
			m_writer.WriteAttributeString("cols", nColSpan.ToString());
			m_writer.WriteStartElement("main");
			base.OpenTableCell(nRowSpan, nColSpan);
		}

		public override void CloseTableCell()
		{
			base.CloseTableCell();
			m_writer.WriteEndElement(); // the "main" element
			if (m_glossesInCellCollector.Any())
			{
				m_writer.WriteStartElement("glosses");
				foreach (var gloss in m_glossesInCellCollector)
				{
					m_writer.WriteStartElement("gloss");
					var icuCode = m_cache.WritingSystemFactory.GetStrFromWs(m_wsGloss);
					m_writer.WriteAttributeString("lang", icuCode);
					m_writer.WriteString(gloss);
					m_writer.WriteEndElement(); // gloss
				}
				// glosses
				m_writer.WriteEndElement();
				// Ready to start collecting for the next cell.
				m_glossesInCellCollector.Clear();
			}

			m_writer.WriteEndElement(); // cell
		}

		/// <summary>
		/// overridden to maintain the frags array.
		/// </summary>
		public override void AddObjVecItems(int tag, IVwViewConstructor vc, int frag)
		{
			m_frags.Add(frag);
			base.AddObjVecItems(tag, vc, frag);
			m_frags.RemoveAt(m_frags.Count - 1);
		}

		/// <summary>
		/// Called whenever we start the display of an object, we currently use it to catch the start of
		/// a row, based on the frag. Overriding OpenTableRow() might be more natural, but I was trying to
		/// minimize changes to other DLLs, and those routines are not currently virtual in the base class.
		/// </summary>
		protected override void OpenTheObject(int hvo, int ihvo)
		{
			var frag = m_frags[m_frags.Count - 1];
			switch (frag)
			{
				case ConstChartVc.kfragChartRow:
					if (m_titleStage == TitleStage.ktsStartedSecondHeaderRow)
					{
						// This is the best way I've found to detect the end of the second header row
						// and terminate it.
						m_titleStage = TitleStage.ktsFinishedHeaders;
						m_writer.WriteEndElement();
					}
					m_writer.WriteStartElement("row");
					var row = m_rowRepo.GetObject(hvo);
					if (row.EndParagraph)
					{
						m_writer.WriteAttributeString("endPara", "true");
					}
					else if (row.EndSentence)
					{
						m_writer.WriteAttributeString("endSent", "true");
					}
					var clauseType = ConstChartVc.GetRowStyleName(row);
					m_writer.WriteAttributeString("type", clauseType);
					var label = row.Label.Text;
					if (!string.IsNullOrEmpty(label))
					{
						m_writer.WriteAttributeString("id", label);
					}
					break;
				default:
					break;
			}
			base.OpenTheObject(hvo, ihvo);
		}

		protected override void CloseTheObject()
		{
			base.CloseTheObject();
			var frag = m_frags[m_frags.Count - 1];
			switch (frag)
			{
				case ConstChartVc.kfragChartRow:
					m_writer.WriteEndElement(); // row
					break;
				default:
					break;
			}
		}
	}
}