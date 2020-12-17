// Copyright (c) 2006-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.Xml;

namespace LanguageExplorer.Filters
{
	/// <summary>
	/// A special filter, where items are LexEntries, and matches are ones where an MSA is an MoStemMsa that
	/// has the correct POS.
	/// </summary>
	internal sealed class EntryPosFilter : ListChoiceFilter
	{
		private const int kflidMsas = LexEntryTags.kflidMorphoSyntaxAnalyses;
		private const int kflidEntrySenses = LexEntryTags.kflidSenses;

		/// <summary>
		/// For use with IPersistAsXml
		/// </summary>
		internal EntryPosFilter(XElement element)
			: base(element)
		{
		}

		internal EntryPosFilter(LcmCache cache, ListMatchOptions mode, int[] targets)
			: base(cache, mode, targets)
		{
		}

		protected override string BeSpec => "EntryPosFilter";

		internal override bool CompatibleFilter(XElement colSpec)
		{
			if (!base.CompatibleFilter(colSpec))
			{
				return false;
			}
			return XmlUtils.GetMandatoryAttributeValue(colSpec, "class").Split('.').Last().Trim() == "EntryPosFilter";
		}

		/// <summary>
		/// Get the items to be compared against the filter.
		/// </summary>
		protected override int[] GetItems(IManyOnePathSortItem item)
		{
			var sda = m_cache.DomainDataByFlid;
			var results = new List<int>();
			if (item.PathLength > 0 && item.PathFlid(0) == kflidMsas)
			{
				// sorted by MSA, match just the one MSA.
				// I don't think this path can occur with the current XML spec where this is used.
				var hvoMsa = item.PathLength > 1 ? item.PathObject(1) : item.KeyObject;
				GetItemsForMsaType(sda, ref results, hvoMsa);
			}
			else if (item.PathLength >= 1 && item.PathFlid(0) == kflidEntrySenses)
			{
				// sorted in a way that shows one sense per row, test that sense's MSA.
				var hvoSense = item.PathLength > 1 ? item.PathObject(1) : item.KeyObject;
				GetItemsForMsaType(sda, ref results, sda.get_ObjectProp(hvoSense, LexSenseTags.kflidMorphoSyntaxAnalysis));
			}
			else
			{
				var hvoEntry = item.RootObjectHvo;
				var cmsa = sda.get_VecSize(hvoEntry, kflidMsas);
				for (var imsa = 0; imsa < cmsa; imsa++)
				{
					var hvoMsa = sda.get_VecItem(hvoEntry, kflidMsas, imsa);
					GetItemsForMsaType(sda, ref results, hvoMsa);
				}
			}
			return results.ToArray();
		}

		private void GetItemsForMsaType(ISilDataAccess sda, ref List<int> results, int hvoMsa)
		{
			if (hvoMsa == 0)
			{
				return;
			}
			var kclsid = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(hvoMsa).ClassID;
			switch (kclsid)
			{
				case MoStemMsaTags.kClassId:
					AddHvoPOStoResults(sda, results, hvoMsa, MoStemMsaTags.kflidPartOfSpeech);
					break;
				case MoInflAffMsaTags.kClassId:
					AddHvoPOStoResults(sda, results, hvoMsa, MoInflAffMsaTags.kflidPartOfSpeech);
					break;
				case MoDerivAffMsaTags.kClassId:
					AddHvoPOStoResults(sda, results, hvoMsa, MoDerivAffMsaTags.kflidFromPartOfSpeech);
					AddHvoPOStoResults(sda, results, hvoMsa, MoDerivAffMsaTags.kflidToPartOfSpeech);
					break;
				case MoUnclassifiedAffixMsaTags.kClassId:
					AddHvoPOStoResults(sda, results, hvoMsa, MoUnclassifiedAffixMsaTags.kflidPartOfSpeech);
					break;
			}
		}

		private static void AddHvoPOStoResults(ISilDataAccess sda, List<int> results, int hvoMsa, int flidPos)
		{
			var hvoPOS = sda.get_ObjectProp(hvoMsa, flidPos);
			if (hvoPOS != 0)
			{
				results.Add(hvoPOS);
			}
		}
	}
}