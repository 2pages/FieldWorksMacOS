// Copyright (c) 2003-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections;
using System.Diagnostics;
using System.Xml;
using SIL.Utils;

namespace XCore
{
	/// <summary>
	/// Summary description for List.
	/// </summary>
	public class List : ArrayList
	{
		/// -----------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="List"/> class.
		/// </summary>
		/// -----------------------------------------------------------------------------------
		public List(XmlNode configurationNode)
		{
			//it's not uncommon for this to be empty, if the list is being automatically generated by some
			//colleague in the system (e.g. AreaManager)
			if (configurationNode  == null)
				return;
			foreach(XmlNode node in configurationNode.SelectNodes("item"))
			{
				XmlNode parameterNode = null;
				if (node.ChildNodes.Count > 0)
					parameterNode = node.ChildNodes[0];
				Debug.Assert( node.ChildNodes.Count < 2, "Currently, only a single parameter node is allowed("+ configurationNode.OuterXml +")");
				Debug.Assert(XmlUtils.GetAttributeValue(node,"value") != null, "You must supply a value for every list item("+ configurationNode.OuterXml +")");
				string label = XmlUtils.GetLocalizedAttributeValue(node, "label", null);
				Add(label,
					XmlUtils.GetAttributeValue(node,"value"),
					XmlUtils.GetAttributeValue(node, "icon", "default"),
					parameterNode);
			}
		}

		public ListItem Add(string label, string value, string imageName, XmlNode parameterNode)
		{
			return Add(label, value, imageName, parameterNode, true);
		}

		public ListItem Add(string label, string value, string imageName, XmlNode parameterNode, bool enabled)
		{
			ListItem item = new ListItem();
			item.label = label;
			item.value = value;
			item.imageName = imageName;
			item.parameterNode = parameterNode;
			item.enabled = enabled;
			Add(item);
			return item;
		}

		public ListItem Insert(int index, string label, string value, string imageName, XmlNode parameterNode)
		{
			ListItem item = new ListItem();
			item.label = label;
			item.value = value;
			item.imageName = imageName;
			item.parameterNode = parameterNode;
			item.enabled = true;
			Insert(index, item);
			return item;
		}

	}

	/// <summary>
	/// JohnT: apparently a ListItem represents one item in a generated list of menu items;
	/// it may be used for other things as well. It is used to create a ListChoiceItem which
	/// in turn is used to create the real corresponding menu item.
	/// </summary>
	public class ListItem : IComparable
	{
		public string label;

		/// <summary>
		/// this would be better named "id", perhaps (JH)
		/// </summary>
		public string value;
		public XmlNode parameterNode;
		public string imageName;
		public bool enabled; // JohnT: specifies whether the menu item will be enabled.

		//// I (JH) added this thinking it would be useful but then realized that I would know how to
		//// actually get at this structure when I wanted to. The property merely changes to be the value of the list item.
		///// <summary>
		///// this can be any object at all, used by whoever actually wants to use this item.
		///// </summary>
		//public object tag;

		#region IComparable implementation
		/// <summary>
		/// Implement CompareTo so that the list can be sorted.
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public int CompareTo(Object obj)
		{
			ListItem li = (ListItem)obj;
			return label.CompareTo(li.label);
		}
		#endregion
	}

	/// <summary>
	/// Used to introduce a separator into a XCore.List
	/// </summary>
	public class SeparatorItem : ListItem
	{
	}

}
