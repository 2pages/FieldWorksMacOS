// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using SIL.FieldWorks.FDO;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml.XPath;
using SIL.Xml;

namespace SIL.FieldWorks.Common.Framework.DetailControls
{
	/// <summary>
	/// a class to determine, at display time, whether individual slices should be displayed or hidden.
	/// </summary>
	public class SliceFilter
	{
		protected XDocument m_filterList;

		/// <summary>
		/// create a filter which does not have an external setup notes to filter (but which will still ask FDO)
		/// </summary>
		public SliceFilter()
		{
			m_filterList= null;
		}

		/// <summary>
		/// create a filter which will consult both FDO and an external XML document when making filtering decisions
		/// </summary>
		/// <param name="filterList">XML document (see distfiles/lexed/basicFilter.xml for an example)</param>
		public SliceFilter(XDocument filterList)
		{
			m_filterList= filterList;
		}

		/// <summary>
		/// tell whether to include the slice
		/// </summary>
		/// <returns>true if this slice should be included</returns>
		virtual public bool IncludeSlice(XElement configurationNode, ICmObject obj, int flid, HashSet<Tuple<int, int>> propsToMonitor)
		{
			if (m_filterList!= null)
			{
				string id = XmlUtils.GetOptionalAttributeValue(configurationNode, "id");
				if (id != null)
				{
					var instruction = m_filterList.XPathSelectElement("SliceFilter/node[@id='" + id + "']");

					if (instruction != null)
						return false;
				}
			}

			//skip slices representing fields which are not relevant under the current circumstances.
			// If necessary note that the list of slices
			var result = obj.IsFieldRelevant(flid, propsToMonitor);

			return result;
		}
	}
}
