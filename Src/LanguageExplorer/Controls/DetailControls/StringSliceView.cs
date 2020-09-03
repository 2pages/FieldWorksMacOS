// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Windows.Forms;
using SIL.FieldWorks.Common.RootSites;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Core.Cellar;
using SIL.LCModel.Core.KernelInterfaces;

namespace LanguageExplorer.Controls.DetailControls
{
	internal class StringSliceView : RootSiteControl, INotifyControlInCurrentSlice
	{
		private ICmObject m_obj;
		private readonly int m_hvoObj;
		private readonly int m_flid;
		private readonly int m_ws; // -1 signifies not a multilingual property
		private IVwViewConstructor m_vc;
		private bool m_fShowWsLabel;

		public StringSliceView(int hvo, int flid, int ws)
		{
			m_hvoObj = hvo;
			m_flid = flid;
			m_ws = ws;
			DoSpellCheck = true;
		}

		/// <summary>
		/// Set the flag to display writing system labels even for monolingual strings.
		/// </summary>
		public bool ShowWsLabel
		{
			set
			{
				m_fShowWsLabel = value;
				if (m_vc is StringSliceVc vc)
				{
					vc.ShowWsLabel = value;
				}
			}
		}

		/// <summary>
		/// Set the default writing system for this string.
		/// </summary>
		public int DefaultWs
		{
			set
			{
				if (m_vc is StringSliceVc vc)
				{
					vc.DefaultWs = value;
				}
			}
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

			base.Dispose(disposing);

			if (disposing)
			{
				// Dispose managed resources here.
				(m_vc as IDisposable)?.Dispose();
			}

			// Dispose unmanaged resources here, whether disposing is true or false.
			m_obj = null;
			m_vc = null;
		}

		#endregion IDisposable override

		/// <summary>
		/// Make a selection at the specified character offset.
		/// </summary>
		public void SelectAt(int ich)
		{
			try
			{
				RootBox.MakeTextSelection(0, 0, null, m_flid, 0, ich, ich, 0, true, -1, null, true);
			}
			catch
			{
			}
		}

		#region INotifyControlInCurrentSlice implementation

		/// <summary>
		/// Adjust controls based on whether the slice is the current slice.
		/// </summary>
		public bool SliceIsCurrent
		{
			set
			{
				if (!value)
				{
					DoValidation();
				}
			}
		}

		private void DoValidation()
		{
			// This may be called in the process of deleting the object after the object
			// has been partially cleared out and thus would certainly fail the constraint
			// check, then try to instantiate an error annotation which wouldn't have an
			// owner, causing bad things to happen.
			if (m_obj == null || !m_obj.IsValidObject)
			{
				return;
			}
			if (m_obj is IPhEnvironment environment)
			{
				environment.CheckConstraints(m_flid, true, out _, /* adjust squiggly line */ true);
			}
			else
			{
				m_obj.CheckConstraints(m_flid, true, out _);
			}
		}

		/// <summary>
		/// This method seems to get called when we are switching to another tool (or area, or slice) AND when the
		/// program is shutting down. This makes it a good point to check constraints, since in some of these
		/// cases, SliceIsCurrent may not get set false.
		/// </summary>
		protected override void OnValidating(System.ComponentModel.CancelEventArgs e)
		{
			base.OnValidating(e);
			DoValidation();
		}

		#endregion INotifyControlInCurrentSlice implementation

		/// <summary>
		/// If the view's root object is valid, then call the base method.  Otherwise do nothing.
		/// (See LT-8656 and LT-9119.)
		/// </summary>
		protected override void OnKeyPress(KeyPressEventArgs e)
		{
			if (m_obj.IsValidObject)
			{
				base.OnKeyPress(e);
			}
			else
			{
				e.Handled = true;
			}
		}

		public override void MakeRoot()
		{
			if (m_cache == null || DesignMode)
			{
				return;
			}
			// A crude way of making sure the property we want is loaded into the cache.
			m_obj = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(m_hvoObj);
			var type = (CellarPropertyType)m_cache.DomainDataByFlid.MetaDataCache.GetFieldType(m_flid);
			switch (type)
			{
				case CellarPropertyType.Unicode:
					m_vc = new UnicodeStringSliceVc(m_flid, m_ws, m_cache);
					break;
				case CellarPropertyType.String:
					// Even if we were given a writing system, we must not use it if not a multistring,
					// otherwise the VC crashes when it tries to read the property as multilingual.
					m_vc = new StringSliceVc(m_flid, m_cache, Publisher);
					((StringSliceVc)m_vc).ShowWsLabel = m_fShowWsLabel;
					break;
				default:
					m_vc = new StringSliceVc(m_flid, m_ws, m_cache, Publisher);
					((StringSliceVc)m_vc).ShowWsLabel = m_fShowWsLabel;
					break;
			}
			base.MakeRoot();
			// And maybe this too, at least by default?
			RootBox.DataAccess = m_cache.DomainDataByFlid;
			// arg3 is a meaningless initial fragment, since this VC only displays one thing.
			// arg4 could be used to supply a stylesheet.
			RootBox.SetRootObject(m_hvoObj, m_vc, 1, m_styleSheet);
		}

		private static bool s_fProcessingSelectionChanged;
		/// <summary>
		/// Try to keep the selection from including any of the characters in a writing system label.
		/// Also update the writing system label if needed.
		/// </summary>
		protected override void HandleSelectionChange(IVwRootBox prootb, IVwSelection vwselNew)
		{
			base.HandleSelectionChange(prootb, vwselNew);
			// 1) We don't want to recurse into here.
			// 2) If the selection is invalid we can't use it.
			if (s_fProcessingSelectionChanged || !vwselNew.IsValid)
			{
				return;
			}
			try
			{
				s_fProcessingSelectionChanged = true;
				// If the selection is entirely formattable ("IsSelectionInOneFormattableProp"), we don't need to do
				// the following selection truncation.
				var hlpr = SelectionHelper.Create(vwselNew, this);
				if (!EditingHelper.IsSelectionInOneFormattableProp())
				{
					var fRange = hlpr.IsRange;
					var fChangeRange = false;
					if (fRange)
					{
						var fAnchorEditable = vwselNew.IsEditable;
						hlpr.GetIch(SelLimitType.Anchor);
						var tagAnchor = hlpr.GetTextPropId(SelLimitType.Anchor);
						hlpr.GetIch(SelLimitType.End);
						var tagEnd = hlpr.GetTextPropId(SelLimitType.End);
						var fEndBeforeAnchor = vwselNew.EndBeforeAnchor;
						if (fEndBeforeAnchor)
						{
							if (fAnchorEditable && tagAnchor > 0 && tagEnd < 0)
							{
								hlpr.SetTextPropId(SelLimitType.End, tagAnchor);
								hlpr.SetIch(SelLimitType.End, 0);
								fChangeRange = true;
							}
						}
						else
						{
							if (!fAnchorEditable && tagAnchor < 0 && tagEnd > 0)
							{
								hlpr.SetTextPropId(SelLimitType.Anchor, tagEnd);
								hlpr.SetIch(SelLimitType.Anchor, 0);
								fChangeRange = true;
							}
						}
					}
					if (fChangeRange)
					{
						hlpr.SetSelection(true);
					}
				}
				if (!m_fShowWsLabel)
				{
					return;
				}
				// Might not be, especially when messing with the selection during Undoing the creation of a record.
				if (!Cache.ServiceLocator.IsValidObjectId(m_hvoObj))
				{
					return;
				}
				var tss = RootBox.DataAccess.get_StringProp(m_hvoObj, m_flid);
				var ttp = tss.get_Properties(0);
				var ws = ttp.GetIntPropValues((int)FwTextPropType.ktptWs, out _);
				if (ws != 0 && m_vc is StringSliceVc stringSliceVc && ws != stringSliceVc.MostRecentlyDisplayedWritingSystemHandle)
				{
					RootBox.Reconstruct();
					hlpr.SetSelection(true);
				}
			}
			finally
			{
				s_fProcessingSelectionChanged = false;
			}
		}
	}
}