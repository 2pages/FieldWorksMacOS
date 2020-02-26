// Copyright (c) 2017-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LanguageExplorer.Controls;
using LanguageExplorer.Controls.DetailControls;
using LanguageExplorer.DictionaryConfiguration;
using LanguageExplorer.LcmUi;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.RootSites;
using SIL.LCModel;
using SIL.LCModel.Core.Cellar;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Infrastructure;

namespace LanguageExplorer.Areas
{
	/// <summary>
	/// Area level services
	/// </summary>
	internal static class AreaServices
	{
		#region General area/tool
		internal const string AreaChoice = "areaChoice";
		internal const string ToolChoice = "toolChoice";
		internal const string ToolForAreaNamed_ = "ToolForAreaNamed_";
		internal const string InitialArea = "InitialArea";
		internal const string InitialAreaMachineName = LexiconAreaMachineName;
		#endregion General area/tool

		#region Lexicon area
		internal const string LexiconAreaMachineName = "lexicon";
			internal const string LexiconAreaUiName = "Lexical Tools";
		internal const string LexiconAreaDefaultToolMachineName = LexiconEditMachineName;
			internal const string LexiconEditMachineName = "lexiconEdit";
				internal const string LexiconEditUiName = "Lexicon Edit";
			internal const string LexiconBrowseMachineName = "lexiconBrowse";
				internal const string LexiconBrowseUiName = "Browse";
			internal const string LexiconDictionaryMachineName = "lexiconDictionary";
				internal const string LexiconDictionaryUiName = "HANGS (XhtmlDocView): Dictionary";
			internal const string RapidDataEntryMachineName = "rapidDataEntry";
				internal const string RapidDataEntryUiName = "Collect Words";
			internal const string LexiconClassifiedDictionaryMachineName = "lexiconClassifiedDictionary";
				internal const string LexiconClassifiedDictionaryUiName = "Classified Dictionary";
			internal const string BulkEditEntriesOrSensesMachineName = "bulkEditEntriesOrSenses";
				internal const string BulkEditEntriesOrSensesUiName = "CRASHES (5002 vs 5035): Bulk Edit Entries";
			internal const string ReversalEditCompleteMachineName = "reversalEditComplete";
				internal const string ReversalEditCompleteUiName = "HANGS (XhtmlDocView): Reversal Indexes";
			internal const string ReversalBulkEditReversalEntriesMachineName = "reversalBulkEditReversalEntries";
				internal const string ReversalBulkEditReversalEntriesUiName = "Bulk Edit Reversal Entries";
		#endregion Lexicon area

		#region Text and Words area
		internal const string TextAndWordsAreaMachineName = "textsWords";
			internal const string TextAndWordsAreaUiName = "Texts & Words";
		internal const string TextAndWordsAreaDefaultToolMachineName = InterlinearEditMachineName;
			internal const string InterlinearEditMachineName = "interlinearEdit";
				internal const string InterlinearEditUiName = "Interlinear Texts";
			internal const string ConcordanceMachineName = "concordance";
				internal const string ConcordanceUiName = "Concordance";
			internal const string ComplexConcordanceMachineName = "complexConcordance";
				internal const string ComplexConcordanceUiName = "Complex Concordance";
			internal const string WordListConcordanceMachineName = "wordListConcordance";
				internal const string WordListConcordanceUiName = "Word List Concordance";
			internal const string AnalysesMachineName = "Analyses";
				internal const string AnalysesUiName = "Word Analyses";
			internal const string BulkEditWordformsMachineName = "bulkEditWordforms";
				internal const string BulkEditWordformsUiName = "Bulk Edit Wordforms";
			internal const string CorpusStatisticsMachineName = "corpusStatistics";
				internal const string CorpusStatisticsUiName = "Statistics";
		#endregion Text and Words area

		#region Grammar area
		internal const string GrammarAreaMachineName = "grammar";
			internal const string GrammarAreaUiName = "Grammar";
		internal const string GrammarAreaDefaultToolMachineName = PosEditMachineName;
			internal const string PosEditMachineName = "posEdit";
				internal const string PosEditUiName = "Category Edit";
			internal const string CategoryBrowseMachineName = "categoryBrowse";
				internal const string CategoryBrowseUiName = "Categories Browse";
			internal const string CompoundRuleAdvancedEditMachineName = "compoundRuleAdvancedEdit";
				internal const string CompoundRuleAdvancedEditUiName = "Compound Rules";
			internal const string PhonemeEditMachineName = "phonemeEdit";
				internal const string PhonemeEditUiName = "Phonemes";
			internal const string PhonologicalFeaturesAdvancedEditMachineName = "phonologicalFeaturesAdvancedEdit";
				internal const string PhonologicalFeaturesAdvancedEditUiName = "Phonological Features";
			internal const string BulkEditPhonemesMachineName = "bulkEditPhonemes";
				internal const string BulkEditPhonemesUiName = "Bulk Edit Phoneme Features";
			internal const string NaturalClassEditMachineName = "naturalClassEdit";
				internal const string NaturalClassEditUiName = "Natural Classes";
			internal const string EnvironmentEditMachineName = "EnvironmentEdit";
				internal const string EnvironmentEditUiName = "Environments";
			internal const string PhonologicalRuleEditMachineName = "PhonologicalRuleEdit";
				internal const string PhonologicalRuleEditUiName = "Phonological Rules";
			internal const string AdhocCoprohibitionRuleEditMachineName = "AdhocCoprohibitionRuleEdit";
				internal const string AdhocCoprohibitionRuleEditUiName = "Ad hoc Rules";
			internal const string FeaturesAdvancedEditMachineName = "featuresAdvancedEdit";
				internal const string FeaturesAdvancedEditUiName = "Inflection Features";
			internal const string ProdRestrictEditMachineName = "ProdRestrictEdit";
				internal const string ProdRestrictEditUiName = "Exception \"Features\"";
			internal const string GrammarSketchMachineName = "grammarSketch";
				internal const string GrammarSketchUiName = "Grammar Sketch";
			internal const string LexiconProblemsMachineName = "lexiconProblems";
				internal const string LexiconProblemsUiName = "Problems";
		#endregion Grammar area

		#region Notebook area
		internal const string NotebookAreaMachineName = "notebook";
			internal const string NotebookAreaUiName = "Notebook";
		internal const string NotebookAreaDefaultToolMachineName = NotebookEditToolMachineName;
			internal const string NotebookEditToolMachineName = "notebookEdit";
				internal const string NotebookEditToolUiName = "Record Edit";
			internal const string NotebookBrowseToolMachineName = "notebookBrowse";
				internal const string NotebookBrowseToolUiName = "Browse";
			internal const string NotebookDocumentToolMachineName = "notebookDocument";
				internal const string NotebookDocumentToolUiName = "Document";
		#endregion Notebook area

		#region Lists area
		internal const string ListsAreaMachineName = "lists";
			internal const string ListsAreaUiName = "Lists";
		internal const string ListsAreaDefaultToolMachineName = DomainTypeEditMachineName;
			internal const string DomainTypeEditMachineName = "domainTypeEdit";
				internal const string DomainTypeEditUiName = "Academic Domains";
			internal const string AnthroEditMachineName = "anthroEdit";
				internal const string AnthroEditUiName = "Anthropology Categories";
			internal const string ComplexEntryTypeEditMachineName = "complexEntryTypeEdit";
				internal const string ComplexEntryTypeEditUiName = "Complex Form Types";
			internal const string ConfidenceEditMachineName = "confidenceEdit";
				internal const string ConfidenceEditUiName = "Confidence Levels";
			internal const string DialectsListEditMachineName = "dialectsListEdit";
				internal const string DialectsListEditUiName = "Dialect Labels";
			internal const string ChartmarkEditMachineName = "chartmarkEdit";
				internal const string ChartmarkEditUiName = "Text Chart Markers";
			internal const string CharttempEditMachineName = "charttempEdit";
				internal const string CharttempEditUiName = "Text Constituent Chart Templates";
			internal const string EducationEditMachineName = "educationEdit";
				internal const string EducationEditUiName = "Education Levels";
			internal const string RoleEditMachineName = "roleEdit";
				internal const string RoleEditUiName = "Roles";
			internal const string ExtNoteTypeEditMachineName = "extNoteTypeEdit";
				internal const string ExtNoteTypeEditUiName = "Extended Note Types";
			internal const string FeatureTypesAdvancedEditMachineName = "featureTypesAdvancedEdit";
				internal const string FeatureTypesAdvancedEditUiName = "Feature Types";
			internal const string GenresEditMachineName = "genresEdit";
				internal const string GenresEditUiName = "Genres";
			internal const string LanguagesListEditMachineName = "languagesListEdit";
				internal const string LanguagesListEditUiName = "Languages";
			internal const string LexRefEditMachineName = "lexRefEdit";
				internal const string LexRefEditUiName = "Lexical Relations";
			internal const string LocationsEditMachineName = "locationsEdit";
				internal const string LocationsEditUiName = "Locations";
			internal const string PublicationsEditMachineName = "publicationsEdit";
				internal const string PublicationsEditUiName = "Publications";
			internal const string MorphTypeEditMachineName = "morphTypeEdit";
				internal const string MorphTypeEditUiName = "Morpheme Types";
			internal const string PeopleEditMachineName = "peopleEdit";
				internal const string PeopleEditUiName = "People";
			internal const string PositionsEditMachineName = "positionsEdit";
				internal const string PositionsEditUiName = "Positions";
			internal const string RestrictionsEditMachineName = "restrictionsEdit";
				internal const string RestrictionsEditUiName = "Restrictions";
			internal const string SemanticDomainEditMachineName = "semanticDomainEdit";
				internal const string SemanticDomainEditUiName = "Semantic Domains";
			internal const string SenseTypeEditMachineName = "senseTypeEdit";
				internal const string SenseTypeEditUiName = "Sense Types";
			internal const string StatusEditMachineName = "statusEdit";
				internal const string StatusEditUiName = "Status";
			internal const string TextMarkupTagsEditMachineName = "textMarkupTagsEdit";
				internal const string TextMarkupTagsEditUiName = "Text Markup Tags";
			internal const string TranslationTypeEditMachineName = "translationTypeEdit";
				internal const string TranslationTypeEditUiName = "Translation Types";
			internal const string UsageTypeEditMachineName = "usageTypeEdit";
				internal const string UsageTypeEditUiName = "Usages";
			internal const string VariantEntryTypeEditMachineName = "variantEntryTypeEdit";
				internal const string VariantEntryTypeEditUiName = "Variant Types";
			internal const string RecTypeEditMachineName = "recTypeEdit";
				internal const string RecTypeEditUiName = "Notebook Record Types";
			internal const string TimeOfDayEditMachineName = "timeOfDayEdit";
				internal const string TimeOfDayEditUiName = "Time Of Day";
			internal const string ReversalToolReversalIndexPOSMachineName = "reversalToolReversalIndexPOS";
				internal const string ReversalToolReversalIndexPOSUiName = "Reversal Index Categories";
		#endregion Lists area

		#region LanguageExplorer.DictionaryConfiguration.ImageHolder smallCommandImages image constants
		internal const int MoveUpIndex = 12;
		internal const int MoveRightIndex = 13;
		internal const int MoveDownIndex = 14;
		internal const int MoveLeftIndex = 15;
		#endregion LanguageExplorer.DictionaryConfiguration.ImageHolder smallCommandImages image constants

		#region Random strings
		internal const string Default = "Default";
		internal const string ShortName = "ShortName";
		internal const string PartOfSpeechGramInfo = "PartOfSpeechGramInfo";
		internal const string WordPartOfSpeech = "WordPartOfSpeech";
		internal const string OwningField = "field";
		internal const string ClassName = "className";
		internal const string OwnerClassName = "ownerClassName";
		internal const string BaseUowMessage = "baseUowMessage";
		internal const string LeftPanelMenuId = "left";
		internal const string RightPanelMenuId = "right";
		internal const string MoveUp = "MoveUp";
		internal const string MoveDown = "MoveDown";
		internal const string Promote = "Promote";
		internal const string List_Item = "List Item";
		internal const string Subitem = "Subitem";
		internal const string InterestingTexts = "InterestingTexts";
		internal const string RecordListOwningObjChanged = "RecordListOwningObjChanged";
		internal const string EntriesOrChildren = "entriesOrChildren";
		#endregion Random strings

		/// <summary>
		/// Handle the provided import dialog.
		/// </summary>
		internal static void HandleDlg(Form importDlg, LcmCache cache, IFlexApp flexApp, IFwMainWnd mainWindow, IPropertyTable propertyTable, IPublisher publisher)
		{
			var oldWsUser = cache.WritingSystemFactory.UserWs;
			((IFwExtension)importDlg).Init(cache, propertyTable, publisher);
			if (importDlg.ShowDialog((Form)mainWindow) != DialogResult.OK)
			{
				return;
			}
			switch (importDlg)
			{
				// NB: Some clients are not any of the types that are checked below, which is fine. That means nothing else is done here.
				case IFormReplacementNeeded _ when oldWsUser != cache.WritingSystemFactory.UserWs:
					flexApp.ReplaceMainWindow(mainWindow);
					break;
				case IImportForm _:
					// Make everything we've imported visible.
					mainWindow.RefreshAllViews();
					break;
			}
		}

		public static bool UpdateCachedObjects(LcmCache cache, FieldDescription fd)
		{
			// We need to find every instance of a reference from this flid to that custom list and delete it!
			// I can't figure out any other way of ensuring that EnsureCompleteIncomingRefs doesn't try to refer
			// to a non-existent flid at some point.
			var owningListGuid = fd.ListRootId;
			if (owningListGuid == Guid.Empty)
			{
				return false;
			}
			var list = cache.ServiceLocator.GetInstance<ICmPossibilityListRepository>().GetObject(owningListGuid);
			// This is only a problem for fields referencing a custom list
			if (list.Owner != null)
			{
				// Not a custom list.
				return false;
			}
			bool changed;
			var type = fd.Type;
			var objRepo = cache.ServiceLocator.GetInstance<ICmObjectRepository>();
			var objClass = fd.Class;
			var flid = fd.Id;
			var ddbf = cache.DomainDataByFlid;
			switch (type)
			{
				case CellarPropertyType.ReferenceSequence: // drop through
				case CellarPropertyType.ReferenceCollection:
					// Handle multiple reference fields
					// Is there a way to do this in LINQ without repeating the get_VecSize call?
					var tupleList = new List<Tuple<int, int>>();
					tupleList.AddRange(objRepo.AllInstances(objClass).Where(obj => ddbf.get_VecSize(obj.Hvo, flid) > 0)
						.Select(obj => new Tuple<int, int>(obj.Hvo, ddbf.get_VecSize(obj.Hvo, flid))));
					NonUndoableUnitOfWorkHelper.Do(cache.ActionHandlerAccessor, () =>
					{
						foreach (var partResult in tupleList)
						{
							ddbf.Replace(partResult.Item1, flid, 0, partResult.Item2, null, 0);
						}
					});
					changed = tupleList.Any();
					break;
				case CellarPropertyType.ReferenceAtomic:
					// Handle atomic reference fields
					// If there's a value for (Hvo, flid), nullify it!
					var objsWithDataThisFlid = new List<int>();
					objsWithDataThisFlid.AddRange(objRepo.AllInstances(objClass).Where(obj => ddbf.get_ObjectProp(obj.Hvo, flid) > 0).Select(obj => obj.Hvo));
					// Delete these references
					NonUndoableUnitOfWorkHelper.Do(cache.ActionHandlerAccessor, () =>
					{
						foreach (var hvo in objsWithDataThisFlid)
						{
							ddbf.SetObjProp(hvo, flid, LcmCache.kNullHvo);
						}
					});
					changed = objsWithDataThisFlid.Any();
					break;
				default:
					changed = false;
					break;
			}
			return changed;
		}

		/// <summary>
		/// Tell the user why we aren't jumping to his record
		/// </summary>
		internal static void GiveSimpleWarning(Form form, string helpFile, ExclusionReasonCode xrc)
		{
			string caption;
			string reason;
			string shlpTopic;
			switch (xrc)
			{
				case ExclusionReasonCode.NotInPublication:
					caption = AreaResources.ksEntryNotPublished;
					reason = AreaResources.ksEntryNotPublishedReason;
					shlpTopic = "User_Interface/Menus/Edit/Find_a_lexical_entry.htm";
					break;
				case ExclusionReasonCode.ExcludedHeadword:
					caption = AreaResources.ksMainNotShown;
					reason = AreaResources.ksMainNotShownReason;
					shlpTopic = "khtpMainEntryNotShown";
					break;
				case ExclusionReasonCode.ExcludedMinorEntry:
					caption = AreaResources.ksMinorNotShown;
					reason = AreaResources.ksMinorNotShownReason;
					shlpTopic = "khtpMinorEntryNotShown";
					break;
				default:
					throw new ArgumentException("Unknown ExclusionReasonCode");
			}
			// TODO-Linux: Help is not implemented on Mono
			MessageBox.Show(form, string.Format(AreaResources.ksSelectedEntryNotInDict, reason), caption, MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, 0, helpFile, HelpNavigator.Topic, shlpTopic);
		}

		internal static void ResetMainPossibilityInsertUiWidgetsText(UiWidgetController uiWidgetController, string newText, string newToolTipText = null)
		{
			ResetInsertUiWidgetsText(uiWidgetController.InsertMenuDictionary[Command.CmdInsertPossibility], newText,
				uiWidgetController.InsertToolBarDictionary[Command.CmdInsertPossibility], String.IsNullOrWhiteSpace(newToolTipText) ? newText : newToolTipText);
		}

		internal static void ResetSubitemPossibilityInsertUiWidgetsText(UiWidgetController uiWidgetController, string newText, string newToolTipText = null)
		{
			ResetInsertUiWidgetsText(uiWidgetController.InsertMenuDictionary[Command.CmdDataTree_Insert_Possibility], newText,
				uiWidgetController.InsertToolBarDictionary[Command.CmdDataTree_Insert_Possibility], String.IsNullOrWhiteSpace(newToolTipText) ? newText : newToolTipText);
		}

		private static void ResetInsertUiWidgetsText(ToolStripItem menu, string newText, ToolStripItem toolBarButton, string newToolTipText)
		{
			menu.Text = newText;
			toolBarButton.ToolTipText = newToolTipText;
		}

		/// <summary>
		/// See if a menu is visible/enabled that moves items down in an owning property.
		/// </summary>
		internal static bool CanMoveDownObjectInOwningSequence(DataTree dataTree, LcmCache cache, out bool visible)
		{
			visible = false;
			bool enabled;
			var type = CellarPropertyType.ReferenceAtomic;
			var sliceObject = dataTree.CurrentSlice.MyCmObject;
			var owningFlid = sliceObject.OwningFlid;
			if (owningFlid > 0)
			{
				type = (CellarPropertyType)cache.DomainDataByFlid.MetaDataCache.GetFieldType(owningFlid);
			}
			if (type != CellarPropertyType.OwningSequence && type != CellarPropertyType.ReferenceSequence)
			{
				visible = false;
				return false;
			}
			var owningObject = sliceObject.Owner;
			var chvo = cache.DomainDataByFlid.get_VecSize(owningObject.Hvo, owningFlid);
			if (chvo < 2)
			{
				enabled = false;
			}
			else
			{
				var hvo = cache.DomainDataByFlid.get_VecItem(owningObject.Hvo, owningFlid, 0);
				enabled = sliceObject.Hvo != hvo;
				// if the first LexEntryRef in LexEntry.EntryRefs is a complex form, and the
				// slice displays the second LexEntryRef in the sequence, then we can't move it
				// up, since the first slot is reserved for the complex form.
				if (enabled && owningFlid == LexEntryTags.kflidEntryRefs && cache.DomainDataByFlid.get_VecSize(hvo, LexEntryRefTags.kflidComplexEntryTypes) > 0)
				{
					enabled = sliceObject.Hvo != cache.DomainDataByFlid.get_VecItem(owningObject.Hvo, owningFlid, 1);
				}
				else
				{
					var sliceObjIdx = cache.DomainDataByFlid.GetObjIndex(owningObject.Hvo, owningFlid, sliceObject.Hvo);
					enabled = sliceObjIdx < chvo - 1;
				}
			}
			visible = true;
			return enabled;
		}

		/// <summary>
		/// See if a menu is visible/enabled that moves items up in an owning property.
		/// </summary>
		internal static bool CanMoveUpObjectInOwningSequence(DataTree dataTree, LcmCache cache, out bool visible)
		{
			visible = false;
			bool enabled;
			var type = CellarPropertyType.ReferenceAtomic;
			var sliceObject = dataTree.CurrentSlice.MyCmObject;
			var owningFlid = sliceObject.OwningFlid;
			if (owningFlid > 0)
			{
				type = (CellarPropertyType)cache.DomainDataByFlid.MetaDataCache.GetFieldType(owningFlid);
			}
			if (type != CellarPropertyType.OwningSequence && type != CellarPropertyType.ReferenceSequence)
			{
				return false;
			}
			var owningObject = sliceObject.Owner;
			var chvo = cache.DomainDataByFlid.get_VecSize(owningObject.Hvo, owningFlid);
			if (chvo < 2)
			{
				enabled = false;
			}
			else
			{
				var hvo = cache.DomainDataByFlid.get_VecItem(owningObject.Hvo, owningFlid, 0);
				enabled = sliceObject.Hvo != hvo;
				if (enabled && owningFlid == LexEntryTags.kflidEntryRefs && cache.DomainDataByFlid.get_VecSize(hvo, LexEntryRefTags.kflidComplexEntryTypes) > 0)
				{
					// if the first LexEntryRef in LexEntry.EntryRefs is a complex form, and the
					// slice displays the second LexEntryRef in the sequence, then we can't move it
					// up, since the first slot is reserved for the complex form.
					enabled = sliceObject.Hvo != cache.DomainDataByFlid.get_VecItem(owningObject.Hvo, owningFlid, 1);
				}
				else
				{
					var sliceObjIdx = cache.DomainDataByFlid.GetObjIndex(owningObject.Hvo, owningFlid, sliceObject.Hvo);
					enabled = sliceObjIdx > 0;
				}
			}
			visible = true;

			return enabled;
		}

		internal static void CreateDeleteMenuItem(List<Tuple<ToolStripMenuItem, EventHandler>> menuItems, ContextMenuStrip contextMenuStrip, Slice slice, string menuText, EventHandler deleteEventHandler)
		{
			var menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, deleteEventHandler, menuText, image: LanguageExplorerResources.Delete);
			menu.Enabled = !slice.IsGhostSlice && slice.CanDeleteNow;
			if (!menu.Enabled)
			{
				menu.Text = $"{menuText} {StringTable.Table.GetString("(cannot delete this)")}";
			}
			menu.ImageTransparentColor = Color.Magenta;
			menu.Tag = slice;
		}

		internal static Dictionary<string, string> PopulateForMainItemInsert(ICmPossibilityList owningList, ICmPossibility currentPossibility, string baseUowMessage)
		{
			Guard.AgainstNull(owningList, nameof(owningList));
			// The list may be empty, so 'currentPossibility' may be null.
			var mdc = owningList.Cache.GetManagedMetaDataCache();
			var owningPossibility = currentPossibility?.OwningPossibility;
			string className;
			string ownerClassName;
			if (owningPossibility == null)
			{
				className = owningList.ClassName;
				ownerClassName = mdc.GetFieldName(CmPossibilityListTags.kflidPossibilities);
			}
			else
			{
				className = owningPossibility.ClassName;
				ownerClassName = mdc.GetFieldName(CmPossibilityTags.kflidSubPossibilities);
			}
			// Top level newbies are of the class specified in the list,
			// even for lists that allow for certain newbies to be of some other class, such as the variant entry ref type list.
			return CreateSharedInsertDictionary(mdc.GetClassName(owningList.ItemClsid), className, ownerClassName, baseUowMessage);
		}

		internal static Dictionary<string, string> PopulateForSubitemInsert(ICmPossibilityList owningList, ICmPossibility owningPossibility, string baseUowMessage)
		{
			// There has to be a list that ultimately owns a possibility.
			Guard.AgainstNull(owningList, nameof(owningList));

			var mdc = owningList.Cache.GetManagedMetaDataCache();
			var className = owningPossibility == null ? mdc.GetClassName(owningList.ItemClsid) : owningPossibility.ClassName;
			var ownerClassName = className;
			return CreateSharedInsertDictionary(className, ownerClassName, mdc.GetFieldName(CmPossibilityTags.kflidSubPossibilities), baseUowMessage);
		}

		private static Dictionary<string, string> CreateSharedInsertDictionary(string className, string ownerClassName, string owningFieldName, string baseUowMessage)
		{
			return new Dictionary<string, string>
			{
				{ ClassName, className },
				{ OwnerClassName, ownerClassName },
				{ OwningField, owningFieldName },
				{ BaseUowMessage, baseUowMessage }
			};
		}

		internal static void LexiconLookup(LcmCache cache, FlexComponentParameters flexComponentParameters, StTextSlice textSlice)
		{
			LexiconLookup(cache, flexComponentParameters, textSlice.RootSite);
		}

		internal static void LexiconLookup(LcmCache cache, FlexComponentParameters flexComponentParameters, RootSite rootSite)
		{
			rootSite.RootBox.Selection.GetWordLimitsOfSelection(out var ichMin, out var ichLim, out var hvo, out var tag, out var ws, out _);
			if (ichLim > ichMin)
			{
				LexEntryUi.DisplayOrCreateEntry(cache, hvo, tag, ws, ichMin, ichLim, flexComponentParameters.PropertyTable.GetValue<IWin32Window>(FwUtils.window), flexComponentParameters, flexComponentParameters.PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider), "UserHelpFile");
			}
		}

		internal static void AddToLexicon(LcmCache lcmCache, FlexComponentParameters flexComponentParameters, StTextSlice currentSliceAsStTextSlice)
		{
			currentSliceAsStTextSlice.RootSite.RootBox.Selection.GetWordLimitsOfSelection(out var ichMin, out var ichLim, out _, out _, out var ws, out var tss);
			if (ws == 0)
			{
				ws = tss.GetWsFromString(ichMin, ichLim);
			}
			if (ichLim <= ichMin || ws != lcmCache.DefaultVernWs)
			{
				return;
			}
			var tsb = tss.GetBldr();
			if (ichLim < tsb.Length)
			{
				tsb.Replace(ichLim, tsb.Length, null, null);
			}

			if (ichMin > 0)
			{
				tsb.Replace(0, ichMin, null, null);
			}
			var tssForm = tsb.GetString();
			using (var dlg = new InsertEntryDlg())
			{
				dlg.InitializeFlexComponent(flexComponentParameters);
				dlg.SetDlgInfo(lcmCache, tssForm);
				if (dlg.ShowDialog(flexComponentParameters.PropertyTable.GetValue<Form>(FwUtils.window)) == DialogResult.OK)
				{
					// is there anything special we want to do, such as jump to the new entry?
				}
			}
		}

		internal static bool CanJumpToTool(string currentToolMachineName, string targetToolMachineNameForJump, LcmCache cache, ICmObject rootObject, ICmObject currentObject, string className)
		{
			if (currentToolMachineName == targetToolMachineNameForJump)
			{
				return (ReferenceEquals(rootObject, currentObject) || currentObject.IsOwnedBy(rootObject));
			}
			if (currentObject is IWfiWordform)
			{
				var concordanceTools = new HashSet<string>
				{
					WordListConcordanceMachineName,
					ConcordanceMachineName
				};
				return concordanceTools.Contains(targetToolMachineNameForJump);
			}
			// Do it the hard way.
			var specifiedClsid = 0;
			var mdc = cache.GetManagedMetaDataCache();
			if (mdc.ClassExists(className)) // otherwise is is a 'magic' class name treated specially in other OnDisplays.
			{
				specifiedClsid = mdc.GetClassId(className);
			}
			if (specifiedClsid == 0)
			{
				// Not visible or enabled.
				return false; // a special magic class id, only enabled explicitly.
			}
			if (currentObject.ClassID == specifiedClsid)
			{
				// Visible & enabled.
				return true;
			}
			// Visible & enabled are the same at this point.
			return cache.DomainDataByFlid.MetaDataCache.GetBaseClsId(currentObject.ClassID) == specifiedClsid;
		}

		/// <summary>
		/// It will add the menu (and optional separator) only if the menu will be both visible and enabled.
		/// </summary>
		internal static void ConditionallyAddJumpToToolMenuItem(ContextMenuStrip contextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>> menuItems, LcmCache cache, IPublisher publisher,
			ICmObject rootObject, ICmObject selectedObject, EventHandler eventHandler, string currentToolMachineName, string targetToolName, ref bool wantSeparator, string className,
			string menuLabel, int separatorInsertLocation = 0)
		{
			var visibleAndEnabled = CanJumpToTool(currentToolMachineName, targetToolName, cache, rootObject, selectedObject, className);
			if (visibleAndEnabled)
			{
				if (wantSeparator)
				{
					ToolStripMenuItemFactory.CreateToolStripSeparatorForContextMenuStrip(contextMenuStrip, separatorInsertLocation);
					wantSeparator = false;
				}
				var menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, eventHandler, menuLabel);
				menu.Tag = new List<object> { publisher, targetToolName, selectedObject };
			}
		}

		internal static void MoveUpObjectInOwningSequence(LcmCache cache, Slice slice)
		{
			var owningObject = slice.MyCmObject.Owner;
			var owningFlid = slice.MyCmObject.OwningFlid;
			var indexInOwningProperty = cache.DomainDataByFlid.GetObjIndex(owningObject.Hvo, owningFlid, slice.MyCmObject.Hvo);
			if (indexInOwningProperty > 0)
			{
				// The slice might be invalidated by the MoveOwningSequence, so we get its
				// values first.  See LT-6670.
				// We found it in the sequence, and it isn't already the first.
				UndoableUnitOfWorkHelper.Do(AreaResources.UndoMoveItem, AreaResources.RedoMoveItem, cache.ActionHandlerAccessor,
					() => cache.DomainDataByFlid.MoveOwnSeq(owningObject.Hvo, (int)owningFlid, indexInOwningProperty, indexInOwningProperty, owningObject.Hvo, owningFlid, indexInOwningProperty - 1));
			}
		}

		internal static void MoveDownObjectInOwningSequence(LcmCache cache, Slice slice)
		{
			var owningObject = slice.MyCmObject.Owner;
			var owningFlid = slice.MyCmObject.OwningFlid;
			var count = cache.DomainDataByFlid.get_VecSize(owningObject.Hvo, owningFlid);
			var indexInOwningProperty = cache.DomainDataByFlid.GetObjIndex(owningObject.Hvo, owningFlid, slice.MyCmObject.Hvo);
			if (indexInOwningProperty >= 0 && indexInOwningProperty + 1 < count)
			{
				// The slice might be invalidated by the MoveOwningSequence, so we get its
				// values first.  See LT-6670.
				// We found it in the sequence, and it isn't already the last.
				// Quoting from VwOleDbDa.cpp, "Insert the selected records before the
				// DstStart object".  This means we need + 2 instead of + 1 for the
				// new location.
				UndoableUnitOfWorkHelper.Do(AreaResources.UndoMoveItem, AreaResources.RedoMoveItem, cache.ActionHandlerAccessor,
					() => cache.DomainDataByFlid.MoveOwnSeq(owningObject.Hvo, owningFlid, indexInOwningProperty, indexInOwningProperty, owningObject.Hvo, owningFlid, indexInOwningProperty + 2));
			}
		}

		internal static string GetMergeMenuText(bool enabled, string baseText)
		{
			return enabled ? baseText : $"{baseText} {StringTable.Table.GetString("(cannot merge this)")}";
		}
	}
}