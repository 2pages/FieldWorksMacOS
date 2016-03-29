// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.RootSites;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FdoUi;
using SIL.Utils;

namespace SIL.FieldWorks.XWorks
{
	/// <summary>
	/// Just a shell class for containing runtime Switches for controling the diagnostic output.
	/// This could go in any file in the XWorks namespace, It's just here as a starting point.
	/// </summary>
	public class RuntimeSwitches
	{
		/// Tracing variable - used to control when and what is output to the debug and trace listeners
		public static TraceSwitch RecordTimingSwitch = new TraceSwitch("XWorks_Timing", "Used for diagnostic timing output", "Off");
		public static TraceSwitch linkListenerSwitch = new TraceSwitch("XWorks_LinkListener", "Used for diagnostic output", "Off");
	}

	/// <summary>
	/// LinkListenerListener handles Hyper linking and history
	/// See the class comment on FwLinkArgs for details on how all the parts of hyperlinking work.
	/// </summary>
	public class LinkListener : IFlexComponent, IFWDisposable
	{
		const int kmaxDepth = 50;		// Limit the stacks to 50 elements (LT-729).
		protected LinkedList<FwLinkArgs> m_backStack;
		protected LinkedList<FwLinkArgs> m_forwardStack;
		protected FwLinkArgs m_currentContext;

		private bool m_fFollowingLink = false;
		private int m_cBackStackOrig = 0;
		private FwLinkArgs m_lnkActive = null;
		private bool m_fUsingHistory = false;

		/// -----------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="LinkListener"/> class.
		/// </summary>
		/// -----------------------------------------------------------------------------------
		public LinkListener()
		{
			m_backStack = new LinkedList<FwLinkArgs>();
			m_forwardStack = new LinkedList<FwLinkArgs>();
			m_currentContext = null;
		}

		#region IDisposable & Co. implementation
		// Region last reviewed: never

		/// <summary>
		/// Check to see if the object has been disposed.
		/// All public Properties and Methods should call this
		/// before doing anything else.
		/// </summary>
		public void CheckDisposed()
		{
			if (IsDisposed)
				throw new ObjectDisposedException(String.Format("'{0}' in use after being disposed.", GetType().Name));
		}

		/// <summary>
		/// True, if the object has been disposed.
		/// </summary>
		private bool m_isDisposed = false;

		/// <summary>
		/// See if the object has been disposed.
		/// </summary>
		public bool IsDisposed
		{
			get { return m_isDisposed; }
		}

		/// <summary>
		/// Finalizer, in case client doesn't dispose it.
		/// Force Dispose(false) if not already called (i.e. m_isDisposed is true)
		/// </summary>
		/// <remarks>
		/// In case some clients forget to dispose it directly.
		/// </remarks>
		~LinkListener()
		{
			Dispose(false);
			// The base class finalizer is called automatically.
		}

		/// <summary>
		///
		/// </summary>
		/// <remarks>Must not be virtual.</remarks>
		public void Dispose()
		{
			Dispose(true);
			// This object will be cleaned up by the Dispose method.
			// Therefore, you should call GC.SupressFinalize to
			// take this object off the finalization queue
			// and prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Executes in two distinct scenarios.
		///
		/// 1. If disposing is true, the method has been called directly
		/// or indirectly by a user's code via the Dispose method.
		/// Both managed and unmanaged resources can be disposed.
		///
		/// 2. If disposing is false, the method has been called by the
		/// runtime from inside the finalizer and you should not reference (access)
		/// other managed objects, as they already have been garbage collected.
		/// Only unmanaged resources can be disposed.
		/// </summary>
		/// <param name="disposing"></param>
		/// <remarks>
		/// If any exceptions are thrown, that is fine.
		/// If the method is being done in a finalizer, it will be ignored.
		/// If it is thrown by client code calling Dispose,
		/// it needs to be handled by fixing the bug.
		///
		/// If subclasses override this method, they should call the base implementation.
		/// </remarks>
		protected virtual void Dispose(bool disposing)
		{
			System.Diagnostics.Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			// Must not be run more than once.
			if (m_isDisposed)
				return;

			if (disposing)
			{
				// Dispose managed resources here.
				if (PropertyTable != null)
				{
					PropertyTable.SetProperty("LinkListener", null, false, false);
				}
				if (m_backStack != null)
					m_backStack.Clear();
				if (m_forwardStack != null)
					m_forwardStack.Clear();
			}

			// Dispose unmanaged resources here, whether disposing is true or false.
			m_currentContext = null;
			m_backStack = null;
			m_forwardStack = null;
			PropertyTable = null;
			Publisher = null;
			Subscriber = null;

			m_isDisposed = true;
		}

		#endregion IDisposable & Co. implementation

		/// <summary>
		/// Return the current link.
		/// </summary>
		public FwLinkArgs CurrentContext
		{
			get
			{
				CheckDisposed();
				return m_currentContext;
			}
		}

		/// <summary>
		/// Handle the specified link if it is local.
		/// </summary>
		/// <param name="source"></param>
		/// <returns></returns>
		public bool OnHandleLocalHotlink(object source)
		{
			LocalLinkArgs args = source as LocalLinkArgs;
			if (args == null)
				return true; // we can't handle it, but probably no one else can either. Maybe should crash?
			var url = args.Link;
			if(!url.StartsWith(FwLinkArgs.kFwUrlPrefix))
				return true; // we can't handle it, but no other colleague can either. Needs to launch whatever can (see VwBaseVc.DoHotLinkAction).
			try
			{
				var fwargs = new FwAppArgs(new[] {url});
				FdoCache cache = PropertyTable.GetValue<FdoCache>("cache");
				if (SameDatabase(fwargs, cache))
				{
					OnFollowLink(fwargs);
					args.LinkHandledLocally = true;
				}
			}
			catch (Exception)
			{
				// Something went wrong, probably its not a kind of link we understand.
			}
			return true;
		}

		private bool SameDatabase(FwAppArgs fwargs, FdoCache cache)
		{
			return fwargs.Database == "this$" ||
				fwargs.Database.ToLowerInvariant() == cache.ProjectId.Name.ToLowerInvariant()
				|| fwargs.Database.ToLowerInvariant() == cache.ProjectId.Path.ToLowerInvariant()
				|| Path.GetFileName(fwargs.Database).ToLowerInvariant() == cache.ProjectId.Name.ToLowerInvariant();
		}

		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public bool OnAddContextToHistory(object _link)
		{
			CheckDisposed();

			//Debug.WriteLineIf(RuntimeSwitches.linkListenerSwitch.TraceInfo, "OnAddContextToHistory(" + m_currentContext + ")", RuntimeSwitches.linkListenerSwitch.DisplayName);
			FwLinkArgs lnk = (FwLinkArgs)_link;
			if (lnk.EssentiallyEquals(m_currentContext))
			{
				//Debug.WriteLineIf(RuntimeSwitches.linkListenerSwitch.TraceInfo, "   Link equals current context.", RuntimeSwitches.linkListenerSwitch.DisplayName);
				return true;
			}
			if (m_currentContext != null &&
				//not where we just came from via a "Back" call
				((m_forwardStack.Count == 0) || (m_currentContext != m_forwardStack.Last.Value)))
			{
				//Debug.WriteLineIf(RuntimeSwitches.linkListenerSwitch.TraceInfo, "  Pushing current to back: " + m_currentContext, RuntimeSwitches.linkListenerSwitch.DisplayName);
				Push(m_backStack, m_currentContext);
			}
			// Try to omit intermediate targets which are added to the stack when switching
			// tools.  This doesn't work in OnFollowLink() because the behavior of following
			// the link is not synchronous even when SendMessage is used at the first two
			// levels of handling.
			if (m_fFollowingLink && lnk.EssentiallyEquals(m_lnkActive))
			{
				int howManyAdded = m_backStack.Count - m_cBackStackOrig;
				for( ; howManyAdded > 1; --howManyAdded)
				{
					m_backStack.RemoveLast();
				}
				m_fFollowingLink = false;
				m_cBackStackOrig = 0;
				m_lnkActive = null;
			}
			// The forward stack should be cleared by jump operations that are NOT spawned by
			// a Back or Forward (ie, history) operation.  This is the standard behavior in
			// browsers, for example (as far as I know).
			if (m_fUsingHistory)
			{
				if (lnk.EssentiallyEquals(m_lnkActive))
				{
					m_fUsingHistory = false;
					m_lnkActive = null;
				}
			}
			else
			{
				m_forwardStack.Clear();
			}

			m_currentContext = lnk;
			return true;
		}

		private void Push(LinkedList<FwLinkArgs> stack, FwLinkArgs context)
		{
			stack.AddLast(context);
			while (stack.Count > kmaxDepth)
				stack.RemoveFirst();
		}

		private FwLinkArgs Pop(LinkedList<FwLinkArgs> stack)
		{
			FwLinkArgs lnk = stack.Last.Value;
			stack.RemoveLast();
			return lnk;
		}

		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public bool OnCopyLocationAsHyperlink(object unused)
		{
			CheckDisposed();
			if (m_currentContext != null)
			{
				FdoCache cache = PropertyTable.GetValue<FdoCache>("cache");
				var args = new FwAppArgs(cache.ProjectId.Handle,
					m_currentContext.ToolName, m_currentContext.TargetGuid);
				ClipboardUtils.SetDataObject(args.ToString(), true);
			}
			return true;
		}

		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public bool OnHistoryBack(object unused)
		{
			CheckDisposed();

			if (m_backStack.Count > 0)
			{
				if (m_currentContext!= null)
				{
					Push(m_forwardStack, m_currentContext);
				}
				m_fUsingHistory = true;
				m_lnkActive = Pop(m_backStack);
				FollowActiveLink();
			}

			return true;
		}

		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public bool OnHistoryForward(object unused)
		{
			CheckDisposed();

			if (m_forwardStack.Count > 0)
			{
				m_fUsingHistory = true;
				m_lnkActive = Pop(m_forwardStack);
				FollowActiveLink();
			}
			return true;
		}

#if RANDYTODO
		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public bool OnDisplayHistoryForward(object commandObject, ref UIItemDisplayProperties display)
		{
			CheckDisposed();

			display.Enabled = m_forwardStack.Count > 0;
			return true;
		}
		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public bool OnDisplayHistoryBack(object commandObject, ref UIItemDisplayProperties display)
		{
			CheckDisposed();

			display.Enabled = m_backStack.Count > 0;
			return true;
		}
#endif

		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public bool OnTestFollowLink(object unused)
		{
			CheckDisposed();
			FdoCache cache = PropertyTable.GetValue<FdoCache>("cache");
			Guid[] guids = (from entry in cache.LanguageProject.LexDbOA.Entries select entry.Guid).ToArray();
			var commands = new List<string>
									{
										"AboutToFollowLink",
										"FollowLink"
									};
			var parms = new List<object>
									{
										null,
										new FwLinkArgs("lexiconEdit", guids[guids.Length - 1])
									};
			Publisher.Publish(commands, parms);
			return true;
		}

		/// <summary>
		/// NOTE: This will not handle link requests for other databases/applications. To handle other
		/// databases or applications, pass a FwAppArgs to the IFieldWorksManager.HandleLinkRequest method.
		/// </summary>
		/// <returns></returns>
		public bool OnFollowLink(object lnk)
		{
			CheckDisposed();

			m_fFollowingLink = true;
			m_cBackStackOrig = m_backStack.Count;
			m_lnkActive = lnk as FwLinkArgs;

			return FollowActiveLink();
		}

		private bool FollowActiveLink()
		{
			try
			{
				//Debug.Assert(!(m_lnkActive is FwAppArgs), "Beware: This will not handle link requests for other databases/applications." +
				//	" To handle other databases or applications, pass the FwAppArgs to the IFieldWorksManager.HandleLinkRequest method.");
				if (m_lnkActive.ToolName == "default")
				{
					// Need some smarts here. The link creator was not sure what tool to use.
					// The object may also be a child we don't know how to jump to directly.
					var cache = PropertyTable.GetValue<FdoCache>("cache");
					ICmObject target;
					if (!cache.ServiceLocator.ObjectRepository.TryGetObject(m_lnkActive.TargetGuid, out target))
						return false; // or message?
					var realTarget = GetObjectToShowInTool(target);
					string realTool;
					var majorObject = realTarget.Owner ?? realTarget;
					switch (majorObject.ClassID)
					{
						case ReversalIndexTags.kClassId:
							realTool = "reversalEditComplete";
							break;
						case TextTags.kClassId:
							realTool = "interlinearEdit";
							break;
						case LexEntryTags.kClassId:
							realTool = "lexiconEdit";
							break;
						case ScriptureTags.kClassId:
							ShowCantJumpMessage(xWorksStrings.ksCantJumpToScripture);
							return false; // Todo: don't know how to handle this yet.
							//app = FwUtils.ksTeAbbrev;
							//realTool = "reversalEditComplete";
							//break;
						case CmPossibilityListTags.kClassId:
							// The area listener knows about the possible list tools.
							// Unfortunately AreaListener is in an assembly we can't reference.
							// But there may be custom ones, so just listing them all here does not seem to be an option,
							// and anyway it would be hard to maintain.
							// Thus we've created this method (on AreaListener) which we call awkwardly throught the mediator.
							var parameters = new object[2];
							parameters[0] = majorObject;
							Publisher.Publish("GetToolForList", parameters);
							realTool = (string)parameters[1];
							break;
						case RnResearchNbkTags.kClassId:
							realTool = "notebookEdit";
							break;
						case DsConstChartTags.kClassId:
							realTarget = ((IDsConstChart) majorObject).BasedOnRA;
							realTool = "interlinearEdit";
							// Enhance JohnT: do something to make it switch to Discourse tab
							break;
						case LexDbTags.kClassId: // other things owned by this??
						case LangProjectTags.kClassId:
							ShowCantJumpMessage(xWorksStrings.ksCantJumpToLangProj);
							return false;
						default:
							var msg = string.Format(xWorksStrings.ksCantJumpToObject,
								cache.MetaDataCacheAccessor.GetClassName(majorObject.ClassID));
							ShowCantJumpMessage(msg);
							return false; // can't jump to it.
					}
					m_lnkActive = new FwLinkArgs(realTool, realTarget.Guid);
					// Todo JohnT: need to do something special here if we c
				}
				// It's important to do this AFTER we set the real tool name if it is "default". Otherwise, the code that
				// handles the jump never realizes we have reached the desired tool (as indicated by the value of
				// SuspendLoadingRecordUntilOnJumpToRecord) and we stop recording context history and various similar problems.
				if (m_lnkActive.TargetGuid != Guid.Empty)
				{
					// allow tools to skip loading a record if we're planning to jump to one.
					// interested tools will need to reset this "JumpToRecord" property after handling OnJumpToRecord.
					PropertyTable.SetProperty("SuspendLoadingRecordUntilOnJumpToRecord",
						m_lnkActive.ToolName + "," + m_lnkActive.TargetGuid,
						SettingsGroup.LocalSettings,
						false,
						true);
				}
#if RANDYTODO
				// TODO: I supect the following three 'Publish' calls need to be merged into one, or we may not be alive to come back to after one or the other of them.
#endif
				Publisher.Publish("SetToolFromName", m_lnkActive.ToolName);
				// Note: It can be Guid.Empty in cases where it was never set,
				// or more likely, when the HVO was set to -1.
				if (m_lnkActive.TargetGuid != Guid.Empty)
				{
					FdoCache cache = PropertyTable.GetValue<FdoCache>("cache");
					ICmObject obj = cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(m_lnkActive.TargetGuid);
					if (obj is IReversalIndexEntry && m_lnkActive.ToolName == "reversalEditComplete")
					{
						// For the reversal index tool, just getting the tool right isn't enough.  We
						// also need to be showing the proper index.  (See FWR-1105.)
						var guid = ReversalIndexEntryUi.GetObjectGuidIfValid(PropertyTable, "ReversalIndexGuid");
						if (!guid.Equals(obj.Owner.Guid))
						{
							PropertyTable.SetProperty("ReversalIndexGuid", obj.Owner.Guid.ToString(), true, true);
						}
					}
					// Allow this to happen after the processing of the tool change above by using the Broadcast
					// method on the mediator, the SendMessage would process it before the above msg and it would
					// use the wrong RecordList.  (LT-3260)
					Publisher.Publish("JumpToRecord", obj.Hvo);
				}

				foreach (Property property in m_lnkActive.PropertyTableEntries)
				{
					PropertyTable.SetProperty(property.name, property.value, true, true);
					//TODO: I can't think at the moment of what to do about setting
					//the persistence or ownership of the property...at the moment the only values we're putting
					//in there are strings or bools
				}
				Publisher.Publish("LinkFollowed", m_lnkActive);
			}
			catch(Exception err)
			{
				string s;
				if (err.InnerException != null && !string.IsNullOrEmpty(err.InnerException.Message))
					s = String.Format(xWorksStrings.UnableToFollowLink0, err.InnerException.Message);
				else
					s = xWorksStrings.UnableToFollowLink;
				MessageBox.Show(s, xWorksStrings.FailedJump, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return false;
			}
			return true;	//we handled this.
		}

		private void ShowCantJumpMessage(string msg)
		{
			var activeFlexWindow = PropertyTable.GetValue<Form>("window");
			if (activeFlexWindow == null)
				activeFlexWindow = Form.ActiveForm;
			if (activeFlexWindow == null)
				MessageBox.Show(activeFlexWindow, msg, xWorksStrings.ksCantJumpCaption);
			else
			{
				activeFlexWindow.Invoke(
					(Action) (() => MessageBox.Show(activeFlexWindow, msg, xWorksStrings.ksCantJumpCaption)));
			}
		}

		/// <summary>
		/// Get the object we want to point our tool at. This is typically the one that is one level down from
		/// a CmMajorObject.
		/// </summary>
		/// <param name="start"></param>
		/// <returns></returns>
		ICmObject GetObjectToShowInTool(ICmObject start)
		{
			for(var current = start;;current = current.Owner)
			{
				if (current.Owner == null)
					return current;
				if (current.Owner is ICmMajorObject)
					return current;
			}
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

		/// <summary>
		/// Initialize a FLEx component with the basic interfaces.
		/// </summary>
		/// <param name="flexComponentParameters">Parameter object that contains the required three interfaces.</param>
		public void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
		{
			FlexComponentCheckingService.CheckInitializationValues(flexComponentParameters, new FlexComponentParameters(PropertyTable, Publisher, Subscriber));

			PropertyTable = flexComponentParameters.PropertyTable;
			Publisher = flexComponentParameters.Publisher;
			Subscriber = flexComponentParameters.Subscriber;

			PropertyTable.SetProperty("LinkListener", this, false, false);
		}

		#endregion
	}
}
