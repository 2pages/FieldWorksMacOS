﻿// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using LanguageExplorer.Controls;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Application;
using SIL.FieldWorks.Resources;
using SIL.FieldWorks.XWorks;

namespace LanguageExplorer.Areas.Notebook.Tools.NotebookDocument
{
	/// <summary>
	/// ITool implementation for the "notebookDocument" tool in the "notebook" area.
	/// </summary>
	internal sealed class NotebookDocumentTool : ITool
	{
		private XDocument _configurationDocument;
		private PaneBarContainer _paneBarContainer;

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
		/// <param name="propertyTable">Interface to a property table.</param>
		/// <param name="publisher">Interface to the publisher.</param>
		/// <param name="subscriber">Interface to the subscriber.</param>
		public void InitializeFlexComponent(IPropertyTable propertyTable, IPublisher publisher, ISubscriber subscriber)
		{
			FlexComponentCheckingService.CheckInitializationValues(propertyTable, publisher, subscriber, PropertyTable, Publisher, Subscriber);

			PropertyTable = propertyTable;
			Publisher = publisher;
			Subscriber = subscriber;
		}

		#endregion

		#region Implementation of IMajorFlexComponent

		/// <summary>
		/// Deactivate the component.
		/// </summary>
		/// <remarks>
		/// This is called on the outgoing component, when the user switches to a component.
		/// </remarks>
		public void Deactivate(ICollapsingSplitContainer mainCollapsingSplitContainer, MenuStrip menuStrip, ToolStripContainer toolStripContainer,
			StatusBar statusbar)
		{
			PaneBarContainerFactory.RemoveFromParentAndDispose(_paneBarContainer);
			_paneBarContainer = null;
		}

		/// <summary>
		/// Activate the component.
		/// </summary>
		/// <remarks>
		/// This is called on the component that is becoming active.
		/// </remarks>
		public void Activate(ICollapsingSplitContainer mainCollapsingSplitContainer, MenuStrip menuStrip, ToolStripContainer toolStripContainer,
			StatusBar statusbar)
		{
			_configurationDocument = XDocument.Parse("<parameters area='notebook' altTitleId='RnGenericRec-Plural' persistContext='Notebk' backColor='White' layout='publishRecord' layoutProperty='NotebookPublicationLayout' editable='false' configureObjectName='Document' findHelpId='khtpFindNotebook'>" + Environment.NewLine +
"<configureLayouts>" + Environment.NewLine +
"<layoutType label='Notebook Records' layout='publishRecord'>" + Environment.NewLine +
"<configure class='RnGenericRec' label='Record' layout='publishRecord'/>" + Environment.NewLine +
"</layoutType>" + Environment.NewLine +
"</configureLayouts>" + Environment.NewLine +
"</parameters>");
			var recordClerk = NotebookArea.CreateRecordClerkForAllNotebookAreaTools(PropertyTable.GetValue<FdoCache>("cache"));
			recordClerk.InitializeFlexComponent(PropertyTable, Publisher, Subscriber);
			_paneBarContainer = PaneBarContainerFactory.Create(
				PropertyTable, Publisher, Subscriber,
				mainCollapsingSplitContainer.SecondControl,
				new XmlDocView(_configurationDocument.Root, recordClerk));
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
		public string MachineName
		{
			get { return "notebookDocument"; }
		}

		/// <summary>
		/// User-visible localizable component name.
		/// </summary>
		public string UiName
		{
			get { return "Document"; }
		}

		#endregion

		#region Implementation of ITool

		/// <summary>
		/// Get the area machine name the tool is for.
		/// </summary>
		public string AreaMachineName
		{
			get { return "notebook"; }
		}

		/// <summary>
		/// Get the image for the area.
		/// </summary>
		public Image Icon
		{
			get
			{
				var image = Images.DocumentView;
				image.MakeTransparent(Color.Magenta);
				return image;
			}
		}

		#endregion
	}
}
