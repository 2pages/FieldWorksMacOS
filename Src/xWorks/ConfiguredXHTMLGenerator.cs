﻿// Copyright (c) 2014-2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.Common.Controls;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.DomainServices;
using SIL.Utils;
using XCore;

namespace SIL.FieldWorks.XWorks
{
	/// <summary>
	/// This class groups the static methods used for generating XHTML, according to specified configurations, from Fieldworks model objects
	/// </summary>
	public static class ConfiguredXHTMLGenerator
	{
		/// <summary>
		/// The Assembly that the model Types should be loaded from. Allows test code to introduce a test model.
		/// </summary>
		internal static string AssemblyFile { get; set; }

		/// <summary>
		/// Map of the Assembly to the file name, so that different tests can use different models
		/// </summary>
		internal static Dictionary<string, Assembly> AssemblyMap = new Dictionary<string, Assembly>();

		private const string PublicIdentifier = @"-//W3C//DTD XHTML 1.1//EN";

		/// <summary>
		/// Static initializer setting the AssemblyFile to the default Fieldworks model dll.
		/// </summary>
		static ConfiguredXHTMLGenerator()
		{
			AssemblyFile = "FDO";
		}

		/// <summary>
		/// Generates self-contained XHTML for a single entry for, eg, the preview panes in Lexicon Edit and the Dictionary Config dialog
		/// </summary>
		/// <returns>The HTML as a string</returns>
		public static string GenerateEntryHtmlWithStyles(ICmObject entry, DictionaryConfigurationModel configuration,
																		 DictionaryPublicationDecorator pubDecorator, Mediator mediator)
		{
			if (entry == null)
			{
				throw new ArgumentNullException("entry");
			}
			if (pubDecorator == null)
			{
				throw new ArgumentException("pubDecorator");
			}
			var projectPath = DictionaryConfigurationListener.GetProjectConfigurationDirectory(mediator);
			var previewCssPath = Path.Combine(projectPath, "Preview.css");
			var stringBuilder = new StringBuilder();
			using (var writer = XmlWriter.Create(stringBuilder))
			using (var cssWriter = new StreamWriter(previewCssPath, false))
			{
				var exportSettings = new GeneratorSettings((FdoCache)mediator.PropertyTable.GetValue("cache"), mediator, writer, false, false, null);
				GenerateOpeningHtml(previewCssPath, exportSettings);
				GenerateXHTMLForEntry(entry, configuration, pubDecorator, exportSettings);
				GenerateClosingHtml(writer);
				writer.Flush();
				cssWriter.Write(CssGenerator.GenerateCssFromConfiguration(configuration, mediator));
				cssWriter.Flush();
			}

			return stringBuilder.ToString();
		}

		[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
			Justification = "xhtmlWriter is a reference")]
		private static void GenerateOpeningHtml(string cssPath, GeneratorSettings exportSettings)
		{
			var xhtmlWriter = exportSettings.Writer;

			xhtmlWriter.WriteDocType("html", PublicIdentifier, null, null);
			xhtmlWriter.WriteStartElement("html", "http://www.w3.org/1999/xhtml");
			xhtmlWriter.WriteAttributeString("lang", "utf-8");
			xhtmlWriter.WriteStartElement("head");
			xhtmlWriter.WriteStartElement("link");
			xhtmlWriter.WriteAttributeString("href", "file:///" + cssPath);
			xhtmlWriter.WriteAttributeString("rel", "stylesheet");
			xhtmlWriter.WriteEndElement(); //</link>
			// write out schema links for writing system metadata
			xhtmlWriter.WriteStartElement("link");
			xhtmlWriter.WriteAttributeString("href", "http://purl.org/dc/terms/");
			xhtmlWriter.WriteAttributeString("rel", "schema.DCTERMS");
			xhtmlWriter.WriteEndElement(); //</link>
			xhtmlWriter.WriteStartElement("link");
			xhtmlWriter.WriteAttributeString("href", "http://purl.org/dc/elements/1.1/");
			xhtmlWriter.WriteAttributeString("rel", "schema.DC");
			xhtmlWriter.WriteEndElement(); //</link>
			GenerateWritingSystemsMetadata(exportSettings);
			xhtmlWriter.WriteEndElement(); //</head>
			xhtmlWriter.WriteStartElement("body");
		}

		private static void GenerateWritingSystemsMetadata(GeneratorSettings exportSettings)
		{
			var xhtmlWriter = exportSettings.Writer;
			var lp = exportSettings.Cache.LangProject;
			var wsList = lp.CurrentAnalysisWritingSystems.Union(lp.CurrentVernacularWritingSystems.Union(lp.CurrentPronunciationWritingSystems));
			foreach (var ws in wsList)
			{
				xhtmlWriter.WriteStartElement("meta");
				xhtmlWriter.WriteAttributeString("name", "DC.language");
				xhtmlWriter.WriteAttributeString("content", String.Format("{0}:{1}", ws.RFC5646, ws.LanguageName));
				xhtmlWriter.WriteAttributeString("scheme", "DCTERMS.RFC5646");
				xhtmlWriter.WriteEndElement();
				xhtmlWriter.WriteStartElement("meta");
				xhtmlWriter.WriteAttributeString("name", ws.RFC5646);
				xhtmlWriter.WriteAttributeString("content", ws.DefaultFontName);
				xhtmlWriter.WriteAttributeString("scheme", "language to font");
				xhtmlWriter.WriteEndElement();
			}
		}

		private static void GenerateClosingHtml(XmlWriter xhtmlWriter)
		{
			xhtmlWriter.WriteEndElement(); //</body>
			xhtmlWriter.WriteEndElement(); //</html>
		}

		/// <summary>
		/// Saves the generated content into the given xhtml and css file paths for all the entries in
		/// the given collection.
		/// </summary>
		public static void SavePublishedHtmlWithStyles(IEnumerable<int> entryHvos, DictionaryPublicationDecorator publicationDecorator, DictionaryConfigurationModel configuration, Mediator mediator, string xhtmlPath, string cssPath, IThreadedProgress progress = null)
		{
			var cache = (FdoCache)mediator.PropertyTable.GetValue("cache");
			using (var xhtmlWriter = XmlWriter.Create(xhtmlPath))
			using (var cssWriter = new StreamWriter(cssPath, false))
			{
				var settings = new GeneratorSettings(cache, mediator, xhtmlWriter, true, true, Path.GetDirectoryName(xhtmlPath));
				GenerateOpeningHtml(cssPath, settings);
				string lastHeader = null;
				foreach (var hvo in entryHvos)
				{
					var entry = cache.ServiceLocator.GetObject(hvo);
					// TODO pH 2014.08: generate only if entry is published (confignode enabled, pubAsMinor, selected complex- or variant-form type)
					GenerateLetterHeaderIfNeeded(entry, ref lastHeader, xhtmlWriter, cache);
					GenerateXHTMLForEntry(entry, configuration, publicationDecorator, settings);
					if (progress != null)
					{
						progress.Position++;
					}
				}
				GenerateClosingHtml(xhtmlWriter);
				xhtmlWriter.Flush();
				cssWriter.Write(CssGenerator.GenerateLetterHeaderCss(mediator));
				cssWriter.Write(CssGenerator.GenerateCssFromConfiguration(configuration, mediator));
				cssWriter.Flush();
			}
		}

		internal static void GenerateLetterHeaderIfNeeded(ICmObject entry, ref string lastHeader, XmlWriter xhtmlWriter, FdoCache cache)
		{

			// If performance is an issue these dummy's can be stored between calls
			var dummyOne = new Dictionary<string, Set<string>>();
			var dummyTwo = new Dictionary<string, Dictionary<string, string>>();
			var dummyThree = new Dictionary<string, Set<string>>();
			var wsString = cache.WritingSystemFactory.GetStrFromWs(cache.DefaultVernWs);
			var firstLetter = ConfiguredExport.GetLeadChar(GetLetHeadbyEntryType(entry), wsString,
																		  dummyOne, dummyTwo, dummyThree, cache);
			if (firstLetter != lastHeader && !String.IsNullOrEmpty(firstLetter))
			{
				var headerTextBuilder = new StringBuilder();
				headerTextBuilder.Append(Icu.ToTitle(firstLetter, wsString));
				headerTextBuilder.Append(' ');
				headerTextBuilder.Append(firstLetter.Normalize());

				xhtmlWriter.WriteStartElement("div");
				xhtmlWriter.WriteAttributeString("class", "letHead");
				xhtmlWriter.WriteStartElement("div");
				xhtmlWriter.WriteAttributeString("class", "letter");
				xhtmlWriter.WriteString(headerTextBuilder.ToString());
				xhtmlWriter.WriteEndElement();
				xhtmlWriter.WriteEndElement();

				lastHeader = firstLetter;
			}
		}

		/// <summary>
		/// To generating the letter headers, we need to know which type the entry is to determine to check the first character.
		/// So, this method will find the correct type by casting the entry with ILexEntry and IReversalIndexEntry
		/// </summary>
		/// <param name="entry">entry which needs to find the type</param>
		/// <returns>letHead text</returns>
		private static string GetLetHeadbyEntryType(ICmObject entry)
		{
			var lexEntry = entry as ILexEntry;
			if (lexEntry == null)
			{
				var revEntry = entry as IReversalIndexEntry;
				return revEntry != null ? revEntry.ReversalForm.BestAnalysisAlternative.Text : string.Empty;
			}
			return lexEntry.HomographForm;
		}

		/// <summary>
		/// Generating the xhtml representation for the given ICmObject using the given configuration node to select which data to write out
		/// If it is a Dictionary Main Entry or non-Dictionary entry, uses the first configuration node.
		/// If it is a Minor Entry, first checks whether the entry should be published as a Minor Entry; then, generates XHTML for each applicable
		/// Minor Entry configuration node.
		/// </summary>
		public static void GenerateXHTMLForEntry(ICmObject entry, DictionaryConfigurationModel configuration,
			DictionaryPublicationDecorator publicationDecorator, GeneratorSettings settings)
		{
			if (IsMinorEntry(entry))
			{
				if (((ILexEntry)entry).PublishAsMinorEntry)
				{
					for (var i = 1; i < configuration.Parts.Count; i++)
					{
						if (IsListItemSelectedForExport(configuration.Parts[i], entry, null))
						{
							GenerateXHTMLForEntry(entry, configuration.Parts[i], publicationDecorator, settings);
						}
					}
				}
			}
			else
			{
				GenerateXHTMLForEntry(entry, configuration.Parts[0], publicationDecorator, settings);
			}
		}

		/// <summary>
		/// If entry might be a minor entry. Sometimes returns true when the entry is not a minor entry.
		/// </summary>
		internal static bool IsMinorEntry(ICmObject entry)
		{
			// owning an ILexEntryRef denotes a minor entry (Complex* or Variant Form)
			return entry is ILexEntry && ((ILexEntry)entry).EntryRefsOS.Any();
			// TODO pH 2014.08: *Owning a LexEntryRef denotes a minor entry only in those configs that display complex forms as subentries
			// TODO				(Root, Bart?, and their descendants) or if the reftype is Variant Form
		}

		/// <summary>Generates XHTML for an ICmObject for a specific ConfigurableDictionaryNode</summary>
		/// <param name="configuration"><remarks>this configuration node must match the entry type</remarks></param>
		internal static void GenerateXHTMLForEntry(ICmObject entry, ConfigurableDictionaryNode configuration, DictionaryPublicationDecorator publicationDecorator, GeneratorSettings settings)
		{
			if (settings == null || entry == null || configuration == null)
			{
				throw new ArgumentNullException();
			}
			if (String.IsNullOrEmpty(configuration.FieldDescription))
			{
				throw new ArgumentException(@"Invalid configuration: FieldDescription can not be null", @"configuration");
			}
			if (entry.ClassID != settings.Cache.MetaDataCacheAccessor.GetClassId(configuration.FieldDescription))
			{
				throw new ArgumentException(@"The given argument doesn't configure this type", @"configuration");
			}
			if (!configuration.IsEnabled)
			{
				return;
			}

			settings.Writer.WriteStartElement("div");
			WriteClassNameAttribute(settings.Writer, configuration);
			settings.Writer.WriteAttributeString("id", "hvo" + entry.Hvo);
			foreach (var config in configuration.Children)
			{
				GenerateXHTMLForFieldByReflection(entry, config, publicationDecorator, settings);
				if (config.CheckForParaNodesEnabled(config) && !config.CheckForPrevParaNodeSibling(config))
				{
					settings.Writer.WriteStartElement("div");
					settings.Writer.WriteAttributeString("class", "paracontinuation");
				}
			}
			if (configuration.Children.Any(x => x.CheckForParaNodesEnabled(x)))
			{
				settings.Writer.WriteEndElement();
			}
			settings.Writer.WriteEndElement(); // </div>
		}

		/// <summary>
		/// This method will write out the class name attribute into the xhtml for the given configuration node
		/// taking into account the current information in ClassNameOverrides
		/// </summary>
		/// <param name="writer"></param>
		/// <param name="configNode">used to look up any mapping overrides</param>
		private static void WriteClassNameAttribute(XmlWriter writer, ConfigurableDictionaryNode configNode)
		{
			writer.WriteAttributeString("class", CssGenerator.GetClassAttributeForConfig(configNode));
		}

		/// <summary>
		/// This method will use reflection to pull data out of the given object based on the given configuration and
		/// write out appropriate XHTML.
		/// </summary>
		private static void GenerateXHTMLForFieldByReflection(object field, ConfigurableDictionaryNode config, DictionaryPublicationDecorator publicationDecorator, GeneratorSettings settings)
		{
			if (!config.IsEnabled)
			{
				return;
			}
			var cache = settings.Cache;
			var entryType = field.GetType();
			object propertyValue = null;
			if (config.IsCustomField)
			{
				var customFieldOwnerClassName = GetClassNameForCustomFieldParent(config, settings.Cache);
				int customFieldFlid;
				customFieldFlid = GetCustomFieldFlid(config, cache, customFieldOwnerClassName);
				if (customFieldFlid != 0)
				{
					var customFieldType = cache.MetaDataCacheAccessor.GetFieldType(customFieldFlid);
					switch (customFieldType)
					{
						case (int)CellarPropertyType.ReferenceCollection:
						case (int)CellarPropertyType.OwningCollection:
							// Collections are stored essentially the same as sequences.
						case (int)CellarPropertyType.ReferenceSequence:
						case (int)CellarPropertyType.OwningSequence:
							{
								var sda = cache.MainCacheAccessor;
								// This method returns the hvo of the object pointed to
								var chvo = sda.get_VecSize(((ICmObject)field).Hvo, customFieldFlid);
								int[] contents;
								using (var arrayPtr = MarshalEx.ArrayToNative<int>(chvo))
								{
									sda.VecProp(((ICmObject)field).Hvo, customFieldFlid, chvo, out chvo, arrayPtr);
									contents = MarshalEx.NativeToArray<int>(arrayPtr, chvo);
								}
								// if the hvo is invalid set propertyValue to null otherwise get the object
								propertyValue = contents.Select(id => cache.LangProject.Services.GetObject(id));
								break;
							}
						case (int)CellarPropertyType.ReferenceAtomic:
						case (int)CellarPropertyType.OwningAtomic:
							{
								// This method returns the hvo of the object pointed to
								propertyValue = cache.MainCacheAccessor.get_ObjectProp(((ICmObject)field).Hvo, customFieldFlid);
								// if the hvo is invalid set propertyValue to null otherwise get the object
								propertyValue = (int)propertyValue > 0 ? cache.LangProject.Services.GetObject((int)propertyValue) : null;
								break;
							}
						case (int)CellarPropertyType.GenDate:
							{
								propertyValue = new GenDate(cache.MainCacheAccessor.get_IntProp(((ICmObject) field).Hvo, customFieldFlid));
								break;
							}

						case (int)CellarPropertyType.Time:
							{
								propertyValue = SilTime.ConvertFromSilTime(cache.MainCacheAccessor.get_TimeProp(((ICmObject)field).Hvo, customFieldFlid));
								break;
							}
						case (int)CellarPropertyType.MultiUnicode:
						case (int)CellarPropertyType.MultiString:
							{
								propertyValue = cache.MainCacheAccessor.get_MultiStringProp(((ICmObject)field).Hvo, customFieldFlid);
								break;
							}
						case (int)CellarPropertyType.String:
							{
								propertyValue = cache.MainCacheAccessor.get_StringProp(((ICmObject)field).Hvo, customFieldFlid);
								break;
							}
					}
				}
			}
			else
			{
				var property = entryType.GetProperty(config.FieldDescription);
				if (property == null)
				{
					Debug.WriteLine(String.Format("Issue with finding {0} for {1}", config.FieldDescription, entryType));
					return;
				}
				propertyValue = property.GetValue(field, new object[] { });
			}
			// If the property value is null there is nothing to generate
			if (propertyValue == null)
			{
				return;
			}
			if (!String.IsNullOrEmpty(config.SubField))
			{
				var subType = propertyValue.GetType();
				var subProp = subType.GetProperty(config.SubField);
				if (subProp == null)
				{
					Debug.WriteLine(String.Format("Issue with finding {0} for {1}", config.SubField, subType));
					return;
				}
				propertyValue = subProp.GetValue(propertyValue, new object[] { });
				// If the property value is null there is nothing to generate
				if (propertyValue == null)
					return;
			}
			var typeForNode = config.IsCustomField
										? GetPropertyTypeFromReflectedTypes(propertyValue.GetType(), null)
										: GetPropertyTypeForConfigurationNode(config, cache);
			switch (typeForNode)
			{
				case (PropertyType.CollectionType):
					{
						if (!IsCollectionEmpty(propertyValue))
						{
							GenerateXHTMLForCollection(propertyValue, config, publicationDecorator, field, settings);
						}
						return;
					}
				case (PropertyType.MoFormType):
					{
						GenerateXHTMLForMoForm(propertyValue as IMoForm, config, settings);
						return;
					}
				case (PropertyType.CmObjectType):
					{
						GenerateXHTMLForICmObject(propertyValue as ICmObject, config, settings);
						return;
					}
				case (PropertyType.CmPictureType):
					{
						var fileProperty = propertyValue as ICmFile;
						if (fileProperty != null)
						{
							GenerateXHTMLForPicture(fileProperty, config, settings);
						}
						else
						{
							GenerateXHTMLForPictureCaption(propertyValue, config, settings);
						}
						return;
					}
				case (PropertyType.CmPossibility):
					{
						GenerateXHTMLForPossibility(propertyValue, config, publicationDecorator, settings);
						return;
					}
				case (PropertyType.CmFileType):
					{
						var fileProperty = propertyValue as ICmFile;
						if (fileProperty != null)
						{
							var audioId = "hvo" + fileProperty.Hvo;
							GenerateXHTMLForAudioFile(fileProperty.ClassName, settings.Writer, audioId,
								GenerateSrcAttributeFromFilePath(fileProperty, settings.UseRelativePaths ? "AudioVisual" : null, settings), "\u25B6");
						}
						return;
					}
				default:
					{
						GenerateXHTMLForValue(field, propertyValue, config, settings);
						break;
					}
			}

			if (config.Children != null)
			{
				foreach (var child in config.Children)
				{
					GenerateXHTMLForFieldByReflection(propertyValue, child, publicationDecorator, settings);
				}
			}
		}

		/// <summary/>
		/// <returns>Returns the flid of the custom field identified by the configuration nodes FieldDescription
		/// in the class identified by <code>customFieldOwnerClassName</code></returns>
		private static int GetCustomFieldFlid(ConfigurableDictionaryNode config, FdoCache cache,
														  string customFieldOwnerClassName)
		{
			int customFieldFlid;
			try
			{
				customFieldFlid = cache.MetaDataCacheAccessor.GetFieldId(customFieldOwnerClassName,
																							config.FieldDescription, false);
			}
			catch (FDOInvalidFieldException)
			{
				var usefulMessage =
					String.Format(
						"The custom field {0} could not be found in the class {1} for the node labelled {2}",
						config.FieldDescription, customFieldOwnerClassName, config.Parent.Label);
				throw new ArgumentException(usefulMessage, "config");
			}
			return customFieldFlid;
		}

		/// <summary>
		/// This method will return the string representing the class name for the parent
		/// node of a configuration item representing a custom field.
		/// </summary>
		private static string GetClassNameForCustomFieldParent(ConfigurableDictionaryNode customFieldNode, FdoCache cache)
		{
			Type unneeded;
			// If the parent node of the custom field represents a collection, calling GetTypeForConfigurationNode
			// with the parent node returns the collection type. We want the type of the elements in the collection.
			var parentNodeType = GetTypeForConfigurationNode(customFieldNode.Parent, cache, out unneeded);
			if (IsCollectionType(parentNodeType))
			{
				parentNodeType = parentNodeType.GetGenericArguments()[0];
			}
			if (parentNodeType.IsInterface)
			{
				// Strip off the interface designation since custom fields are added to concrete classes
				return parentNodeType.Name.Substring(1);
			}
			return parentNodeType.Name;
		}

		[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
			Justification = "writer is a reference")]
		private static void GenerateXHTMLForPossibility(object propertyValue, ConfigurableDictionaryNode config,
			DictionaryPublicationDecorator publicationDecorator, GeneratorSettings settings)
		{
			var writer = settings.Writer;
			if (config.Children.Any(node => node.IsEnabled))
			{
				writer.WriteStartElement("span");
				writer.WriteAttributeString("class", CssGenerator.GetClassAttributeForConfig(config));
				if (config.Children != null)
				{
					foreach (var child in config.Children)
					{
						GenerateXHTMLForFieldByReflection(propertyValue, child, publicationDecorator, settings);
					}
				}
				writer.WriteEndElement();
			}
		}

		[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
			Justification = "writer is a reference")]
		private static void GenerateXHTMLForPictureCaption(object propertyValue, ConfigurableDictionaryNode config, GeneratorSettings settings)
		{
			var writer = settings.Writer;
			writer.WriteStartElement("div");
			writer.WriteAttributeString("class", CssGenerator.GetClassAttributeForConfig(config));
			// todo: get sense numbers and captions into the same div and get rid of this if else
			if (config.DictionaryNodeOptions != null)
			{
				GenerateXHTMLForStrings(propertyValue as IMultiString, config, settings);
			}
			else
			{
				GenerateXHTMLForString(propertyValue as ITsString, config, settings);
			}
			writer.WriteEndElement();
		}

		[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
			Justification = "writer is a reference")]
		private static void GenerateXHTMLForPicture(ICmFile pictureFile, ConfigurableDictionaryNode config, GeneratorSettings settings)
		{
			var writer = settings.Writer;
			writer.WriteStartElement("img");
			writer.WriteAttributeString("class", CssGenerator.GetClassAttributeForConfig(config));
			var srcAttribute = GenerateSrcAttributeFromFilePath(pictureFile, settings.UseRelativePaths ? "pictures" : null, settings);
			writer.WriteAttributeString("src", srcAttribute);
			writer.WriteAttributeString("id", "hvo" + pictureFile.Hvo);
			writer.WriteEndElement();
		}
		/// <summary>
		/// This method will generate a src attribute which will point to the given file from the xhtml.
		/// </summary>
		/// <para name="subfolder">If not null the path generated will be a relative path with the file in subfolder</para>
		private static string GenerateSrcAttributeFromFilePath(ICmFile file, string subFolder, GeneratorSettings settings)
		{
			string filePath;
			if (settings.UseRelativePaths && subFolder != null)
			{
				filePath = Path.Combine(subFolder, Path.GetFileName(MakeSafeFilePath(file.InternalPath)));
				if (settings.CopyFiles)
				{
					FileUtils.EnsureDirectoryExists(Path.Combine(settings.ExportPath, subFolder));
					var destination = Path.Combine(settings.ExportPath, filePath);
					var source = MakeSafeFilePath(file.AbsoluteInternalPath);
					if (!File.Exists(destination))
					{
						if (File.Exists(source))
						{
							FileUtils.Copy(source, destination);
						}
					}
					else if (!FileUtils.AreFilesIdentical(source, destination))
					{
						var fileWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
						var fileExtension = Path.GetExtension(filePath);
						var copyNumber = 0;
						do
						{
							++copyNumber;
							destination = Path.Combine(settings.ExportPath, subFolder, String.Format("{0}{1}{2}", fileWithoutExtension, copyNumber, fileExtension));
						}
						while (File.Exists(destination));
						if (File.Exists(source))
						{
							FileUtils.Copy(source, destination);
						}
						// Change the filepath to point to the copied file
						filePath = Path.Combine(subFolder, String.Format("{0}{1}{2}", fileWithoutExtension, copyNumber, fileExtension));
					}
				}
			}
			else
			{
				filePath = MakeSafeFilePath(file.AbsoluteInternalPath);
			}
			return settings.UseRelativePaths ? filePath : new Uri(filePath).ToString();
		}
		private static string GenerateSrcAttributeForAudioFromFilePath(string filename, string subFolder, GeneratorSettings settings)
		{
			string filePath;
			var linkedFilesRootDir = settings.Cache.LangProject.LinkedFilesRootDir;
			var audioVisualFile = Path.Combine(linkedFilesRootDir, subFolder, filename);
			if (settings.UseRelativePaths && subFolder != null)
			{
				filePath = Path.Combine(subFolder, Path.GetFileName(MakeSafeFilePath(filename)));
				if (settings.CopyFiles)
				{
					FileUtils.EnsureDirectoryExists(Path.Combine(settings.ExportPath, subFolder));
					var destination = Path.Combine(settings.ExportPath, filePath);
					var source = MakeSafeFilePath(audioVisualFile);
					if (!File.Exists(destination))
					{
						if (File.Exists(source))
						{
							FileUtils.Copy(source, destination);
						}
					}
					else if (!FileUtils.AreFilesIdentical(source, destination))
					{
						var fileWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
						var fileExtension = Path.GetExtension(filePath);
						var copyNumber = 0;
						do
						{
							++copyNumber;
							destination = Path.Combine(settings.ExportPath, subFolder, String.Format("{0}{1}{2}", fileWithoutExtension, copyNumber, fileExtension));
						}
						while (File.Exists(destination));
						if (File.Exists(source))
						{
							FileUtils.Copy(source, destination);
						}
						// Change the filepath to point to the copied file
						filePath = Path.Combine(subFolder, String.Format("{0}{1}{2}", fileWithoutExtension, copyNumber, fileExtension));
					}
				}
			}
			else
			{
				filePath = MakeSafeFilePath(audioVisualFile);
			}
			return settings.UseRelativePaths ? filePath : new Uri(filePath).ToString();
		}
		private static string MakeSafeFilePath(string filePath)
		{
			if (Unicode.CheckForNonAsciiCharacters(filePath))
			{
				// Flex keeps the filename as NFD in memory because it is unicode. We need NFC to actually link to the file
				filePath = Icu.Normalize(filePath, Icu.UNormalizationMode.UNORM_NFC);
			}
			return filePath;
		}

		internal enum PropertyType
		{
			CollectionType,
			MoFormType,
			CmObjectType,
			CmPictureType,
			CmFileType,
			CmPossibility,
			PrimitiveType,
			InvalidProperty
		}

		private static Dictionary<ConfigurableDictionaryNode, PropertyType> _configNodeToTypeMap = new Dictionary<ConfigurableDictionaryNode, PropertyType>();

		/// <summary>
		/// This method will reflectively return the type that represents the given configuration node as
		/// described by the ancestry and FieldDescription and SubField properties of each node in it.
		/// </summary>
		/// <returns></returns>
		internal static PropertyType GetPropertyTypeForConfigurationNode(ConfigurableDictionaryNode config, FdoCache cache = null)
		{
			Type parentType;
			var fieldType = GetTypeForConfigurationNode(config, cache, out parentType);
			return GetPropertyTypeFromReflectedTypes(fieldType, parentType);
		}

		private static PropertyType GetPropertyTypeFromReflectedTypes(Type fieldType, Type parentType)
		{
			if (fieldType == null)
			{
				return PropertyType.InvalidProperty;
			}
			if (IsCollectionType(fieldType))
			{
				return PropertyType.CollectionType;
			}
			if (typeof(ICmPicture).IsAssignableFrom(parentType))
			{
				return PropertyType.CmPictureType;
			}
			if (typeof(ICmFile).IsAssignableFrom(fieldType))
			{
				return PropertyType.CmFileType;
			}
			if (typeof(IMoForm).IsAssignableFrom(fieldType))
			{
				return PropertyType.MoFormType;
			}
			if (typeof(ICmPossibility).IsAssignableFrom(fieldType))
			{
				return PropertyType.CmPossibility;
			}
			if (typeof(ICmObject).IsAssignableFrom(fieldType))
			{
				return PropertyType.CmObjectType;
			}
			return PropertyType.PrimitiveType;
		}

		/// <summary>
		/// This method will return the Type that represents the data in the given configuration node.
		/// </summary>
		/// <param name="config">This node and it's lineage will be used to find the type</param>
		/// <param name="cache">Used when dealing with custom field nodes</param>
		/// <param name="parentType">This will be set to the type of the parent of config which is sometimes useful to the callers</param>
		/// <returns></returns>
		internal static Type GetTypeForConfigurationNode(ConfigurableDictionaryNode config, FdoCache cache, out Type parentType)
		{
			if (config == null)
			{
				throw new ArgumentNullException("config", "The configuration node must not be null.");
			}

			parentType = null;
			var lineage = new Stack<ConfigurableDictionaryNode>();
			// Build a list of the direct line up to the top of the configuration
			lineage.Push(config);
			var next = config;
			while (next.Parent != null)
			{
				next = next.Parent;
				lineage.Push(next);
			}
			// pop off the root configuration and read the FieldDescription property to get our starting point
			var assembly = GetAssemblyForFile(AssemblyFile);
			var rootNode = lineage.Pop();
			var lookupType = assembly.GetType(rootNode.FieldDescription);
			if (lookupType == null) // If the FieldDescription didn't load prepend the default model namespace and try again
			{
				lookupType = assembly.GetType("SIL.FieldWorks.FDO.DomainImpl." + rootNode.FieldDescription);
			}
			if (lookupType == null)
			{
				throw new ArgumentException(String.Format(xWorksStrings.InvalidRootConfigurationNode, rootNode.FieldDescription));
			}
			var fieldType = lookupType;

			// Traverse the configuration reflectively inspecting the types in parent to child order
			foreach (var node in lineage)
			{
				PropertyInfo property;
				if (node.IsCustomField)
				{
					fieldType = GetCustomFieldType(lookupType, node, cache);
				}
				else
				{
					property = GetProperty(lookupType, node);
					if (property != null)
					{
						fieldType = property.PropertyType;
					}
					else
					{
						return null;
					}
					if (IsCollectionType(fieldType))
					{
						// When a node points to a collection all the child nodes operate on individual items in the
						// collection, so look them up in the type that the collection contains. e.g. IEnumerable<ILexEntry>
						// gives ILexEntry and IFdoVector<ICmObject> gives ICmObject
						lookupType = fieldType.GetGenericArguments()[0];
					}
					else
					{
						parentType = lookupType;
						lookupType = fieldType;
					}
				}
			}
			return fieldType;
		}

		private static Type GetCustomFieldType(Type lookupType, ConfigurableDictionaryNode config, FdoCache cache)
		{
			// FDO doesn't work with interfaces, just concrete classes so chop the I off any interface types
			var customFieldOwnerClassName = lookupType.Name.TrimStart('I');
			var customFieldFlid = GetCustomFieldFlid(config, cache, customFieldOwnerClassName);
			if (customFieldFlid != 0)
			{
				var customFieldType = cache.MetaDataCacheAccessor.GetFieldType(customFieldFlid);
				switch (customFieldType)
				{
					case (int)CellarPropertyType.ReferenceSequence:
					case (int)CellarPropertyType.OwningSequence:
						{
							return typeof(IFdoVector);
						}
					case (int)CellarPropertyType.ReferenceAtomic:
					case (int)CellarPropertyType.OwningAtomic:
						{
							return typeof(ICmObject);
						}
					case (int)CellarPropertyType.Time:
						{
							return typeof(DateTime);
						}
					case (int)CellarPropertyType.MultiUnicode:
						{
							return typeof(IMultiUnicode);
						}
					case (int)CellarPropertyType.MultiString:
						{
							return typeof(IMultiString);
						}
					case (int)CellarPropertyType.String:
						{
							return typeof(string);
						}
					default:
						return null;
				}
			}
			return null;
		}

		/// <summary>
		/// Loading an assembly is expensive so we cache the assembly once it has been loaded
		/// for enahanced performance.
		/// </summary>
		private static Assembly GetAssemblyForFile(string assemblyFile)
		{
			if (!AssemblyMap.ContainsKey(assemblyFile))
			{
				AssemblyMap[assemblyFile] = Assembly.Load(AssemblyFile);
			}
			return AssemblyMap[assemblyFile];
		}

		/// <summary>
		/// Return the property info from a given class and node. Will check interface heirarchy for the property
		/// if <code>lookupType</code> is an interface.
		/// </summary>
		/// <param name="lookupType"></param>
		/// <param name="node"></param>
		/// <returns></returns>
		private static PropertyInfo GetProperty(Type lookupType, ConfigurableDictionaryNode node)
		{
			string propertyOfInterest;
			PropertyInfo propInfo;
			var typesToCheck = new Stack<Type>();
			typesToCheck.Push(lookupType);
			do
			{
				var current = typesToCheck.Pop();
				propertyOfInterest = node.FieldDescription;
				// if there is a SubField we need to use the type of the FieldDescription
				// for the rest of this method so set current to the FieldDescription type.
				if (node.SubField != null)
				{
					var property = current.GetProperty(node.FieldDescription);
					propertyOfInterest = node.SubField;
					if (property != null)
					{
						current = property.PropertyType;
					}
				}
				propInfo = current.GetProperty(propertyOfInterest, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (propInfo == null)
				{
					foreach (var i in current.GetInterfaces())
					{
						typesToCheck.Push(i);
					}
				}
			} while (propInfo == null && typesToCheck.Count > 0);
			return propInfo;
		}

		private static void GenerateXHTMLForMoForm(IMoForm moForm, ConfigurableDictionaryNode config, GeneratorSettings settings)
		{
			// Don't export if there is no such data
			if (moForm == null)
				return;
			GenerateXHTMLForStrings(moForm.Form, config, settings, moForm.Owner.Hvo);
			if (config.Children != null && config.Children.Any())
			{
				throw new NotImplementedException("Children for MoForm types not yet supported.");
			}
		}

		/// <summary>
		/// This method will generate the XHTML that represents a collection and its contents
		/// </summary>
		[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
			Justification = "writer is a reference")]
		private static void GenerateXHTMLForCollection(object collectionField, ConfigurableDictionaryNode config, DictionaryPublicationDecorator publicationDecorator, object collectionOwner, GeneratorSettings settings)
		{
			var writer = settings.Writer;
			writer.WriteStartElement("span");
			WriteClassNameAttribute(writer, config);
			IEnumerable collection;
			if (collectionField is IEnumerable)
			{
				collection = collectionField as IEnumerable;
			}
			else if (collectionField is IFdoVector)
			{
				collection = (collectionField as IFdoVector).Objects;
			}
			else
			{
				throw new ArgumentException("The given field is not a recognized collection");
			}
			if (config.DictionaryNodeOptions is DictionaryNodeSenseOptions)
			{
				GenerateXHTMLForSenses(config, publicationDecorator, settings, collection);

			}
			else
			{
				foreach (var item in collection)
				{
					if (publicationDecorator != null &&
						item is ICmObject &&
						publicationDecorator.IsExcludedObject((item as ICmObject).Hvo))
					{
						// Don't show examples or subentries that have been marked to exclude from publication.
						// See https://jira.sil.org/browse/LT-15697 and https://jira.sil.org/browse/LT-16775.
						continue;
					}
					GenerateCollectionItemContent(config, publicationDecorator, item, collectionOwner, settings);
				}
			}
			writer.WriteEndElement();
		}

		/// <summary>
		/// This method will generate the XHTML that represents a senses collection and its contents
		/// </summary>
		private static void GenerateXHTMLForSenses(ConfigurableDictionaryNode config, DictionaryPublicationDecorator publicationDecorator, GeneratorSettings settings, IEnumerable collection)
		{
			// Check whether all the senses have been excluded from publication.  See https://jira.sil.org/browse/LT-15697.
			int excluded = 0;
			foreach (var item in collection)
			{
				Debug.Assert(item is ILexSense);
				if (publicationDecorator != null && publicationDecorator.IsExcludedObject((item as ILexSense).Hvo))
					++excluded;
			}
			int count = collection.Cast<object>().Count();
			if (excluded == count)
				return;
			var isSingle = count == 1;
			string lastGrammaticalInfo, langId;
			var isSameGrammaticalInfo = IsAllGramInfoTheSame(config, collection, out lastGrammaticalInfo, out langId);
			if (isSameGrammaticalInfo)
				InsertGramInfoBeforeSenses(settings, lastGrammaticalInfo, langId);
			//sensecontent sensenumber sense morphosyntaxanalysis mlpartofspeech en
			foreach (var item in collection)
			{
				if (publicationDecorator != null && publicationDecorator.IsExcludedObject((item as ILexSense).Hvo))
					continue;
				GenerateSenseContent(config, publicationDecorator, item, isSingle, settings, isSameGrammaticalInfo);
			}
		}

		private static void InsertGramInfoBeforeSenses(GeneratorSettings settings, string lastGrammaticalInfo, string langId)
		{
			var writer = settings.Writer;
			writer.WriteStartElement("span");
			writer.WriteAttributeString("class", "sharedgrammaticalinfo");
			writer.WriteStartElement("span");
			writer.WriteAttributeString("lang", langId);
			writer.WriteString(lastGrammaticalInfo);
			writer.WriteEndElement();
			writer.WriteEndElement();
		}

		private static bool IsAllGramInfoTheSame(ConfigurableDictionaryNode config, IEnumerable collection, out string lastGrammaticalInfo, out string langId)
		{
			lastGrammaticalInfo = "";
			langId = "";
			var requestedString = string.Empty;
			var isSameGrammaticalInfo = false;
			if (config.FieldDescription == "SensesOS")
			{
				var senseNode = (DictionaryNodeSenseOptions) config.DictionaryNodeOptions;
				if (senseNode == null) return false;
				if (senseNode.ShowSharedGrammarInfoFirst)
				{
					foreach (var item in collection)
					{
						var owningObject = (ICmObject)item;
						var defaultWs = owningObject.Cache.WritingSystemFactory.get_EngineOrNull(owningObject.Cache.DefaultUserWs);
						langId = defaultWs.Id;
						var entryType = item.GetType();
						var grammaticalInfo = config.Children.FirstOrDefault(e => (e.FieldDescription == "MorphoSyntaxAnalysisRA" && e.IsEnabled));
						if (grammaticalInfo == null) return false;
						var property = entryType.GetProperty(grammaticalInfo.FieldDescription);
						var propertyValue = property.GetValue(item, new object[] {});
						if (propertyValue == null) return false;
						var child = grammaticalInfo.Children.FirstOrDefault(e => (e.IsEnabled && e.Children.Count == 0));
						if (child == null) return false;
						entryType = propertyValue.GetType();
						property = entryType.GetProperty(child.FieldDescription);
						propertyValue = property.GetValue(propertyValue, new object[] {});
						if (propertyValue is ITsString)
						{
							ITsString fieldValue = (ITsString) propertyValue;
							requestedString = fieldValue.Text;
						}
						else
						{
							IMultiAccessorBase fieldValue = (IMultiAccessorBase) propertyValue;
							var bestStringValue = fieldValue.BestAnalysisAlternative.Text;
							if(bestStringValue != fieldValue.NotFoundTss.Text)
								requestedString = bestStringValue;
						}
						if (string.IsNullOrEmpty(lastGrammaticalInfo))
							lastGrammaticalInfo = requestedString;
						else if (requestedString == lastGrammaticalInfo)
						{
							isSameGrammaticalInfo = true;
						}
						else
						{
							return false;
						}
					}
				}
			}
			return isSameGrammaticalInfo && !string.IsNullOrEmpty(lastGrammaticalInfo);
		}

		[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
			Justification = "writer is a reference")]
		private static void GenerateSenseContent(ConfigurableDictionaryNode config, DictionaryPublicationDecorator publicationDecorator,
			object item, bool isSingle, GeneratorSettings settings, bool isSameGrammaticalInfo)
		{
			var writer = settings.Writer;
			if (config.Children.Count != 0)
			{
				// Wrap the number and sense combination in a sensecontent span so that can both be affected by DisplayEachSenseInParagraph
				writer.WriteStartElement("span");
				writer.WriteAttributeString("class", "sensecontent");
				GenerateSenseNumberSpanIfNeeded(config, writer, item, settings.Cache,
														  publicationDecorator, isSingle);
			}

			writer.WriteStartElement(GetElementNameForProperty(config));
			WriteCollectionItemClassAttribute(config, writer);
			if (config.Children != null)
			{
				foreach (var child in config.Children)
				{
					if (child.FieldDescription != "MorphoSyntaxAnalysisRA" || !isSameGrammaticalInfo)
					{
						GenerateXHTMLForFieldByReflection(item, child, publicationDecorator, settings);
					}
				}
			}

			writer.WriteEndElement();
			// close out the sense wrapping
			if (config.Children.Count != 0)
			{
				writer.WriteEndElement();
			}
		}

		[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
			Justification = "writer is a reference")]
		private static void GenerateCollectionItemContent(ConfigurableDictionaryNode config, DictionaryPublicationDecorator publicationDecorator,
			object item, object collectionOwner, GeneratorSettings settings)
		{
			var writer = settings.Writer;
			if (config.DictionaryNodeOptions is DictionaryNodeListOptions && !IsListItemSelectedForExport(config, item, collectionOwner))
			{
				return;
			}
			writer.WriteStartElement(GetElementNameForProperty(config));
			WriteCollectionItemClassAttribute(config, writer);
			if (config.Children != null)
			{
				var listOptions = config.DictionaryNodeOptions as DictionaryNodeListOptions;
				// sense and entry options types suggest that we are working with a cross reference
				if (listOptions != null &&
					(listOptions.ListId == DictionaryNodeListOptions.ListIds.Sense ||
					 listOptions.ListId == DictionaryNodeListOptions.ListIds.Entry))
				{
					GenerateCrossReferenceChildren(config, publicationDecorator, (ILexReference)item, collectionOwner, settings);
				}
				else if (listOptions is DictionaryNodeComplexFormOptions)
				{
					foreach (var child in config.Children)
					{
						if (child.FieldDescription == "LookupComplexEntryType")
							GenerateSubentryTypeChild(child, publicationDecorator, (ILexEntry)item, (ILexEntry)collectionOwner, settings);
						else
							GenerateXHTMLForFieldByReflection(item, child, publicationDecorator, settings);
					}
				}
				else
				{
					foreach (var child in config.Children)
					{
						GenerateXHTMLForFieldByReflection(item, child, publicationDecorator, settings);
					}
				}
			}
			writer.WriteEndElement();
		}

		private static void GenerateCrossReferenceChildren(ConfigurableDictionaryNode config, DictionaryPublicationDecorator publicationDecorator,
			ILexReference reference, object collectionOwner, GeneratorSettings settings)
		{
			if(config.Children != null)
			{
				foreach(var child in config.Children)
				{
					if(child.FieldDescription == "ConfigTargets")
					{
						settings.Writer.WriteStartElement("span");
						WriteClassNameAttribute(settings.Writer, child);
						var ownerHvo = collectionOwner is ILexEntry ? ((ILexEntry)collectionOwner).Hvo : ((ILexSense)collectionOwner).Owner.Hvo;
						// "Where" excludes the entry we are displaying. (The LexReference contains all involved entries)
						// If someone ever uses a "Sequence" type lexical relation, should the current item
						// be displayed in its location in the sequence?  Just asking...
						foreach(var target in reference.ConfigTargets.Where(x => x.EntryHvo != ownerHvo))
						{
							GenerateCollectionItemContent(child, publicationDecorator, target, reference, settings);
							if (LexRefTypeTags.IsAsymmetric((LexRefTypeTags.MappingTypes)reference.OwnerType.MappingType) &&
								LexRefDirection(reference, collectionOwner) == ":r")
							{
								// In the reverse direction of an asymmetric lexical reference, we want only the first item.
								// See https://jira.sil.org/browse/LT-16427.
								break;
							}
						}
						settings.Writer.WriteEndElement();
					}
					// OwnerType is a LexRefType, some of which are asymmetric (e.g. Part/Whole). If this Type is symmetric or we are currently
					// working in the forward direction, the generic code will work; however, if we are working on an asymmetric LexRefType
					// in the reverse direction, we need to display the ReverseName or ReverseAbbreviation instead of the Name or Abbreviation.
					else if(child.FieldDescription == "OwnerType"
						&& LexRefTypeTags.IsAsymmetric((LexRefTypeTags.MappingTypes)reference.OwnerType.MappingType)
						&& LexRefDirection(reference, collectionOwner) == ":r")
					{
						// Changing the SubField changes the default CSS Class name.
						// If there is no override, override with the default before changing the SubField.
						if(string.IsNullOrEmpty(child.CSSClassNameOverride))
							child.CSSClassNameOverride = CssGenerator.GetClassAttributeForConfig(child);

						// Prefix the SubField with "Reverse" just long enough to generate XHTML for this node.
						var subField = child.SubField;
						child.SubField = "Reverse" + subField;
						GenerateXHTMLForFieldByReflection(reference, child, publicationDecorator, settings);
						child.SubField = subField;
					}
					else
						GenerateXHTMLForFieldByReflection(reference, child, publicationDecorator, settings);
				}
			}
		}

		private static void GenerateSubentryTypeChild(ConfigurableDictionaryNode config, DictionaryPublicationDecorator publicationDecorator,
			ILexEntry subEntry, ILexEntry mainEntry, GeneratorSettings settings)
		{
			if (!config.IsEnabled)
				return;

			var entryRefs = subEntry.ComplexFormEntryRefs.Where(entryRef => entryRef.PrimaryEntryRoots.Contains(mainEntry));
			var complexEntryRef = entryRefs.FirstOrDefault();
			if (complexEntryRef == null)
				return;

			GenerateXHTMLForCollection(complexEntryRef.ComplexEntryTypesRS, config, publicationDecorator, subEntry, settings);
		}

		private static void GenerateSenseNumberSpanIfNeeded(ConfigurableDictionaryNode senseConfigNode, XmlWriter writer,
																			 object sense, FdoCache cache,
																			 DictionaryPublicationDecorator publicationDecorator, bool isSingle)
		{
			var senseOptions = senseConfigNode.DictionaryNodeOptions as DictionaryNodeSenseOptions;
			if (senseOptions == null || (isSingle && !senseOptions.NumberEvenASingleSense))
				return;
			if (string.IsNullOrEmpty(senseOptions.NumberingStyle))
				return;
			writer.WriteStartElement("span");
			writer.WriteAttributeString("class", "sensenumber");
			string senseNumber = cache.GetOutlineNumber((ICmObject) sense, LexSenseTags.kflidSenses, false, true,
				publicationDecorator);
			string formatedSenseNumber = GenerateOutlineNumber(senseOptions.NumberingStyle, senseNumber, senseConfigNode);
			writer.WriteString(formatedSenseNumber);
			writer.WriteEndElement();
		}

		private static string GenerateOutlineNumber(string numberingStyle, string senseNumber, ConfigurableDictionaryNode senseConfigNode)
		{
			string nextNumber;
			switch (numberingStyle)
			{
				case "%d":
					nextNumber = GetLastPartOfSenseNumber(senseNumber).ToString();
					break;
				case "%a":
				case "%A":
					nextNumber = GetAlphaSenseCounter(numberingStyle, senseNumber);
					break;
				case "%i":
				case "%I":
					nextNumber = GetRomanSenseCounter(numberingStyle, senseNumber);
					break;
				case "%O":
					nextNumber = GetSubSenseNumber(senseNumber, senseConfigNode);
					break;
				default://this handles "%z"
					nextNumber = senseNumber;
					break;
			}
			return nextNumber;
		}

		private static string GetSubSenseNumber(string senseNumber, ConfigurableDictionaryNode senseConfigNode)
		{
			string subSenseNumber = string.Empty;
			var parentSenseNode = senseConfigNode.Parent.DictionaryNodeOptions as DictionaryNodeSenseOptions;
			if (parentSenseNode != null)
			{
				if (!string.IsNullOrEmpty(parentSenseNode.NumberingStyle) && senseNumber.Contains('.'))
					subSenseNumber = GenerateOutlineNumber(parentSenseNode.NumberingStyle, senseNumber.Split('.')[0], senseConfigNode) + ".";
			}
			subSenseNumber += senseNumber.Split('.')[senseNumber.Split('.').Length - 1];
			return subSenseNumber;
		}

		private static string GetAlphaSenseCounter(string numberingStyle, string senseNumber)
		{
			string nextNumber;
			int asciiBytes = 64;
			asciiBytes = asciiBytes + GetLastPartOfSenseNumber(senseNumber);
			nextNumber = ((char) (asciiBytes)).ToString();
			if (numberingStyle == "%a")
				nextNumber = nextNumber.ToLower();
			return nextNumber;
		}

		private static int GetLastPartOfSenseNumber(string senseNumber)
		{
			if (senseNumber.Contains("."))
				return Int32.Parse(senseNumber.Split('.')[senseNumber.Split('.').Length - 1]);
			return Int32.Parse(senseNumber);
		}

		private static string GetRomanSenseCounter(string numberingStyle, string senseNumber)
		{
			int num = GetLastPartOfSenseNumber(senseNumber);
			string[] ten = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC" };
			string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
			string roman = string.Empty;
			roman += ten[(num / 10)];
			roman += ones[num % 10];
			if (numberingStyle == "%i")
				roman = roman.ToLower();
			return roman;
		}

		[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
			Justification = "writer is a reference")]
		private static void GenerateXHTMLForICmObject(ICmObject propertyValue, ConfigurableDictionaryNode config, GeneratorSettings settings)
		{
			var writer = settings.Writer;
			// Don't export if there is no such data
			if (propertyValue == null)
				return;
			writer.WriteStartElement("span");
			// Rely on configuration to handle adjusting the classname for "RA" or "OA" model properties
			var fieldDescription = CssGenerator.GetClassAttributeForConfig(config);
			writer.WriteAttributeString("class", fieldDescription);
			if (config.Children != null)
			{
				foreach (var child in config.Children)
				{
					if (child.IsEnabled)
					{
						GenerateXHTMLForFieldByReflection(propertyValue, child, null, settings);
					}
				}
			}

			writer.WriteEndElement();
		}

		/// <summary>
		///  Write out the class element to use in the span for the individual items in the collection
		/// </summary>
		/// <param name="config"></param>
		/// <param name="writer"></param>
		private static void WriteCollectionItemClassAttribute(ConfigurableDictionaryNode config, XmlWriter writer)
		{
			var collectionName = CssGenerator.GetClassAttributeForConfig(config);
			// chop the pluralization off the parent class
			writer.WriteAttributeString("class", collectionName.Substring(0, collectionName.Length - 1).ToLower());
		}

		/// <summary>
		/// This method is used to determine if we need to iterate through a property and generate xhtml for each item
		/// </summary>
		internal static bool IsCollectionType(Type entryType)
		{
			// The collections we test here are generic collection types (e.g. IEnumerable<T>). Note: This (and other code) does not work for arrays.
			// We do have at least one collection type with at least two generic arguments; hence `> 0` instead of `== 1`
			return (entryType.GetGenericArguments().Length > 0);
		}

		/// <summary>
		/// Determines if the user has specified that this item should generate content.
		/// <returns><c>true</c> if the user has ticked the list item that applies to this object</returns>
		/// </summary>
		internal static bool IsListItemSelectedForExport(ConfigurableDictionaryNode config, object listItem, object parent)
		{
			var listOptions = (DictionaryNodeListOptions)config.DictionaryNodeOptions;
			if (listOptions == null)
				throw new ArgumentException(string.Format("This configuration node had no options and we were expecting them: {0} ({1})", config.DisplayLabel, config.FieldDescription), "config");

			var selectedListOptions = new List<Guid>();
			var forwardReverseOptions = new List<Tuple<Guid, string>>();
			foreach (var option in listOptions.Options.Where(optn => optn.IsEnabled))
			{
				var forwardReverseIndicator = option.Id.IndexOf(':');
				if (forwardReverseIndicator > 0)
				{
					var guid = new Guid(option.Id.Substring(0, forwardReverseIndicator));
					forwardReverseOptions.Add(new Tuple<Guid, string>(guid, option.Id.Substring(forwardReverseIndicator)));
				}
				else
				{
					selectedListOptions.Add(new Guid(option.Id));
				}
			}
			switch (listOptions.ListId)
			{
				case DictionaryNodeListOptions.ListIds.Variant:
				case DictionaryNodeListOptions.ListIds.Complex:
				case DictionaryNodeListOptions.ListIds.Minor:
					{
						return IsListItemSelectedForExportInternal(listOptions.ListId, listItem, selectedListOptions);
					}
				case DictionaryNodeListOptions.ListIds.Entry:
				case DictionaryNodeListOptions.ListIds.Sense:
					{
						var lexRef = (ILexReference)listItem;
						var entryTypeGuid = lexRef.OwnerType.Guid;
						if (selectedListOptions.Contains(entryTypeGuid))
						{
							return true;
						}
						var entryTypeGuidAndDirection = new Tuple<Guid, string>(entryTypeGuid, LexRefDirection(lexRef, parent));
						return forwardReverseOptions.Contains(entryTypeGuidAndDirection);
					}
				default:
					{
						Debug.WriteLine("Unhandled list ID encountered: " + listOptions.ListId);
						return true;
					}
			}
		}

		private static bool IsListItemSelectedForExportInternal(DictionaryNodeListOptions.ListIds listId,
			object listItem, IEnumerable<Guid> selectedListOptions)
		{
			var entryTypeGuids = new Set<Guid>();
			var entryRef = listItem as ILexEntryRef;
			var entry = listItem as ILexEntry;
			if (entryRef != null)
			{
				if (listId == DictionaryNodeListOptions.ListIds.Variant || listId == DictionaryNodeListOptions.ListIds.Minor)
					GetVariantTypeGuidsForEntryRef(entryRef, entryTypeGuids);
				if (listId == DictionaryNodeListOptions.ListIds.Complex || listId == DictionaryNodeListOptions.ListIds.Minor)
					GetComplexFormTypeGuidsForEntryRef(entryRef, entryTypeGuids);
			}
			else if (entry != null)
			{
				if (listId == DictionaryNodeListOptions.ListIds.Variant || listId == DictionaryNodeListOptions.ListIds.Minor)
					foreach (var visibleEntryRef in entry.VisibleVariantEntryRefs)
						GetVariantTypeGuidsForEntryRef(visibleEntryRef, entryTypeGuids);
				if (listId == DictionaryNodeListOptions.ListIds.Complex || listId == DictionaryNodeListOptions.ListIds.Minor)
					foreach (var complexFormEntryRef in entry.ComplexFormEntryRefs)
						GetComplexFormTypeGuidsForEntryRef(complexFormEntryRef, entryTypeGuids);
			}
			return entryTypeGuids.Intersect(selectedListOptions).Any();
		}

		private static void GetVariantTypeGuidsForEntryRef(ILexEntryRef entryRef, Set<Guid> entryTypeGuids)
		{
			if (entryRef.VariantEntryTypesRS.Any())
				entryTypeGuids.AddRange(entryRef.VariantEntryTypesRS.Select(guid => guid.Guid));
			else
				entryTypeGuids.Add(XmlViewsUtils.GetGuidForUnspecifiedVariantType());
		}

		private static void GetComplexFormTypeGuidsForEntryRef(ILexEntryRef entryRef, Set<Guid> entryTypeGuids)
		{
			if (entryRef.ComplexEntryTypesRS.Any())
				entryTypeGuids.AddRange(entryRef.ComplexEntryTypesRS.Select(guid => guid.Guid));
			else
				entryTypeGuids.Add(XmlViewsUtils.GetGuidForUnspecifiedComplexFormType());
		}

		/// <returns>
		/// ":f" if we are working in the forward direction (the parent is the head of a tree or asymmetric pair);
		/// ":r" if we are working in the reverse direction (the parent is a subordinate in a tree or asymmetric pair).
		/// </returns>
		/// <remarks>This method does not determine symmetry; use <see cref="LexRefTypeTags.IsAsymmetric"/> for that.</remarks>
		private static string LexRefDirection(ILexReference lexRef, object parent)
		{
			return Equals(lexRef.TargetsRS[0], parent) ? ":f" : ":r";
		}

		/// <summary>
		/// Returns true if the given collection is empty (type determined at runtime)
		/// </summary>
		/// <param name="collection"></param>
		/// <exception cref="ArgumentException">if the object given is null, or not a handled collection</exception>
		/// <returns></returns>
		private static bool IsCollectionEmpty(object collection)
		{
			if (collection == null)
			{
				throw new ArgumentNullException("collection");
			}
			if (collection is IEnumerable)
			{
				return !(((IEnumerable)collection).Cast<object>().Any());
			}
			if (collection is IFdoVector)
			{
				return ((IFdoVector)collection).ToHvoArray().Length == 0;
			}
			throw new ArgumentException(@"Cannot test something that isn't a collection", "collection");
		}

		/// <summary>
		/// This method generates XHTML content for a given object
		/// </summary>
		/// <param name="field">This is the object that owns the property, needed to look up writing system info for virtual string fields</param>
		/// <param name="propertyValue">data to generate xhtml for</param>
		/// <param name="config"></param>
		/// <param name="settings"></param>
		private static void GenerateXHTMLForValue(object field, object propertyValue, ConfigurableDictionaryNode config, GeneratorSettings settings)
		{
			// If we're working with a headword, either for this entry or another one (Variant or Complex Form, etc.), store that entry's HVO
			// so we can generate a link to the main or minor entry for this headword.
			var hvo = 0;
			if (config.CSSClassNameOverride == "headword")
			{
				if (field is ILexEntry)
					hvo = ((ILexEntry)field).Hvo;
				else if (field is ILexEntryRef)
					hvo = ((ILexEntryRef)field).OwningEntry.Hvo;
				else if (field is ISenseOrEntry)
					hvo = ((ISenseOrEntry)field).EntryHvo;
				else
					Debug.WriteLine("Need to find Entry Hvo for {0}",
						field == null ? DictionaryConfigurationMigrator.BuildPathStringFromNode(config) : field.GetType().Name);
			}

			if (propertyValue is ITsString)
			{
				if (!TsStringUtils.IsNullOrEmpty((ITsString)propertyValue))
				{
					settings.Writer.WriteStartElement("span");
					WriteClassNameAttribute(settings.Writer, config);
					GenerateXHTMLForString((ITsString)propertyValue, config, settings, hvo: hvo);
					settings.Writer.WriteEndElement();
				}
			}
			else if (propertyValue is IMultiStringAccessor)
			{
				GenerateXHTMLForStrings((IMultiStringAccessor)propertyValue, config, settings, hvo);
			}
			else if (propertyValue is int)
			{
				WriteElementContents(propertyValue, config, settings.Writer);
			}
			else if (propertyValue is DateTime)
			{
				WriteElementContents(((DateTime)propertyValue).ToLongDateString(), config, settings.Writer);
			}
			else if (propertyValue is GenDate)
			{
				WriteElementContents(((GenDate)propertyValue).ToLongString(), config, settings.Writer);
			}
			else if (propertyValue is IMultiAccessorBase)
			{
				GenerateXHTMLForVirtualStrings((ICmObject)field, (IMultiAccessorBase)propertyValue, config, settings, hvo);
			}
			else if (propertyValue is String)
			{
				var propValueString = (String)propertyValue;
				if (!String.IsNullOrEmpty(propValueString))
				{
					Debug.Assert(hvo == 0, "we have a hvo; make a link!");
					// write out Strings something like: <span class="foo">Bar</span>
					settings.Writer.WriteStartElement("span");
					WriteClassNameAttribute(settings.Writer, config);
					settings.Writer.WriteString(propValueString);
					settings.Writer.WriteEndElement();
				}
			}
			else
			{
				if(propertyValue == null)
				{
					Debug.WriteLine(String.Format("Bad configuration node: {0}", DictionaryConfigurationMigrator.BuildPathStringFromNode(config)));
				}
				else
				{
					Debug.WriteLine(String.Format("What do I do with {0}?", propertyValue.GetType().Name));
				}
			}

		}

		private static void WriteElementContents(object propertyValue, ConfigurableDictionaryNode config,
															  XmlWriter writer)
		{
			writer.WriteStartElement(GetElementNameForProperty(config));
			WriteClassNameAttribute(writer, config);
			writer.WriteString(propertyValue.ToString());
			writer.WriteEndElement();
		}

		/// <summary>
		/// This method will generate an XHTML span with a string for each selected writing system in the
		/// DictionaryWritingSystemOptions of the configuration that also has data in the given IMultiStringAccessor
		/// </summary>
		private static void GenerateXHTMLForStrings(IMultiStringAccessor multiStringAccessor, ConfigurableDictionaryNode config,
			GeneratorSettings settings, int hvo = 0)
		{
			var wsOptions = config.DictionaryNodeOptions as DictionaryNodeWritingSystemOptions;
			if (wsOptions == null)
			{
				throw new ArgumentException(@"Configuration nodes for MultiString fields should have WritingSystemOptions", "config");
			}
			// TODO pH 2014.12: this can generate an empty span if no checked WS's contain data
			settings.Writer.WriteStartElement("span");
			WriteClassNameAttribute(settings.Writer, config);
			foreach (var option in wsOptions.Options)
			{
				if (!option.IsEnabled)
				{
					continue;
				}
				var wsId = WritingSystemServices.GetMagicWsIdFromName(option.Id);
				// The string for the specific wsId in the option, or the best string option in the accessor if the wsId is magic
				ITsString bestString;
				if (wsId == 0)
				{
					// This is not a magic writing system, so grab the user requested string
					wsId = settings.Cache.WritingSystemFactory.GetWsFromStr(option.Id);
					if (wsId == 0)
						throw new ArgumentException(string.Format("Writing system requested that is not known in local store: {0}", option.Id), "option");
					bestString = multiStringAccessor.get_String(wsId);
				}
				else
				{
					// Writing system is magic i.e. 'best vernacular' or 'first pronunciation'
					// use the method in the multi-string to get the right string and set wsId to the used one
					bestString = multiStringAccessor.GetAlternativeOrBestTss(wsId, out wsId);
				}
				GenerateWsPrefixAndString(config, settings, wsOptions, wsId, bestString, hvo);
			}
			settings.Writer.WriteEndElement();
		}

		/// <summary>
		/// This method will generate an XHTML span with a string for each selected writing system in the
		/// DictionaryWritingSystemOptions of the configuration that also has data in the given IMultiAccessorBase
		/// </summary>
		private static void GenerateXHTMLForVirtualStrings(ICmObject owningObject, IMultiAccessorBase multiStringAccessor,
																			ConfigurableDictionaryNode config, GeneratorSettings settings, int hvo)
		{
			var wsOptions = config.DictionaryNodeOptions as DictionaryNodeWritingSystemOptions;
			if (wsOptions == null)
			{
				throw new ArgumentException(@"Configuration nodes for MultiString fields should have WritingSystemOptions", "config");
			}
			settings.Writer.WriteStartElement("span");
			WriteClassNameAttribute(settings.Writer, config);

			foreach (var option in wsOptions.Options)
			{
				if (!option.IsEnabled)
				{
					continue;
				}
				var wsId = WritingSystemServices.GetMagicWsIdFromName(option.Id);
				// The string for the specific wsId in the option, or the best string option in the accessor if the wsId is magic
				if (wsId == 0)
				{
					// This is not a magic writing system, so grab the user requested string
					wsId = settings.Cache.WritingSystemFactory.GetWsFromStr(option.Id);
				}
				else
				{
					var defaultWs = owningObject.Cache.WritingSystemFactory.get_EngineOrNull(owningObject.Cache.DefaultUserWs);
					wsId = WritingSystemServices.InterpretWsLabel(owningObject.Cache, option.Id, (IWritingSystem)defaultWs,
																					owningObject.Hvo, multiStringAccessor.Flid, (IWritingSystem)defaultWs);
				}
				var requestedString = multiStringAccessor.get_String(wsId);
				GenerateWsPrefixAndString(config, settings, wsOptions, wsId, requestedString, hvo);
			}
			settings.Writer.WriteEndElement();
		}

		[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
			Justification = "writer is a reference. cache is a reference.")]
		private static void GenerateWsPrefixAndString(ConfigurableDictionaryNode config, GeneratorSettings settings,
			DictionaryNodeWritingSystemOptions wsOptions, int wsId, ITsString requestedString, int hvo)
		{
			var writer = settings.Writer;
			var cache = settings.Cache;
			if (String.IsNullOrEmpty(requestedString.Text))
			{
				return;
			}
			if (wsOptions.DisplayWritingSystemAbbreviations)
			{
				writer.WriteStartElement("span");
				writer.WriteAttributeString("class", "writingsystemprefix");
				var prefix = ((IWritingSystem)cache.WritingSystemFactory.get_EngineOrNull(wsId)).Abbreviation;
				writer.WriteString(prefix);
				writer.WriteEndElement();
			}
			var wsName = cache.WritingSystemFactory.get_EngineOrNull(wsId).Id;
			GenerateXHTMLForString(requestedString, config, settings, wsName, hvo);
		}

		[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
			Justification = "writer is a reference")]
		private static void GenerateXHTMLForString(ITsString fieldValue, ConfigurableDictionaryNode config,
													GeneratorSettings settings, string writingSystem = null, int hvo = 0)
		{
			var writer = settings.Writer;
			if (writingSystem != null && writingSystem.Contains("audio"))
			{
				if (fieldValue != null)
				{
					writer.WriteStartElement("span");
					var audioId = fieldValue.Text.Substring(0, fieldValue.Text.IndexOf(".", StringComparison.Ordinal));
					GenerateXHTMLForAudioFile(writingSystem, writer, audioId,
						GenerateSrcAttributeForAudioFromFilePath(fieldValue.Text, "AudioVisual", settings), String.Empty);
					writer.WriteEndElement();
				}
			}
			else
			{
				//use the passed in writing system unless null
				//otherwise use the first option from the DictionaryNodeWritingSystemOptions or english if the options are null
				writingSystem = writingSystem ?? GetLanguageFromFirstOption(config.DictionaryNodeOptions as DictionaryNodeWritingSystemOptions, settings.Cache);

				if (hvo > 0)
				{
					settings.Writer.WriteStartElement("a");
					settings.Writer.WriteAttributeString("href", "#hvo" + hvo);
				}
				if (fieldValue.RunCount > 1)
				{
					for (int i = 0; i < fieldValue.RunCount; i++)
					{
						var text = fieldValue.get_RunText(i);
						var props = fieldValue.get_Properties(i);
						var style = props.GetStrPropValue((int)FwTextPropType.ktptNamedStyle);
						writer.WriteStartElement("span");
						// TODO: In case of multi-writingsystem ITsSring, update WS for each run
						writer.WriteAttributeString("lang", writingSystem);
						if (!String.IsNullOrEmpty(style))
						{
							var css_style = CssGenerator.GenerateCssStyleFromFwStyleSheet(style, settings.Cache.WritingSystemFactory.GetWsFromStr(writingSystem), settings.Mediator);
							writer.WriteAttributeString("style", css_style.ToString());
						}
						writer.WriteString(text);
						writer.WriteEndElement();
					}
				}
				else
				{
					writer.WriteStartElement("span");
					writer.WriteAttributeString("lang", writingSystem);
					writer.WriteString(fieldValue.Text);
					writer.WriteEndElement();
				}
				if (hvo > 0)
				{
					settings.Writer.WriteEndElement(); // </a>
				}
			}
		}

		/// <summary>
		/// This method Generate XHTML for Audio file
		/// </summary>
		/// <param name="classname">value for class attribute for audio tag</param>
		/// <param name="writer"></param>
		/// <param name="audioId">value for Id attribute for audio tag</param>
		/// <param name="srcAttribute">Source location path for audio file</param>
		/// <param name="caption">Innertext for hyperlink</param>
		/// <returns></returns>
		private static void GenerateXHTMLForAudioFile(string classname,
			XmlWriter writer, string audioId, string srcAttribute,string caption)
		{
			writer.WriteStartElement("audio");
			writer.WriteAttributeString("id", audioId);
			writer.WriteStartElement("source");
			writer.WriteAttributeString("src",
				srcAttribute);
			writer.WriteEndElement();
			writer.WriteEndElement();
			writer.WriteStartElement("a");
			writer.WriteAttributeString("class", classname);
			writer.WriteAttributeString("href", "#");
			writer.WriteAttributeString("onclick", "document.getElementById('" + audioId + "').play()");
			if (!String.IsNullOrEmpty(caption))
			{
				writer.WriteString(caption);
			}
			writer.WriteEndElement();
		}

		/// <summary>
		/// This method is intended to produce the xhtml element that we want for given configuration objects.
		/// </summary>
		/// <param name="config"></param>
		/// <returns></returns>
		private static string GetElementNameForProperty(ConfigurableDictionaryNode config)
		{
			//TODO: Improve this logic to deal with subentries if necessary
			if (config.FieldDescription.Equals("LexEntry") || config.DictionaryNodeOptions is DictionaryNodePictureOptions)
			{
				return "div";
			}
			return "span";
		}

		/// <summary>
		/// This method returns the lang attribute value from the first selected writing system in the given options.
		/// </summary>
		/// <param name="wsOptions"></param>
		/// <param name="cache"></param>
		/// <returns></returns>
		private static string GetLanguageFromFirstOption(DictionaryNodeWritingSystemOptions wsOptions, FdoCache cache)
		{
			const string defaultLang = "en";
			if (wsOptions == null)
				return defaultLang;
			foreach (var option in wsOptions.Options)
			{
				if (option.IsEnabled)
				{
					var wsId = WritingSystemServices.GetMagicWsIdFromName(option.Id);
					// if the writing system isn't a magic name just use it
					if (wsId == 0)
					{
						return option.Id;
					}
					// otherwise get a list of the writing systems for the magic name, and use the first one
					return WritingSystemServices.GetWritingSystemList(cache, wsId, true).First().Id;
				}
			}
			// paranoid fallback to first option of the list in case there are no enabled options
			return wsOptions.Options[0].Id;
		}

		public static DictionaryPublicationDecorator GetPublicationDecoratorAndEntries(Mediator mediator, out int[] entriesToSave)
		{
			var cache = mediator.PropertyTable.GetValue("cache") as FdoCache;
			if (cache == null)
			{
				throw new ArgumentException(@"Mediator had no cache", "mediator");
			}
			var clerk = mediator.PropertyTable.GetValue("ActiveClerk", null) as RecordClerk;
			if (clerk == null)
			{
				throw new ArgumentException(@"Mediator had no clerk", "mediator");
			}

			ICmPossibility currentPublication;
			var currentPublicationString = mediator.PropertyTable.GetStringProperty("SelectedPublication", xWorksStrings.AllEntriesPublication);
			if (currentPublicationString == xWorksStrings.AllEntriesPublication)
			{
				currentPublication = null;
			}
			else
			{
				currentPublication =
					(from item in cache.LangProject.LexDbOA.PublicationTypesOA.PossibilitiesOS
					 where item.Name.UserDefaultWritingSystem.Text == currentPublicationString
					 select item).FirstOrDefault();
			}
			var decorator = new DictionaryPublicationDecorator(cache, clerk.VirtualListPublisher, clerk.VirtualFlid, currentPublication);
			entriesToSave = decorator.GetEntriesToPublish(mediator, clerk.VirtualFlid).ToArray();
			return decorator;
		}

		[SuppressMessage("Gendarme.Rules.Design", "TypesWithDisposableFieldsShouldBeDisposableRule",
			Justification = "Cache and Mediator are a references")]
		public class GeneratorSettings
		{
			public FdoCache Cache { get; private set; }
			public XmlWriter Writer { get; private set; }
			public bool UseRelativePaths { get; private set; }
			public bool CopyFiles { get; private set; }
			public string ExportPath { get; private set; }
			public Mediator Mediator { get; private set;}
			public GeneratorSettings(FdoCache cache, Mediator mediator, XmlWriter writer, bool relativePaths, bool copyFiles, string exportPath)
			{
				if (cache == null || writer == null)
				{
					throw new ArgumentNullException();
				}
				Cache = cache;
				Mediator = mediator;
				Writer = writer;
				UseRelativePaths = relativePaths;
				CopyFiles = copyFiles;
				ExportPath = exportPath;
			}
		}
	}
}
