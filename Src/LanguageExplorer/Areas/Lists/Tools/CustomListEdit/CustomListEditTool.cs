// Copyright (c) 2017-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using LanguageExplorer.Controls;
using LanguageExplorer.Controls.DetailControls;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Resources;
using SIL.LCModel;
using SIL.LCModel.Application;

namespace LanguageExplorer.Areas.Lists.Tools.CustomListEdit
{
	internal sealed class CustomListEditTool : IListTool
	{
		/// <summary>
		/// Main control to the right of the side bar control. This holds a RecordBar on the left and a PaneBarContainer on the right.
		/// The RecordBar has no top PaneBar for information, menus, etc.
		/// </summary>
		private CollapsingSplitContainer _collapsingSplitContainer;
		private CustomListMenuHelper _toolMenuHelper;
		private readonly IListArea _area;
		private IRecordList _recordList;

		internal CustomListEditTool(IListArea area, ICmPossibilityList customList)
		{
			Guard.AgainstNull(area, nameof(area));
			Guard.AgainstNull(customList, nameof(customList));

			_area = area;
			MyList = customList;
			MachineName = $"CustomList_{MyList.Guid}_Edit";
		}

		#region Implementation of IMajorFlexComponent

		/// <summary>
		/// Deactivate the component.
		/// </summary>
		/// <remarks>
		/// This is called on the outgoing component, when the user switches to another component.
		/// </remarks>
		public void Deactivate(MajorFlexComponentParameters majorFlexComponentParameters)
		{
			// This will also remove any event handlers set up by the tool's UserControl instances that may have registered event handlers.
			majorFlexComponentParameters.UiWidgetController.RemoveToolHandlers();
			CollapsingSplitContainerFactory.RemoveFromParentAndDispose(majorFlexComponentParameters.MainCollapsingSplitContainer, ref _collapsingSplitContainer);

			// Dispose after the main UI stuff.
			_toolMenuHelper.Dispose();
			_toolMenuHelper = null;
		}

		/// <summary>
		/// Activate the component.
		/// </summary>
		/// <remarks>
		/// This is called on the component that is becoming active.
		/// </remarks>
		public void Activate(MajorFlexComponentParameters majorFlexComponentParameters)
		{
			if (_recordList == null)
			{
				_recordList = majorFlexComponentParameters.FlexComponentParameters.PropertyTable.GetValue<IRecordListRepositoryForTools>(LanguageExplorerConstants.RecordListRepository).GetRecordList(MachineName, majorFlexComponentParameters.StatusBar, MyList, FactoryMethod);
			}
			var dataTree = new DataTree(majorFlexComponentParameters.SharedEventHandlers, majorFlexComponentParameters.FlexComponentParameters.PropertyTable.GetValue(UiWidgetServices.CreateShowHiddenFieldsPropertyName(MachineName), false));
			_collapsingSplitContainer = CollapsingSplitContainerFactory.Create(majorFlexComponentParameters.FlexComponentParameters, majorFlexComponentParameters.MainCollapsingSplitContainer,
				true, XDocument.Parse(ListResources.CustomEditParameters).Root, XDocument.Parse(ListResources.ListToolsSliceFilters), MachineName,
				majorFlexComponentParameters.LcmCache, _recordList, dataTree, majorFlexComponentParameters.UiWidgetController);
			_toolMenuHelper = new CustomListMenuHelper(majorFlexComponentParameters, this, MyList, _recordList, dataTree);
		}

		/// <summary>
		/// Do whatever might be needed to get ready for a refresh.
		/// </summary>
		public void PrepareToRefresh()
		{
		}

		/// <summary>
		/// Finish the refresh.
		/// </summary>
		public void FinishRefresh()
		{
		}

		/// <summary>
		/// The properties are about to be saved, so make sure they are all current.
		/// Add new ones, as needed.
		/// </summary>
		public void EnsurePropertiesAreCurrent()
		{
		}

        #endregion

        #region Implementation of IMajorFlexUiComponent

        /// <summary>
        /// Get the internal name of the component.
        /// </summary>
        /// <remarks>NB: This is the machine friendly name, not the user friendly name.</remarks>
        public string MachineName { get; }

		/// <summary>
		/// User-visible localized component name.
		/// </summary>
		public string UiName => StringTable.Table.LocalizeLiteralValue(MyList.Name.BestAnalysisAlternative.Text);

		#endregion

		#region Implementation of ITool

		/// <summary>
		/// Get the area for the tool.
		/// </summary>
		public IArea Area => _area;

		/// <summary>
		/// Get the image for the area.
		/// </summary>
		public Image Icon => Images.SideBySideView.SetBackgroundColor(Color.Magenta);

        #endregion

        #region Implementation of IListTool
        /// <inheritdoc />
        public ICmPossibilityList MyList { get; }
        #endregion

        private IRecordList FactoryMethod(ICmPossibilityList customList, LcmCache cache, FlexComponentParameters flexComponentParameters, string recordListId, StatusBar statusBar)
		{
			Require.That(recordListId == MachineName, $"I don't know how to create a record list with an ID of '{recordListId}', as I can only create one with an id of '{MachineName}'.");

			return new TreeBarHandlerAwarePossibilityRecordList(recordListId, statusBar, cache.ServiceLocator.GetInstance<ISilDataAccessManaged>(),
				customList, new PossibilityTreeBarHandler(flexComponentParameters.PropertyTable, false, customList.Depth > 1, customList.DisplayOption == (int)PossNameType.kpntName, customList.GetWsString()));
		}

		private sealed class CustomListMenuHelper : IDisposable
		{
			private readonly MajorFlexComponentParameters _majorFlexComponentParameters;
			private readonly ITool _tool;
			private readonly ICmPossibilityList _list;
			private readonly IRecordList _recordList;
			private SharedListToolsUiWidgetMenuHelper _sharedListToolsUiWidgetMenuHelper;
			private IListArea Area => (IListArea)_tool.Area;

			internal CustomListMenuHelper(MajorFlexComponentParameters majorFlexComponentParameters, ITool tool, ICmPossibilityList list, IRecordList recordList, DataTree dataTree)
			{
				Guard.AgainstNull(majorFlexComponentParameters, nameof(majorFlexComponentParameters));
				Guard.AgainstNull(tool, nameof(tool));
				Guard.AgainstNull(list, nameof(list));
				Guard.AgainstNull(recordList, nameof(recordList));
				Guard.AgainstNull(dataTree, nameof(dataTree));

				_majorFlexComponentParameters = majorFlexComponentParameters;
				_tool = tool;
				_list = list;
				_recordList = recordList;
				_sharedListToolsUiWidgetMenuHelper = new SharedListToolsUiWidgetMenuHelper(majorFlexComponentParameters, tool, list, recordList, dataTree);

				SetupToolUiWidgets();
			}

			private void SetupToolUiWidgets()
			{
				var toolUiWidgetParameterObject = new ToolUiWidgetParameterObject(_tool);
				_sharedListToolsUiWidgetMenuHelper.SetupToolUiWidgets(toolUiWidgetParameterObject, commands: new HashSet<Command> { Command.CmdAddToLexicon, Command.CmdExport, Command.CmdLexiconLookup });
				var menuItemsDictionary = toolUiWidgetParameterObject.MenuItemsForTool;
				// <command id = "CmdDeleteCustomList" label="Delete Custom _List" message="DeleteCustomList" />
				menuItemsDictionary[MainMenu.Edit].Add(Command.CmdDeleteCustomList, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(DeleteCustomList_Click, ()=> UiWidgetServices.CanSeeAndDo));
				// Goes in Insert menu
				var insertMenuDictionary = menuItemsDictionary[MainMenu.Insert];
				var insertToolbarDictionary = toolUiWidgetParameterObject.ToolBarItemsForTool[ToolBar.Insert];
				// Goes in Insert menu & Insert toolbar
				// <command id="CmdInsertCustomItem" label="_Item" message="InsertItemInVector" icon="AddItem">
				insertMenuDictionary.Add(Command.CmdInsertPossibility, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CmdInsertCustomItem_Click, ()=> UiWidgetServices.CanSeeAndDo));
				insertToolbarDictionary.Add(Command.CmdInsertPossibility, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CmdInsertCustomItem_Click, () => UiWidgetServices.CanSeeAndDo));
				AreaServices.ResetMainPossibilityInsertUiWidgetsText(_majorFlexComponentParameters.UiWidgetController, ListResources.List_Item);
				// Goes in Insert menu & Insert toolbar
				// <command id="CmdDataTree_Insert_CustomItem" label="Insert subitem" message="DataTreeInsert" icon="AddSubItem">
				insertMenuDictionary.Add(Command.CmdDataTree_Insert_Possibility, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CmdInsertCustomSubItem_Click, () => CanCmdInsertCustomSubItem));
				insertToolbarDictionary.Add(Command.CmdDataTree_Insert_Possibility, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CmdInsertCustomSubItem_Click, () => CanCmdInsertCustomSubItem));
				AreaServices.ResetSubitemPossibilityInsertUiWidgetsText(_majorFlexComponentParameters.UiWidgetController, ListResources.Subitem);
				_majorFlexComponentParameters.UiWidgetController.AddHandlers(toolUiWidgetParameterObject);
			}

			private void DeleteCustomList_Click(object sender, EventArgs e)
			{
				UowHelpers.UndoExtension(ListResources.CustomList, _majorFlexComponentParameters.LcmCache.ActionHandlerAccessor, () => new DeleteCustomList(_majorFlexComponentParameters.LcmCache).Run(_list));
				Area.OnRemoveCustomListTool(_tool);
			}

			private void CmdInsertCustomItem_Click(object sender, EventArgs e)
			{
				ICmPossibility newCustomItem = null;
				UowHelpers.UndoExtension(ListResources.Custom_Item, _majorFlexComponentParameters.LcmCache.ActionHandlerAccessor, () =>
				{
					newCustomItem = _majorFlexComponentParameters.LcmCache.ServiceLocator.GetInstance<ICmCustomItemFactory>().Create(Guid.NewGuid(), _list);
				});
				if (newCustomItem != null)
				{
					_recordList.UpdateRecordTreeBar();
				}
			}

			private Tuple<bool, bool> CanCmdInsertCustomSubItem => new Tuple<bool, bool>(true, _recordList.CurrentObject != null);

			private void CmdInsertCustomSubItem_Click(object sender, EventArgs e)
			{
				ICmPossibility newCustomSubItem = null;
				UowHelpers.UndoExtension(ListResources.Custom_Item, _majorFlexComponentParameters.LcmCache.ActionHandlerAccessor, () =>
				{
					newCustomSubItem = _majorFlexComponentParameters.LcmCache.ServiceLocator.GetInstance<ICmCustomItemFactory>().Create(Guid.NewGuid(), (ICmCustomItem)_recordList.CurrentObject);
				});
				if (newCustomSubItem != null)
				{
					_recordList.UpdateRecordTreeBar();
				}
			}

			#region Implementation of IDisposable
			private bool _isDisposed;

			~CustomListMenuHelper()
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
					_sharedListToolsUiWidgetMenuHelper.Dispose();
				}
				_sharedListToolsUiWidgetMenuHelper = null;

				_isDisposed = true;
			}
			#endregion
		}
	}
}