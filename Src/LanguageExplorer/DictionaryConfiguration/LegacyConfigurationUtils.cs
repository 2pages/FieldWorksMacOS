// Copyright (c) 2014-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using LanguageExplorer.Controls.XMLViews;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Core.Cellar;
using SIL.Xml;

namespace LanguageExplorer.DictionaryConfiguration
{
	/// <summary>
	/// This class holds methods which are used by legacy view configuration settings as well as migration of these
	/// configuration settings to new formats.
	/// <note>Most of these methods were moved here from the XmlDocConfigureDlg class</note>
	/// </summary>
	internal static class LegacyConfigurationUtils
	{
		internal static void BuildTreeFromLayoutAndParts(XElement configurationLayoutsNode, ILayoutConverter converter)
		{
			var layoutTypes = new List<XElement>();
			layoutTypes.AddRange(configurationLayoutsNode.Elements().Where(x => x.Name.LocalName == "layoutType"));
			Debug.Assert(layoutTypes.Count > 0);
			var xnConfig = layoutTypes[0].Element("configure");
			Debug.Assert(xnConfig != null);
			var configClass = XmlUtils.GetMandatoryAttributeValue(xnConfig, "class");
			layoutTypes.AddRange(converter.GetLayoutTypes().Where(xn => XmlUtils.GetMandatoryAttributeValue(xn.Element("configure"), "class") == configClass));
			foreach (var xnLayoutType in layoutTypes)
			{
				if (xnLayoutType.Name.LocalName != "layoutType")
				{
					continue;
				}
				if (XmlUtils.GetOptionalAttributeValue(xnLayoutType, "label") == "$wsName") // if the label for the layout matches $wsName then this is a reversal index layout
				{
					var sLayout = XmlUtils.GetOptionalAttributeValue(xnLayoutType, "layout");
					Debug.Assert(sLayout.EndsWith("-$ws"));
					var fReversalIndex = xnLayoutType.Elements()
						.Where(config => config.Name.LocalName == "configure")
						.Select(config => XmlUtils.GetOptionalAttributeValue(config, "class")).All(sClass => sClass == "ReversalIndexEntry");
					if (!fReversalIndex)
					{
						continue;
					}
					foreach (var ri in converter.Cache.LangProject.LexDbOA.CurrentReversalIndices)
					{
						var ws = converter.Cache.ServiceLocator.WritingSystemManager.Get(ri.WritingSystem);
						var sWsTag = ws.Id;
						converter.ExpandWsTaggedNodes(sWsTag);  // just in case we have a new index.
																// Create a copy of the layoutType node for the specific writing system.
						var xnRealLayout = CreateWsSpecficLayoutType(xnLayoutType, ws.DisplayLabel, sLayout.Replace("$ws", sWsTag), sWsTag);
						converter.AddDictionaryTypeItem(xnRealLayout, BuildLayoutTree(xnRealLayout, converter));
					}
				}
				else
				{
					var rgltnStyle = BuildLayoutTree(xnLayoutType, converter);
					if (rgltnStyle.Count > 0)
					{
						converter.AddDictionaryTypeItem(xnLayoutType, rgltnStyle);
					}
				}
			}
		}

		private static XElement CreateWsSpecficLayoutType(XElement xnLayoutType, string sWsLabel, string sWsLayout, string sWsTag)
		{
			var xnRealLayout = xnLayoutType.Clone();
			if (!xnRealLayout.HasAttributes)
			{
				return xnRealLayout;
			}
			xnRealLayout.Attribute("label").Value = sWsLabel;
			xnRealLayout.Attribute("layout").Value = sWsLayout;
			foreach (var config in xnRealLayout.Elements())
			{
				if (config.Name.LocalName != "configure")
				{
					continue;
				}
				var sInternalLayout = XmlUtils.GetOptionalAttributeValue(config, "layout");
				Debug.Assert(sInternalLayout.EndsWith("-$ws"));
				if (config.HasAttributes)
				{
					config.Attribute("layout").Value = sInternalLayout.Replace("$ws", sWsTag);
				}
			}
			return xnRealLayout;
		}

		/// <summary>
		/// Configure LayoutType via its child configure nodes
		/// </summary>
		internal static List<LayoutTreeNode> BuildLayoutTree(XElement xnLayoutType, ILayoutConverter converter)
		{
			var treeNodeList = new List<LayoutTreeNode>();
			foreach (var config in xnLayoutType.Elements())
			{
				// expects a configure element
				if (config.Name.LocalName != "configure")
				{
					continue;
				}
				var ltn = BuildMainLayout(config, converter);
				if (ltn != null)
				{
					if (XmlUtils.GetOptionalBooleanAttributeValue(config, "hideConfig", false))
					{
						treeNodeList.AddRange(ltn.Nodes.Cast<LayoutTreeNode>());
					}
					else
					{
						treeNodeList.Add(ltn);
					}
				}
			}
			return treeNodeList;
		}

		/// <summary>
		/// Builds control tree nodes based on a configure element
		/// </summary>
		private static LayoutTreeNode BuildMainLayout(XElement config, ILayoutConverter converter)
		{
			var mainLayoutNode = new LayoutTreeNode(config, converter, null);
			converter.SetOriginalIndexForNode(mainLayoutNode);
			var className = mainLayoutNode.ClassName;
			var layoutName = mainLayoutNode.LayoutName;
			var layout = converter.GetLayoutElement(className, layoutName);
			if (layout == null)
			{
				var msg = $"Cannot configure layout {layoutName} of class {className} because it does not exist";
				converter.LogConversionError(msg);
				return null;
			}
			mainLayoutNode.ParentLayout = layout;   // not really the parent layout, but the parent of this node's children
			var sVisible = XmlUtils.GetOptionalAttributeValue(layout, "visibility");
			mainLayoutNode.Checked = sVisible != "never";
			AddChildNodes(layout, mainLayoutNode, mainLayoutNode.Nodes.Count, converter);
			mainLayoutNode.OriginalNumberOfSubnodes = mainLayoutNode.Nodes.Count;
			return mainLayoutNode;
		}

		internal static void AddChildNodes(XElement layout, LayoutTreeNode ltnParent, int iStart, ILayoutConverter converter)
		{
			var fMerging = iStart < ltnParent.Nodes.Count;
			var className = XmlUtils.GetMandatoryAttributeValue(layout, "class");
			foreach (var node in PartGenerator.GetGeneratedChildren(layout, converter.Cache, new[] { "ref", "label" }))
			{
				switch (node.Name.LocalName)
				{
					case "sublayout":
					{
						Debug.Assert(!fMerging);
						var subLayoutName = XmlUtils.GetOptionalAttributeValue(node, "name", null);
						var subLayout = subLayoutName == null ? node : converter.GetLayoutElement(className, subLayoutName);
						if (subLayout != null)
						{
							AddChildNodes(subLayout, ltnParent, ltnParent.Nodes.Count, converter);
						}
						break;
					}
					default:
					{
						if (node.Name == "part")
						{
							// Check whether this node has already been added to this parent.  Don't add
							// it if it's already there!
							var ltnOld = FindMatchingNode(ltnParent, node);
							if (ltnOld != null)
							{
								continue;
							}
							var sRef = XmlUtils.GetMandatoryAttributeValue(node, "ref");
							var part = converter.GetPartElement(className, sRef);
							if (part == null && sRef != "$child")
							{
								continue;
							}
							var fHide = XmlUtils.GetOptionalBooleanAttributeValue(node, "hideConfig", false);
							LayoutTreeNode ltn;
							var cOrig = 0;
							if (!fHide)
							{
								ltn = new LayoutTreeNode(node, converter, className)
								{
									OriginalIndex = ltnParent.Nodes.Count,
									ParentLayout = layout,
									HiddenNode = converter.LayoutLevels.HiddenPartRef,
									HiddenNodeLayout = converter.LayoutLevels.HiddenLayout
								};
								if (!string.IsNullOrEmpty(ltn.LexRelType))
								{
									converter.BuildRelationTypeList(ltn);
								}

								if (!string.IsNullOrEmpty(ltn.EntryType))
								{
									converter.BuildEntryTypeList(ltn, ltnParent.LayoutName);
								}
								ltnParent.Nodes.Add(ltn);
							}
							else
							{
								Debug.Assert(!fMerging);
								ltn = ltnParent;
								cOrig = ltn.Nodes.Count;
								if (className == "StTxtPara")
								{
									ltnParent.HiddenChildLayout = layout;
									ltnParent.HiddenChild = node;
								}
							}
							try
							{
								converter.LayoutLevels.Push(node, layout);
								var fOldAdding = ltn.AddingSubnodes;
								ltn.AddingSubnodes = true;
								if (part != null)
								{
									ProcessChildNodes(part.Elements(), className, ltn, converter);
								}
								ltn.OriginalNumberOfSubnodes = ltn.Nodes.Count;
								ltn.AddingSubnodes = fOldAdding;
								if (!fHide)
								{
									continue;
								}
								var cNew = ltn.Nodes.Count - cOrig;
								if (cNew > 1)
								{
									var msg = $"{cNew} nodes for a hidden PartRef ({node.GetOuterXml()})!";
									converter.LogConversionError(msg);
								}
							}
							finally
							{
								converter.LayoutLevels.Pop();
							}
						}
						break;
					}
				}
			}
		}

		/// <summary>
		/// Walk the tree of child nodes, storing information for each &lt;obj&gt; or &lt;seq&gt;
		/// node.
		/// </summary>
		private static void ProcessChildNodes(IEnumerable<XElement> xmlNodeList, string className, LayoutTreeNode ltn, ILayoutConverter converter)
		{
			foreach (var xn in xmlNodeList)
			{
				if (xn.Name.LocalName == "obj" || xn.Name.LocalName == "seq" || xn.Name.LocalName == "objlocal")
				{
					StoreChildNodeInfo(xn, className, ltn, converter);
				}
				else
				{
					ProcessChildNodes(xn.Elements(), className, ltn, converter);
				}
			}
		}

		private static void StoreChildNodeInfo(XElement xn, string className, LayoutTreeNode ltn, ILayoutConverter converter)
		{
			var xnCaller = converter.LayoutLevels.PartRef ?? ltn.Configuration;
			// Insert any special configuration appropriate for this property...unless the caller is hidden, in which case,
			// we don't want to configure it at all.
			var sField = XmlUtils.GetMandatoryAttributeValue(xn, "field");
			if (!ltn.IsTopLevel && !(xnCaller != null && XmlUtils.GetOptionalBooleanAttributeValue(xnCaller, "hideConfig", false)))
			{
				switch (sField)
				{
					case "Senses" when ltn.ClassName == "LexEntry" || ltn.ClassName == "LexSense":
					case "ReferringSenses" when ltn.ClassName == "ReversalIndexEntry":
						ltn.ShowSenseConfig = true;
						break;
					case "MorphoSyntaxAnalysis" when ltn.ClassName == "LexSense":
						ltn.ShowGramInfoConfig = true;
						break;
					case "VisibleComplexFormBackRefs":
					case "ComplexFormsNotSubentries":
					{
						//The existence of the attribute is important for this setting, not its value!
						ltn.ShowComplexFormParaConfig = !string.IsNullOrEmpty(XmlUtils.GetOptionalAttributeValue(ltn.Configuration, "showasindentedpara"));
						break;
					}
				}
			}
			if (!XmlUtils.GetOptionalBooleanAttributeValue(ltn.Configuration, "recurseConfig", true))
			{
				// We don't want to recurse forever just because senses have subsenses, which
				// can have subsenses, which can ...
				// Or because entries have subentries (in root type layouts)...
				ltn.UseParentConfig = true;
				return;
			}
			var sLayout = XmlVc.GetLayoutName(xn, xnCaller);
			var clidDst = 0;
			string sClass = null;
			string sTargetClasses = null;
			try
			{
				// Failure should be fairly unusual, but, for example, part MoForm-Jt-FormEnvPub attempts to display
				// the property PhoneEnv inside an if that checks that the MoForm is one of the subclasses that has
				// the PhoneEnv property. MoForm itself does not.
				if (!converter.Cache.GetManagedMetaDataCache().FieldExists(className, sField, true))
				{
					return;
				}
				var flid = converter.Cache.DomainDataByFlid.MetaDataCache.GetFieldId(className, sField, true);
				var type = (CellarPropertyType)converter.Cache.DomainDataByFlid.MetaDataCache.GetFieldType(flid);
				Debug.Assert(type >= CellarPropertyType.MinObj);
				if (type >= CellarPropertyType.MinObj)
				{
					var mdc = converter.Cache.MetaDataCacheAccessor;
					sTargetClasses = XmlUtils.GetOptionalAttributeValue(xn, "targetclasses");
					clidDst = mdc.GetDstClsId(flid);
					sClass = clidDst == 0 ? XmlUtils.GetOptionalAttributeValue(xn, "targetclass") : mdc.GetClassName(clidDst);
					if (clidDst == StParaTags.kClassId)
					{
						var sClassT = XmlUtils.GetOptionalAttributeValue(xn, "targetclass");
						if (!string.IsNullOrEmpty(sClassT))
						{
							sClass = sClassT;
						}
					}
				}
			}
			catch
			{
				return;
			}
			if (clidDst == MoFormTags.kClassId && !sLayout.StartsWith("publi"))
			{
				return; // ignore the layouts used by the LexEntry-Jt-Headword part.
			}
			if (string.IsNullOrEmpty(sLayout) || string.IsNullOrEmpty(sClass))
			{
				return;
			}
			if (sTargetClasses == null)
			{
				sTargetClasses = sClass;
			}
			var rgsClasses = sTargetClasses.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			XElement subLayout = null;
			if (rgsClasses.Length > 0)
			{
				subLayout = converter.GetLayoutElement(rgsClasses[0], sLayout);
			}
			if (subLayout != null)
			{
				var iStart = ltn.Nodes.Count;
				var cNodes = subLayout.Elements().Count();
				AddChildNodes(subLayout, ltn, iStart, converter);
				var fRepeatedConfig = XmlUtils.GetOptionalBooleanAttributeValue(xn, "repeatedConfig", false);
				if (fRepeatedConfig)
				{
					return;     // repeats an earlier part element (probably as a result of <if>s)
				}
				for (var i = 1; i < rgsClasses.Length; i++)
				{
					var mergedLayout = converter.GetLayoutElement(rgsClasses[i], sLayout);
					if (mergedLayout != null && mergedLayout.Elements().Count() == cNodes)
					{
						AddChildNodes(mergedLayout, ltn, iStart, converter);
					}
				}
			}
			else
			{
				// The "layout" in a part node can actually refer directly to another part, so check
				// for that possibility.
				var subPart = converter.GetPartElement(rgsClasses[0], sLayout) ?? converter.GetPartElement(className, sLayout);
				if (subPart == null && !sLayout.EndsWith("-en"))
				{
					// Complain if we can't find either a layout or a part, and the name isn't tagged
					// for a writing system.  (We check only for English, being lazy.)
					converter.LogConversionError($"Missing jtview layout for class=\"{rgsClasses[0]}\" name=\"{sLayout}\"");
				}
			}
		}

		private static LayoutTreeNode FindMatchingNode(LayoutTreeNode ltn, XElement node)
		{
			if (ltn == null || node == null)
			{
				return null;
			}
			foreach (LayoutTreeNode ltnSub in ltn.Nodes)
			{
				if (NodesMatch(ltnSub.Configuration, node))
				{
					return ltnSub;
				}
			}
			return FindMatchingNode(ltn.Parent as LayoutTreeNode, node);
		}

		private static bool NodesMatch(XElement first, XElement second)
		{
			if (first.Name != second.Name)
			{
				return false;
			}
			if (!first.HasAttributes && (second.HasAttributes))
			{
				return false;
			}
			if (!first.HasAttributes)
			{
				return ChildNodesMatch(first.Elements().ToList(), second.Elements().ToList());
			}
			if (first.Attributes().Count() != second.Attributes().Count())
			{
				return false;
			}
			var firstAttSet = new SortedList<string, string>();
			var secondAttSet = new SortedList<string, string>();
			var firstAttributes = first.Attributes().ToList();
			var secondAttributes = second.Attributes().ToList();
			for (var i = 0; i < firstAttributes.Count; ++i)
			{
				firstAttSet.Add(firstAttributes[i].Name.LocalName, firstAttributes[i].Value);
				secondAttSet.Add(secondAttributes[i].Name.LocalName, secondAttributes[i].Value);
			}
			using (var firstIter = firstAttSet.GetEnumerator())
			using (var secondIter = secondAttSet.GetEnumerator())
			{
				for (; firstIter.MoveNext() && secondIter.MoveNext();)
				{
					if (!firstIter.Current.Equals(secondIter.Current))
					{
						return false;
					}
				}
				return true;
			}
		}

		/// <summary>
		/// This method should sort the node lists and call NodesMatch with each pair.
		/// </summary>
		private static bool ChildNodesMatch(IList<XElement> firstNodeList, IList<XElement> secondNodeList)
		{
			if (firstNodeList.Count != secondNodeList.Count)
			{
				return false;
			}
			var firstAtSet = new SortedList<string, XElement>();
			var secondAtSet = new SortedList<string, XElement>();
			for (var i = 0; i < firstNodeList.Count; ++i)
			{
				firstAtSet.Add(firstNodeList[i].Name.LocalName, firstNodeList[i]);
				secondAtSet.Add(secondNodeList[i].Name.LocalName, secondNodeList[i]);
			}
			using (var firstIter = firstAtSet.GetEnumerator())
			using (var secondIter = secondAtSet.GetEnumerator())
			{
				for (; firstIter.MoveNext() && secondIter.MoveNext();)
				{
					if (!NodesMatch(firstIter.Current.Value, secondIter.Current.Value))
					{
						return false;
					}
				}
				return true;
			}
		}
	}
}