// Copyright (c) 2014-15 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml;
using SIL.FieldWorks.Common.Framework;
using System.Xml.Schema;
using System.Xml.Serialization;
using SIL.CoreImpl.Text;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.Common.FwKernelInterfaces;
using SIL.FieldWorks.Common.FwUtils;
using SIL.Utils;
using SIL.FieldWorks.FDO.DomainServices;
using SIL.FieldWorks.XWorks;
using StyleInfo = SIL.FieldWorks.FwCoreDlgControls.StyleInfo;

namespace LanguageExplorer
{
	/// <summary>
	/// Specialization of StylesXmlAccessor for loading the Flex factory styles or serializing to xml
	/// </summary>
	[XmlRoot(ElementName = "Styles")]
	internal class FlexStylesXmlAccessor : StylesXmlAccessor, IXmlSerializable
	{
		private readonly ILexDb m_lexicon;

		/// <summary>
		/// Parameterless constructor for the purposes of Xml serialization
		/// </summary>
		private FlexStylesXmlAccessor() {}

		private string m_sourceDocumentPath;

		/// <summary/>
		public FlexStylesXmlAccessor(ILexDb lexicon, bool loadDocument = false, string sourceDocument = null)
			: base(lexicon.Cache)
		{
			m_sourceDocumentPath = sourceDocument;
			m_lexicon = lexicon;
			if (loadDocument)
			{
				m_sourceStyles = LoadDoc(sourceDocument);
				if (!string.IsNullOrEmpty(sourceDocument))
					CreateStyles(new ConsoleProgress(), new object[] { m_cache.LangProject.StylesOC, m_sourceStyles, false});
			}
		}

		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Required implementation of abstract method gives relative path to configuration file
		/// from the FieldWorks install folder.
		/// </summary>
		/// -------------------------------------------------------------------------------------
		protected override string ResourceFilePathFromFwInstall
		{
			get { return Path.DirectorySeparatorChar + @"Language Explorer" + Path.DirectorySeparatorChar + ResourceFileName; }
		}

		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Required implementation of abstract method gives name of the Flex styles sheet
		/// resource
		/// </summary>
		/// -------------------------------------------------------------------------------------
		protected override string ResourceName
		{
			get { return "FlexStyles"; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the resource list in which the CmResources are owned.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected override IFdoOwningCollection<ICmResource> ResourceList
		{
			get { return m_lexicon.ResourcesOC; }
		}

		/// -------------------------------------------------------------------------------------
		/// <summary>
		/// Required implementation of abstract method gives style collection.
		/// </summary>
		/// -------------------------------------------------------------------------------------
		protected override IFdoOwningCollection<IStStyle> StyleCollection
		{
			get { return m_cache.LangProject.StylesOC; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the FdoCache
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected override FdoCache Cache
		{
			get { return m_lexicon.Cache; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Special overridable method to allow application-specific overrides to allow a
		/// particular style to be renamed.
		/// </summary>
		/// <param name="styleName">Name of the original style.</param>
		/// <param name="replStyleName">Name of the replacement style.</param>
		/// <returns>The default always returns <c>false</c>; but an application may
		/// override this to return <c>true</c> for a specific pair of stylenames.</returns>
		/// ------------------------------------------------------------------------------------
		protected override bool StyleReplacementAllowed(string styleName, string replStyleName)
		{
			return (styleName == "External Link" && replStyleName == "Hyperlink") ||
				(styleName == "Internal Link" && replStyleName == "Hyperlink") ||
				(styleName == "Language Code" && replStyleName == "Writing System Abbreviation");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// If the current stylesheet version in the Db doesn't match that of the current XML
		/// file, update the DB.
		/// </summary>
		/// <param name="lp">The language project</param>
		/// <param name="progressDlg">The progress dialog.</param>
		/// ------------------------------------------------------------------------------------
		internal static void EnsureCurrentStylesheet(ILangProject lp, IThreadedProgress progressDlg)
		{
			// We don't need to establish a NonUndoableUnitOfWork here because caller has already
			// done it and if not, the internal code of StylesXmlAccessor will do it for us.
			var acc = new FlexStylesXmlAccessor(lp.LexDbOA);
			acc.EnsureCurrentResource(progressDlg);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Complain if the context is not valid for the tool that is loading the styles.
		/// Flex currently allows general styles and its own special one.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="styleName"></param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		protected override void ValidateContext(ContextValues context, string styleName)
		{
			if (context != ContextValues.InternalConfigureView &&
				context != ContextValues.Internal &&
				context != ContextValues.General)
				ReportInvalidInstallation(String.Format(
					"Style {0} is illegally defined with context '{1}' in {2}.",
					styleName, context, ResourceFileName));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether the given style is (possibly) in use.
		/// </summary>
		/// <remarks>This override is needed because previously hotlinks in FLEx were
		/// not have made good use of the InUse property of styles.</remarks>
		/// <param name="style">The style.</param>
		/// <returns><c>true</c> if there is any reasonable chance the given style is in use
		/// somewhere in the project data; <c>false</c> if the style has never been used and
		/// there is no real possibility it could be in the data.</returns>
		/// ------------------------------------------------------------------------------------
		protected override bool StyleIsInUse(IStStyle style)
		{
			return (style.Name == "External Link" || base.StyleIsInUse(style));
		}

		/// <summary>
		/// Set the properties of a StyleInfo to the factory default settings
		/// </summary>
		public void SetPropsToFactorySettings(StyleInfo styleInfo)
		{
			ResetProps(styleInfo);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Loads the settings file and checks the DTD version.
		/// </summary>
		/// <returns>The root node</returns>
		/// ------------------------------------------------------------------------------------
		protected override XmlNode LoadDoc(string xmlLocation = null)
		{
			return base.LoadDoc(m_sourceDocumentPath);
		}

		public XmlSchema GetSchema()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Currently the reading is handled by CreateStyles
		/// </summary>
		/// <param name="reader"></param>
		public void ReadXml(XmlReader reader)
		{
			throw new NotImplementedException();
		}

		public void WriteXml(XmlWriter writer)
		{
			writer.WriteAttributeString("DTDver", DtdRequiredVersion);
			writer.WriteAttributeString("label", "Flex Dictionary");
			writer.WriteAttributeString("date", DateTime.UtcNow.ToString("yyyy-MM-dd"));
			writer.WriteStartElement("markup");
			writer.WriteAttributeString("version", GetVersion(m_sourceStyles).ToString());
			foreach (var style in StyleCollection)
			{
				if (DictionaryConfigurationImportController.UnsupportedStyles.Contains(style.Name))
					continue;
				var exportStyle = new ExportStyleInfo(style, style.Rules);
				WriteStyleXml(exportStyle, writer);
			}
			writer.WriteEndElement(); // markup
		}

		private void WriteStyleXml(ExportStyleInfo style, XmlWriter writer)
		{
			writer.WriteStartElement("tag");
			writer.WriteAttributeString("id", GetStyleId(style));
			writer.WriteAttributeString("guid", style.RealStyle.Guid.ToString());
			writer.WriteAttributeString("userlevel", style.UserLevel.ToString());
			writer.WriteAttributeString("context", GetStyleContext(style));
			writer.WriteAttributeString("type", GetStyleType(style));

			if (GetStyleType(style) == "character" && style.InheritsFrom != null)
			{
				// LT-18267 Character styles put their basedOn in a different place
				// than paragraph styles.
				writer.WriteAttributeString("basedOn", GetStyleId(style.InheritsFrom));
			}

			WriteUsageElement(style.RealStyle.Usage, writer);
			WriteFontAndParagraphRulesXml(style, writer, style.InheritsFrom, style.NextStyle);
			writer.WriteEndElement(); // tag
		}

		private static string GetStyleType(ExportStyleInfo style)
		{
			switch (style.RealStyle.Type)
			{
				case StyleType.kstCharacter:
					return "character";
				case StyleType.kstParagraph:
					return "paragraph";
			}
			return style.RealStyle.Type.ToString();
		}

		///<remarks>The first letter for the context is supposed to be lower case</remarks>
		private static string GetStyleContext(ExportStyleInfo style)
		{
			var contextString = style.Context.ToString();
			if(string.IsNullOrEmpty(contextString))
				throw new ArgumentException("The context in the style is invalid", "style");
			return contextString.Substring(0, 1).ToLowerInvariant() + contextString.Substring(1);
		}

		private void WriteUsageElement(IMultiUnicode styleUsage, XmlWriter writer)
		{
			foreach (var wsId in styleUsage.AvailableWritingSystemIds)
			{
				writer.WriteStartElement("usage");
				writer.WriteAttributeString("wsId", Cache.WritingSystemFactory.GetStrFromWs(wsId));
				writer.WriteString(styleUsage.get_String(wsId).Text);
				writer.WriteEndElement(); // usage
			}
		}

		private void WriteFontAndParagraphRulesXml(ExportStyleInfo style, XmlWriter writer, string basedOnStyle, BaseStyleInfo nextStyle)
		{
			if (style.FontInfoForWs(-1) == null)
			{
				writer.WriteStartElement("font");
				writer.WriteEndElement();
				return;
			}
			// Generate the font info (the font element is required by the DTD even if it has no attributes)
			writer.WriteStartElement("font");
			IEnumerable<Tuple<string, string>> fontProps = CollectFontProps(style.FontInfoForWs(-1));
			if (fontProps.Any())
			{
				foreach (var prop in fontProps)
				{
					writer.WriteAttributeString(prop.Item1, prop.Item2);
				}
			}
			foreach (var writingSystem in Cache.LangProject.AllWritingSystems)
			{
				var wsOverrideProps = CollectFontProps(style.FontInfoForWs(writingSystem.Handle));
				if (wsOverrideProps.Any())
				{
					writer.WriteStartElement("override");
					writer.WriteAttributeString("wsId", writingSystem.LanguageTag);
					foreach (var prop in wsOverrideProps)
					{
						writer.WriteAttributeString(prop.Item1, prop.Item2);
					}
					writer.WriteEndElement();
				}
			}
			writer.WriteEndElement(); // font
			IEnumerable<Tuple<string, string>> paragraphProps = CollectParagraphProps(style, basedOnStyle, nextStyle);
			if (paragraphProps.Any())
			{
				writer.WriteStartElement("paragraph");
				foreach (var prop in paragraphProps)
				{
					writer.WriteAttributeString(prop.Item1, prop.Item2);
				}
				writer.WriteEndElement(); // paragraph
			}
		}

		/// <summary>
		/// Collects the font info for the style in tuples of attribute name, attribute value
		/// </summary>
		private IEnumerable<Tuple<string, string>> CollectFontProps(FontInfo styleRules)
		{
			var fontProperties = new List<Tuple<string, string>>();
			GetPointPropAttribute("size", styleRules.FontSize, fontProperties);
			if (styleRules.FontName.ValueIsSet)
			{
				fontProperties.Add(new Tuple<string, string>("family", styleRules.FontName.Value));
			}
			if (styleRules.Bold.ValueIsSet)
			{
				fontProperties.Add(new Tuple<string, string>("bold", styleRules.Bold.Value.ToString().ToLowerInvariant()));
			}
			if (styleRules.Italic.ValueIsSet)
			{
				fontProperties.Add(new Tuple<string, string>("italic", styleRules.Italic.Value.ToString().ToLowerInvariant()));
			}
			GetColorValueAttribute("color", styleRules.FontColor, fontProperties);
			GetColorValueAttribute("underlineColor", styleRules.UnderlineColor, fontProperties);
			if (styleRules.Underline.ValueIsSet)
			{
				string underLineValue;
				switch (styleRules.Underline.Value)
				{
					case FwUnderlineType.kuntStrikethrough:
						underLineValue = "strikethrough";
						break;
					case FwUnderlineType.kuntSingle:
						underLineValue = "single";
						break;
					case FwUnderlineType.kuntDouble:
						underLineValue = "double";
						break;
					case FwUnderlineType.kuntDashed:
						underLineValue = "dashed";
						break;
					case FwUnderlineType.kuntDotted:
						underLineValue = "dotted";
						break;
					case FwUnderlineType.kuntSquiggle:
						underLineValue = "squiggle";
						break;
					default:
						underLineValue = "none";
						break;
				}
				fontProperties.Add(new Tuple<string, string>("underline", underLineValue));
			}
			return fontProperties;
		}

		/// <summary>
		/// Collects the paragraph info for the style in tuples of attribute name, attribute value
		/// </summary>
		private IEnumerable<Tuple<string, string>> CollectParagraphProps(ExportStyleInfo styleRules, string basedOnStyle, BaseStyleInfo nextStyle)
		{
			var paragraphProps = new List<Tuple<string, string>>();
			GetPointPropAttribute((int)FwTextPropType.ktptSpaceBefore, "spaceBefore", styleRules.RealStyle.Rules, paragraphProps);
			GetPointPropAttribute((int)FwTextPropType.ktptSpaceAfter, "spaceAfter", styleRules.RealStyle.Rules, paragraphProps);
			GetPointPropAttribute((int)FwTextPropType.ktptLeadingIndent, "indentLeft", styleRules.RealStyle.Rules, paragraphProps);
			GetPointPropAttribute((int)FwTextPropType.ktptTrailingIndent, "indentRight", styleRules.RealStyle.Rules, paragraphProps);
			GetColorValueAttribute((int)FwTextPropType.ktptBackColor, "background", styleRules.RealStyle.Rules, paragraphProps);
			if (basedOnStyle != null)
			{
				paragraphProps.Add(new Tuple<string, string>("basedOn", GetStyleId(basedOnStyle)));
			}
			if (nextStyle != null)
			{
				paragraphProps.Add(new Tuple<string, string>("next", GetStyleId(nextStyle)));
			}
			if (styleRules.HasFirstLineIndent)
			{
				// hanging and firstLine are stored in an overloaded property value, negative for hanging, positive for firstline
				if (styleRules.FirstLineIndent < 0)
				{
					paragraphProps.Add(new Tuple<string, string>("hanging", -(styleRules.FirstLineIndent / 1000) + " pt"));
				}
				else
				{
					paragraphProps.Add(new Tuple<string, string>("firstLine", styleRules.FirstLineIndent / 1000 + " pt"));
				}
			}
			if (styleRules.HasAlignment)
			{
				var alignment = styleRules.Alignment;
				string alignValue = "none";
				switch (alignment)
				{
					case FwTextAlign.ktalCenter:
						alignValue = "center";
						break;
					case FwTextAlign.ktalLeft:
						alignValue = "left";
						break;
					case FwTextAlign.ktalRight:
						alignValue = "right";
						break;
					case FwTextAlign.ktalJustify:
						alignValue = "full";
						break;
				}
				paragraphProps.Add(new Tuple<string, string>("alignment", alignValue));
			}
			if (styleRules.HasLineSpacing)
			{
				string lineSpaceType;
				// relative is used for single, 1.5, double space
				if (styleRules.LineSpacing.m_relative)
				{
					lineSpaceType = "rel";
				}
				else if (styleRules.LineSpacing.m_lineHeight <= 0)
				{
					// for historical reasons negative values mean exact, and positive mean at least
					// (see: Framework\StylesXmlAccessor.cs SetParagraphProperties())
					lineSpaceType = "exact";
				}
				else
				{
					lineSpaceType = "atleast";
				}
				var lineSpace = Math.Abs(styleRules.LineSpacing.m_lineHeight) / 1000 + " pt";
				paragraphProps.Add(new Tuple<string, string>("lineSpacing", lineSpace));
				paragraphProps.Add(new Tuple<string, string>("lineSpacingType", lineSpaceType));
			}

			return paragraphProps;
		}

		/// <summary>
		/// Takes the property identifier integer and the attribute name that we want to use in the xml and generates a tuple
		/// with the attribute name and value if this property is set in the style rules. This method assumes the property is
		/// for a size value stored in millipoints.
		/// </summary>
		private static void GetPointPropAttribute(int property, string attributeName, ITsTextProps styleRules,
			List<Tuple<string, string>> resultsList)
		{
			if (styleRules == null)
				return;
			int propValue;
			int hasProperty;
			propValue = styleRules.GetIntPropValues(property, out hasProperty);
			if (hasProperty != -1)
			{
				resultsList.Add(new Tuple<string, string>(attributeName, propValue / 1000 + " pt"));
			}
		}

		/// <summary>
		/// Takes the attribute name we want to use in the xml and uses the style prop for a point property and generates
		/// a tuple with the attribute name and value if this property is set.
		/// </summary>
		private void GetPointPropAttribute(string attributeName, IStyleProp<int> sizeProp, List<Tuple<string, string>> resultsList)
		{
			if (sizeProp.ValueIsSet)
			{
				resultsList.Add(new Tuple<string, string>(attributeName, sizeProp.Value / 1000 + " pt"));
			}
		}

		/// <summary>
		/// Takes the property identifier integer  and the attribute name that we want to use in the xml and generates a tuple
		/// with the attribute name and value if this property is set in the style rules. This method assumes the property
		/// is for a color value.
		/// </summary>
		private static void GetColorValueAttribute(int property, string attributeName, ITsTextProps styleRules,
			List<Tuple<string, string>> resultsList)
		{
			if (styleRules == null)
				return;
			int hasColor;
			var colorValueBGR = styleRules.GetIntPropValues(property, out hasColor);
			if (hasColor != -1)
			{
				var color = Color.FromArgb((int)ColorUtil.ConvertRGBtoBGR((uint)colorValueBGR)); // convert BGR to RGB
				GetColorValueFromSystemColor(attributeName, resultsList, color);
			}
		}

		private void GetColorValueAttribute(string attributeName, IStyleProp<Color> fontColor, List<Tuple<string, string>> resultsList)
		{
			if (fontColor.ValueIsSet)
			{
				var color = fontColor.Value;
				GetColorValueFromSystemColor(attributeName, resultsList, color);
			}
		}

		/// <summary>
		/// Takes a system color and writes out a string if it is a known color, or an RGB value that the import code can read
		/// </summary>
		private static void GetColorValueFromSystemColor(string attributeName, List<Tuple<string, string>> resultsList, Color color)
		{
			var colorString = color.IsKnownColor
				? color.Name.ToLowerInvariant()
				: string.Format("({0},{1},{2})", color.R, color.G, color.B);
			resultsList.Add(new Tuple<string, string>(attributeName, colorString));
		}

		/// <summary>
		/// Converts the style name into the 'id' attribute expected by the code that reads in stylesheet files
		/// </summary>
		private static string GetStyleId(BaseStyleInfo style) { return GetStyleId(style.Name); }

		/// <summary>
		/// Converts the style name into the 'id' attribute expected by the code that reads in stylesheet files
		/// </summary>
		private static string GetStyleId(string styleName)
		{
			return styleName.Replace(' ', '_');
		}
	}
}
