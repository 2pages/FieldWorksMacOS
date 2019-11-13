// Copyright (c) 2008-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using SIL.LCModel.DomainServices;

namespace LanguageExplorer.Areas.TextsAndWords.Discourse
{
	internal class OccurrenceSorter : IComparer<AnalysisOccurrence>
	{
		#region IComparer<AnalysisOccurrence> Members

		/// <summary>
		/// Compares two AnalysisOccurrences to determine which comes first in a text.
		/// </summary>
		/// <returns>Positive integer if y occurs prior to x in text.
		/// Negative integer if x occurs prior to y in the text.</returns>
		public int Compare(AnalysisOccurrence x, AnalysisOccurrence y)
		{
			return x.Paragraph.Hvo != y.Paragraph.Hvo
				? x.Paragraph.IndexInOwner - y.Paragraph.IndexInOwner
				: x.Segment.Hvo == y.Segment.Hvo
					? x.Index - y.Index
					: x.Segment.IndexInOwner - y.Segment.IndexInOwner;
		}

		#endregion
	}
}