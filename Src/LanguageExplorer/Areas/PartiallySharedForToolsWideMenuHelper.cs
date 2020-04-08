// Copyright (c) 2017-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using LanguageExplorer.Controls;
using LanguageExplorer.Controls.DetailControls;
using SIL.Code;
using SIL.Collections;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;

namespace LanguageExplorer.Areas
{
	/// <summary>
	/// Provides menu adjustments for areas/tools that cross those boundaries, and that areas/tools can be more selective in what to use.
	/// One might think of these as more 'global', but not quite to the level of 'universal' across all areas/tools,
	/// which events are handled by the main window.
	/// </summary>
	internal sealed class PartiallySharedForToolsWideMenuHelper : IDisposable
	{
		private MajorFlexComponentParameters _majorFlexComponentParameters;
		private IRecordList _recordList;
		private ISharedEventHandlers _sharedEventHandlers;
		private readonly HashSet<Command> _sharedEventKeyNames = new HashSet<Command>();
		private readonly HashSet<Command> _eventuallySharedCommands = new HashSet<Command>();
		private static PartiallySharedForToolsWideMenuHelper s_partiallySharedForToolsWideMenuHelper;

		internal PartiallySharedForToolsWideMenuHelper(MajorFlexComponentParameters majorFlexComponentParameters, IRecordList recordList)
		{
			Guard.AgainstNull(majorFlexComponentParameters, nameof(majorFlexComponentParameters));
			Guard.AgainstNull(recordList, nameof(recordList));
			Require.That(s_partiallySharedForToolsWideMenuHelper == null, "Static member 's_partiallySharedForToolsWideMenuHelper' is not null.");

			_majorFlexComponentParameters = majorFlexComponentParameters;
			_recordList = recordList;

			_sharedEventHandlers = _majorFlexComponentParameters.SharedEventHandlers;
			PropertyTable = _majorFlexComponentParameters.FlexComponentParameters.PropertyTable;
			Publisher = _majorFlexComponentParameters.FlexComponentParameters.Publisher;
			Subscriber = _majorFlexComponentParameters.FlexComponentParameters.Subscriber;

			_sharedEventKeyNames.AddRange( new[]
			{
				Command.CmdJumpToTool,
				Command.CmdDataTreeDelete,
				Command.CmdDeleteSelectedBrowseViewObject
			});
			foreach (var key in _sharedEventKeyNames)
			{
				switch (key)
				{
					case Command.CmdJumpToTool:
						_sharedEventHandlers.Add(key, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(JumpToTool_Clicked, () => UiWidgetServices.CanSeeAndDo));
						break;
					case Command.CmdDataTreeDelete:
						_sharedEventHandlers.Add(key, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(DataTreeDelete_Clicked, () => UiWidgetServices.CanSeeAndDo));
						break;
					case Command.CmdDeleteSelectedBrowseViewObject:
						_sharedEventHandlers.Add(key, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(DeleteSelectedBrowseViewObject_Clicked, () => UiWidgetServices.CanSeeAndDo));
						break;
				}
			}
			s_partiallySharedForToolsWideMenuHelper = this;
		}

		/// <summary>
		/// Placement in the IPropertyTableProvider interface lets FwApp call IPropertyTable.DoStuff.
		/// </summary>
		private IPropertyTable PropertyTable { get; }

		/// <summary>
		/// Get the IPublisher.
		/// </summary>
		private IPublisher Publisher { get; }

		/// <summary>
		/// Get the ISubscriber.
		/// </summary>
		private ISubscriber Subscriber { get; }

		#region IDisposable
		private bool _isDisposed;

		~PartiallySharedForToolsWideMenuHelper()
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
				s_partiallySharedForToolsWideMenuHelper = null;
				foreach (var key in _sharedEventKeyNames)
				{
					_sharedEventHandlers.Remove(key);
				}
				_sharedEventKeyNames.Clear();
				foreach (var command in _eventuallySharedCommands)
				{
					_sharedEventHandlers.Remove(command);
				}
				_eventuallySharedCommands.Clear();
			}
			_majorFlexComponentParameters = null;
			_recordList = null;
			_sharedEventHandlers = null;

			_isDisposed = true;
		}
		#endregion

		private static void DataTreeDelete_Clicked(object sender, EventArgs e)
		{
			HandleDeletion(sender);
		}

		private static void DeleteSelectedBrowseViewObject_Clicked(object sender, EventArgs e)
		{
			var tag = (IList<object>)((ToolStripMenuItem)sender).Tag;
			((IRecordList)tag[0]).DeleteRecord((string)tag[1], (StatusBarProgressPanel)tag[2]);
		}

		private static void HandleDeletion(object sender)
		{
			((ISlice)((ToolStripMenuItem)sender).Tag).HandleDeleteCommand();
		}

		private void Insert_Slash_Clicked(object sender, EventArgs e)
		{
			UowHelpers.UndoExtension(AreaResources.ksInsertEnvironmentSlash, PropertyTable.GetValue<LcmCache>(FwUtils.cache).ActionHandlerAccessor, () => SenderTagAsIPhEnvSliceCommon(sender).InsertSlash());
		}

		private void Insert_Underscore_Clicked(object sender, EventArgs e)
		{
			UowHelpers.UndoExtension(AreaResources.ksInsertEnvironmentBar, PropertyTable.GetValue<LcmCache>(FwUtils.cache).ActionHandlerAccessor, () => SenderTagAsIPhEnvSliceCommon(sender).InsertEnvironmentBar());
		}

		private void Insert_NaturalClass_Clicked(object sender, EventArgs e)
		{
			UowHelpers.UndoExtension(AreaResources.ksInsertNaturalClass, PropertyTable.GetValue<LcmCache>(FwUtils.cache).ActionHandlerAccessor, () => SenderTagAsIPhEnvSliceCommon(sender).InsertNaturalClass());
		}

		private void Insert_OptionalItem_Clicked(object sender, EventArgs e)
		{
			UowHelpers.UndoExtension(AreaResources.ksInsertOptionalItem, PropertyTable.GetValue<LcmCache>(FwUtils.cache).ActionHandlerAccessor, () => SenderTagAsIPhEnvSliceCommon(sender).InsertOptionalItem());
		}

		private void Insert_HashMark_Clicked(object sender, EventArgs e)
		{
			UowHelpers.UndoExtension(AreaResources.ksInsertWordBoundary, PropertyTable.GetValue<LcmCache>(FwUtils.cache).ActionHandlerAccessor, () => SenderTagAsIPhEnvSliceCommon(sender).InsertHashMark());
		}

		private static void ShowEnvironmentError_Clicked(object sender, EventArgs e)
		{
			SenderTagAsIPhEnvSliceCommon(sender).ShowEnvironmentError();
		}

		private static IPhEnvSliceCommon SenderTagAsIPhEnvSliceCommon(object sender)
		{
			return (IPhEnvSliceCommon)((ToolStripMenuItem)sender).Tag;
		}

		internal static void CreateShowEnvironmentErrorMessageContextMenuStripMenus(ISlice slice, List<Tuple<ToolStripMenuItem, EventHandler>> menuItems, ContextMenuStrip contextMenuStrip)
		{
			/*
		      <item command="CmdShowEnvironmentErrorMessage" />
					<command id="CmdShowEnvironmentErrorMessage" label="_Describe Error in Environment" message="ShowEnvironmentError" /> SHARED
			*/
			var menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, ShowEnvironmentError_Clicked, LanguageExplorerResources.Describe_Error_in_Environment);
			menu.Enabled = ((IPhEnvSliceCommon)slice).CanShowEnvironmentError;
			menu.Tag = slice;
		}

		internal static void CreateCommonEnvironmentContextMenuStripMenus(ISlice slice, List<Tuple<ToolStripMenuItem, EventHandler>> menuItems, ContextMenuStrip contextMenuStrip)
		{
			if (contextMenuStrip.Items.Count > 0)
			{
				/*
				  <item label="-" translate="do not translate" />
				*/
				ToolStripMenuItemFactory.CreateToolStripSeparatorForContextMenuStrip(contextMenuStrip);
			}

			/*
		      <item command="CmdInsertEnvSlash" />
					<command id="CmdInsertEnvSlash" label="Insert Environment _slash" message="InsertSlash" /> SHARED
			*/
			var sliceAsIPhEnvSliceCommon = ((IPhEnvSliceCommon)slice);
			var menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, s_partiallySharedForToolsWideMenuHelper.Insert_Slash_Clicked, AreaResources.Insert_Environment_slash);
			menu.Enabled = sliceAsIPhEnvSliceCommon.CanInsertSlash;
			menu.Tag = slice;

			/*
		      <item command="CmdInsertEnvUnderscore" />
					<command id="CmdInsertEnvUnderscore" label="Insert Environment _bar" message="InsertEnvironmentBar" /> SHARED
			*/

			menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, s_partiallySharedForToolsWideMenuHelper.Insert_Underscore_Clicked, AreaResources.Insert_Environment_bar);
			menu.Enabled = sliceAsIPhEnvSliceCommon.CanInsertEnvironmentBar;
			menu.Tag = slice;

			/*
		      <item command="CmdInsertEnvNaturalClass" />
					<command id="CmdInsertEnvNaturalClass" label="Insert _Natural Class" message="InsertNaturalClass" /> SHARED
			*/
			menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, s_partiallySharedForToolsWideMenuHelper.Insert_NaturalClass_Clicked, AreaResources.Insert_Natural_Class);
			menu.Enabled = sliceAsIPhEnvSliceCommon.CanInsertNaturalClass;
			menu.Tag = slice;

			/*
		      <item command="CmdInsertEnvOptionalItem" />
					<command id="CmdInsertEnvOptionalItem" label="Insert _Optional Item" message="InsertOptionalItem" /> SHARED
			*/
			menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, s_partiallySharedForToolsWideMenuHelper.Insert_OptionalItem_Clicked, AreaResources.Insert_Optional_Item);
			menu.Enabled = sliceAsIPhEnvSliceCommon.CanInsertOptionalItem;
			menu.Tag = slice;

			/*
		      <item command="CmdInsertEnvHashMark" />
					<command id="CmdInsertEnvHashMark" label="Insert _Word Boundary" message="InsertHashMark" /> SHARED
			*/
			menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, s_partiallySharedForToolsWideMenuHelper.Insert_HashMark_Clicked, AreaResources.Insert_Word_Boundary);
			menu.Enabled = sliceAsIPhEnvSliceCommon.CanInsertHashMark;
			menu.Tag = slice;
		}

		private static void JumpToTool_Clicked(object sender, EventArgs e)
		{
			var tagList = (List<object>)((ToolStripMenuItem)sender).Tag;
			Guid jumpToGuid;
			var guidSupplier = tagList[2];
			if (guidSupplier is Guid guid)
			{
				jumpToGuid = guid;
			}
			else if (guidSupplier is IRecordList list)
			{
				jumpToGuid = list.CurrentObject.Guid;
			}
			else if (guidSupplier is DataTree dataTree)
			{
				jumpToGuid = dataTree.CurrentSlice.MyCmObject.Guid;
			}
			else if (guidSupplier is ICmObject cmObject)
			{
				jumpToGuid = cmObject.Guid;
			}
			else
			{
				MessageBox.Show($"Deal with type of '{guidSupplier.GetType().Name}' in shared 'JumpToTool_Clicked' event handler!");
				throw new ArgumentException("Who is it?");
			}
			LinkHandler.PublishFollowLinkMessage((IPublisher)tagList[0], new FwLinkArgs((string)tagList[1], jumpToGuid));
		}

		internal void SetupCmdInsertPossibility(ToolUiWidgetParameterObject toolUiWidgetParameterObject, Func<Tuple<bool, bool>> seeAndDo)
		{
			_eventuallySharedCommands.Add(Command.CmdInsertPossibility);
			_sharedEventHandlers.Add(Command.CmdInsertPossibility, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CmdInsertPOS_Click, seeAndDo));
			var insertMenuDictionary = toolUiWidgetParameterObject.MenuItemsForTool[MainMenu.Insert];
			var insertToolbarDictionary = toolUiWidgetParameterObject.ToolBarItemsForTool[ToolBar.Insert];
			insertMenuDictionary.Add(Command.CmdInsertPossibility, _sharedEventHandlers.Get(Command.CmdInsertPossibility));
			insertToolbarDictionary.Add(Command.CmdInsertPossibility, _sharedEventHandlers.Get(Command.CmdInsertPossibility));
			AreaServices.ResetMainPossibilityInsertUiWidgetsText(_majorFlexComponentParameters.UiWidgetController, AreaResources.PartOfSpeech, AreaResources.Add_a_new_category);
		}

		private void CmdInsertPOS_Click(object sender, EventArgs e)
		{
			// Insert in main list.
			InsertPossibility();
		}

		internal void SetupCmdDataTree_Insert_Possibility(ToolUiWidgetParameterObject toolUiWidgetParameterObject, Func<Tuple<bool, bool>> seeAndDo)
		{
			_eventuallySharedCommands.Add(Command.CmdDataTree_Insert_Possibility);
			_sharedEventHandlers.Add(Command.CmdDataTree_Insert_Possibility, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CmdDataTree_Insert_POS_SubPossibilities_Click, seeAndDo));
			var insertMenuDictionary = toolUiWidgetParameterObject.MenuItemsForTool[MainMenu.Insert];
			var insertToolbarDictionary = toolUiWidgetParameterObject.ToolBarItemsForTool[ToolBar.Insert];
			insertMenuDictionary.Add(Command.CmdDataTree_Insert_Possibility, _sharedEventHandlers.Get(Command.CmdDataTree_Insert_Possibility));
			insertToolbarDictionary.Add(Command.CmdDataTree_Insert_Possibility, _sharedEventHandlers.Get(Command.CmdDataTree_Insert_Possibility));
			AreaServices.ResetSubitemPossibilityInsertUiWidgetsText(_majorFlexComponentParameters.UiWidgetController, AreaResources.Subcategory);
		}

		private void CmdDataTree_Insert_POS_SubPossibilities_Click(object sender, EventArgs e)
		{
			InsertPossibility(_recordList.CurrentObject as IPartOfSpeech);
		}

		private void InsertPossibility(IPartOfSpeech selectedCategoryOwner = null)
		{
			IPartOfSpeech newPossibility;
			using (var dlg = new MasterCategoryListDlg())
			{
				var propertyTable = _majorFlexComponentParameters.FlexComponentParameters.PropertyTable;
				dlg.SetDlginfo(_majorFlexComponentParameters.LcmCache.LanguageProject.PartsOfSpeechOA, propertyTable, true, selectedCategoryOwner);
				dlg.ShowDialog(propertyTable.GetValue<Form>(FwUtils.window));
				newPossibility = dlg.SelectedPOS;
			}
			if (newPossibility != null)
			{
				_recordList.UpdateRecordTreeBar();
			}
		}

		internal void SetupCmdAddToLexicon(ToolUiWidgetParameterObject toolUiWidgetParameterObject, DataTree dataTree, Func<Tuple<bool, bool>> seeAndDo)
		{
			_eventuallySharedCommands.Add(Command.CmdAddToLexicon);
			_sharedEventHandlers.Add(Command.CmdAddToLexicon, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CmdAddToLexicon_Clicked, seeAndDo));
			// CmdAddToLexicon goes on Insert menu & Insert toolbar
			toolUiWidgetParameterObject.MenuItemsForTool[MainMenu.Insert].Add(Command.CmdAddToLexicon, _sharedEventHandlers.Get(Command.CmdAddToLexicon));
			_majorFlexComponentParameters.UiWidgetController.InsertMenuDictionary[Command.CmdAddToLexicon].Tag = dataTree;
			toolUiWidgetParameterObject.ToolBarItemsForTool[ToolBar.Insert].Add(Command.CmdAddToLexicon, _sharedEventHandlers.Get(Command.CmdAddToLexicon));
			_majorFlexComponentParameters.UiWidgetController.InsertToolBarDictionary[Command.CmdAddToLexicon].Tag = dataTree;
		}

		private void CmdAddToLexicon_Clicked(object sender, EventArgs e)
		{
			LanguageExplorerServices.AddToLexicon(_majorFlexComponentParameters.LcmCache, new FlexComponentParameters(PropertyTable, Publisher, Subscriber), ((DataTree)((ToolStripItem)sender).Tag).CurrentSliceAsStTextSlice.RootSite);
		}

		internal void SetupCmdLexiconLookup(ToolUiWidgetParameterObject toolUiWidgetParameterObject, DataTree dataTree, Func<Tuple<bool, bool>> seeAndDo)
		{
			_eventuallySharedCommands.Add(Command.CmdLexiconLookup);
			var tuple = new Tuple<EventHandler, Func<Tuple<bool, bool>>>(LexiconLookup_Clicked, seeAndDo);
			_sharedEventHandlers.Add(Command.CmdLexiconLookup, tuple);
			// CmdLexiconLookup goes on Tools menu & Insert toolbar
			toolUiWidgetParameterObject.MenuItemsForTool[MainMenu.Tools].Add(Command.CmdLexiconLookup, tuple);
			_majorFlexComponentParameters.UiWidgetController.ToolsMenuDictionary[Command.CmdLexiconLookup].Tag = dataTree;
			toolUiWidgetParameterObject.ToolBarItemsForTool[ToolBar.Insert].Add(Command.CmdLexiconLookup, tuple);
			_majorFlexComponentParameters.UiWidgetController.InsertToolBarDictionary[Command.CmdLexiconLookup].Tag = dataTree;
		}

		private void LexiconLookup_Clicked(object sender, EventArgs e)
		{
			LanguageExplorerServices.LexiconLookup(_majorFlexComponentParameters.LcmCache, new FlexComponentParameters(PropertyTable, Publisher, Subscriber), ((DataTree)((ToolStripItem)sender).Tag).CurrentSliceAsStTextSlice.RootSite);
		}
	}
}