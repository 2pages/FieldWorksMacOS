// Copyright (c) 2014-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace LanguageExplorer.Controls.XMLViews
{
	/// <summary>
	/// The event args for the SearchCompleted async event.
	/// </summary>
	public class SearchCompletedEventArgs : AsyncCompletedEventArgs
	{
		private readonly List<SearchField> m_fields;
		private readonly List<int> m_results;

		/// <summary />
		public SearchCompletedEventArgs(IEnumerable<SearchField> fields, IEnumerable<int> results)
			: base(null, false, null)
		{
			m_fields = fields.ToList();
			m_results = results.ToList();
		}

		/// <summary>
		/// Gets the fields.
		/// </summary>
		public IEnumerable<SearchField> Fields => m_fields;

		/// <summary>
		/// Gets the results.
		/// </summary>
		public IEnumerable<int> Results => m_results;
	}
}
