// Copyright (c) 2005-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

namespace LanguageExplorer.Controls.DetailControls
{
	internal class ConcSlice : ViewSlice
	{
		private ConcView m_cv;

		public ConcSlice(ConcView cv)
			: base(cv)
		{
			m_cv = cv;
		}

		public IConcSliceInfo SliceInfo => m_cv.SliceInfo;

		/// <summary>
		/// Expand this node, which is at position iSlice in its parent.
		/// </summary>
		public override void Expand(int iSlice)
		{
			((MultiLevelConcordanceDataTree)ContainingDataTree).InsertDummies(this, iSlice + 1, SliceInfo.Count);
			Expansion = TreeItemState.ktisExpanded;
			PerformLayout();
			Invalidate(true); // invalidates all children.
		}
	}
}