﻿// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Windows.Forms;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.Widgets;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.LexText.Controls;

namespace SIL.FieldWorks.IText
{
	internal class PossibilityComboController : POSPopupTreeManager
	{
		/// <summary>
		/// Constructor.
		/// </summary>
		public PossibilityComboController(TreeCombo treeCombo, FdoCache cache, ICmPossibilityList list, int ws, bool useAbbr, IPropertyTable propertyTable, IPublisher publisher, Form parent) :
			base(treeCombo, cache, list, ws, useAbbr, propertyTable, publisher, parent)
		{
			Sorted = true;
		}

		protected override TreeNode MakeMenuItems(PopupTree popupTree, int hvoTarget)
		{
			int tagName = UseAbbr ?
				CmPossibilityTags.kflidAbbreviation :
				CmPossibilityTags.kflidName;
			popupTree.Sorted = Sorted;
			TreeNode match = null;
			if (List != null)
				match = AddNodes(popupTree.Nodes, List.Hvo,
									CmPossibilityListTags.kflidPossibilities, hvoTarget, tagName);
			var empty = AddAnyItem(popupTree);
			if (hvoTarget == 0)
				match = empty;
			return match;
		}

		public bool Sorted { get; set; }
	}
}
