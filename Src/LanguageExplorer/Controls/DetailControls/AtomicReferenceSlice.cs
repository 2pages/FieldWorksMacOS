// Copyright (c) 2003-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.Xml;

namespace LanguageExplorer.Controls.DetailControls
{
	/// <summary />
	internal class AtomicReferenceSlice : ReferenceSlice, IVwNotifyChange
	{
		// remember width when OnSizeChanged called.
		private int m_dxLastWidth;
		/// <summary>
		/// Use this to do the Add/RemoveNotifications.
		/// </summary>
		private ISilDataAccess m_sda;

		/// <summary />
		protected AtomicReferenceSlice(Control control)
			: base(control)
		{
		}

		/// <summary />
		protected AtomicReferenceSlice(Control control, LcmCache cache, ICmObject obj, int flid)
			: base(control, cache, obj, flid)
		{
			m_sda = Cache.MainCacheAccessor;
			m_sda.AddNotification(this);
		}

		/// <summary />
		public AtomicReferenceSlice(LcmCache cache, ICmObject obj, int flid)
			: this(new AtomicReferenceLauncher(), cache, obj, flid)
		{
		}

		#region IDisposable override

		/// <inheritdoc />
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
				// Dispose managed resources here.
				m_sda?.RemoveNotification(this);
				if (Control is AtomicReferenceLauncher arl)
				{
					arl.ChoicesMade -= RefreshTree;
					arl.ViewSizeChanged -= OnViewSizeChanged;
					var view = (AtomicReferenceView)arl.MainControl;
					view.ViewSizeChanged -= OnViewSizeChanged;
				}
			}
			// Dispose unmanaged resources here, whether disposing is true or false.
			m_sda = null;

			base.Dispose(disposing);
		}

		#endregion IDisposable override

		public override void FinishInit()
		{
			base.FinishInit();
			var arl = (AtomicReferenceLauncher)Control;
			arl.Initialize(Cache, MyCmObject, m_flid, m_fieldName, PersistenceProvider, DisplayNameProperty, BestWsName); // TODO: Get better default 'best ws'.
			arl.ConfigurationNode = ConfigurationNode;
			var deParams = ConfigurationNode.Element("deParams");
			if (XmlUtils.GetOptionalBooleanAttributeValue(deParams, "changeRequiresRefresh", false))
			{
				arl.ChoicesMade += RefreshTree;
			}
			// We don't want to be visible until later, since otherwise we get a temporary
			// display in the wrong place with the wrong size that serves only to annoy the
			// user.  See LT-1518 "The drawing of the DataTree for Lexicon/Advanced Edit draws
			// some initial invalid controls."  Becoming visible when we set the width and
			// height seems to delay things enough to avoid this visual clutter.
			// Now done in Slice.ctor: arl.Visible = false;
			arl.ViewSizeChanged += OnViewSizeChanged;
			var view = (AtomicReferenceView)arl.MainControl;
			view.ViewSizeChanged += OnViewSizeChanged;
		}

		protected void RefreshTree(object sender, EventArgs args)
		{
			ContainingDataTree.RefreshList(false);
		}

		public override void ShowSubControls()
		{
			base.ShowSubControls();
			Control.Visible = true;
		}

		/// <summary>
		/// Handle changes in the size of the underlying view.
		/// </summary>
		protected void OnViewSizeChanged(object sender, FwViewSizeEventArgs e)
		{
			// When height is more than one line (e.g., long definition without gloss),
			// this can get called initially before it has a parent.
			if (ContainingDataTree == null)
			{
				return;
			}
			// For now, just handle changes in the height.
			var arl = (AtomicReferenceLauncher)Control;
			var view = (AtomicReferenceView)arl.MainControl;
			var hMin = ContainingDataTree.GetMinFieldHeight();
			var h1 = view.RootBox.Height;
			Debug.Assert(e.Height == h1);
			var hOld = TreeNode.Height;
			var hNew = Math.Max(h1, hMin) + 3;
			if (hNew <= hOld)
			{
				return;
			}
			TreeNode.Height = hNew;
			arl.Height = hNew - 1;
			view.Height = hNew - 1;
			Height = hNew;
		}

		protected override void OnSizeChanged(EventArgs e)
		{
			base.OnSizeChanged(e);
			if (Width == m_dxLastWidth)
			{
				return;
			}
			m_dxLastWidth = Width; // BEFORE doing anything, actions below may trigger recursive call.
			var arl = (AtomicReferenceLauncher)Control;
			var view = (AtomicReferenceView)arl.MainControl;
			view.PerformLayout();
			var h1 = view.RootBox.Height;
			var hNew = Math.Max(h1, ContainingDataTree.GetMinFieldHeight()) + 3;
			if (hNew != Height)
			{
				Height = hNew;
			}
		}

		protected override void UpdateDisplayFromDatabase()
		{
			var arl = (AtomicReferenceLauncher)Control;
			arl.UpdateDisplayFromDatabase();
		}

		#region IVwNotifyChange Members
		/// <summary>
		/// This PropChanged detects a needed UI update.  See LT-9002.
		/// </summary>
		public void PropChanged(int hvo, int tag, int ivMin, int cvIns, int cvDel)
		{
			if (m_flid != PartOfSpeechTags.kflidDefaultInflectionClass || cvIns != 0 || cvDel <= 0 || tag != PartOfSpeechTags.kflidInflectionClasses && tag != MoInflClassTags.kflidSubclasses
			    || ((IPartOfSpeech)MyCmObject).DefaultInflectionClassRA != null)
			{
				return;
			}
			var arl = (AtomicReferenceLauncher)Control;
			arl.UpdateDisplayFromDatabase();
		}

		#endregion
	}
}