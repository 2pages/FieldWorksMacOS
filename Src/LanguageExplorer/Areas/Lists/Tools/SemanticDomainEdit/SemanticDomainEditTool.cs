// Copyright (c) 2015-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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

namespace LanguageExplorer.Areas.Lists.Tools.SemanticDomainEdit
{
	/// <summary>
	/// ITool implementation for the "semanticDomainEdit" tool in the "lists" area.
	/// </summary>
	[Export(AreaServices.ListsAreaMachineName, typeof(ITool))]
	internal sealed class SemanticDomainEditTool : IListTool
	{
		private const string SemanticDomainList_ListArea = "SemanticDomainList_ListArea";
		/// <summary>
		/// Main control to the right of the side bar control. This holds a RecordBar on the left and a PaneBarContainer on the right.
		/// The RecordBar has no top PaneBar for information, menus, etc.
		/// </summary>
		private CollapsingSplitContainer _collapsingSplitContainer;
		private SemanticDomainEditMenuHelper _toolMenuHelper;
		private IRecordList _recordList;
		private LcmCache _cache;

		#region Implementation of IMajorFlexComponent

		/// <summary>
		/// Deactivate the component.
		/// </summary>
		/// <remarks>
		/// This is called on the outgoing component, when the user switches to a component.
		/// </remarks>
		public void Deactivate(MajorFlexComponentParameters majorFlexComponentParameters)
		{
			// This will also remove any event handlers set up by the tool's UserControl instances that may have registered event handlers.
			majorFlexComponentParameters.UiWidgetController.RemoveToolHandlers();
			CollapsingSplitContainerFactory.RemoveFromParentAndDispose(majorFlexComponentParameters.MainCollapsingSplitContainer, ref _collapsingSplitContainer);

			// Dispose after the main UI stuff.
			_toolMenuHelper.Dispose();
			_toolMenuHelper = null;
			_cache = null;
		}

		/// <summary>
		/// Activate the component.
		/// </summary>
		/// <remarks>
		/// This is called on the component that is becoming active.
		/// </remarks>
		public void Activate(MajorFlexComponentParameters majorFlexComponentParameters)
		{
			_cache = majorFlexComponentParameters.LcmCache;
			if (_recordList == null)
			{
				_recordList = majorFlexComponentParameters.FlexComponentParameters.PropertyTable.GetValue<IRecordListRepositoryForTools>(LanguageExplorerConstants.RecordListRepository).GetRecordList(SemanticDomainList_ListArea, majorFlexComponentParameters.StatusBar, FactoryMethod);
			}
			var dataTree = new DataTree(majorFlexComponentParameters.SharedEventHandlers, majorFlexComponentParameters.FlexComponentParameters.PropertyTable.GetValue(UiWidgetServices.CreateShowHiddenFieldsPropertyName(MachineName), false));
			_toolMenuHelper = new SemanticDomainEditMenuHelper(majorFlexComponentParameters, this, MyList, _recordList, dataTree);
			_collapsingSplitContainer = CollapsingSplitContainerFactory.Create(majorFlexComponentParameters.FlexComponentParameters, majorFlexComponentParameters.MainCollapsingSplitContainer,
				true, XDocument.Parse(ListResources.SemanticDomainEditParameters).Root, XDocument.Parse(ListResources.ListToolsSliceFilters), MachineName,
				majorFlexComponentParameters.LcmCache, _recordList, dataTree, majorFlexComponentParameters.UiWidgetController);
		}

		/// <summary>
		/// Do whatever might be needed to get ready for a refresh.
		/// </summary>
		/// <remarks>
		/// One might expect this method to pass this call into the area's current tool.
		/// </remarks>
		public void PrepareToRefresh()
		{
		}

		/// <summary>
		/// Finish the refresh.
		/// </summary>
		/// <remarks>
		/// One might expect this method to pass this call into the area's current tool.
		/// </remarks>
		public void FinishRefresh()
		{
			_recordList.ReloadIfNeeded();
			((DomainDataByFlidDecoratorBase)_recordList.VirtualListPublisher).Refresh();
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
		public string MachineName => AreaServices.SemanticDomainEditMachineName;

		/// <summary>
		/// User-visible localized component name.
		/// </summary>
		public string UiName => StringTable.Table.LocalizeLiteralValue(AreaServices.SemanticDomainEditUiName);

		#endregion

		#region Implementation of ITool

		/// <summary>
		/// Get the area for the tool.
		/// </summary>
		[field: Import(AreaServices.ListsAreaMachineName)]
		public IArea Area { get; private set; }

		/// <summary>
		/// Get the image for the area.
		/// </summary>
		public Image Icon => Images.SideBySideView.SetBackgroundColor(Color.Magenta);

		#endregion

		#region Implementation of IListTool
		/// <inheritdoc />
		public ICmPossibilityList MyList => _cache.LanguageProject.SemanticDomainListOA;
		#endregion

		private IRecordList FactoryMethod(LcmCache cache, FlexComponentParameters flexComponentParameters, string recordListId, StatusBar statusBar)
		{
			Require.That(recordListId == SemanticDomainList_ListArea, $"I don't know how to create a record list with an ID of '{recordListId}', as I can only create one with an id of '{SemanticDomainList_ListArea}'.");

			/*
            <clerk id="SemanticDomainList">
              <recordList owner="LangProject" property="SemanticDomainList">
                <dynamicloaderinfo assemblyPath="xWorks.dll" class="SIL.FieldWorks.XWorks.PossibilityRecordList" />
              </recordList>
              <treeBarHandler assemblyPath="xWorks.dll" expand="false" hierarchical="true" includeAbbr="true" ws="best analysis" class="SIL.FieldWorks.XWorks.SemanticDomainRdeTreeBarHandler" altTitleId="SemanticDomain-Plural" />
              <filters />
              <sortMethods>
                <sortMethod label="Default" assemblyPath="Filters.dll" class="SIL.FieldWorks.Filters.PropertyRecordSorter" sortProperty="ShortName" />
              </sortMethods>
            </clerk>
			*/
			return new TreeBarHandlerAwarePossibilityRecordList(recordListId, statusBar, cache.ServiceLocator.GetInstance<ISilDataAccessManaged>(),
				MyList, new PossibilityTreeBarHandler(flexComponentParameters.PropertyTable, false, true, true, "best analysis"));
		}

		private sealed class SemanticDomainEditMenuHelper : IDisposable
		{
			private readonly MajorFlexComponentParameters _majorFlexComponentParameters;
			private readonly ICmPossibilityList _list;
			private readonly IRecordList _recordList;
			private SharedListToolsUiWidgetMenuHelper _sharedListToolsUiWidgetMenuHelper;

			internal SemanticDomainEditMenuHelper(MajorFlexComponentParameters majorFlexComponentParameters, ITool tool, ICmPossibilityList list, IRecordList recordList, DataTree dataTree)
			{
				Guard.AgainstNull(majorFlexComponentParameters, nameof(majorFlexComponentParameters));
				Guard.AgainstNull(tool, nameof(tool));
				Guard.AgainstNull(list, nameof(list));
				Guard.AgainstNull(recordList, nameof(recordList));
				Guard.AgainstNull(dataTree, nameof(dataTree));

				_majorFlexComponentParameters = majorFlexComponentParameters;
				_list = list;
				_recordList = recordList;
				_sharedListToolsUiWidgetMenuHelper = new SharedListToolsUiWidgetMenuHelper(majorFlexComponentParameters, tool, list, recordList, dataTree);
				SetupToolUiWidgets(tool, dataTree);
			}

			private void SetupToolUiWidgets(ITool tool, DataTree dataTree)
			{
				var toolUiWidgetParameterObject = new ToolUiWidgetParameterObject(tool);
				_sharedListToolsUiWidgetMenuHelper.SetupToolUiWidgets(toolUiWidgetParameterObject, commands: new HashSet<Command> { Command.CmdAddToLexicon, Command.CmdExport, Command.CmdLexiconLookup });
				var insertMenuDictionary = toolUiWidgetParameterObject.MenuItemsForTool[MainMenu.Insert];
				var insertToolbarDictionary = toolUiWidgetParameterObject.ToolBarItemsForTool[ToolBar.Insert];
				// <command id="CmdInsertSemDom" label="_Semantic Domain" message="InsertItemInVector" icon="AddItem">
				// <command id="CmdDataTree_Insert_SemanticDomain" label="Insert subdomain" message="DataTreeInsert" icon="AddSubItem">
				// Insert menu & tool bar
				insertMenuDictionary.Add(Command.CmdInsertPossibility, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CmdInsertSemDom_Click, ()=> UiWidgetServices.CanSeeAndDo));
				insertToolbarDictionary.Add(Command.CmdInsertPossibility, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CmdInsertSemDom_Click, () => UiWidgetServices.CanSeeAndDo));
				AreaServices.ResetMainPossibilityInsertUiWidgetsText(_majorFlexComponentParameters.UiWidgetController, ListResources.Semantic_Domain);
				insertMenuDictionary.Add(Command.CmdDataTree_Insert_Possibility, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CmdDataTree_Insert_SemanticDomain_Click, () => CanCmdDataTree_Insert_SemanticDomain));
				insertToolbarDictionary.Add(Command.CmdDataTree_Insert_Possibility, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CmdDataTree_Insert_SemanticDomain_Click, () => CanCmdDataTree_Insert_SemanticDomain));
				AreaServices.ResetSubitemPossibilityInsertUiWidgetsText(_majorFlexComponentParameters.UiWidgetController, ListResources.Subdomain);

				/*
					<part id="CmSemanticDomain-Detail-Questions" type="Detail">
						<slice label="Questions" menu="mnuDataTree_InsertQuestion">
						  <seq field="Questions"/>
						</slice>
					</part>
				*/
				dataTree.DataTreeSliceContextMenuParameterObject.LeftEdgeContextMenuFactory.RegisterLeftEdgeContextMenuCreatorMethod(ContextMenuName.mnuDataTree_InsertQuestion, Create_mnuDataTree_InsertQuestion);
				/*
					<part id="CmDomainQ-Detail-QuestionAllA" type="Detail">
						<slice field="Question" label="Question" editor="multistring" ws="all analysis" menu="mnuDataTree_DeleteQuestion">
						</slice>
					</part>
				*/
				dataTree.DataTreeSliceContextMenuParameterObject.LeftEdgeContextMenuFactory.RegisterLeftEdgeContextMenuCreatorMethod(ContextMenuName.mnuDataTree_DeleteQuestion, Create_mnuDataTree_DeleteQuestion);
				// <menu id="mnuDataTree_SubSemanticDomain">
				dataTree.DataTreeSliceContextMenuParameterObject.LeftEdgeContextMenuFactory.RegisterLeftEdgeContextMenuCreatorMethod(ContextMenuName.mnuDataTree_SubSemanticDomain, Create_mnuDataTree_SubSemanticDomain);

				_majorFlexComponentParameters.UiWidgetController.AddHandlers(toolUiWidgetParameterObject);
			}

			private Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>> Create_mnuDataTree_SubSemanticDomain(Slice slice, ContextMenuName contextMenuId)
			{
				Require.That(contextMenuId == ContextMenuName.mnuDataTree_SubSemanticDomain, $"Expected argument value of '{ContextMenuName.mnuDataTree_SubSemanticDomain.ToString()}', but got '{contextMenuId.ToString()}' instead.");

				// Start: <menu id="mnuDataTree_SubSemanticDomain">
				var contextMenuStrip = new ContextMenuStrip
				{
					Name = ContextMenuName.mnuDataTree_SubSemanticDomain.ToString()
				};
				var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>(1);

				/*
					<item command="CmdDataTree_Insert_SemanticDomain" />
				*/
				ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, CmdDataTree_Insert_SemanticDomain_Click, ListResources.Insert_Subdomain, image: AreaResources.AddSubItem.ToBitmap());

				// End: <menu id="mnuDataTree_SubSemanticDomain">

				return new Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>>(contextMenuStrip, menuItems);
			}

			private Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>> Create_mnuDataTree_InsertQuestion(Slice slice, ContextMenuName contextMenuId)
			{
				Require.That(contextMenuId == ContextMenuName.mnuDataTree_InsertQuestion, $"Expected argument value of '{ContextMenuName.mnuDataTree_InsertQuestion.ToString()}', but got '{contextMenuId.ToString()}' instead.");

				// Start: <menu id="mnuDataTree_InsertQuestion">
				var contextMenuStrip = new ContextMenuStrip
				{
					Name = ContextMenuName.mnuDataTree_InsertQuestion.ToString()
				};
				var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>(1);

				/*
					<command id="CmdDataTree_Insert_Question" label="Insert Question" message="DataTreeInsert" icon="AddSubItem">
					  <parameters field="Questions" className="CmDomainQ" />
					</command> // Insert_Question
				*/
				ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, InsertQuestion_Clicked, ListResources.Insert_Question, image: AreaResources.AddSubItem.ToBitmap());

				// End: <menu id="mnuDataTree_InsertQuestion">

				return new Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>>(contextMenuStrip, menuItems);
			}

			private void InsertQuestion_Clicked(object sender, EventArgs e)
			{
				var currentSemanticDomain = (ICmSemanticDomain)_recordList.CurrentObject;
				var cache = currentSemanticDomain.Cache;
				UowHelpers.UndoExtension(ListResources.Insert_Question, cache.ActionHandlerAccessor, () =>
				{
					currentSemanticDomain.QuestionsOS.Add(cache.ServiceLocator.GetInstance<ICmDomainQFactory>().Create());
				});
			}

			private Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>> Create_mnuDataTree_DeleteQuestion(Slice slice, ContextMenuName contextMenuId)
			{
				Require.That(contextMenuId == ContextMenuName.mnuDataTree_DeleteQuestion, $"Expected argument value of '{ContextMenuName.mnuDataTree_DeleteQuestion.ToString()}', but got '{contextMenuId.ToString()}' instead.");

				// Start: <menu id="mnuDataTree-Delete-Question">
				var contextMenuStrip = new ContextMenuStrip
				{
					Name = ContextMenuName.mnuDataTree_DeleteQuestion.ToString()
				};
				var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>(1);

				/*
					<command id="CmdDataTree_Delete_Question" label="Delete Question" message="DataTreeDelete" icon="Delete">
						<parameters field="Questions" className="CmDomainQ" />
					</command>
				*/
				AreaServices.CreateDeleteMenuItem(menuItems, contextMenuStrip, slice, ListResources.Delete_Question, _majorFlexComponentParameters.SharedEventHandlers.GetEventHandler(Command.CmdDataTreeDelete));

				// End: <menu id="mnuDataTree-Delete-Question">

				return new Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>>(contextMenuStrip, menuItems);
			}

			private void CmdInsertSemDom_Click(object sender, EventArgs e)
			{
				ICmPossibility newPossibility = null;
				UowHelpers.UndoExtension(ListResources.Insert_Item, _majorFlexComponentParameters.LcmCache.ActionHandlerAccessor, () =>
				{
					newPossibility = _majorFlexComponentParameters.LcmCache.ServiceLocator.GetInstance<ICmSemanticDomainFactory>().Create(Guid.NewGuid(), _list);
				});
				if (newPossibility != null)
				{
					_recordList.UpdateRecordTreeBar();
				}
			}

			private Tuple<bool, bool> CanCmdDataTree_Insert_SemanticDomain => new Tuple<bool, bool>(true, _recordList.CurrentObject != null);

			private void CmdDataTree_Insert_SemanticDomain_Click(object sender, EventArgs e)
			{
				ICmPossibility newSubPossibility = null;
				UowHelpers.UndoExtension(ListResources.Insert_Item, _majorFlexComponentParameters.LcmCache.ActionHandlerAccessor, () =>
				{
					newSubPossibility = _majorFlexComponentParameters.LcmCache.ServiceLocator.GetInstance<ICmSemanticDomainFactory>().Create(Guid.NewGuid(), (ICmSemanticDomain)_recordList.CurrentObject);
				});
				if (newSubPossibility != null)
				{
					_recordList.UpdateRecordTreeBar();
				}
			}

			#region Implementation of IDisposable
			private bool _isDisposed;

			~SemanticDomainEditMenuHelper()
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