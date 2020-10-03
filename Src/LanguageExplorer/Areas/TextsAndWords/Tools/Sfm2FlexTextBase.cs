// Copyright (c) 2012-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using LanguageExplorer.SfmToXml;
using SIL.LCModel.Core.WritingSystems;
using SilEncConverters40;

namespace LanguageExplorer.Areas.TextsAndWords.Tools
{
	internal class Sfm2FlexTextBase<TMapping> where TMapping : InterlinearMapping
	{
		protected Dictionary<string, TMapping> m_mappings = new Dictionary<string, TMapping>();
		protected EncConverters m_encConverters;
		protected ByteReader m_reader;
		protected WritingSystemManager m_wsManager;
		protected XmlWriter m_writer;
		protected List<string> m_openElements = new List<string>();
		protected string m_pendingMarker; // marker pushed back because not the extra one we were looking for
		protected byte[] m_pendingData; // data corresponding to m_pendingMarker.
		private IList<string> m_docStructure = new List<string>();

		internal Sfm2FlexTextBase(IList<string> docStructure)
		{
			m_docStructure = docStructure;
		}

		internal byte[] Convert(ByteReader reader, List<TMapping> mappings, WritingSystemManager wsManager)
		{
			m_reader = reader;
			m_wsManager = wsManager;
			using (var output = new MemoryStream())
			{
				using (m_writer = XmlWriter.Create(output, new XmlWriterSettings() { CloseOutput = true }))
				{
					WriteStartElement("document");
					foreach (var mapping in mappings)
					{
						m_mappings[mapping.Marker] = mapping;
					}
					while (GetNextSfmMarkerAndData(out var marker, out var data))
					{
						if (!m_mappings.TryGetValue(marker, out var mapping))
						{
							continue; // ignore any markers we don't know.
						}
						WriteToDocElement(data, mapping);
					}
					m_writer.Close();
				}
				var result = output.ToArray();
				if (Form.ActiveForm != null && (Control.ModifierKeys & Keys.Shift) == Keys.Shift)
				{
					// write out the intermediate file
					var intermediatePath = Path.Combine(Path.GetDirectoryName(reader.FileName), Path.GetFileNameWithoutExtension(reader.FileName) + "-intermediate.xml");
					using (var stream = File.Create(intermediatePath))
					{
						stream.Write(result, 0, result.Length);
						stream.Close();
					}
				}
				return result;
			}
		}

		protected virtual void WriteStartElement(string marker)
		{
			m_writer.WriteStartElement(marker);
			m_openElements.Add(marker);
		}

		protected void WriteEndElement()
		{
			m_writer.WriteEndElement();
			m_openElements.RemoveAt(m_openElements.Count - 1);
		}

		protected virtual void WriteToDocElement(byte[] data, TMapping mapping)
		{
			// override
		}

		private bool GetNextSfmMarkerAndData(out string marker, out byte[] data)
		{
			if (m_pendingMarker != null)
			{
				marker = m_pendingMarker;
				data = m_pendingData;
				m_pendingMarker = null;
				return true;
			}
			return GetRawData(out marker, out data);
		}

		protected bool GetRawData(out string marker, out byte[] data)
		{
			return m_reader.GetNextSfmMarkerAndData(out marker, out data, out _);
		}

		/// <summary>
		/// Make an item of the type indicated by the mapping by first converting the given data using the mapping's converter.
		/// It should have the specified item type and parent marker.
		/// If there are following consecutive fields with the same marker combine them into a single item.
		/// If a phrase is open and already has this type of element, ignore this one.
		/// Set phraseHasElement to indicate that now it does.
		/// </summary>
		protected virtual void MakeRepeatableItem(TMapping mapping, byte[] data, string itemType, string parentMarker, HashSet<Tuple<InterlinDestination, string>> itemsInThisParent)
		{
			var text = GetString(data, mapping).Trim();
			while (GetMoreData(mapping.Marker, out var moreData))
			{
				var moreText = GetString(moreData, mapping).Trim();
				if (string.IsNullOrEmpty(text))
				{
					text = moreText;
				}
				else
				{
					text += " " + moreText;
				}
			}
			var key = new Tuple<InterlinDestination, string>(mapping.Destination, mapping.WritingSystem);
			if (itemsInThisParent.Contains(key) && ParentElementIsOpen(parentMarker))
			{
				return;
			}
			itemsInThisParent.Add(key);
			MakeItem(mapping, text, itemType, parentMarker);
		}

		/// <summary>
		/// Read one more marker and data from the input. If it is the continuation marker, return true and the data;
		/// otherwise save the data to be read later and return false.
		/// </summary>
		private bool GetMoreData(string marker, out byte[] data)
		{
			if (!GetRawData(out var nextMarker, out data))
			{
				return false;
			}
			if (nextMarker == marker)
			{
				return true;
			}
			m_pendingData = data;
			m_pendingMarker = nextMarker;
			data = null;
			return false;
		}

		// True if a phrase is open (some child element may also be open)
		protected bool ParentElementIsOpen(string parent)
		{
			return m_openElements.Count > m_docStructure.IndexOf(parent);
		}

		protected void MakeItem(TMapping mapping, byte[] data, string itemType, string parentMarker)
		{
			MakeItem(mapping, GetString(data, mapping).Trim(), itemType, parentMarker);
		}

		protected void MakeItem(TMapping mapping, string text, string itemType, string parentMarker)
		{
			WriteStartElementIn("item", parentMarker);
			m_writer.WriteAttributeString("type", itemType);
			if (!string.IsNullOrEmpty(mapping.WritingSystem))
			{
				m_writer.WriteAttributeString("lang", mapping.WritingSystem);
			}
			m_writer.WriteString(text);
			WriteEndElement();
		}

		protected void WriteStartElementIn(string marker, string parentMarker)
		{
			AdjustDepth(parentMarker); //<-May initiate EndElement or StartElement calls as needed.
			WriteStartElement(marker);
		}

		protected string GetString(byte[] data, TMapping mapping)
		{
			if (string.IsNullOrEmpty(mapping.Converter))
			{
				return Encoding.UTF8.GetString(data); // todo: use encoding converter if present in mapping
			}
			if (m_encConverters == null)
			{
				m_encConverters = new EncConverters();
			}
			var converter = m_encConverters[mapping.Converter];
			return converter.ConvertToUnicode(data);
		}

		/// <summary>
		/// Do whatever start or end element calls are needed so that we are at the level where the current
		/// open element is parentMarker
		/// <note>This implementation precludes any element name re-use in the output format,
		/// i.e. a phrase element could not have a phrase element nested in it, or in any of its children</note>
		/// </summary>
		private void AdjustDepth(string parentMarker)
		{
			var depth = m_docStructure.IndexOf(parentMarker) + 1;
			while (m_openElements.Count > depth)
			{
				WriteEndElement();
			}
			while (m_openElements.Count < depth)
			{
				WriteStartElement(m_docStructure[m_openElements.Count]);
			}
		}

		/// <summary />
		protected virtual void MakeRootItem(TMapping mapping, byte[] data, string itemType)
		{
			// override
		}
	}
}