// Copyright (c) 2005-2023 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Diagnostics;
using System.Windows.Forms;
using SIL.LCModel;
using SIL.FieldWorks.Common.FwUtils;
using static SIL.FieldWorks.Common.FwUtils.FwUtils;
using SIL.FieldWorks.LexText.Controls;
using SIL.Utils;

namespace SIL.FieldWorks.XWorks.MorphologyEditor
{
	/// <summary>
	/// Listener class for adding POSes via Insert menu.
	/// </summary>
	[XCore.MediatorDispose]
	public class MasterInflFeatDlgListener : MasterDlgListener
	{

		#region Properties

		protected override string PersistentLabel
		{
			get { return "InsertInflectionFeature"; }
		}

		#endregion Properties

		#region Construction and Initialization

		/// <summary>
		/// Constructor.
		/// </summary>
		public MasterInflFeatDlgListener()
		{
		}

		#endregion Construction and Initialization

		#region IDisposable & Co. implementation
		// Region last reviewed: never

		/// <summary>
		/// Finalizer, in case client doesn't dispose it.
		/// Force Dispose(false) if not already called (i.e. m_isDisposed is true)
		/// </summary>
		/// <remarks>
		/// In case some clients forget to dispose it directly.
		/// </remarks>
		~MasterInflFeatDlgListener()
		{
			Dispose(false);
			// The base class finalizer is called automatically.
		}


		#endregion IDisposable & Co. implementation

		#region XCORE Message Handlers

		/// <summary>
		/// Handles the xWorks message to insert a new FsFeatDefn.
		/// Invoked by the RecordClerk via a main menu.
		/// </summary>
		/// <param name="argument">The xCore Command object.</param>
		/// <returns>true, if we handled the message, otherwise false, if there was an unsupported 'classname' parameter</returns>
		public override bool OnDialogInsertItemInVector(object argument)
		{
			CheckDisposed();

			Debug.Assert(argument != null && argument is XCore.Command);
			string className = XmlUtils.GetOptionalAttributeValue(
				(argument as XCore.Command).Parameters[0], "className");
			if (className == null || ((className != "FsClosedFeature") && (className != "FsComplexFeature")))
				return false;
			if (className == "FsClosedFeature" && (argument as XCore.Command).Id != "CmdInsertClosedFeature")
				return false;

			using (MasterInflectionFeatureListDlg dlg = new MasterInflectionFeatureListDlg(className))
			{
				LcmCache cache = m_propertyTable.GetValue<LcmCache>("cache");
				Debug.Assert(cache != null);
				dlg.SetDlginfo(cache.LangProject.MsFeatureSystemOA, m_mediator, m_propertyTable, true);
				switch (dlg.ShowDialog(m_propertyTable.GetValue<Form>("window")))
				{
					case DialogResult.OK: // Fall through.
					case DialogResult.Yes:
						Publisher.Publish(new PublisherParameterObject(EventConstants.MasterRefresh, cache.LangProject.MsFeatureSystemOA));
						Publisher.Publish(new PublisherParameterObject(EventConstants.JumpToRecord, dlg.SelectedFeatDefn.Hvo));
						break;
				}
			}
			return true; // We "handled" the message, regardless of what happened.
		}

		#endregion XCORE Message Handlers
	}
}
