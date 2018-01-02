// Copyright (c) 2015-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using SIL.Code;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Infrastructure;
using SIL.LCModel;
using SIL.LCModel.DomainServices;
using SIL.FieldWorks.Filters;
using SIL.FieldWorks.Common.FwUtils;
using SIL.ObjectModel;
using SIL.Xml;

namespace LanguageExplorer.Controls.XMLViews
{
	/// <summary>
	/// This class caches the layout and part inventories and optimizes looking up a particular item.
	/// </summary>
	public class LayoutCache
	{
		IFwMetaDataCache m_mdc;
		readonly Inventory m_layoutInventory;
		readonly Inventory m_partInventory;
		readonly Dictionary<Tuple<int, string, bool>, XElement> m_map = new Dictionary<Tuple<int, string, bool>, XElement>();

		/// <summary>
		/// Initializes a new instance of the <see cref="LayoutCache"/> class.
		/// </summary>
		/// <param name="mdc">The MDC.</param>
		/// <param name="layouts">The layouts.</param>
		/// <param name="parts">The parts.</param>
		/// <remarks>TESTS ONLY.</remarks>
		public LayoutCache(IFwMetaDataCache mdc, Inventory layouts, Inventory parts)
		{
			m_mdc = mdc;
			m_layoutInventory = layouts;
			m_partInventory = parts;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LayoutCache"/> class.
		/// </summary>
		/// <param name="mdc">The MDC.</param>
		/// <param name="sDatabase">The database name.</param>
		/// <param name="applicationName">The application's name.</param>
		/// <param name="projectPath">The project folder.</param>
		public LayoutCache(IFwMetaDataCache mdc, string sDatabase, string applicationName, string projectPath)
		{
			m_mdc = mdc;
			m_layoutInventory = Inventory.GetInventory("layouts", sDatabase);
			m_partInventory = Inventory.GetInventory("parts", sDatabase);
			if (m_layoutInventory == null || m_partInventory == null)
			{
				InitializePartInventories(sDatabase, applicationName, projectPath);
				m_layoutInventory = Inventory.GetInventory("layouts", sDatabase);
				m_partInventory = Inventory.GetInventory("parts", sDatabase);
			}
		}

		/// <summary>
		/// Layout Version Number (last updated by GordonM, 10 June 2016, as part of Etymology cluster update).
		/// </summary>
		/// <remarks>Note: often we also want to update BrowseViewer.kBrowseViewVersion.</remarks>
		public static readonly int LayoutVersionNumber = 25;

		/// <summary>
		/// Initializes the part inventories.
		/// </summary>
		/// <param name="sDatabase">The name of the database.</param>
		/// <param name="applicationName">The application's name.</param>
		/// <param name="projectPath">The path to the project folder.</param>
		public static void InitializePartInventories(string sDatabase, string applicationName, string projectPath)
		{
			InitializePartInventories(sDatabase, applicationName, true, projectPath);
		}

		/// <summary>
		/// Initialize the part inventories.
		/// </summary>
		public static void InitializePartInventories(string sDatabase, string applicationName, bool fLoadUserOverrides, string projectPath)
		{
			Guard.AgainstNullOrEmptyString(applicationName, nameof(applicationName));

			var partDirectory = Path.Combine(FwDirectoryFinder.FlexFolder, Path.Combine("Configuration", "Parts"));
			var keyAttrs = new Dictionary<string, string[]>
			{
				["layout"] = new[] {"class", "type", "name", "choiceGuid"},
				["group"] = new[] {"label"},
				["part"] = new[] {"ref"}
			};

			var layoutInventory = new Inventory(new[] {partDirectory}, "*.fwlayout", "/LayoutInventory/*", keyAttrs, applicationName, projectPath)
			{
				Merger = new LayoutMerger()
			};
			// Holding shift key means don't use extant preference file, no matter what.
			// This includes user overrides of layouts.
			if (fLoadUserOverrides && System.Windows.Forms.Control.ModifierKeys != System.Windows.Forms.Keys.Shift)
			{
				layoutInventory.LoadUserOverrides(LayoutVersionNumber, sDatabase);
			}
			else
			{
				layoutInventory.DeleteUserOverrides(sDatabase);
				// LT-11193: The above may leave some user defined dictionary views to be loaded.
				layoutInventory.LoadUserOverrides(LayoutVersionNumber, sDatabase);
			}
			Inventory.SetInventory("layouts", sDatabase, layoutInventory);

			keyAttrs = new Dictionary<string, string[]>
			{
				["part"] = new[] {"id"}
			};

			Inventory.SetInventory("parts", sDatabase, new Inventory(new[] {partDirectory}, "*Parts.xml", "/PartInventory/bin/*", keyAttrs, applicationName, projectPath));
		}

		/// <summary>
		/// Displaying Reversal Indexes requires expanding a variable number of writing system
		/// specific layouts.  This method does that for a specific writing system and database.
		/// </summary>
		/// <param name="sWsTag"></param>
		/// <param name="sDatabase"></param>
		public static void InitializeLayoutsForWsTag(string sWsTag, string sDatabase)
		{
			var layouts = Inventory.GetInventory("layouts", sDatabase);
			layouts?.ExpandWsTaggedNodes(sWsTag);
		}

		static readonly char[] ktagMarkers = { '-', LayoutKeyUtils.kcMarkLayoutCopy, LayoutKeyUtils.kcMarkNodeCopy };

		/// <summary>
		/// Gets the node.
		/// </summary>
		/// <param name="clsid">The CLSID.</param>
		/// <param name="layoutName">Name of the layout.</param>
		/// <param name="fIncludeLayouts">if set to <c>true</c> [f include layouts].</param>
		public XElement GetNode(int clsid, string layoutName, bool fIncludeLayouts)
		{
			Tuple<int, string, bool> key = Tuple.Create(clsid, layoutName, fIncludeLayouts);
			if (m_map.ContainsKey(key))
				return m_map[key];

			XElement node;
			int classId = clsid;
			string useName = layoutName ?? "default";
			string origName = useName;
			for( ; ; )
			{
				string classname = m_mdc.GetClassName(classId);
				if (fIncludeLayouts)
				{
					// Inventory of layouts has keys class, type, name
					node = m_layoutInventory.GetElement("layout", new[] {classname, "jtview", useName, null});
					if (node != null)
						break;
				}
				// inventory of parts has key id.
				node = m_partInventory.GetElement("part", new[] {classname + "-Jt-" + useName});
				if (node != null)
					break;
				if (classId == 0 && useName == origName)
				{
					// This is somewhat by way of robustness. When we generate a modified layout name we should generate
					// a modified layout to match. If something slips through the cracks, use the unmodified original
					// view in preference to a default view of Object.
					int index = origName.IndexOfAny(ktagMarkers);
					if (index > 0)
					{
						useName = origName.Substring(0, index);
						classId = clsid;
						continue;
					}
				}
				if (classId == 0 && useName != "default")
				{
					// Nothing found all the way to CmObject...try default layout.
					useName = "default";
					classId = clsid;
					continue; // try again with the main class, don't go to its base class at once.
				}
				if (classId == 0)
				{
					if (fIncludeLayouts)
					{
						// Really surprising...default view not found on CmObject??
						throw new ApplicationException("No matching layout found for class " + classname + " jtview layout " + origName);
					}
					// okay to not find specific custom parts...we can generate them.
					return null;
				}
				// Otherwise try superclass.
				classId = m_mdc.GetBaseClsId(classId);
			}
			m_map[key] = node; // find faster next time!
			return node;
		}

		/// <summary>
		/// Gets the layout inventory.
		/// </summary>
		/// <value>The layout inventory.</value>
		public Inventory LayoutInventory => m_layoutInventory;
	}

	/// <summary>
	/// An interface for lists that can switch between multiple lists of items.
	/// </summary>
	public interface IMultiListSortItemProvider : ISortItemProvider
	{
		/// <summary>
		/// A token to store with items returned from FindCorrespondingItemsInCurrentList()
		/// that can be passed back into that interface to help convert those
		/// items to the relatives in the current list (i.e.
		/// associated with a different ListSourceToken)
		/// </summary>
		object ListSourceToken { get; }

		/// <summary>
		/// The specification that can be used to create a PartOwnershipTree helper.
		/// </summary>
		XElement PartOwnershipTreeSpec { get;  }

		/// <summary>
		///
		/// </summary>
		/// <param name="itemAndListSourceTokenPairs"></param>
		/// <returns>a set of hvos of (non-sibling) items related to those given in itemAndListSourceTokenPairs</returns>
		void ConvertItemsToRelativesThatApplyToCurrentList(ref IDictionary<int, object> itemAndListSourceTokenPairs);
	}

	/// <summary>
	/// Describes the relationship between list classes in a PartOwnershipTree
	/// </summary>
	public enum RelationshipOfRelatives
	{
		/// <summary>
		/// Items (e.g. Entries) in the same ItemsListClass (LexEntry) are siblings.
		/// </summary>
		Sibling,
		/// <summary>
		/// (Includes Parent relationship)
		/// </summary>
		Ancestor,
		/// <summary>
		/// (Includes Child relationship)
		/// </summary>
		Descendent,
		/// <summary>
		/// Entries.Allomorphs and Entries.Senses are cousins.
		/// </summary>
		Cousin
	}

	/// <summary>
	/// Helper for handling switching between related (ListItemClass) lists.
	/// </summary>
	public class PartOwnershipTree : DisposableBase
	{
		LcmCache m_cache = null;
		XElement m_classOwnershipTree = null;
		XElement m_parentToChildrenSpecs = null;

		/// <summary>
		/// Factory for returning a PartOwnershipTree
		/// </summary>
		/// <param name="cache"></param>
		/// <param name="sortItemProvider"></param>
		/// <param name="fReturnFirstDecendentOnly"></param>
		/// <returns></returns>
		public static PartOwnershipTree Create(LcmCache cache, IMultiListSortItemProvider sortItemProvider, bool fReturnFirstDecendentOnly)
		{
			return new PartOwnershipTree(cache, sortItemProvider, fReturnFirstDecendentOnly);
		}

		/// <summary />
		private PartOwnershipTree(LcmCache cache, IMultiListSortItemProvider sortItemProvider, bool fReturnFirstDecendentOnly)
		{
			var partOwnershipTreeSpec = sortItemProvider.PartOwnershipTreeSpec;
			m_cache = cache;
			m_classOwnershipTree = partOwnershipTreeSpec.Element("ClassOwnershipTree");
			var parentClassPathsToChildren = partOwnershipTreeSpec.Element("ParentClassPathsToChildren");
			m_parentToChildrenSpecs = parentClassPathsToChildren.Clone();
			// now go through the seq specs and set the "firstOnly" to the requested value.
			var seqElements = m_parentToChildrenSpecs.Elements("part").Elements("seq");
			foreach (var xe in seqElements)
			{
				var xaFirstOnly = xe.Attribute("firstOnly");
				if (xaFirstOnly == null)
				{
					// Create the first only attribute, with no value (reset soon).
					xaFirstOnly = new XAttribute("firstOnly", string.Empty);
					xe.Add(xaFirstOnly);
				}
				xaFirstOnly.Value = fReturnFirstDecendentOnly.ToString().ToLowerInvariant();
			}
		}

		private LcmCache Cache
		{
			get { return m_cache; }
		}

		#region DisposableBase overrides
		/// <summary>
		///
		/// </summary>
		protected override void DisposeUnmanagedResources()
		{
			m_cache = null;
			m_classOwnershipTree = null;
			m_parentToChildrenSpecs = null;
		}
		#endregion DisposableBase overrides


		/// <summary>
		/// Get the field name that should be used for the main record list when we want to edit the specified field
		/// of the specified object class. TargetFieldId may be 0 to get the default (or only) main record list field
		/// for the specified class.
		/// </summary>
		public string GetSourceFieldName(int targetClsId, int targetFieldId)
		{
			var targetClassName = m_cache.DomainDataByFlid.MetaDataCache.GetClassName(targetClsId);
			var classNode = m_classOwnershipTree.Descendants(targetClassName).First();
			var flidName = XmlUtils.GetMandatoryAttributeValue(classNode, "sourceField");
			if (targetFieldId != 0)
			{
				var altSourceField = XmlUtils.GetOptionalAttributeValue(classNode, "altSourceField");
				if (altSourceField != null)
				{
					var targetFieldName = m_cache.MetaDataCacheAccessor.GetFieldName(targetFieldId);
					foreach (var option in altSourceField.Split(';'))
					{
						var parts = option.Split(':');
						if (parts.Length != 2)
							throw new FwConfigurationException("altSourceField must contain Field:SourceField;Field:SourceField...");
						if (parts[0].Trim() == targetFieldName)
						{
							flidName = parts[1].Trim();
							break;
						}
					}
				}
			}
			return flidName;
		}

		/// <summary>
		/// Map itemsBeforeListChange (associated with flidForItemsBeforeListChange)
		/// to those in the current list (associated with flidForCurrentList)
		/// and provide a set of common ancestors.
		/// </summary>
		/// <param name="flidForItemsBeforeListChange"></param>
		/// <param name="itemsBeforeListChange"></param>
		/// <param name="flidForCurrentList"></param>
		/// <param name="commonAncestors"></param>
		/// <returns></returns>
		public ISet<int> FindCorrespondingItemsInCurrentList(int flidForItemsBeforeListChange, ISet<int> itemsBeforeListChange, int flidForCurrentList, out ISet<int> commonAncestors)
		{
			var relatives = new HashSet<int>();
			commonAncestors = new HashSet<int>();
			int newListItemsClass = GhostParentHelper.GetBulkEditDestinationClass(Cache, flidForCurrentList);
			int prevListItemsClass = GhostParentHelper.GetBulkEditDestinationClass(Cache, flidForItemsBeforeListChange);
			RelationshipOfRelatives relationshipOfTarget = FindTreeRelationship(prevListItemsClass, newListItemsClass);
			// if new listListItemsClass is same as the given object, there's nothing more we have to do.
			switch (relationshipOfTarget)
			{
				case RelationshipOfRelatives.Sibling:
					{
						Debug.Fail("Sibling relationships are not supported.");
						// no use for this currently.
						break;
					}
				case RelationshipOfRelatives.Ancestor:
					{
						GhostParentHelper gph = GetGhostParentHelper(flidForItemsBeforeListChange);
						// the items (e.g. senses) are owned by the new class (e.g. entry),
						// so find the (new class) ancestor for each item.
						foreach (int hvoBeforeListChange in itemsBeforeListChange)
						{
							int hvoAncestorOfItem;
							if (gph != null && gph.GhostOwnerClass == newListItemsClass &&
								gph.IsGhostOwnerClass(hvoBeforeListChange))
							{
								// just add the ghost owner, as the ancestor relative,
								// since it's already in the newListItemsClass
								hvoAncestorOfItem = hvoBeforeListChange;
							}
							else
							{
								var obj =
									Cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(hvoBeforeListChange);
								hvoAncestorOfItem = obj.OwnerOfClass(newListItemsClass).Hvo;
							}
							relatives.Add(hvoAncestorOfItem);
						}
						commonAncestors = relatives;
						break;
					}
				case RelationshipOfRelatives.Descendent:
				case RelationshipOfRelatives.Cousin:
					{
						HashSet<int> newClasses =
							new HashSet<int>(((IFwMetaDataCacheManaged)Cache.MetaDataCacheAccessor).GetAllSubclasses(newListItemsClass));
						foreach (int hvoBeforeListChange in itemsBeforeListChange)
						{
							if (!Cache.ServiceLocator.IsValidObjectId(hvoBeforeListChange))
								continue; // skip this one.
							if (newClasses.Contains(Cache.ServiceLocator.GetObject(hvoBeforeListChange).ClassID))
							{
								// strangely, the 'before' object is ALREADY one that is valid for, and presumably in,
								// the destination property. One way this happens is at startup, when switching to
								// the saved target column, but we have also saved the list of objects.
								relatives.Add(hvoBeforeListChange);
								continue;
							}
							int hvoCommonAncestor;
							if (relationshipOfTarget == RelationshipOfRelatives.Descendent)
							{
								// the item is the ancestor
								hvoCommonAncestor = hvoBeforeListChange;
							}
							else
							{
								// the item and its cousins have a common ancestor.
								hvoCommonAncestor = GetHvoCommonAncestor(hvoBeforeListChange,
																		 prevListItemsClass, newListItemsClass);
							}

							// only add the descendants/cousins if we haven't already processed the ancestor.
							if (!commonAncestors.Contains(hvoCommonAncestor))
							{
								GhostParentHelper gph = GetGhostParentHelper(flidForCurrentList);
								ISet<int> descendents = GetDescendents(hvoCommonAncestor, flidForCurrentList);
								if (descendents.Count > 0)
								{
									relatives.UnionWith(descendents);
								}
								else if (gph != null && gph.IsGhostOwnerClass(hvoCommonAncestor))
								{
									relatives.Add(hvoCommonAncestor);
								}
								commonAncestors.Add(hvoCommonAncestor);
							}
						}
						break;
					}
			}
			return relatives;
		}

		private GhostParentHelper GetGhostParentHelper(int flidToTry)
		{
			return GhostParentHelper.CreateIfPossible(Cache.ServiceLocator, flidToTry);
		}

		private ISet<int> GetDescendents(int hvoCommonAncestor, int relativesFlid)
		{
			var listPropertyName = Cache.MetaDataCacheAccessor.GetFieldName(relativesFlid);
			var parentObjName = Cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(hvoCommonAncestor).ClassName;
			var xpathToPart = "./part[@id='" + parentObjName + "-Jt-" + listPropertyName + "']";
			var pathSpec = m_parentToChildrenSpecs.XPathSelectElement(xpathToPart);
			Debug.Assert(pathSpec != null, string.Format("You are experiencing a rare and difficult-to-reproduce error (LT- 11443 and linked issues). If you can add any information to the issue or fix it please do. If JohnT is available please call him over. Expected to find part ({0}) in ParentClassPathsToChildren", xpathToPart));
			if (pathSpec == null)
			{
				return new HashSet<int>(); // This just means we don't find a related object. Better than crashing, but not what we intend.
}
			// get the part spec that gives us the path from obsolete current (parent) list item object
			// to the new one.
			var vc = new XmlBrowseViewBaseVc(m_cache, null);
			var parentItem = new ManyOnePathSortItem(hvoCommonAncestor, null, null);
			var collector = new XmlBrowseViewBaseVc.ItemsCollectorEnv(null, m_cache, hvoCommonAncestor);
			var doc = XDocument.Load(pathSpec.ToString());
			vc.DisplayCell(parentItem, doc.Root.Elements().First(), hvoCommonAncestor, collector);
			if (collector.HvosCollectedInCell != null && collector.HvosCollectedInCell.Count > 0)
			{
				return new HashSet<int>(collector.HvosCollectedInCell);
			}
			return new HashSet<int>();
		}

		private RelationshipOfRelatives FindTreeRelationship(int prevListItemsClass, int newListItemsClass)
		{
			RelationshipOfRelatives relationshipOfTarget;
			if (DomainObjectServices.IsSameOrSubclassOf(Cache.DomainDataByFlid.MetaDataCache, prevListItemsClass, newListItemsClass))
			{
				relationshipOfTarget = RelationshipOfRelatives.Sibling;
			}
			else
			{
				// lookup new class in ownership tree and decide how to select the most related object
				var newClassName = Cache.DomainDataByFlid.MetaDataCache.GetClassName(newListItemsClass);
				var prevClassName = Cache.DomainDataByFlid.MetaDataCache.GetClassName(prevListItemsClass);
				var prevClassNode = m_classOwnershipTree.XPathSelectElement(".//" + prevClassName);
				var newClassNode = m_classOwnershipTree.XPathSelectElement(".//" + newClassName);
				// determine if prevClassName is owned (has anscestor) by the new.
				bool fNewIsAncestorOfPrev = prevClassNode.XPathSelectElement("ancestor::" + newClassName) != null;
				if (fNewIsAncestorOfPrev)
				{
					relationshipOfTarget = RelationshipOfRelatives.Ancestor;
				}
				else
				{
					// okay, now find most related object in new items list.
					bool fNewIsChildOfPrev = newClassNode.XPathSelectElement("ancestor::" + prevClassName) != null;
					if (fNewIsChildOfPrev)
					{
						relationshipOfTarget = RelationshipOfRelatives.Descendent;
					}
					else
					{
						relationshipOfTarget = RelationshipOfRelatives.Cousin;
					}
				}
			}
			return relationshipOfTarget;
		}

		private XElement GetTreeNode(int classId)
		{
			var className = Cache.DomainDataByFlid.MetaDataCache.GetClassName(classId);
			return m_classOwnershipTree.XPathSelectElement(".//" + className);
		}

		private int GetHvoCommonAncestor(int hvoBeforeListChange, int prevListItemsClass, int newListItemsClass)
		{
			var prevClassNode = GetTreeNode(prevListItemsClass);
			var newClassNode = GetTreeNode(newListItemsClass);
			int hvoCommonAncestor = 0;
			// NOTE: the class of hvoBeforeListChange could be different then prevListItemsClass, if the item is a ghost (owner).
			int classOfHvoBeforeListChange = Cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(hvoBeforeListChange).ClassID;
			// so go up the parent of the previous one until it's an ancestor of the newClass.
			var ancestorOfPrev = prevClassNode.Parent;
			while (ancestorOfPrev != null)
			{
				if (newClassNode.XPathSelectElement("ancestor::" + ancestorOfPrev.Name) != null)
				{
					var commonAncestor = ancestorOfPrev;
					var classCommonAncestor = Cache.MetaDataCacheAccessor.GetClassId(commonAncestor.Name.ToString());
					if (DomainObjectServices.IsSameOrSubclassOf(Cache.DomainDataByFlid.MetaDataCache, classOfHvoBeforeListChange, classCommonAncestor))
						hvoCommonAncestor = hvoBeforeListChange;
					else
					{
						var obj = Cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(hvoBeforeListChange);
						hvoCommonAncestor = obj.OwnerOfClass(classCommonAncestor).Hvo;
					}
					break;
				}
				ancestorOfPrev = ancestorOfPrev.Parent;
			}
			return hvoCommonAncestor;
		}
	}
}
