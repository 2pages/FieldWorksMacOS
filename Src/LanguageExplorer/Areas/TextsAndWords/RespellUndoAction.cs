// Copyright (c) 2009-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LanguageExplorer.Controls;
using LanguageExplorer.Controls.XMLViews;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Application;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.SpellChecking;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;

namespace LanguageExplorer.Areas.TextsAndWords
{
	/// <summary>
	/// This class handles some of the functionality that is common to doing the action and previewing it.
	/// </summary>
	internal class RespellUndoAction : IUndoAction
	{
		/// <summary>
		/// The spelling change
		/// </summary>
		private readonly string m_oldSpelling;
		/// <summary>
		/// CBAs that represent occurrences we will change.
		/// </summary>
		private readonly HashSet<int> m_changes = new HashSet<int>();
		/// <summary>
		/// Key is hvo of StTxtPara, value is list (eventually sorted by BeginOffset) of
		/// CBAs that refer to it AND ARE BEING CHANGED.
		/// </summary>
		private readonly Dictionary<int, ParaChangeInfo> m_changedParas = new Dictionary<int, ParaChangeInfo>();
		private readonly XMLViewsDataCache m_specialSda;
		private readonly LcmCache m_cache;
		/// <summary>
		/// items requiring preview.
		/// </summary>
		private IEnumerable<int> m_occurrences;
		private ISegmentRepository m_repoSeg;
		private IStTxtParaRepository m_repoPara;
		private IWfiWordformRepository m_repoWf;
		private IWfiWordformFactory m_factWf;
		private IWfiAnalysisFactory m_factWfiAnal;
		private IWfiGlossFactory m_factWfiGloss;
		private IWfiMorphBundleFactory m_factWfiMB;
		private int m_tagPrecedingContext;
		private int m_tagPreview;
		private int m_tagAdjustedBegin;
		private int m_tagAdjustedEnd;
		private int m_tagEnabled;
		/// <summary>
		/// Case functions per writing system
		/// </summary>
		private readonly Dictionary<int, CaseFunctions> m_caseFunctions = new Dictionary<int, CaseFunctions>();
		private readonly int m_vernWs; // The WS we want to use throughout.
		private int[] m_oldOccurrencesNewWf; // occurrences of new spelling wordform before change
		private int[] m_newOccurrencesOldWf; // occurrences of original wordform after change
		private int[] m_newOccurrencesNewWf; // occurrences of new spelling after change.

		#region Properties

		/// <summary>
		/// HVO of wordform created (or found or made real) during DoIt for new spelling.
		/// </summary>
		internal int NewWordform { get; private set; }
		/// <summary>
		/// HVO of original wordform (possibly made real during DoIt) for old spelling.
		/// </summary>
		internal int OldWordform { get; private set; }

		internal string NewSpelling { get; }

		internal int[] OldOccurrencesOfOldWordform { get; private set; }

		internal RespellingSda RespellSda => ((DomainDataByFlidDecoratorBase)m_specialSda.BaseSda).BaseSda as RespellingSda;

		internal ISegmentRepository RepoSeg => m_repoSeg ?? (m_repoSeg = m_cache.ServiceLocator.GetInstance<ISegmentRepository>());

		internal IStTxtParaRepository RepoPara => m_repoPara ?? (m_repoPara = m_cache.ServiceLocator.GetInstance<IStTxtParaRepository>());

		internal IWfiWordformRepository RepoWf => m_repoWf ?? (m_repoWf = m_cache.ServiceLocator.GetInstance<IWfiWordformRepository>());

		internal IWfiWordformFactory FactWf => m_factWf ?? (m_factWf = m_cache.ServiceLocator.GetInstance<IWfiWordformFactory>());

		internal IWfiAnalysisFactory FactWfiAnal => m_factWfiAnal ?? (m_factWfiAnal = m_cache.ServiceLocator.GetInstance<IWfiAnalysisFactory>());

		internal IWfiGlossFactory FactWfiGloss => m_factWfiGloss ?? (m_factWfiGloss = m_cache.ServiceLocator.GetInstance<IWfiGlossFactory>());

		internal IWfiMorphBundleFactory FactWfiMB => m_factWfiMB ?? (m_factWfiMB = m_cache.ServiceLocator.GetInstance<IWfiMorphBundleFactory>());

		#endregion

		/// <summary />
		/// <remarks>Tests want cache.DefaultVernWs, so they skip sending that vernWs parameter.</remarks>
		internal RespellUndoAction(XMLViewsDataCache sda, LcmCache cache, string oldSpelling, string newSpelling, int vernWs = int.MinValue)
		{
			m_specialSda = sda;
			m_cache = cache;
			m_vernWs = vernWs == int.MinValue ? cache.DefaultVernWs : vernWs;
			m_oldSpelling = oldSpelling;
			NewSpelling = newSpelling;
		}

		internal bool PreserveCase { get; set; }

		/// <summary>
		/// Note one occurrence that we should change.
		/// </summary>
		internal void AddOccurrence(int hvoCba)
		{
			m_changes.Add(hvoCba);
		}
		internal void RemoveOccurrence(int hvoCba)
		{
			m_changes.Remove(hvoCba);
		}

		/// <summary>
		/// These three properties determine what we will do to a run which is corrected,
		/// but is not the primary spelling change in focus
		/// </summary>
		internal static int SecondaryTextProp => (int)FwTextPropType.ktptForeColor;

		internal static int SecondaryTextVar => (int)FwTextPropVar.ktpvDefault;

		internal static int SecondaryTextVal => (int)ColorUtil.ConvertColorToBGR(Color.Gray);

		/// <summary>
		/// Set up the appropriate preceding and following context for the given occurrence.
		/// </summary>
		internal void SetupPreviews(int tagPrecedingContext, int tagPreview, int tagAdjustedBegin, int tagAdjustedEnd, int tagEnabled, IEnumerable<int> occurrences, IVwRootBox rootb)
		{
			m_tagPrecedingContext = tagPrecedingContext;
			m_tagPreview = tagPreview;
			m_tagAdjustedBegin = tagAdjustedBegin;
			m_tagAdjustedEnd = tagAdjustedEnd;
			m_tagEnabled = tagEnabled;
			m_occurrences = occurrences;
			UpdatePreviews(false);
			RootBox = rootb;
		}

		/// <summary>
		/// Update all previews for the previously supplied list of occurrences.
		/// </summary>
		internal void UpdatePreviews(bool fPropChange)
		{
			// Build the dictionary that indicates what will change in each paragraph
			m_changedParas.Clear();
			BuildChangedParasInfo();
			ComputeParaChanges(false, null);
			UpdatePreviews(m_occurrences);
			if (fPropChange && RootBox != null)
			{
				foreach (var hvo in m_occurrences)
				{
					// This is enough PropChanged to redraw the whole containing paragraph
					RootBox.PropChanged(hvo, m_tagPreview, 0, 0, 0);
				}
			}
		}

		/// <summary>
		/// Return the original text indicated by the cba
		/// </summary>
		internal ITsString OldOccurrence(int hvoCba)
		{
			return OldOccurrence(hvoCba, 0);
		}

		/// <summary>
		/// Return the original text indicated by the cba
		/// </summary>
		/// <param name="hvoFake"></param>
		/// <param name="delta">Normally zero, in one case BeginOffset has already been adjusted by
		/// adding delta, need to subtract it here.</param>
		/// <returns></returns>
		internal ITsString OldOccurrence(int hvoFake, int delta)
		{
			var hvoTarget = GetTargetObject(hvoFake);
			var flid = FlidOfTarget(hvoTarget);
			var ws = 0;
			if (flid == CmPictureTags.kflidCaption)
			{
				ws = m_cache.DefaultVernWs;
			}
			var tssValue = AnnotationTargetString(hvoTarget, flid, ws, RespellSda);
			var bldr = tssValue.GetBldr();
			var ichBegin = BeginOffset(hvoFake) - delta;
			var ichLim = EndOffset(hvoFake);
			if (ichLim < bldr.Length)
			{
				bldr.Replace(ichLim, bldr.Length, string.Empty, null);
			}
			if (ichBegin > 0)
			{
				bldr.Replace(0, ichBegin, string.Empty, null);
			}
			return bldr.GetString();
		}

		// Enhance JohnT: could we get the LCM object and just ask whether it is StTxtPara/CmPicture?
		// This approach is brittle if we add subclasses of either.
		private int FlidOfTarget(int hvoTarget)
		{
			var clid = m_specialSda.get_IntProp(hvoTarget, CmObjectTags.kflidClass);
			switch (clid)
			{
				case ScrTxtParaTags.kClassId:
				case StTxtParaTags.kClassId:
					return StTxtParaTags.kflidContents;
				case CmPictureTags.kClassId:
					return CmPictureTags.kflidCaption;
				default:
					return 0;
			}
		}

		internal static ITsString AnnotationTargetString(int hvoTarget, int flid, int ws, RespellingSda sda)
		{
			return IsMultilingual(flid) ? sda.get_MultiStringAlt(hvoTarget, flid, ws) : sda.get_StringProp(hvoTarget, flid);
		}

		/// <summary>
		/// Update previews for the listed occurrences.
		/// </summary>
		private void UpdatePreviews(IEnumerable<int> occurrences)
		{
			foreach (var hvoFake in occurrences)
			{
				var hvoPara = GetTargetObject(hvoFake);
				ParaChangeInfo info;
				if (m_changedParas.TryGetValue(hvoPara, out info))
				{
					// We have to build a modified string, and we might find hvoCba in the list.
					// We also have to figure out how much our offset changed, if the new spelling differs in length.
					var bldr = info.NewContents.GetBldr();
					var delta = 0; // amount to add to offsets of later words.
					var beginTarget = BeginOffset(hvoFake);
					var ichange = 0;
					var fGotOffsets = false;
					for (; ichange < info.Changes.Count; ichange++)
					{
						var hvoChange = info.Changes[ichange];
						var beginChange = BeginOffset(hvoChange);
						if (hvoChange == hvoFake)
						{
							// stop preceding context just before the current one.
							var ich = BeginOffset(hvoFake) + delta;
							bldr.ReplaceTsString(ich, bldr.Length, OldOccurrence(hvoFake));
							m_specialSda.SetInt(hvoFake, m_tagAdjustedBegin, BeginOffset(hvoFake) + delta);
							m_specialSda.SetInt(hvoFake, m_tagAdjustedEnd, EndOffset(hvoFake) + delta);
							break;
						}
						if (beginChange > beginTarget && !fGotOffsets)
						{
							// This and future changes are after this occurrence, so the current delta is the one
							// we want (and this is an occurrence we are not changing, or we would have found it).
							SetOffsets(m_tagAdjustedBegin, m_tagAdjustedEnd, hvoFake, delta, beginTarget);
							fGotOffsets = true;
							// don't stop the loop, we want everything in the preceding context string, with later occurrences marked.
							// enhance JohnT: preceding context is the same for every unchanged occurrence in the paragraph,
							// if it is common to change some but not all in the same paragraph we could save it.
						}
						// It's another changed occurrence, not the primary one, highlight it.
						bldr.SetIntPropValues(beginChange + delta, beginChange + delta + NewSpelling.Length, SecondaryTextProp, SecondaryTextVar, SecondaryTextVal);
						delta += NewSpelling.Length - m_oldSpelling.Length;
					}
					m_specialSda.SetString(hvoFake, m_tagPrecedingContext, bldr.GetString());
					if (ichange < info.Changes.Count)
					{
						// need to set up following context also
						bldr = info.NewContents.GetBldr();
						// remove everything before occurrence.
						bldr.Replace(0, beginTarget + delta, string.Empty, null);
						// Make the primary occurrence bold
						bldr.SetIntPropValues(0, NewSpelling.Length, (int)FwTextPropType.ktptBold, (int)FwTextPropVar.ktpvEnum, (int)FwTextToggleVal.kttvForceOn);
						delta = -beginTarget + NewSpelling.Length - m_oldSpelling.Length;
						ichange++;
						for (; ichange < info.Changes.Count; ichange++)
						{
							var hvoChange = info.Changes[ichange];
							var beginChange = BeginOffset(hvoChange);
							// It's another changed occurrence, not the primary one, highlight it.
							bldr.SetIntPropValues(beginChange + delta, beginChange + delta + NewSpelling.Length, SecondaryTextProp, SecondaryTextVar, SecondaryTextVal);
							delta += NewSpelling.Length - m_oldSpelling.Length;
						}
						m_specialSda.SetString(hvoFake, m_tagPreview, bldr.GetString());
					}
					else if (!fGotOffsets)
					{
						// an unchanged occurrence after all the changed ones
						SetOffsets(m_tagAdjustedBegin, m_tagAdjustedEnd, hvoFake, delta, beginTarget);
					}
				}
				else
				{
					// Unchanged paragraph, copy the key info over.
					var flid = FlidOfTarget(hvoPara);
					var tssVal = IsMultilingual(flid) ? m_specialSda.get_MultiStringAlt(hvoPara, flid, m_cache.DefaultVernWs) : m_specialSda.get_StringProp(hvoPara, flid);
					m_specialSda.SetString(hvoFake, m_tagPrecedingContext, tssVal);
					m_specialSda.SetInt(hvoFake, m_tagAdjustedBegin, BeginOffset(hvoFake));
					m_specialSda.SetInt(hvoFake, m_tagAdjustedEnd, EndOffset(hvoFake));
				}
			}
		}

		private void SetOffsets(int tagAdjustedBegin, int tagAdjustedEnd, int hvoFake, int delta, int beginTarget)
		{
			m_specialSda.SetInt(hvoFake, tagAdjustedBegin, beginTarget + delta);
			m_specialSda.SetInt(hvoFake, tagAdjustedEnd, beginTarget + m_oldSpelling.Length + delta);
		}

		internal int BeginOffset(int hvoFake)
		{
			return m_specialSda.get_IntProp(hvoFake, ConcDecorator.kflidBeginOffset);
		}

		private int EndOffset(int hvoFake)
		{
			return m_specialSda.get_IntProp(hvoFake, ConcDecorator.kflidEndOffset);
		}

		/// <summary>
		/// Set up the dictionary which tracks changes for each paragraph.
		/// </summary>
		private void BuildChangedParasInfo()
		{
			foreach (var hvoCba in m_changes)
			{
				var info = EnsureParaInfo(hvoCba, 0);
				if (!info.Changes.Contains(hvoCba))
				{
					info.Changes.Add(hvoCba);
				}
			}
		}

		/// <summary>
		/// Determine the contents for each of the paragraphs to be changed, applying the
		/// changes immediately if so requested.
		/// </summary>
		private void ComputeParaChanges(bool fMakeChangeNow, ProgressDialogWorkingOn progress)
		{
			// Build the new strings for each
			foreach (var info1 in m_changedParas.Values)
			{
				info1.MakeNewContents(fMakeChangeNow, progress);
			}
		}

		private ParaChangeInfo EnsureParaInfo(int hvoFake, int hvoTargetPara)
		{
			var hvoPara = GetTargetObject(hvoFake);
			if (hvoTargetPara != 0 && hvoPara != hvoTargetPara)
			{
				return null;
			}
			if (m_changedParas.TryGetValue(hvoPara, out var info))
			{
				return info;
			}
			var flid = FlidOfTarget(hvoPara);
			var ws = 0;
			if (flid == CmPictureTags.kflidCaption)
			{
				ws = m_cache.DefaultVernWs;
			}
			Debug.Assert(flid != 0);
			info = new ParaChangeInfo(this, hvoPara, flid, ws);
			m_changedParas[hvoPara] = info;
			return info;
		}

		// For now it's good enough to treat anything but StTxtPara.Contents as multilingual.
		internal static bool IsMultilingual(int flid)
		{
			return flid != StTxtParaTags.kflidContents;
		}

		private int GetTargetObject(int hvoCba)
		{
			return m_specialSda.get_ObjectProp(hvoCba, ConcDecorator.kflidTextObject);
		}

		private IVwRootBox RootBox { get; set; }

		/// <summary>
		/// Update the preview when the check status of a single HVO changes.
		/// </summary>
		internal void UpdatePreview(int hvoChanged, bool isChecked)
		{
			if (m_changes.Contains(hvoChanged) == isChecked)
			{
				return;
			}
			if (isChecked)
			{
				m_changes.Add(hvoChanged);
			}
			else
			{
				m_changes.Remove(hvoChanged);
			}
			var hvoPara = GetTargetObject(hvoChanged);
			var occurrencesAffected = m_occurrences.Where(hvo => GetTargetObject(hvo) == hvoPara).ToList();
			var info = EnsureParaInfo(hvoChanged, hvoPara);
			if (isChecked)
			{
				info.Changes.Add(hvoChanged);
			}
			else
			{
				info.Changes.Remove(hvoChanged);
			}
			info.MakeNewContents(false, null);
			UpdatePreviews(occurrencesAffected);
			if (RootBox == null)
			{
				return;
			}
			{
				foreach (var hvo in occurrencesAffected)
				{
					// This is enough PropChanged to redraw the whole containing paragraph
					RootBox.PropChanged(hvo, m_tagEnabled, 0, 0, 0);
				}
			}
		}

		/// <summary>
		/// Actually make the change indicated in the action (execute the original command,
		/// from the Apply button in the dialog).
		/// </summary>
		public void DoIt(IPublisher publisher)
		{
			if (m_changes.Count == 0)
			{
				return;
			}
			BuildChangedParasInfo();

			if (m_changedParas.Count < 10)
			{
				CoreDoIt(null, publisher);
			}
			else
			{
				using (var dlg = new ProgressDialogWorkingOn())
				{
					dlg.Owner = Form.ActiveForm;
					dlg.Icon = dlg.Owner.Icon;
					dlg.Minimum = 0;
					// 2x accounts for two main loops; extra 10 very roughly accounts for final cleanup.
					dlg.Maximum = m_changedParas.Count * 2 + 10;
					dlg.Text = TextAndWordsResources.ksChangingSpelling;
					dlg.WorkingOnText = TextAndWordsResources.ksChangingSpelling;
					dlg.ProgressLabel = TextAndWordsResources.ksProgress;
					dlg.Show();
					dlg.BringToFront();
					CoreDoIt(dlg, publisher);
					dlg.Close();
				}
			}
		}

		/// <summary>
		/// Core of the DoIt method, may be called with or without progress dialog.
		/// </summary>
		private void CoreDoIt(ProgressDialogWorkingOn progress, IPublisher publisher)
		{
			var specialMdc = m_specialSda.MetaDataCache;
			var flidOccurrences = specialMdc.GetFieldId2(WfiWordformTags.kClassId, "Occurrences", false);
			using (var uuow = new UndoableUnitOfWorkHelper(m_cache.ActionHandlerAccessor, string.Format(TextAndWordsResources.ksUndoChangeSpelling, m_oldSpelling, NewSpelling), string.Format(TextAndWordsResources.ksRedoChangeSpelling, m_oldSpelling, NewSpelling)))
			{
				var wfOld = FindOrCreateWordform(m_oldSpelling, m_vernWs);
				var originalOccurencesInTexts = wfOld.OccurrencesInTexts.ToList(); // At all levels.
				var wfNew = FindOrCreateWordform(NewSpelling, m_vernWs);
				SetOldOccurrencesOfWordforms(flidOccurrences, wfOld, wfNew);
				UpdateProgress(progress);
				// It's important to do this BEFORE we update the changed paragraphs. As we update the analysis to point
				// at the new wordform and update the text, it may happen that AnalysisAdjuster sees the only occurrence
				// of the (new) wordform go away, if the text is being changed to an other-case form. If we haven't set
				// the spelling status first, the wordform may get deleted before we ever record its spelling status.
				// This way, having a known spelling status will prevent the deletion.
				SetSpellingStatus(wfNew);
				ComputeParaChanges(true, progress);
				if (progress != null)
				{
					progress.WorkingOnText = TextAndWordsResources.ksDealingAnalyses;
				}
				UpdateProgress(progress);
				// Compute new occurrence lists, save and cache
				SetNewOccurrencesOfWordforms(progress);
				UpdateProgress(progress);
				// Deal with analyses.
				if (wfOld.IsValidObject && CopyAnalyses)
				{
					// Note: "originalOccurencesInTexts" may have fewer segments, after the call, as they can be removed.
					CopyAnalysesToNewWordform(originalOccurencesInTexts, wfOld, wfNew);
				}
				UpdateProgress(progress);
				if (AllChanged)
				{
					SpellingHelper.SetSpellingStatus(m_oldSpelling, m_vernWs, m_cache.LanguageWritingSystemFactoryAccessor, false);
					if (wfOld.IsValidObject)
					{
						ProcessAnalysesAndLexEntries(progress, wfOld, wfNew);
					}
					UpdateProgress(progress);
				}
				// Only mess with shifting if it was only a case diff in wf, but no changes were made in paragraphs.
				// Regular spelling changes will trigger re-tokenization of para, otherwise
				if (PreserveCase)
				{
					// Move pointers in segments to new WF, if the segment references the original WF.
					foreach (var segment in originalOccurencesInTexts)
					{
						if (!m_changedParas.ContainsKey(segment.Owner.Hvo))
						{
							continue; // Skip shifting it for items that were not checked
						}
						var wfIdx = segment.AnalysesRS.IndexOf(wfOld);
						while (wfIdx > -1)
						{
							segment.AnalysesRS.RemoveAt(wfIdx);
							segment.AnalysesRS.Insert(wfIdx, wfNew);
							wfIdx = segment.AnalysesRS.IndexOf(wfOld);
						}
					}
				}
				// The timing of this is rather crucial. During the work above, we may (if this is invoked from a
				// wordform concordance) detect that the current occurrence is no longer valid (since we change the spelling
				// and that wordform no longer occurs in that position). This leads to reloading the list and broadcasting
				// a RecordNavigation message, as we switch the selection to the first item. However, before we reload the
				// list, we need to process ItemDataModified, because that figures out what the new list items are. If
				// it doesn't happen before we reload the list, we will put a bunch of invalid occurrences back into it.
				// Things to downhill from there as we try to select an invalid one.
				// OTOH, we can't figure out the new item data for the wordforms until the work above updates the occurrences!
				// The right solution is to wait until we have updated the instances, then send ItemDataModified
				// to update the ConcDecorator state, then close the UOW which triggers other PropChanged effects.
				// We have to use SendMessage so the ConcDecorator gets that updated before the record list using it
				// tries to re-read the list.
				if (wfOld.IsValidObject)
				{
					if (wfOld.CanDelete)
					{
						wfOld.Delete();
					}
					else
					{
						publisher.Publish(new PublisherParameterObject(LanguageExplorerConstants.ItemDataModified, wfOld));
					}
				}
				uuow.RollBack = false;
			}
		}

		private IWfiWordform FindOrCreateWordform(string sForm, int wsForm)
		{
			return RepoWf.GetMatchingWordform(wsForm, sForm) ?? FactWf.Create(TsStringUtils.MakeString(sForm, wsForm));
		}

		private void SetOldOccurrencesOfWordforms(int flidOccurrences, IWfiWordform wfOld, IWfiWordform wfNew)
		{
			OldWordform = wfOld.Hvo;
			OldOccurrencesOfOldWordform = m_specialSda.VecProp(wfOld.Hvo, flidOccurrences);
			NewWordform = wfNew.Hvo;
			m_oldOccurrencesNewWf = m_specialSda.VecProp(wfNew.Hvo, flidOccurrences);
		}

		private void SetNewOccurrencesOfWordforms(ProgressDialogWorkingOn progress)
		{
			var changes = new HashSet<int>();
			foreach (var info in m_changedParas.Values)
			{
				changes.UnionWith(info.Changes);
			}
			if (AllChanged)
			{
				m_newOccurrencesOldWf = new int[0]; // no remaining occurrences
			}
			else
			{
				// Only some changed, need to figure m_newOccurrences
				var newOccurrencesOldWf = new List<int>();
				foreach (var hvo in OldOccurrencesOfOldWordform)
				{
					//The offsets of our occurrences have almost certainly changed.
					//Update them so that the respelling dialog view will appear correct.
					var occur = RespellSda.OccurrenceFromHvo(hvo) as LocatedAnalysisOccurrence;
					if (occur != null)
					{
						occur.ResetSegmentOffsets();
					}
					if (!changes.Contains(hvo))
					{
						newOccurrencesOldWf.Add(hvo);
					}
				}
				m_newOccurrencesOldWf = newOccurrencesOldWf.ToArray();
			}
			UpdateProgress(progress);
			var newOccurrences = new List<int>(m_oldOccurrencesNewWf.Length + changes.Count);
			newOccurrences.AddRange(m_oldOccurrencesNewWf);
			newOccurrences.AddRange(changes);
			m_newOccurrencesNewWf = newOccurrences.ToArray();
			RespellSda.ReplaceOccurrences(OldWordform, m_newOccurrencesOldWf);
			RespellSda.ReplaceOccurrences(NewWordform, m_newOccurrencesNewWf);
			SendCountVirtualPropChanged(NewWordform);
			SendCountVirtualPropChanged(OldWordform);
		}

		private void SetSpellingStatus(IWfiWordform wfNew)
		{
			wfNew.SpellingStatus = (int)SpellingStatusStates.correct;
			SpellingHelper.SetSpellingStatus(NewSpelling, m_vernWs, m_cache.LanguageWritingSystemFactoryAccessor, true);
		}

		private void CopyAnalysesToNewWordform(ICollection<ISegment> originalOccurencesInTexts, IWfiWordform wfOld, IWfiWordform wfNew)
		{
			var shiftedSegments = new List<ISegment>(originalOccurencesInTexts.Count);
			foreach (var oldAnalysis in wfOld.AnalysesOC)
			{
				// Only copy approved analyses.
				if (oldAnalysis.GetAgentOpinion(m_cache.LangProject.DefaultUserAgent) != Opinions.approves)
				{
					continue;
				}
				var newAnalysis = FactWfiAnal.Create();
				wfNew.AnalysesOC.Add(newAnalysis);
				foreach (var segment in originalOccurencesInTexts)
				{
					if (!m_changedParas.ContainsKey(segment.Owner.Hvo))
					{
						continue; // Skip shifting it for items that were not checked
					}
					var analysisIdx = segment.AnalysesRS.IndexOf(oldAnalysis);
					while (analysisIdx > -1)
					{
						shiftedSegments.Add(segment);
						segment.AnalysesRS.RemoveAt(analysisIdx);
						segment.AnalysesRS.Insert(analysisIdx, newAnalysis);
						analysisIdx = segment.AnalysesRS.IndexOf(oldAnalysis);
					}
				}
				foreach (var shiftedSegment in shiftedSegments)
				{
					originalOccurencesInTexts.Remove(shiftedSegment);
				}
				shiftedSegments.Clear();
				foreach (var oldGloss in oldAnalysis.MeaningsOC)
				{
					var newGloss = FactWfiGloss.Create();
					newAnalysis.MeaningsOC.Add(newGloss);
					newGloss.Form.CopyAlternatives(oldGloss.Form);
					foreach (var segment in originalOccurencesInTexts)
					{
						if (!m_changedParas.ContainsKey(segment.Owner.Hvo))
						{
							continue; // Skip shifting it for items that were not checked
						}
						var glossIdx = segment.AnalysesRS.IndexOf(oldGloss);
						while (glossIdx > -1)
						{
							shiftedSegments.Add(segment);
							segment.AnalysesRS.RemoveAt(glossIdx);
							segment.AnalysesRS.Insert(glossIdx, newGloss);
							glossIdx = segment.AnalysesRS.IndexOf(oldGloss);
						}
					}
				}
				foreach (var shiftedSegment in shiftedSegments)
				{
					originalOccurencesInTexts.Remove(shiftedSegment);
				}
				foreach (var bundle in oldAnalysis.MorphBundlesOS)
				{
					var newBundle = FactWfiMB.Create();
					newAnalysis.MorphBundlesOS.Add(newBundle);
					newBundle.Form.CopyAlternatives(bundle.Form);
					newBundle.SenseRA = bundle.SenseRA;
					newBundle.MorphRA = bundle.MorphRA;
					newBundle.MsaRA = bundle.MsaRA;
				}
			}
		}

		private void ProcessAnalysesAndLexEntries(ProgressDialogWorkingOn progress, IWfiWordform wfOld, IWfiWordform wfNew)
		{
			wfOld.SpellingStatus = (int)SpellingStatusStates.incorrect;
			if (!KeepAnalyses)
			{
				// Remove multi-morpheme anals in src wf.
				foreach (var goner in wfOld.AnalysesOC.Where(goner => goner.MorphBundlesOS.Count > 1).ToList())
				{
					var wf = goner.OwnerOfClass<IWfiWordform>();
					wf.AnalysesOC.Remove(goner);
				}
			}
			if (UpdateLexicalEntries)
			{
				// Change LE allo on single morpheme anals.
				foreach (var update in wfOld.AnalysesOC)
				{
					if (update.MorphBundlesOS.Count != 1)
					{
						continue; // Skip any with zero or more than one.
					}
					var mb = update.MorphBundlesOS[0];
					var tss = mb.Form.get_String(m_vernWs);
					var srcForm = tss.Text;
					if (srcForm != null)
					{
						// Change morph bundle form.
						mb.Form.set_String(m_vernWs, NewSpelling);
					}
					var mf = mb.MorphRA;
					mf?.Form.set_String(m_vernWs, NewSpelling);
				}
			}
			// Move remaining anals from src wf to new wf.
			// This changes the owners of the remaining ones,
			// since it is an owning property.
			foreach (var anal in wfOld.AnalysesOC.ToList())
			{
				wfNew.AnalysesOC.Add(anal);
			}
		}

		internal static void UpdateProgress(ProgressDialogWorkingOn dlg)
		{
			if (dlg == null)
			{
				return;
			}
			dlg.PerformStep();
			dlg.Update();
		}

		private void SendCountVirtualPropChanged(int hvoWf)
		{
			// Notify everyone about the change in the virtual properties
			// for the three types of analyses.
			var virtualProperties = new List<string>
			{
				"HumanApprovedAnalyses",
				"HumanNoOpinionParses",
				"HumanDisapprovedParses",
				"FullConcordanceCount",
				"UserCount",
				"ParserCount",
				"ConflictCount"
			};
			foreach (var virtualProperty in virtualProperties)
			{
				WordformVirtualPropChanged(hvoWf, virtualProperty);
			}
		}

		private void WordformVirtualPropChanged(int hvoWf, string name)
		{
			RootBox?.PropChanged(hvoWf, m_specialSda.MetaDataCache.GetFieldId2(WfiWordformTags.kClassId, name, false), 0, 0, 1);
		}

		/// <summary>
		/// Flag may be set to true so that where a wordform has monomorphemic analysis/es,
		/// the lexical entry(s) will also be updated. That is, if this is true, and there is a
		/// monomorphemic analysis that points at a particular MoForm, the form of the MoForm will
		/// be updated.
		/// </summary>
		public bool UpdateLexicalEntries { get; set; }

		/// <summary>
		/// Flag set true if all occurrences changed. Enables effects of UpdateLexicalEntries
		/// and KeepAnalyses, and causes old wordform to be marked incorrect.
		/// </summary>
		public bool AllChanged { get; set; }

		/// <summary>
		/// Flag set true to keep analyses, even if all occurrences changed.
		/// (Monomorphemic analyses are kept anyway, if UpdateLexicalEntries is true.)
		/// </summary>
		public bool KeepAnalyses { get; set; }

		/// <summary>
		/// Flag set true to copy analyses.
		/// </summary>
		public bool CopyAnalyses { get; set; }

		#region IUndoAction Members

		public void Commit()
		{
		}

		public bool IsDataChange => true;

		public bool IsRedoable => true;

		public bool Redo()
		{
			return true;
		}

		public bool SuppressNotification
		{
			set { }
		}

		public bool Undo()
		{
			return true;
		}

		#endregion

		/// <summary>
		/// Remove all changed items from the set of enabled ones.
		/// </summary>
		internal void RemoveChangedItems(HashSet<int> enabledItems, int tagEnabled)
		{
			foreach (var hvoFake in m_changedParas.Values.SelectMany(info => info.Changes))
			{
				m_specialSda.SetInt(hvoFake, tagEnabled, 0);
				var matchingItem = (enabledItems.Where(item => item == hvoFake)).FirstOrDefault();
				if (matchingItem != 0) // 0 is the standard default value for ints.
				{
					enabledItems.Remove(matchingItem);
				}
			}
		}

		/// <summary>
		/// Gets the case function for the given writing system.
		/// </summary>
		internal CaseFunctions GetCaseFunctionFor(int ws)
		{
			if (m_caseFunctions.TryGetValue(ws, out var caseFunctions))
			{
				return caseFunctions;
			}
			caseFunctions = new CaseFunctions(m_cache.ServiceLocator.WritingSystemManager.Get(ws));
			m_caseFunctions[ws] = caseFunctions;
			return caseFunctions;
		}

		/// <summary>
		/// Information about how a paragraph is being changed. Also handles actually making the change.
		/// </summary>
		private sealed class ParaChangeInfo
		{
			readonly RespellUndoAction m_action;
			readonly int m_hvoTarget; // the one being changed.
			readonly int m_flid; // property being changed (typically paragraph contents, but occasionally picture caption)
			readonly int m_ws; // if m_flid is multilingual, ws of alternative; otherwise, zero.

			internal ParaChangeInfo(RespellUndoAction action, int hvoTarget, int flid, int ws)
			{
				m_action = action;
				m_hvoTarget = hvoTarget;
				m_flid = flid;
				m_ws = ws;
			}

			/// <summary>
			/// para contents if change proceeds.
			/// </summary>
			internal ITsString NewContents { get; set; }

			/// <summary>
			/// para contents if change does not proceed (or is undone).
			/// </summary>
			private ITsString OldContents { get; set; }

			/// <summary>
			/// Get the list of changes that are to be made to this paragraph, that is, CmBaseAnnotations
			/// which point at it and have been selected to be changed.
			/// </summary>
			internal List<int> Changes { get; } = new List<int>();

			/// <summary>
			/// Figure what the new contents needs to be. (Also sets OldContents.) Maybe even set
			/// the contents.
			/// </summary>
			/// <param name="fMakeChangeNow">if set to <c>true</c> make the actual change now;
			/// otherwise, just figure out what the new contents will be.</param>
			/// <param name="progress"></param>
			internal void MakeNewContents(bool fMakeChangeNow, ProgressDialogWorkingOn progress)
			{
				var sda = m_action.RespellSda;
				OldContents = RespellUndoAction.AnnotationTargetString(m_hvoTarget, m_flid, m_ws, sda);
				var bldr = OldContents.GetBldr();
				Changes.Sort((left, right) => sda.get_IntProp(left, ConcDecorator.kflidBeginOffset).CompareTo(sda.get_IntProp(right, ConcDecorator.kflidBeginOffset)));

				for (var i = Changes.Count - 1; i >= 0; i--)
				{
					var ichMin = sda.get_IntProp(Changes[i], ConcDecorator.kflidBeginOffset);
					var ichLim = sda.get_IntProp(Changes[i], ConcDecorator.kflidEndOffset);
					var replacement = Replacement(m_action.OldOccurrence(Changes[i]));
					bldr.Replace(ichMin, ichLim, replacement, null);
					if (!fMakeChangeNow)
					{
						continue;
					}
					var tssNew = bldr.GetString();
					if (OldContents.Equals(tssNew))
					{
						continue;
					}

					if (m_ws == 0)
					{
						sda.SetString(m_hvoTarget, m_flid, tssNew);
					}
					else
					{
						sda.SetMultiStringAlt(m_hvoTarget, m_flid, m_ws, tssNew);
					}
				}
				RespellUndoAction.UpdateProgress(progress);
				NewContents = bldr.GetString();
			}

			/// <summary>
			/// Update all the changed wordforms to point at the new Wf.
			/// Must be called before we change the text of the paragraph, as it
			/// depends on offsets into the old string.
			/// This is usually redundant, since updating the text of the paragraph automatically updates the
			/// segment analysis. However, this can be used to force an upper case occurrence to be analyzed
			/// as the lower-case wordform (FWR-3134). The paragraph adjustment does not force this back
			/// (at least not immediately).
			/// </summary>
			private void UpdateInstanceOf(ProgressDialogWorkingOn progress)
			{
#if JASONTODO
			// TODO: Fix it, or get rid of the method.
#endif
				Debug.Fail(@"use of this method was causing very unpleasant data corruption in texts, the bug it fixed needs addressing though.");
				var analysesToChange = new List<Tuple<ISegment, int>>();
				var sda = m_action.RespellSda;
				foreach (var hvoFake in Changes)
				{
					var hvoSeg = sda.get_ObjectProp(hvoFake, ConcDecorator.kflidSegment);
					var beginOffset = sda.get_IntProp(hvoFake, ConcDecorator.kflidBeginOffset);
					if (hvoSeg > 0)
					{
						var seg = m_action.RepoSeg.GetObject(hvoSeg);
						var canal = seg.AnalysesRS.Count;
						for (var i = 0; i < canal; ++i)
						{
							var anal = seg.AnalysesRS[i];
							if (anal.HasWordform && anal.Wordform.Hvo == m_action.OldWordform)
							{
								if (seg.GetAnalysisBeginOffset(i) == beginOffset)
								{
									// Remember that we want to change it, but don't do it yet,
									// because there may be other occurrences in this paragraph,
									// and changing the analysis to something which may have a different
									// length could mess things up.
									analysesToChange.Add(new Tuple<ISegment, int>(seg, i));
								}
							}
						}
					}
				}
				if (analysesToChange.Count > 0)
				{
					var newVal = new[] { m_action.RepoWf.GetObject(m_action.NewWordform) };
					foreach (var change in analysesToChange)
					{
						change.Item1.AnalysesRS.Replace(change.Item2, 1, newVal);
					}
				}
				RespellUndoAction.UpdateProgress(progress);
			}

			private string Replacement(ITsString oldTss)
			{
				var replacement = m_action.NewSpelling;
				if (!m_action.PreserveCase)
				{
					return replacement;
				}

				var ws = oldTss.get_Properties(0).GetIntPropValues((int)FwTextPropType.ktptWs, out _);
				var cf = m_action.GetCaseFunctionFor(ws);
				if (cf.StringCase(oldTss.Text) == StringCaseStatus.title)
				{
					replacement = cf.ToTitle(replacement);
				}
				return replacement;
			}
		}
	}
}