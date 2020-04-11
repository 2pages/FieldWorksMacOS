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
using LanguageExplorer.Controls.PaneBar;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Resources;
using SIL.LCModel;
using SIL.LCModel.Application;

namespace LanguageExplorer.Areas.Grammar.Tools.CompoundRuleAdvancedEdit
{
	/// <summary>
	/// ITool implementation for the "compoundRuleAdvancedEdit" tool in the "grammar" area.
	/// </summary>
	[Export(LanguageExplorerConstants.GrammarAreaMachineName, typeof(ITool))]
	internal sealed class CompoundRuleAdvancedEditTool : ITool
	{
		private CompoundRuleAdvancedEditToolMenuHelper _toolMenuHelper;
		private const string CompoundRules = "compoundRules";
		private MultiPane _multiPane;
		private RecordBrowseActiveView _recordBrowseActiveView;
		private IRecordList _recordList;

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
			_toolMenuHelper.Dispose();
			MultiPaneFactory.RemoveFromParentAndDispose(majorFlexComponentParameters.MainCollapsingSplitContainer, ref _multiPane);
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
				_recordList = majorFlexComponentParameters.FlexComponentParameters.PropertyTable.GetValue<IRecordListRepositoryForTools>(LanguageExplorerConstants.RecordListRepository).GetRecordList(CompoundRules, majorFlexComponentParameters.StatusBar, FactoryMethod);
			}
			var root = XDocument.Parse(GrammarResources.CompoundRuleAdvancedEditToolParameters).Root;
			_recordBrowseActiveView = new RecordBrowseActiveView(root.Element("browseview").Element("parameters"), majorFlexComponentParameters.LcmCache, _recordList, majorFlexComponentParameters.UiWidgetController);
			var showHiddenFieldsPropertyName = UiWidgetServices.CreateShowHiddenFieldsPropertyName(MachineName);
			var dataTree = new DataTree(majorFlexComponentParameters.SharedEventHandlers, majorFlexComponentParameters.FlexComponentParameters.PropertyTable.GetValue(showHiddenFieldsPropertyName, false));
			_toolMenuHelper = new CompoundRuleAdvancedEditToolMenuHelper(majorFlexComponentParameters, this, dataTree, _recordBrowseActiveView, _recordList);
			var recordEditView = new RecordEditView(root.Element("recordview").Element("parameters"), XDocument.Parse(AreaResources.HideAdvancedListItemFields), majorFlexComponentParameters.LcmCache, _recordList, dataTree, majorFlexComponentParameters.UiWidgetController);
			var mainMultiPaneParameters = new MultiPaneParameters
			{
				Orientation = Orientation.Vertical,
				Area = Area,
				Id = "CompoundRuleItemsAndDetailMultiPane",
				ToolMachineName = MachineName
			};
			var recordEditViewPaneBar = new PaneBar();
			var panelButton = new PanelButton(majorFlexComponentParameters.FlexComponentParameters, null, showHiddenFieldsPropertyName, LanguageExplorerResources.ksShowHiddenFields, LanguageExplorerResources.ksShowHiddenFields)
			{
				Dock = DockStyle.Right
			};
			recordEditViewPaneBar.AddControls(new List<Control> { panelButton });
			_multiPane = MultiPaneFactory.CreateMultiPaneWithTwoPaneBarContainersInMainCollapsingSplitContainer(majorFlexComponentParameters.FlexComponentParameters, majorFlexComponentParameters.MainCollapsingSplitContainer,
				mainMultiPaneParameters, _recordBrowseActiveView, "Browse", new PaneBar(), recordEditView, "Details", recordEditViewPaneBar);
			// Too early before now.
			recordEditView.FinishInitialization();
		}

		/// <summary>
		/// Do whatever might be needed to get ready for a refresh.
		/// </summary>
		public void PrepareToRefresh()
		{
			_recordBrowseActiveView.BrowseViewer.BrowseView.PrepareToRefresh();
		}

		/// <summary>
		/// Finish the refresh.
		/// </summary>
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
		public string MachineName => LanguageExplorerConstants.CompoundRuleAdvancedEditMachineName;

		/// <summary>
		/// User-visible localized component name.
		/// </summary>
		public string UiName => StringTable.Table.LocalizeLiteralValue(LanguageExplorerConstants.CompoundRuleAdvancedEditUiName);

		#endregion

		#region Implementation of ITool

		/// <summary>
		/// Get the area for the tool.
		/// </summary>
		[field: Import(LanguageExplorerConstants.GrammarAreaMachineName)]
		public IArea Area { get; private set; }

		/// <summary>
		/// Get the image for the area.
		/// </summary>
		public Image Icon => Images.SideBySideView.SetBackgroundColor(Color.Magenta);

		#endregion

		private static IRecordList FactoryMethod(LcmCache cache, FlexComponentParameters flexComponentParameters, string recordListId, StatusBar statusBar)
		{
			Require.That(recordListId == CompoundRules, $"I don't know how to create a record list with an ID of '{recordListId}', as I can only create one with an id of '{CompoundRules}'.");
			/*
            <clerk id="compoundRules">
              <recordList owner="MorphologicalData" property="CompoundRules" />
            </clerk>
			*/
			return new RecordList(recordListId, statusBar,
				cache.ServiceLocator.GetInstance<ISilDataAccessManaged>(), true,
				new VectorPropertyParameterObject(cache.LanguageProject.MorphologicalDataOA, "CompoundRules", MoMorphDataTags.kflidCompoundRules));
		}

		private sealed class CompoundRuleAdvancedEditToolMenuHelper : IDisposable
		{
			private MajorFlexComponentParameters _majorFlexComponentParameters;
			private DataTree _dataTree;
			private RecordBrowseActiveView _recordBrowseActiveView;
			private IRecordList _recordList;
			private ToolStripMenuItem _menu;
			private IMoMorphData _moMorphData;
			private LcmCache _cache;

			internal CompoundRuleAdvancedEditToolMenuHelper(MajorFlexComponentParameters majorFlexComponentParameters, ITool tool, DataTree dataTree, RecordBrowseActiveView recordBrowseActiveView, IRecordList recordList)
			{
				Guard.AgainstNull(majorFlexComponentParameters, nameof(majorFlexComponentParameters));
				Guard.AgainstNull(tool, nameof(tool));
				Guard.AgainstNull(dataTree, nameof(dataTree));
				Guard.AgainstNull(recordBrowseActiveView, nameof(recordBrowseActiveView));
				Guard.AgainstNull(recordList, nameof(recordList));

				_majorFlexComponentParameters = majorFlexComponentParameters;
				_dataTree = dataTree;
				_recordBrowseActiveView = recordBrowseActiveView;
				_recordList = recordList;
				_cache = _majorFlexComponentParameters.LcmCache;
				_moMorphData = _cache.LanguageProject.MorphologicalDataOA;
				var toolUiWidgetParameterObject = new ToolUiWidgetParameterObject(tool);
				SetupUiWidgets(toolUiWidgetParameterObject);
				_majorFlexComponentParameters.UiWidgetController.AddHandlers(toolUiWidgetParameterObject);
				CreateBrowseViewContextMenu();
			}

			private void SetupUiWidgets(ToolUiWidgetParameterObject toolUiWidgetParameterObject)
			{
				var insertMenuDictionary = toolUiWidgetParameterObject.MenuItemsForTool[MainMenu.Insert];
				var insertToolBarDictionary = toolUiWidgetParameterObject.ToolBarItemsForTool[ToolBar.Insert];
				// <command id="CmdInsertEndocentricCompound" label="Headed Compound" message="InsertItemInVector" icon="endocompoundRule">
				insertMenuDictionary.Add(Command.CmdInsertEndocentricCompound, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(InsertEndocentricCompound_Clicked, () => UiWidgetServices.CanSeeAndDo));
				insertToolBarDictionary.Add(Command.CmdInsertEndocentricCompound, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(InsertEndocentricCompound_Clicked, () => UiWidgetServices.CanSeeAndDo));
				// <command id="CmdInsertExocentricCompound" label="Non-headed Compound" message="InsertItemInVector" icon="exocompoundRule">
				insertMenuDictionary.Add(Command.CmdInsertExocentricCompound, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(InsertInsertExocentricCompound_Clicked, () => UiWidgetServices.CanSeeAndDo));
				insertToolBarDictionary.Add(Command.CmdInsertExocentricCompound, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(InsertInsertExocentricCompound_Clicked, () => UiWidgetServices.CanSeeAndDo));
			}

			private void InsertEndocentricCompound_Clicked(object sender, EventArgs e)
			{
				UowHelpers.UndoExtension(GrammarResources.Insert_Headed_Compound, _majorFlexComponentParameters.LcmCache.ActionHandlerAccessor, () =>
				{
					_moMorphData.CompoundRulesOS.Add(_cache.ServiceLocator.GetInstance<IMoEndoCompoundFactory>().Create());
				});
			}

			private void InsertInsertExocentricCompound_Clicked(object sender, EventArgs e)
			{
				UowHelpers.UndoExtension(GrammarResources.Insert_Non_headed_Compound, _majorFlexComponentParameters.LcmCache.ActionHandlerAccessor, () =>
				{
					_moMorphData.CompoundRulesOS.Add(_cache.ServiceLocator.GetInstance<IMoExoCompoundFactory>().Create());
				});
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
				var currentObject = _recordList.CurrentObject;
				var lookupName = currentObject == null ? "MoCompoundRule" : currentObject.ClassName;
				_menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, CmdDeleteSelectedObject_Clicked, string.Format(LanguageExplorerResources.Delete_selected_0, StringTable.Table.GetString(lookupName, StringTable.ClassNames)));
				contextMenuStrip.Opening += ContextMenuStrip_Opening;

				// End: <menu id="mnuBrowseView" (partial) >
				_recordBrowseActiveView.ContextMenuStrip = contextMenuStrip;
			}

			private void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
			{
				_recordBrowseActiveView.ContextMenuStrip.Visible = !_recordList.HasEmptyList;
				if (!_recordBrowseActiveView.ContextMenuStrip.Visible)
				{
					return;
				}
				// Set to correct class
				_menu.ResetTextIfDifferent(string.Format(LanguageExplorerResources.Delete_selected_0, StringTable.Table.GetString(_recordList.CurrentObject.ClassName, StringTable.ClassNames)));
			}

			private void CmdDeleteSelectedObject_Clicked(object sender, EventArgs e)
			{
				var currentSlice = _dataTree.CurrentSlice;
				if (currentSlice == null)
				{
					_dataTree.GotoFirstSlice();
					currentSlice = _dataTree.CurrentSlice;
				}
				currentSlice.HandleDeleteCommand();
			}

			#region Implementation of IDisposable
			private bool _isDisposed;

			~CompoundRuleAdvancedEditToolMenuHelper()
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
					if (_recordBrowseActiveView?.ContextMenuStrip != null)
					{
						_recordBrowseActiveView.ContextMenuStrip.Opening -= ContextMenuStrip_Opening;
						_recordBrowseActiveView.ContextMenuStrip.Dispose();
						_recordBrowseActiveView.ContextMenuStrip = null;
					}
				}
				_majorFlexComponentParameters = null;
				_dataTree = null;
				_recordBrowseActiveView = null;
				_recordList = null;
				_menu = null;
				_moMorphData = null;

				_isDisposed = true;
			}
			#endregion
		}
	}
}