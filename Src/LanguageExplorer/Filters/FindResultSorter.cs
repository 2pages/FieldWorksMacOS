// Copyright (c) 2015-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections;
using SIL.LCModel.Core.KernelInterfaces;

namespace LanguageExplorer.Filters
{
	/// <summary>
	/// Extends the GenRecordSorter to prioritize the sorting of more exact matches
	/// </summary>
	public class FindResultSorter : GenRecordSorter
	{
		public FindResultSorter(ITsString searchString, RecordSorter sorter)
		{
			Comparer = searchString.Text != null ? new ExactMatchFirstComparer(searchString.Text, sorter.getComparer()) : sorter.getComparer();
		}

		private sealed class ExactMatchFirstComparer : IComparer
		{
			private string SearchString { get; }
			private IComparer Comparer { get; }

			public ExactMatchFirstComparer(string text, IComparer comparer)
			{
				Comparer = comparer;
				SearchString = text;
			}

			public int Compare(object x, object y)
			{
				// Run the core comparison first
				var comparerResult = Comparer.Compare(x, y);
				// if items are equal no further tests are needed
				if (comparerResult == 0)
				{
					return 0;
				}
				// Get the relevant strings out of the objects we are comparing using the GetValue on the StringFinderComparer.
				// This makes use of the StringFinderCompare's built in cache.
				var stringComparer = (StringFinderCompare)Comparer;
				var xStringArray = stringComparer.GetValue(x, stringComparer.SortedFromEnd);
				var yStringArray = stringComparer.GetValue(y, stringComparer.SortedFromEnd);
				var xString = xStringArray.Length == 0 ? string.Empty : xStringArray[0];
				var yString = yStringArray.Length == 0 ? string.Empty : yStringArray[0];
				// Avoid string comparisons if the result is already what we would possibly return
				if (comparerResult >= 0)
				{
					// Exact matches of the search string should sort to the top by virtue of being the shortest
					// starts with matches should sort just below exact matches
					if (xString.StartsWith(SearchString, StringComparison.InvariantCultureIgnoreCase)
						&& !yString.StartsWith(SearchString, StringComparison.InvariantCultureIgnoreCase))
					{
						return -1;
					}
				}
				else
				{
					// Exact matches of the search string should sort to the top by virtue of being the shortest
					// starts with matches should sort just below exact matches
					if (yString.StartsWith(SearchString, StringComparison.InvariantCultureIgnoreCase)
						&& !xString.StartsWith(SearchString, StringComparison.InvariantCultureIgnoreCase))
					{
						return 1;
					}
				}
				// Everything else should sort by the core comparer result
				return comparerResult;
			}
		}
	}
}