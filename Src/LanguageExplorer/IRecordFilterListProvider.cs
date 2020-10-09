// Copyright (c) 2018-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using LanguageExplorer.Filters;
using SIL.FieldWorks.Common.FwUtils;

namespace LanguageExplorer
{
	internal interface IRecordFilterListProvider : IFlexComponent
	{
		/// <summary>
		/// Reload the data items.
		/// </summary>
		void ReLoad();

		/// <summary>
		/// Get the list of filters.
		/// </summary>
		List<IRecordFilter> Filters { get; }

		/// <summary>
		/// Get a filter with the given name.
		/// </summary>
		IRecordFilter GetFilter(string filterName);

		/// <summary>
		/// May want to update / reload the list based on user selection.
		/// </summary>
		bool AdjustFilterSelection(IRecordFilter recordFilter);
	}
}
