// Copyright (c) 2009-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using SIL.LCModel;
using SIL.Xml;

namespace LanguageExplorer.Controls.XMLViews
{
	/// <summary>
	/// this class depends upon column spec for figuring path information
	/// from the list item, since the list item (and hence flid and subflid)
	/// may change.
	/// </summary>
	public class ColumnSpecFilter : ListChoiceFilter
	{
		XElement m_colSpec;
		XmlBrowseViewVc m_vc;

		/// <summary>
		/// Required for persistence. Do not remove!
		/// </summary>
		public ColumnSpecFilter()
		{ }

		/// <summary>
		/// Filter used to compare against the hvos in a cell in rows described by IManyOnePathSortItem items.
		/// This depends upon xml spec to find the hvos, not a preconceived list item class, which is helpful for BulkEditEntries where
		/// our list item class can vary.
		/// </summary>
		public ColumnSpecFilter(LcmCache cache, ListMatchOptions mode, int[] targets, XElement colSpec)
			: base(cache, mode, targets)
		{
			m_colSpec = colSpec;
		}


		private XmlBrowseViewVc Vc => m_vc ?? (m_vc = new XmlBrowseViewVc(m_cache, m_sda) { SuppressPictures = true });

		/// <summary>
		/// eg. persist the column specification
		/// </summary>
		public override void PersistAsXml(XElement element)
		{
			base.PersistAsXml(element);
			if (m_colSpec != null)
			{
				element.Add(m_colSpec.Clone());
			}
		}

		/// <summary />
		public override void InitXml(XElement element)
		{
			base.InitXml(element);
			// Review: How do we validate this columnSpec is still valid?
			var colSpec = element.XPathSelectElement("./column");
			if (colSpec != null)
			{
				m_colSpec = colSpec.Clone();
			}
		}

		/// <summary>
		/// return all the hvos for this column in the row identified by the sort item.
		/// </summary>
		protected override int[] GetItems(IManyOnePathSortItem rowItem)
		{
			var hvoRootObj = rowItem.RootObjectHvo;
			var collector = new ItemsCollectorEnv(null, m_sda, hvoRootObj);
			if (Vc != null && m_colSpec != null)
			{
				Vc.DisplayCell(rowItem, m_colSpec, hvoRootObj, collector);
			}
			return collector.HvosCollectedInCell.ToArray();
		}

		/// <summary />
		protected override string BeSpec
		{
			get
			{
				// we can get this from the columnSpec
				var beSpec = XmlUtils.GetOptionalAttributeValue(m_colSpec, "bulkEdit", string.Empty);
				if (string.IsNullOrWhiteSpace(beSpec))
				{
					beSpec = XmlUtils.GetOptionalAttributeValue(m_colSpec, "chooserFilter", string.Empty);
				}
				if (string.IsNullOrWhiteSpace(beSpec))
				{
					// Enhance: figure it out from the the column spec parts.
				}
				return beSpec;
			}
		}

		/// <summary />
		public override bool CompatibleFilter(XElement colSpec)
		{
			if (!base.CompatibleFilter(colSpec))
			{
				return false;
			}
			// see if we can compare layout attributes
			var possibleAttributes = new Queue<string>(new[] { "layout", "subfield", "field" });
			return XmlViewsUtils.TryMatchExistingAttributes(colSpec, m_colSpec, ref possibleAttributes);
		}
	}
}