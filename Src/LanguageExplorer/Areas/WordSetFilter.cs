// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Linq;
using System.Xml.Linq;
using LanguageExplorer.Filters;
using SIL.LCModel;
using SIL.Xml;

namespace LanguageExplorer.Areas
{
	/// <summary>
	/// A filter for selecting a group of wordform spaced on a "IWfiWordSet"
	/// </summary>
	internal sealed class WordSetFilter : RecordFilter
	{
		/// <summary />
		private int[] m_hvos;

		/// <summary />
		public WordSetFilter(IWfiWordSet wordSet)
		{
			Id = wordSet.Hvo.ToString();
			Name = wordSet.Name.AnalysisDefaultWritingSystem.Text;
			LoadCases(wordSet);
		}

		private void LoadCases(IWfiWordSet wordSet)
		{
			m_hvos = wordSet.CasesRC.ToHvoArray();
		}

		/// <summary>
		/// Sync the word references to the state of the word list in the database.
		/// This is what we need to do when restoring our Filter from xml to make sure
		/// the ids are valid.
		/// </summary>
		internal void ReloadWordSet(LcmCache cache)
		{
			LoadCases((IWfiWordSet)cache.ServiceLocator.GetObject(int.Parse(Id)));
		}

		/// <summary>
		/// Default constructor for IPersistAsXml
		/// </summary>
		public WordSetFilter()
		{
		}

		/// <summary>
		/// Persists as XML.
		/// </summary>
		public override void PersistAsXml(XElement element)
		{
			base.PersistAsXml(element);
			XmlUtils.SetAttribute(element, "id", Id);
			XmlUtils.SetAttribute(element, "wordlist", XmlUtils.MakeStringFromList(m_hvos.ToList()));
		}

		/// <summary>
		/// Inits the XML.
		/// </summary>
		public override void InitXml(XElement element)
		{
			base.InitXml(element);
			Id = XmlUtils.GetMandatoryAttributeValue(element, "id");
			m_hvos = XmlUtils.GetMandatoryIntegerListAttributeValue(element, "wordlist");
		}

		/// <summary>
		/// Test to see if this filter matches the other filter.
		/// </summary>
		public override bool SameFilter(IRecordFilter other)
		{
			return other is WordSetFilter && other.Id == Id && other.Name == Name;
		}

		/// <summary>
		/// decide whether this object should be included
		/// </summary>
		public override bool Accept(IManyOnePathSortItem item)
		{
			var hvo = item.KeyObject;

			for (var i = m_hvos.Length - 1; i >= 0; i--)
			{
				if (m_hvos[i] == hvo)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// This is always set by the user.
		/// </summary>
		public override bool IsUserVisible => true;
	}
}