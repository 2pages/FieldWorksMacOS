// Copyright (c) 2009-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Diagnostics;
using LanguageExplorer.Controls;
using LanguageExplorer.LcmUi;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;

namespace LanguageExplorer.Areas.Grammar.Tools.PhonologicalFeaturesAdvancedEdit
{
	/// <summary />
	internal sealed class PhonologicalFeatureListDlgLauncherView : RootSiteControl, IVwNotifyChange
	{
		private IFsFeatStruc m_fs;
		private CmAnalObjectVc m_vc;

		/// <summary />
		public void Init(LcmCache cache, IFsFeatStruc fs)
		{
			m_fs = fs;
			m_cache = cache;

			UpdateRootObject();
			m_cache.DomainDataByFlid.AddNotification(this);
		}

		/// <summary />
		protected override void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}

			if (disposing)
			{
				m_cache.DomainDataByFlid.RemoveNotification(this);
			}

			base.Dispose(disposing);
		}

		/// <summary />
		public void UpdateFS(IFsFeatStruc fs)
		{
			m_fs = fs;
			UpdateRootObject();
		}

		private void UpdateRootObject()
		{
			if (RootBox == null)
			{
				MakeRoot();
			}
			else if (m_fs != null)
			{
				RootBox.SetRootObject(m_fs.Hvo, m_vc, (int)VcFrags.kfragName, RootBox.Stylesheet);
				RootBox.Reconstruct();
			}
		}

		/// <summary />
		public IPhPhoneme Phoneme { get; set; }

		#region RootSite required methods

		/// <summary />
		public override void MakeRoot()
		{
			if (m_cache == null || DesignMode)
			{
				return;
			}
			base.MakeRoot();

			RootBox.DataAccess = m_cache.MainCacheAccessor;
			m_vc = new CmAnalObjectVc(m_cache);

			if (m_fs != null)
			{
				RootBox.SetRootObject(m_fs.Hvo, m_vc, (int)VcFrags.kfragName, RootBox.Stylesheet);
			}
		}

		#endregion // RootSite required methods

		/// <summary>
		/// Listen for change to basic IPA symbol
		/// If description and/or features are empty, try to supply the values associated with the symbol
		/// </summary>
		public void PropChanged(int hvo, int tag, int ivMin, int cvIns, int cvDel)
		{
			if (hvo == 0)
			{
				return;
			}
			// We only want to do something when the basic IPA symbol changes
			if ((tag != PhPhonemeTags.kflidFeatures) && (tag != FsFeatStrucTags.kflidFeatureSpecs))
			{
				return;
			}
			switch (tag)
			{
				case FsFeatStrucTags.kflidFeatureSpecs:
				{
					var featStruc = m_cache.ServiceLocator.GetInstance<IFsFeatStrucRepository>().GetObject(hvo);
					// only want to do something when the feature structure is part of a IPhPhoneme))
					if (featStruc.OwningFlid != PhPhonemeTags.kflidFeatures)
					{
						return;
					}
					break;
				}
				case PhPhonemeTags.kflidFeatures when Phoneme != null && hvo == Phoneme.Hvo:
				{
					m_fs = Phoneme.FeaturesOA;
					if (m_fs != null)
					{
						RootBox?.SetRootObject(m_fs.Hvo, m_vc, (int)VcFrags.kfragName, RootBox.Stylesheet);
					}
					break;
				}
			}
			RootBox?.Reconstruct();
		}
	}
}