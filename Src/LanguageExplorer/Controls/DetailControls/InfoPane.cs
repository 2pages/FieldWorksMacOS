// Copyright (c) 2010-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml.Linq;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;

namespace LanguageExplorer.Controls.DetailControls
{
	/// <summary />
	public class InfoPane : UserControl, IFlexComponent, IInterlinearTabControl
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private Container components = null;
		private RecordEditView _xrev;
		private IRecordList _recordList;
		/// <summary>
		/// A key component to getting the InfoPane to work right, when it is selected first in the Conc tools.
		/// The provided RecordList would be deactivated, so nothing was shown in the main browse tool,
		/// and then nothing was displayed in any of the tabs, including this InfoPane control.
		/// </summary>
		private bool _createdLocally;

		#region Constructors, destructors, and such like methods.

		// This constructor is used by the Windows.Forms Form Designer.
		public InfoPane()
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();
		}

		#region Implementation of IPropertyTableProvider

		/// <summary>
		/// Placement in the IPropertyTableProvider interface lets FwApp call IPropertyTable.DoStuff.
		/// </summary>
		public IPropertyTable PropertyTable { get; private set; }

		#endregion

		#region Implementation of IPublisherProvider

		/// <summary>
		/// Get the IPublisher.
		/// </summary>
		public IPublisher Publisher { get; private set; }

		#endregion

		#region Implementation of ISubscriberProvider

		/// <summary>
		/// Get the ISubscriber.
		/// </summary>
		public ISubscriber Subscriber { get; private set; }

		#endregion

		#region Implementation of IFlexComponent

		/// <summary>
		/// Initialize a FLEx component with the basic interfaces.
		/// </summary>
		/// <param name="flexComponentParameters">Parameter object that contains the required three interfaces.</param>
		public void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
		{
			FlexComponentParameters.CheckInitializationValues(flexComponentParameters, new FlexComponentParameters(PropertyTable, Publisher, Subscriber));

			PropertyTable = flexComponentParameters.PropertyTable;
			Publisher = flexComponentParameters.Publisher;
			Subscriber = flexComponentParameters.Subscriber;
		}

		#endregion

		/// <summary>
		/// Initialize the pane with a record list. (It already has the cache.)
		/// </summary>
		internal void Initialize(ISharedEventHandlers sharedEventHandlers, StatusBar statusBar, IRecordList recordList, UiWidgetController uiWidgetController)
		{
			if (_xrev != null)
			{
				// Already done
				return;
			}
			_recordList = recordList;
			if (_recordList.GetType().Name != LanguageExplorerConstants.InterlinearTextsRecordList)
			{
				// Don't use the one handed to us by the main interlinear edit tool,
				// but create a new one for the InfoPane to use.
				// Otherwise, the main one would be deactivated, and the tabs in InterlinMaster wouldn't show the selected wordform,
				// since the main browse view wouldn't be populated.
				_createdLocally = true;
				_recordList = PropertyTable.GetValue<IRecordListRepositoryForTools>(LanguageExplorerConstants.RecordListRepository).GetRecordList(LanguageExplorerConstants.InterlinearTextsRecordList, statusBar, LanguageExplorerServices.InterlinearTextsForInfoPaneFactoryMethod);
				// Activate it manually, but do not hand it to the record list repository to claim as active.
				// Otherwise, that deactivates the 'recordList' that is handed in, which is a bad thing!
				_recordList.ActivateUI(false);
			}
			_xrev = new InterlinearTextsRecordEditView(this, new XElement("parameters", new XAttribute("layout", "FullInformation"), new XAttribute("treeBarAvailability", "NotMyBusiness")), sharedEventHandlers, Cache, _recordList, uiWidgetController);
			_xrev.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
			_xrev.Dock = DockStyle.Fill;
			Controls.Add(_xrev);
			DisplayCurrentRoot();
			_xrev.FinishInitialization();
		}

		/// <summary>
		/// Check whether the pane has been initialized.
		/// </summary>
		internal bool IsInitialized => PropertyTable != null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****************** Missing Dispose() call for " + GetType().Name + ". ******************");
			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}

			if (disposing)
			{
				components?.Dispose();
				_xrev?.Dispose();
				if (_createdLocally)
				{
					PropertyTable.GetValue<IRecordListRepositoryForTools>(LanguageExplorerConstants.RecordListRepository).RemoveRecordList(_recordList);
					_recordList.Dispose();
					_createdLocally = false;
				}
			}
			Cache = null;
			_xrev = null;
			PropertyTable = null;
			Publisher = null;
			Subscriber = null;
			_recordList = null;

			base.Dispose(disposing);
		}

		#endregion // Constructors, destructors, and suchlike methods.

		#region Component Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			ComponentResourceManager resources = new ComponentResourceManager(typeof(InfoPane));
			this.SuspendLayout();
			//
			// InfoPane
			//
			this.Name = "InfoPane";
			resources.ApplyResources(this, "$this");
			this.ResumeLayout(false);

		}
		#endregion

		#region IInterlinearTabControl Members

		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public LcmCache Cache { get; set; }

		#endregion

		#region IChangeRootObject Members

		public void SetRoot(int hvo)
		{
			CurrentRootHvo = hvo;
			if (_xrev != null)
			{
				DisplayCurrentRoot();
			}
		}

		#endregion

		private void DisplayCurrentRoot()
		{
			if (CurrentRootHvo > 0)
			{
				_xrev.MyDataTree.Visible = true;
				var repo = Cache.ServiceLocator.GetInstance<ICmObjectRepository>();
				ICmObject root;
				// JohnT: I don't know why this is done at all. Therefore I made a minimal change rather than removing it
				// altogether. If someone knows why it sometimes needs doing, please comment. Or if you know why it once did
				// and it no longer applies, please remove it. I added the test that the record list is not aleady looking
				// at this object to suppress switching back to the raw text pane when clicking on the Info pane of an empty text.
				// (FWR-3180)
				if (repo.TryGetObject(CurrentRootHvo, out root) && root is IStText && _xrev.MyRecordList.CurrentObjectHvo != CurrentRootHvo)
				{
					_xrev.MyRecordList.JumpToRecord(CurrentRootHvo);
				}
			}
			else if (CurrentRootHvo == 0)
			{
				_xrev.MyDataTree.Visible = false;
			}
		}

		internal int CurrentRootHvo { get; private set; }

		private sealed class InterlinearTextsRecordEditView : RecordEditView
		{
			internal InterlinearTextsRecordEditView(InfoPane infoPane, XElement configurationParametersElement, ISharedEventHandlers sharedEventHandlers, LcmCache cache, IRecordList recordList, UiWidgetController uiWidgetController)
				: base(configurationParametersElement, XDocument.Parse(LanguageExplorerResources.VisibilityFilter_All), cache, recordList, new StTextDataTree(infoPane, sharedEventHandlers, cache), uiWidgetController)
			{
			}

			#region Overrides of RecordEditView
			/// <summary>
			/// Initialize a FLEx component with the basic interfaces.
			/// </summary>
			/// <param name="flexComponentParameters">Parameter object that contains the required three interfaces.</param>
			public override void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
			{
				base.InitializeFlexComponent(flexComponentParameters);

				ReadParameters();
				SetupDataContext();
				ShowRecord();
			}
			#endregion


			private sealed class StTextDataTree : DataTree
			{
				private InfoPane InfoPane { get; }

				internal StTextDataTree(InfoPane infoPane, ISharedEventHandlers sharedEventHandlers, LcmCache cache)
					: base(sharedEventHandlers, false)
				{
					InfoPane = infoPane;
					Cache = cache;
					InitializeBasic(cache, false);
					InitializeComponent();
				}

				public override void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
				{
					base.InitializeFlexComponent(flexComponentParameters);

					// Set up Slice menu: "mnuTextInfo_Notebook"
					DataTreeSliceContextMenuParameterObject.LeftEdgeContextMenuFactory.RegisterLeftEdgeContextMenuCreatorMethod(ContextMenuName.mnuTextInfo_Notebook, Create_mnuTextInfo_Notebook);
				}

				private Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>> Create_mnuTextInfo_Notebook(ISlice slice, ContextMenuName contextMenuId)
				{
					Require.That(contextMenuId == ContextMenuName.mnuTextInfo_Notebook, $"Expected argument value of '{ContextMenuName.mnuTextInfo_Notebook}', but got '{contextMenuId}' instead.");

					// Start: <menu id="mnuTextInfo_Notebook">

					var contextMenuStrip = new ContextMenuStrip
					{
						Name = ContextMenuName.mnuTextInfo_Notebook
					};
					var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>(1);
					// <item command="CmdJumpToNotebook"/>
					ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, JumpToNotebook_Clicked, LanguageExplorerResources.Show_Record_in_Notebook);

					// End: <menu id="mnuTextInfo_Notebook">

					return new Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>>(contextMenuStrip, menuItems);
				}

				private void JumpToNotebook_Clicked(object sender, EventArgs e)
				{
					/*
					<command id="CmdJumpToNotebook" label="Show Record in Notebook" message="JumpToTool">
						<parameters tool="notebookEdit" className="RnGenericRecord"/>
					</command>
					*/
					var currentObject = CurrentSlice.MyCmObject;
					if (currentObject is IText)
					{
						currentObject = ((IText)currentObject).AssociatedNotebookRecord;
					}
					LinkHandler.PublishFollowLinkMessage(Publisher, new FwLinkArgs(LanguageExplorerConstants.NotebookEditToolMachineName, currentObject.Guid));
				}

				protected override void SetDefaultCurrentSlice(bool suppressFocusChange)
				{
					base.SetDefaultCurrentSlice(suppressFocusChange);
					// currently we always want the focus in the first slice by default,
					// since the user cannot control the governing browse view with a cursor.
					if (!suppressFocusChange && CurrentSlice == null)
					{
						FocusFirstPossibleSlice();
					}
				}

				public override void ShowObject(ICmObject root, string layoutName, string layoutChoiceField, ICmObject descendant, bool suppressFocusChange)
				{
					if (InfoPane != null && InfoPane.CurrentRootHvo == 0)
					{
						return;
					}
					var showObj = root;
					ICmObject stText;
					if (root.ClassID == CmBaseAnnotationTags.kClassId)  // RecordList is tracking the annotation
					{
						// This pane, as well as knowing how to work with a record list of Texts, knows
						// how to work with one of CmBaseAnnotations, that is, a list of occurrences of
						// a word.
						var cba = (ICmBaseAnnotation)root;
						var cmoPara = cba.BeginObjectRA;
						stText = cmoPara.Owner;
						showObj = stText;
					}
					else
					{
						stText = root;
					}
					if (stText.OwningFlid == TextTags.kflidContents)
					{
						showObj = stText.Owner;
					}
					base.ShowObject(showObj, layoutName, layoutChoiceField, showObj, suppressFocusChange);
				}
			}
		}
	}
}