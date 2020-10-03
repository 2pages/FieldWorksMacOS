// Copyright (c) 2017-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;
using LanguageExplorer.Controls;
using LanguageExplorer.LIFT;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.RootSites;
using SIL.FieldWorks.FwCoreDlgs;
using SIL.FieldWorks.Resources;
using SIL.IO;
using SIL.LCModel;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;
using SIL.LCModel.Utils;
using SIL.Lift;
using SIL.Lift.Migration;
using SIL.Lift.Parsing;
using SIL.Lift.Validation;

namespace LanguageExplorer.SendReceive
{
	/// <summary>
	/// Handle the menus on the global Send/Receive menu.
	/// </summary>
	/// <remarks>
	/// This class is not even created if FB is not installed.
	/// </remarks>
	internal sealed class SendReceiveMenuManager : IFlexComponent, IDisposable
	{
		private GlobalUiWidgetParameterObject _globalUiWidgetParameterObject;
		private readonly Dictionary<string, IBridge> _bridges = new Dictionary<string, IBridge>(2);
		private IdleQueue IdleQueue { get; set; }
		private IFwMainWnd MainWindow { get; set; }
		private IFlexApp FlexApp { get; set; }
		private LcmCache Cache { get; set; }

		// For send/receive involving LIFT projects, use the lift version "0.13_ldml3" so the version 3 ldml files will exist on a different chorus branch
		private const string LiftModelVersion = "0.13_ldml3";

		private const string NoBridgeUsedYet = "NoBridgeUsedYet";

		/// <summary />
		internal SendReceiveMenuManager(IdleQueue idleQueue, IFwMainWnd mainWindow, IFlexApp flexApp, LcmCache cache, GlobalUiWidgetParameterObject globalParameterObject)
		{
			Guard.AgainstNull(idleQueue, nameof(idleQueue));
			Guard.AgainstNull(mainWindow, nameof(mainWindow));
			Guard.AgainstNull(flexApp, nameof(flexApp));
			Guard.AgainstNull(cache, nameof(cache));
			Guard.AgainstNull(globalParameterObject, nameof(globalParameterObject));

			IdleQueue = idleQueue;
			MainWindow = mainWindow;
			FlexApp = flexApp;
			Cache = cache;
			_globalUiWidgetParameterObject = globalParameterObject;
		}

		private Tuple<bool, bool> CanDoCmdCheckForFlexBridgeUpdates => new Tuple<bool, bool>(!MiscUtils.IsUnix, !MiscUtils.IsUnix);

		private Tuple<bool, bool> CanDoCmdFLExLiftBridge
		{
			get
			{
				bool enabled;
				var lastBridgeUsed = GetLastBridge();
				if (lastBridgeUsed == null)
				{
					enabled = false;
				}
				else
				{
					switch (lastBridgeUsed.Name)
					{
						case LanguageExplorerConstants.FLExBridge:
							// If Fix it app does not exist, then disable main FLEx S/R, since FB needs to call it, after a merge.
							// If !IsConfiguredForSR (failed the first time), disable the button and hotkey
							enabled = FLExBridgeHelper.FixItAppExists && IsConfiguredForSR(Cache.ProjectId.ProjectFolder);
							break;
						case LanguageExplorerConstants.LiftBridge:
							// If !IsConfiguredForLiftSR (failed first time), disable the button and hotkey
							enabled = IsConfiguredForLiftSR(Cache.ProjectId.ProjectFolder);
							break;
						case NoBridgeUsedYet: // Fall through. This isn't really needed, but it is clearer that it covers the case.
						default:
							enabled = false;
							break;
					}
				}
				return new Tuple<bool, bool>(true, enabled);
			}
		}

		#region Implementation of IPropertyTableProvider

		/// <inheritdoc />
		public IPropertyTable PropertyTable { get; private set; }

		#endregion

		#region Implementation of IPublisherProvider

		/// <inheritdoc />
		public IPublisher Publisher { get; private set; }

		#endregion

		#region Implementation of ISubscriberProvider

		/// <inheritdoc />
		public ISubscriber Subscriber { get; private set; }

		#endregion

		#region Implentation of IFlexComponent

		/// <inheritdoc />
		public void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
		{
			FlexComponentParameters.CheckInitializationValues(flexComponentParameters, new FlexComponentParameters(PropertyTable, Publisher, Subscriber));

			PropertyTable = flexComponentParameters.PropertyTable;
			Publisher = flexComponentParameters.Publisher;
			Subscriber = flexComponentParameters.Subscriber;

			_bridges.Add(LanguageExplorerConstants.FLExBridge, new FlexBridge(Cache, FlexApp));
			_bridges.Add(LanguageExplorerConstants.LiftBridge, new LiftBridge(Cache, MainWindow, FlexApp));
			_bridges.Add(NoBridgeUsedYet, null);
			var flexBridge = _bridges[LanguageExplorerConstants.FLExBridge];
			flexBridge.InitializeFlexComponent(flexComponentParameters);
			flexBridge.RegisterHandlers(_globalUiWidgetParameterObject);
			var liftBridge = _bridges[LanguageExplorerConstants.LiftBridge];
			liftBridge.InitializeFlexComponent(flexComponentParameters);
			liftBridge.RegisterHandlers(_globalUiWidgetParameterObject);
			// Common to Project and LIFT S/R.
			// S/R menu
			var srMenuDictionary = _globalUiWidgetParameterObject.GlobalMenuItems[MainMenu.SendReceive];
			srMenuDictionary.Add(Command.CmdHelpChorus, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(HelpChorus_Click, () => UiWidgetServices.CanSeeAndDo));
			srMenuDictionary.Add(Command.CmdCheckForFlexBridgeUpdates, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CheckForFlexBridgeUpdates_Click, () => CanDoCmdCheckForFlexBridgeUpdates));
			srMenuDictionary.Add(Command.CmdHelpAboutFLEXBridge, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(HelpAboutFLEXBridge_Click, () => UiWidgetServices.CanSeeAndDo));
			// Tool bar button
			_globalUiWidgetParameterObject.GlobalToolBarItems[ToolBar.Standard].Add(Command.CmdFLExLiftBridge, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(Flex_Or_Lift_Bridge_Clicked, () => CanDoCmdFLExLiftBridge));
		}

		#endregion

		private void HelpAboutFLEXBridge_Click(object sender, EventArgs e)
		{
			FLExBridgeHelper.LaunchFieldworksBridge(Cache.GetFullProjectFileName(), LanguageExplorerConstants.SendReceiveUser, FLExBridgeHelper.AboutFLExBridge,
				null, LcmCache.ModelVersion, LiftModelVersion, null, null, out _, out _);
		}

		private void CheckForFlexBridgeUpdates_Click(object sender, EventArgs e)
		{
			FLExBridgeHelper.LaunchFieldworksBridge(Cache.GetFullProjectFileName(), LanguageExplorerConstants.SendReceiveUser, FLExBridgeHelper.CheckForUpdates,
				null, LcmCache.ModelVersion, LiftModelVersion, null, null, out _, out _);
		}

		private void HelpChorus_Click(object sender, EventArgs eventArgs)
		{
			var chorusHelpPathname = Path.Combine(Path.GetDirectoryName(FLExBridgeHelper.FullFieldWorksBridgePath()), "Chorus_Help.chm");
			try
			{
				// When the help window is closed it will return focus to the window that opened it (see MSDN
				// documentation for HtmlHelp()). We don't want to use the main window as the parent, because if
				// a modal dialog is visible, it will still return focus to the main window, allowing the main window
				// to perform some behaviors (such as refresh by pressing F5) while the modal dialog is visible,
				// which can be bad. So, we just create a dummy control and pass that in as the parent.
				using (var dummyParent = new Control())
				{
					Help.ShowHelp(dummyParent, chorusHelpPathname);
				}
			}
			catch (Exception)
			{
				MessageBox.Show((Form)MainWindow, string.Format(LanguageExplorerResources.ksCannotLaunchX, chorusHelpPathname), LanguageExplorerResources.ksError);
			}
		}

		private IBridge GetLastBridge()
		{
			return _bridges[PropertyTable.GetValue<string>(LanguageExplorerConstants.LastBridgeUsed, NoBridgeUsedYet, SettingsGroup.LocalSettings)];
		}

		private void Flex_Or_Lift_Bridge_Clicked(object sender, EventArgs e)
		{
			var lastBridgeRun = GetLastBridge();
			// Process the event for the toolbar button that does S/R for the last repo that was done.
			if (MiscUtils.IsMono)
			{
				// This is a horrible workaround for a nasty bug in Mono. The toolbar button captures the mouse,
				// and does not release it before calling this event handler. If we proceed to run the bridge,
				// which freezes our UI thread until FlexBridge returns, the mouse stays captured...and the whole
				// system UI is frozen, for all applications.
				IdleQueue.Add(IdleQueuePriority.High, obj =>
				{
					lastBridgeRun.RunBridge();
					return true; // IdleQueue requires the function to have a boolean return value.
				});
			}
			else
			{
				// on windows we can safely do it right away.
				lastBridgeRun.RunBridge();
			}
		}

		internal static bool IsConfiguredForLiftSR(string folder)
		{
			var otherRepoPath = Path.Combine(folder, LcmFileHelper.OtherRepositories);
			if (!Directory.Exists(otherRepoPath))
			{
				return false;
			}
			var liftFolder = Directory.EnumerateDirectories(otherRepoPath, "*_LIFT").FirstOrDefault();
			return !string.IsNullOrEmpty(liftFolder) && IsConfiguredForSR(liftFolder);
		}

		private static void PrepareForSR(IPropertyTable propertyTable, IPublisher publisher, LcmCache cache, IBridge lastBridgeUsed)
		{
			//Make sure any last changes are saved. (Process focus lost for controls)
			Application.DoEvents();
			StopParser(publisher);
			//Give all forms the opportunity to save any uncommitted data
			//(important for analysis sandboxes)
			var activeForm = propertyTable.GetValue<Form>(FwUtilsConstants.window);
			activeForm?.ValidateChildren(ValidationConstraints.Enabled);
			//Commit all the data in the cache and save to disk
			ProjectLockingService.UnlockCurrentProject(cache);
			propertyTable.SetProperty(LanguageExplorerConstants.LastBridgeUsed, lastBridgeUsed.Name, true, settingsGroup: SettingsGroup.LocalSettings);
		}

		private static void StopParser(IPublisher publisher)
		{
			publisher.Publish(new PublisherParameterObject(LanguageExplorerConstants.StopParser));
		}

		/// <summary>Callback to refresh the Message Slice after OnView[Lift]Messages</summary>
		private static void BroadcastMasterRefresh(IPublisher publisher)
		{
			publisher.Publish(new PublisherParameterObject(LanguageExplorerConstants.MasterRefresh));
		}

		private static bool ShowMessageBeforeFirstSendReceive_IsUserReady(IHelpTopicProvider helpTopicProvider)
		{
			using (var firstTimeDlg = new FLExBridgeFirstSendReceiveInstructionsDlg(helpTopicProvider))
			{
				return DialogResult.OK == firstTimeDlg.ShowDialog();
			}
		}

		private static void PublishHandleLocalHotlinkMessage(IPublisher publisher, object sender, FLExJumpEventArgs e)
		{
			if (!string.IsNullOrEmpty(e.JumpUrl))
			{
				publisher.Publish(new PublisherParameterObject(FwUtilsConstants.HandleLocalHotlink, new LocalLinkArgs { Link = e.JumpUrl }));
			}
		}

		private static void ReportDuplicateBridge()
		{
			ObtainProjectMethod.ReportDuplicateBridge();
		}

		private static void RefreshCacheWindowAndAll(IFieldWorksManager manager, string fullProjectFileName, string publisherMessage, bool conflictOccurred)
		{
			var appArgs = new FwAppArgs(fullProjectFileName);
			var newAppWindow = (IFwMainWnd)manager.ReopenProject(manager.Cache.ProjectId.Name, appArgs).ActiveMainWindow;
			if (newAppWindow.PropertyTable.GetValue(LanguageExplorerConstants.UseVernSpellingDictionary, true))
			{
				WfiWordformServices.ConformSpellingDictToWordforms(newAppWindow.Cache);
			}
			// Clear out any sort cache files (or whatever else might mess us up) and then refresh
			newAppWindow.ClearInvalidatedStoredData();
			newAppWindow.RefreshAllViews();
			if (conflictOccurred)
			{
				// Send a message for the reopened instance to display the message viewer (used to be conflict report).
				// Caller has been disposed by now.
				newAppWindow.Publisher.Publish(new PublisherParameterObject(publisherMessage));
			}
		}

		private static bool IsConfiguredForSR(string projectFolder)
		{
			return Directory.Exists(Path.Combine(projectFolder, ".hg"));
		}

		/// <summary>
		/// Returns true if there are any Chorus Notes to view in the main FW repo or in the Lift repo.
		/// </summary>
		/// <param name="cache"></param>
		/// <param name="checkForLiftNotes">
		/// When 'false', then don't consider any Lift notes files in considering those present.
		/// When 'true', then skip any Flex notes, and only consider the Lift notes.
		/// </param>
		/// <returns>'true' if there are any Chorus Notes files at the given level. Otherwise, it returns 'false'.</returns>
		private static bool NotesFileIsPresent(LcmCache cache, bool checkForLiftNotes)
		{
			// Default to look for notes in the main FW repo.
			var folderToSearchIn = cache.ProjectId.ProjectFolder;
			var liftFolder = GetLiftRepositoryFolderFromFwProjectFolder(folderToSearchIn);
			if (checkForLiftNotes)
			{
				if (!Directory.Exists(liftFolder))
				{
					return false; // If the folder doesn't even exist, there can't be any lift notes.
				}

				// Switch to look for note files in the Lift repo.
				folderToSearchIn = liftFolder;
			}
			if (!Directory.Exists(Path.Combine(folderToSearchIn, ".hg")))
			{
				return false; // No repo, so there can be no notes files.
			}
			foreach (var notesPathname in Directory.GetFiles(folderToSearchIn, "*.ChorusNotes", SearchOption.AllDirectories))
			{
				if (!NotesFileHasContent(notesPathname) || checkForLiftNotes)
				{
					continue; // Skip ones with no content.
				}
				if (!notesPathname.Contains(liftFolder)/* Skip any lift ones down in a nested repo. */)
				{
					return true;
				}
				// Must be a nested lift one to get here, so try another one.
			}
			return false;
		}

		private static bool NotesFileHasContent(string chorusNotesPathname)
		{
			var doc = XDocument.Load(chorusNotesPathname);
			return doc.Root.HasElements; // Files with no notes (e.g., "Lexicon.fwstub.ChorusNotes") are not interesting.
		}

		/// <summary>
		/// Convert FLEx ChorusNotes file referencing lex entries to LIFT notes by adjusting the "ref" attributes.
		/// </summary>
		/// <remarks>
		/// This method is internal, rather than static to let a test call it.
		/// </remarks>
		internal static void ConvertFlexNotesToLift(TextReader reader, TextWriter writer, string liftFileName)
		{
			// Typical input is something like
			// silfw://localhost/link?app=flex&amp;database=current&amp;server=&amp;tool=default&amp;guid=bab7776e-531b-4ce1-997f-fa638c09e381&amp;tag=&amp;id=bab7776e-531b-4ce1-997f-fa638c09e381&amp;label=Entry &quot;pintu&quot;
			// produce: lift://John.lift?type=entry&amp;label=fox&amp;id=f3093b9b-ea2f-422b-86b6-0defaa4646fe
			ConvertRefAttrs(reader, writer, liftFileName, "lift://{0}?type=entry&amp;label={1}&amp;id={2}");
		}

		/// <summary>
		/// Convert LIFT ChorusNotes file to FLEx notes by adjusting the "ref" attributes.
		/// </summary>
		/// <remarks>
		/// This method is internal, rather than static to let a test call it.
		/// </remarks>
		internal static void ConvertLiftNotesToFlex(TextReader reader, TextWriter writer)
		{
			// produce: silfw://localhost/link?app=flex&amp;database=current&amp;server=&amp;tool=default&amp;guid=bab7776e-531b-4ce1-997f-fa638c09e381&amp;tag=&amp;id=bab7776e-531b-4ce1-997f-fa638c09e381&amp;label=Entry &quot;pintu&quot;
			ConvertRefAttrs(reader, writer, String.Empty, "silfw://localhost/link?app=flex&amp;database=current&amp;server=&amp;tool=default&amp;guid={2}&amp;tag=&amp;id={2}&amp;label={1}");
		}

		private static void ConvertRefAttrs(TextReader reader, TextWriter writer, string liftFileName, string outputTemplate)
		{
			// Typical input is something like
			// silfw://localhost/link?app=flex&amp;database=current&amp;server=&amp;tool=default&amp;guid=bab7776e-531b-4ce1-997f-fa638c09e381&amp;tag=&amp;id=bab7776e-531b-4ce1-997f-fa638c09e381&amp;label=Entry &quot;pintu&quot;
			// or: lift://John.lift?type=entry&amp;label=fox&amp;id=f3093b9b-ea2f-422b-86b6-0defaa4646fe
			// both contain id=...&amp; and label=...&amp. One may be at the end without following &amp;.
			// Note that the ? is essential to prevent the greedy match including multiple parameters.
			// A label may contain things like &quot; so we can't just search for [^&]*.
			var reOuter = new Regex("ref=\\\"([^\\\"]*)\"");
			var reLabel = new Regex("label=(.*?)(&amp;|$)");
			var reId = new Regex("id=(.*?)(&amp;|$)");
			string line;
			while ((line = reader.ReadLine()) != null)
			{
				var matchLine = reOuter.Match(line);
				if (matchLine.Success)
				{
					var input = matchLine.Groups[1].Value;
					var matchLabel = reLabel.Match(input);
					var matchId = reId.Match(input);
					if (matchLabel.Success && matchId.Success)
					{
						var guid = matchId.Groups[1].Value;
						var label = matchLabel.Groups[1].Value;
						var output = string.Format(outputTemplate, liftFileName, label, guid);
						writer.WriteLine(line.Replace(input, output));
						continue;
					}
				}
				writer.WriteLine(line);
			}
		}

		/// <summary />
		internal static string GetLiftRepositoryFolderFromFwProjectFolder(string projectFolder)
		{
			var otherDir = Path.Combine(projectFolder, LcmFileHelper.OtherRepositories);
			if (Directory.Exists(otherDir))
			{
				var extantOtherFolders = Directory.GetDirectories(otherDir);
				var extantLiftFolder = extantOtherFolders.FirstOrDefault(folder => folder.EndsWith("_LIFT"));
				if (extantLiftFolder != null)
				{
					return extantLiftFolder; // Reuse the old one, no matter what the new project dir name is.
				}
			}
			var flexProjName = Path.GetFileName(projectFolder);
			return Path.Combine(projectFolder, LcmFileHelper.OtherRepositories, flexProjName + '_' + FLExBridgeHelper.LIFT);
		}

		#region IDisposable
		private bool _isDisposed;

		~SendReceiveMenuManager()
		{
			Dispose(false);
		}

		/// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
		public void Dispose()
		{
			Dispose(true);
			// The base class finalizer is called automatically.
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			if (_isDisposed)
			{
				// No need to run it more than once.
				return;
			}

			if (disposing)
			{
				foreach (var bridge in _bridges.Values)
				{
					// "NoBridgeUsedYet" key will have  null value in the dictionary, so skip it.
					bridge?.Dispose();
				}
				_bridges.Clear();
			}

			IdleQueue = null;
			MainWindow = null;
			FlexApp = null;
			Cache = null;
			_globalUiWidgetParameterObject = null;

			_isDisposed = true;
		}

		/// <summary>
		/// This is invoked by reflection, due to almost insuperable spaghetti in the relevant project references,
		/// from ChoooseLangProjectDialog.CreateProjectFromLift().
		/// If you are tempted to rename the method, be sure to do so in ChooseLangProjectDialog.CreateProjectFromLift(), as well.
		/// </summary>
		internal static bool ImportObtainedLexicon(LcmCache cache, string liftPath, Form parentForm)
		{
			using (var liftBridge = new LiftBridge(cache, liftPath, parentForm))
			{
				return liftBridge.ImportLiftCommon(MergeStyle.MsKeepBoth); // should be a new project
			}
		}
		#endregion

		/// <summary>
		/// Interface that allows for running different kinds of S/R bridges.
		/// </summary>
		private interface IBridge : IFlexComponent, IDisposable
		{
			/// <summary>
			/// Get the name of the bridge.
			/// </summary>
			/// <remarks>
			/// Names of all implementations, *must* be unique!
			/// </remarks>
			string Name { get; }

			/// <summary>
			/// Run the bridge.
			/// </summary>
			void RunBridge();

			/// <summary>
			/// Register UI widgets.
			/// </summary>
			void RegisterHandlers(GlobalUiWidgetParameterObject globalParameterObject);
		}

		private sealed class FlexBridge : IBridge
		{
			private LcmCache Cache { get; set; }
			private IFlexApp FlexApp { get; set; }

			internal FlexBridge(LcmCache cache, IFlexApp flexApp)
			{
				Guard.AgainstNull(cache, nameof(cache));
				Guard.AgainstNull(flexApp, nameof(flexApp));

				Cache = cache;
				FlexApp = flexApp;
			}

			private Tuple<bool, bool> CanDoCmdFLExBridge => new Tuple<bool, bool>(true, IsConfiguredForSR(Cache.ProjectId.ProjectFolder) && FLExBridgeHelper.FixItAppExists);

			private Tuple<bool, bool> CanDoCmdViewMessages => new Tuple<bool, bool>(true, NotesFileIsPresent(Cache, false));

			private Tuple<bool, bool> CanDoCmdObtainAnyFlexBridgeProject => new Tuple<bool, bool>(true, IsConfiguredForSR(Cache.ProjectId.ProjectFolder) && FLExBridgeHelper.FixItAppExists);

			private Tuple<bool, bool> CanDoCmdObtainFirstFlexBridgeProject => new Tuple<bool, bool>(true, !IsConfiguredForSR(Cache.ProjectId.ProjectFolder));

			#region Implementation of IBridge
			/// <inheritdoc />
			public string Name => LanguageExplorerConstants.FLExBridge;

			/// <inheritdoc />
			public void RunBridge()
			{
				PrepareForSR(PropertyTable, Publisher, Cache, this);
				if (!LcmFileHelper.GetDefaultLinkedFilesDir(Cache.ServiceLocator.DataSetup.ProjectId.ProjectFolder).Equals(Cache.LanguageProject.LinkedFilesRootDir))
				{
					using (var dlg = new WarningNotUsingDefaultLinkedFilesLocation(FlexApp))
					{
						var result = dlg.ShowDialog();
						if (result == DialogResult.Yes)
						{
							var sLinkedFilesRootDir = Cache.LangProject.LinkedFilesRootDir;
							NonUndoableUnitOfWorkHelper.Do(Cache.ActionHandlerAccessor, () =>
							{
								Cache.LangProject.LinkedFilesRootDir = LcmFileHelper.GetDefaultLinkedFilesDir(Cache.ProjectId.ProjectFolder);
							});
							FlexApp.UpdateExternalLinks(sLinkedFilesRootDir);
						}
					}
				}
				// Make sure that there aren't multiple applications accessing the project
				// It is possible for a user to start up an application that accesses the
				// project after this check, but the application should not interfere with
				// the S/R operation.
				while (SharedBackendServices.AreMultipleApplicationsConnected(Cache))
				{
					if (ThreadHelper.ShowMessageBox(null, LanguageExplorerResources.ksSendReceiveNotPermittedMultipleAppsText, LanguageExplorerResources.ksSendReceiveNotPermittedMultipleAppsCaption, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) == DialogResult.Cancel)
					{
						return;
					}
				}
				string url;
				var projectFolder = Cache.ProjectId.ProjectFolder;
				var savedState = PrepareToDetectMainConflicts(projectFolder);
				var fullProjectFileName = Path.Combine(projectFolder, Cache.ProjectId.Name + LcmFileHelper.ksFwDataXmlFileExtension);
				bool dataChanged;
				using (CopyDictionaryConfigFileToTemp(projectFolder))
				{
					var success = FLExBridgeHelper.LaunchFieldworksBridge(fullProjectFileName, LanguageExplorerConstants.SendReceiveUser, FLExBridgeHelper.SendReceive, null, LcmCache.ModelVersion,
						LiftModelVersion, Cache.LangProject.DefaultVernacularWritingSystem.Id, null, out dataChanged, out _);
					if (!success)
					{
						ReportDuplicateBridge();
						ProjectLockingService.LockCurrentProject(Cache);
						return;
					}
				}
				if (dataChanged)
				{
					RefreshCacheWindowAndAll(FlexApp.FwManager, fullProjectFileName, "ViewMessages", DetectMainConflicts(projectFolder, savedState));
				}
				else //Re-lock project if we aren't trying to close the app
				{
					ProjectLockingService.LockCurrentProject(Cache);
				}
			}

			private static bool DetectMainConflicts(string path, IReadOnlyDictionary<string, long> savedState)
			{
				foreach (var file in Directory.GetFiles(path, "*.ChorusNotes", SearchOption.AllDirectories))
				{
					// TODO: Test to see if one conflict tool can do both FLEx and LIFT conflicts.
					if (file.Contains(LcmFileHelper.OtherRepositories))
					{
						continue; // Skip them, since they are part of some other repository.
					}
					savedState.TryGetValue(file, out var oldLength);
					if (new FileInfo(file).Length == oldLength)
					{
						continue; // no new notes in this file.
					}
					return true; // Review JohnT: do we need to look in the file to see if what was added is a conflict?
				}
				return false; // no conflicts added.
			}

			/// <inheritdoc />
			public void RegisterHandlers(GlobalUiWidgetParameterObject globalParameterObject)
			{
				Guard.AgainstNull(globalParameterObject, nameof(globalParameterObject));

				var srMenuDictionary = globalParameterObject.GlobalMenuItems[MainMenu.SendReceive];
				srMenuDictionary.Add(Command.CmdFLExBridge, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(S_R_FlexBridge_Click, () => CanDoCmdFLExBridge));
				srMenuDictionary.Add(Command.CmdViewMessages, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(ViewMessages_FlexBridge_Click, () => CanDoCmdViewMessages));
				srMenuDictionary.Add(Command.CmdObtainAnyFlexBridgeProject, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(ObtainAnyFlexBridgeProject_Click, () => CanDoCmdObtainAnyFlexBridgeProject));
				srMenuDictionary.Add(Command.CmdObtainFirstFlexBridgeProject, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(SendFlexBridgeFirstTime_Click, () => CanDoCmdObtainFirstFlexBridgeProject));
			}
			#endregion

			#region Implementation of IPropertyTableProvider
			/// <inheritdoc />
			public IPropertyTable PropertyTable { get; private set; }
			#endregion

			#region Implementation of IPublisherProvider
			/// <inheritdoc />
			public IPublisher Publisher { get; private set; }
			#endregion

			#region Implementation of ISubscriberProvider
			/// <inheritdoc />
			public ISubscriber Subscriber { get; private set; }

			#endregion

			#region IFlexComponent

			/// <inheritdoc />
			public void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
			{
				FlexComponentParameters.CheckInitializationValues(flexComponentParameters, new FlexComponentParameters(PropertyTable, Publisher, Subscriber));

				PropertyTable = flexComponentParameters.PropertyTable;
				Publisher = flexComponentParameters.Publisher;
				Subscriber = flexComponentParameters.Subscriber;

				Subscriber.Subscribe("ViewMessages", ViewMessages);
			}
			#endregion

			#region IDisposable
			private bool _isDisposed;

			~FlexBridge()
			{
				// The base class finalizer is called automatically.
				Dispose(false);
			}

			/// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
			public void Dispose()
			{
				Dispose(true);
				// This object will be cleaned up by the Dispose method.
				// Therefore, you should call GC.SuppressFinalize to
				// take this object off the finalization queue
				// and prevent finalization code for this object
				// from executing a second time.
				GC.SuppressFinalize(this);
			}

			private void Dispose(bool disposing)
			{
				Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
				if (_isDisposed)
				{
					// No need to run it more than once.
					return;
				}

				if (disposing)
				{
					Subscriber.Unsubscribe("ViewMessages", ViewMessages);
				}
				Cache = null;
				FlexApp = null;

				_isDisposed = true;
			}
			#endregion

			private void SendFlexBridgeFirstTime_Click(object sender, EventArgs e)
			{
				if (ShowMessageBeforeFirstSendReceive_IsUserReady(FlexApp))
				{
					RunBridge();
				}
			}

			private void ObtainAnyFlexBridgeProject_Click(object sender, EventArgs e)
			{
				var newprojectPathname = ObtainProjectMethod.ObtainProjectFromAnySource(PropertyTable.GetValue<Form>(FwUtilsConstants.window), out var obtainedProjectType);
				if (string.IsNullOrEmpty(newprojectPathname))
				{
					return; // We dealt with it.
				}
				PropertyTable.SetProperty(LanguageExplorerConstants.LastBridgeUsed, obtainedProjectType == ObtainedProjectType.Lift ? LanguageExplorerConstants.LiftBridge : LanguageExplorerConstants.FLExBridge, true, settingsGroup: SettingsGroup.LocalSettings);
				var fieldWorksAssembly = Assembly.Load("FieldWorks.exe");
				var fieldWorksType = fieldWorksAssembly.GetType("SIL.FieldWorks.FieldWorks");
				var methodInfo = fieldWorksType.GetMethod("OpenNewProject", BindingFlags.Static | BindingFlags.Public);
				methodInfo.Invoke(null, new object[] { new ProjectId(newprojectPathname) });
			}

			private void S_R_FlexBridge_Click(object sender, EventArgs e)
			{
				RunBridge();
			}

			private void ViewMessages_FlexBridge_Click(object sender, EventArgs e)
			{
				FLExBridgeHelper.FLExJumpUrlChanged += JumpToFlexObject;
				var success = FLExBridgeHelper.LaunchFieldworksBridge(Path.Combine(Cache.ProjectId.ProjectFolder, Cache.ProjectId.Name + LcmFileHelper.ksFwDataXmlFileExtension),
					LanguageExplorerConstants.SendReceiveUser, FLExBridgeHelper.ConflictViewer, null, LcmCache.ModelVersion, LiftModelVersion, null,
					() => BroadcastMasterRefresh(Publisher), out _, out _);
				if (!success)
				{
					ReportDuplicateBridge();
				}
				FLExBridgeHelper.FLExJumpUrlChanged -= JumpToFlexObject;
			}

			private void JumpToFlexObject(object sender, FLExJumpEventArgs e)
			{
				PublishHandleLocalHotlinkMessage(Publisher, sender, e);
			}

			/// <summary>
			/// This is only used for the main FW repo, so it excludes any notes in a lower level repo.
			/// </summary>
			private static Dictionary<string, long> PrepareToDetectMainConflicts(string projectFolder)
			{
				var result = new Dictionary<string, long>();
				foreach (var file in Directory.GetFiles(projectFolder, "*.ChorusNotes", SearchOption.AllDirectories))
				{
					if (file.Contains(LcmFileHelper.OtherRepositories))
					{
						continue; // Skip them, since they are part of some other repository.
					}
					result[file] = new FileInfo(file).Length;
				}
				return result;
			}

			// FlexBridge looks for the schema to validate Dictionary Configuration files in the project's Temp directory.
			private static TempFile CopyDictionaryConfigFileToTemp(string projectFolder)
			{
				const string dictConfigSchemaFileName = "DictionaryConfiguration.xsd";
				var dictConfigSchemaPath = Path.Combine(FwDirectoryFinder.FlexFolder, "Configuration", dictConfigSchemaFileName);
				var projectTempFolder = Path.Combine(projectFolder, "Temp");
				var dictConfigSchemaTempPath = Path.Combine(projectTempFolder, dictConfigSchemaFileName);
				if (!Directory.Exists(projectTempFolder))
				{
					Directory.CreateDirectory(projectTempFolder);
				}
				if (File.Exists(dictConfigSchemaTempPath))
				{
					// We've had difficulties in the past trying to delete this file while it's read-only. This may apply only to early testers' projects.
					File.SetAttributes(dictConfigSchemaTempPath, FileAttributes.Normal);
					File.Delete(dictConfigSchemaTempPath);
				}
				File.Copy(dictConfigSchemaPath, dictConfigSchemaTempPath);
				File.SetAttributes(dictConfigSchemaTempPath, FileAttributes.Normal);
				return new TempFile(dictConfigSchemaTempPath, true);
			}

			private void ViewMessages(object obj)
			{
				ViewMessages_FlexBridge_Click(null, null);
			}
		}

		private sealed class LiftBridge : IBridge
		{
			private readonly bool _isInFullBlownDisposeMode;
			private string _liftProjectDir;
			private string _liftPathname;
			private IProgress _progressDlg;
			/// <summary>
			/// The OldLiftBridgeProjects is populated from the mapping file if it is present.
			/// Things are never removed because that isn't important since the only purpose is to enable a menu item
			/// which will remain enabled after the project is migrated to the new flexbridge location.
			/// </summary>
			private readonly List<string> _oldLiftBridgeProjects = new List<string>();
			private LcmCache Cache { get; set; }
			private Form ParentForm { get; set; }
			private IFlexApp FlexApp { get; set; }
			/// <summary>
			/// This is the file that our Message slice is configured to look for in the root project folder.
			/// The actual Lexicon.fwstub doesn't contain anything.
			/// Lexicon.fwstub.ChorusNotes contains notes about lexical entries.
			/// </summary>
			private const string FakeLexiconFileName = "Lexicon.fwstub";
			/// <summary>
			/// This is the file that actually holds the chorus notes for the lexicon.
			/// </summary>
			private const string FlexLexiconNotesFileName = FakeLexiconFileName + LanguageExplorerConstants.kChorusNotesExtension;
			/// <summary>
			/// Get the Flex notes pathname.
			/// </summary>
			private string FlexNotesPath => Path.Combine(Cache.ProjectId.ProjectFolder, FlexLexiconNotesFileName);
			/// <summary>
			/// Get the Lift notes pathname.
			/// </summary>
			private string LiftNotesPath => _liftPathname + LanguageExplorerConstants.kChorusNotesExtension;

			internal LiftBridge(LcmCache cache, IFwMainWnd mainWindow, IFlexApp flexApp)
			{
				Guard.AgainstNull(cache, nameof(cache));
				Guard.AgainstNull(mainWindow, nameof(mainWindow));
				Guard.AgainstNull(flexApp, nameof(flexApp));

				Cache = cache;
				ParentForm = (Form)mainWindow;
				FlexApp = flexApp;
				// Set up for handling antique Lift Bridge systems, if they are still around.
				var repoMapFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LiftBridge", "LanguageProject_Repository_Map.xml");
				// look for old liftbridge repo info in path similar to C:\Users\<user>\AppData\Local\LiftBridge\LanguageProject_Repository_Map.xml
				if (File.Exists(repoMapFile))
				{
					var repoMapDoc = XDocument.Load(repoMapFile);
					var mappingNodes = repoMapDoc.Elements("Mapping");
					foreach (var mappingNode in mappingNodes)
					{
						_oldLiftBridgeProjects.Add(mappingNode.Attribute("projectguid").Value);
					}
				}
				_liftProjectDir = GetLiftRepositoryFolderFromFwProjectFolder(Cache.ProjectId.ProjectFolder);
				_liftPathname = GetLiftPathname();
				_isInFullBlownDisposeMode = true; // Help the dispose method know what to do.
			}

			/// <summary>
			/// NB: This constructor is only to be used by the static method "ImportObtainedLexicon",
			/// which is called from afar via Reflection.
			/// </summary>
			internal LiftBridge(LcmCache cache, string liftPath, Form parentForm)
			{
				Guard.AgainstNull(cache, nameof(cache));
				Guard.AgainstNullOrEmptyString(liftPath, nameof(liftPath));
				Guard.AgainstNull(parentForm, nameof(parentForm));

				Cache = cache;
				ParentForm = parentForm;
				_liftProjectDir = GetLiftRepositoryFolderFromFwProjectFolder(Cache.ProjectId.ProjectFolder);
				_liftPathname = liftPath;

				_isInFullBlownDisposeMode = false; // Help the dispose method know what to *not* do.
			}

			private Tuple<bool, bool> CanDoCmdLiftBridge => new Tuple<bool, bool>(true, _oldLiftBridgeProjects.Contains(Cache.LangProject.Guid.ToString()) && SendReceiveMenuManager.IsConfiguredForLiftSR(Cache.ProjectId.ProjectFolder));

			private Tuple<bool, bool> CanDoCmdViewLiftMessages => new Tuple<bool, bool>(true, NotesFileIsPresent(Cache, true));

			private Tuple<bool, bool> CanDoCmdObtainLiftProject => new Tuple<bool, bool>(true, !Directory.Exists(GetLiftRepositoryFolderFromFwProjectFolder(Cache.ProjectId.ProjectFolder)));

			private Tuple<bool, bool> CanDoCmdObtainFirstLiftProject => new Tuple<bool, bool>(true, !_oldLiftBridgeProjects.Contains(Cache.LangProject.Guid.ToString()) && !SendReceiveMenuManager.IsConfiguredForLiftSR(Cache.ProjectId.ProjectFolder));

			#region Implementation of IBridge
			/// <inheritdoc />
			public string Name => LanguageExplorerConstants.LiftBridge;

			/// <inheritdoc />
			public void RunBridge()
			{
				PrepareForSR(PropertyTable, Publisher, Cache, this);
				// Step 0. Try to move an extant lift repo from old location to new.
				if (!MoveOldLiftRepoIfNeeded())
				{
					return;
				}
				// Step 1. If notifier exists, re-try import (brutal or merciful, depending on contents of it).
				if (RepeatPriorFailedImportIfNeeded())
				{
					return;
				}
				// Step 2. Export lift file. If fails, then call into bridge with undo_export_lift and quit.
				if (!ExportLiftLexicon())
				{
					MessageBox.Show(ParentForm, LanguageExplorerResources.FLExBridgeListener_UndoExport_Error_exporting_LIFT, LanguageExplorerResources.FLExBridgeListener_UndoExport_LIFT_Export_failed_Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}
				// Step 3. Have Flex Bridge do the S/R.
				// after saving the state enough to detect if conflicts are created.
				var fullProjectFileName = Cache.GetFullProjectFileName();
				if (!DoSendReceiveForLift(fullProjectFileName, out var dataChanged))
				{
					// Bail out, since the S/R failed for some reason.
					return;
				}
				// Step 4. Import lift file. If fails, then add the notifier file.
				if (!DoMercilessLiftImport(dataChanged))
				{
					return;
				}
				if (!dataChanged)
				{
					return;
				}
				var liftFolder = GetLiftRepositoryFolderFromFwProjectFolder(Cache.ProjectId.ProjectFolder);
				HandlePotentialConflicts(FlexApp.FwManager, liftFolder, PrepareToDetectLiftConflicts(liftFolder), fullProjectFileName);
			}

			/// <inheritdoc />
			public void RegisterHandlers(GlobalUiWidgetParameterObject globalParameterObject)
			{
				Guard.AgainstNull(globalParameterObject, nameof(globalParameterObject));

				var srMenuDictionary = globalParameterObject.GlobalMenuItems[MainMenu.SendReceive];
				srMenuDictionary.Add(Command.CmdLiftBridge, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(S_R_LiftBridge_Click, () => CanDoCmdLiftBridge));
				srMenuDictionary.Add(Command.CmdViewLiftMessages, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(ViewMessages_LiftBridge_Click, () => CanDoCmdViewLiftMessages));
				srMenuDictionary.Add(Command.CmdObtainLiftProject, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(ObtainLiftBridgeProject_Click, () => CanDoCmdObtainLiftProject));
				srMenuDictionary.Add(Command.CmdObtainFirstLiftProject, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(SendLiftBridgeFirstTime_Click, () => CanDoCmdObtainFirstLiftProject));
			}
			#endregion

			#region Implementation of IPropertyTableProvider
			/// <inheritdoc />
			public IPropertyTable PropertyTable { get; private set; }
			#endregion

			#region Implementation of IPublisherProvider
			/// <inheritdoc />
			public IPublisher Publisher { get; private set; }
			#endregion

			#region Implementation of ISubscriberProvider
			/// <inheritdoc />
			public ISubscriber Subscriber { get; private set; }

			#endregion

			#region IFlexComponent

			/// <inheritdoc />
			public void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
			{
				FlexComponentParameters.CheckInitializationValues(flexComponentParameters, new FlexComponentParameters(PropertyTable, Publisher, Subscriber));

				PropertyTable = flexComponentParameters.PropertyTable;
				Publisher = flexComponentParameters.Publisher;
				Subscriber = flexComponentParameters.Subscriber;

				Subscriber.Subscribe("ViewLiftMessages", ViewMessages);
			}
			#endregion

			#region IDisposable
			private bool _isDisposed;

			~LiftBridge()
			{
				// The base class finalizer is called automatically.
				Dispose(false);
			}

			/// <inheritdoc />
			public void Dispose()
			{
				Dispose(true);
				// This object will be cleaned up by the Dispose method.
				// Therefore, you should call GC.SuppressFinalize to
				// take this object off the finalization queue
				// and prevent finalization code for this object
				// from executing a second time.
				GC.SuppressFinalize(this);
			}

			private void Dispose(bool disposing)
			{
				Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
				if (_isDisposed)
				{
					// No need to run it more than once.
					return;
				}

				if (disposing)
				{
					if (_isInFullBlownDisposeMode)
					{
						Subscriber.Unsubscribe("ViewLiftMessages", ViewMessages);
						_oldLiftBridgeProjects.Clear();
					}
				}
				_liftProjectDir = null;
				_liftPathname = null;
				_progressDlg = null;
				Cache = null;
				ParentForm = null;
				FlexApp = null;

				_isDisposed = true;
			}
			#endregion

			private static void HandlePotentialConflicts(IFieldWorksManager manager, string liftFolder, IReadOnlyDictionary<string, long> savedState, string fullProjectFileName)
			{
				var detectedLiftConflicts = false;
				foreach (var file in Directory.GetFiles(liftFolder, "*.ChorusNotes", SearchOption.AllDirectories))
				{
					savedState.TryGetValue(file, out var oldLength);
					if (new FileInfo(file).Length == oldLength)
					{
						continue; // no new notes in this file.
					}
					detectedLiftConflicts = true; // Review JohnT: do we need to look in the file to see if what was added is a conflict?
				}
				if (!detectedLiftConflicts)
				{
					return;
				}
				RefreshCacheWindowAndAll(manager, fullProjectFileName, "ViewLiftMessages", true);
			}

			private void SendLiftBridgeFirstTime_Click(object sender, EventArgs e)
			{
				if (ShowMessageBeforeFirstSendReceive_IsUserReady(FlexApp))
				{
					RunBridge();
				}
			}

			private void ObtainLiftBridgeProject_Click(object sender, EventArgs e)
			{
				if (Directory.Exists(GetLiftRepositoryFolderFromFwProjectFolder(Cache.ProjectId.ProjectFolder)))
				{
					MessageBox.Show(ParentForm, LanguageExplorerResources.kProjectAlreadyHasLiftRepo, LanguageExplorerResources.kCannotDoGetAndMergeAgain, MessageBoxButtons.OK);
					return;
				}
				StopParser(Publisher);
				var success = FLExBridgeHelper.LaunchFieldworksBridge(Cache.ProjectId.ProjectFolder, null, FLExBridgeHelper.ObtainLift, null,
					LcmCache.ModelVersion, LiftModelVersion, null, null, out _, out _liftPathname);
				if (!success || string.IsNullOrEmpty(_liftPathname))
				{
					_liftPathname = null;
					return;
				}
				// Do merciful import.
				ImportLiftCommon(MergeStyle.MsKeepBoth);
				PropertyTable.SetProperty(LanguageExplorerConstants.LastBridgeUsed, LanguageExplorerConstants.LiftBridge, true, settingsGroup: SettingsGroup.LocalSettings);
				Publisher.Publish(new PublisherParameterObject(LanguageExplorerConstants.MasterRefresh));
			}

			private void ViewMessages(object obj)
			{
				ViewMessages_LiftBridge_Click(null, null);
			}

			private void S_R_LiftBridge_Click(object sender, EventArgs e)
			{
				RunBridge();
			}

			private void ViewMessages_LiftBridge_Click(object sender, EventArgs e)
			{
				FLExBridgeHelper.FLExJumpUrlChanged += JumpToFlexObject;
				var success = FLExBridgeHelper.LaunchFieldworksBridge(Cache.GetFullProjectFileName(), LanguageExplorerConstants.SendReceiveUser, FLExBridgeHelper.LiftConflictViewer,
					null, LcmCache.ModelVersion, LiftModelVersion, null, () => BroadcastMasterRefresh(Publisher), out _, out _);
				if (!success)
				{
					ReportDuplicateBridge();
				}
				FLExBridgeHelper.FLExJumpUrlChanged -= JumpToFlexObject;
			}

			private void JumpToFlexObject(object sender, FLExJumpEventArgs e)
			{
				PublishHandleLocalHotlinkMessage(Publisher, sender, e);
			}

			/// <summary>
			/// If the repo exists in the foo\OtherRepositories\LIFT folder, then do nothing.
			/// If the repo or the entire folder structure does not yet exist,
			/// then ask FLEx Bridge to move the previous lift repo to the new home,
			/// it is exists.
			/// </summary>
			/// <remarks>
			/// <para>If the call to FLEx Bridge returns the pathname to the lift file (_liftPathname), we know the move took place,
			/// and we have the lift file that is in the repository. That lift file's name may or may not match the FW project name,
			/// but it ought not matter if it does or does not match.</para>
			/// <para>If the call returned null, we know the move did not take place.
			/// In this case the caller of this method will continue on and probably create a new repository,
			///	thus doing the equivalent of the original Lift Bridge code where there FLEx user started a S/R lift system.</para>
			/// </remarks>
			/// <returns>'true' if the move succeeded, or if there was no need to do the move. The caller code will continue its work.
			/// Return 'false', if the calling code should quit its work.</returns>
			private bool MoveOldLiftRepoIfNeeded()
			{
				var liftProjectDir = GetLiftRepositoryFolderFromFwProjectFolder(Cache.ProjectId.ProjectFolder);
				// It is fine to try the repo move if the liftProjectDir exists, but *only* if it is completely empty.
				// Mercurial can't do a clone into a folder that has contents of any sort.
				if (Directory.Exists(liftProjectDir) && (Directory.GetDirectories(liftProjectDir).Length > 0 || Directory.GetFiles(liftProjectDir).Length > 0))
				{
					return true;
				}
				// flexbridge -p <path to fwdata file> -u <username> -v move_lift -g Langprojguid
				var success = FLExBridgeHelper.LaunchFieldworksBridge(Cache.GetFullProjectFileName(), LanguageExplorerConstants.SendReceiveUser, FLExBridgeHelper.MoveLift,
					Cache.LanguageProject.Guid.ToString().ToLowerInvariant(), LcmCache.ModelVersion, "0.13", null, null, out _, out _liftPathname); // _liftPathname will be null, if no repo was moved.
				if (!success)
				{
					ReportDuplicateBridge();
					_liftPathname = null;
					return false;
				}
				return true;
			}

			/// <summary>
			/// Reregisters an import failure, if needed, otherwise clears the token.
			/// </summary>
			/// <returns>'true' if the import failure continues, otherwise 'false'.</returns>
			private bool RepeatPriorFailedImportIfNeeded()
			{
				if (!Directory.Exists(_liftProjectDir))
				{
					return false;
				}
				if (_liftPathname == null)
				{
					return false;
				}
				var previousImportStatus = LiftImportFailureServices.GetFailureStatus(_liftProjectDir);
				switch (previousImportStatus)
				{
					case ImportFailureStatus.BasicImportNeeded:
						return !ImportLiftCommon(MergeStyle.MsKeepBoth);
					case ImportFailureStatus.StandardImportNeeded:
						return !ImportLiftCommon(MergeStyle.MsKeepOnlyNew);
					case ImportFailureStatus.NoImportNeeded:
						// Nothing to do. :-)
						break;
				}
				return false;
			}

			private string GetLiftPathname()
			{
				// Part 2 of the LT-14809 fix is to test for the existence of the lift folder.
				// FB will delete it if the S/R was cancelled and Flex had just created the lift folder and file.
				// So don't crash if the folder no longer exists.
				return Directory.Exists(_liftProjectDir) ? Directory.GetFiles(_liftProjectDir, "*.lift").FirstOrDefault() : null;
			}

			/// <summary>
			/// Import the lift file using the given MergeStyle:
			///		FlexLiftMerger.MergeStyle.MsKeepNew (aka 'merciful', in that all entries from lift file and those in FLEx are retained)
			///		FlexLiftMerger.MergeStyle.MsKeepOnlyNew (aka 'merciless',
			///			in that the Flex lexicon ends up with the same entries as in the lift file, even if some need to be deleted in FLEx.)
			/// </summary>
			/// <param name="mergeStyle">FlexLiftMerger.MergeStyle.MsKeepNew or FlexLiftMerger.MergeStyle.MsKeepOnlyNew</param>
			/// <returns>'true' if the import succeeded, otherwise 'false'.</returns>
			internal bool ImportLiftCommon(MergeStyle mergeStyle)
			{
				using (new WaitCursor(ParentForm))
				using (var progressDlg = new ProgressDialogWithTask(ParentForm))
				{
					_progressDlg = progressDlg;
					try
					{
						if (mergeStyle == MergeStyle.MsKeepBoth)
						{
							LiftImportFailureServices.RegisterBasicImportFailure(Path.GetDirectoryName(_liftPathname));
						}
						else
						{
							LiftImportFailureServices.RegisterStandardImportFailure(Path.GetDirectoryName(_liftPathname));
						}
						progressDlg.Title = ResourceHelper.GetResourceString("kstidImportLiftlexicon");
						var logFile = (string)progressDlg.RunTask(true, ImportLiftLexicon, _liftPathname, mergeStyle);
						if (logFile != null)
						{
							LiftImportFailureServices.ClearImportFailure(Path.GetDirectoryName(_liftPathname));
							return true;
						}
						LiftImportFailureServices.DisplayLiftFailureNoticeIfNecessary(ParentForm, _liftPathname);
						return false;
					}
					catch (WorkerThreadException error)
					{
						// It appears to be an analyst issue to sort out how we should report this.
						// LT-12340 however says we must report it somehow.
						var sMsg = string.Format(LanguageExplorerResources.kProblemImportWhileMerging, _liftPathname, error.InnerException.Message);
						MessageBoxUtils.Show(sMsg, LanguageExplorerResources.kProblemMerging, MessageBoxButtons.OK, MessageBoxIcon.Warning);
						return false;
					}
					finally
					{
						_progressDlg = null;
					}
				}
			}

			/// <summary>
			/// Import the LIFT file into FieldWorks.
			/// </summary>
			/// <returns>the name of the exported LIFT file if successful, or null if an error occurs.</returns>
			/// <remarks>
			/// This method is called in a thread, during the export process.
			/// </remarks>
			private object ImportLiftLexicon(IProgress progressDialog, params object[] parameters)
			{
				var liftPathname = parameters[0].ToString();
				var mergeStyle = (MergeStyle)parameters[1];
				// If we use true while importing changes from repo it will fail to copy any pix/aud files that have changed.
				var fTrustModTimes = mergeStyle != MergeStyle.MsKeepOnlyNew;
				if (_progressDlg == null)
				{
					_progressDlg = progressDialog;
				}
				progressDialog.Minimum = 0;
				progressDialog.Maximum = 100;
				progressDialog.Position = 0;
				string sLogFile = null;
				if (File.Exists(LiftNotesPath))
				{

					using (var reader = new StreamReader(LiftNotesPath, Encoding.UTF8))
					using (var writer = new StreamWriter(FlexNotesPath, false, Encoding.UTF8))
					{
						ConvertLiftNotesToFlex(reader, writer);
					}
				}
				NonUndoableUnitOfWorkHelper.Do(Cache.ActionHandlerAccessor, () =>
				{
					string sFilename;
					var fMigrationNeeded = Migrator.IsMigrationNeeded(liftPathname);
					if (fMigrationNeeded)
					{
						var sOldVersion = Validator.GetLiftVersion(liftPathname);
						progressDialog.Message = string.Format(ResourceHelper.GetResourceString("kstidLiftVersionMigration"), sOldVersion, Validator.LiftVersion);
						sFilename = Migrator.MigrateToLatestVersion(liftPathname);
					}
					else
					{
						sFilename = liftPathname;
					}
					progressDialog.Message = ResourceHelper.GetResourceString("kstidLoadingListInfo");
					var flexImporter = new FlexLiftMerger(Cache, mergeStyle, fTrustModTimes);
					var parser = new LiftParser<LiftObject, CmLiftEntry, CmLiftSense, CmLiftExample>(flexImporter);
					parser.SetTotalNumberSteps += ParserSetTotalNumberSteps;
					parser.SetStepsCompleted += ParserSetStepsCompleted;
					parser.SetProgressMessage += ParserSetProgressMessage;
					flexImporter.LiftFile = liftPathname;
					flexImporter.LoadLiftRanges(liftPathname + "-ranges");
					var cEntries = parser.ReadLiftFile(sFilename);
					if (fMigrationNeeded)
					{
						// Try to move the migrated file to the temp directory, even if a copy of it
						// already exists there.
						var sTempMigrated = Path.Combine(Path.GetTempPath(), Path.ChangeExtension(Path.GetFileName(sFilename), "." + Validator.LiftVersion + ".lift"));
						if (File.Exists(sTempMigrated))
						{
							File.Delete(sTempMigrated);
						}
						File.Move(sFilename, sTempMigrated);
					}
					progressDialog.Message = ResourceHelper.GetResourceString("kstidFixingRelationLinks");
					flexImporter.ProcessPendingRelations(progressDialog);
					sLogFile = flexImporter.DisplayNewListItems(liftPathname, cEntries);
				});
				return sLogFile;
			}

			private void ParserSetTotalNumberSteps(object sender, LiftParser<LiftObject, CmLiftEntry, CmLiftSense, CmLiftExample>.StepsArgs e)
			{
				_progressDlg.Maximum = e.Steps;
				_progressDlg.Position = 0;
			}

			private void ParserSetProgressMessage(object sender, LiftParser<LiftObject, CmLiftEntry, CmLiftSense, CmLiftExample>.MessageArgs e)
			{
				_progressDlg.Position = 0;
				_progressDlg.Message = e.Message;
			}

			private void ParserSetStepsCompleted(object sender, LiftParser<LiftObject, CmLiftEntry, CmLiftSense, CmLiftExample>.ProgressEventArgs e)
			{
				var nMax = _progressDlg.Maximum;
				_progressDlg.Position = e.Progress > nMax ? e.Progress % nMax : e.Progress;
			}

			/// <summary>
			/// Export the FieldWorks lexicon into the LIFT file.
			/// The file may, or may not, exist.
			/// </summary>
			/// <returns>True, if the import successful, otherwise false.</returns>
			/// <remarks>
			/// This method calls an overloaded ExportLiftLexicon, which is run in a thread.
			/// </remarks>
			private bool ExportLiftLexicon()
			{
				using (new WaitCursor(ParentForm))
				using (var progressDlg = new ProgressDialogWithTask(ParentForm))
				{
					_progressDlg = progressDlg;
					try
					{
						progressDlg.Title = ResourceHelper.GetResourceString("kstidExportLiftLexicon");
						var outPath = (string)progressDlg.RunTask(true, ExportLiftLexicon, null);
						var retval = (!string.IsNullOrEmpty(outPath));
						if (!retval && CanUndoLiftExport)
						{
							UndoExport();
						}
						return retval;
					}
					catch
					{
						if (CanUndoLiftExport)
						{
							UndoExport();
						}
						return false;
					}
					finally
					{
						_progressDlg = null;
					}
				}
			}

			private bool CanUndoLiftExport => Directory.Exists(_liftProjectDir) && Directory.Exists(Path.Combine(_liftProjectDir, ".hg"));

			private void UndoExport()
			{
				// Have FLEx Bridge do its 'undo'
				// flexbridge -p <project folder name> #-u username -v undo_export_lift)
				FLExBridgeHelper.LaunchFieldworksBridge(Cache.ProjectId.ProjectFolder, LanguageExplorerConstants.SendReceiveUser, FLExBridgeHelper.UndoExportLift, null, LcmCache.ModelVersion,
					LiftModelVersion, null, null, out _, out _);
			}

			/// <summary>
			/// Export the contents of the lift lexicon.
			/// </summary>
			/// <param name="progressDialog"></param>
			/// <param name="parameters">parameters are not used in this method. This method is called by an invoker,
			/// which requires this signature.</param>
			/// <returns>the name of the exported LIFT file if successful, or null if an error occurs.</returns>
			/// <remarks>
			/// This method is called in a thread, during the export process.
			/// </remarks>
			private object ExportLiftLexicon(IProgress progressDialog, params object[] parameters)
			{
				try
				{
					if (!Directory.Exists(_liftProjectDir))
					{
						Directory.CreateDirectory(_liftProjectDir);
					}
					if (string.IsNullOrEmpty(_liftPathname))
					{
						_liftPathname = Path.Combine(_liftProjectDir, Cache.ProjectId.Name + ".lift");
					}
					progressDialog.Message = string.Format(ResourceHelper.GetResourceString("kstidExportingEntries"), Cache.LangProject.LexDbOA.Entries.Count());
					progressDialog.Minimum = 0;
					progressDialog.Maximum = Cache.ServiceLocator.GetInstance<ILexEntryRepository>().Count;
					progressDialog.Position = 0;
					progressDialog.AllowCancel = false;
					var exporter = new LiftExporter(Cache);
					exporter.UpdateProgress += OnDumperUpdateProgress;
					exporter.SetProgressMessage += OnDumperSetProgressMessage;
					exporter.ExportPicturesAndMedia = true;
					using (TextWriter textWriter = new StreamWriter(_liftPathname))
					{
						exporter.ExportLift(textWriter, Path.GetDirectoryName(_liftPathname));
					}
					LiftSorter.SortLiftFile(_liftPathname);
					//Output the Ranges file
					var outPathRanges = Path.ChangeExtension(_liftPathname, "lift-ranges");
					using (var stringWriter = new StringWriter(new StringBuilder()))
					{
						exporter.ExportLiftRanges(stringWriter);
						File.WriteAllText(outPathRanges, stringWriter.ToString());
					}
					LiftSorter.SortLiftRangesFiles(outPathRanges);
					if (File.Exists(FlexNotesPath))
					{

						using (var reader = new StreamReader(FlexNotesPath, Encoding.UTF8))
						using (var writer = new StreamWriter(LiftNotesPath, false, Encoding.UTF8))
						{
							ConvertFlexNotesToLift(reader, writer, Path.GetFileName(_liftPathname));
						}
					}
					return _liftPathname;
				}
				catch
				{
					_liftPathname = null;
					return _liftPathname;
				}
			}

			private void OnDumperSetProgressMessage(object sender, ProgressMessageArgs e)
			{
				if (_progressDlg == null)
				{
					return;
				}
				var message = ResourceHelper.GetResourceString(e.MessageId);
				if (!string.IsNullOrEmpty(message))
				{
					_progressDlg.Message = message;
				}
				_progressDlg.Minimum = 0;
				_progressDlg.Maximum = e.Max;
			}

			private void OnDumperUpdateProgress(object sender)
			{
				if (_progressDlg == null)
				{
					return;
				}
				var nMax = _progressDlg.Maximum;
				if (_progressDlg.Position >= nMax)
				{
					_progressDlg.Position = 0;
				}
				_progressDlg.Step(1);
				if (_progressDlg.Position > nMax)
				{
					_progressDlg.Position = _progressDlg.Position % nMax;
				}
			}

			private bool DoMercilessLiftImport(bool dataChanged)
			{
				if (!dataChanged)
				{
					return true;
				}
				if (!ImportLiftCommon(MergeStyle.MsKeepOnlyNew))
				{
					return false;
				}
				var liftFolder = GetLiftRepositoryFolderFromFwProjectFolder(Cache.ProjectId.ProjectFolder);
				HandlePotentialConflicts(FlexApp.FwManager, liftFolder, PrepareToDetectLiftConflicts(liftFolder), Cache.GetFullProjectFileName());
				return true;
			}

			/// <summary>
			/// Do the S/R. This *may* actually create the Lift repository, if it doesn't exist, or it may do a more normal S/R
			/// </summary>
			/// <returns>'true' if the S/R succeed, otherwise 'false'.</returns>
			private bool DoSendReceiveForLift(string fullProjectFileName, out bool dataChanged)
			{
				if (!Directory.Exists(_liftProjectDir))
				{
					Directory.CreateDirectory(_liftProjectDir);
				}
				_liftPathname = GetLiftPathname();
				PrepareToDetectLiftConflicts(_liftPathname);
				// flexbridge -p <path to fwdata/fwdb file> -u <username> -v send_receive_lift
				var success = FLExBridgeHelper.LaunchFieldworksBridge(fullProjectFileName, LanguageExplorerConstants.SendReceiveUser,
					FLExBridgeHelper.SendReceiveLift, // May create a new lift repo in the process of doing the S/R. Or, it may just use the extant lift repo.
					null, LcmCache.ModelVersion, "0.13", Cache.LangProject.DefaultVernacularWritingSystem.Id, null, out dataChanged, out _);
				if (!success)
				{
					ReportDuplicateBridge();
					dataChanged = false;
					_liftPathname = null;
					return false;
				}
				_liftPathname = GetLiftPathname();
				if (_liftPathname == null)
				{
					dataChanged = false; // If there is no lift file, there cannot be any new data.
					return false;
				}
				return true;
			}

			/// <summary>
			/// This is only used for the Lift repo folder.
			/// </summary>
			private static Dictionary<string, long> PrepareToDetectLiftConflicts(string liftPath)
			{
				var result = new Dictionary<string, long>();
				if (!Directory.Exists(liftPath))
				{
					return result;
				}
				foreach (var file in Directory.GetFiles(liftPath, "*.ChorusNotes", SearchOption.AllDirectories))
				{
					result[file] = new FileInfo(file).Length;
				}
				return result;
			}
		}
	}
}