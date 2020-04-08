// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Diagnostics;
using LanguageExplorer.LcmUi;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;

namespace LanguageExplorer.Controls.DetailControls.Slices
{
	/// <summary />
	internal sealed class MSADlglauncherView : RootSiteControl, IVwNotifyChange
	{
		private IMoMorphSynAnalysis m_msa;
		private MsaVc m_vc;

		private System.ComponentModel.IContainer components = null;

		/// <summary />
		internal MSADlglauncherView()
		{
			InitializeComponent();
		}

		internal void Init(LcmCache cache, IMoMorphSynAnalysis msa)
		{
			Debug.Assert(msa != null);
			m_msa = msa;
			m_cache = cache;
			if (RootBox == null)
			{
				MakeRoot();
			}
			else
			{
				RootBox.SetRootObject(m_msa.Hvo, Vc, (int)VcFrags.kfragFullMSAInterlinearname, RootBox.Stylesheet);
				RootBox.Reconstruct();
			}
			m_cache.DomainDataByFlid.AddNotification(this);
		}

		private IVwViewConstructor Vc => m_vc ?? (m_vc = new MsaVc(m_cache));

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
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
				components?.Dispose();
				m_cache?.DomainDataByFlid.RemoveNotification(this);
			}
			m_vc = null;
			m_msa = null;

			base.Dispose(disposing);
		}

		#region RootSite required methods

		public override void MakeRoot()
		{
			if (m_cache == null || DesignMode)
			{
				return;
			}
			base.MakeRoot();

			RootBox.DataAccess = m_cache.DomainDataByFlid;
			RootBox.SetRootObject(m_msa.Hvo, Vc, (int)VcFrags.kfragFullMSAInterlinearname, RootBox.Stylesheet);
		}

		#endregion // RootSite required methods

		#region Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			//
			// MSADlglauncherView
			//
			this.Name = "MSADlglauncherView";
			this.Size = new System.Drawing.Size(168, 24);

		}
		#endregion

		#region IVwNotifyChange Members

		public void PropChanged(int hvo, int tag, int ivMin, int cvIns, int cvDel)
		{
			if (m_msa.Hvo == hvo)
			{
				RootBox.Reconstruct();
			}
		}

		#endregion
	}
}