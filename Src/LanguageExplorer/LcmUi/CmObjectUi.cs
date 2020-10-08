// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Xml.Linq;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;
using SIL.Reporting;

namespace LanguageExplorer.LcmUi
{
	internal class CmObjectUi : IFlexComponent, IDisposable
	{
		#region Data members

		protected ICmObject m_cmObject;
		protected int m_hvo;
		protected LcmCache m_cache;
		// Map from uint to uint, specifically, from clsid to clsid.
		// The key is any clsid that we have so far been asked to make a UI object for.
		// The value is the corresponding clsid that actually occurs in the switch.
		// Review JohnH (JohnT): would it be more efficient to store a Class object in the map,
		// and use reflection to make an instance?
		static readonly Dictionary<int, int> m_subclasses = new Dictionary<int, int>();
		protected IVwViewConstructor m_vc;

		#endregion Data members

		#region Properties

		/// <summary>
		/// Retrieve the CmObject we are providing UI functions for.
		/// </summary>
		internal ICmObject MyCmObject => m_cmObject ?? (m_cmObject = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(m_hvo));

		internal string ClassName => MyCmObject.ClassName;

		/// <summary>
		/// Returns a View Constructor that can be used to produce various displays of the
		/// object. Various fragments may be supported, depending on the class.
		///
		/// Typical usage:
		/// 		public override void Display(IVwEnv vwenv, int hvo, int frag)
		/// 		{
		/// 		...
		/// 		switch(frag)
		/// 		{
		/// 		...
		/// 		case sometypeshownbyshortname:
		/// 			IVwViewConstructor vcName = CmObjectUi.MakeLcmModelUiObject(m_cache, hvo).Vc;
		/// 			vwenv.AddObj(hvo, vcName, VcFrags.kfragShortName);
		/// 			break;
		/// 		...
		/// 		}
		///
		/// Note that this involves putting an extra level of object structure into the display,
		/// unless it is done in an AddObjVec loop, where AddObj is needed anyway for each object.
		/// This is unavoidable in cases where the property involves polymorphic objects.
		/// If all objects in a sequence are the same type, the appropriate Vc may be retrieved
		/// in the fragment that handles the sequence and passed to AddObjVecItems.
		/// If an atomic property is to be displayed in this way, code like the following may be used:
		///			case something:
		///				...// possibly other properties of containing object.
		///				// Display shortname of object in atomic object property XYZ
		///				int hvoObj = vwenv.DataAccess.get_ObjectProp(hvo, kflidXYZ);
		///				IVwViewConstructor vcName = CmObjectUi.MakeLcmModelUiObject(m_cache, hvoObj).Vc;
		///				vwenv.AddObjProp(kflidXYZ, vcName, VcFrags.kfragShortName);
		///				...
		///				break;
		/// </summary>
		internal virtual IVwViewConstructor Vc => m_vc ?? (m_vc = new CmObjectVc(m_cache));

		/// <summary>
		/// Returns a View Constructor that can be used to produce various displays of the
		/// object in the default vernacular writing system.  Various fragments may be
		/// supported, depending on the class.
		///
		/// Typical usage:
		/// 		public override void Display(IVwEnv vwenv, int hvo, int frag)
		/// 		{
		/// 		...
		/// 		switch(frag)
		/// 		{
		/// 		...
		/// 		case sometypeshownbyshortname:
		/// 			IVwViewConstructor vcName = CmObjectUi.MakeLcmModelUiObject(m_cache, hvo).VernVc;
		/// 			vwenv.AddObj(hvo, vcName, VcFrags.kfragShortName);
		/// 			break;
		/// 		...
		/// 		}
		///
		/// Note that this involves putting an extra level of object structure into the display,
		/// unless it is done in an AddObjVec loop, where AddObj is needed anyway for each
		/// object.  This is unavoidable in cases where the property involves polymorphic
		/// objects.  If all objects in a sequence are the same type, the appropriate Vc may be
		/// retrieved in the fragment that handles the sequence and passed to AddObjVecItems.
		/// If an atomic property is to be displayed in this way, code like the following may be
		/// used:
		///			case something:
		///				...// possibly other properties of containing object.
		///				// Display shortname of object in atomic object property XYZ
		///				int hvoObj = vwenv.DataAccess.get_ObjectProp(hvo, kflidXYZ);
		///				IVwViewConstructor vcName = CmObjectUi.MakeLcmModelUiObject(m_cache, hvoObj).VernVc;
		///				vwenv.AddObjProp(kflidXYZ, vcName, VcFrags.kfragShortName);
		///				...
		///				break;
		/// </summary>
		internal virtual IVwViewConstructor VernVc => new CmVernObjectVc(m_cache);

		internal virtual IVwViewConstructor AnalVc => new CmAnalObjectVc(m_cache);
		#endregion Properties

		#region Construction and initialization

		/// <summary>
		/// If you KNOW for SURE the right subclass of CmObjectUi, you can just make one
		/// directly. Most clients should use MakeLcmModelUiObject.
		/// </summary>
		protected CmObjectUi(ICmObject obj)
		{
			m_cmObject = obj;
			m_cache = obj.Cache;
		}

		/// <summary>
		/// This should only be used by MakeLcmModelUiObject.
		/// </summary>
		protected CmObjectUi()
		{
		}

		/// <summary>
		/// In many cases we don't really need the LCM object, which can be relatively expensive
		/// to create. This version saves the information, and creates it when needed.
		/// </summary>
		internal static CmObjectUi MakeLcmModelUiObject(LcmCache cache, int hvo)
		{
			return MakeLcmModelUiObject(cache, hvo, cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(hvo).ClassID);
		}

		private static CmObjectUi MakeLcmModelUiObject(LcmCache cache, int hvo, int clsid)
		{
			var mdc = cache.DomainDataByFlid.MetaDataCache;
			// If we've encountered an object with this Clsid before, and this clsid isn't in
			// the switch below, the dictionary will give us the appropriate clsid that IS in the
			// map, so the loop below will have only one iteration. Otherwise, we start the
			// search with the clsid of the object itself.
			var realClsid = m_subclasses.ContainsKey(clsid) ? m_subclasses[clsid] : clsid;
			// Each iteration investigates whether we have a CmObjectUi subclass that
			// corresponds to realClsid. If not, we move on to the base class of realClsid.
			// In this way, the CmObjectUi subclass we return is the one designed for the
			// closest base class of obj that has one.
			CmObjectUi result = null;
			while (result == null)
			{
				switch (realClsid)
				{
					// Todo: lots more useful cases.
					case WfiAnalysisTags.kClassId:
						result = new WfiAnalysisUi();
						break;
					case PartOfSpeechTags.kClassId:
						result = new PartOfSpeechUi();
						break;
					case CmPossibilityTags.kClassId:
						result = new CmPossibilityUi();
						break;
					case CmObjectTags.kClassId:
						result = new CmObjectUi();
						break;
					case LexPronunciationTags.kClassId:
						result = new LexPronunciationUi();
						break;
					case LexSenseTags.kClassId:
						result = new LexSenseUi();
						break;
					case LexEntryTags.kClassId:
						result = new LexEntryUi();
						break;
					case MoInflAffMsaTags.kClassId:
					case MoDerivAffMsaTags.kClassId:
					case MoMorphSynAnalysisTags.kClassId:
					case MoStemMsaTags.kClassId:
						result = new MoMorphSynAnalysisUi();
						break;
					case MoAffixAllomorphTags.kClassId:
					case MoStemAllomorphTags.kClassId:
						result = new MoFormUi();
						break;
					case ReversalIndexEntryTags.kClassId:
						result = new ReversalIndexEntryUi();
						break;
					case WfiWordformTags.kClassId:
						result = new WfiWordformUi();
						break;
					case WfiGlossTags.kClassId:
						result = new WfiGlossUi();
						break;
					default:
						realClsid = mdc.GetBaseClsId(realClsid);
						break;
				}
			}
			if (realClsid != clsid)
			{
				m_subclasses[clsid] = realClsid;
			}
			result.m_hvo = hvo;
			result.m_cache = cache;
			return result;
		}

		/// <summary>
		/// Create a new LCM object.
		/// </summary>
		internal static CmObjectUi MakeLcmModelUiObject(IPropertyTable propertyTable, IPublisher publisher, int classId, int hvoOwner, int flid, int insertionPosition)
		{
			var cache = propertyTable.GetValue<LcmCache>(FwUtilsConstants.cache);
			switch (classId)
			{
				default:
					return MakeLcmModelUiObject(classId, hvoOwner, flid, insertionPosition, cache);
				case CmPossibilityTags.kClassId:
					return CmPossibilityUi.MakeLcmModelUiObject(cache, classId, hvoOwner, flid, insertionPosition);
				case PartOfSpeechTags.kClassId:
					return PartOfSpeechUi.MakeLcmModelUiObject(cache, propertyTable, publisher, classId, hvoOwner, flid, insertionPosition);
				case FsFeatDefnTags.kClassId:
					return FsFeatDefnUi.MakeLcmModelUiObject(cache, propertyTable, publisher, classId, hvoOwner, flid, insertionPosition);
				case LexSenseTags.kClassId:
					return LexSenseUi.MakeLcmModelUiObject(cache, hvoOwner, insertionPosition);
				case LexPronunciationTags.kClassId:
					return LexPronunciationUi.MakeLcmModelUiObject(cache, classId, hvoOwner, flid, insertionPosition);
			}
		}

		internal static CmObjectUi MakeLcmModelUiObject(int classId, int hvoOwner, int flid, int insertionPosition, LcmCache cache)
		{
			CmObjectUi newUiObj = null;
			UndoableUnitOfWorkHelper.Do(LcmUiResources.ksUndoInsert, LcmUiResources.ksRedoInsert, cache.ServiceLocator.GetInstance<IActionHandler>(), () =>
			{
				var newHvo = cache.DomainDataByFlid.MakeNewObject(classId, hvoOwner, flid, insertionPosition);
				newUiObj = MakeLcmModelUiObject(cache, newHvo, classId);
			});
			return newUiObj;
		}

		#endregion Construction and initialization

		#region IDisposable & Co. implementation

		/// <summary>
		/// See if the object has been disposed.
		/// </summary>
		private bool IsDisposed { get; set; }

		/// <summary>
		/// Finalizer, in case client doesn't dispose it.
		/// Force Dispose(false) if not already called (i.e. m_isDisposed is true)
		/// </summary>
		/// <remarks>
		/// In case some clients forget to dispose it directly.
		/// </remarks>
		~CmObjectUi()
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
			// Therefore, you should call GC.SuppressFinalize to
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
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}

			if (disposing)
			{
				// Dispose managed resources here.
				PropertyTable?.GetValue<IFwMainWnd>(FwUtilsConstants.window).IdleQueue.Remove(TryToDeleteFile);
				(m_vc as IDisposable)?.Dispose();
			}

			// Dispose unmanaged resources here, whether disposing is true or false.
			m_cmObject = null;
			m_cache = null;
			m_vc = null;
			PropertyTable = null;
			Publisher = null;
			Subscriber = null;

			IsDisposed = true;

			// Keep this from being collected, since it got removed from the static.
			GC.KeepAlive(this);
		}

		#endregion IDisposable & Co. implementation

		#region Jumping

		/// <summary>
		/// Return either the object or an owner ("parent") up the ownership chain that is of
		/// the desired class.  Being a subclass of the desired class also matches, unlike
		/// ICmObject.OwnerOfClass() where the class must match exactly.
		/// </summary>
		internal static ICmObject GetSelfOrParentOfClass(ICmObject cmo, int classIdToSearchFor)
		{
			if (cmo == null)
			{
				return null;
			}
			var mdc = cmo.Cache.DomainDataByFlid.MetaDataCache;
			for (; cmo != null; cmo = cmo.Owner)
			{
				if ((DomainObjectServices.IsSameOrSubclassOf(mdc, cmo.ClassID, classIdToSearchFor)))
				{
					return cmo;
				}
			}
			return null;
		}

		private ICmObject GetCurrentCmObject()
		{
			return PropertyTable.GetValue<ICmObject>(LanguageExplorerConstants.ActiveListSelectedObject, null);
		}

		#endregion

		#region Other methods

		internal virtual bool CanDelete(out string cannotDeleteMsg)
		{
			if (MyCmObject.CanDelete)
			{
				cannotDeleteMsg = null;
				return true;
			}
			cannotDeleteMsg = LcmUiResources.ksCannotDeleteItem;
			return false;
		}

		/// <summary>
		/// Delete the object, after showing a confirmation dialog.
		/// Return true if deleted, false, if cancelled.
		/// </summary>
		internal bool DeleteUnderlyingObject()
		{
			var cmo = GetCurrentCmObject();
			if (cmo != null && m_cmObject != null && cmo.Hvo == m_cmObject.Hvo)
			{
				Publisher.Publish(new PublisherParameterObject("DeleteRecord", this));
			}
			else
			{
				var mainWindow = PropertyTable.GetValue<Form>(FwUtilsConstants.window);
				using (new WaitCursor(mainWindow))
				{
					using (var dlg = new ConfirmDeleteObjectDlg(PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider)))
					{
						if (CanDelete(out var cannotDeleteMsg))
						{
							dlg.SetDlgInfo(MyCmObject, m_cache, PropertyTable);
						}
						else
						{
							dlg.SetDlgInfo(MyCmObject, m_cache, PropertyTable, TsStringUtils.MakeString(cannotDeleteMsg, m_cache.DefaultUserWs));
						}
						if (DialogResult.Yes == dlg.ShowDialog(mainWindow))
						{
							ReallyDeleteUnderlyingObject();
							return true; // deleted it
						}
					}
				}
			}
			return false; // didn't delete it.
		}

		/// <summary>
		/// Do any cleanup that involves interacting with the user, after the user has confirmed that our object should be
		/// deleted.
		/// </summary>
		protected virtual void DoRelatedCleanupForDeleteObject()
		{
			// For media and pictures: should we delete the file also?
			// arguably this should be on a subclass, but it's easier to share behavior for both here.
			ICmFile file = null;
			if (m_cmObject is ICmPicture pict)
			{
				file = pict.PictureFileRA;
			}
			else if (m_cmObject is ICmMedia media)
			{
				file = media.MediaFileRA;
			}
			else if (m_cmObject != null)
			{
				// No cleanup needed
				return;
			}
			ConsiderDeletingRelatedFile(file, PropertyTable);
		}

		internal static bool ConsiderDeletingRelatedFile(ICmFile file, IPropertyTable propertyTable)
		{
			if (file == null)
			{
				return false;
			}
			var refs = file.ReferringObjects;
			if (refs.Count > 1)
			{
				return false; // exactly one if only this CmPicture uses it.
			}
			var path = file.InternalPath;
			if (Path.IsPathRooted(path))
			{
				return false; // don't delete external file
			}
			var msg = string.Format(LcmUiResources.ksDeleteFileAlso, path);
			if (MessageBox.Show(Form.ActiveForm, msg, LcmUiResources.ksDeleteFileCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			{
				return false;
			}
			if (propertyTable != null && propertyTable.TryGetValue(LanguageExplorerConstants.App, out IFlexApp app))
			{
				app.PictureHolder.ReleasePicture(file.AbsoluteInternalPath);
			}
			var fileToDelete = file.AbsoluteInternalPath;
			propertyTable.GetValue<IFwMainWnd>(FwUtilsConstants.window).IdleQueue.Add(IdleQueuePriority.Low, TryToDeleteFile, fileToDelete);
			return false;
		}

		private static bool TryToDeleteFile(object param)
		{
			try
			{
				// I'm not sure why, but if we try to delete it right away, we typically get a failure,
				// with an exception indicating that something is using the file, despite the code above that
				// tries to make our picture cache let go of it.
				// However, waiting until idle seems to solve the problem.
				File.Delete((string)param);
			}
			catch (IOException)
			{
				// If we can't actually delete the file for some reason, don't bother the user complaining.
			}
			return true; // task is complete, don't try again.
		}

		protected virtual void ReallyDeleteUnderlyingObject()
		{
			Logger.WriteEvent("Deleting '" + MyCmObject.ShortName + "'...");
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW(LcmUiResources.ksUndoDelete, LcmUiResources.ksRedoDelete, m_cache.ActionHandlerAccessor, () =>
			{
				DoRelatedCleanupForDeleteObject();
				MyCmObject.Cache.DomainDataByFlid.DeleteObj(MyCmObject.Hvo);
			});
			Logger.WriteEvent("Done Deleting.");
			m_cmObject = null;
		}

		/// <summary>
		/// Merge the underling objects. This method handles the confirm dialog, then delegates
		/// the actual merge to ReallyMergeUnderlyingObject. If the flag is true, we merge
		/// strings and owned atomic objects; otherwise, we don't change any that aren't null
		/// to begin with.
		/// </summary>
		internal void MergeUnderlyingObject(bool fLoseNoTextData)
		{
			var mainWindow = PropertyTable.GetValue<Form>(FwUtilsConstants.window);
			using (new WaitCursor(mainWindow))
			using (var dlg = new MergeObjectDlg(PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider)))
			{
				dlg.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
				var wp = new WindowParams();
				var mergeCandidates = new List<DummyCmObject>();
				var dObj = GetMergeinfo(wp, mergeCandidates, out var guiControlParameters, out var helpTopic);
				mergeCandidates.Sort();
				dlg.SetDlgInfo(m_cache, wp, dObj, mergeCandidates, guiControlParameters, helpTopic);
				if (DialogResult.OK == dlg.ShowDialog(mainWindow))
				{
					ReallyMergeUnderlyingObject(dlg.Hvo, fLoseNoTextData);
				}
			}
		}

		/// <summary>
		/// Merge the underling objects. This method handles the transaction, then delegates
		/// the actual merge to MergeObject. If the flag is true, we merge
		/// strings and owned atomic objects; otherwise, we don't change any that aren't null
		/// to begin with.
		/// </summary>
		protected virtual void ReallyMergeUnderlyingObject(int survivorHvo, bool fLoseNoTextData)
		{
			var survivor = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(survivorHvo);
			Logger.WriteEvent("Merging '" + MyCmObject.ShortName + "' into '" + survivor.ShortName + "'.");
			var ah = m_cache.ServiceLocator.GetInstance<IActionHandler>();
			UndoableUnitOfWorkHelper.Do(LcmUiResources.ksUndoMerge, LcmUiResources.ksRedoMerge, ah, () => survivor.MergeObject(MyCmObject, fLoseNoTextData));
			Logger.WriteEvent("Done Merging.");
			m_cmObject = null;
		}

		protected virtual DummyCmObject GetMergeinfo(WindowParams wp, List<DummyCmObject> mergeCandidates, out XElement guiControlParameters, out string helpTopic)
		{
			Debug.Assert(false, "Subclasses must override this method.");
			guiControlParameters = null;
			helpTopic = null;
			return null;
		}

		/// <summary />
		internal virtual void MoveUnderlyingObjectToCopyOfOwner()
		{
			MessageBox.Show(PropertyTable.GetValue<Form>(FwUtilsConstants.window), LcmUiResources.ksCannotMoveObjectToCopy, LcmUiResources.ksBUG);
		}

		#endregion Other methods

		#region Implementation of IPropertyTableProvider

		/// <summary>
		/// Placement in the IPropertyTableProvider interface lets FwApp call IPropertyTable.DoStuff.
		/// </summary>
		public IPropertyTable PropertyTable { get; set; }

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
		public virtual void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
		{
			FlexComponentParameters.CheckInitializationValues(flexComponentParameters, new FlexComponentParameters(PropertyTable, Publisher, Subscriber));

			PropertyTable = flexComponentParameters.PropertyTable;
			Publisher = flexComponentParameters.Publisher;
			Subscriber = flexComponentParameters.Subscriber;
		}

		#endregion
	}
}