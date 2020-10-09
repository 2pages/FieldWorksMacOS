// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;

namespace LanguageExplorer.Filters
{
	/// <summary>
	/// Abstract RecordSorter base class.
	/// </summary>
	public abstract class RecordSorter : IPersistAsXml, IStoresLcmCache, IReportsSortProgress, INoteComparision
	{
		internal int m_comparisonsDone;
		internal int m_comparisonsEstimated;
		internal int m_percentDone;

		#region IPersistAsXml implementation

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
		public virtual void PersistAsXml(XElement element)
		{
		}

		/// <summary>
		/// Initialize an instance into the state indicated by the node, which was
		/// created by a call to PersistAsXml.
		/// </summary>
		public virtual void InitXml(XElement element)
		{
		}
		#endregion IPersistAsXml

		#region IStoresLcmCache implementation

		/// <summary>
		/// Set an LcmCache for anything that needs to know.
		/// </summary>
		public virtual LcmCache Cache
		{
			set
			{
				// do nothing by default.
			}
		}
		#endregion IStoresLcmCache

		#region IReportsSortProgress implementation

		public Action<int> SetPercentDone
		{
			get; set;
		}
		#endregion

		#region INoteComparision implementation.

		public void ComparisonOccurred()
		{
			if (SetPercentDone == null)
			{
				return;
			}
			m_comparisonsDone++;
			var newPercentDone = m_comparisonsDone * 100 / m_comparisonsEstimated;
			if (newPercentDone == m_percentDone)
			{
				return;
			}
			m_percentDone = newPercentDone;
			SetPercentDone(Math.Min(m_percentDone, 100)); // just in case we do more than the estimated number.
		}
		#endregion

		/// <summary>
		/// Set the data access to use in interpreting properties of objects.
		/// Call this AFTER setting Cache, otherwise, it may be overridden.
		/// </summary>
		public virtual ISilDataAccess DataAccess
		{
			set { }// do nothing by default.
		}

		/// <summary>
		/// Method to retrieve the IComparer used by this sorter
		/// </summary>
		protected internal abstract IComparer Comparer { get; }

		/// <summary>
		/// Sorts the specified records.
		/// </summary>
		public abstract void Sort(List<IManyOnePathSortItem> records);

		/// <summary>
		/// Merges the into.
		/// </summary>
		public abstract void MergeInto(List<IManyOnePathSortItem> records, List<IManyOnePathSortItem> newRecords);

		/// <summary>
		/// Merge the new records into the original list using the specified comparer
		/// </summary>
		protected void MergeInto(List<IManyOnePathSortItem> records, List<IManyOnePathSortItem> newRecords, IComparer comparer)
		{
			for (int i = 0, j = 0; j < newRecords.Count; i++)
			{
				if (i >= records.Count || comparer.Compare(records[i], newRecords[j]) > 0)
				{
					// object at i is greater than current object to insert, or no more obejcts in records;
					// insert next object here.
					records.Insert(i, newRecords[j]);
					j++;
					// i gets incremented as usual past the inserted object. It will index the same
					// object next iteration as it did this one.
				}
			}
		}

		/// <summary>
		/// Add to collector the ManyOnePathSortItems which this sorter derives from
		/// the specified object. This default method makes a single mopsi not involving any
		/// path.
		/// </summary>
		public virtual void CollectItems(int hvo, List<IManyOnePathSortItem> collector)
		{
			collector.Add(new ManyOnePathSortItem(hvo, null, null));
		}

		/// <summary>
		/// May be implemented to preload before sorting large collections (typically most of the instances).
		/// Currently will not be called if the column is also filtered; typically the same Preload() would end
		/// up being done.
		/// </summary>
		public virtual void Preload(ICmObject rootObj)
		{
		}

		/// <summary>
		/// Return true if the other sorter is 'compatible' with this, in the sense that
		/// either they produce the same sort sequence, or one derived from it (e.g., by reversing).
		/// </summary>
		public virtual bool CompatibleSorter(RecordSorter other)
		{
			return false;
		}
	}
}