﻿// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Drawing;
using System.Windows.Forms;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Resources;

namespace LanguageExplorer.Areas.TextsAndWords.Tools.Concordance
{
	/// <summary>
	/// ITool implementation for the "concordance" tool in the "textsWords" area.
	/// </summary>
	internal sealed class ConcordanceTool : ITool
	{
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
		/// <param name="flexComponentParameterObject">Parameter object that contains the required three interfaces.</param>
		public void InitializeFlexComponent(FlexComponentParameterObject flexComponentParameterObject)
		{
			FlexComponentCheckingService.CheckInitializationValues(flexComponentParameterObject, new FlexComponentParameterObject(PropertyTable, Publisher, Subscriber));

			PropertyTable = flexComponentParameterObject.PropertyTable;
			Publisher = flexComponentParameterObject.Publisher;
			Subscriber = flexComponentParameterObject.Subscriber;
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
			TemporaryToolProviderHack.RemoveToolDisplay(mainCollapsingSplitContainer);
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
#if RANDYTODO
			// TODO: This tool need Itext's ConcordanceContainer main control and a gaggle of other stuff currently in IText.
#endif
			TemporaryToolProviderHack.SetupToolDisplay(mainCollapsingSplitContainer, this);
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
			get { return "concordance"; }
		}

		/// <summary>
		/// User-visible localizable component name.
		/// </summary>
		public string UiName
		{
			get { return "Concordance"; }
		}

		#endregion

		#region Implementation of ITool

		/// <summary>
		/// Get the area machine name the tool is for.
		/// </summary>
		public string AreaMachineName
		{
			get { return "textsWords"; }
		}

		/// <summary>
		/// Get the image for the area.
		/// </summary>
		public Image Icon
		{
			get
			{
				var image = Images.SideBySideView;
				image.MakeTransparent(Color.Magenta);
				return image;
			}
		}

		#endregion
	}
}
