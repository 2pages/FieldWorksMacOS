// Copyright (c) 2010-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Runtime.InteropServices;
using SIL.FieldWorks.Common.ViewsInterfaces;

namespace SIL.FieldWorks.Common.RootSites
{
	/// <summary>
	/// Helps saving and restoring a selection around code that could possibly destroy the
	/// selection (or even the location where the selection was located). This is mostly
	/// used around a RefreshDisplay which will reconstruct the whole view from scratch and
	/// will destroy the selection and could, possibly, destroy the text where the selection
	/// was located.
	/// </summary>
	public class SelectionRestorer : IDisposable
	{
		/// <summary>The selection that will be restored</summary>
		protected readonly SelectionHelper m_savedSelection;
		/// <summary>The selection that was at the top of the visible area and that will be
		/// scrolled to be the top of the new visible area</summary>
		protected readonly SelectionHelper m_topOfViewSelection;
		/// <summary>The rootsite that will get the selection when restored</summary>
		protected readonly SimpleRootSite m_rootSite;

		/// <summary />
		public SelectionRestorer(SimpleRootSite rootSite)
		{
			// we can't use EditingHelper.CurrentSelection here because the scroll position
			// of the selection may have changed.
			m_savedSelection = SelectionHelper.Create(rootSite);
			m_rootSite = rootSite;
			rootSite.GetCoordRects(out var rcSrc, out var rcDst);
			try
			{
				m_topOfViewSelection = SelectionHelper.Create(rootSite.RootBox.MakeSelAt(5, 5, rcSrc, rcDst, false), rootSite);
			}
			catch (COMException)
			{
				// Just ignore any errors
			}
		}

		#region Disposable stuff
		/// <summary />
		~SelectionRestorer()
		{
			Dispose(false);
		}

		/// <summary />
		protected bool IsDisposed { get; set; }

		/// <summary />
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting
		/// unmanaged resources. In this case, attempt to restore the selection we originally
		/// saved.
		/// </summary>
		protected virtual void Dispose(bool disposing)
		{
			System.Diagnostics.Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			if (!disposing || IsDisposed || m_savedSelection == null || m_rootSite.RootBox.Height <= 0)
			{
				return;
			}

			if (m_rootSite.ReadOnlyView)
			{
				// if we are a read-only view, then we can't make a writable selection
				RestoreSelectionWhenReadOnly();
				return;
			}

			var newSel = RestoreSelection();
			if (newSel == null)
			{
				try
				{
					// Any selection is better than no selection...
					m_rootSite.RootBox.MakeSimpleSel(true, true, false, true);
				}
				catch (COMException)
				{
					// Just ignore any errors - don't get an selection but who cares.
				}
			}


			IsDisposed = true;
		}

		/// <summary>
		/// This is the normal RestoreSelection. For some reason by default it is not used when read-only.
		/// Returns the selection it successfully restored, or null if it could not restore one.
		/// </summary>
		protected virtual IVwSelection RestoreSelection()
		{
			var makeVisible = false;
			if (m_topOfViewSelection != null)
			{
				var selTop = m_topOfViewSelection.SetSelection(m_rootSite, false, false);
				if (selTop != null && selTop.IsValid)
				{
					m_topOfViewSelection.RestoreScrollPos();
				}
				else
				{
					makeVisible = true;
				}
			}

			return m_savedSelection.MakeBest(makeVisible);
		}

		private void RestoreSelectionWhenReadOnly()
		{
			try
			{
				m_rootSite.RootBox.MakeSimpleSel(true, false, false, true);
			}
			catch (COMException)
			{
				// Just ignore any errors - don't get an selection but who cares.
			}
		}

		#endregion
	}
}