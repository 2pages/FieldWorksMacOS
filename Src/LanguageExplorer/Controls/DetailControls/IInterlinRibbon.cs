// Copyright (c) 2008-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using SIL.LCModel.Application;
using SIL.LCModel.DomainServices;

namespace LanguageExplorer.Controls.DetailControls
{
	/// <summary>
	/// This interface defines the functionality that the logic needs from the ribbon.
	/// Test code implements mock ribbons.
	/// </summary>
	internal interface IInterlinRibbon
	{
		AnalysisOccurrence[] SelectedOccurrences { get; }
		void CacheRibbonItems(List<AnalysisOccurrence> wordForms);
		void MakeInitialSelection();
		void SelectFirstOccurence();
		int OccurenceListId { get; }
		int EndSelLimitIndex { get; set; }
		AnalysisOccurrence SelLimOccurrence { get; set; }
		ISilDataAccessManaged Decorator { get; }
	}
}