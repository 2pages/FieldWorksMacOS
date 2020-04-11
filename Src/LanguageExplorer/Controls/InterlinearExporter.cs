// Copyright (c) 2006-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;
using LanguageExplorer.Controls.DetailControls;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainServices;

namespace LanguageExplorer.Controls
{
	/// <summary>
	/// InterlinearExporter is an IVwEnv implementation which exports interlinear data to an XmlWriter.
	/// Make one of these by creating an XmlTextWriter.
	/// </summary>
	internal class InterlinearExporter : CollectorEnv
	{
		protected XmlWriter m_writer;
		protected LcmCache m_cache;
		private bool m_fItemIsOpen; // true while doing some <item> element (causes various things to output data)
		private bool m_fDoingHeadword; // true while displaying a headword (causes some special behaviors)
		private bool m_fDoingHomographNumber; // true after note dependency on homograph number, to end of headword.
		private bool m_fDoingVariantTypes; // can become true after processing headword and homograph number
		private bool m_fAwaitingHeadwordForm; // true after start of headword until we get a string alt.
		private bool m_fDoingMorphType; // true during display of MorphType of MoForm.
		private bool m_fDoingInterlinName; // true during MSA
		private bool m_fDoingGlossPrepend; // true after special AddProp
		private bool m_fDoingGlossAppend; // true after special AddProp
		private string m_sPendingPrefix; // got a prefix, need the ws from the form itself before we write it.
		private ITsString m_tssPendingHomographNumber;
		private List<ITsString> pendingTitles = new List<ITsString>(); // these come along before the right element is open.
		private List<ITsString> pendingSources = new List<ITsString>();
		private List<ITsString> pendingAbbreviations = new List<ITsString>();
		private List<ITsString> pendingComments = new List<ITsString>();
		private int m_flidStTextTitle;
		private int m_flidStTextSource;
		private InterlinVc m_vc;
		private readonly HashSet<int> m_usedWritingSystems = new HashSet<int>();
		/// <summary>saves the morphtype so that glosses can be marked as pro/enclitics.  See LT-8288.</summary>
		private Guid m_guidMorphType = Guid.Empty;
		private IMoMorphType m_mmtEnclitic;
		private IMoMorphType m_mmtProclitic;
		protected WritingSystemManager m_wsManager;
		protected ICmObjectRepository m_repoObj;

		internal static InterlinearExporter Create(string mode, LcmCache cache, XmlWriter writer, ICmObject objRoot, InterlinLineChoices lineChoices, InterlinVc vc)
		{
			return mode != null && mode.ToLowerInvariant() == "elan" ? new InterlinearExporterForElan(cache, writer, objRoot, lineChoices, vc) : new InterlinearExporter(cache, writer, objRoot, lineChoices, vc);
		}

		protected InterlinearExporter(LcmCache cache, XmlWriter writer, ICmObject objRoot, InterlinLineChoices lineChoices, InterlinVc vc)
			: base(null, cache.MainCacheAccessor, objRoot.Hvo)
		{
			m_cache = cache;
			m_writer = writer;
			m_flidStTextTitle = m_cache.MetaDataCacheAccessor.GetFieldId("StText", "Title", false);
			m_flidStTextSource = m_cache.MetaDataCacheAccessor.GetFieldId("StText", "Source", false);
			m_vc = vc;
			SetTextTitleAndMetadata(objRoot as IStText);
			// Get morphtype information that we need later.
			m_cache.ServiceLocator.GetInstance<IMoMorphTypeRepository>().GetMajorMorphTypes(out _, out _, out _, out _, out _, out m_mmtProclitic,
				out m_mmtEnclitic, out _, out _);
			m_wsManager = m_cache.ServiceLocator.WritingSystemManager;
			m_repoObj = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>();
		}

		internal void ExportDisplay()
		{
			m_vc.Display(this, OpenObject, InterlinVc.kfragStText);
		}

		internal virtual void WriteBeginDocument()
		{
			m_writer.WriteStartDocument();
			m_writer.WriteStartElement("document");
		}

		internal void WriteEndDocument()
		{
			m_writer.WriteEndElement();
			m_writer.WriteEndDocument();
		}

		internal string FreeAnnotationType { get; set; }

		public override void AddStringProp(int tag, IVwViewConstructor vwvc)
		{
			if (tag == PunctuationFormTags.kflidForm)
			{
				// <item type="punct">
				WriteItem(tag, "punct", 0);
			}
			else if (m_cache.GetManagedMetaDataCache().IsCustom(tag))
			{
				// custom fields are not multi-strings, so pass 0 for the ws
				WriteItem(tag, m_cache.MetaDataCacheAccessor.GetFieldName(tag), 0);
			}
		}

		public override void NoteDependency(int[] rghvo, int[] rgtag, int chvo)
		{
			if (m_fDoingHeadword && rgtag.Length == 1 && rgtag[0] == LexEntryTags.kflidHomographNumber)
			{
				m_fDoingHomographNumber = true;
			}
		}

		public override void AddStringAltMember(int tag, int ws, IVwViewConstructor vwvc)
		{
			//get the writing system for english for use in writing out the morphType, the types will ALWAYS be defined in english
			//but they may not be defined in other languages, using English on import and export should guarantee the correct behavior
			if (m_fDoingMorphType)
			{
				m_writer.WriteAttributeString("type", GetText(DataAccess.get_MultiStringAlt(OpenObject, tag, m_cache.WritingSystemFactory.GetWsFromStr("en"))));
				// also output the GUID so any tool that processes the output can know for sure which morphtype it is
				// Save the morphtype.  See LT-8288.
				m_guidMorphType = WriteGuidAttributeForCurrentObj();
			}
			else if (m_fDoingHeadword)
			{
				// Lexeme or citation form in headword. Dump it out, along with any saved affix marker.
				WritePrefixLangAlt(ws, tag);
				m_fAwaitingHeadwordForm = false;
			}
			else if (tag == MoFormTags.kflidForm)
			{
				if (m_fItemIsOpen)
				{
					WritePrefixLangAlt(ws, tag);
				}
			}
			else if (m_fItemIsOpen)
			{
				WriteLangAndContent(ws, tag == LexSenseTags.kflidGloss ? GetMarkedGloss(OpenObject, tag, ws) : DataAccess.get_MultiStringAlt(OpenObject, tag, ws));
			}
			else
			{
				switch (tag)
				{
					case WfiWordformTags.kflidForm:
						WriteItem(tag, "txt", ws);
						break;
					case WfiGlossTags.kflidForm:
						WriteItem(tag, "gls", ws);
						break;
					case WfiMorphBundleTags.kflidForm:
						WriteItem(tag, "txt", ws);
						break;
					case SegmentTags.kflidFreeTranslation:
					case SegmentTags.kflidLiteralTranslation:
					case NoteTags.kflidContent:
						WriteItem(tag, FreeAnnotationType, ws);
						break;
					default:
						if (tag == m_flidStTextTitle)
						{
							var tssTitle = DataAccess.get_MultiStringAlt(OpenObject, tag, ws);
							if (!pendingTitles.Contains(tssTitle))
							{
								pendingTitles.Add(tssTitle);
							}
						}
						else if (tag == m_flidStTextSource)
						{
							var source = DataAccess.get_MultiStringAlt(OpenObject, tag, ws);
							if (!pendingSources.Contains(source))
							{
								pendingSources.Add(source);
							}
						}
						else if (tag == CmMajorObjectTags.kflidDescription)
						{
							var comment = DataAccess.get_MultiStringAlt(OpenObject, tag, ws);
							if (!pendingComments.Contains(comment))
							{
								pendingComments.Add(comment);
							}
						}
						else
						{
							Debug.WriteLine("Export.AddStringAltMember(hvo={0}, tag={1}, ws={2})", OpenObject, tag, ws);
						}
						break;
				}
			}
		}

		protected Guid WriteGuidAttributeForCurrentObj()
		{
			return WriteGuidAttributeForObj(OpenObject);
		}

		protected Guid WriteGuidAttributeForObj(int hvo)
		{
			if (m_repoObj.IsValidObjectId(hvo))
			{
				var guid = m_repoObj.GetObject(hvo).Guid;
				m_writer.WriteAttributeString("guid", guid.ToString());
				return guid;
			}
			return Guid.Empty;
		}

		/// <summary>
		/// Glosses must be marked as proclitics or enclitics.  See LT-8288.
		/// </summary>
		private ITsString GetMarkedGloss(int hvo, int tag, int ws)
		{
			var tss = DataAccess.get_MultiStringAlt(hvo, tag, ws);
			string sPrefix = null;
			string sPostfix = null;
			if (m_guidMorphType == m_mmtEnclitic.Guid)
			{
				sPrefix = m_mmtEnclitic.Prefix;
				sPostfix = m_mmtEnclitic.Postfix;
			}
			else if (m_guidMorphType == m_mmtProclitic.Guid)
			{
				sPrefix = m_mmtProclitic.Prefix;
				sPostfix = m_mmtProclitic.Postfix;
			}
			if (sPrefix == null && sPostfix == null)
			{
				return tss;
			}
			var tsb = tss.GetBldr();
			if (!string.IsNullOrEmpty(sPrefix))
			{
				tsb.Replace(0, 0, sPrefix, null);
			}
			if (!string.IsNullOrEmpty(sPostfix))
			{
				tsb.Replace(tsb.Length, tsb.Length, sPostfix, null);
			}
			return tsb.GetString();
		}

		private void WritePrefixLangAlt(int ws, int tag)
		{
			m_writer.WriteAttributeString("lang", m_cache.LanguageWritingSystemFactoryAccessor.GetStrFromWs(ws));
			if (m_sPendingPrefix != null)
			{
				m_writer.WriteString(m_sPendingPrefix);
				m_sPendingPrefix = null;
			}
			m_writer.WriteString(GetText(DataAccess.get_MultiStringAlt(OpenObject, tag, ws)));
		}

		private void WriteItem(int tag, string itemType, int alt)
		{
			WriteItem(itemType, alt == 0 ? DataAccess.get_StringProp(OpenObject, tag) : DataAccess.get_MultiStringAlt(OpenObject, tag, alt));
		}

		private void WriteItem(string itemType, ITsString tss)
		{
			m_writer.WriteStartElement("item");
			m_writer.WriteAttributeString("type", itemType);
			WriteLangAndContent(GetWsFromTsString(tss), tss);
			m_writer.WriteEndElement();
		}

		private static int GetWsFromTsString(ITsString tss)
		{
			return tss.get_PropertiesAt(0).GetIntPropValues((int)FwTextPropType.ktptWs, out _);
		}

		/// <summary>
		/// Write a lang attribute identifying the string, then its content as the body of
		/// an element.
		/// </summary>
		private void WriteLangAndContent(int ws, ITsString tss)
		{
			UpdateWsList(ws);
			m_writer.WriteAttributeString("lang", m_cache.LanguageWritingSystemFactoryAccessor.GetStrFromWs(ws));
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

		private static string GetText(ITsString tss)
		{
			return tss.Text?.Normalize() ?? string.Empty;
		}

		public override void AddObj(int hvoItem, IVwViewConstructor vc, int frag)
		{
			switch (frag)
			{
				case (int)LcmUi.VcFrags.kfragHeadWord:
					// In the course of this AddObj, typically we get AddString calls for
					// morpheme separators, AddObjProp for kflidLexemeForm,
					// AddStringAltMember for the form of the LF, NoteDependency for a homograph number,
					// and possibly AddString for the HN itself. We want to produce something like
					// <item type="cf" lang="xkal">-de<hn lang="en">2</hn></item>
					OpenItem("cf");
					m_fDoingHeadword = true;
					m_fAwaitingHeadwordForm = true;
					break;
				case LcmUi.LexEntryVc.kfragVariantTypes:
					m_fDoingVariantTypes = true;
					OpenItem("variantTypes");
					var icuCode = m_cache.LanguageWritingSystemFactoryAccessor.GetStrFromWs(m_cache.DefaultAnalWs);
					m_writer.WriteAttributeString("lang", icuCode);
					break;
			}
			// (LT-9374) Export Variant Type information for variants
			if (vc is InterlinVc interlinVc && frag >= InterlinVc.kfragLineChoices && frag < InterlinVc.kfragLineChoices + interlinVc.LineChoices.Count)
			{
				var spec = interlinVc.LineChoices[frag - InterlinVc.kfragLineChoices];
				if (spec.Flid == InterlinLineChoices.kflidLexGloss)
				{
					OpenItem("gls");
				}
			}
			base.AddObj(hvoItem, vc, frag);
			switch (frag)
			{
				case (int)LcmUi.VcFrags.kfragHeadWord:
					CloseItem();
					m_fDoingHeadword = false;
					m_fDoingHomographNumber = false;
					WritePendingItem("hn", ref m_tssPendingHomographNumber);
					break;
				case LcmUi.LexEntryVc.kfragVariantTypes:
					CloseItem();
					m_fDoingVariantTypes = false;
					break;
			}
			if (!(vc is InterlinVc) || frag < InterlinVc.kfragLineChoices || frag >= InterlinVc.kfragLineChoices + ((InterlinVc)vc).LineChoices.Count)
			{
				return;
			}
			if (((InterlinVc)vc).LineChoices[frag - InterlinVc.kfragLineChoices].Flid == InterlinLineChoices.kflidLexGloss)
			{
				CloseItem();
			}
		}

		public override void AddProp(int tag, IVwViewConstructor vc, int frag)
		{
			switch (tag)
			{
				case InterlinVc.ktagGlossPrepend:
					m_fDoingGlossPrepend = true;
					break;
				case InterlinVc.ktagGlossAppend:
					m_fDoingGlossAppend = true;
					break;
			}

			base.AddProp(tag, vc, frag);
			switch (tag)
			{
				case InterlinVc.ktagGlossPrepend:
					m_fDoingGlossPrepend = false;
					break;
				case InterlinVc.ktagGlossAppend:
					m_fDoingGlossAppend = false;
					break;
			}
		}

		public override void AddTsString(ITsString tss)
		{
			if (m_fDoingGlossAppend)
			{
				WriteItem("glsAppend", tss);
			}
			else if (m_fDoingGlossPrepend)
			{
				WriteItem("glsPrepend", tss);
			}
			base.AddTsString(tss);
		}

		public override void AddString(ITsString tss)
		{
			// Ignore directionality markers on export.
			if (tss.Text == "\x200F" || tss.Text == "\x200E")
			{
				return;
			}
			if (m_fDoingHomographNumber)
			{
				m_tssPendingHomographNumber = tss;
			}
			else if (m_fDoingVariantTypes)
			{
				// For now just concatenate all the variant types info into one string (including separators [+,]).
				// NOTE: We'll need to re-evaluate this when we want this (and homograph item) to be
				// standard enough to import (see LT-9664).
				m_writer.WriteString(GetText(tss));
			}
			else if (m_fDoingHeadword)
			{
				if (m_fAwaitingHeadwordForm)
				{
					//prefix marker
					m_sPendingPrefix = GetText(tss);
				}
				else
				{
					// suffix, etc.; just write, we've done the lang attribute
					m_writer.WriteString(GetText(tss));
				}
			}
			else if (m_fDoingInterlinName)
			{
				WriteLangAndContent(GetWsFromTsString(tss), tss);
			}
			else if (m_vc.IsDoingRealWordForm)
			{
				WriteItem("txt", tss);
			}
			else if (m_vc.IsAddingSegmentReference)
			{
				WriteItem("segnum", tss);
			}
			base.AddString(tss);
		}

		public override void AddObjProp(int tag, IVwViewConstructor vc, int frag)
		{
			switch (frag)
			{
				case InterlinVc.kfragMorphType:
					m_fDoingMorphType = true;
					break;
				default:
					switch (tag)
					{
						case WfiAnalysisTags.kflidCategory:
							// <item type="pos"...AddStringAltMember will add the content...
							OpenItem("pos");
							break;
						case WfiMorphBundleTags.kflidMorph:
							OpenItem("txt");
							break;
						case WfiMorphBundleTags.kflidSense:
							OpenItem("gls");
							break;
						case WfiMorphBundleTags.kflidMsa:
							OpenItem("msa");
							m_fDoingInterlinName = true;
							break;
						default:
							break;
					}
					break;
			}
			base.AddObjProp(tag, vc, frag);

			switch (frag)
			{
				case InterlinVc.kfragStText:
					m_writer.WriteEndElement();
					break;
				case InterlinVc.kfragMorphType:
					m_fDoingMorphType = false;
					break;
				default:
					switch (tag)
					{
						case WfiAnalysisTags.kflidCategory:
						case WfiMorphBundleTags.kflidMorph:
						case WfiMorphBundleTags.kflidSense:
							CloseItem();
							break;
						case WfiMorphBundleTags.kflidMsa:
							CloseItem();
							m_fDoingInterlinName = false;
							break;
						default:
							break;
					}
					break;
			}
		}

		/// <summary>
		/// Write an item consisting of the specified string, and clear it.
		/// </summary>
		private void WritePendingItem(string itemType, ref ITsString tss)
		{
			if (tss == null)
			{
				return;
			}
			OpenItem(itemType);
			WriteLangAndContent(GetWsFromTsString(tss), tss);
			CloseItem();
			tss = null;
		}

		private void CloseItem()
		{
			m_writer.WriteEndElement();
			m_fItemIsOpen = false;
		}

		private void OpenItem(string itemType)
		{
			m_writer.WriteStartElement("item");
			m_writer.WriteAttributeString("type", itemType);
			m_fItemIsOpen = true;
		}

		/// <summary>
		/// This method (as far as I know) will be first called on the StText object, and then recursively from the
		/// base implementation for vector items in component objects.
		/// </summary>
		public override void AddObjVecItems(int tag, IVwViewConstructor vc, int frag)
		{
			ICmObject text = null;
			switch (frag)
			{
				case InterlinVc.kfragInterlinPara:
					m_writer.WriteStartElement("interlinear-text");
					//here the m_hvoCurr object is an StText object, store the IText owner
					//so that we can pull data from it to close out the interlinear-text element
					//Naylor 11-2011
					text = m_repoObj.GetObject(OpenObject).Owner;
					if (text is IScrBook || text is IScrSection)
					{
						m_writer.WriteAttributeString("guid", m_repoObj.GetObject(OpenObject).Guid.ToString());
					}
					else
					{
						m_writer.WriteAttributeString("guid", text.Guid.ToString());
					}
					foreach (var mTssPendingTitle in pendingTitles)
					{
						var hystericalRaisens = mTssPendingTitle;
						WritePendingItem("title", ref hystericalRaisens);
					}
					foreach (var mTssPendingAbbrev in pendingAbbreviations)
					{
						var hystericalRaisens = mTssPendingAbbrev;
						WritePendingItem("title-abbreviation", ref hystericalRaisens);
					}
					foreach (var source in pendingSources)
					{
						var hystericalRaisens = source;
						WritePendingItem("source", ref hystericalRaisens);
					}
					foreach (var desc in pendingComments)
					{
						var hystericalRaisens = desc;
						WritePendingItem("comment", ref hystericalRaisens);
					}
					m_writer.WriteStartElement("paragraphs");
					break;
				case InterlinVc.kfragParaSegment:
					m_writer.WriteStartElement("phrases");
					break;
				case InterlinVc.kfragBundle:
					m_writer.WriteStartElement("words");
					break;
				case InterlinVc.kfragMorphBundle:
					m_writer.WriteStartElement("morphemes");
					break;
				default:
					break;
			}
			base.AddObjVecItems(tag, vc, frag);
			switch (frag)
			{
				case InterlinVc.kfragInterlinPara:
					m_writer.WriteEndElement(); // paragraphs
					m_writer.WriteStartElement("languages");
					foreach (var wsActual in m_usedWritingSystems)
					{
						m_writer.WriteStartElement("language");
						// we don't have enough context at this point to get all the possible writing system
						// information we may encounter in the word bundles.
						m_writer.WriteAttributeString("lang", m_cache.LanguageWritingSystemFactoryAccessor.GetStrFromWs(wsActual));
						var ws = m_wsManager.Get(wsActual);
						m_writer.WriteAttributeString("font", ws.DefaultFontName);
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
					// languages
					m_writer.WriteEndElement();
					//Media files section
					if ((text as IText)?.MediaFilesOA != null)
					{
						var theText = (IText)text;
						m_writer.WriteStartElement("media-files");
						m_writer.WriteAttributeString("offset-type", theText.MediaFilesOA.OffsetType);
						foreach (var mediaFile in theText.MediaFilesOA.MediaURIsOC)
						{
							m_writer.WriteStartElement("media");
							m_writer.WriteAttributeString("guid", mediaFile.Guid.ToString());
							m_writer.WriteAttributeString("location", mediaFile.MediaURI);
							m_writer.WriteEndElement(); //media
						}
						m_writer.WriteEndElement(); //media-files
					}
					// interlinear-text
					m_writer.WriteEndElement();
					//wipe out the pending items to be clean for next text.
					pendingTitles.Clear();
					pendingSources.Clear();
					pendingAbbreviations.Clear();
					pendingComments.Clear();
					break;
				case InterlinVc.kfragParaSegment:
				case InterlinVc.kfragBundle:
				case InterlinVc.kfragMorphBundle:
					m_writer.WriteEndElement();
					break;
				default:
					break;
			}
		}

		protected override void OpenTheObject(int hvo, int ihvo)
		{
			// NOTE: this block executes BEFORE we update CurrentObject() by calling base.OpenTheObject() below.
			var tag = CurrentPropTag;
			switch (tag)
			{
				case StTextTags.kflidParagraphs:
					// The paragraph data may need to be loaded if the paragraph has not yet
					// appeared on the screen.  See LT-7071.
					m_vc.LoadDataFor(this, new[] { hvo }, 1, CurrentObject(), tag, -1, ihvo);
					WriteStartParagraph(hvo);
					break;
				case WfiAnalysisTags.kflidMorphBundles:
					WriteStartMorpheme(hvo);
					m_guidMorphType = Guid.Empty;   // reset morphtype before each morph bundle.
					break;
				case StTxtParaTags.kflidSegments:
					WriteStartPhrase(hvo);
					break;
				case SegmentTags.kflidAnalyses:
					WriteStartWord(hvo);
					break;
			}
			base.OpenTheObject(hvo, ihvo);
		}

		protected virtual void WriteStartMorpheme(int hvo)
		{
			m_writer.WriteStartElement("morph");

		}

		protected virtual void WriteStartWord(int hvo)
		{
			m_writer.WriteStartElement("word");
		}

		protected virtual void WriteStartPhrase(int hvo)
		{
			m_writer.WriteStartElement("phrase");
		}

		protected virtual void WriteStartParagraph(int hvo)
		{
			m_writer.WriteStartElement("paragraph");
		}

		protected override void CloseTheObject()
		{
			base.CloseTheObject();
			switch (CurrentPropTag)
			{
				case StTextTags.kflidParagraphs:
					m_writer.WriteEndElement();
					break;
				case WfiAnalysisTags.kflidMorphBundles:
					m_writer.WriteEndElement();
					m_guidMorphType = Guid.Empty;   // reset morphtype after each morph bundle.
					break;
				case StTxtParaTags.kflidSegments:
					m_writer.WriteEndElement();
					break;
				case SegmentTags.kflidAnalyses:
					m_writer.WriteEndElement();
					break;
			}
		}

		public override void AddUnicodeProp(int tag, int ws, IVwViewConstructor vwvc)
		{
			switch (tag)
			{
				case MoMorphTypeTags.kflidPrefix:
					m_sPendingPrefix = DataAccess.get_UnicodeProp(OpenObject, tag);
					break;
				case MoMorphTypeTags.kflidPostfix:
					m_writer.WriteString(DataAccess.get_UnicodeProp(OpenObject, tag));
					break;
			}
			base.AddUnicodeProp(tag, ws, vwvc);
		}

		/// <summary>
		/// This allows the exporter to be called multiple times with different roots.
		/// </summary>
		internal void SetRootObject(ICmObject objRoot)
		{
			OpenObject = objRoot.Hvo;
			SetTextTitleAndMetadata(objRoot as IStText);
		}

		/// <summary>
		/// Sets title, abbreviation, source and comment(description) data for the text.
		/// </summary>
		private void SetTextTitleAndMetadata(IStText txt)
		{
			if (txt == null)
			{
				return;
			}
			if (txt.Owner is IText text)
			{
				foreach (var writingSystemId in text.Name.AvailableWritingSystemIds)
				{
					pendingTitles.Add(text.Name.get_String(writingSystemId));
				}
				foreach (var writingSystemId in text.Abbreviation.AvailableWritingSystemIds)
				{
					pendingAbbreviations.Add(text.Abbreviation.get_String(writingSystemId));
				}
				foreach (var writingSystemId in text.Source.AvailableWritingSystemIds)
				{
					pendingSources.Add(text.Source.get_String(writingSystemId));
				}
				foreach (var writingSystemId in text.Description.AvailableWritingSystemIds)
				{
					pendingComments.Add(text.Description.get_String(writingSystemId));
				}
			}
			else if (TextSource.IsScriptureText(txt))
			{
				pendingTitles.Add(txt.ShortNameTSS);
				pendingAbbreviations.Add(null);
			}
		}

		/// <summary>
		/// This handles exporting interlinear data into an xml format that is friendly to ELAN's overlapping time sequences.
		/// (LT-9904)
		/// </summary>
		private sealed class InterlinearExporterForElan : InterlinearExporter
		{
			private const int kDocVersion = 2;
			internal InterlinearExporterForElan(LcmCache cache, XmlWriter writer, ICmObject objRoot, InterlinLineChoices lineChoices, InterlinVc vc)
				: base(cache, writer, objRoot, lineChoices, vc)
			{
			}

			internal override void WriteBeginDocument()
			{
				base.WriteBeginDocument();
				m_writer.WriteAttributeString("version", kDocVersion.ToString());
			}

			protected override void WriteStartParagraph(int hvo)
			{
				base.WriteStartParagraph(hvo);
				WriteGuidAttributeForObj(hvo);
			}

			protected override void WriteStartPhrase(int hvo)
			{
				base.WriteStartPhrase(hvo);
				WriteGuidAttributeForObj(hvo);
				var phrase = m_repoObj.GetObject(hvo) as ISegment;
				if (phrase?.MediaURIRA == null)
				{
					return;
				}
				m_writer.WriteAttributeString("begin-time-offset", phrase.BeginTimeOffset);
				m_writer.WriteAttributeString("end-time-offset", phrase.EndTimeOffset);
				if (phrase.SpeakerRA != null)
				{
					m_writer.WriteAttributeString("speaker", phrase.SpeakerRA.Name.BestVernacularAlternative.Text);
				}
				m_writer.WriteAttributeString("media-file", phrase.MediaURIRA.Guid.ToString());
			}

			protected override void WriteStartWord(int hvo)
			{
				base.WriteStartWord(hvo);
				// Note that this guid may well not be unique in the file, since it refers to a
				// WfiWordform, WfiAnalysis, WfiGloss, or PunctuationForm (the last is not output),
				// any of which may be referred to repeatedly in an analyzed text.
				if (m_repoObj.GetClsid(hvo) != PunctuationFormTags.kClassId)
				{
					WriteGuidAttributeForObj(hvo);
				}
			}
		}
	}
}