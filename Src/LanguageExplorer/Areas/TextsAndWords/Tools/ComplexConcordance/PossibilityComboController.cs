// Copyright (c) 2013-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Windows.Forms;
using LanguageExplorer.Controls;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;

namespace LanguageExplorer.Areas.TextsAndWords.Tools.ComplexConcordance
{
	internal class PossibilityComboController : POSPopupTreeManager
	{
		/// <summary />
		public PossibilityComboController(TreeCombo treeCombo, LcmCache cache, ICmPossibilityList list, int ws, bool useAbbr, FlexComponentParameters flexComponentParameters, Form parent) :
			base(treeCombo, cache, list, ws, useAbbr, flexComponentParameters, parent)
		{
			Sorted = true;
		}

		protected override TreeNode MakeMenuItems(PopupTree popupTree, int hvoTarget)
		{
			var tagName = UseAbbr ? CmPossibilityTags.kflidAbbreviation : CmPossibilityTags.kflidName;
			popupTree.Sorted = Sorted;
			TreeNode match = null;
			if (List != null)
			{
				match = AddNodes(popupTree.Nodes, List.Hvo, CmPossibilityListTags.kflidPossibilities, hvoTarget, tagName);
			}
			if (hvoTarget == 0)
			{
				match = AddAnyItem(popupTree);
			}
			return match;
		}

		public bool Sorted { get; set; }
	}
}