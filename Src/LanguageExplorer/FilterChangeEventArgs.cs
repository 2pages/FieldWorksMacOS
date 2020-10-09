// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using LanguageExplorer.Filters;

namespace LanguageExplorer
{
	/// <summary>
	/// Event arguments for FilterChangeHandler event.
	/// Arguably, we could have separate events for adding and removing, but that would make it
	/// more difficult to avoid refreshing the list twice when switching from one filter to
	/// another. Arguably, both add and remove could be arrays. But so far there has been no
	/// need for this, and if we do, we can easily keep the current constructor but change
	/// the accessors, which are probably rather less used.
	/// </summary>
	public class FilterChangeEventArgs
	{
		/// <summary />
		public FilterChangeEventArgs(IRecordFilter added, IRecordFilter removed)
		{
			Added = added;
			Removed = removed;
		}

		/// <summary>
		/// Gets the added IRecordFilter.
		/// </summary>
		public IRecordFilter Added { get; }

		/// <summary>
		/// Gets the removed IRecordFilter.
		/// </summary>
		public IRecordFilter Removed { get; }
	}

	/// <summary />
	public delegate void FilterChangeHandler(object sender, FilterChangeEventArgs e);
}