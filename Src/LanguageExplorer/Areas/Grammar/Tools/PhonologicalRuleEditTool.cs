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

namespace LanguageExplorer.Areas.Grammar.Tools
{
	/// <summary>
	/// ITool implementation for the "PhonologicalRuleEdit" tool in the "grammar" area.
	/// </summary>
	[Export(LanguageExplorerConstants.GrammarAreaMachineName, typeof(ITool))]
	internal sealed class PhonologicalRuleEditTool : ITool
	{
		private PhonologicalRuleEditToolMenuHelper _toolMenuHelper;
		private const string PhonologicalRules = "phonologicalRules";
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
			_recordBrowseActiveView = null;
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
				_recordList = majorFlexComponentParameters.FlexComponentParameters.PropertyTable.GetValue<IRecordListRepositoryForTools>(LanguageExplorerConstants.RecordListRepository).GetRecordList(PhonologicalRules, majorFlexComponentParameters.StatusBar, FactoryMethod);
			}
			var root = XDocument.Parse(GrammarResources.PhonologicalRuleEditToolParameters).Root;
			_recordBrowseActiveView = new RecordBrowseActiveView(root.Element("browseview").Element("parameters"), majorFlexComponentParameters.LcmCache, _recordList, majorFlexComponentParameters.UiWidgetController);
			var showHiddenFieldsPropertyName = UiWidgetServices.CreateShowHiddenFieldsPropertyName(MachineName);
			var dataTree = new DataTree(majorFlexComponentParameters.SharedEventHandlers, majorFlexComponentParameters.FlexComponentParameters.PropertyTable.GetValue(showHiddenFieldsPropertyName, false));
			_toolMenuHelper = new PhonologicalRuleEditToolMenuHelper(majorFlexComponentParameters, this, dataTree, _recordBrowseActiveView, _recordList);
			var recordEditView = new RecordEditView(root.Element("recordview").Element("parameters"), XDocument.Parse(LanguageExplorerResources.VisibilityFilter_All), majorFlexComponentParameters.LcmCache, _recordList, dataTree, majorFlexComponentParameters.UiWidgetController);
			var mainMultiPaneParameters = new MultiPaneParameters
			{
				Orientation = Orientation.Vertical,
				Area = Area,
				Id = "PhonologicalRuleItemsAndDetailMultiPane",
				ToolMachineName = MachineName
			};
			var recordEditViewPaneBar = new PaneBar();
			var panelButton = new PanelButton(majorFlexComponentParameters.FlexComponentParameters, null, showHiddenFieldsPropertyName, LanguageExplorerResources.ksShowHiddenFields, LanguageExplorerResources.ksShowHiddenFields)
			{
				Dock = DockStyle.Right
			};
			recordEditViewPaneBar.AddControls(new List<Control> { panelButton });
			_multiPane = MultiPaneFactory.CreateMultiPaneWithTwoPaneBarContainersInMainCollapsingSplitContainer(majorFlexComponentParameters.FlexComponentParameters,
				majorFlexComponentParameters.MainCollapsingSplitContainer, mainMultiPaneParameters, _recordBrowseActiveView, "Browse", new PaneBar(),
				recordEditView, "Details", recordEditViewPaneBar);
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
		public string MachineName => LanguageExplorerConstants.PhonologicalRuleEditMachineName;

		/// <summary>
		/// User-visible localized component name.
		/// </summary>
		public string UiName => StringTable.Table.LocalizeLiteralValue(LanguageExplorerConstants.PhonologicalRuleEditUiName);

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
		public Image Icon => SIL.FieldWorks.Resources.Images.SideBySideView.SetBackgroundColor(Color.Magenta);

		#endregion

		private static IRecordList FactoryMethod(LcmCache cache, FlexComponentParameters flexComponentParameters, string recordListId, StatusBar statusBar)
		{
			Require.That(recordListId == PhonologicalRules, $"I don't know how to create a record list with an ID of '{recordListId}', as I can only create one with an id of '{PhonologicalRules}'.");
			/*
			 // NB: How can Flex work when the clerk claims the owner is MorphologicalData, but the real world has it PhonologicalDataOA?
            <clerk id="phonologicalRules">
              <recordList owner="MorphologicalData" property="PhonologicalRules" />
            </clerk>
			*/
			return new RecordList(recordListId, statusBar, cache.ServiceLocator.GetInstance<ISilDataAccessManaged>(), true,
				new VectorPropertyParameterObject(cache.LanguageProject.PhonologicalDataOA, "PhonologicalRules", PhPhonDataTags.kflidPhonRules));
		}

		private sealed class PhonologicalRuleEditToolMenuHelper : IDisposable
		{
			private MajorFlexComponentParameters _majorFlexComponentParameters;
			private DataTree _dataTree;
			private RecordBrowseActiveView _recordBrowseActiveView;
			private IRecordList _recordList;
			private ToolStripMenuItem _menu;
			private IPhPhonData _phPhonData;

			internal PhonologicalRuleEditToolMenuHelper(MajorFlexComponentParameters majorFlexComponentParameters, ITool tool, DataTree dataTree, RecordBrowseActiveView recordBrowseActiveView, IRecordList recordList)
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
				_phPhonData = _majorFlexComponentParameters.LcmCache.LanguageProject.PhonologicalDataOA;
				SetupUiWidgets(tool);
				CreateBrowseViewContextMenu();
			}

			private void SetupUiWidgets(ITool tool)
			{
				var toolUiWidgetParameterObject = new ToolUiWidgetParameterObject(tool);
				var insertMenuDictionary = toolUiWidgetParameterObject.MenuItemsForTool[MainMenu.Insert];
				var insertToolBarDictionary = toolUiWidgetParameterObject.ToolBarItemsForTool[ToolBar.Insert];
				UiWidgetServices.InsertPair(insertToolBarDictionary, insertMenuDictionary, Command.CmdInsertPhRegularRule, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CmdInsertPhRegularRule_Click, () => UiWidgetServices.CanSeeAndDo));
				UiWidgetServices.InsertPair(insertToolBarDictionary, insertMenuDictionary, Command.CmdInsertPhMetathesisRule, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(CmdInsertPhMetathesisRule_Click, () => UiWidgetServices.CanSeeAndDo));
				_majorFlexComponentParameters.UiWidgetController.AddHandlers(toolUiWidgetParameterObject);
			}

			private void CmdInsertPhRegularRule_Click(object sender, EventArgs e)
			{
				/*
			<command id="CmdInsertPhRegularRule" label="Phonological Rule" message="InsertItemInVector" icon="environment">
				<params className="PhRegularRule" />
			</command>
				*/
				IPhRegularRule newbie = null;
				UowHelpers.UndoExtension(GrammarResources.Insert_Phonological_Rule, _majorFlexComponentParameters.LcmCache.ActionHandlerAccessor, () =>
				{
					newbie = _majorFlexComponentParameters.LcmCache.ServiceLocator.GetInstance<IPhRegularRuleFactory>().Create();
					_phPhonData.PhonRulesOS.Add(newbie);
				});
				_recordList.JumpToRecord(newbie.Hvo);
			}

			private void CmdInsertPhMetathesisRule_Click(object sender, EventArgs e)
			{
				/*
				<command id="CmdInsertPhMetathesisRule" label="Metathesis Rule" message="InsertItemInVector" icon="metathesis">
					<params className="PhMetathesisRule" />
				</command>
				*/
				IPhMetathesisRule newbie = null;
				UowHelpers.UndoExtension(GrammarResources.Insert_Metathesis_Rule, _majorFlexComponentParameters.LcmCache.ActionHandlerAccessor, () =>
				{
					newbie = _majorFlexComponentParameters.LcmCache.ServiceLocator.GetInstance<IPhMetathesisRuleFactory>().Create();
					_phPhonData.PhonRulesOS.Add(newbie);
				});
				_recordList.JumpToRecord(newbie.Hvo);
			}

			private void CreateBrowseViewContextMenu()
			{
				// The actual menu declaration has a gazillion menu items, but only two of them are seen in this tool (plus the separator).
				// Start: <menu id="mnuBrowseView" (partial) >
				var contextMenuStrip = new ContextMenuStrip
				{
					Name = ContextMenuName.mnuBrowseView
				};
				var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>(1);
				// <command id="CmdDeleteSelectedObject" label="Delete selected {0}" message="DeleteSelectedItem"/>
				_menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, CmdDeleteSelectedObject_Clicked, string.Format(LanguageExplorerResources.Delete_selected_0, StringTable.Table.GetString("PhSegmentRule", StringTable.ClassNames)));
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

			~PhonologicalRuleEditToolMenuHelper()
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
				_phPhonData = null;

				_isDisposed = true;
			}
			#endregion
		}
	}
}