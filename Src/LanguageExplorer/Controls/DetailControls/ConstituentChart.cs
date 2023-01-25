// Copyright (c) 2015-2022 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;
using SIL.Xml;

namespace LanguageExplorer.Controls.DetailControls
{
	/// <summary>
	/// A constituent chart is used to organize words (and perhaps eventually somehow morphemes)
	/// into a table where rows roughly correspond to clauses and columns to key parts of a clause.
	/// A typical chart has two pre-nuclear columns, three or four nuclear ones (SVO and perhaps indirect
	/// object) and one or two post-nuclear ones.
	/// </summary>
	internal partial class ConstituentChart : UserControl, IInterlinConfigurable, ISetupLineChoices, IHandleBookmark, IFlexComponent, IStyleSheet, IInterlinearConfigurator
	{
		#region Member Variables
		private InterlinRibbon m_ribbon;
		// Buttons for moving ribbon text into a specific column
		private readonly List<Button> m_MoveHereButtons = new List<Button>();
		// Popups associated with each 'MoveHere' button
		private readonly List<Button> m_ContextMenuButtons = new List<Button>();
		private bool m_fContextMenuButtonsEnabled;
		private IDsConstChart m_chart;
		private ICmPossibility m_template;
		private ICmPossibility[] m_allColumns;
		private readonly ConstituentChartLogic m_logic;
		private Panel m_templateSelectionPanel;
		private Panel m_buttonRow;
		private Panel m_bottomStuff;
		// m_buttonRow above m_ribbon
		private SplitContainer m_topBottomSplit;
		private readonly List<ChartHeaderView> m_headerColGroups = new List<ChartHeaderView>();
		private ChartHeaderView m_headerMainCols;
		// width of each table cell in millipoints
		private float m_dxpInch;
		private int[] m_columnWidths;
		private bool m_fInColWidthChanged;
		// left of each column in pixels. First is zero. Count is one MORE than number
		// of columns, so last position is width of window (right of last column).
		// controls the popup help items for the Constituent Chart Form
		private ToolTip m_toolTip;
		private InterAreaBookmark m_bookmark;
		private readonly ILcmServiceLocator m_serviceLocator;
		private XmlNode m_configurationParameters;
		private ISharedEventHandlers _sharedEventHandlers;
		private UiWidgetController _uiWidgetController;
		private bool _interlineMasterWantsExportDiscourseChartMenu;
		private ContextMenuStrip _contextMenuStrip;
		private InterlinVc Vc { get; }

		#endregion

		/// <summary />
		internal ConstituentChart(LcmCache cache, ISharedEventHandlers sharedEventHandlers, UiWidgetController uiWidgetController = null, ConstituentChartLogic logic = null)
		{
			if (logic == null)
			{
				// Tests will supply one, but normal use is to use this version.
				logic = new ConstituentChartLogic(cache);
			}
			_sharedEventHandlers = sharedEventHandlers;
			// Tests don't have uiWidgetController, so it will be null
			_uiWidgetController = uiWidgetController;
			_sharedEventHandlers.Add(Command.CmdRepeatLastMoveLeft, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(RepeatLastMoveLeft_Clicked, () => CanRepeatLastMoveLeft));
			_sharedEventHandlers.Add(Command.CmdRepeatLastMoveRight, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(RepeatLastMoveRight_Clicked, () => CanRepeatLastMoveRight));
			Cache = cache;
			m_serviceLocator = Cache.ServiceLocator;
			m_logic = logic;
			ForEditing = true;
			AccessibleName = "Constituent Chart";
			Name = "ConstituentChart";
			Vc = new InterlinVc(Cache);
			ConfigPropName = "InterlinConfig_v3_Edit_ConstituentChart";
			OldConfigPropName = "InterlinConfig_v2_Edit_ConstituentChart";
		}

		internal bool InterlineMasterWantsExportDiscourseChartDiscourseChartMenu
		{
			get => _interlineMasterWantsExportDiscourseChartMenu;
			set
			{
				_interlineMasterWantsExportDiscourseChartMenu = value;
				if (_interlineMasterWantsExportDiscourseChartMenu)
				{
					// Add handler stuff.
					var userController = new UserControlUiWidgetParameterObject(this);
					userController.MenuItemsForUserControl[MainMenu.File].Add(Command.CmdExportDiscourseChart, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(ExportDiscourseChart_Click, () => CanShowExportDiscourseChartMenu));
					var dataMenuDictionary = userController.MenuItemsForUserControl[MainMenu.Data];
					dataMenuDictionary.Add(Command.CmdRepeatLastMoveLeft, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(RepeatLastMoveLeft_Clicked, () => CanRepeatLastMoveLeft));
					dataMenuDictionary.Add(Command.CmdRepeatLastMoveRight, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(RepeatLastMoveRight_Clicked, () => CanRepeatLastMoveRight));
					_uiWidgetController.AddHandlers(userController);
				}
				else
				{
					// remove handler stuff.
					_uiWidgetController.RemoveUserControlHandlers(this);
				}
			}
		}

		private Tuple<bool, bool> CanShowExportDiscourseChartMenu => new Tuple<bool, bool>(true, InterlineMasterWantsExportDiscourseChartDiscourseChartMenu && m_hvoRoot != 0 && m_chart != null && Body != null && m_logic != null);

		private void ExportDiscourseChart_Click(object sender, EventArgs e)
		{
			using (var dlg = new DiscourseExportDialog(m_chart.Hvo, Body.Vc, m_logic.WsLineNumber))
			{
				dlg.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
				dlg.ShowDialog(this);
			}
		}

		#region Implementation of all interfaces

		#region Implementation of IInterlinearTabControl
		public LcmCache Cache { get; set; }
		#endregion

		#region Implementation of IInterlinConfigurable (PropertyTable here also implements IPropertyTableProvider)
		public IPropertyTable PropertyTable { get; set; }
		public IVwRootBox Rootb { get; set; }
		#endregion

		#region Implementation of IChangeRootObject
		/// <summary>
		/// Set the root object.
		/// </summary>
		public void SetRoot(int hvo)
		{
			var oldTemplateHvo = 0;
			if (m_template != null)
			{
				oldTemplateHvo = m_template.Hvo;
			}
			// does it already have a chart? If not make one.
			m_chart = null;
			m_hvoRoot = hvo;
			if (m_hvoRoot == 0)
			{
				RootStText = null;
			}
			else
			{
				RootStText = (IStText)Cache.ServiceLocator.ObjectRepository.GetObject(hvo);
			}
			if (m_hvoRoot > 0)
			{
				DetectAndReportTemplateProblem();
				// Make sure text is parsed!
				if (RootStText.HasParagraphNeedingParse())
				{
					NonUndoableUnitOfWorkHelper.Do(RootStText.Cache.ActionHandlerAccessor, () => RootStText.LoadParagraphAnnotationsAndGenerateEntryGuessesIfNeeded(false));
				}
				// We need to make or set the chart before calling NextUnusedInput.
				FindAndCleanUpMyChart(m_hvoRoot);
				// Sets m_chart if it finds one for hvoStText
				if (m_chart == null)
				{
					CreateChartInNonUndoableUOW();
				}
				m_logic.Chart = m_chart;
				m_ribbon.CacheRibbonItems(m_logic.NextUnchartedInput(RootStText, kmaxWordforms).ToList());
				// Don't need PropChanged here, CacheRibbonItems handles it.
				if (m_logic.StTextHvo != 0 && m_hvoRoot != m_logic.StTextHvo)
				{
					EnableAllContextButtons();
					EnableAllMoveHereButtons();
					m_logic.ResetRibbonLimits();
					m_logic.CurrHighlightCells = null;
					// Should reset highlighting (w/PropChanged)
				}
				// Tell the ribbon whether it needs to display and select words Right to Left or not
				m_ribbon.SetRoot(m_hvoRoot);
				if (m_chart.TemplateRA == null)
				{
					// LT-8700: if original template is deleted we might need this
					m_chart.TemplateRA = Cache.LangProject.GetDefaultChartTemplate();
				}
				m_template = m_chart.TemplateRA;
				m_logic.StTextHvo = m_hvoRoot;
				m_allColumns = m_logic.AllMyColumns;
			}
			else
			{
				// no text, so no chart
				m_ribbon.SetRoot(0);
				m_logic.Chart = null;
				m_logic.StTextHvo = 0;
				m_allColumns = new ICmPossibility[0];
			}
			if (m_template != null && m_template.Hvo != oldTemplateHvo)
			{
				m_fInColWidthChanged = true;
				try
				{
					var headers = m_logic.ColumnsAndGroups.Headers;
					headers.Reverse();
					m_logic.MakeHeaderColsFor(m_headerMainCols, headers[0], headers.Count == 1);
					m_headerColGroups.ForEach(h => h.Dispose());
					m_headerColGroups.Clear();
					for (var i = 1; i < headers.Count; i++)
					{
						var headLevel = new ChartHeaderView(this) { Dock = DockStyle.Top, Height = 22 };
						headLevel.ColumnWidthChanged += m_headerMainCols_ColumnWidthChanged; // TODO (Hasso) 2022.03: register other event handlers (Layout, SizeChanged)?
						m_logic.MakeHeaderColsFor(headLevel, headers[i], headers.Count == i + 1);
						m_headerColGroups.Add(headLevel);
					}
					RebuildTopStuffUI();
					if (m_allColumns == new ICmPossibility[0])
						return;
					int ccolsWanted = m_allColumns.Length + ConstituentChartLogic.NumberOfExtraColumns;
					m_columnWidths = new int[ccolsWanted];
					ColumnPositions = new int[ccolsWanted + 1];
					// one extra for after the last column
					if (!RestoreColumnWidths())
					{
						SetDefaultColumnWidths();
					}
				}
				finally
				{
					m_fInColWidthChanged = false;
				}
			}

			// If necessary adjust number of buttons
			if (m_MoveHereButtons.Count != m_allColumns.Length && hvo > 0)
			{
				SetupMoveHereButtonsToMatchTemplate();
			}
			SetHeaderColAndButtonWidths();
			BuildTemplatePanel();
			if (m_chart != null)
			{
				Body.SetRoot(m_chart.Hvo, m_allColumns, ChartIsRtL);
				GetAndScrollToBookmark();
			}
			else
			{
				Body.SetRoot(0, null, false);
			}
		}
		#endregion

		#region Implementation of ISetupLineChoices
		public bool ForEditing { get; set; }

		/// <summary>
		/// Retrieves the Line Choices from persistence, or otherwise sets them to a default option
		/// </summary>
		/// <param name="lineConfigPropName">The string key to retrieve Line Choices from the Property Table</param>
		/// <param name="mode">Should always be Chart for this override</param>
		public InterlinLineChoices SetupLineChoices(string lineConfigPropName, string oldPropName, InterlinMode mode)
		{
			ConfigPropName = lineConfigPropName;
			if (!TryRestoreLineChoices(out var lineChoices))
			{
				if (ForEditing)
				{
					lineChoices = EditableInterlinLineChoices.DefaultChoices(Cache.LangProject, WritingSystemServices.kwsVern, WritingSystemServices.kwsAnal);
					lineChoices.Mode = mode;
					lineChoices.SetStandardChartState();
				}
				else
				{
					lineChoices = InterlinLineChoices.DefaultChoices(Cache.LangProject, WritingSystemServices.kwsVern, WritingSystemServices.kwsAnal, mode);
				}
			}
			else if (ForEditing)
			{
				// just in case this hasn't been set for restored lines
				lineChoices.Mode = mode;
			}
			LineChoices = lineChoices;
			return LineChoices;
		}
		#endregion

		#region Implementation of IHandleBookmark
		/// <summary>
		/// This public version enables call by reflection from InterlinMaster of the internal CCBody
		/// method that selects (and scrolls to) the bookmarked location in the constituent chart.
		/// </summary>
		public void SelectBookmark(IStTextBookmark bookmark)
		{
			Body.SelectAndScrollToBookmark(bookmark as InterAreaBookmark);
		}
		#endregion

		#region Implementation of IStyleSheet

		/// <summary>
		/// Set/get the style sheet.
		/// </summary>
		public IVwStylesheet StyleSheet
		{
			get => Body.StyleSheet;
			set
			{
				Body.StyleSheet = value;
				var oldStyles = m_ribbon.StyleSheet;
				m_ribbon.StyleSheet = value;
				if (oldStyles != value)
				{
					m_ribbon.SelectFirstOccurence();
				}
				// otherwise, selection disappears.
			}
		}
		#endregion

		#region Implementation of IInterlinearConfigurator
		/// <summary>
		///  Launch the Configure interlinear dialog and deal with the results.
		/// </summary>
		void IInterlinearConfigurator.ConfigureInterlinear()
		{
			LineChoices = GetLineChoices();
			Vc.LineChoices = LineChoices;
			using (var dlg = new ConfigureInterlinDialog(Cache, PropertyTable, PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider), m_ribbon.Vc.LineChoices.Clone() as InterlinLineChoices))
			{
				if (dlg.ShowDialog(this) == DialogResult.OK)
				{
					UpdateForNewLineChoices(dlg.Choices);
				}
			}
		}
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

			m_logic.Init(PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider));
			// The BuildUIComponents() call has to be done before the "Body" use, otherwise it is null.
			// BuildUIComponents() used to be in the constructor, but that failed, because there was no PropertyTable yet.
			BuildUIComponents();
			var lineChoices = GetLineChoices();
			Body.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
			Body.LineChoices = lineChoices;
			m_ribbon.LineChoices = lineChoices;
		}

		#endregion

		#endregion

		/// <summary>
		/// This is for setting Vc.LineChoices even before we have a valid vc.
		/// </summary>
		private InterlinLineChoices LineChoices { get; set; }

		/// <summary>
		/// Persist the new line choices and
		/// Reconstruct the document based on the given newChoices for interlinear lines.
		/// </summary>
		private void UpdateForNewLineChoices(InterlinLineChoices newChoices)
		{
			LineChoices = newChoices;
			m_ribbon.Vc.LineChoices = newChoices;
			Body.LineChoices = newChoices;

			PersistAndDisplayChangedLineChoices();
		}

		private void PersistAndDisplayChangedLineChoices()
		{
			PropertyTable.SetProperty(ConfigPropName, m_ribbon.Vc.LineChoices.Persist(Cache.LanguageWritingSystemFactoryAccessor), true, settingsGroup: SettingsGroup.LocalSettings);
			PropertyTable.SetProperty(ConfigPropName, Body.LineChoices.Persist(Cache.LanguageWritingSystemFactoryAccessor), true, settingsGroup: SettingsGroup.LocalSettings);
			UpdateDisplayForNewLineChoices();
		}

		/// <summary>
		/// Do whatever is necessary to display new line choices.
		/// </summary>
		private void UpdateDisplayForNewLineChoices()
		{
			if (m_ribbon.RootBox == null || Body.RootBox == null)
			{
				return;
			}
			m_ribbon.RootBox.Reconstruct();
			Body.RootBox.Reconstruct();
		}

		private void BuildUIComponents()
		{
			SuspendLayout();

			m_topBottomSplit = new SplitContainer();
			m_topBottomSplit.Layout += SplitLayout;
			BuildBottomStuffUI();
			BuildTopStuffUI();
			m_topBottomSplit.Orientation = Orientation.Horizontal;
			Controls.Add(m_topBottomSplit);
			Dock = DockStyle.Fill;
			ResumeLayout();
		}

		private void SplitLayout(object sender, LayoutEventArgs e)
		{
			var container = (SplitContainer)sender;
			container.Width = Width;
			container.Height = Height;
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			// We don't want to know about column width changes until after we're initialized and have restored original widths.
			m_headerMainCols.ColumnWidthChanged += m_headerMainCols_ColumnWidthChanged;
		}

		protected override void OnLayout(LayoutEventArgs e)
		{
			m_topBottomSplit.SplitterMoved -= RibbonSizeChanged;
			//Call SplitLayout here to ensure Mono properly updates Splitter length
			SplitLayout(m_topBottomSplit, e);
			base.OnLayout(e);
			int splitterValue;
			// use a default property unless the property has been set
			if (PropertyTable.PropertyExists("constChartRibbonSize"))
			{
				// GetIntProperty will set the default value if it isn't set.
				// OnLayout will be called several times before the final correct values are available
				splitterValue = PropertyTable.GetValue("constChartRibbonSize", 100);
			}
			else
			{
				splitterValue = (int)(Height * .9);
			}

			//Mono makes SplitLayout calls while Splitter is moving so set default distance here
			m_topBottomSplit.SplitterDistance = splitterValue;
			m_topBottomSplit.SplitterMoved += RibbonSizeChanged;
		}

		/// <summary>
		/// Method called by Mediator to refresh view after Undoable UOW is completed
		/// Method name is defined by a mediator message posted in ConstituentChartLogic.changeTemplate_Click
		/// </summary>
		public virtual void OnTemplateChanged(string name)
		{
			SetRoot(m_hvoRoot);
		}

		private void BuildTopStuffUI()
		{
			Body = new ConstChartBody(m_logic, this)
			{
				Cache = Cache,
				Dock = DockStyle.Fill
			};
			// Seems to be right (cf BrowseViewer) but not ideal.
			m_headerMainCols = new ChartHeaderView(this)
			{
				Dock = DockStyle.Top,
				Height = 22
			};
			m_headerMainCols.Layout += m_headerMainCols_Layout;
			m_headerMainCols.SizeChanged += m_headerMainCols_SizeChanged;

			m_templateSelectionPanel = new Panel { Height = new Button().Height, Dock = DockStyle.Top, Width = 0 };
			m_templateSelectionPanel.Layout += TemplateSelectionPanel_Layout;

			RebuildTopStuffUI();
		}

		private void RebuildTopStuffUI()
		{
			m_topBottomSplit.Panel1.Controls.Clear();
			m_topBottomSplit.Panel1.Controls.AddRange(
				new Control[]
				{
					Body,
					m_headerMainCols
				});
			// ReSharper disable once CoVariantArrayConversion
			m_topBottomSplit.Panel1.Controls.AddRange(m_headerColGroups.ToArray());
			m_topBottomSplit.Panel1.Controls.Add(m_templateSelectionPanel);
		}

		private static void TemplateSelectionPanel_Layout(object sender, EventArgs e)
		{
			var panel = (Panel)sender;
			if (panel.Controls.Count == 0)
			{
				return;
			}
			var templateButton = panel.Controls[0];
			templateButton.SuspendLayout();
			templateButton.Width = new Button().Width * 2;
			templateButton.Left = panel.Width - templateButton.Width;
			templateButton.ResumeLayout();
		}

		private void BuildBottomStuffUI()
		{
			// fills the 'bottom stuff'
			m_ribbon = new InterlinRibbon(Cache, 0) { Dock = DockStyle.Fill };
			m_ribbon.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
			m_logic.Ribbon = m_ribbon;
			m_logic.Ribbon_Changed += OnLogicRibbonChanged;

			// Holds tooltip help for 'Move Here' buttons.
			// Set up the delays for the ToolTip.
			// Force the ToolTip text to be displayed whether or not the form is active.
			m_toolTip = new ToolTip { AutoPopDelay = 5000, InitialDelay = 1000, ReshowDelay = 500, ShowAlways = true };

			m_bottomStuff = m_topBottomSplit.Panel2;
			m_bottomStuff.SuspendLayout();

			m_buttonRow = new Panel { Height = new Button().Height, Dock = DockStyle.Top, BackColor = Color.FromKnownColor(KnownColor.ControlLight) };
			m_fContextMenuButtonsEnabled = true;
			m_buttonRow.Layout += m_buttonRow_Layout;

			m_bottomStuff.Controls.AddRange(new Control[] { m_ribbon, m_buttonRow });
			m_bottomStuff.ResumeLayout();
		}

		private void RibbonSizeChanged(object sender, EventArgs e)
		{
			PropertyTable.SetProperty("constChartRibbonSize", m_topBottomSplit.SplitterDistance, false);
		}

		private const int kmaxWordforms = 20;

		protected int GetWidth(string text, Font fnt)
		{
			using (var g = Graphics.FromHwnd(Handle))
			{
				return (int)g.MeasureString(text, fnt).Width + 1;
			}
		}

		/// <summary>
		/// Return the left of each column, starting with zero for the first, and containing
		/// one extra value for the extreme right.
		/// N.B. This is a display thing, so RTL script will make it logically backwards from LTR script.
		/// </summary>
		internal int[] ColumnPositions { get; private set; }

		internal Tuple<bool, bool> CanRepeatLastMoveLeft => new Tuple<bool, bool>(true, m_logic.CanRepeatLastMove);

		/// <summary>
		/// Repeat move left handler.
		/// </summary>
		private void RepeatLastMoveLeft_Clicked(object sender, EventArgs e)
		{
			if (ChartIsRtL)
			{
				m_logic.RepeatLastMoveForward();
			}
			else
			{
				m_logic.RepeatLastMoveBack();
			}
		}

		internal Tuple<bool, bool> CanRepeatLastMoveRight => new Tuple<bool, bool>(true, m_logic.CanRepeatLastMove);

		/// <summary>
		/// Repeat move right handler.
		/// </summary>
		private void RepeatLastMoveRight_Clicked(object sender, EventArgs e)
		{
			if (ChartIsRtL)
			{
				m_logic.RepeatLastMoveBack();
			}
			else
			{
				m_logic.RepeatLastMoveForward();
			}
		}

		private void m_headerMainCols_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
		{
			if (m_fInColWidthChanged)
			{
				return;
			}
			m_fInColWidthChanged = true;
			try
			{
				var icolChanged = e.ColumnIndex;
				var ccol = m_headerMainCols.Controls.Count;
				var totalWidth = 0;
				var maxWidth = MaxUseableWidth();
				foreach (Control ch in m_headerMainCols.Controls)
				{
					totalWidth += ch.Width + 0;
				}
				if (totalWidth > maxWidth)
				{
					var delta = totalWidth - maxWidth;
					var remainingCols = ccol - icolChanged - 1;
					var icolAdjust = icolChanged + 1;
					while (remainingCols > 0)
					{
						var deltaThis = delta / remainingCols;
						m_headerMainCols[icolAdjust].Width -= deltaThis;
						delta -= deltaThis;
						icolAdjust++;
						remainingCols--;
					}
				}
				if (m_columnWidths == null)
				{
					m_columnWidths = new int[m_allColumns.Length + 1];
				}
			}
			finally
			{
				m_fInColWidthChanged = false;
			}
			GetColumnWidths();
			// Transfer from header to variables.
			PersistColumnWidths();
			// Now adjust everything else
			ComputeButtonWidths();
			Body.SetColWidths(m_columnWidths);
			m_headerMainCols.UpdatePositions();
		}

		private void m_headerMainCols_SizeChanged(object sender, EventArgs e)
		{
			SetHeaderColAndButtonWidths();
		}

		private void m_headerMainCols_Layout(object sender, LayoutEventArgs e)
		{
			SetHeaderColAndButtonWidths();
		}

		/// <summary />
		protected virtual void SetHeaderColAndButtonWidths()
		{
			// Do not change column widths until positions have been updated to represent template change
			// ColumnPositions should be one longer due to fence posting
			if (ColumnPositions != null && ColumnPositions.Length == m_headerMainCols.Controls.Count + 1)
			{
				m_fInColWidthChanged = true;
				try
				{
					for (var i = 0; i < m_headerMainCols.Controls.Count; i++)
					{
						var width = ColumnPositions[i + 1] - ColumnPositions[i];
						if (m_headerMainCols[i].Width != width)
						{
							m_headerMainCols[i].Width = width;
						}
						m_headerMainCols.UpdatePositions();
					}
				}
				finally
				{
					m_fInColWidthChanged = false;
				}
			}
			ComputeButtonWidths();
			if (m_columnWidths != null)
			{
				Body.SetColWidths(m_columnWidths);
			}
		}

		private int MpToPixelX(int dxmp)
		{
			EnsureDpiX();
			return (int)(dxmp * m_dxpInch / 72000);
		}

		private int PixelToMpX(int dx)
		{
			EnsureDpiX();
			return (int)(dx * 72000 / m_dxpInch);
		}

		private void EnsureDpiX()
		{
			if (m_dxpInch != 0F)
			{
				return;
			}

			using (var g = m_buttonRow.CreateGraphics())
			{
				m_dxpInch = g.DpiX;
			}
		}

		protected override void OnSizeChanged(EventArgs e)
		{
			if (m_columnWidths != null && m_chart != null && !HasPersistentColWidths)
			{
				SetDefaultColumnWidths();
				SetHeaderColAndButtonWidths();
			}
			base.OnSizeChanged(e);
		}

		private void SetDefaultColumnWidths()
		{
			if (ChartIsRtL)
			{
				SetDefaultColumnWidthsRtL();
				return;
			}
			var numColWidthMp = Body.NumColWidth;
			var numColWidth = MpToPixelX(numColWidthMp);
			m_columnWidths[0] = numColWidthMp;
			ColumnPositions[0] = 0;
			ColumnPositions[1] = numColWidth + 1;
			var maxWidth = MaxUseableWidth();
			var remainingWidth = maxWidth - numColWidth;
			// Evenly space all but the row number column.
			var remainingCols = m_allColumns.Length + ConstituentChartLogic.NumberOfExtraColumns - 1;
			var icol1 = 0;
			while (remainingCols > 0)
			{
				icol1++;
				var colWidth = remainingWidth / remainingCols;
				remainingWidth -= colWidth;
				remainingCols--;
				m_columnWidths[icol1] = PixelToMpX(colWidth);
				ColumnPositions[icol1 + 1] = ColumnPositions[icol1] + colWidth;
			}
		}

		private void SetDefaultColumnWidthsRtL()
		{
			// Same as SetDefaultColumnWidths(), but for Right to Left scripts
			var numColWidthMp = Body.NumColWidth;
			var numColWidth = MpToPixelX(numColWidthMp);
			ColumnPositions[0] = 0;
			var maxWidth = MaxUseableWidth();
			var remainingWidth = maxWidth - numColWidth;
			var totalColumns = m_allColumns.Length + ConstituentChartLogic.NumberOfExtraColumns;
			// Evenly space all but the row number column.
			var remainingCols = totalColumns - 1;
			var icol1 = -1;
			while (remainingCols > 0)
			{
				icol1++;
				var colWidth = remainingWidth / remainingCols;
				remainingWidth -= colWidth;
				remainingCols--;
				m_columnWidths[icol1] = PixelToMpX(colWidth);
				ColumnPositions[icol1 + 1] = ColumnPositions[icol1] + colWidth;
			}
			// Set row number column width
			icol1++;
			m_columnWidths[icol1] = numColWidthMp;
			ColumnPositions[icol1 + 1] = ColumnPositions[icol1] + numColWidth;
		}

		private int MaxUseableWidth()
		{
			var maxUsableWidth = Width;
			if (VerticalScroll.Visible)
			{
				maxUsableWidth -= SystemInformation.VerticalScrollBarWidth;
			}
			return maxUsableWidth;
		}

		/// Compute (or eventually retrieve from persistence) column widths,
		/// if not already known.
		private void GetColumnWidths()
		{
			if (m_allColumns == null)
			{
				return; // no cols, can't do anything useful.
			}
			if (m_headerMainCols != null && m_headerMainCols.Controls.Count == m_allColumns.Length + ConstituentChartLogic.NumberOfExtraColumns)
			{
				return;
			}
			// Take it from the headers if we have them set up already.
			m_columnWidths = new int[m_allColumns.Length + ConstituentChartLogic.NumberOfExtraColumns];
			ColumnPositions = new int[m_allColumns.Length + ConstituentChartLogic.NumberOfExtraColumns + 1];
			var ccol = m_headerMainCols.Controls.Count;
			for (var icol = 0; icol < ccol; icol++)
			{
				var width = m_headerMainCols[icol].Width;
				// The column seems to be really one pixel wider than the column width of the header,
				// possibly because of the boundary line width.
				ColumnPositions[icol + 1] = ColumnPositions[icol] + width + 0;
				m_columnWidths[icol] = PixelToMpX(width);
			}
		}

		/// <summary>
		/// Temporary layout thing until we make it align properly with the chart.
		/// </summary>
		private void m_buttonRow_Layout(object sender, LayoutEventArgs e)
		{
			ComputeButtonWidths();
		}

		private void ComputeButtonWidths()
		{
			var cPairs = m_buttonRow.Controls.Count / 2;
			if (cPairs == 0)
			{
				return;
			}
			var widthBtnContextMenu = SIL.FieldWorks.Resources.ResourceHelper.ButtonMenuArrowIcon.Width + 10;
			var offset = (NotesColumnOnRight ? 0 : 1) + (ChartIsRtL ? 0 : 1);
			var columnNames = m_logic.AllMyColumns.Select(ConstituentChartLogic.GetColumnHeaderFrom).ToList();
			if (!columnNames.Any())
			{
				// No columnNames probably means no text. Nothing to recalculate since we may be in the midst of disappearing.
				return;
			}
			if (ChartIsRtL)
			{
				columnNames.Reverse();
			}
			for (var ipair = 0; ipair < cPairs; ipair++)
			{
				Control c = m_buttonRow.Controls[ipair * 2];
				// main button
				c.Left = ColumnPositions[ipair + offset] + 2;
				// skip number column, fine tune
				c.Width = ColumnPositions[ipair + offset + 1] - ColumnPositions[ipair + offset] - widthBtnContextMenu;
				// Redo button name in case some won't (or now will!) fit on the button
				c.Text = GetBtnName(columnNames[ipair], c.Width - (((Button)c).Image.Width * 2));
				Control c2 = m_buttonRow.Controls[ipair * 2 + 1];
				// pull-down
				c2.Left = c.Right;
				c2.Width = widthBtnContextMenu;
			}
		}

		private int m_hvoRoot;

		protected internal IStText RootStText { get; set; }

		protected internal bool ChartIsRtL => RootStText != null && RootStText.IsValidObject && Cache.ServiceLocator.WritingSystemManager.Get(RootStText.MainWritingSystem).RightToLeftScript;

		public void RefreshRoot()
		{
			SetRoot(m_hvoRoot);
		}

		private void BuildTemplatePanel()
		{
			if (m_template == null)
			{
				return;
			}
			if (m_templateSelectionPanel.Controls.Count > 0)
			{
				((ComboBox)m_templateSelectionPanel.Controls[0]).SelectedItem = m_template;
				return;
			}
			var templateButton = new ComboBox();
			m_templateSelectionPanel.Controls.Add(templateButton);
			templateButton.Layout += TemplateDropDownMenu_Layout;
			templateButton.Left = m_templateSelectionPanel.Width - templateButton.Width;
			templateButton.DropDownStyle = ComboBoxStyle.DropDownList;
			templateButton.SelectionChangeCommitted += TemplateSelectionChanged;
			foreach (var chartTemplate in ((ICmPossibilityList)m_template.Owner).PossibilitiesOS)
			{
				templateButton.Items.Add(chartTemplate);
			}
			templateButton.SelectedItem = m_template;
			templateButton.Items.Add(LanguageExplorerResources.ksCreateNewTemplate);
		}

		private void TemplateSelectionChanged(object sender, EventArgs e)
		{
			var selection = (ComboBox)sender;
			var template = selection.SelectedItem as ICmPossibility;
			// If user chooses to add a new template then navigate them to the Text Constituent Chart Template list view
			if (selection.SelectedItem as string == LanguageExplorerResources.ksCreateNewTemplate)
			{
				MessageBoxUtils.Show(selection.Parent, LanguageExplorerResources.ksNewConstChartMessage, LanguageExplorerResources.ksNewConstChartCaption, MessageBoxButtons.OK);
				Cache.DomainDataByFlid.BeginUndoTask("Undo Insert new Text Constituent Chart Template",
					"Redo Insert new Text Constituent Chart Template");
				var list = Cache.LanguageProject.DiscourseDataOA.ConstChartTemplOA;
				var newKid = list.Services.GetInstance<ICmPossibilityFactory>().Create();
				list.PossibilitiesOS.Add(newKid);
				RecordList.SetUpConstChartTemplateTemplate(newKid);
				Cache.DomainDataByFlid.EndUndoTask();
				LinkHandler.PublishFollowLinkMessage(Publisher, new FwLinkArgs(LanguageExplorerResources.ksNewTemplateLink, newKid.Guid));
				selection.SelectedItem = m_template;
				return;
			}
			//Return if user selects current template
			if (template == m_template)
			{
				return;
			}
			// Detect if there is already a chart created for the given text and template
			IDsConstChart selectedChart = null;
			foreach (var chart in Cache.LangProject.DiscourseDataOA.ChartsOC.Cast<IDsConstChart>().Where(chart => chart.BasedOnRA != null && chart.BasedOnRA == RootStText && chart.TemplateRA == template))
			{
				selectedChart = chart;
			}
			//If there is no such chart, then create one
			if (selectedChart == null)
			{
				NonUndoableUnitOfWorkHelper.Do(Cache.ActionHandlerAccessor, () =>
				{
					selectedChart = m_serviceLocator.GetInstance<IDsConstChartFactory>().Create(Cache.LangProject.DiscourseDataOA, RootStText, selection.SelectedItem as ICmPossibility);
				});
			}

			PropertyTable.SetProperty(GetLastChartPropForText(RootStText.Guid), selectedChart.Guid, true, false, SettingsGroup.LocalSettings);
			SetRoot(m_hvoRoot);
		}

		private void TemplateDropDownMenu_Layout(object sender, EventArgs e)
		{
			var button = (ComboBox)sender;
			button.Left = m_templateSelectionPanel.Width - button.Width;
		}

		/// <summary>
		/// Try to get the bookmark from InterlinMaster, if there are rows in the chart.
		/// </summary>
		private void GetAndScrollToBookmark()
		{
			if (m_chart.RowsOS.Count <= 0)
			{
				// Reset bookmark to prevent LT-12666
				m_bookmark?.Reset(m_chart.BasedOnRA.IndexInOwner);
				return;
			}
			// no rows in chart; no selection necessary
			m_bookmark = GetAncestorBookmark(this, m_chart.BasedOnRA);
			m_logic.RaiseRibbonChgEvent();
			// This will override bookmark if there is a ChOrph to be inserted first.
			if (m_logic.IsChOrphActive)
			{
				return;
			}
			if (m_bookmark != null && m_bookmark.IndexOfParagraph >= 0)
			{
				Body.SelectAndScrollToBookmark(m_bookmark);
			}
			else if (!m_logic.IsChartComplete)
			{
				ScrollToEndOfChart();
			}
			// Hopefully the 'otherwise' will automatically display chart at top.
		}

		/// <summary>
		/// Sets up Move Here buttons and also determines ChOrph status by
		/// raising Ribbon Changed event.
		/// </summary>
		private void SetupMoveHereButtonsToMatchTemplate()
		{
			m_buttonRow.SuspendLayout();
			while (m_MoveHereButtons.Count > m_allColumns.Length)
			{
				// Remove MoveHere button
				var lastButton = m_MoveHereButtons[m_MoveHereButtons.Count - 1];
				lastButton.Click -= btnMoveHere_Click;
				m_buttonRow.Controls.Remove(lastButton);
				m_MoveHereButtons.Remove(lastButton);

				// Remove Context Menu button
				var lastBtnContextMenu = m_ContextMenuButtons[m_ContextMenuButtons.Count - 1];
				lastBtnContextMenu.Click -= btnContextMenu_Click;
				m_buttonRow.Controls.Remove(lastBtnContextMenu);
				m_ContextMenuButtons.Remove(lastBtnContextMenu);
			}
			while (m_MoveHereButtons.Count < m_allColumns.Length)
			{
				// Install MoveHere button
				var newButton = new Button();
				newButton.Click += btnMoveHere_Click;
				var sColName = m_logic.GetColumnLabel(m_MoveHereButtons.Count);
				// Holds column name while setting buttons
				m_buttonRow.Controls.Add(newButton);
				// Enhance GordonM: This should deal in pixel length, not character length.
				// And column width needs to be known!
				newButton.Image = SIL.FieldWorks.Resources.ResourceHelper.MoveUpArrowIcon;
				newButton.ImageAlign = ContentAlignment.MiddleRight;

				// useable space is button width less (icon width * 2) because of centering
				var btnSpace = newButton.Width - (newButton.Image.Size.Width * 2);
				// useable pixel length on button
				newButton.TextAlign = ContentAlignment.MiddleCenter;
				newButton.Text = GetBtnName(sColName, btnSpace);

				// Set up the ToolTip text for the Button.
				m_toolTip.SetToolTip(newButton, string.Format(LanguageExplorerResources.ksMoveHereToolTip, sColName));

				m_MoveHereButtons.Add(newButton);

				// Install context menu button
				var newBtnContextMenu = new Button();
				newBtnContextMenu.Click += btnContextMenu_Click;
				newBtnContextMenu.Image = SIL.FieldWorks.Resources.ResourceHelper.ButtonMenuArrowIcon;
				m_buttonRow.Controls.Add(newBtnContextMenu);
				m_ContextMenuButtons.Add(newBtnContextMenu);
			}
			// To handle Refresh problem where buttons aren't set to match ChOrph state,
			// raise Ribbon changed event again here
			m_fContextMenuButtonsEnabled = true;
			// the newly added buttons will be enabled
			m_logic.RaiseRibbonChgEvent();
			m_buttonRow.ResumeLayout();
		}

		private void CreateChartInNonUndoableUOW()
		{
			NonUndoableUnitOfWorkHelper.Do(Cache.ActionHandlerAccessor, () =>
			{
				m_chart = m_serviceLocator.GetInstance<IDsConstChartFactory>().Create(Cache.LangProject.DiscourseDataOA, RootStText, Cache.LangProject.GetDefaultChartTemplate());
			});
			PropertyTable.SetProperty(GetLastChartPropForText(RootStText.Guid), m_chart.Guid, true, false, SettingsGroup.LocalSettings);
		}

		private void DetectAndReportTemplateProblem()
		{
			var templates = Cache.LangProject.DiscourseDataOA.ConstChartTemplOA.PossibilitiesOS;
			if (templates.Count == 0 || templates[0].SubPossibilitiesOS.Count == 0)
			{
				MessageBox.Show(this, LanguageExplorerResources.ksNoColumns, LanguageExplorerResources.ksWarning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
			if (templates.Count != 1)
			{
				MessageBox.Show(this, LanguageExplorerResources.ksOnlyOneTemplateAllowed, LanguageExplorerResources.ksWarning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		/// <summary>
		/// Main Chart part of preparation. Calls Chart Logic part.
		/// Scroll to ChartOrphan, highlight cell insert possibilities, disable ineligible MoveHere buttons
		/// </summary>
		private void PrepareForChOrphInsert(int iPara, int offset)
		{
			// disable ineligible MoveHere buttons
			SetEligibleButtons(m_logic.PrepareForChOrphInsert(iPara, offset, out var rowPrec));
			// disable dropdown context buttons (next to MoveHere buttons)
			DisableAllContextButtons();
			// create a ChartLocation for scrolling and scroll to first row
			Body.SelectAndScrollToLoc(new ChartLocation(rowPrec, 0), false);
			// bookmark this location, but don't persist.
			m_bookmark?.Save(SegmentServices.FindNearestAnalysis(GetTextParagraphByIndex(iPara), offset, offset, out _), false, m_bookmark.TextIndex);
		}

		private IStTxtPara GetTextParagraphByIndex(int iPara)
		{
			return m_chart.BasedOnRA[iPara];
		}

		/// <summary>
		/// Disable all MoveHere buttons whose column corresponds to a false entry in the parameter bool array.
		/// </summary>
		private void SetEligibleButtons(bool[] goodColumns)
		{
			if (m_MoveHereButtons.Count <= 0)
			{
				return;
			}
			//This method is called multiple times and sometimes on the early calls the data does not agree
			//if so, wait until a later call to enable buttons
			if (m_MoveHereButtons.Count != goodColumns.Length)
			{
				return;
			}
			for (var icol = 0; icol < goodColumns.Length; icol++)
			{
				m_MoveHereButtons[icol].Enabled = goodColumns[icol];
			}
		}

		internal void ScrollToEndOfChart()
		{
			// Scroll to LastRow of chart
			var row = m_logic.LastRow;
			if (row == null)
			{
				return;
			}
			Body.SelectAndScrollToLoc(new ChartLocation(row, 0), true);
		}

		private static InterAreaBookmark GetAncestorBookmark(Control curLevelControl, IStText basedOnRa)
		{
			object myParent = curLevelControl.Parent;
			return myParent == null ? null
				: !(myParent is InterlinMaster) ? GetAncestorBookmark((Control)myParent, basedOnRa) : InterlinMaster.m_bookmarks[new Tuple<string, Guid>((myParent as InterlinMaster).MyRecordList.Id, basedOnRa.Guid)];
		}

		public void SelectOccurrence(AnalysisOccurrence point)
		{
			Body.SelectAndScrollToAnalysisOccurrence(point);
		}

		/// <summary>
		/// This public method enables call by reflection from InterlinMaster of internal Logic method
		/// that retrieves a 'bookmarkable' Wordform from the Ribbon.
		/// </summary>
		public AnalysisOccurrence GetUnchartedWordForBookmark()
		{
			// Enhance GordonM: We don't actually want to save a bookmark, if the user hasn't
			// changed anything in the Chart or clicked in the Ribbon. Perhaps we need to save
			// the first uncharted word when coming into this tab and check here to see
			// if it has changed? (use OnVisibleChanged?)
			// Check here because this is a Control.
			return m_logic.GetUnchartedWordForBookmark();
		}

		private string GetLastChartPropForText(Guid guid) => $"LastChartForText_{guid.ToString()}";

		/// <summary>
		/// Find the last chart used for this text (or the first chart available), set it in
		/// the chart member and in the chart logic, and clean up any invalid cells
		/// </summary>
		/// <param name="hvoStText"></param>
		private void FindAndCleanUpMyChart(int hvoStText)
		{
			IDsConstChart chartToClean = null;
			// Try to retrieve the last chart used for this text from the property table
			var textGuid = Cache.ServiceLocator.GetObject(hvoStText).Guid;
			if(PropertyTable.TryGetValue(GetLastChartPropForText(textGuid), out Guid chartGuid))
			{
				if (Cache.ServiceLocator.ObjectRepository.TryGetObject(chartGuid, out var chart))
				{
					if (chart is IDsConstChart constChart)
					{
						chartToClean = constChart;
					}
				}
				// if that chart no longer exists clear it from the prop table
				if (chartToClean == null)
				{
					PropertyTable.RemoveProperty(GetLastChartPropForText(textGuid));
				}
			}
			// Use the retrieved last chart, or pick the first valid chart for this text
			m_logic.Chart = m_chart = chartToClean ?? Cache.LangProject.DiscourseDataOA.ChartsOC
				.Cast<IDsConstChart>()
				.FirstOrDefault(chart => chart.BasedOnRA != null && chart.BasedOnRA.Hvo == hvoStText);
			m_logic.CleanupInvalidChartCells();
		}

		/// <summary>
		/// Figure out what substring of the column name to put on the button.
		/// </summary>
		/// <param name="strName">The name of the column.</param>
		/// <param name="pxUseable">The useable space on the button in pixels.</param>
		/// <returns>Some substring of the column name (possibly the whole).</returns>
		private string GetBtnName(string strName, int pxUseable)
		{
			if (pxUseable >= GetWidth(strName, Font))
			{
				return strName;
			}
			if (pxUseable < GetWidth(strName.Substring(0, 1), Font))
			{
				return string.Empty;
			}
			for (var i = 0; i < strName.Length; i++)
			{
				if (GetWidth(strName.Substring(0, i + 1), Font) > pxUseable)
				{
					return strName.Substring(0, i);
				}
			}
			// Shouldn't ever get here.
			return strName;
		}

		private bool HasPersistentColWidths => PropertyTable.GetValue<string>(ColWidthId()) != null;

		/// <summary>
		/// Restore column widths if any are persisted for this chart
		/// </summary>
		/// <returns>true if it found a valid set of widths.</returns>
		private bool RestoreColumnWidths()
		{
			var savedCols = PropertyTable?.GetValue<string>(ColWidthId());
			if (savedCols == null)
			{
				return false;
			}
			XDocument doc;
			try
			{
				doc = XDocument.Parse(savedCols);
			}
			catch (Exception)
			{
				// If anything is wrong with the saved data, ignore it.
				return false;
			}
			if (doc.Root == null || doc.Root.Elements().Count() != m_columnWidths.Length)
			{
				return false; // prevents crash on deleting a chart-internal template column.
			}
			var i = 0;
			ColumnPositions[0] = 0;
			foreach (var element in doc.Root.Elements())
			{
				var width = XmlUtils.GetMandatoryIntegerAttributeValue(element, "width");
				ColumnPositions[i + 1] = ColumnPositions[i] + MpToPixelX(width);
				if (i < m_columnWidths.Length)
				{
					m_columnWidths[i++] = width;
				}
				else
				{
					return false;
				}
			}
			// succeed only if exact expected number.
			return i == m_columnWidths.Length;
		}

		/// <summary>
		/// Save the current column widths in the mediator's property table.
		/// </summary>
		private void PersistColumnWidths()
		{
			var colList = new StringBuilder();
			colList.Append("<root>");
			foreach (var val in m_columnWidths)
			{
				colList.Append("<col width=\"" + val + "\"/>");
			}
			colList.Append("</root>");
			var cwId = ColWidthId();
			PropertyTable.SetProperty(cwId, colList.ToString(), true, true);
		}

		private string ColWidthId()
		{
			return "ConstChartColWidths" + (m_chart?.Guid ?? Guid.Empty);
		}

		private void btnMoveHere_Click(object sender, EventArgs e)
		{
			// find the index in the button row.
			m_logic.MoveToColumnInUOW(GetColumnOfButton((Button)sender));
		}

		private int GetColumnOfButton(Button btn)
		{
			// each column corresponds to a pair of MoveHereButtons and ContextMenuButtons in the buttonRow.
			var icol = btn.Parent.Controls.IndexOf(btn) / 2;
			if (ChartIsRtL)
			{
				icol = m_logic.ConvertColumnIndexToFromRtL(icol, m_logic.AllMyColumns.Length - 1);
			}
			return icol;
		}

		// Event handler to run if Ribbon changes
		void OnLogicRibbonChanged(object sender, EventArgs e)
		{
			int iPara, offset;
			// 'out' vars for NextInputIsChOrph()
			// Tests ribbon contents
			if (m_logic.NextInputIsChOrph(out iPara, out offset))
			{
				if (m_bookmark != null)
				{
					m_bookmark.Reset(m_bookmark.TextIndex);
				}
				else
				{
					// This code path was unexpected but the conditions were seen in a crash stack
					// from the field. We will avoid NullReference by attempting to get a bookmark to use
					// and checking for a null bookmark in PrepareForChOrphInsert.
					// This very well could be the right thing to do, but understanding why the bookmark is null
					// is worthwhile
					m_bookmark = GetAncestorBookmark(this, m_chart.BasedOnRA);
					Debug.Fail("This is not an expected path, analyze.");
				}
				// Resetting of highlight is done in the array setter now.
				PrepareForChOrphInsert(iPara, offset);
				// scroll to ChOrph, highlight cell possibilities, set bookmark etc.
			}
			else
			{
				if (!m_logic.IsChOrphActive)
				{
					return;
				}
				// Got past the last ChOrph, now reset for normal charting
				EnableAllContextButtons();
				EnableAllMoveHereButtons();
				m_logic.ResetRibbonLimits();
				m_logic.CurrHighlightCells = null;
				// Should reset highlighting (w/PropChanged)
				// Where should we go next? End or top of chart depending on whether chart is complete
				if (!m_logic.IsChartComplete)
				{
					ScrollToEndOfChart();
				}
				else
				{
					// create a ChartLocation for scrolling and scroll to first row
					Body.SelectAndScrollToLoc(new ChartLocation(m_chart.RowsOS[0], 0), false);
				}
			}
		}

		/// <summary>
		/// Shuts off all the little down-arrow buttons next to the MoveHere buttons.
		/// For use when the next input is a ChOrph.
		/// </summary>
		protected internal void DisableAllContextButtons()
		{
			if (m_fContextMenuButtonsEnabled && m_ContextMenuButtons.Count > 0)
			{
				foreach (var btnContext in m_ContextMenuButtons)
				{
					btnContext.Enabled = false;
					btnContext.Image = null;
				}
			}
			m_fContextMenuButtonsEnabled = false;
		}

		/// <summary>
		/// Turns back on all the little down-arrow buttons next to the MoveHere buttons.
		/// For use when the next input is no longer a ChOrph.
		/// </summary>
		protected internal void EnableAllContextButtons()
		{
			if (!m_fContextMenuButtonsEnabled && m_ContextMenuButtons.Count > 0)
			{
				foreach (var btnContext in m_ContextMenuButtons)
				{
					btnContext.Enabled = true;
					btnContext.Image = SIL.FieldWorks.Resources.ResourceHelper.ButtonMenuArrowIcon;
				}
			}
			m_fContextMenuButtonsEnabled = true;
		}

		private void EnableAllMoveHereButtons()
		{
			if (m_chart == null || m_MoveHereButtons.Count <= 0)
			{
				return;
			}
			foreach (var button in m_MoveHereButtons)
			{
				button.Enabled = true;
			}
		}

		/// <summary>
		/// Handles clicking of the down arrow button beside a column button.
		/// </summary>
		private void btnContextMenu_Click(object sender, EventArgs e)
		{
			// find the index in the button row.
			DisposeContextMenu(this, new EventArgs());
			var btn = (Button)sender;
			_contextMenuStrip = m_logic.InsertIntoChartContextMenu(GetColumnOfButton(btn));
			_contextMenuStrip.Closed += contextMenuStrip_Closed; // dispose when no longer needed (but not sooner! needed after this returns)
			_contextMenuStrip.Show(btn, new Point(0, btn.Height));
		}

		private void contextMenuStrip_Closed(object sender, ToolStripDropDownClosedEventArgs e)
		{
			// It's apparently still needed by the menu handling code in .NET.
			// So we can't dispose it yet.
			// But we want to eventually (Eberhard says if it has a Dispose we MUST call it to make Mono happy)
			Application.Idle += DisposeContextMenu;
		}

		private void DisposeContextMenu(object sender, EventArgs e)
		{
			Application.Idle -= DisposeContextMenu;
			if (_contextMenuStrip == null || _contextMenuStrip.IsDisposed)
			{
				return;
			}
			_contextMenuStrip.Dispose();
			_contextMenuStrip = null;
		}

		protected override void OnGotFocus(EventArgs e)
		{
			base.OnGotFocus(e);
			m_ribbon.Focus();
			// Enhance: decide which one should have focus.
		}

		public bool NotesColumnOnRight
		{
			get;
			internal set;
		} = true;

		/// <summary>
		/// For testing.
		/// </summary>
		internal ConstChartBody Body { get; private set; }

		/// <summary>
		/// The property table key storing InterlinLineChoices used by our display.
		/// </summary>
		private static string ConfigPropName
		{
			get;
			set;
		}

		/// <summary>
		/// The old property table key storing InterlinLineChoices used by our display.
		/// </summary>
		private static string OldConfigPropName
		{
			get;
			set;
		}

		/// <summary>
		/// Tries to retrieve the Line Choices from the Property Table and returns if it was succesful
		/// </summary>
		internal bool TryRestoreLineChoices(out InterlinLineChoices lineChoices)
		{
			lineChoices = null;
			var persist = PropertyTable.GetValue<string>(ConfigPropName, null, SettingsGroup.LocalSettings);
			if (persist == null)
			{
				persist = PropertyTable.GetValue<string>(OldConfigPropName, null, SettingsGroup.LocalSettings);
			}
			if (persist != null)
			{
				// Intentionally never pass OldConfigPropName into Restore to prevent corrupting it's old value with the new format.
				lineChoices = InterlinLineChoices.Restore(persist, Cache.LanguageWritingSystemFactoryAccessor,
					Cache.LangProject, Cache.DefaultVernWs, Cache.DefaultAnalWs, InterlinMode.Analyze, PropertyTable, ConfigPropName);
			}
			return persist != null && lineChoices != null;
		}

		/// <summary>
		/// Gets the Line Choices stored in the property table
		/// </summary>
		private InterlinLineChoices GetLineChoices()
		{
			var result = new InterlinLineChoices(Cache.LangProject, Cache.DefaultVernWs, Cache.DefaultAnalWs);
			string persist = null;
			if (PropertyTable != null)
			{
				persist = PropertyTable.GetValue<string>(ConfigPropName, null, SettingsGroup.LocalSettings);
				if (persist == null)
				{
					persist = PropertyTable.GetValue<string>(OldConfigPropName, null, SettingsGroup.LocalSettings);
				}
			}
			InterlinLineChoices lineChoices = null;
			if (persist != null)
			{
				lineChoices = InterlinLineChoices.Restore(persist, Cache.ServiceLocator.GetInstance<ILgWritingSystemFactory>(), Cache.LangProject, Cache.DefaultVernWs, Cache.DefaultAnalWs, InterlinMode.Chart, PropertyTable, ConfigPropName);
			}
			else
			{
				GetLineChoice(result, lineChoices, InterlinLineChoices.kflidWord, InterlinLineChoices.kflidWordGloss);
				return result;
			}
			// Intentionally never pass OldConfigPropName into Restore to prevent corrupting it's old value with the new format.
			lineChoices = InterlinLineChoices.Restore(persist, Cache.ServiceLocator.GetInstance<ILgWritingSystemFactory>(), Cache.LangProject, Cache.DefaultVernWs, Cache.DefaultAnalWs, InterlinMode.Chart, PropertyTable, ConfigPropName);
			return lineChoices;
		}

		/// <summary>
		/// Make sure there is SOME lineChoice for the one of the specified flids.
		/// If lineChoices is non-null and contains one for the right flid, choose the first.
		/// </summary>
		private static void GetLineChoice(InterlinLineChoices dest, InterlinLineChoices source, params int[] flids)
		{
			foreach (var flid in flids)
			{
				if (source != null)
				{
					var index = source.IndexInEnabled(flid);
					if (index >= 0)
					{
						dest.Add(source.EnabledLineSpecs[index]);
						return;
					}
				}
				// Last resort.
				dest.Add(flid);
			}
		}

		public bool NotesOnRightFromPropertyTable
		{
			get => PropertyTable == null || PropertyTable.GetValue("notesOnRight", true, SettingsGroup.LocalSettings);
			set => PropertyTable?.SetProperty("notesOnRight", value, settingsGroup: SettingsGroup.LocalSettings);
		}

		/// <summary>
		/// Discourse export dialog implements a dialog for exporting the discourse chart.
		/// Considerable refactoring is in order to share more code with InterlinearExportDialog,
		/// or move common code down to ExportDialog. This has been postponed in the interests
		/// of being able to release FW 5.2.1 without requiring changes to DLLs other than Discourse.
		/// </summary>
		private sealed class DiscourseExportDialog : ExportDialog
		{
			private readonly List<XmlNode> m_ddNodes = new List<XmlNode>(8); // Saves XML nodes used to configure items.
			private readonly int m_hvoRoot;
			private readonly IVwViewConstructor m_vc;
			private readonly int m_wsLineNumber;

			internal DiscourseExportDialog(int hvoRoot, IVwViewConstructor vc, int wsLineNumber)
			{
				m_hvoRoot = hvoRoot;
				m_vc = vc;
				m_wsLineNumber = wsLineNumber;
			}

			#region Overrides of ExportDialog

			/// <summary>
			/// Initialize a FLEx component with the basic interfaces.
			/// </summary>
			/// <param name="flexComponentParameters">Parameter object that contains the required three interfaces.</param>
			public override void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
			{
				base.InitializeFlexComponent(flexComponentParameters);

				m_helpTopic = "khtpExportDiscourse";
				columnHeader1.Text = LanguageExplorerResources.ksFormat;
				columnHeader2.Text = LanguageExplorerResources.ksExtension;
				Text = LanguageExplorerResources.ksExportDiscourse;
			}

			#endregion

			protected override string ConfigurationFilePath => Path.Combine("Language Explorer", "Export Templates", "Discourse");

			// Items in this version are never disabled.
			protected override bool ItemDisabled(string tag)
			{
				return false;
			}

			/// <summary>
			/// Override to do nothing since not configuring an FXT export process.
			/// </summary>
			protected override void ConfigureItem(XmlDocument document, ListViewItem item, XmlNode ddNode)
			{
				m_ddNodes.Add(ddNode);
				columnHeader1.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
			}

			// Export the data according to specifications.
			// Prime candidate for refactoring, almost identical to base class method once we reinstate OO, as we
			// will want to. Main diffs are using a different class of exporter and a different directory path.
			protected override void DoExport(string outPath)
			{
				var fxtPath = (string)m_exportList.SelectedItems[0].Tag;
				var ddNode = m_ddNodes[NodeIndex(fxtPath)];
				var mode = XmlUtils.GetOptionalAttributeValue(ddNode, "mode", "xml");
				using (new WaitCursor(this))
				{
					try
					{
						ExportPhase1(out var exporter, outPath);
						var rootDir = FwDirectoryFinder.CodeDirectory;
						var transform = XmlUtils.GetOptionalAttributeValue(ddNode, "transform", "");
						var sTransformPath = Path.Combine(rootDir, "Language Explorer", "Export Templates", "Discourse");
						switch (mode)
						{
							case "doNothing":
								break;
							case "applySingleTransform":
								var sTransform = Path.Combine(sTransformPath, transform);
								exporter.PostProcess(sTransform, outPath, 1);
								break;
						}
					}
					catch (Exception e)
					{
						MessageBox.Show(this, string.Format(LanguageExplorerResources.ksExportErrorMsg, e.Message));
					}
				}
				Close();
			}

			private int NodeIndex(string tag)
			{
				var file = tag.Substring(tag.LastIndexOfAny(new[] { '\\', '/' }) + 1);
				for (var i = 0; i < m_ddNodes.Count; i++)
				{
					var fileN = m_ddNodes[i].BaseURI.Substring(m_ddNodes[i].BaseURI.LastIndexOf('/') + 1);
					if (fileN == file)
					{
						return i;
					}
				}
				return 0;
			}

			private void ExportPhase1(out DiscourseExporter exporter, string fileName)
			{
				using (var writer = new XmlTextWriter(fileName, System.Text.Encoding.UTF8))
				{
					exporter = new DiscourseExporter(m_cache, writer, m_hvoRoot, m_vc, m_wsLineNumber);
					exporter.ExportDisplay();
					writer.Close();
				}
			}
		}
	}
}