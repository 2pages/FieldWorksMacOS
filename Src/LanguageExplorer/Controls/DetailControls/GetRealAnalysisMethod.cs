// Copyright (c) 2006-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.DomainServices;

namespace LanguageExplorer.Controls.DetailControls
{
	/// <summary>
	/// Determine the real analysis corresponding to the current state of the sandbox.
	/// The 'real analysis' may be a WfiWordform (if it hasn't been analyzed at all), a WfiAnalysis,
	/// or a WfiGloss.
	/// </summary>
	internal class GetRealAnalysisMethod
	{
		protected CachePair m_caches;
		protected int m_hvoSbWord;
		private IWfiWordform m_wf;
		private IWfiGloss m_wg;
		private AnalysisTree m_oldAnalysis;
		// The sandbox we're working from.
		protected SandboxBase m_sandbox;
		protected int[] m_analysisMorphs;
		protected int[] m_analysisMsas;
		protected int[] m_analysisSenses;
		private int m_hvoCategoryReal;
		protected IWfiAnalysis m_wa;
		protected bool m_fWantOnlyWfiAnalysis;
		protected InterlinLineChoices m_choices;
		private readonly Form _mainFlexWindow;
		protected IHelpTopicProvider m_helpTopicProvider;
		protected ISilDataAccess m_sda;
		protected ISilDataAccess m_sdaMain;
		protected int m_cmorphs;// the form to use if we have to create a new WfiWordform.
		private ITsString m_tssForm;
		// These variables get filled in by CheckItOut. The Long message is suitable for a
		// MessageBox, the short one should fit in a status line. Currently always null.
		private string m_LongMessage;

		/// <summary>
		/// Only used to make the UpdateRealAnalysisMethod constructor happy. Do not use directly.
		/// </summary>
		internal GetRealAnalysisMethod()
		{
		}

		internal GetRealAnalysisMethod(Form mainFlexWindow, IHelpTopicProvider helpTopicProvider, SandboxBase owner, CachePair caches, int hvoSbWord, AnalysisTree oldAnalysis, IWfiAnalysis wa,
			IWfiGloss gloss, InterlinLineChoices choices, ITsString tssForm, bool fWantOnlyWfiAnalysis)
			: this()
		{
			_mainFlexWindow = mainFlexWindow;
			m_helpTopicProvider = helpTopicProvider;
			m_sandbox = owner;
			m_caches = caches;
			m_hvoSbWord = hvoSbWord;
			m_oldAnalysis = oldAnalysis;
			m_wf = oldAnalysis.Wordform;
			m_wa = wa;
			m_wg = gloss;
			m_sda = m_caches.DataAccess;
			m_sdaMain = m_caches.MainCache.MainCacheAccessor;
			m_cmorphs = m_sda.get_VecSize(m_hvoSbWord, SandboxBase.ktagSbWordMorphs);
			m_choices = choices;
			m_tssForm = tssForm;
			m_fWantOnlyWfiAnalysis = fWantOnlyWfiAnalysis;
		}

		/// <summary>
		/// Rather than deleting an obsolete analysis internally, we need to pass it
		/// back to the caller.  See LT-11502.
		/// </summary>
		internal IWfiAnalysis ObsoleteAnalysis { get; private set; }

		internal string ShortMessage { get; private set; }

		/// <summary>
		/// Run the algorithm, returning the 'analysis' hvo (WfiWordform, WfiAnalysis, or WfiGloss).
		/// </summary>
		internal IAnalysis Run()
		{
			CheckItOut();
			if (m_LongMessage == null)
			{
				return FinishItOff();
			}
			MessageBox.Show(m_LongMessage, LanguageExplorerResources.ksProblem);
			return null;
		}

		internal void CheckItOut()
		{
			m_LongMessage = null;
			ShortMessage = null;
		}

		/// <summary>
		/// Do the bulk of the computation, everything after initial error checking, which is now nonexistent.
		/// </summary>
		/// <returns>HVO of analysis (WfiWordform, WfiAnalyis, or WfiGloss)</returns>
		private IAnalysis FinishItOff()
		{
			var mainCache = m_caches.MainCache;
			var wfRepository = mainCache.ServiceLocator.GetInstance<IWfiWordformRepository>();
			if (m_wf == null)
			{
				IWfiWordform wf;
				// first see if we can find a matching form
				if (wfRepository.TryGetObject(m_tssForm, false, out wf))
				{
					m_wf = wf;
				}
				else
				{
					// The user selected a case form that did not previously have a WfiWordform.
					// Since he is confirming this analysis, we now need to create one.
					// Note: if in context of the wordforms DummyRecordList, the RecordList is
					// smart enough to handle inserting one object without having to Reload the whole list.
					m_wf = mainCache.ServiceLocator.GetInstance<IWfiWordformFactory>().Create(m_tssForm);
				}
			}
			// If sandbox contains only an empty morpheme string, don't consider this to be a true analysis.
			// Assume that the user was not finished with his analysis (cf. LT-1621).
			if (m_sandbox.IsAnalysisEmpty)
			{
				return m_wf;
			}
			// Update the wordform with any additional wss.
			var wordformWss = m_choices.OtherEnabledWritingSystemsForFlid(InterlinLineChoices.kflidWord, 0);
			// we need another way to detect the static ws for kflidWord.
			foreach (var wsId in wordformWss)
			{
				UpdateMlaIfDifferent(m_hvoSbWord, SandboxBase.ktagSbWordForm, wsId, m_wf.Hvo, WfiWordformTags.kflidForm);
			}
			// (LT-7807 later refined by FWR-3536)
			// if we're in a special mode for adding monomorphemic words to lexicon and the user's proposed analysis is monomorphemic,
			// if there is an existing possible analysis that matches on form, gloss, and POS, use it.
			// else if there is an existing possible analysis that matches on form and gloss and has no POS,
			//	update the POS and use it.
			// else if the occurrence is currently analyzed as a particular sense and there are no other occurrences
			//  of that sense, update the gloss and/or POS of the sense to match what we want (and use it)
			// else if there is a matching entry with the right form, add a suitable sense to use
			// else make a new entry and sense to use.
			if (m_sandbox.ShouldAddWordGlossToLexicon)
			{
				var matchingMorphItem = new MorphItem(0, null);
				// go through the combo options for lex entries / senses to see if we can find any existing matches.
				using (var handler = InterlinComboHandler.MakeCombo(_mainFlexWindow, m_helpTopicProvider, SandboxBase.ktagWordGlossIcon, m_sandbox, 0) as IhMorphEntry)
				{
					var morphItems = handler.MorphItems;
					// see if we can use an existing Sense, if it matches the word gloss and word MSA
					foreach (var morphItem in morphItems.Where(SbWordGlossMatchesSenseGloss).Where(SbWordPosMatchesSenseMsaPos))
					{
						// found a LexSense matching our Word Gloss and MSA POS
						matchingMorphItem = morphItem;
						break;
					}
					if (matchingMorphItem.m_hvoSense == 0)
					{
						// Next see if we can find an existing analysis where the gloss matches and POS is null.
						foreach (var morphItem in morphItems)
						{
							// skip lex senses that do not match word gloss and pos in the Sandbox
							if (!SbWordGlossMatchesSenseGloss(morphItem))
							{
								continue;
							}
							// found a LexSense matching our Word Gloss and that has no POS.
							var pos = m_caches.RealObject(m_sda.get_ObjectProp(m_hvoSbWord, SandboxBase.ktagSbWordPos)) as IPartOfSpeech;
							var sense = m_caches.MainCache.ServiceLocator.GetObject(morphItem.m_hvoSense) as ILexSense;
							var msa = sense?.MorphoSyntaxAnalysisRA as IMoStemMsa;
							if (msa == null || msa.PartOfSpeechRA != null)
							{
								continue; // for this case we can only use one that doesn't already have a POS.
							}
							msa.PartOfSpeechRA = pos; // adjust it
							if (m_oldAnalysis.WfiAnalysis != null) // always?
							{
								if (m_oldAnalysis.WfiAnalysis.CategoryRA != pos)
								{
									m_oldAnalysis.WfiAnalysis.CategoryRA = pos;
								}
							}
							matchingMorphItem = morphItem; // and use it.
							break;
						}
					}
					if (matchingMorphItem.m_hvoSense == 0 && m_oldAnalysis != null && m_oldAnalysis.WfiAnalysis != null)
					{
						// still don't have one we can use; see whether it is legitimate to modify the current
						// analysis.
						var oldAnalysis = m_oldAnalysis.WfiAnalysis;
						if (oldAnalysis.MorphBundlesOS.Count == 1 && oldAnalysis.MorphBundlesOS[0].SenseRA?.MorphoSyntaxAnalysisRA is IMoStemMsa && OnlyUsedThisOnce(oldAnalysis) && OnlyUsedThisOnce(oldAnalysis.MorphBundlesOS[0].SenseRA))
						{
							// We're allowed to change the existing sense and analysis! A side effect of updating the sense
							// is updating the MSA of the morph bundle of the oldAnalysis.
							var pos = m_caches.RealObject(m_sda.get_ObjectProp(m_hvoSbWord, SandboxBase.ktagSbWordPos)) as IPartOfSpeech;
							UpdateSense(oldAnalysis.MorphBundlesOS[0].SenseRA, pos);
							if (oldAnalysis.CategoryRA != pos)
							{
								oldAnalysis.CategoryRA = pos;
							}
							if (m_oldAnalysis.Gloss != null)
							{
								// if the old analysis is a gloss, update it also.
								CopyGlossesToWfiGloss(m_oldAnalysis.Gloss);
								return m_oldAnalysis.Gloss;
							}
							// Don't have an old gloss, create one.
							var newGloss = oldAnalysis.Services.GetInstance<IWfiGlossFactory>().Create();
							oldAnalysis.MeaningsOC.Add(newGloss);
							CopyGlossesToWfiGloss(newGloss);
							return newGloss;
						}
					}
					// If we get here we could not use an existing analysis with any safe modification.
					// if we couldn't use an existing sense but we match a LexEntry form,
					// add a new sense to an existing entry.
					ILexEntry bestEntry = null;
					if (morphItems.Any() && matchingMorphItem.m_hvoSense == 0)
					{
						// Tried using FindBestLexEntryAmongstHomographs() but it matches
						// only CitationForm which MorphItems doesn't know anything about,
						// and doesn't match against Allomorphs which MorphItems do track
						// so this could lead to a crash (LT-9430).
						//
						// Solution: if the user specified a category, see if we can find an entry
						// with a sense using that same category
						// otherwise just add the new sense to the first entry in MorphItems.
						var bestMorphItem = morphItems[0];
						foreach (var morphItem in morphItems.Where(SbWordMainPosMatchesSenseMsaMainPos))
						{
							bestMorphItem = morphItem;
							break;
						}
						bestEntry = bestMorphItem.GetPrimaryOrOwningEntry(m_caches.MainCache);
						// lookup this entry;
						matchingMorphItem = FindLexEntryMorphItem(morphItems, bestEntry);
					}

					if (matchingMorphItem.m_hvoMorph == 0)
					{
						// we didn't find a matching lex entry, so create a new entry
						handler.CreateNewEntry(true, out _, out _, out _);
					}
					else if (bestEntry != null)
					{
						// we found matching lex entry, so create a new sense for it
						var senseFactory = mainCache.ServiceLocator.GetInstance<ILexSenseFactory>();
						var newSense = senseFactory.Create(bestEntry, new SandboxGenericMSA(), string.Empty);
						// copy over any word glosses we're showing.
						CopyGlossesToSense(newSense);
						// copy over the Word POS
						((IMoStemMsa)newSense.MorphoSyntaxAnalysisRA).PartOfSpeechRA = (IPartOfSpeech)m_caches.RealObject(m_sda.get_ObjectProp(m_hvoSbWord, SandboxBase.ktagSbWordPos));
						var morph = mainCache.ServiceLocator.GetInstance<IMoFormRepository>().GetObject(matchingMorphItem.m_hvoMorph);
						handler.UpdateMorphEntry(morph, bestEntry, newSense);
					}
					else
					{
						// we found a matching lex entry and sense, so select it.
						var iMorphItem = morphItems.IndexOf(matchingMorphItem);
						handler.HandleSelect(iMorphItem);
					}
				}
			}
			BuildMorphLists(); // Used later on in the code.
			m_hvoCategoryReal = m_caches.RealHvo(m_sda.get_ObjectProp(m_hvoSbWord, SandboxBase.ktagSbWordPos));
			// We may need to create a new WfiAnalysis based on whether we have any sandbox gloss content.
			var fNeedGloss = false;
			var fWordGlossLineIsShowing = false; // Set to 'true' if the wrod gloss line is included in the m_choices fields.
			foreach (InterlinLineSpec ilc in m_choices.EnabledLineSpecs)
			{
				if (ilc.Flid == InterlinLineChoices.kflidWordGloss)
				{
					fWordGlossLineIsShowing = true;
					break;
				}
			}
			if (fWordGlossLineIsShowing)
			{
				// flag that we need to create wfi gloss information if any configured word gloss lines have content.
				if (m_choices.EnabledWritingSystemsForFlid(InterlinLineChoices.kflidWordGloss).Any(wsId => m_sda.get_MultiStringAlt(m_hvoSbWord, SandboxBase.ktagSbWordGloss, wsId).Length > 0))
				{
					fNeedGloss = true;
				}
			}
			// OK, we have some information that corresponds to an analysis. Find or create
			// an analysis that matches.
			var wsVern = m_sandbox.RawWordformWs;
			m_wa = FindMatchingAnalysis(true);
			var fFoundAnalysis = m_wa != null;
			if (!fFoundAnalysis)
			{
				// Clear the checksum on the wordform. This allows the parser filer to re-evaluate it and
				// delete the old analysis if it is just a simpler, parser-generated form of the one we're now making.
				m_wf.Checksum = 0;
				// Check whether there's a parser-generated analysis that the current settings
				// subsume.  If so, reuse that analysis by filling in the missing data (word gloss,
				// word category, and senses).
				// Another option is that there is an existing 'analysis' that is a trivial one,
				// created by word-only glossing. We can re-use that, filling in the other details
				// now supplied.
				var partialWa = FindMatchingAnalysis(false);
				var fNewAnal = partialWa == null;
				if (fNewAnal)
				{
					foreach (var ana in m_wf.AnalysesOC)
					{
						if (m_oldAnalysis != null && ana == m_oldAnalysis.WfiAnalysis && OnlyUsedThisOnce(ana) && SandboxBase.IsAnalysisHumanApproved(m_caches.MainCache, ana))
						{
							ObsoleteAnalysis = ana;
							break;
						}
					}
					// Create one.
					var waFactory = mainCache.ServiceLocator.GetInstance<IWfiAnalysisFactory>();
					var waNew = waFactory.Create();
					m_wf.AnalysesOC.Add(waNew);
					m_wa = waNew;
				}
				else
				{
					m_wa = partialWa;
					// For setting word glosses, we should treat this as a 'found' not new analysis
					// if it has any glosses, so we will search for and find any existing ones that match.
					fFoundAnalysis = m_wa.MeaningsOC.Count > 0;
				}
				IPartOfSpeech pos = null;
				if (m_hvoCategoryReal != 0)
				{
					pos = mainCache.ServiceLocator.GetInstance<IPartOfSpeechRepository>().GetObject(m_hvoCategoryReal);
				}
				m_wa.CategoryRA = pos;
				var mbFactory = mainCache.ServiceLocator.GetInstance<IWfiMorphBundleFactory>();
				var msaRepository = mainCache.ServiceLocator.GetInstance<IMoMorphSynAnalysisRepository>();
				var mfRepository = mainCache.ServiceLocator.GetInstance<IMoFormRepository>();
				var senseRepository = mainCache.ServiceLocator.GetInstance<ILexSenseRepository>();
				var inflTypeRepository = mainCache.ServiceLocator.GetInstance<ILexEntryInflTypeRepository>();
				for (var imorph = 0; imorph < m_cmorphs; imorph++)
				{
					IWfiMorphBundle mb;
					if (imorph >= m_wa.MorphBundlesOS.Count)
					{
						mb = mbFactory.Create();
						m_wa.MorphBundlesOS.Insert(imorph, mb);
					}
					else
					{
						mb = m_wa.MorphBundlesOS[imorph];
					}
					// An undo operation can leave stale information in the sandbox.  If
					// that happens, the stored database id values are invalid.  Set them
					// all to zero if the morph is invalid.  (See LT-3824 for a crash
					// scenario.)  This fix prevents a crash, but doesn't do anything for
					// restoring the original values before the operation that is undone.
					if (m_analysisMorphs[imorph] != 0 && !m_sdaMain.get_IsValidObject(m_analysisMorphs[imorph]))
					{
						m_analysisMorphs[imorph] = 0;
						m_analysisMsas[imorph] = 0;
						m_analysisSenses[imorph] = 0;
					}
					// Set the Morph of the bundle if we know a real one; otherwise, just set its Form
					if (m_analysisMorphs[imorph] == 0)
					{
						var hvoSbMorph = m_sda.get_VecItem(m_hvoSbWord, SandboxBase.ktagSbWordMorphs, imorph);
						mb.Form.set_String(wsVern, m_sandbox.GetFullMorphForm(hvoSbMorph));
					}
					else
					{
						mb.MorphRA = mfRepository.GetObject(m_analysisMorphs[imorph]);
					}
					// Set the MSA if we have one. Note that it is (pathologically) possible that the user has done
					// something in another window to destroy the MSA we remember, so don't try to set it if so.
					if (m_analysisMsas[imorph] != 0 && m_sdaMain.get_IsValidObject(m_analysisMsas[imorph]))
					{
						mb.MsaRA = msaRepository.GetObject(m_analysisMsas[imorph]);
					}
					// Likewise the Sense
					if (m_analysisSenses[imorph] != 0)
					{
						mb.SenseRA = senseRepository.GetObject(m_analysisSenses[imorph]);
						// set the InflType if we have one.
						var hvoSbInflType = GetRealHvoFromSbWmbInflType(imorph);
						mb.InflTypeRA = hvoSbInflType != 0 ? inflTypeRepository.GetObject(hvoSbInflType) : null;
					}
				}
			}
			else if (fWordGlossLineIsShowing) // If the line is not showing at all, don't bother.
			{
				// (LT-1428) Since we're using an existing WfiAnalysis,
				// We will find or create a new WfiGloss (even for blank lines)
				// if WfiAnalysis already has WfiGlosses
				//	or m_hvoWordGloss is nonzero
				//	or Sandbox has gloss content.
				var fSbGlossContent = fNeedGloss;
				var cGloss = m_wa.MeaningsOC.Count;
				fNeedGloss = cGloss > 0 || m_wg != null || fSbGlossContent;
			}
			if (m_wa != null)
			{
				EnsureCorrectMorphForms();
			}
			if (!fNeedGloss || m_fWantOnlyWfiAnalysis)
			{
				return m_wa;
			}
			if (m_wg != null)
			{
				// We may consider editing it instead of making a new one.
				// But ONLY if it belongs to the right analysis!!
				if (m_wg.Owner != m_wa)
				{
					m_wg = null;
				}
			}
			/* These are the types of things we are trying to accomplish here.
			Problem 1 -- Correcting a spelling mistake.
				Gloss1: mn <-
			User corrects spelling to men
			Desired:
				Gloss1: men <-
			Bad result:
				Gloss1: mn
				Gloss2: men <-

			Problem 2 -- Switching to a different gloss via typing.
				Gloss1: he <-
				Gloss2: she
			User types in she rather than using dropdown box to select it
			Desired:
				Gloss1: he
				Gloss2: she <-
			Bad result:
				Gloss1: she <-
				Gloss2: she

			Problem 2A
						Gloss1: he <-
			User types in she without first using dropdown box to select "add new gloss"
			Desired:
						Gloss1: he (still used for N other occurrences)
						Gloss2: she <-
			Bad (at least dubious) result:
						Gloss1: she <- (used for this and all other occurrences)

			Problem 3 -- Adding a missing alternative when there are not duplications.
				Gloss1: en=green <-
			User adds the French equivalent
			Desired:
				Gloss1: en=green, fr=vert <-
			Bad result:
				Gloss1: en=green
				Gloss2: en=green, fr=vert <-

			The logic used to be to look for glosses with all alternatives matching or else it
			creates a new one. So 2 would actually work, but 1 and 3 were both bad.

			New logic: keep track of the starting WfiAnalysis and WfiGloss.
			Assuming we haven't changed to a new WfiAnalysis based on other changes, if there
			is a WfiGloss that matches any of the existing alternatives, we switch to that.
			Otherwise we set the alternatives of the starting WfiGloss to whatever the user
			entered. This logic would work for all three examples above, but has problems
			with the following.

			Problem -- add a missing gloss where there are identical glosses in another ws.
				Gloss1: en=them <-
			User adds Spanish gloss
			Desired:
				Gloss1: en=them, es=ellas <-
			This works ok with above logic. But now in another location the user needs to
			use the masculine them in Spanish, so changes ellas to ellos
			Desired:
				Gloss1: en=them, es=ellas
				Gloss2: en=them, es=ellos <-
			Bad result:
				Gloss1: en=them, es=ellos <-

			Eventually, we'll probably want to display a dialog box to ask users what they really want.
			"There are 15 places where "XXX" analyzed as 3pp is currently glossed
			en->"them".  Would you like to
			<radio button, selected> change them all to en->"them" sp->"ellas"?
			<radio button> leave the others glossed en->"them" and let just this one
			be en->"them" sp->"ellas"?
			<radio button> see a concordance and choose which ones to change to
			en->"them" sp->"ellas"?
			*/
			// (LT-1428, LT-12472)
			// When the user edits a gloss,
			// (1) If there is an existing gloss matching what they just changed it to
			//		then switch this instance to point to that one.
			// (2) Else if the gloss is used only in this instance, or if it matches on all WSs that are not blank in the gloss,
			//		then apply the edits directly to the gloss.
			// (3) Else, create a new gloss.
			var gloss = fFoundAnalysis ? FindMatchingGloss() : null;
			if (gloss == null && m_sandbox.WordGlossReferenceCount == 1)
			{
				gloss = m_wg; // update the existing gloss.
			}
			if (gloss == null)
			{
				// Create one.
				var wgFactory = mainCache.ServiceLocator.GetInstance<IWfiGlossFactory>();
				gloss = wgFactory.Create();
				m_wa.MeaningsOC.Add(gloss);
			}
			foreach (var wsId in m_choices.EnabledWritingSystemsForFlid(InterlinLineChoices.kflidWordGloss))
			{
				var tssGloss = m_sda.get_MultiStringAlt(m_hvoSbWord, SandboxBase.ktagSbWordGloss, wsId);
				if (!tssGloss.Equals(gloss.Form.get_String(wsId)))
				{
					gloss.Form.set_String(wsId, tssGloss);
				}
			}
			return gloss;
		}

		private void CopyGlossesToSense(ILexSense sense)
		{
			foreach (var wsId in m_choices.EnabledWritingSystemsForFlid(InterlinLineChoices.kflidWordGloss))
			{
				UpdateMlaIfDifferent(m_hvoSbWord, SandboxBase.ktagSbWordGloss, wsId, sense.Hvo, LexSenseTags.kflidGloss);
			}
		}

		private void CopyGlossesToWfiGloss(IWfiGloss gloss)
		{
			foreach (var wsId in m_choices.EnabledWritingSystemsForFlid(InterlinLineChoices.kflidWordGloss))
			{
				UpdateMlaIfDifferent(m_hvoSbWord, SandboxBase.ktagSbWordGloss, wsId, gloss.Hvo, WfiGlossTags.kflidForm);
			}
		}

		private void UpdateSense(ILexSense sense, IPartOfSpeech pos)
		{
			CopyGlossesToSense(sense);
			var msa = (IMoStemMsa)sense.MorphoSyntaxAnalysisRA;
			var lexEntry = sense.Entry;
			if (msa.PartOfSpeechRA != pos)
			{
				// is there another MSA we can use?
				foreach (var msaOther in lexEntry.MorphoSyntaxAnalysesOC)
				{
					if (!(msaOther is IMoStemMsa stem))
					{
						continue;
					}
					if (stem.PartOfSpeechRA == pos)
					{
						sense.MorphoSyntaxAnalysisRA = msaOther; // also updates WfiMorphBundle and deletes old obsolete MSA if obsolete.
						return;
					}
				}
				// Is this msa used elsewhere or can we modify it?
				if (lexEntry.SensesOS.Where(s => s != sense && s.MorphoSyntaxAnalysisRA == msa).Take(1).Any())
				{
					// Used; have to make a new one.
					msa = sense.Services.GetInstance<IMoStemMsaFactory>().Create();
					lexEntry.MorphoSyntaxAnalysesOC.Add(msa);
					sense.MorphoSyntaxAnalysisRA = msa;
				}
				msa.PartOfSpeechRA = pos;
			}
		}

		// Answer true if the analysis is only used in one place (typically the current one).
		// It must have at most one WfiGloss and a net of one Segment that references it and its gloss if any.
		// That one segment must only reference it once.
		private static bool OnlyUsedThisOnce(IWfiAnalysis oldAnalysis)
		{
			if (oldAnalysis.MeaningsOC.Count > 1)
			{
				// It's technically possible there might only be one use, but which one would we update?
				return false;
			}
			// No need to enumerate more than two! This will speed it up for words used a lot.
			var segsUsingAnalysis = oldAnalysis.ReferringObjects.Where(obj => obj is ISegment).Take(2).ToList();
			if (segsUsingAnalysis.Count > 1)
			{
				return false;
			}
			var seg = segsUsingAnalysis.FirstOrDefault() as ISegment;
			IAnalysis target = oldAnalysis;
			if (oldAnalysis.MeaningsOC.Count == 1)
			{
				var wfiGloss = oldAnalysis.MeaningsOC.ToArray()[0];
				var segsUsingGloss = wfiGloss.ReferringObjects.Where(obj => obj is ISegment).Take(2).ToList();
				if (segsUsingAnalysis.Count + segsUsingGloss.Count > 1)
				{
					return false;
				}
				if (seg == null)
				{
					seg = segsUsingGloss.FirstOrDefault() as ISegment;
					target = wfiGloss;
				}
			}
			if (seg == null)
			{
				return true; // no uses at all...probably can't happen
			}
			return seg.AnalysesRS.Where(a => a == target).Take(2).Count() <= 1;
		}

		// Answer true if the sense is only used in one WfiAnalysis.
		private static bool OnlyUsedThisOnce(ILexSense sense)
		{
			return sense.ReferringObjects.Where(obj => obj is IWfiMorphBundle).Take(2).Count() <= 1;
		}

		/// <summary>
		/// Find the morph item referring to the LexEntry (only), not
		/// a sense or msa.
		/// </summary>
		private static MorphItem FindLexEntryMorphItem(List<MorphItem> morphItems, ILexEntry leTarget)
		{
			if (leTarget == null)
			{
				return new MorphItem(0, null);
			}
			foreach (var mi in morphItems)
			{
				if (mi.m_hvoSense != 0 || mi.m_hvoMorph == 0)
				{
					continue;
				}
				var entryCandidate = mi.GetPrimaryOrOwningEntry(leTarget.Cache);
				if (entryCandidate == leTarget)
				{
					return mi;
				}
			}
			return new MorphItem(0, null);
		}

		private bool SbWordPosMatchesSenseMsaPos(MorphItem morphItem)
		{
			var pos = m_caches.RealObject(m_sda.get_ObjectProp(m_hvoSbWord, SandboxBase.ktagSbWordPos)) as IPartOfSpeech;
			// currently only support MoStemMsa, since that is what a WordPos expects to match against.
			// (but -- see FWR-3475 part 2 -- the user can pathologically analyze a whole word as an affix
			// in which case we MIGHT see another kind here, so use the root repository and 'as'.
			if (morphItem.m_hvoMsa == 0)
			{
				return pos == null;
			}
			return m_caches.MainCache.ServiceLocator.GetInstance<IMoMorphSynAnalysisRepository>().GetObject(morphItem.m_hvoMsa) is IMoStemMsa msa && msa.PartOfSpeechRA == pos;
		}

		/// <summary>
		/// see if the MainPossibilities match for the given morphItem and
		/// the Word Part of Speech in the sandbox
		/// </summary>
		private bool SbWordMainPosMatchesSenseMsaMainPos(MorphItem morphItem)
		{
			var targetPos = m_caches.RealObject(m_sda.get_ObjectProp(m_hvoSbWord, SandboxBase.ktagSbWordPos)) as IPartOfSpeech;
			var hvoMainPosCandidate = 0;
			var hvoMainPosTarget = 0;
			// currently only support MoStemMsa, since that is what a WordPos expects to match against.
			// (but -- see FWR-3475 part 2 -- the user can pathologically analyze a whole word as an affix
			// in which case we MIGHT see another kind here, so use the root repository and 'as'.
			if (morphItem.m_hvoMsa != 0)
			{
				if (m_caches.MainCache.ServiceLocator.GetInstance<IMoMorphSynAnalysisRepository>().GetObject(morphItem.m_hvoMsa) is IMoStemMsa msa && msa.PartOfSpeechRA != null)
				{
					var posCandidate = msa.PartOfSpeechRA;
					var mainPosCandidate = posCandidate.MainPossibility;
					if (mainPosCandidate != null)
					{
						hvoMainPosCandidate = mainPosCandidate.Hvo;
					}
				}
			}
			var mainPosTarget = targetPos?.MainPossibility;
			if (mainPosTarget != null)
			{
				hvoMainPosTarget = mainPosTarget.Hvo;
			}
			return hvoMainPosCandidate == hvoMainPosTarget;
		}

		private bool SbWordGlossMatchesSenseGloss(MorphItem morphItem)
		{
			if (morphItem.m_hvoSense <= 0)
			{
				return false;
			}
			// compare our gloss information.
			return m_choices.EnabledWritingSystemsForFlid(InterlinLineChoices.kflidWordGloss).All(wsId => IsMlSame(m_hvoSbWord, SandboxBase.ktagSbWordGloss, wsId, morphItem.m_hvoSense, LexSenseTags.kflidGloss));
		}

		protected void BuildMorphLists()
		{
			// Build lists of morphs, msas, and senses, that we can use in subsequent code.
			m_analysisMorphs = new int[m_cmorphs];
			m_analysisMsas = new int[m_cmorphs];
			m_analysisSenses = new int[m_cmorphs];
			for (var imorph = m_cmorphs; --imorph >= 0;)
			{
				var hvoMorph = m_sda.get_VecItem(m_hvoSbWord, SandboxBase.ktagSbWordMorphs, imorph);
				var hvoMorphForm = m_sda.get_ObjectProp(hvoMorph, SandboxBase.ktagSbMorphForm);
				m_analysisMorphs[imorph] = m_caches.RealHvo(hvoMorphForm);
				m_analysisMsas[imorph] = m_caches.RealHvo(m_sda.get_ObjectProp(hvoMorph, SandboxBase.ktagSbMorphPos));
				m_analysisSenses[imorph] = m_caches.RealHvo(m_sda.get_ObjectProp(hvoMorph, SandboxBase.ktagSbMorphGloss));
			}
		}

		/// <summary>
		/// Ensure that the specified writing system of property flidDest in object hvoDst in m_sdaMain
		/// is the same as property flidSrc in object hvoSrc in m_sda. If not, update.
		/// </summary>
		private void UpdateMlaIfDifferent(int hvoSrc, int flidSrc, int wsId, int hvoDst, int flidDest)
		{
			if (IsMlSame(hvoSrc, flidSrc, wsId, hvoDst, flidDest, out var tss, out _))
			{
				return;
			}
			m_sdaMain.SetMultiStringAlt(hvoDst, flidDest, wsId, tss);
		}

		private bool IsMlSame(int hvoSrc, int flidSrc, int wsId, int hvoDst, int flidDest)
		{
			return IsMlSame(hvoSrc, flidSrc, wsId, hvoDst, flidDest, out _, out _);
		}
		private bool IsMlSame(int hvoSrc, int flidSrc, int wsId, int hvoDst, int flidDest, out ITsString tss, out ITsString tssOld)
		{
			tss = m_sda.get_MultiStringAlt(hvoSrc, flidSrc, wsId);
			tssOld = m_sdaMain.get_MultiStringAlt(hvoDst, flidDest, wsId);
			return tss.Equals(tssOld);
		}

		/// <summary>
		/// m_hvoAnalysis is the selected analysis. However, we did not consider writing systems
		/// of the morpheme line except the default vernacular one in deciding to use it.
		/// If additional WS information has been supplied, save it.
		/// </summary>
		private void EnsureCorrectMorphForms()
		{
			var otherWss = m_choices.OtherEnabledWritingSystemsForFlid(InterlinLineChoices.kflidMorphemes, 0);
			foreach (var wsId in otherWss)
			{
				for (var imorph = 0; imorph < m_cmorphs; imorph++)
				{
					var mb = m_wa.MorphBundlesOS[imorph];
					var hvoSbMorph = m_sda.get_VecItem(m_hvoSbWord, SandboxBase.ktagSbWordMorphs, imorph);
					var hvoSecMorph = m_sda.get_ObjectProp(hvoSbMorph, SandboxBase.ktagSbMorphForm);

					if (m_analysisMorphs[imorph] == 0)
					{
						// We have no morph, set it on the WfiMorphBundle.
						UpdateMlaIfDifferent(hvoSecMorph, SandboxBase.ktagSbNamedObjName, wsId, mb.Hvo, WfiMorphBundleTags.kflidForm);
					}
					else
					{
						// Set it on the MoForm.
						UpdateMlaIfDifferent(hvoSecMorph, SandboxBase.ktagSbNamedObjName, wsId, m_analysisMorphs[imorph], MoFormTags.kflidForm);
					}
				}
			}
		}

		/// <summary>
		/// Find one of the WfiGlosses of m_hvoWfiAnalysis where the form matches for each analysis writing system.
		/// Review: We probably want to find a WfiGloss that matches any (non-zero length) alternative since we don't want to
		/// force the user to type in every alternative each time they enter a gloss.
		/// Otherwise, if we match one with all non-zero length, return that one. (LT-1428)
		/// </summary>
		internal IWfiGloss FindMatchingGloss()
		{
			var wsIds = m_choices.EnabledWritingSystemsForFlid(InterlinLineChoices.kflidWordGloss);
			var sda = m_sda;
			var wfiAnalysis = m_wa;
			var hvoWord = m_hvoSbWord;
			return GetBestGloss(wfiAnalysis, wsIds, sda, hvoWord);
		}

		/// <summary>
		/// Get the most appropriate WfiGloss from among those belonging to the given WfiAnalysis.
		/// The goal is to match as closely as possible the strings found in sda.get_MultiStringAlt(hvoWord, ktagSbWordGloss, ws)
		/// for each ws in wsIds. Ideally we find an alternative where all writing systems match exactly.
		/// Failing that, one where as many as possible match, and the others are blank.
		/// </summary>
		internal static IWfiGloss GetBestGloss(IWfiAnalysis wfiAnalysis, List<int> wsIds, ISilDataAccess sda, int hvoWord)
		{
			IWfiGloss best = null;
			var cBestMatch = 0;
			var cBestBlanks = 0; // number of non-matching alternatives that are blank, in the best match so far
			foreach (var possibleGloss in wfiAnalysis.MeaningsOC)
			{
				var cMatch = 0;
				var cBlanks = 0; // number of non-matching alternatives that are blank, in the current item.
				foreach (var wsId in wsIds)
				{
					var tssGloss = sda.get_MultiStringAlt(hvoWord, SandboxBase.ktagSbWordGloss, wsId);
					var tssMainGloss = possibleGloss.Form.get_String(wsId);
					if (tssGloss.Equals(tssMainGloss))
					{
						cMatch++;
					}
					else if (tssMainGloss.Length == 0)
					{
						cBlanks++;
					}
					else
					{
						cMatch = int.MinValue; // non-matching alternative on the WfiGloss, this one is no good for sure
						cBlanks = int.MinValue;
					}
				}
				// This one is better if:
				// - it has more matches
				// - it has as many matches and more relevant empty alternatives
				// - it is as good otherwise and has fewer non-relevant alternatives.
				if (cMatch > cBestMatch || cMatch == cBestMatch && cBlanks > cBestBlanks || cMatch == cBestMatch && cBlanks == cBestBlanks && best != null
				    && AdditionalAlternatives(possibleGloss, wsIds) < AdditionalAlternatives(best, wsIds))
				{
					cBestMatch = cMatch;
					cBestBlanks = cBlanks;
					best = possibleGloss;
				}
			}
			// we won't return something where nothing matched, just because it had a blank alternative
			return cBestMatch > 0 ? best : null;
		}

		/// <summary>
		/// Count how many non-blank alternatives tss has that are not listed in wsIds.
		/// </summary>
		private static int AdditionalAlternatives(IWfiGloss wg, List<int> wsIds)
		{
			return wg.Form.AvailableWritingSystemIds.Where(ws => !wsIds.Contains(ws)).Count(ws => wg.Form.get_String(ws).Length > 0);
		}

		/// <summary>
		/// Find the analysis that matches the info in the secondary cache.
		/// </summary>
		internal IWfiAnalysis FindMatchingAnalysis(bool fExactMatch)
		{
			foreach (var possibleAnalysis in m_wf.AnalysesOC)
			{
				if (fExactMatch)
				{
					if (CheckAnalysis(possibleAnalysis.Hvo, true))
					{
						return possibleAnalysis;
					}
				}
				else
				{
					// If this possibility is Human evaluated, it must match exactly regardless
					// of the input parameter to count as a match on the analysis.
					var fIsHumanApproved = SandboxBase.IsAnalysisHumanApproved(m_caches.MainCache, possibleAnalysis);
					if (CheckAnalysis(possibleAnalysis.Hvo, fIsHumanApproved))
					{
						return possibleAnalysis;
					}
				}
			}
			if (fExactMatch)
			{
				return null;
			}
			// in this inexact case, another way to match is to have correct gloss(es) and trivial analysis.
			// Todo JohnT: do this and adjust caller.
			foreach (var possibleAnalysis in m_wf.AnalysesOC)
			{
				if (!IsTrivialAnalysis(possibleAnalysis))
				{
					continue;
				}
				foreach (var gloss in possibleAnalysis.MeaningsOC)
				{
					if (!MatchesCurrentGlosses(gloss))
					{
						continue;
					}
					// We want to reuse this gloss. If possible we will reuse and modify this
					// analysis. However, if it has other glosses, we don't want to change them.
					if (possibleAnalysis.MeaningsOC.Count == 1)
					{
						return possibleAnalysis;
					}
					var result = possibleAnalysis.Services.GetInstance<IWfiAnalysisFactory>().Create();
					m_wf.AnalysesOC.Add(result);
					result.MeaningsOC.Add(gloss); // moves it to its own new private analysis
					return result;
				}
			}

			return null; // no match found.
		}

		private bool MatchesCurrentGlosses(IWfiGloss gloss)
		{
			foreach (var wsId in m_choices.EnabledWritingSystemsForFlid(InterlinLineChoices.kflidWordGloss))
			{
				var tssGloss = m_sda.get_MultiStringAlt(m_hvoSbWord, SandboxBase.ktagSbWordGloss, wsId);
				if (!tssGloss.Equals(gloss.Form.get_String(wsId)))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// A trivial analysis, the sort created when just doing word glossing, doesn't really specify anything,
		/// though it may have one morph bundle that just matches the whole wordform.
		/// It may possibly specify a part of speech, in which case it must match the one the user entered
		/// to be reusable.
		/// </summary>
		private bool IsTrivialAnalysis(IWfiAnalysis possibleAnalysis)
		{
			if (possibleAnalysis.CategoryRA != null && possibleAnalysis.CategoryRA != m_caches.RealObject(m_sda.get_ObjectProp(m_hvoSbWord, SandboxBase.ktagSbWordPos)) as IPartOfSpeech)
			{
				return false;
			}
			if (possibleAnalysis.MorphBundlesOS.Count == 0)
			{
				return true;
			}
			if (possibleAnalysis.MorphBundlesOS.Count > 1)
			{
				return false;
			}
			var mb = possibleAnalysis.MorphBundlesOS[0];
			return mb.MorphRA == null && mb.MsaRA == null && mb.SenseRA == null;
		}

		/// <summary>
		/// Evaluate the given possible analysis to see whether it matches the current Sandbox data.
		/// Review: This is not testing word gloss at all. Is this right?
		/// </summary>
		private bool CheckAnalysis(int hvoPossibleAnalysis, bool fExactMatch)
		{
			// First, check that the analysis has the right word category.
			var hvoWordCat = m_sdaMain.get_ObjectProp(hvoPossibleAnalysis, WfiAnalysisTags.kflidCategory);
			var fCheck = fExactMatch || hvoWordCat != 0;
			if (fCheck && m_hvoCategoryReal != hvoWordCat)
			{
				return false;
			}
			// Next, it must at least have the right number of morphemes.
			var cmorphs = m_sdaMain.get_VecSize(hvoPossibleAnalysis, WfiAnalysisTags.kflidMorphBundles);
			if (cmorphs != m_cmorphs)
			{
				return false;
			}
			// Each morpheme must have the right data.
			for (var imorph = 0; imorph < cmorphs; imorph++)
			{
				var hvoMb = m_sdaMain.get_VecItem(hvoPossibleAnalysis, WfiAnalysisTags.kflidMorphBundles, imorph);
				// the morpheme must have the right MSA.
				var hvoMsa = m_sdaMain.get_ObjectProp(hvoMb, WfiMorphBundleTags.kflidMsa);
				if (hvoMsa != m_analysisMsas[imorph])
				{
					return false;
				}
				// and also the right sense
				var hvoSense = m_sdaMain.get_ObjectProp(hvoMb, WfiMorphBundleTags.kflidSense);
				fCheck = fExactMatch || hvoSense != 0;
				if (fCheck && hvoSense != m_analysisSenses[imorph])
				{
					return false;
				}
				// check InflType setting
				{
					var hvoSbInflType = GetRealHvoFromSbWmbInflType(imorph);
					var hvoRealInflType = m_sdaMain.get_ObjectProp(hvoMb, WfiMorphBundleTags.kflidInflType);
					if (hvoRealInflType != hvoSbInflType)
					{
						return false;
					}
				}

				// and finally the right form...either by pointing to a MoForm in its Morph property,
				// or by actually storing the string in its Form property.
				if (m_analysisMorphs[imorph] == 0)
				{
					// The form of the morph bundle must match the name of the dummy MoForm object.
					// No to this blocked line, since this gets a ts string with a null underlying string, as it is not in the cache with the '0' hvo.
					// This was the source of the multiple duplicate analyses in LT-837 "IText creating identical parses?"
					// ITsString tssForm = m_sda.get_StringProp(m_analysisMorphs[imorph], ktagSbNamedObjName);
					// The fix for LT-837 is to get the string for the real dummy hvo.
					var hvoMorph = m_sda.get_VecItem(m_hvoSbWord, SandboxBase.ktagSbWordMorphs, imorph);
					var tssForm = m_sandbox.GetFullMorphForm(hvoMorph);
					var tssMb = m_sdaMain.get_MultiStringAlt(hvoMb, WfiMorphBundleTags.kflidForm, m_sandbox.RawWordformWs);
					if (!tssForm.Equals(tssMb))
					{
						return false;
					}
					// Enhance JohnT: if we are showing other alternatives of the moform, must they match too?
					// My inclination is, not.
				}
				else
				{
					var hvoMorph = m_sdaMain.get_ObjectProp(hvoMb, WfiMorphBundleTags.kflidMorph);
					if (hvoMorph != m_analysisMorphs[imorph])
					{
						return false;
					}
				}
			}
			return true;
		}

		private int GetRealHvoFromSbWmbInflType(int imorph)
		{
			return m_caches.RealHvo(m_sda.get_ObjectProp(m_sda.get_VecItem(m_hvoSbWord, SandboxBase.ktagSbWordMorphs, imorph), SandboxBase.ktagSbNamedObjInflType));
		}
	}
}