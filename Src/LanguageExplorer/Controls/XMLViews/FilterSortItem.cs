// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Xml.Linq;
using LanguageExplorer.Filters;

namespace LanguageExplorer.Controls.XMLViews
{
	/// <summary>
	/// This class will be used in both sorting and filtering. From a viewSpec node is derived
	/// a IStringFinder that can find one or several strings that display in the column for a
	/// particular object. Using that and the list of records we build a combo box displaying
	/// options for filtering this column. From what is typed and selected in the combo,
	/// we may construct a Matcher, which combines with the string finder to make a
	/// FilterBarCellFilter. Eventually we will use the string finder also if sorting by this
	/// column.</summary>
	/// <remarks>
	/// Todo: for reasonable efficiency, need a way to preload the information needed to
	/// evaluate filter for all items. This might be a method on IRecordFilter.
	/// </remarks>
	internal sealed class FilterSortItem : IDisposable
	{
		private FwComboBox m_combo;
		private IMatcher m_matcher;
		private IRecordFilter m_filter;

		/// <summary />
		internal event FilterChangeHandler FilterChanged;

		#region IDisposable & Co. implementation

		/// <summary>
		/// See if the object has been disposed.
		/// </summary>
		private bool IsDisposed { get; set; }

		/// <summary>
		/// Finalizer, in case client doesn't dispose it.
		/// Force Dispose(false) if not already called (i.e. m_isDisposed is true)
		/// </summary>
		/// <remarks>
		/// In case some clients forget to dispose it directly.
		/// </remarks>
		~FilterSortItem()
		{
			Dispose(false);
			// The base class finalizer is called automatically.
		}

		/// <summary />
		public void Dispose()
		{
			Dispose(true);
			// This object will be cleaned up by the Dispose method.
			// Therefore, you should call GC.SuppressFinalize to
			// take this object off the finalization queue
			// and prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Executes in two distinct scenarios.
		///
		/// 1. If disposing is true, the method has been called directly
		/// or indirectly by a user's code via the Dispose method.
		/// Both managed and unmanaged resources can be disposed.
		///
		/// 2. If disposing is false, the method has been called by the
		/// runtime from inside the finalizer and you should not reference (access)
		/// other managed objects, as they already have been garbage collected.
		/// Only unmanaged resources can be disposed.
		/// </summary>
		/// <param name="disposing"></param>
		/// <remarks>
		/// If any exceptions are thrown, that is fine.
		/// If the method is being done in a finalizer, it will be ignored.
		/// If it is thrown by client code calling Dispose,
		/// it needs to be handled by fixing the bug.
		///
		/// If subclasses override this method, they should call the base implementation.
		/// </remarks>
		private void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****************** Missing Dispose() call for " + GetType().Name + ". ******************");
			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}

			if (disposing)
			{
				// Dispose managed resources here.
				m_combo?.Dispose();
			}

			// Dispose unmanaged resources here, whether disposing is true or false.
			Spec = null;
			m_combo = null;
			Finder = null;
			Sorter = null;
			m_filter = null;
			m_matcher = null;

			IsDisposed = true;
		}

		#endregion IDisposable & Co. implementation

		/// <summary>
		/// Gets or sets the spec.
		/// </summary>
		internal XElement Spec { get; set; }

		/// <summary>
		/// Gets or sets the finder.
		/// </summary>
		/// <remarks>A Finder assigned here will be disposed by FilterSortItem.Dispose.</remarks>
		internal IStringFinder Finder { get; set; }

		/// <summary>
		/// Gets or sets the combo.
		/// </summary>
		/// <remarks>The Combo that gets set here will be disposed by FilterSortItem.</remarks>
		internal FwComboBox Combo
		{
			get => m_combo;
			set
			{
				m_combo?.Dispose();
				m_combo = value;
			}
		}

		/// <summary>
		/// Gets or sets the matcher.
		/// </summary>
		internal IMatcher Matcher
		{
			get => m_matcher;
			set
			{
				m_matcher = value;
				if (m_matcher != null && m_matcher.WritingSystemFactory == null && m_combo != null)
				{
					m_matcher.WritingSystemFactory = m_combo.WritingSystemFactory;
				}
			}
		}

		/// <summary>
		/// Gets or sets the sorter.
		/// </summary>
		/// <remarks>A Sorter assigned here will be disposed by FilterSortItem.Dispose.</remarks>
		internal IRecordSorter Sorter { get; set; }

		/// <summary>
		/// Gets or sets the filter.
		/// </summary>
		/// <value>The filter.</value>
		/// <remarks>A Filter assigned here will be disposed by FilterSortItem.Dispose.</remarks>
		internal IRecordFilter Filter
		{
			get => m_filter;
			set
			{
				var old = m_filter;
				m_filter = value;
				// Change the filter if they are not the same );
				if (FilterChanged != null && (m_filter != null && !m_filter.SameFilter(old)) || (old != null && !old.SameFilter(m_filter)))
				{
					FilterChanged?.Invoke(this, new FilterChangeEventArgs(m_filter, old));
				}
			}
		}

		/// <summary>
		/// If this filter could have been created from this FSI, set it as your active
		/// filter and update your display accordingly, and answer true. Otherwise
		/// answer false.
		/// </summary>
		internal bool SetFromFilter(IRecordFilter filter)
		{
			// Need to set even if set previously. Otherwise it doesn't refresh properly.
			if (m_combo == null)
			{
				return false; // probably can't happen, but play safe
			}
			foreach (FilterComboItem fci in m_combo.Items)
			{
				var tssLabel = fci?.SetFromFilter(filter, this);
				if (tssLabel == null)
				{
					continue;
				}
				m_combo.SelectedIndex = -1; // prevents failure of setting Tss if not in list.
				m_combo.Tss = tssLabel;
				m_filter = filter; // remember this filter is active!
				return true;
			}
			return false;
		}

		// Todo:
		// Add to FilterBar event for changing (add and/or remove) filter.
		// Add same to BrowseViewer (connect so forwards to from FilterBar if any)
		// Add to RecordList ability to add/remove filters and refresh list.
		// Configure RecordBrowseView to handle filter changes by updating record list.
	}
}