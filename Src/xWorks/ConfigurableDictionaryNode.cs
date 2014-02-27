﻿// Copyright (c) 2014 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using SIL.FieldWorks.FDO;

namespace SIL.FieldWorks.XWorks
{
	/// <summary>
	/// This class allows configuring an element of dictionary data
	/// </summary>
	[XmlType (AnonymousType = false, TypeName = @"ConfigurationItem")]
	public class ConfigurableDictionaryNode
	{
		/// <summary>
		/// Information about the field in the model that this node is configuring
		/// </summary>
		[XmlAttribute(AttributeName = "identifier")]
		public string FieldDescription { get; set; }

		/// <summary>
		/// The style to apply to the data configured by this node
		/// </summary>
		[XmlAttribute(AttributeName = "style")]
		public string Style { get; set; }

		/// <summary>
		/// The label to display for this node
		/// </summary>
		[XmlAttribute(AttributeName = "name")]
		public string Label { get; set; }

		/// <summary>
		/// String to apply before the content configured by this node
		/// </summary>
		[XmlAttribute(AttributeName = "before")]
		public string Before { get; set; }

		/// <summary>
		/// String to apply after the content configured by this node
		/// </summary>
		[XmlAttribute(AttributeName = "after")]
		public string After { get; set; }

		/// <summary>
		/// String to apply between content items configured by this node (only applicable to lists)
		/// </summary>
		[XmlAttribute(AttributeName = "between")]
		public string Between { get; set; }

		/// <summary>
		/// Parent of this node, or null.
		/// </summary>
		[XmlIgnore]
		public ConfigurableDictionaryNode Parent { get; internal set; }

		/// <summary>
		/// Ordered list of nodes contained by this configurable node
		/// </summary>
		[XmlElement(ElementName = "ConfigurationItem")]
		public List<ConfigurableDictionaryNode> Children { get; set; }

		/// <summary>
		/// Type specific configuration options for this configurable node;
		/// </summary>
		public DictionaryNodeOptions DictionaryNodeOptions { get; set; }

		/// <summary>
		/// Whether this element of dictionary data is to shown as part of the dictionary.
		/// </summary>
		[XmlAttribute(AttributeName = "isEnabled")]
		public bool IsEnabled { get; set; }

		/// <summary>
		/// Whether this element of dictionary data was duplicated from another element of dictionary data.
		/// </summary>
		[XmlAttribute(AttributeName = "isDuplicate")]
		public bool IsDuplicate { get; set; }

		/// <summary>
		/// Clone this node. Point to the same Parent object. Deep-clone Children and DictionaryNodeOptions.
		/// </summary>
		internal ConfigurableDictionaryNode DeepCloneUnderSameParent()
		{
			var clone = new ConfigurableDictionaryNode();

			// Copy everything over at first, importantly handling strings, bools, and Parent.
			var properties = typeof (ConfigurableDictionaryNode).GetProperties();
			foreach (var property in properties)
			{
				var originalValue = property.GetValue(this, null);
				property.SetValue(clone, originalValue, null);
			}

			// Deep-clone Children
			if (Children != null)
			{
				var clonedChildren = new List<ConfigurableDictionaryNode>();
				foreach (var child in Children)
				{
					var clonedChild = child.DeepCloneUnderSameParent();
					// Cloned children should point to their newly-cloned parent
					clonedChild.Parent = clone;
					clonedChildren.Add(clonedChild);
				}
				clone.Children = clonedChildren;
			}

			// TODO: Deep-clone DictionaryNodeOptions

			return clone;
		}

		/// <summary>
		/// Duplicate this node and its Children, adding the result to the Parent's list of children.
		/// </summary>
		public ConfigurableDictionaryNode DuplicateAmongSiblings()
		{
			var duplicate = DeepCloneUnderSameParent();
			duplicate.IsDuplicate = true;
			Parent.Children.Add(duplicate);
			return duplicate;
		}
	}
}
