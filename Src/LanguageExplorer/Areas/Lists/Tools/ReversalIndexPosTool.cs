// Copyright (c) 2015-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using LanguageExplorer.Controls;
using LanguageExplorer.Controls.DetailControls;
using LanguageExplorer.Controls.PaneBar;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Resources;
using SIL.LCModel;
using SIL.LCModel.Application;

namespace LanguageExplorer.Areas.Lists.Tools
{
	/// <summary>
	/// ITool implementation for the "reversalToolReversalIndexPOS" tool in the "lists" area.
	/// </summary>
	[Export(LanguageExplorerConstants.ListsAreaMachineName, typeof(ITool))]
	internal sealed class ReversalIndexPosTool : IListTool
	{
		private MultiPane _multiPane;
		private IRecordList _recordList;
		private RecordBrowseView _recordBrowseView;
		private ReversalIndexPosEditMenuHelper _toolMenuHelper;
		private IReversalIndex _currentReversalIndex;

		[Import]
		private IPropertyTable _propertyTable;
		[Import]
		private IPublisher _publisher;
		[Import]
		private ISubscriber _subscriber;
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
			MultiPaneFactory.RemoveFromParentAndDispose(majorFlexComponentParameters.MainCollapsingSplitContainer, ref _multiPane);
			_subscriber.Unsubscribe(LanguageExplorerConstants.ReversalIndexGuid, HandleReversalIndexGuid_Changed);

			// Dispose after the main UI stuff.
			_toolMenuHelper.Dispose();

			_recordBrowseView = null;
			_currentReversalIndex = null;
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
			_subscriber.Subscribe(LanguageExplorerConstants.ReversalIndexGuid, HandleReversalIndexGuid_Changed);
			HandleReversalIndexGuid_Changed(null);
			if (_recordList == null)
			{
				_recordList = majorFlexComponentParameters.FlexComponentParameters.PropertyTable.GetValue<IRecordListRepositoryForTools>(LanguageExplorerConstants.RecordListRepository).GetRecordList(LanguageExplorerConstants.ReversalEntriesPOS, majorFlexComponentParameters.StatusBar, RecordListActivator.ReversalIndexPOSFactoryMethod);
			}
			_recordBrowseView = new RecordBrowseView(XDocument.Parse(ListResources.ReversalToolReversalIndexPOSBrowseViewParameters).Root, _cache, _recordList, majorFlexComponentParameters.UiWidgetController);
			var showHiddenFieldsPropertyName = UiWidgetServices.CreateShowHiddenFieldsPropertyName(MachineName);
			var dataTree = new DataTree(majorFlexComponentParameters.SharedEventHandlers, _propertyTable.GetValue(showHiddenFieldsPropertyName, false));
			_toolMenuHelper = new ReversalIndexPosEditMenuHelper(majorFlexComponentParameters, this, _currentReversalIndex, _recordList, dataTree, _recordBrowseView);
			var recordEditView = new RecordEditView(XDocument.Parse(ListResources.ReversalToolReversalIndexPOSRecordEditViewParameters).Root, XDocument.Parse(AreaResources.HideAdvancedListItemFields), _cache, _recordList, dataTree, majorFlexComponentParameters.UiWidgetController);
			var mainMultiPaneParameters = new MultiPaneParameters
			{
				Orientation = Orientation.Vertical,
				Area = Area,
				Id = "RevEntryPOSesAndDetailMultiPane",
				ToolMachineName = MachineName
			};
			var browseViewPaneBar = new PaneBar();
			var img = LanguageExplorerResources.MenuWidget;
			img.MakeTransparent(Color.Magenta);
			// NB: FW9 has this on the left and another on the right.
			// Trouble is, the right one does nothing, so FW10 throw it overboard.
			var panelMenu = new PanelMenu(_toolMenuHelper.MainPanelMenuContextMenuFactory, AreaServices.LeftPanelMenuId)
			{
				Dock = DockStyle.Left,
				BackgroundImage = img,
				BackgroundImageLayout = ImageLayout.Center
			};
			browseViewPaneBar.AddControls(new List<Control> { panelMenu });
			var recordEditViewPaneBar = new PaneBar();
			var panelButton = new PanelButton(majorFlexComponentParameters.FlexComponentParameters, null, showHiddenFieldsPropertyName, LanguageExplorerResources.ksShowHiddenFields, LanguageExplorerResources.ksShowHiddenFields)
			{
				Dock = DockStyle.Right
			};
			recordEditViewPaneBar.AddControls(new List<Control> { panelButton });
			_multiPane = MultiPaneFactory.CreateMultiPaneWithTwoPaneBarContainersInMainCollapsingSplitContainer(majorFlexComponentParameters.FlexComponentParameters, majorFlexComponentParameters.MainCollapsingSplitContainer,
				mainMultiPaneParameters, _recordBrowseView, "Browse", browseViewPaneBar, recordEditView, "Details", recordEditViewPaneBar);
			// Too early before now.
			recordEditView.FinishInitialization();
		}

		/// <summary>
		/// Do whatever might be needed to get ready for a refresh.
		/// </summary>
		/// <remarks>
		/// One might expect this method to pass this call into the area's current tool.
		/// </remarks>
		public void PrepareToRefresh()
		{
			_recordBrowseView.BrowseViewer.BrowseView.PrepareToRefresh();
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
		public string MachineName => LanguageExplorerConstants.ReversalToolReversalIndexPOSMachineName;

		/// <summary>
		/// User-visible localized component name.
		/// </summary>
		public string UiName => StringTable.Table.LocalizeLiteralValue(LanguageExplorerConstants.ReversalToolReversalIndexPOSUiName);

		#endregion

		#region Implementation of ITool

		/// <summary>
		/// Get the area for the tool.
		/// </summary>
		[field: Import(LanguageExplorerConstants.ListsAreaMachineName)]
		public IArea Area { get; private set; }

		/// <summary>
		/// Get the image for the area.
		/// </summary>
		public Image Icon => Images.SideBySideView.SetBackgroundColor(Color.Magenta);

		#endregion

		#region Implementation of IListTool
		/// <inheritdoc />
		public ICmPossibilityList MyList => _currentReversalIndex.PartsOfSpeechOA;
		#endregion

		private void HandleReversalIndexGuid_Changed(object obj)
		{
			// 'obj' is a string of the guid.
			var currentGuid = FwUtils.GetObjectGuidIfValid(_propertyTable, LanguageExplorerConstants.ReversalIndexGuid);
			if (currentGuid != Guid.Empty)
			{
				_currentReversalIndex = (IReversalIndex)_cache.ServiceLocator.GetObject(currentGuid);
			}
		}

		private sealed class ReversalIndexPosEditMenuHelper : IDisposable
		{
			private MajorFlexComponentParameters _majorFlexComponentParameters;
			private IReversalIndex _currentReversalIndex;
			private ICmPossibilityList _list;
			private IRecordList _recordList;
			private DataTree _dataTree;
			private LcmCache _cache;
			private IReversalIndexRepository _reversalIndexRepository;
			private RecordBrowseView _recordBrowseView;
			private IPropertyTable _propertyTable;
			internal PanelMenuContextMenuFactory MainPanelMenuContextMenuFactory { get; private set; }
			private SharedListToolsUiWidgetMenuHelper _sharedListToolsUiWidgetMenuHelper;

			private IPropertyTable PropertyTable => _majorFlexComponentParameters.FlexComponentParameters.PropertyTable;

			internal ReversalIndexPosEditMenuHelper(MajorFlexComponentParameters majorFlexComponentParameters, ITool tool, IReversalIndex currentReversalIndex, IRecordList recordList, DataTree dataTree, RecordBrowseView recordBrowseView)
			{
				Guard.AgainstNull(majorFlexComponentParameters, nameof(majorFlexComponentParameters));
				Guard.AgainstNull(tool, nameof(tool));
				Guard.AgainstNull(currentReversalIndex, nameof(currentReversalIndex));
				Guard.AgainstNull(recordList, nameof(recordList));
				Guard.AgainstNull(dataTree, nameof(dataTree));
				Guard.AgainstNull(recordBrowseView, nameof(recordBrowseView));

				_majorFlexComponentParameters = majorFlexComponentParameters;
				_currentReversalIndex = currentReversalIndex;
				_list = _currentReversalIndex.PartsOfSpeechOA;
				_recordList = recordList;
				_dataTree = dataTree;
				_cache = _majorFlexComponentParameters.LcmCache;
				_recordBrowseView = recordBrowseView;
				_sharedListToolsUiWidgetMenuHelper = new SharedListToolsUiWidgetMenuHelper(majorFlexComponentParameters, tool, _list, recordList, dataTree);
				_propertyTable = _majorFlexComponentParameters.FlexComponentParameters.PropertyTable;
				MainPanelMenuContextMenuFactory = new PanelMenuContextMenuFactory();

				SetupToolUiWidgets(tool, dataTree);
			}

			private void SetupToolUiWidgets(ITool tool, DataTree dataTree)
			{
				var toolUiWidgetParameterObject = new ToolUiWidgetParameterObject(tool);
				_sharedListToolsUiWidgetMenuHelper.SetupToolUiWidgets(toolUiWidgetParameterObject, commands: new HashSet<Command> { Command.CmdAddToLexicon, Command.CmdExport, Command.CmdLexiconLookup });
				// <command id="CmdInsertPOS" label="Category" message="InsertItemInVector" shortcut="Ctrl+I" icon="AddItem">
				// <command id="CmdDataTree_Insert_POS_SubPossibilities" label="Insert Subcategory..." message="DataTreeInsert" icon="AddSubItem">
				// Insert menu & tool bar for both.
				_sharedListToolsUiWidgetMenuHelper.MyPartiallySharedForToolsWideMenuHelper.SetupCmdInsertPossibility(toolUiWidgetParameterObject, () => UiWidgetServices.CanSeeAndDo);
				// Override labels.
				AreaServices.ResetMainPossibilityInsertUiWidgetsText(_majorFlexComponentParameters.UiWidgetController, AreaResources.Category);

				_sharedListToolsUiWidgetMenuHelper.MyPartiallySharedForToolsWideMenuHelper.SetupCmdDataTree_Insert_Possibility(toolUiWidgetParameterObject, () => CanCmdDataTree_Insert_POS_SubPossibilities);

				dataTree.DataTreeSliceContextMenuParameterObject.LeftEdgeContextMenuFactory.RegisterLeftEdgeContextMenuCreatorMethod(ContextMenuName.mnuDataTree_MoveMainReversalPOS, Create_mnuDataTree_MoveMainReversalPOS);
				dataTree.DataTreeSliceContextMenuParameterObject.LeftEdgeContextMenuFactory.RegisterLeftEdgeContextMenuCreatorMethod(ContextMenuName.mnuDataTree_MoveReversalPOS, Create_mnuDataTree_MoveReversalPOS);
				MainPanelMenuContextMenuFactory.RegisterPanelMenuCreatorMethod(AreaServices.LeftPanelMenuId, CreateMainPanelLeftContextMenuStrip);

				_majorFlexComponentParameters.UiWidgetController.AddHandlers(toolUiWidgetParameterObject);
				_recordBrowseView.RegisterUiWidgets(true);
				CreateBrowseViewContextMenu();
			}

			private void CreateBrowseViewContextMenu()
			{
				// The actual menu declaration has a gazillion menu items, but only two of them are seen in this tool (plus the separator).
				// Start: <menu id="mnuBrowseView" (partial) >
				var contextMenuStrip = new ContextMenuStrip
				{
					Name = ContextMenuName.mnuBrowseView.ToString()
				};
				var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>(1);

				// <command id="CmdDeleteSelectedObject" label="Delete selected {0}" message="DeleteSelectedItem"/>
				ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, CmdDeleteSelectedObject_Clicked, string.Format(LanguageExplorerResources.Delete_selected_0, StringTable.Table.GetString("PartOfSpeech", StringTable.ClassNames)));

				// End: <menu id="mnuBrowseView" (partial) >
				_recordBrowseView.ContextMenuStrip = contextMenuStrip;
			}

			private void CmdDeleteSelectedObject_Clicked(object sender, EventArgs e)
			{
				_recordList.DeleteRecord(((ToolStripMenuItem)sender).Text, StatusBarPanelServices.GetStatusBarProgressPanel(_majorFlexComponentParameters.StatusBar));
			}

			private Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>> CreateMainPanelLeftContextMenuStrip(string panelMenuId)
			{
				Require.That(panelMenuId == AreaServices.LeftPanelMenuId, $"I don't know how to create the panel menu with an ID of '{panelMenuId}', as I can only create one with an id of '{AreaServices.LeftPanelMenuId}'.");

				var contextMenuStrip = new ContextMenuStrip();
				var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>();
				var retVal = new Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>>(contextMenuStrip, menuItems);
				if (_reversalIndexRepository == null)
				{
					_reversalIndexRepository = _cache.ServiceLocator.GetInstance<IReversalIndexRepository>();
				}
				var allInstancesInRepository = _reversalIndexRepository.AllInstances().ToDictionary(rei => rei.Guid);
				foreach (var rei in allInstancesInRepository.Values)
				{
					var newMenuItem = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, ReversalIndex_Menu_Clicked, rei.ChooserNameTS.Text);
					newMenuItem.Tag = rei;
					if (_currentReversalIndex == rei)
					{
						newMenuItem.Checked = true;
					}
				}

				return retVal;
			}

			private void ReversalIndex_Menu_Clicked(object sender, EventArgs e)
			{
				var contextMenuItem = (ToolStripMenuItem)sender;
				_currentReversalIndex = (IReversalIndex)contextMenuItem.Tag;
				_propertyTable.SetProperty(LanguageExplorerConstants.ReversalIndexGuid, _currentReversalIndex.Guid.ToString(), true, settingsGroup: SettingsGroup.LocalSettings);
				((IReversalRecordList)_recordList).ChangeOwningObjectIfPossible();
			}

			private Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>> Create_mnuDataTree_MoveMainReversalPOS(ISlice slice, ContextMenuName contextMenuId)
			{
				Require.That(contextMenuId == ContextMenuName.mnuDataTree_MoveMainReversalPOS, $"Expected argument value of '{ContextMenuName.mnuDataTree_MoveMainReversalPOS.ToString()}', but got '{contextMenuId.ToString()}' instead.");

				// Start: <menu id="mnuDataTree_MoveMainReversalPOS">
				var contextMenuStrip = new ContextMenuStrip
				{
					Name = ContextMenuName.mnuDataTree_MoveMainReversalPOS.ToString()
				};
				var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>(4);

				/*
				  <item command="CmdDataTree_Move_MoveReversalPOS" /> // Shared locally
						<command id="CmdDataTree_Move_MoveReversalPOS" label="Move Category..." message="MoveReversalPOS">
						  <!--<parameters field="SubPossibilities" className="PartOfSpeech"/>-->
						</command>
				*/
				var currentPartOfSpeech = _recordList.CurrentObject as IPartOfSpeech;
				var enabled = _list.ReallyReallyAllPossibilities.Count > 1;
				var menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, MoveReversalPOS_Clicked, ListResources.Move_Category);
				menu.Enabled = enabled;
				menu.Tag = currentPartOfSpeech;

				/*
				  <item label="-" translate="do not translate" />
				*/
				ToolStripMenuItemFactory.CreateToolStripSeparatorForContextMenuStrip(contextMenuStrip);

				/*
				  <item command="CmdDataTree_Merge_MergeReversalPOS" /> // Shared locally
					<command id="CmdDataTree_Merge_MergeReversalPOS" label="Merge Category into..." message="MergeReversalPOS" />
				*/
				menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, MergeReversalPOS_Clicked, AreaServices.GetMergeMenuText(enabled, ListResources.Merge_Category_into));
				menu.Enabled = enabled;
				menu.Tag = currentPartOfSpeech;

				/*
				  <item command="CmdDataTree_Delete_ReversalSubPOS" />
					<command id="CmdDataTree_Delete_ReversalSubPOS" label="Delete this Category and any Subcategories" message="DataTreeDelete" icon="Delete">
					  <parameters field="SubPossibilities" className="PartOfSpeech" />
					</command> Delete_this_Category_and_any_Subcategories
				*/
				AreaServices.CreateDeleteMenuItem(menuItems, contextMenuStrip, slice, ListResources.Delete_this_Category_and_any_Subcategories, _majorFlexComponentParameters.SharedEventHandlers.GetEventHandler(Command.CmdDataTreeDelete));

				// End: <menu id="mnuDataTree_MoveMainReversalPOS">

				return new Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>>(contextMenuStrip, menuItems);
			}

			private Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>> Create_mnuDataTree_MoveReversalPOS(ISlice slice, ContextMenuName contextMenuId)
			{
				Require.That(contextMenuId == ContextMenuName.mnuDataTree_MoveReversalPOS, $"Expected argument value of '{ContextMenuName.mnuDataTree_MoveReversalPOS.ToString()}', but got '{contextMenuId.ToString()}' instead.");

				// Start: <menu id="mnuDataTree_MoveReversalPOS">
				var contextMenuStrip = new ContextMenuStrip
				{
					Name = ContextMenuName.mnuDataTree_MoveReversalPOS.ToString()
				};
				var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>(5);

				/*
				  <item command="CmdDataTree_Move_MoveReversalPOS" /> // Shared locally
				*/
				var enabled = _list.ReallyReallyAllPossibilities.Count > 1;
				var menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, MoveReversalPOS_Clicked, ListResources.Move_Category);
				menu.Enabled = _list.ReallyReallyAllPossibilities.Count > 1;

				using (var imageHolder = new DictionaryConfiguration.ImageHolder())
				{
					/*
						<command id="CmdDataTree_Promote_ProReversalSubPOS" label="Promote" message="PromoteReversalSubPOS" icon="MoveLeft">
						  <parameters field="SubPossibilities" className="PartOfSpeech" />
						</command>
					*/
					ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, Promote_ReversalSubPOS_Clicked, AreaResources.Promote, image: imageHolder.smallCommandImages.Images[AreaServices.MoveLeftIndex]);
				}

				// <item label="-" translate="do not translate" />
				ToolStripMenuItemFactory.CreateToolStripSeparatorForContextMenuStrip(contextMenuStrip);

				// <item command="CmdDataTree_Merge_MergeReversalPOS" />
				menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, MergeReversalPOS_Clicked, AreaServices.GetMergeMenuText(enabled, ListResources.Merge_Category_into));
				menu.Enabled = enabled;

				// <item command="CmdDataTree_Delete_ReversalSubPOS" />
				AreaServices.CreateDeleteMenuItem(menuItems, contextMenuStrip, slice, ListResources.Delete_this_Category_and_any_Subcategories, _majorFlexComponentParameters.SharedEventHandlers.GetEventHandler(Command.CmdDataTreeDelete));

				// End: <menu id="mnuDataTree_MoveReversalPOS">

				return new Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>>(contextMenuStrip, menuItems);
			}

			private static IEnumerable<IPartOfSpeech> MergeOrMoveCandidates(IPartOfSpeech partOfSpeechCandidate)
			{
				var retval = new HashSet<IPartOfSpeech>();
				foreach (var partOfSpeech in partOfSpeechCandidate.OwningList.ReallyReallyAllPossibilities)
				{
					if (ReferenceEquals(partOfSpeechCandidate, partOfSpeech))
					{
						continue;
					}
					retval.Add((IPartOfSpeech)partOfSpeech);
				}
				return retval;
			}

			private void MoveReversalPOS_Clicked(object sender, EventArgs e)
			{
				var slice = _dataTree.CurrentSlice;
				if (slice == null)
				{
					return;
				}
				var currentPartOfSpeech = (IPartOfSpeech)slice.MyCmObject;
				var cache = _dataTree.Cache;
				var labels = MergeOrMoveCandidates(currentPartOfSpeech).Where(pos => !pos.SubPossibilitiesOS.Contains(currentPartOfSpeech))
					.Select(pos => ObjectLabel.CreateObjectLabelOnly(cache, pos, "ShortNameTSS", "best analysis")).ToList();

				IPartOfSpeech newOwner = null;
				using (var dlg = new SimpleListChooser(cache, null, PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider), labels, null, AreaResources.Category_to_move_to, null))
				{
					dlg.SetHelpTopic("khtpChoose-CategoryToMoveTo");
					if (dlg.ShowDialog() == DialogResult.OK)
					{
						var currentPOS = currentPartOfSpeech;
						newOwner = (IPartOfSpeech)dlg.ChosenOne.Object;
						UowHelpers.UndoExtension(AreaResources.Move_Reversal_Category, cache.ActionHandlerAccessor, () =>
						{
							newOwner.MoveIfNeeded(currentPOS); //important when an item is moved into it's own subcategory
							if (!newOwner.SubPossibilitiesOS.Contains(currentPOS)) //this is also prevented in the interface, but I'm paranoid
							{
								newOwner.SubPossibilitiesOS.Add(currentPOS);
							}
						});
						_recordList.JumpToRecord(newOwner.MainPossibility.Hvo);
					}
				}
				if (newOwner != null)
				{
					_recordList.JumpToRecord(newOwner.MainPossibility.Hvo);
				}
			}

			private void MergeReversalPOS_Clicked(object sender, EventArgs e)
			{
				var slice = _dataTree.CurrentSlice;
				if (slice == null)
				{
					return;
				}
				var currentPartOfSpeech = (IPartOfSpeech)slice.MyCmObject;
				var cache = _dataTree.Cache;
				var labels = MergeOrMoveCandidates(currentPartOfSpeech).Select(pos => ObjectLabel.CreateObjectLabelOnly(cache, pos, "ShortNameTSS", "best analysis")).ToList();
				IPartOfSpeech survivor = null;
				using (var dlg = new SimpleListChooser(cache, null, PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider), labels, null, AreaResources.Category_to_merge_into, null))
				{
					dlg.SetHelpTopic("khtpMergeCategories");
					if (dlg.ShowDialog() == DialogResult.OK)
					{
						var currentPOS = currentPartOfSpeech;
						survivor = (IPartOfSpeech)dlg.ChosenOne.Object;
						// Pass false to MergeObject, since we really don't want to merge the string info.
						UowHelpers.UndoExtension(AreaResources.Merge_Reversal_Category, cache.ActionHandlerAccessor, () => survivor.MergeObject(currentPOS, false));
					}
				}
				if (survivor != null)
				{
					_recordList.JumpToRecord(survivor.MainPossibility.Hvo);
				}
			}

			private void Promote_ReversalSubPOS_Clicked(object sender, EventArgs e)
			{
				var slice = _dataTree.CurrentSlice;
				if (slice == null)
				{
					return;
				}
				var cache = _dataTree.Cache;
				var currentPartOfSpeech = (ICmPossibility)slice.MyCmObject;
				var newOwner = currentPartOfSpeech.Owner.Owner;
				switch (newOwner.ClassID)
				{
					default:
						throw new ArgumentException("Illegal class.");
					case PartOfSpeechTags.kClassId:
						UowHelpers.UndoExtension(AreaResources.Promote, cache.ActionHandlerAccessor, () => ((IPartOfSpeech)newOwner).SubPossibilitiesOS.Add(currentPartOfSpeech));
						break;
					case CmPossibilityListTags.kClassId:
						UowHelpers.UndoExtension(AreaResources.Promote, cache.ActionHandlerAccessor, () => ((ICmPossibilityList)newOwner).PossibilitiesOS.Add(currentPartOfSpeech));
						break;
				}
			}

			private Tuple<bool, bool> CanCmdDataTree_Insert_POS_SubPossibilities => new Tuple<bool, bool>(true, _recordList.CurrentObject != null);

			#region Implementation of IDisposable
			private bool _isDisposed;

			~ReversalIndexPosEditMenuHelper()
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
					MainPanelMenuContextMenuFactory.Dispose();
					_recordBrowseView.ContextMenuStrip?.Dispose();
					_recordBrowseView.ContextMenuStrip = null;
				}
				_majorFlexComponentParameters = null;
				_currentReversalIndex = null;
				_list = null;
				_recordList = null;
				_dataTree = null;
				_cache = null;
				_reversalIndexRepository = null;
				_recordBrowseView = null;
				_propertyTable = null;
				MainPanelMenuContextMenuFactory = null;
				_sharedListToolsUiWidgetMenuHelper = null;

				_isDisposed = true;
			}
			#endregion
		}
	}
}