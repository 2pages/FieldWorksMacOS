// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using LanguageExplorer.Controls.DetailControls;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;

namespace LanguageExplorer.Areas.Lexicon.Tools.Edit
{
	/// <summary />
	internal sealed class LexReferenceSequenceView : VectorReferenceView
	{
		/// <summary />
		private ICmObject m_displayParent;

		/// <summary />
		public LexReferenceSequenceView()
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();
		}

		/// <summary />
		protected override VectorReferenceVc CreateVectorReferenceVc()
		{
			var vc = new LexReferenceSequenceVc(m_cache, m_rootFlid, m_displayNameProperty, m_displayWs);
			if (m_displayParent != null)
			{
				vc.DisplayParent = m_displayParent;
			}
			return vc;
		}

		/// <summary />
		public ICmObject DisplayParent
		{
			set
			{
				m_displayParent = value;
				if (m_VectorReferenceVc != null)
				{
					((LexReferenceSequenceVc)m_VectorReferenceVc).DisplayParent = value;
				}
			}
		}

		/// <summary>
		/// This handles deleting the "owning" sense or entry from a calendar type lex
		/// reference by posting a message instead of simply removing the sense or entry from
		/// the reference vector.  This keeps things nice and tidy on the screen, and behaving
		/// like users would (or ought to) expect.  See LT-4114.
		/// </summary>
		protected override void Delete()
		{
			var sel = RootBox.Selection;
			if (!CheckForValidDelete(sel, out var cvsli, out var hvoObj))
			{
				return;
			}
			if (m_displayParent != null && hvoObj == m_displayParent.Hvo)
			{
				// We need to handle this the same way as the delete command in the slice menu.
				Publisher.Publish(new PublisherParameterObject("DataTreeDelete"));
			}
			else
			{
				DeleteObjectFromVector(sel, cvsli, hvoObj, AreaResources.ksUndoDeleteRef, AreaResources.ksRedoDeleteRef);
			}
		}

		#region Component Designer generated code
		/// <summary>
		/// The Superclass handles everything except our Name property.
		/// </summary>
		private void InitializeComponent()
		{
			this.Name = "LexReferenceSequenceView";
		}
		#endregion
	}
}