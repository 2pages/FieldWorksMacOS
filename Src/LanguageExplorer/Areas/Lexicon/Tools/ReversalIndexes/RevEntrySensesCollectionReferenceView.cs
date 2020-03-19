// Copyright (c) 2003-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Diagnostics;
using LanguageExplorer.Controls.DetailControls;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Infrastructure;

namespace LanguageExplorer.Areas.Lexicon.Tools.ReversalIndexes
{
	/// <summary>
	/// Main class for displaying the VectorReferenceSlice.
	/// </summary>
	internal sealed class RevEntrySensesCollectionReferenceView : VectorReferenceView
	{
		#region Constants and data members

		private int m_selectedSenseHvo;
		private bool m_handlingSelectionChanged;
		private System.ComponentModel.IContainer components = null;

		#endregion // Constants and data members

		#region Construction, initialization, and disposal

		/// <summary />
		public RevEntrySensesCollectionReferenceView()
		{
			InitializeComponent();
		}

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

			base.Dispose(disposing);

			if (disposing)
			{
				components?.Dispose();
			}
		}

		#endregion // Construction, initialization, and disposal

		#region other overrides and related methods

		/// <summary />
		protected override void Delete()
		{
			RemoveReversalEntryFromSense();
			base.Delete();
		}

		/// <summary />
		protected override void HandleSelectionChange(IVwRootBox rootb, IVwSelection vwselNew)
		{
			if (m_handlingSelectionChanged)
			{
				return;
			}

			m_handlingSelectionChanged = true;
			try
			{
				m_selectedSenseHvo = 0;
				if (vwselNew == null)
				{
					return;
				}
				base.HandleSelectionChange(rootb, vwselNew);

				// Get the Id of the selected snes, and store it.
				var cvsli = vwselNew.CLevels(false);
				// CLevels includes the string property itself, but AllTextSelInfo doesn't need it.
				cvsli--;
				if (cvsli == 0)
				{
					// No objects in selection: don't allow a selection.
					RootBox.DestroySelection();
					// Enhance: invoke launcher's selection dialog.
					return;
				}
				vwselNew.TextSelInfo(false, out _, out _, out _, out var hvoObj, out _, out _);
				vwselNew.TextSelInfo(true, out _, out _, out _, out var hvoObjEnd, out _, out _);
				if (hvoObj != hvoObjEnd)
				{
					return;
				}
				m_selectedSenseHvo = hvoObj;
			}
			finally
			{
				m_handlingSelectionChanged = false;
			}
		}

		private void RemoveReversalEntryFromSense()
		{
			if (m_selectedSenseHvo == 0)
			{
				return;     // must be selecting multiple objects!  (See LT-5724.)
			}
			var h1 = RootBox.Height;
			var sense = (ILexSense)m_cache.ServiceLocator.GetObject(m_selectedSenseHvo);
			using (var helper = new UndoableUnitOfWorkHelper(m_cache.ActionHandlerAccessor, LanguageExplorerResources.ksUndoDeleteRevFromSense, LanguageExplorerResources.ksRedoDeleteRevFromSense))
			{
				((IReversalIndexEntry)m_rootObj).SensesRS.Remove(sense);
				helper.RollBack = false;
			}
			CheckViewSizeChanged(h1, RootBox.Height);
		}

		#endregion other overrides and related methods

		#region Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			//
			// RevEntrySensesCollectionReferenceView
			//
			this.Name = "RevEntrySensesCollectionReferenceView";
			this.Size = new System.Drawing.Size(232, 40);

		}
		#endregion
	}
}