// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;

namespace LanguageExplorer.Filters
{
	/// <summary>
	/// A very general record sorter class, based on an arbitrary implementation of IComparer
	/// that can compare two LCM objects.
	/// </summary>
	internal class GenRecordSorter : RecordSorter
	{
		protected IComparer _comparer;

		/// <summary />
		public GenRecordSorter(IComparer comp) : this()
		{
			_comparer = comp;
		}

		/// <summary>
		/// Default constructor for IPersistAsXml
		/// </summary>
		public GenRecordSorter()
		{
		}

		/// <summary>
		/// See whether the comparer can preload. Currently we only know about one kind that can.
		/// </summary>
		public override void Preload(ICmObject rootObj)
		{
			base.Preload(rootObj);
			if (Comparer is StringFinderCompare stringFinderCompare)
			{
				stringFinderCompare.Preload(rootObj);
			}
		}

		/// <summary>
		/// Override to pass it on to the comparer, if relevant.
		/// </summary>
		public override ISilDataAccess DataAccess
		{
			set
			{
				base.DataAccess = value;
				if (Comparer is StringFinderCompare stringFinderCompare)
				{
					stringFinderCompare.DataAccess = value;
				}
			}
		}

		/// <summary>
		/// Return true if the other sorter is 'compatible' with this, in the sense that
		/// either they produce the same sort sequence, or one derived from it (e.g., by reversing).
		/// </summary>
		public override bool CompatibleSorter(IRecordSorter other)
		{
			if (!(other is GenRecordSorter grsOther))
			{
				return false;
			}
			if (CompatibleComparers(Comparer, grsOther.Comparer))
			{
				return true;
			}
			// Currently the only other kind of compatibility we know how to detect
			// is StringFinderCompares that do more-or-less the same thing.
			var sfcOther = grsOther.Comparer as StringFinderCompare;
			if (!(Comparer is StringFinderCompare sfcThis) || sfcOther == null)
			{
				return false;
			}
			if (!sfcThis.Finder.SameFinder(sfcOther.Finder))
			{
				return false;
			}
			// We deliberately don't care if one has a ReverseCompare and the other
			// doesn't. That's handled by a different icon.
			var subCompOther = UnpackReverseCompare(sfcOther);
			var subCompThis = UnpackReverseCompare(sfcThis);
			return CompatibleComparers(subCompThis, subCompOther);
		}

		private static IComparer UnpackReverseCompare(StringFinderCompare sfc)
		{
			var subComp = sfc.SubComparer;
			return subComp is ReverseComparer reverseComparer ? reverseComparer.SubComp : subComp;
		}

		/// <summary>
		/// Return true if the two comparers will give the same result. Ideally this would
		/// be an interface method on ICompare, but that interface is defined by .NET so we
		/// can't enhance it. This knows about a few interesting cases.
		/// </summary>
		private static bool CompatibleComparers(IComparer first, IComparer second)
		{
			// identity
			if (ReferenceEquals(first, second))
			{
				return true;
			}
			// IcuComparers on same Ws?
			if (first is IcuComparer firstIcu && second is IcuComparer secondIcu && firstIcu.WsCode == secondIcu.WsCode)
			{
				return true;
			}
			// WritingSystemComparers on same Ws?
			if (first is WritingSystemComparer firstWs && second is WritingSystemComparer secondWs && firstWs.WsId == secondWs.WsId)
			{
				return true;
			}
			// Both IntStringComparers?
			if (first is IntStringComparer && second is IntStringComparer)
			{
				return true;
			}
			// LcmComparers on the same property?
			return first is LcmCompare firstLcm && second is LcmCompare secondLcm && firstLcm.PropertyName == secondLcm.PropertyName;
		}

		/// <summary>
		/// Gets the comparer.
		/// </summary>
		public override IComparer Comparer => _comparer;

		/// <summary>
		/// Add to the specified XML node information required to create a new
		/// record sorter equivalent to yourself.
		/// The default is not to add any information.
		/// It may be assumed that the node already contains the assembly and class
		/// information required to create an instance of the sorter in a default state.
		/// An equivalent XmlNode will be passed to the InitXml method of the
		/// re-created sorter.
		/// This default implementation does nothing.
		/// </summary>
		public override void PersistAsXml(XElement element)
		{
			base.PersistAsXml(element); // does nothing, but in case needed later...
			if (!(Comparer is IPersistAsXml persistComparer))
			{
				throw new Exception($"cannot persist GenRecSorter with comparer class {Comparer.GetType().AssemblyQualifiedName}");
			}
			LanguageExplorerServices.PersistObject(persistComparer, element, "comparer");
		}

		/// <summary>
		/// Initialize an instance into the state indicated by the node, which was
		/// created by a call to PersistAsXml.
		/// </summary>
		public override void InitXml(XElement element)
		{
			base.InitXml(element);
			var compNode = element.Elements().First();
			if (compNode.Name != "comparer")
			{
				throw new Exception("persist info for GenRecordSorter must have comparer child element");
			}
			_comparer = DynamicLoader.RestoreObject<IComparer>(compNode);
			if (_comparer == null)
			{
				throw new Exception("restoring sorter failed...comparer does not implement IComparer");
			}
		}

		/// <summary>
		/// Set an LcmCache for anything that needs to know.
		/// </summary>
		public override LcmCache Cache
		{
			set
			{
				if (Comparer is IStoresLcmCache storesLcmCache)
				{
					storesLcmCache.Cache = value;
				}
			}
		}

		/// <summary>
		/// Add to collector the ManyOnePathSortItems which this sorter derives from
		/// the specified object. This default method makes a single mopsi not involving any
		/// path.
		/// </summary>
		public override void CollectItems(int hvo, List<IManyOnePathSortItem> collector)
		{
			if (Comparer is StringFinderCompare stringFinderCompare)
			{
				stringFinderCompare.CollectItems(hvo, collector);
			}
			else
			{
				base.CollectItems(hvo, collector);
			}
		}

		/// <summary>
		/// Sorts the specified records.
		/// </summary>
		public override void Sort(List<IManyOnePathSortItem> records)
		{
#if DEBUG
			var dt1 = DateTime.Now;
			var tc1 = Environment.TickCount;
#endif
			if (Comparer is StringFinderCompare stringFinderCompare)
			{
				stringFinderCompare.Init();
				stringFinderCompare.ComparisonNoter = this;
				m_comparisonsDone = 0;
				m_percentDone = 0;
				// Make sure at least 1 so we don't divide by zero.
				m_comparisonsEstimated = Math.Max(records.Count * (int)Math.Ceiling(Math.Log(records.Count, 2.0)), 1);
			}
			records.Sort(Comparer);
			if (Comparer is StringFinderCompare finderCompare)
			{
				finderCompare.Cleanup();
			}
#if DEBUG
			// only do this if the timing switch is info or verbose
			if (RuntimeSwitches.RecordTimingSwitch.TraceInfo)
			{
				var tc2 = Environment.TickCount;
				var ts1 = DateTime.Now - dt1;
				var s = $"GenRecordSorter:  Sorting {records.Count} records took {tc2 - tc1} ticks, or {ts1.Minutes}:{ts1.Seconds}.{ts1.Milliseconds:d3} min:sec.";
				Debug.WriteLine(s, RuntimeSwitches.RecordTimingSwitch.DisplayName);
			}
#endif
		}
		/// <summary>
		/// Required implementation.
		/// </summary>
		public override void MergeInto(List<IManyOnePathSortItem> records, List<IManyOnePathSortItem> newRecords)
		{
			if (Comparer is StringFinderCompare stringFinderCompare)
			{
				stringFinderCompare.Init();
			}
			MergeInto(records, newRecords, Comparer);
			if (Comparer is StringFinderCompare finderCompare)
			{
				finderCompare.Cleanup();
			}
		}

		/// <summary>
		/// Check whether this GenRecordSorter is equal to another object.
		/// </summary>
		public override bool Equals(object obj)
		{
			if (obj == null)
			{
				return false;
			}
			// TODO-Linux: System.Boolean System.Type::op_Inequality(System.Type,System.Type)
			// is marked with [MonoTODO] and might not work as expected in 4.0.
			if (GetType() != obj.GetType())
			{
				return false;
			}
			var that = (GenRecordSorter)obj;
			if (Comparer == null)
			{
				if (that.Comparer != null)
				{
					return false;
				}
			}
			else
			{
				if (that.Comparer == null)
				{
					return false;
				}
				// TODO-Linux: System.Boolean System.Type::op_Inequality(System.Type,System.Type)
				// is marked with [MonoTODO] and might not work as expected in 4.0.
				return Comparer.GetType() == that.Comparer.GetType() && Comparer.Equals(that.Comparer);
			}
			return true;
		}

		/// <summary />
		public override int GetHashCode()
		{
			var hash = GetType().GetHashCode();
			if (Comparer != null)
			{
				hash *= Comparer.GetHashCode();
			}
			return hash;
		}
	}
}