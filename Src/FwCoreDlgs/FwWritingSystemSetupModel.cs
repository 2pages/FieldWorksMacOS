// Copyright (c) 2019-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Icu;
using SIL.Code;
using SIL.Extensions;
using SIL.FieldWorks.Common.FwUtils;
using SIL.Keyboarding;
using SIL.LCModel;
using SIL.LCModel.Core.SpellChecking;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;
using SIL.Lexicon;
using SIL.Windows.Forms.WritingSystems;
using SIL.WritingSystems;
using SilEncConverters40;

namespace SIL.FieldWorks.FwCoreDlgs
{
	/// <summary>
	/// Presentation model for the WritingSystemConfiguration dialog. Handles one list worth of writing systems.
	/// e.g. Analysis, or Vernacular
	/// </summary>
	internal sealed class FwWritingSystemSetupModel
	{
		/// <summary/>
		internal enum ListType
		{
			/// <summary/>
			Vernacular,
			/// <summary/>
			Analysis,
			/// <summary/>
			Pronunciation
		}

		/// <summary/>
		internal List<WSListItemModel> WorkingList
		{
			get;
			set;
		}
		private CoreWritingSystemDefinition _currentWs;
		private readonly ListType _listType;
		private readonly IWritingSystemManager _wsManager;
		private string _languageName;
		private WritingSystemSetupModel _currentWsSetupModel;
		private readonly Dictionary<CoreWritingSystemDefinition, CoreWritingSystemDefinition> _mergedWritingSystems = new Dictionary<CoreWritingSystemDefinition, CoreWritingSystemDefinition>();

		// function for retrieving Encoding converter keys, internal to allow mock results in unit tests
		internal Func<ICollection> EncodingConverterKeys = () =>
		{
			try
			{
				var encConverters = new EncConverters();
				return encConverters.Keys;
			}
			catch (Exception)
			{
				// If we can't use encoding converters don't crash, just return an empty list
				return new string[] { };
			}
		};

		/// <summary/>
		internal readonly LcmCache Cache;

		private readonly IPublisher _publisher;

		/// <summary/>
		internal event EventHandler WritingSystemListUpdated;

		/// <summary/>
		internal delegate void ShowMessageBoxDelegate(string message);

		/// <summary/>
		internal delegate bool ChangeLanguageDelegate(out LanguageInfo info);

		/// <summary/>
		internal delegate void ValidCharacterDelegate();

		/// <summary/>
		internal delegate bool ModifyConvertersDelegate(string originalConverter, out string selectedConverter);

		/// <summary/>
		internal delegate bool ConfirmDeleteWritingSystemDelegate(string wsDisplayLabel);

		/// <summary/>
		internal delegate bool ConfirmMergeWritingSystemDelegate(string wsToMerge, out CoreWritingSystemDefinition wsTag);

		/// <summary/>
		internal delegate void ImportTranslatedListDelegate(string icuLocaleToImport);

		/// <summary/>
		internal delegate bool SharedWsChangeDelegate(string originalLanguage);

		/// <summary/>
		internal delegate bool AddNewVernacularLanguageDelegate();

		/// <summary/>
		internal delegate bool ChangeHomographWs(string newHomographWs);

		/// <summary/>
		internal delegate bool ConfirmClearAdvancedDelegate();

		/// <summary/>
		internal ShowMessageBoxDelegate ShowMessageBox;

		/// <summary/>
		internal ChangeLanguageDelegate ShowChangeLanguage;

		/// <summary/>
		internal ValidCharacterDelegate ShowValidCharsEditor;

		/// <summary/>
		internal ModifyConvertersDelegate ShowModifyEncodingConverters;

		/// <summary/>
		internal ConfirmDeleteWritingSystemDelegate ConfirmDeleteWritingSystem;

		/// <summary/>
		internal SharedWsChangeDelegate AcceptSharedWsChangeWarning;

		/// <summary/>
		internal AddNewVernacularLanguageDelegate AddNewVernacularLanguageWarning;

		/// <summary/>
		internal ChangeHomographWs ShouldChangeHomographWs;

		/// <summary/>
		internal ImportTranslatedListDelegate ImportListForNewWs;

		/// <summary/>
		internal ConfirmMergeWritingSystemDelegate ConfirmMergeWritingSystem;

		/// <summary/>
		internal ConfirmClearAdvancedDelegate ConfirmClearAdvanced;

		private IWritingSystemContainer _wsContainer;
		private ProjectLexiconSettingsDataMapper _projectLexiconSettingsDataMapper;
		private ProjectLexiconSettings _projectLexiconSettings;
		// We need to know if the homographWs was equal to the top vernacular writing system on construction
		// to be able to show warnings triggered by changing this.
		private bool _homographWsWasTopVern;
		// We need to know if the homographWs was in the current list on construction
		// to be able to show warnings triggered by removing it from the current list
		private bool _homographWsWasInCurrent;
		// backing variable for when the user checks the box on something that doesn't require advanced view yet
		private bool _showAdvancedView;

		/// <summary>
		/// event raised when the writing system has been changed by the presenter
		/// </summary>
		internal event EventHandler OnCurrentWritingSystemChanged = delegate { };

		/// <summary/>
		internal FwWritingSystemSetupModel(IWritingSystemContainer container, ListType type, IWritingSystemManager wsManager = null, LcmCache cache = null, IPublisher publisher = null)
		{
			switch (type)
			{
				case ListType.Analysis:
					WorkingList = BuildWorkingList(container.AnalysisWritingSystems, container.CurrentAnalysisWritingSystems);
					break;
				case ListType.Vernacular:
					WorkingList = BuildWorkingList(container.VernacularWritingSystems, container.CurrentVernacularWritingSystems);
					break;
				case ListType.Pronunciation:
					throw new NotImplementedException();
			}
			_currentWs = WorkingList.First().WorkingWs;
			_listType = type;
			_wsManager = wsManager;
			SetCurrentWsSetupModel(_currentWs);
			Cache = cache;
			_publisher = publisher;
			_wsContainer = container;
			_projectLexiconSettings = new ProjectLexiconSettings();
			// ignore on disk settings if we are testing without a cache
			if (Cache != null)
			{
				_projectLexiconSettingsDataMapper = new ProjectLexiconSettingsDataMapper(Cache?.ServiceLocator.DataSetup.ProjectSettingsStore);
				_projectLexiconSettingsDataMapper.Read(_projectLexiconSettings);
				_homographWsWasTopVern = WorkingList.First(ws => ws.InCurrentList).OriginalWs.Id == Cache.LangProject.HomographWs;
				// guard against homograph ws not being in the project for paranoia's sake
				var homographWs = WorkingList.FirstOrDefault(ws => ws.OriginalWs.Id == Cache.LangProject.HomographWs);
				_homographWsWasInCurrent = homographWs?.InCurrentList ?? false;
			}
		}

		private static List<WSListItemModel> BuildWorkingList(ICollection<CoreWritingSystemDefinition> allForType, IList<CoreWritingSystemDefinition> currentForType)
		{
			// We prefer to display the current ones first, because that's what the code that orders them
			// in the lexical data fields does. Usually this happens automatically, but there have
			// been cases where the order is different in the two lists, even if they have the same items.
			return currentForType.Concat(allForType.Except(currentForType))
				.Select(ws => new WSListItemModel(currentForType.Contains(ws), ws, new CoreWritingSystemDefinition(ws, true))).ToList();
		}

		/// <summary/>
		internal WritingSystemSetupModel CurrentWsSetupModel
		{
			get => _currentWsSetupModel;
			private set
			{
				_currentWsSetupModel = value;
				_languageName = _currentWsSetupModel.CurrentLanguageName;
			}
		}

		private void SetCurrentWsSetupModel(WritingSystemDefinition ws)
		{
			CurrentWsSetupModel = new WritingSystemSetupModel(ws);
			if (IsTheOriginalPlainEnglish())
			{
				CurrentWsSetupModel.CurrentItemUpdated += PreventChangingPlainEnglish;
			}
		}

		private void PreventChangingPlainEnglish(object sender, EventArgs args)
		{
			if (CurrentWsSetupModel.CurrentLanguageTag != "en")
			{
				_currentWs.Script = null;
				_currentWs.Region = null;
				_currentWs.Variants.Clear();
				ShowMessageBox(string.Format(FwCoreDlgs.kstidCantChangeEnglishSRV, CurrentWsSetupModel.CurrentDisplayLabel));
				// TODO (Hasso) 2019.05: reset the Special combobox to None (possibly by refreshing the entire view)
			}
		}

		/// <summary>
		/// This indicates if the advanced Script/Region/Variant view should be used
		/// </summary>
		public bool ShowAdvancedScriptRegionVariantView
		{
			get => _showAdvancedView || _currentWs.Language.IsPrivateUse || _currentWs.Script != null && _currentWs.Script.IsPrivateUse
				   || _currentWs.Region != null && _currentWs.Region.IsPrivateUse || _currentWs.Variants.Count > 1 && !_currentWs.Variants.First().IsPrivateUse;
			set
			{
				if (ShowAdvancedScriptRegionVariantView && !_currentWs.Language.IsPrivateUse && ConfirmClearAdvanced())
				{
					if (_currentWs.Region != null && _currentWs.Region.IsPrivateUse)
					{
						_currentWs.Region = null;
					}
					if (_currentWs.Script != null && _currentWs.Script.IsPrivateUse)
					{
						_currentWs.Script = null;
					}
					if (_currentWs.Variants.Count > 1)
					{
						_currentWs.Variants.RemoveRangeAt(1, _currentWs.Variants.Count - 1);
					}
					_showAdvancedView = value;
				}
				else if (!ShowAdvancedScriptRegionVariantView)
				{
					_showAdvancedView = true;
				}
			}
		}

		/// <summary>
		/// This is used to determine if the 'Advanced' checkbox should be shown under the writing system identity control
		/// </summary>
		internal bool ShowAdvancedScriptRegionVariantCheckBox => CurrentWsSetupModel.SelectionForSpecialCombo == WritingSystemSetupModel.SelectionsForSpecialCombo.ScriptRegionVariant
																 || ShowAdvancedScriptRegionVariantView;

		/// <summary>
		/// This indicates if the Graphite Font options should be configurable
		/// </summary>
		internal bool EnableGraphiteFontOptions => _currentWs?.DefaultFont != null && _currentWs.DefaultFont.Engines.HasFlag(FontEngines.Graphite);

		/// <summary/>
		internal bool CanMoveUp() => WorkingList.Count > 1 && WorkingList.First().WorkingWs != _currentWs;

		/// <summary/>
		internal bool CanMoveDown() => WorkingList.Count > 1 && WorkingList.Last().WorkingWs != _currentWs;

		/// <summary/>
		internal bool CanMerge() => WorkingList.Count > 1 && !IsCurrentWsNew() && !IsPlainEnglish();

		/// <summary/>
		internal bool CanDelete()
		{
			// The only remaining WS cannot be deleted from the list.
			// Plain English is a required Analysis WS, but it can be removed from other lists.
			return WorkingList.Count > 1 && (_listType != ListType.Analysis || !IsTheOriginalPlainEnglish());
		}

		private bool IsPlainEnglish() => CurrentWsSetupModel.CurrentLanguageTag == "en";

		/// <remarks>The original plain English is a required WS that cannot be changed or deleted</remarks>
		private bool IsTheOriginalPlainEnglish()
		{
			var origWs = WorkingList[CurrentWritingSystemIndex].OriginalWs;
			return origWs != null && origWs.LanguageTag == "en";
		}

		/// <summary/>
		internal void SelectWs(string wsTag)
		{
			// didn't change, no-op
			if (wsTag == _currentWs.LanguageTag)
			{
				return;
			}
			SelectWs(WorkingList.First(ws => ws.WorkingWs.LanguageTag == wsTag).WorkingWs);
		}

		/// <summary/>
		internal void SelectWs(int index)
		{
			// didn't change, no-op
			if (index == CurrentWritingSystemIndex)
			{
				return;
			}
			SelectWs(WorkingList[index].WorkingWs);
		}

		private void SelectWs(CoreWritingSystemDefinition ws)
		{
			_currentWs = ws;
			SetCurrentWsSetupModel(_currentWs);
			OnCurrentWritingSystemChanged(this, EventArgs.Empty);
		}

		/// <summary/>
		internal void ToggleInCurrentList()
		{
			var index = CurrentWritingSystemIndex;
			var newListItem = new WSListItemModel(!WorkingList[index].InCurrentList, WorkingList[index].OriginalWs, WorkingList[index].WorkingWs);
			WorkingList.RemoveAt(index);
			WorkingList.Insert(index, newListItem);
			CurrentWsListChanged = true;
		}

		/// <summary/>
		internal void MoveUp()
		{
			var currentItem = WorkingList.Find(ws => ws.WorkingWs == _currentWs);
			var currentIndex = WorkingList.IndexOf(currentItem);
			Guard.Against(currentIndex >= WorkingList.Count, "Programming error: Invalid state for MoveUp");
			WorkingList.Remove(currentItem);
			WorkingList.Insert(currentIndex - 1, currentItem);
			if (currentItem.InCurrentList)
			{
				CurrentWsListChanged = true;
			}
		}

		/// <summary/>
		internal void MoveDown()
		{
			var currentItem = WorkingList.Find(ws => ws.WorkingWs == _currentWs);
			var currentIndex = WorkingList.IndexOf(currentItem);
			Guard.Against(currentIndex >= WorkingList.Count, "Programming error: Invalid state for MoveUp");
			WorkingList.Remove(currentItem);
			WorkingList.Insert(currentIndex + 1, currentItem);
			if (currentItem.InCurrentList)
			{
				CurrentWsListChanged = true;
			}
		}

		/// <summary/>
		internal bool IsListValid => IsAtLeastOneSelected && FirstDuplicateWs == null;

		/// <summary/>
		internal bool IsAtLeastOneSelected => WorkingList.Any(item => item.InCurrentList);

		/// <returns>the DisplayLabel of the first duplicate WS; if there are no duplicates, <c>null</c></returns>
		internal string FirstDuplicateWs
		{
			get
			{
				var langTagSet = new HashSet<string>();
				foreach (var ws in WorkingList)
				{
					if (langTagSet.Contains(ws.WorkingWs.LanguageTag))
					{
						return ws.WorkingWs.DisplayLabel;
					}
					langTagSet.Add(ws.WorkingWs.LanguageTag);
				}
				return null;
			}
		}

		/// <summary/>
		internal string Title => $"{_listType.ToString()} Writing System Properties";

		/// <summary>
		/// The code for just the language part of the language tag. e.g. the en in en-Latn-US
		/// </summary>
		internal string LanguageCode => _currentWs?.Language.Iso3Code;

		/// <summary>
		/// The language name corresponding to just the language part of the tag e.g. French for fr-fonipa
		/// </summary>
		internal string LanguageName
		{
			get => _languageName;
			set
			{
				if (!string.IsNullOrEmpty(value) && value != _languageName)
				{
					foreach (var relatedWs in WorkingList.Where(ws => ws.WorkingWs.Language.Code == _currentWs.Language.Code))
					{
						relatedWs.WorkingWs.Language = new LanguageSubtag(relatedWs.WorkingWs.Language, value);
					}
				}
				_languageName = value;
			}
		}

		/// <summary>
		/// The descriptive name for the current writing system
		/// </summary>
		internal string WritingSystemName => _currentWs.DisplayLabel;

		/// <summary/>
		internal string EthnologueLabel => $"Ethnologue entry for {LanguageCode}";

		/// <summary/>
		internal string EthnologueLink => $"https://www.ethnologue.com/show_language.asp?code={LanguageCode}";

		/// <summary/>
		internal int CurrentWritingSystemIndex => WorkingList.FindIndex(ws => ws.WorkingWs == _currentWs);

		/// <summary/>
		internal void ChangeLanguage()
		{
			if (_currentWs.Language.Code == "en")
			{
				ShowMessageBox(FwCoreDlgs.kstidCantChangeEnglishWS);
				return;
			}

			if (ShowChangeLanguage(out var info))
			{
				if (WorkingList.Exists(ws => ws.WorkingWs.LanguageTag == info.LanguageTag))
				{
					ShowMessageBox(string.Format(FwCoreDlgs.kstidCantCauseDuplicateWS, info.LanguageTag, info.DesiredName));
					return;
				}
				var languagesToChange = new List<WSListItemModel>(WorkingList.Where(ws => ws.WorkingWs.LanguageName == _languageName));
				if (!IetfLanguageTag.TryGetSubtags(info.LanguageTag, out var languageSubtag, out var scriptSubtag, out var regionSubtag, out _))
				{
					return;
				}
				languageSubtag = new LanguageSubtag(languageSubtag, info.DesiredName);
				if (!CheckChangingWSForSRProject())
				{
					return;
				}
				foreach (var ws in languagesToChange)
				{
					ws.WorkingWs.Language = languageSubtag;
					if (ws.WorkingWs.Script == null)
					{
						ws.WorkingWs.Script = scriptSubtag;
					}
					if (ws.WorkingWs.Region == null)
					{
						ws.WorkingWs.Region = regionSubtag;
					}
				}
				// Set the private language name
				_languageName = info.DesiredName;
			}
		}

		/// <summary>
		/// Check if the writing system is being changed and prompt the user with instructions to successfully perform the change
		/// </summary>
		private bool CheckChangingWSForSRProject()
		{
			return !(FLExBridgeHelper.DoesProjectHaveFlexRepo(Cache?.ProjectId) || FLExBridgeHelper.DoesProjectHaveLiftRepo(Cache?.ProjectId)) || WorkingList.Where(ws => ws.OriginalWs != null).Where(ws => ws.WorkingWs.LanguageTag != ws.OriginalWs.LanguageTag).Any(ws => AcceptSharedWsChangeWarning(ws.OriginalWs.LanguageName));
		}

		/// <summary>
		/// Save all the writing system changes into the container
		/// </summary>
		internal void Save()
		{
			// Update the writing system data
			// when this dialog is called from the new language project dialog, there is no FDO cache,
			// but we still need to update the WorkingWs manager, so we have to execute the save even if Cache is null
			NonUndoableUnitOfWorkHelper uowHelper = null;
			if (Cache != null)
			{
				uowHelper = new NonUndoableUnitOfWorkHelper(Cache.ActionHandlerAccessor);
			}
			try
			{
				IList<CoreWritingSystemDefinition> currentWritingSystems;
				ICollection<CoreWritingSystemDefinition> allWritingSystems;
				// track the other list to see if a removed writing system should actually be deleted
				ICollection<CoreWritingSystemDefinition> otherWritingSystems;
				switch (_listType)
				{
					case ListType.Vernacular:
					{
						currentWritingSystems = _wsContainer.CurrentVernacularWritingSystems;
						allWritingSystems = _wsContainer.VernacularWritingSystems;
						otherWritingSystems = _wsContainer.AnalysisWritingSystems;
						break;
					}
					case ListType.Analysis:
					{
						currentWritingSystems = _wsContainer.CurrentAnalysisWritingSystems;
						allWritingSystems = _wsContainer.AnalysisWritingSystems;
						otherWritingSystems = _wsContainer.VernacularWritingSystems;
						break;
					}
					default:
						throw new NotImplementedException($"{_listType} not yet supported.");
				}
				// Track the new writing systems for importing translated lists
				var newWritingSystems = new List<CoreWritingSystemDefinition>();
				// Adjust the homograph writing system after possibly interacting with the user
				HandleHomographWsChanges(_homographWsWasTopVern, WorkingList, Cache?.LangProject.HomographWs, _homographWsWasInCurrent);
				// Handle any deleted writing systems
				DeleteWritingSystems(currentWritingSystems, allWritingSystems, otherWritingSystems, WorkingList.Select(ws => ws.WorkingWs));
				for (int workinglistIndex = 0, curIndex = 0; workinglistIndex < WorkingList.Count; ++workinglistIndex)
				{
					var wsListItem = WorkingList[workinglistIndex];
					var workingWs = wsListItem.WorkingWs;
					var origWs = wsListItem.OriginalWs;
					if (IsNew(wsListItem))
					{
						// origWs is used to update the order
						origWs = workingWs;
						// Create the new writing system, overwriting any existing ws of the same id
						_wsManager.Replace(origWs);
						newWritingSystems.Add(origWs);
					}
					else if (workingWs.IsChanged)
					{
						var oldId = origWs.Id;
						var oldHandle = origWs.Handle;
						// copy the working writing system content into the original writing system
						origWs.Copy(workingWs);
						if (string.IsNullOrEmpty(oldId) || !IetfLanguageTag.AreTagsEquivalent(oldId, workingWs.LanguageTag))
						{
							// update the ID
							_wsManager.Replace(origWs);
							if (uowHelper != null)
							{
								WritingSystemServices.UpdateWritingSystemId(Cache, origWs, oldHandle, oldId);
							}
						}
						_publisher?.Publish(new PublisherParameterObject("WritingSystemUpdated", origWs.Id));
					}
					// whether or not the WS was created or changed, its list position may have changed (LT-19788)
					AddOrMoveInList(allWritingSystems, workinglistIndex, origWs);
					if (wsListItem.InCurrentList)
					{
						AddOrMoveInList(currentWritingSystems, curIndex, origWs);
						++curIndex;
					}
					else
					{
						SafelyRemoveFromList(currentWritingSystems, wsListItem);
					}
				}
				// Handle any merged writing systems
				foreach (var mergedWs in _mergedWritingSystems)
				{
					WritingSystemServices.MergeWritingSystems(Cache, mergedWs.Key, mergedWs.Value);
				}
				// Save all the changes to the current writing systems
				_wsManager.Save();
				foreach (var newWs in newWritingSystems)
				{
					ImportListForNewWs(newWs.IcuLocale);
				}
				_projectLexiconSettingsDataMapper?.Write(_projectLexiconSettings);
				if (uowHelper != null)
				{
					uowHelper.RollBack = false;
				}
			}
			finally
			{
				if (CurrentWsListChanged)
				{
					WritingSystemListUpdated?.Invoke(this, EventArgs.Empty);
				}
				uowHelper?.Dispose();
			}
		}

		private static void AddOrMoveInList(ICollection<CoreWritingSystemDefinition> allWritingSystems, int desiredIndex, CoreWritingSystemDefinition workingWs)
		{
			// copy original contents into a list
			var updatedList = new List<CoreWritingSystemDefinition>(allWritingSystems);
			var ws = updatedList.Find(listItem => listItem.Id == (string.IsNullOrEmpty(workingWs.Id) ? workingWs.LanguageTag : workingWs.Id));
			if (ws != null)
			{
				updatedList.Remove(ws);
			}
			if (desiredIndex > updatedList.Count)
			{
				updatedList.Add(workingWs);
			}
			else
			{
				updatedList.Insert(desiredIndex, workingWs);
			}
			allWritingSystems.Clear();
			allWritingSystems.AddRange(updatedList);
		}

		/// <summary>
		/// Remove the writing system associated with this list item from the given list (unless it didn't exist there before)
		/// </summary>
		/// <returns>true if item was removed</returns>
		private static void SafelyRemoveFromList(IList<CoreWritingSystemDefinition> currentWritingSystems, WSListItemModel wsListItem)
		{
			if (wsListItem.OriginalWs != null)
			{
				currentWritingSystems.Remove(wsListItem.OriginalWs);
			}
		}

		private void HandleHomographWsChanges(bool homographWsWasTopVern, List<WSListItemModel> workingList, string homographWs, bool wasSelected)
		{
			if (_listType != ListType.Vernacular || Cache == null)
			{
				return;
			}
			// If the homograph writing system has been removed then change to the top current vernacular with no user interaction
			if (workingList.All(ws => ws.OriginalWs?.Id != homographWs))
			{
				Cache.LangProject.HomographWs = workingList.First(ws => ws.InCurrentList).WorkingWs.Id;
				return;
			}
			var userWantsChange = false;
			var newTopVernacular = workingList.First(ws => ws.InCurrentList);
			if (homographWsWasTopVern)
			{
				// if the top language is new or different then display the question
				if (newTopVernacular.OriginalWs == null || newTopVernacular.OriginalWs.Id != homographWs)
				{
					userWantsChange = ShouldChangeHomographWs(newTopVernacular.WorkingWs.DisplayLabel);
				}
			}
			else if(wasSelected && !workingList.First(ws => ws.OriginalWs?.Id == homographWs).InCurrentList)
			{
				userWantsChange = ShouldChangeHomographWs(newTopVernacular.WorkingWs.DisplayLabel);
			}
			if (userWantsChange)
			{
				Cache.LangProject.HomographWs = workingList.First(ws => ws.InCurrentList).WorkingWs.Id ?? workingList.First(ws => ws.InCurrentList).WorkingWs.LanguageTag;
			}
		}

		private void DeleteWritingSystems(ICollection<CoreWritingSystemDefinition> currentWritingSystems, ICollection<CoreWritingSystemDefinition> allWritingSystems,
			ICollection<CoreWritingSystemDefinition> otherWritingSystems, IEnumerable<CoreWritingSystemDefinition> workingWritingSystems)
		{
			// Delete any writing systems that were removed from the active list and are not present in the other list
			var deletedWsIds = new List<string>();
			var deletedWritingSystems = new List<CoreWritingSystemDefinition>(allWritingSystems);
			deletedWritingSystems.RemoveAll(ws => workingWritingSystems.Any(wws => wws.Id == ws.Id));
			foreach (var deleteCandidate in deletedWritingSystems)
			{
				currentWritingSystems.Remove(deleteCandidate);
				allWritingSystems.Remove(deleteCandidate);
				// The cache will be null while creating a new project, in which case we aren't really deleting anything
				if (!otherWritingSystems.Contains(deleteCandidate) && Cache != null)
				{
					WritingSystemServices.DeleteWritingSystem(Cache, deleteCandidate);
					deletedWsIds.Add(deleteCandidate.Id);
				}
			}
			if (deletedWsIds.Any())
			{
				_publisher?.Publish(new PublisherParameterObject("WritingSystemDeleted", deletedWsIds.ToArray()));
			}
		}

		private static bool IsNew(WSListItemModel tempWs)
		{
			return tempWs.OriginalWs == null;
		}

		private bool IsCurrentWsNew()
		{
			return IsNew(WorkingList[CurrentWritingSystemIndex]);
		}

		/// <summary/>
		internal List<WSMenuItemModel> GetAddMenuItems()
		{
			var addIpaInputSystem = FwCoreDlgs.WritingSystemList_AddIpa;
			var addAudioInputSystem = FwCoreDlgs.WritingSystemList_AddAudio;
			var addDialect = FwCoreDlgs.WritingSystemList_AddDialect;
			const string addNewLanguage = "Add new language...";
			var menuItemList = new List<WSMenuItemModel>();
			if (!ListHasIpaForSelectedWs())
			{
				menuItemList.Add(new WSMenuItemModel(string.Format(addIpaInputSystem, CurrentWsSetupModel.CurrentLanguageName), AddIpaHandler));
			}
			if (!ListHasVoiceForSelectedWs())
			{
				menuItemList.Add(new WSMenuItemModel(string.Format(addAudioInputSystem, CurrentWsSetupModel.CurrentLanguageName), AddAudioHandler));
			}
			menuItemList.Add(new WSMenuItemModel(string.Format(addDialect, CurrentWsSetupModel.CurrentLanguageName), AddDialectHandler));
			menuItemList.Add(new WSMenuItemModel(addNewLanguage, AddNewLanguageHandler));
			return menuItemList;
		}

		/// <summary/>
		internal List<WSMenuItemModel> GetRightClickMenuItems()
		{
			var deleteWritingSystem = FwCoreDlgs.WritingSystemList_DeleteWs;
			var mergeWritingSystem = FwCoreDlgs.WritingSystemList_MergeWs;
			var menuItemList = new List<WSMenuItemModel>();
			if (CanMerge())
			{
				menuItemList.Add(new WSMenuItemModel(mergeWritingSystem, MergeWritingSystem));
			}
			menuItemList.Add(new WSMenuItemModel(string.Format(deleteWritingSystem, CurrentWsSetupModel.CurrentDisplayLabel), DeleteCurrentWritingSystem, CanDelete()));
			return menuItemList;
		}

		private void MergeWritingSystem(object sender, EventArgs e)
		{
			if (!ConfirmMergeWritingSystem(CurrentWsSetupModel.CurrentDisplayLabel, out var mergeWithWsId))
			{
				return;
			}
			// If we are in the new language project dialog we do not need to track the merged writing systems
			if (Cache != null)
			{
				_mergedWritingSystems[WorkingList[CurrentWritingSystemIndex].OriginalWs] = mergeWithWsId;
			}
			WorkingList.RemoveAt(CurrentWritingSystemIndex);
			CurrentWsListChanged = true;
			SelectWs(WorkingList.First().WorkingWs);
		}

		private void DeleteCurrentWritingSystem(object sender, EventArgs e)
		{
			// If the writing system is in the other list as well, simply hide it silently.
			var otherList = _listType == ListType.Vernacular ? _wsContainer.AnalysisWritingSystems : _wsContainer.VernacularWritingSystems;
			if (WorkingList[CurrentWritingSystemIndex].InCurrentList)
			{
				CurrentWsListChanged = true;
			}
			if (otherList.Contains(_currentWs) // will be hidden, not deleted
				|| IsCurrentWsNew() // it hasn't been created yet, so it has no data
				|| ConfirmDeleteWritingSystem(CurrentWsSetupModel.CurrentDisplayLabel)) // prompt the user to delete the WS and its data
			{
				WorkingList.RemoveAt(CurrentWritingSystemIndex);
				SelectWs(WorkingList.First().WorkingWs);
			}
		}

		private bool ListHasVoiceForSelectedWs()
		{
			// build a string that represents the tag for an audio input system for
			// the current language and return if it is found in the list.
			return WorkingList.Exists(item => item.WorkingWs.LanguageTag == $"{_currentWs.Language.Code}-Zxxx-x-audio");
		}

		private bool ListHasIpaForSelectedWs()
		{
			// build a regex that will match an ipa input system for the current language
			// and return if it is found in the list.
			return WorkingList.Exists(item => Regex.IsMatch(item.WorkingWs.LanguageTag, $"^{_currentWs.Language.Code}(-.*)?-fonipa.*"));
		}

		private void AddNewLanguageHandler(object sender, EventArgs e)
		{
			if (_listType == ListType.Vernacular && !AddNewVernacularLanguageWarning())
			{
				return;
			}
			if (ShowChangeLanguage(out var langInfo))
			{
				WSListItemModel wsListItem;
				if (_wsManager.TryGet(langInfo.LanguageTag, out var wsDef))
				{
					// (LT-19728) At this point, wsDef is a live reference to an actual WS in this project.
					// We don't want the user modifying plain English, or modifying any WS without performing the necessary update steps,
					// so create a "new dialect" (if the selected WS is already in the current list)
					// or set the OriginalWS and create a copy for editing (if this is the first instance of the selected WS in the current list)
					if (WorkingList.Any(wItem => wItem.WorkingWs == wsDef))
					{
						// The requested WS already exists in the list; create a dialect
						AddDialectOf(wsDef);
						return;
					}
					// Set the WS up as an existing WS, the same way as existings WS's are set up when the dialog is opened:
					// (later in this method, we set wsDef's Language Name to the user's DesiredName. This needs to happen on the working WS)
					var origWs = wsDef;
					wsDef = new CoreWritingSystemDefinition(wsDef, true);
					wsListItem = new WSListItemModel(true, origWs, wsDef);
				}
				else
				{
					wsDef = _wsManager.Set(langInfo.LanguageTag);
					wsListItem = new WSListItemModel(true, null, wsDef);
				}
				wsDef.Language = new LanguageSubtag(wsDef.Language, langInfo.DesiredName);
				WorkingList.Insert(CurrentWritingSystemIndex + 1, wsListItem);
				CurrentWsListChanged = true;
				SelectWs(wsDef);
			}
		}

		private void AddDialectHandler(object sender, EventArgs e)
		{
			AddDialectOf(_currentWs);
		}

		private void AddDialectOf(CoreWritingSystemDefinition baseWs)
		{
			var wsDef = new CoreWritingSystemDefinition(baseWs);
			WorkingList.Insert(CurrentWritingSystemIndex + 1, new WSListItemModel(true, null, wsDef));
			CurrentWsListChanged = true;
			// Set language name to be based on current language
			wsDef.Language = new LanguageSubtag(wsDef.Language, baseWs.LanguageName);
			// Can't use SelectWs because it won't select ScriptRegionVariant in the combobox when no SRV info has been entered
			CurrentWsSetupModel = new WritingSystemSetupModel(wsDef, WritingSystemSetupModel.SelectionsForSpecialCombo.ScriptRegionVariant);
			_currentWs = wsDef;
			OnCurrentWritingSystemChanged(this, EventArgs.Empty);
		}

		private void AddAudioHandler(object sender, EventArgs e)
		{
			var wsDef = new CoreWritingSystemDefinition(_currentWs) {IsVoice = true};
			// Set language name to be based on current language
			wsDef.Language = new LanguageSubtag(wsDef.Language, _currentWs.LanguageName);
			WorkingList.Insert(CurrentWritingSystemIndex + 1, new WSListItemModel(true, null, wsDef));
			CurrentWsListChanged = true;
			SelectWs(wsDef);
		}

		private void AddIpaHandler(object sender, EventArgs e)
		{
			var variants = new List<VariantSubtag> { WellKnownSubtags.IpaVariant };
			variants.AddRange(_currentWs.Variants.Where(variant => variant != WellKnownSubtags.AudioPrivateUse));
			var cleanScript = _currentWs.Script == null || _currentWs.Script.Code == WellKnownSubtags.AudioScript ? null : _currentWs.Script;
			var ipaLanguageTag = IetfLanguageTag.Create(_currentWs.Language, cleanScript, _currentWs.Region, variants);
			if (!_wsManager.TryGet(ipaLanguageTag, out var wsDef))
			{
				wsDef = new CoreWritingSystemDefinition(ipaLanguageTag);
				_wsManager.Set(wsDef);
			}
			wsDef.Abbreviation = "ipa";
			var ipaKeyboard = Keyboard.Controller.AvailableKeyboards.FirstOrDefault(k => k.Id.ToLower().Contains("ipa"));
			if (ipaKeyboard != null)
			{
				wsDef.Keyboard = ipaKeyboard.Id;
			}
			// Set language name to be based on current language
			wsDef.Language = new LanguageSubtag(wsDef.Language, _currentWs.LanguageName);
			WorkingList.Insert(CurrentWritingSystemIndex + 1, new WSListItemModel(true, null, wsDef));
			CurrentWsListChanged = true;
			SelectWs(wsDef);
		}

		/// <summary/>
		internal void EditValidCharacters()
		{
			ShowValidCharsEditor();
		}

		/// <summary/>
		internal List<string> GetEncodingConverters()
		{
			var encodingConverters = new List<string> {FwCoreDlgs.kstidNone};
			encodingConverters.AddRange(EncodingConverterKeys().Cast<string>());
			return encodingConverters;
		}

		/// <summary/>
		internal void ModifyEncodingConverters()
		{
			var oldConverter = CurrentLegacyConverter;
			if (ShowModifyEncodingConverters(oldConverter, out var selectedConverter))
			{
				CurrentLegacyConverter = selectedConverter;
			}
			else
			{
				if (!GetEncodingConverters().Contains(oldConverter))
				{
					CurrentLegacyConverter = null;
				}
			}
		}

		/// <summary/>
		internal string CurrentLegacyConverter
		{
			get => _currentWs?.LegacyMapping;
			set => _currentWs.LegacyMapping = value;
		}

		/// <summary>
		/// Any writing system that existed before we started working and that is not the current writing system is
		/// a potential merge target
		/// </summary>
		internal IEnumerable<WSListItemModel> MergeTargets
		{
			get
			{
				return WorkingList.Where(item => item.OriginalWs != null && item.WorkingWs != _currentWs);
			}
		}

		/// <summary>
		/// Are we displaying the share with SLDR setting
		/// </summary>
		internal bool ShowSharingWithSldr => _listType == ListType.Vernacular;

		/// <summary>
		/// Should the vernacular language data be shared with the SLDR
		/// </summary>
		internal bool IsSharingWithSldr {
			get => _projectLexiconSettings.AddWritingSystemsToSldr;
			set => _projectLexiconSettings.AddWritingSystemsToSldr = value;
		}

		/// <summary>
		/// Set to true if anything that would update the displayed view of writing systems has changed.
		/// Moving current writing systems up or down. Adding a writing system, removing a writing system,
		/// or changing the selection state of a writing system.
		/// </summary>
		/// <remarks>We are not currently attempting to set back to false if a user undoes some work</remarks>
		internal bool CurrentWsListChanged { get; private set; }

		/// <summary/>
		internal SpellingDictionaryItem SpellingDictionary
		{
			get => string.IsNullOrEmpty(_currentWs?.SpellCheckingId)
					? new SpellingDictionaryItem(null, null) : new SpellingDictionaryItem(_currentWs.SpellCheckingId.Replace('_', '-'), _currentWs.SpellCheckingId);
			set
			{
				if (_currentWs != null)
				{
					_currentWs.SpellCheckingId = value?.Id;
				}
			}
		}

		/// <summary/>
		internal SpellingDictionaryItem[] GetSpellingDictionaryComboBoxItems()
		{
			var dictionaries = new List<SpellingDictionaryItem> { new SpellingDictionaryItem(FwCoreDlgs.ksWsNoDictionaryMatches, FwCoreDlgs.kstidNone) };
			var spellCheckingDictionary = _currentWs.SpellCheckingId;
			if (string.IsNullOrEmpty(spellCheckingDictionary))
			{
				dictionaries.Add(new SpellingDictionaryItem(_currentWs.LanguageTag, _currentWs.LanguageTag.Replace('-', '_')));
			}
			var fDictionaryExistsForLanguage = false;
			var fAlternateDictionaryExistsForLanguage = false;
			dictionaries.AddRange(SpellingHelper.GetDictionaryIds().OrderBy(GetDictionaryName).Select(languageId => new SpellingDictionaryItem(GetDictionaryName(languageId), languageId)));
			return dictionaries.ToArray();
		}

		private static string GetDictionaryName(string languageId)
		{
			var locale = new Locale(languageId);
			var country = locale.GetDisplayCountry("en");
			var languageName = locale.GetDisplayLanguage("en");
			var languageAndCountry = new StringBuilder(languageName);
			if (!string.IsNullOrEmpty(country))
			{
				languageAndCountry.AppendFormat(" ({0})", country);
			}
			if (languageName != languageId)
			{
				languageAndCountry.AppendFormat(" [{0}]", languageId);
			}
			return languageAndCountry.ToString();
		}
	}
}
