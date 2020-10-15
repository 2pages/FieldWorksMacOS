// Copyright (c) 2004-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Linq;
using LanguageExplorer.Filters;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.FieldWorks.FwCoreDlgs.Controls;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Utils;
using SIL.Xml;

namespace LanguageExplorer.Controls.XMLViews
{
	/// <summary>
	/// A FilterBar contains a sequence of combos or grey areas, one for each column of a browse view.
	/// </summary>
	internal sealed class FilterBar : UserControl
	{
		private BrowseViewer m_bv;
		private List<XElement> m_columns;
		private FilterSortItems m_items;
		private IFwMetaDataCache m_mdc;
		private LcmCache m_cache; // Use minimally, may want to factor out for non-db use.
		private ISilDataAccess m_sda;
		private ILgWritingSystemFactory m_wsf;
		private int m_userWs;
		private int m_stdFontHeight; // Keep track of this font height for calculating FilterBar heights
		private IVwStylesheet m_stylesheet;
		// True during UpdateActiveItems to suppress side-effects of setting text of combo.
		private bool m_fInUpdateActive;
		private IApp m_app;

		/// <summary>
		/// This is invoked when the user sets up, removes, or changes the filter for a column.
		/// </summary>
		internal event FilterChangeHandler FilterChanged;

		/// <summary />
		internal FilterBar(BrowseViewer bv, IApp app)
		{
			m_bv = bv;
			m_columns = m_bv.ColumnSpecs;
			m_app = app;
			m_cache = bv.Cache;
			m_mdc = m_cache.DomainDataByFlid.MetaDataCache;
			m_sda = m_cache.DomainDataByFlid;
			m_wsf = m_sda.WritingSystemFactory;
			m_userWs = m_cache.ServiceLocator.WritingSystemManager.UserWs;
			// Store the standard font height for use in SetStyleSheet
			using (var tempFont = new Font(MiscUtils.StandardSerif, (float)10.0))
			{
				m_stdFontHeight = tempFont.Height;
			}
			// This light grey background shows through for any columns where we don't have a combo
			// because we can't figure a IStringFinder from the XmlParameters.
			BackColor = Color.FromKnownColor(KnownColor.ControlLight);
			MakeItems();
			AccessibilityObject.Name = "FilterBar";
		}

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
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
				foreach (Control ctl in Controls)
				{
					if (!(ctl is FwComboBox))
					{
						continue;
					}
					var combo = ctl as FwComboBox;
					combo.SelectedIndexChanged -= Combo_SelectedIndexChanged;
					// The Clear() below disposes the items in the ObjectCollection
					if (combo.ListBox != null && !combo.ListBox.IsDisposed) // ListBox contains Items
					{
						combo.Items.Clear();
					}
				}
				if (m_items != null)
				{
					for (var i = 0; i < m_items.Count(); i++)
					{
						var fsi = m_items[i];
						fsi.FilterChanged -= FilterChangedHandler;
						fsi.Dispose();
					}
				}
			}

			// Dispose unmanaged resources here, whether disposing is true or false.
			m_bv = null; // Parent window.
			m_cache = null;
			m_columns = null; // Client needs to deal with the columns.
			m_items = null;
			m_mdc = null;
			m_sda = null;
			m_wsf = null;

			// This will handle any controls that are in the Controls property.
			base.Dispose(disposing);
		}

		/// <summary>
		/// An array of info about all columns (except the extra check box column, if present).
		/// </summary>
		internal FilterSortItem[] ColumnInfo => m_items.ToArray();

		// Offset to add to real column index to get corresponding index into ColumnInfo.
		// Current 1 if check boxes present, otherwise zero.
		internal int ColumnOffset { get; private set; }

		/// <summary>
		/// Updates the column list. User has changed list of columns. Rework everything.
		/// </summary>
		internal void UpdateColumnList()
		{
			m_columns = m_bv.ColumnSpecs;
			SuspendLayout();
			foreach (var fsi in m_items)
			{
				// Will be disposed in MakeOrReuseItems().
				if (fsi?.Combo != null)
				{
					Controls.Remove(fsi.Combo);
				}
			}
			MakeOrReuseItems();
			ResumeLayout();
		}

		/// <summary>
		/// Makes the items.
		/// </summary>
		internal void MakeItems()
		{
			if (m_items != null)
			{
				Debug.Fail("Don't call method more than once!");
				return; // already made.
			}
			MakeOrReuseItems();
		}

		/// <summary>
		/// Make the items for the columns. If we are updating the columns we're trying to reuse
		/// existing items so that filter and sorter don't get messed up.
		/// </summary>
		private void MakeOrReuseItems()
		{
			if (m_bv.BrowseView == null || m_bv.BrowseView.Vc == null)
			{
				Debug.Fail("Don't call method too soon!");
				return; // too soon.
			}
			ColumnOffset = m_bv.BrowseView.Vc.HasSelectColumn ? 1 : 0;
			var oldItems = m_items ?? new FilterSortItems();
			m_items = new FilterSortItems();
			// Here we figure which columns we can filter on.
			foreach (var colSpec in m_columns)
			{
				if (oldItems.Contains(colSpec))
				{
					var item = oldItems[colSpec];
					m_items.Add(item);
					Controls.Add(item.Combo);
					oldItems.Remove(colSpec);
				}
				else
				{
					m_items.Add(MakeItem(colSpec));
				}
			}
			foreach (var item in oldItems)
			{
				item.FilterChanged -= FilterChangedHandler;
				item.Dispose();
			}
		}

		/// <summary>
		/// Given the current record filter of the record list, determine whether any of the active
		/// filters could have been created by any of your filter sort items, and if so,
		/// update the filter bar to show they are active.
		/// </summary>
		internal void UpdateActiveItems(IRecordFilter currentFilter)
		{
			try
			{
				m_fInUpdateActive = true;
				if (currentFilter is AndFilter andFilter)
				{
					// We have to copy the list, because the internal operation of this loop
					// may change the original list, invalidating the (implicit) enumerator.
					// See LT-1133 for an example of what happens without making this copy.
					var filters = new List<IRecordFilter>(andFilter.Filters);
					foreach (var filter in filters)
					{
						ActivateCompatibleFilter(filter);
					}
				}
				else
				{
					// Try to activate the single filter itself.
					ActivateCompatibleFilter(currentFilter);
				}
			}
			finally
			{
				//Adjust the FilterBar height and combo box heights to accomodate the
				//strings in the filter comboBoxes
				AdjustBarHeights();
				m_fInUpdateActive = false;
			}
		}

		/// <summary>
		/// Adjust the FilterBar and Combo boxes to reflect writing system font sizes for the active filters.
		/// </summary>
		private void AdjustBarHeights()
		{
			var maxComboHeight = GetMaxComboHeight();
			SetBarHeight(maxComboHeight); // Set height of FilterBar and its ComboBoxes
		}

		/// <summary>
		/// Given a filter bar cell filter which is (part of) the active filter for your
		/// record list, if one of your cells understands it install it as the active filter
		/// for that cell. Otherwise, remove it from the record list filter.
		/// (Except: if it's not a user-visible filter, we don't expect to show it, so
		/// skip it.)
		/// </summary>
		private void ActivateCompatibleFilter(IRecordFilter filter)
		{
			if (filter == null || !filter.IsUserVisible)
			{
				return;
			}
			foreach (var fsi in m_items)
			{
				if (fsi != null && fsi.SetFromFilter(filter))
				{
					return;
				}
			}
			// we couldn't find a match in the active columns.
			// if we've already fully initialized the filters, then remove it from the record list filter.
			FilterChanged?.Invoke(this, new FilterChangeEventArgs(null, filter));
		}

		/// <summary>
		/// Determine if the given filter (or subfilter) can be activated for the given
		/// column specification.
		/// </summary>
		/// <returns>true, if the column node spec can use the filter.</returns>
		internal bool CanActivateFilter(IRecordFilter filter, XElement colSpec)
		{
			switch (filter)
			{
				case AndFilter andFilter:
				{
					var filters = andFilter.Filters;
					if (filters.Any(rf => (rf is FilterBarCellFilter || rf is ListChoiceFilter) && CanActivateFilter(rf, colSpec)))
					{
						return true;
					}
					break;
				}
				case FilterBarCellFilter filterBarCellFilter:
				{
					var colFinder = LayoutFinder.CreateFinder(m_cache, colSpec, m_bv.BrowseView.Vc, m_app);
					var fSameFinder = filterBarCellFilter.Finder.SameFinder(colFinder);
					(colFinder as IDisposable)?.Dispose();
					return fSameFinder;
				}
				case ListChoiceFilter choiceFilter:
					return choiceFilter.CompatibleFilter(colSpec);
			}
			return false;
		}

		/// <summary>
		/// Set the widths of the columns.
		/// </summary>
		internal void SetColWidths(int[] widths)
		{
			// We can only do this meaningfully if given the right number of lengths.
			// If this is wrong (which for example can happen if this routine gets
			// called during UpdateColumnList of the browse view before UpdateColumnList
			// has been called on the filter bar), ignore it, and hope we get adjusted
			// again after everything has the right number of items.
			if (widths.Length - ColumnOffset != m_items.Count)
			{
				return;
			}
			var x = 0;
			if (ColumnOffset > 0)
			{
				x = widths[0];
			}
			// not sure how to get the correct value for this, but it looks like column headers
			// are offset by a small value, so we shift the filter bar to line up properly
			x += 2;
			for (var i = 0; i < widths.Length - ColumnOffset; ++i)
			{
				if (m_items[i] != null)
				{
					m_items[i].Combo.Left = x;
					m_items[i].Combo.Width = widths[i + ColumnOffset];
				}
				x += widths[i + ColumnOffset];
			}
		}

		/// <summary>
		/// Make a FilterSortItem for the column specified by the given viewSpec,
		/// if it follows a pattern we recognize. Otherwise, return null.
		/// If successful, the FSI is initialized with the viewSpec, a IStringFinder, and
		/// a combo.
		/// The child of the column node is like the contents of a fragment for displaying
		/// the item.
		/// Often the thing we really want is embedded inside something else.
		/// <para>
		/// 		<properties>
		/// 			<bold value="on"/>
		/// 		</properties>
		/// 		<stringalt class="LexEntry" field="CitationForm" ws="vernacular"/>
		/// 	</para>
		/// </summary>
		private FilterSortItem MakeItem(XElement colSpec)
		{
			return MakeLayoutItem(colSpec);
		}

		/* Not used.
		private static string GetStringAtt(XElement node, string name)
		{
			return node.Attribute(name)?.Value;
		}*/

		/// <summary>
		/// Make a FilterSortItem with a finder that is a LayoutFinder with the specified layout name.
		/// </summary>
		private FilterSortItem MakeLayoutItem(XElement colSpec)
		{
			var result = new FilterSortItem
			{
				Spec = colSpec,
				Finder = LayoutFinder.CreateFinder(m_cache, colSpec, m_bv.BrowseView.Vc, m_app)
			};
			SetupFsi(result);
			var ws = WritingSystemServices.GetWritingSystem(m_cache, colSpec.ConvertElement(), null, 0) ?? m_cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem;
			result.Sorter = new GenRecordSorter(new StringFinderCompare(result.Finder, new WritingSystemComparer(ws)));
			return result;
		}

		/// <summary>
		/// Get a default size for a FilterBar. The width is arbitrary, as it is always docked
		/// top, but the height is important and should match a standard combo.
		/// </summary>
		protected override Size DefaultSize => new Size(100, FwComboBoxBase.ComboHeight);

		private ITsString MakeLabel(string name)
		{
			return MakeLabel(name, m_userWs);
		}

		/// <summary>
		/// Make the standard sort of label we put in combo items for the filter bar for the specified string.
		/// </summary>
		private static ITsString MakeLabel(string name, int userWs)
		{
			var bldr = TsStringUtils.MakeString(name, userWs).GetBldr();
			// per FWR-1256, we want to use the default font for stuff in the UI writing system.
			bldr.SetStrPropValue(0, bldr.Length, (int)FwTextPropType.ktptNamedStyle, StyleServices.UiElementStylename);
			return bldr.GetString();
		}

		/// <summary>
		/// The stuff common to all the ways we mak an FSI.
		/// </summary>
		private void SetupFsi(FilterSortItem item)
		{
			MakeCombo(item);
			item.FilterChanged += FilterChangedHandler;
		}

		private void FilterChangedHandler(object sender, FilterChangeEventArgs args)
		{
			FilterChanged?.Invoke(this, args);
		}

		/// <summary>
		/// Create the common options for all FSI combos (except Integer).
		/// </summary>
		private void MakeCombo(FilterSortItem item)
		{
			var combo = new FwComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				BackColor = SystemColors.Window,
				WritingSystemFactory = m_wsf,
				StyleSheet = m_bv.StyleSheet
			};
			item.Combo = combo;
			combo.Items.Add(new FilterComboItem(MakeLabel(XMLViewsStrings.ksShowAll), null, item));
			var blankPossible = XmlUtils.GetOptionalAttributeValue(item.Spec, "blankPossible", "true");
			if (blankPossible == "true")
			{
				combo.Items.Add(new FilterComboItem(MakeLabel(XMLViewsStrings.ksBlanks), new BlankMatcher(), item));
				combo.Items.Add(new FilterComboItem(MakeLabel(XMLViewsStrings.ksNonBlanks), new NonBlankMatcher(), item));
			}
			// Enhance JohnT: figure whether the column has vernacular or analysis data...
			var ws = 0;
			if (item.Spec != null)
			{
				var wsParam = XmlViewsUtils.FindWsParam(item.Spec);
				if (wsParam.Length == 0)
				{
					wsParam = XmlUtils.GetOptionalAttributeValue(item.Spec, "ws", string.Empty);
				}
				ws = XmlViewsUtils.GetWsFromString(wsParam, m_cache);
			}
			if (ws == 0)
			{
				ws = m_cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem.Handle; // some sort of fall-back in case we can't determine a WS from the spec.
			}
			var beSpec = XmlUtils.GetOptionalAttributeValue(item.Spec, "bulkEdit", string.Empty);
			if (string.IsNullOrEmpty(beSpec))
			{
				beSpec = XmlUtils.GetOptionalAttributeValue(item.Spec, "chooserFilter", string.Empty);
			}
			var sortType = XmlUtils.GetOptionalAttributeValue(item.Spec, "sortType", null);
			switch (sortType)
			{
				case "integer":
					// For columns which are integer values we offer the user a couple preset filters
					// one is  "0"  and the other is "Greater than zero"
					combo.Items.Add(new FilterComboItem(MakeLabel(XMLViewsStrings.ksZero),
						new ExactMatcher(MatchExactPattern(XMLViewsStrings.ksZero)), item));
					combo.Items.Add(new FilterComboItem(MakeLabel(XMLViewsStrings.ksGreaterThanZero),
						new RangeIntMatcher(1, int.MaxValue), item));
					combo.Items.Add(new FilterComboItem(MakeLabel(XMLViewsStrings.ksGreaterThanOne),
						new RangeIntMatcher(2, int.MaxValue), item));
					combo.Items.Add(new RestrictComboItem(MakeLabel(XMLViewsStrings.ksRestrict_),
						m_bv.PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider),
						item,
						m_cache.ServiceLocator.WritingSystemManager.UserWs,
						combo));
					break;
				case "genDate":
				case "date":
					combo.Items.Add(new RestrictDateComboItem(MakeLabel(XMLViewsStrings.ksRestrict_),
						m_bv.PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider),
						item,
						m_cache.ServiceLocator.WritingSystemManager.UserWs,
						sortType == "genDate",
						combo));
					break;
				case "YesNo":
					// For columns which have only the values of "yes" or "no" we offer the user these preset
					// filters to choose.
					combo.Items.Add(new FilterComboItem(MakeLabel(LanguageExplorerResources.ksYes.ToLowerInvariant()), new ExactMatcher(MatchExactPattern(LanguageExplorerResources.ksYes.ToLowerInvariant())), item));
					combo.Items.Add(new FilterComboItem(MakeLabel(LanguageExplorerResources.ksNo.ToLowerInvariant()), new ExactMatcher(MatchExactPattern(LanguageExplorerResources.ksNo.ToLowerInvariant())), item));
					break;
				case "stringList":
					var labels = m_bv.BrowseView.GetStringList(item.Spec);
					if (labels == null)
					{
						break;
					}
					foreach (var aLabel in labels)
					{
						combo.Items.Add(new FilterComboItem(MakeLabel(aLabel), new ExactMatcher(MatchExactPattern(aLabel)), item));
					}
					if (labels.Length > 2)
					{
						foreach (var aLabel in labels)
						{
							combo.Items.Add(new FilterComboItem(MakeLabel(string.Format(XMLViewsStrings.ksExcludeX, aLabel)), new InvertMatcher(new ExactMatcher(MatchExactPattern(aLabel))), item));
						}
					}
					break;
				default:
					// If it isn't any of those, include the bad spelling item, provided we have a dictionary
					// for the relevant language, and provided it is NOT a list (for which we will make a chooser).
					if (!string.IsNullOrEmpty(beSpec))
					{
						break;
					}
					AddSpellingErrorsIfAppropriate(item, combo, ws);
					break;
			}
			combo.Items.Add(new FindComboItem(MakeLabel(XMLViewsStrings.ksFilterFor_), item, ws, combo, m_bv));
			if (!string.IsNullOrEmpty(beSpec))
			{
				MakeListChoiceFilterItem(item, combo, beSpec, m_bv.PropertyTable);
			}
			// Todo: lots more interesting items.
			// - search the list for existing names
			// - "any of" and "none of" launch a dialog with check boxes for all existing values.
			//		- maybe a control to check all items containing...
			// - "containing" launches dialog asking for string (may contain # at start or end).
			// - "matching pattern" launches dialog to obtain pattern.
			// - "custom" may launch dialog with "OR" options and "is, is not, is less than, is greater than, matches,..."
			// How can we get the current items? May not be available until later...
			// - May need to add 'ShowList' event to FwComboBox so we can populate the list when we show it.
			combo.SelectedIndex = 0;
			// Do this after selecting initial item, so we don't get a spurious notification.
			combo.SelectedIndexChanged += Combo_SelectedIndexChanged;
			combo.AccessibleName = "FwComboBox";
			Controls.Add(combo);
		}

		private void AddSpellingErrorsIfAppropriate(FilterSortItem item, FwComboBox combo, int ws)
		{
			// LT-9047 For certain fields, filtering on Spelling Errors just doesn't make sense.
			var layoutNode = item.Spec.Attribute("layout") ?? item.Spec.Attribute("label");
			var layout = string.Empty;
			if (layoutNode != null)
			{
				layout = layoutNode.Value;
			}
			switch (layout)
			{
				case "Pronunciation":
				case "CVPattern":
					break;
				default:
					var dict = m_bv.BrowseView.RootSiteEditingHelper.GetDictionary(ws);
					if (dict != null)
					{
						combo.Items.Add(new FilterComboItem(MakeLabel(XMLViewsStrings.ksSpellingErrors), new BadSpellingMatcher(ws), item));
					}
					break;
			}
		}

		internal IVwPattern MatchExactPattern(string str)
		{
			var ws = m_cache.ServiceLocator.WritingSystems.DefaultAnalysisWritingSystem.Handle;
			IVwPattern pattern = VwPatternClass.Create();
			pattern.MatchOldWritingSystem = false;
			pattern.MatchDiacritics = false;
			pattern.MatchWholeWord = false;
			pattern.MatchCase = false;
			pattern.UseRegularExpressions = false;
			pattern.Pattern = TsStringUtils.MakeString(str, ws);
			return pattern;
		}

		/// <summary>
		/// Make a combo menu item (and install it) for choosing from a list, based on the column
		/// spec at item.Spec.
		/// </summary>
		private void MakeListChoiceFilterItem(FilterSortItem item, FwComboBox combo, string beSpec, IPropertyTable propertyTable)
		{
			/*
			// Non-recursive caller: "bulkEdit" OR "chooserFilter"
			var beSpec = XmlUtils.GetOptionalAttributeValue(item.Spec, "bulkEdit", string.Empty);
			if (string.IsNullOrEmpty(beSpec))
			{
				beSpec = XmlUtils.GetOptionalAttributeValue(item.Spec, "chooserFilter", string.Empty);
			}
			// Recursive caller: "chooserFilter"
			// if we didn't find it, try "chooserFilter", if we haven't already.
			var chooserFilter = XmlUtils.GetOptionalAttributeValue(item.Spec, "chooserFilter", string.Empty);
			if (!string.IsNullOrEmpty(chooserFilter) && chooserFilter != beSpec)
			{
				MakeListChoiceFilterItem(item, combo, chooserFilter, propertyTable);
			}
			 */
			switch (beSpec)
			{
				case "complexListMultiple":
					combo.Items.Add(new ListChoiceComboItem(MakeLabel(XMLViewsStrings.ksChoose_), item, m_cache, propertyTable, combo, false));
					break;
				case "textsFilterItem":
					combo.Items.Add(new TextsFilterItem(MakeLabel(XmlUtils.GetOptionalAttributeValue(item.Spec, "specialItemName", XMLViewsStrings.ksChoose_)), m_bv.Publisher));
					break;
				case "atomicFlatListItem": // Fall through
				case "morphTypeListItem":
					combo.Items.Add(new ListChoiceComboItem(MakeLabel(XMLViewsStrings.ksChoose_), item, m_cache, propertyTable, combo, true));
					break;
				case "EntryPosFilter":
					SetUpConfusedCases(propertyTable, combo, item, typeof(EntryPosFilter));
					break;
				case "PosFilter":
					SetUpConfusedCases(propertyTable, combo, item, typeof(PosFilter));
					break;
				case "InflectionClassFilter":
					SetUpConfusedCases(propertyTable, combo, item, typeof(InflectionClassFilter));
					break;
				default:
					// if we didn't find it, try "chooserFilter", if we haven't already.
					var chooserFilter = XmlUtils.GetOptionalAttributeValue(item.Spec, "chooserFilter", string.Empty);
					if (!string.IsNullOrEmpty(chooserFilter) && chooserFilter != beSpec)
					{
						MakeListChoiceFilterItem(item, combo, chooserFilter, propertyTable);
					}
					return;
			}
		}

		/// <summary>
		/// These cases cannot be created until much later, and are created via Reflection,
		/// when more context is available for the filters to use.
		/// </summary>
		private void SetUpConfusedCases(IPropertyTable propertyTable, FwComboBox combo, FilterSortItem item, Type beType)
		{
			Type filterType = null;
			if (typeof(ListChoiceFilter).IsAssignableFrom(beType))
			{
				// typically it is a chooserFilter attribute, and gives the actual filter.
				filterType = beType;
			}
			else
			{
				// typically got a bulkEdit spec, and the editor class may know a compatible filter class.
				var mi = beType.GetMethod("FilterType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
				if (mi != null)
				{
					filterType = mi.Invoke(null, null) as Type;
				}
			}
			if (filterType != null)
			{
				var fAtomic = false;
				var pi = filterType.GetProperty("Atomic", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
				if (pi != null)
				{
					fAtomic = (bool)pi.GetValue(null, null);
				}
				var comboItem = new ListChoiceComboItem(MakeLabel(XMLViewsStrings.ksChoose_), item, m_cache, propertyTable, combo, fAtomic, filterType);
				combo.Items.Add(comboItem);
				var piLeaf = filterType.GetProperty("LeafFlid", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
				if (piLeaf != null)
				{
					comboItem.LeafFlid = (int)piLeaf.GetValue(null, null);
				}
			}
		}

		private bool m_fInSelectedIndexChanged;

		private void Combo_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (!(sender is FwComboBox combo) || m_fInSelectedIndexChanged)
			{
				return;
			}
			m_fInSelectedIndexChanged = true;
			try
			{
				if (m_fInUpdateActive)
				{
					// The following colorization was requested by LT-2183.
					combo.BackColor = combo.SelectedIndex == 0 ? SystemColors.Window : Color.Yellow;
					return;
				}
				if (combo.SelectedItem is FilterComboItem fci) // Happens when we set the text to what the user typed.
				{
					if (fci.Invoke())
					{
						// The following colorization was requested by LT-2183.
						combo.BackColor = combo.SelectedIndex == 0 ? SystemColors.Window : Color.Yellow;
					}
					else
					{
						// Restore previous combo text
						combo.Tss = combo.PreviousTextBoxText;
					}
				}
			}
			finally
			{
				m_fInSelectedIndexChanged = false;
			}
		}

		/// <summary>
		/// Reset any filters to empty.  This assumes that index 0 of the internal combobox
		///  selects the "no filter".
		/// </summary>
		internal void RemoveAllFilters()
		{
			if (m_items == null)
			{
				return;
			}
			foreach (var sortItem in m_items)
			{
				if (sortItem?.Combo != null && sortItem.Combo.SelectedIndex != 0)
				{
					sortItem.Combo.SelectedIndex = 0;
				}
			}
			//Adjust the FilterBar height and combo box heights to accomodate the
			//strings in the filter comboBoxes
			AdjustBarHeights();
		}

		/// <summary>
		/// Apply the stylesheet to each combobox.
		/// </summary>
		internal void SetStyleSheet(IVwStylesheet stylesheet)
		{
			m_stylesheet = stylesheet;
			// Also apply stylesheet to each ComboBox.
			foreach (var item in m_items)
			{
				if (item.Combo is FwComboBox comboBox)
				{
					comboBox.StyleSheet = stylesheet;
				}
			}
		}

		/// <summary>
		/// Search through the strings for the filters on columns and pick the tallest font height
		/// </summary>
		private int GetMaxComboHeight()
		{
			var maxComboHeight = 0;
			// For each column in browse views seach through the Combo boxes (Filters)
			// then return the tallest font height from these.
			foreach (var item in m_items)
			{
				var ws = TsStringUtils.GetWsAtOffset(item.Combo.Tss, 0);
				using (var tempFont = FontHeightAdjuster.GetFontForNormalStyle(ws, m_stylesheet, m_wsf))
				{
					maxComboHeight = Math.Max(maxComboHeight, tempFont.Height);
				}
			}
			return maxComboHeight;
		}

		/// <summary>
		/// Set the 'FilterBar' height and that of all its associated 'ComboBox'es
		/// </summary>
		private void SetBarHeight(int height)
		{
			// Calculate what to add to height for combobox to look right
			height += FwComboBoxBase.ComboHeight - m_stdFontHeight;
			Height = height;
			foreach (var item in m_items)
			{
				if (!(item.Combo is FwComboBox comboBox))
				{
					continue;
				}
				item.Combo.Height = height;
				item.Combo.PerformLayout();
				item.Combo.Tss = FontHeightAdjuster.GetUnadjustedTsString(item.Combo.Tss);
			}
		}

		/// <summary>
		/// List of FilterSortItems that can also be accessed by the XML spec.
		/// </summary>
		private sealed class FilterSortItems : KeyedCollection<XElement, FilterSortItem>
		{
			protected override XElement GetKeyForItem(FilterSortItem item)
			{
				return item.Spec;
			}
		}

		/// <summary />
		private sealed class RestrictDateComboItem : FilterComboItem
		{
			FwComboBox m_combo;
			int m_ws;
			private IHelpTopicProvider m_helpTopicProvider;
			bool m_fGenDate;

			/// <summary />
			internal RestrictDateComboItem(ITsString tssName, IHelpTopicProvider helpTopicProvider, FilterSortItem fsi, int ws, bool fGenDate, FwComboBox combo) : base(tssName, null, fsi)
			{
				m_helpTopicProvider = helpTopicProvider;
				m_combo = combo;
				m_ws = ws;
				m_fGenDate = fGenDate;
			}

			/// <inheritdoc />
			protected override void Dispose(bool disposing)
			{
				// Must not be run more than once.
				if (IsDisposed)
				{
					return;
				}

				if (disposing)
				{
				}

				m_combo = null;

				base.Dispose(disposing);
			}

			/// <summary>
			/// Invokes this instance.
			/// </summary>
			internal override bool Invoke()
			{
				using (var dlg = new SimpleDateMatchDlg(m_helpTopicProvider))
				{
					dlg.SetDlgValues(m_matcher);
					dlg.HandleGenDate = m_fGenDate;
					if (dlg.ShowDialog(m_combo) != DialogResult.OK)
					{
						return false;
					}

					m_matcher = dlg.ResultingMatcher;
					m_matcher.WritingSystemFactory = m_combo.WritingSystemFactory;
					m_combo.SelectedIndex = -1; // allows setting text to item not in list, see comment in FindComboItem.Invoke().
					m_combo.Tss = TsStringUtils.MakeString(dlg.Pattern, m_ws);
					var label = m_combo.Tss;
					m_matcher.Label = label;
					// We can't call base.Invoke BEFORE we set the label, because it will persist
					// the wrong label. And we can't call it AFTER we set the label, becaseu it
					// will override our label. So we just copy here a simplified version of the
					// base method. If it gets much more complicated, factor out the common parts
					// into new methods.
					//base.Invoke ();
					m_fsi.Matcher = m_matcher;
					m_fsi.Filter = new FilterBarCellFilter(m_fsi.Finder, m_matcher);
				}

				return true;
			}

			/// <summary>
			/// Determine whether this combo item could have produced the specified matcher.
			/// If so, return the string that should be displayed as the value of the combo box
			/// when this matcher is active. Otherwise return null.
			/// </summary>
			internal override ITsString SetFromMatcher(IMatcher matcher)
			{
				return matcher is DateTimeMatcher ? matcher.Label : null;
			}
		}

		/// <summary />
		private sealed class RestrictComboItem : FilterComboItem
		{
			FwComboBox m_combo;
			int m_ws;
			private IHelpTopicProvider m_helpTopicProvider;

			/// <summary />
			internal RestrictComboItem(ITsString tssName, IHelpTopicProvider helpTopicProvider, FilterSortItem fsi, int ws, FwComboBox combo) : base(tssName, null, fsi)
			{
				m_helpTopicProvider = helpTopicProvider;
				m_combo = combo;
				m_ws = ws;
			}

			/// <inheritdoc />
			protected override void Dispose(bool disposing)
			{
				// Must not be run more than once.
				if (IsDisposed)
				{
					return;
				}

				if (disposing)
				{
				}

				m_combo = null;

				base.Dispose(disposing);
			}

			/// <summary>
			/// Invokes this instance.
			/// </summary>
			internal override bool Invoke()
			{
				using (var dlg = new SimpleIntegerMatchDlg(m_helpTopicProvider))
				{
					dlg.SetDlgValues(m_matcher);
					if (dlg.ShowDialog(m_combo) != DialogResult.OK)
					{
						return false;
					}

					m_matcher = dlg.ResultingMatcher;
					m_matcher.WritingSystemFactory = m_combo.WritingSystemFactory;
					m_combo.SelectedIndex = -1; // allows setting text to item not in list, see comment in FindComboItem.Invoke().
					m_combo.Tss = TsStringUtils.MakeString(dlg.Pattern, m_ws);
					var label = m_combo.Tss;
					m_matcher.Label = label;
					// We can't call base.Invoke BEFORE we set the label, because it will persist
					// the wrong label. And we can't call it AFTER we set the label, becaseu it
					// will override our label. So we just copy here a simplified version of the
					// base method. If it gets much more complicated, factor out the common parts
					// into new methods.
					//base.Invoke ();
					m_fsi.Matcher = m_matcher;
					m_fsi.Filter = new FilterBarCellFilter(m_fsi.Finder, m_matcher);
				}
				return true;
			}

			/// <summary>
			/// Determine whether this combo item could have produced the specified matcher.
			/// If so, return the string that should be displayed as the value of the combo box
			/// when this matcher is active. Otherwise return null.
			/// </summary>
			internal override ITsString SetFromMatcher(IMatcher matcher)
			{
				return matcher is IIntMatcher ? matcher.Label : null;
			}
		}
	}
}