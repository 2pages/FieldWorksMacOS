﻿// Copyright (c) 2014-2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ExCSS;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.Common.Framework;
using SIL.FieldWorks.Common.Widgets;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.DomainServices;
using XCore;
using Property = ExCSS.Property;

namespace SIL.FieldWorks.XWorks
{
	public static class CssGenerator
	{
		/// <summary>
		/// id that triggers using the default selection on a character style instead of a writing system specific one
		/// </summary>
		internal const int DefaultStyle = -1;

		internal const string BeforeAfterBetweenStyleName = "Dictionary-Context";
		internal const string LetterHeadingStyleName = "Dictionary-LetterHeading";
		private static readonly Dictionary<string, string> BulletSymbolsCollection = new Dictionary<string, string>();
		/// <summary>
		/// Generate all the css rules necessary to represent every enabled portion of the given configuration
		/// </summary>
		/// <param name="model"></param>
		/// <param name="propertyTable">Necessary to access the styles as configured in FLEx</param>
		/// <returns></returns>
		public static string GenerateCssFromConfiguration(DictionaryConfigurationModel model, PropertyTable propertyTable)
		{
			if(model == null)
				throw new ArgumentNullException("model");
			var styleSheet = new StyleSheet();
			var mediatorstyleSheet = FontHeightAdjuster.StyleSheetFromPropertyTable(propertyTable);
			var cache = propertyTable.GetValue<FdoCache>("cache");
			LoadBulletUnicodes();
			if (mediatorstyleSheet != null && mediatorstyleSheet.Styles.Contains("Normal"))
				GenerateCssForWsSpanWithNormalStyle(styleSheet, propertyTable, cache);
			MakeLinksLookLikePlainText(styleSheet);
			GenerateCssForAudioWs(styleSheet, cache);
			foreach(var configNode in model.Parts)
			{
				GenerateCssFromConfigurationNode(configNode, styleSheet, null, propertyTable);
			}
			// Pretty-print the stylesheet
			return styleSheet.ToString(true, 1);
		}

		private static void MakeLinksLookLikePlainText(StyleSheet styleSheet)
		{
			var rule = new StyleRule { Value = "a" };
			rule.Declarations.Properties.AddRange(new [] {
				new Property("text-decoration") { Term = new PrimitiveTerm(UnitType.Attribute, "inherit") },
				new Property("color") { Term = new PrimitiveTerm(UnitType.Attribute, "inherit") }
			});
			styleSheet.Rules.Add(rule);
		}

		private static void GenerateCssForWsSpanWithNormalStyle(StyleSheet styleSheet, PropertyTable propertyTable, FdoCache cache)
		{
			foreach (var aws in cache.ServiceLocator.WritingSystems.AllWritingSystems)
			{
				var wsRule = new StyleRule { Value = "span" + String.Format("[lang|=\"{0}\"]", aws.LanguageTag) };
				wsRule.Declarations.Properties.AddRange(GenerateCssStyleFromFwStyleSheet("Normal", aws.Handle, propertyTable));
				styleSheet.Rules.Add(wsRule);
			}
		}

		private static void GenerateCssForAudioWs(StyleSheet styleSheet, FdoCache cache)
		{
			foreach (var aws in cache.ServiceLocator.WritingSystems.AllWritingSystems)
			{
				if (aws.LanguageTag.Contains("audio"))
				{
					var wsaudioRule = new StyleRule {Value = String.Format("a.{0}:after", aws.LanguageTag)};
					wsaudioRule.Declarations.Properties.Add(new Property("content")
					{
						Term = new PrimitiveTerm(UnitType.String, "\uD83D\uDD0A")
					});
					styleSheet.Rules.Add(wsaudioRule);
					wsaudioRule = new StyleRule {Value = String.Format("a.{0}", aws.LanguageTag)};
					wsaudioRule.Declarations.Properties.Add(new Property("text-decoration")
					{
						Term = new PrimitiveTerm(UnitType.Attribute, "none")
					});
					styleSheet.Rules.Add(wsaudioRule);
				}
			}
		}

		/// <summary>
		/// Generates css rules for a configuration node and adds them to the given stylesheet (recursive).
		/// </summary>
		private static void GenerateCssFromConfigurationNode(ConfigurableDictionaryNode configNode,
																			  StyleSheet styleSheet,
																			  string baseSelection,
																			  PropertyTable propertyTable)
		{
			var rule = new StyleRule();
			var senseOptions = configNode.DictionaryNodeOptions as DictionaryNodeSenseOptions;
			if(senseOptions != null)
			{
				// Try to generate the css for the sense number before the baseSelection is updated because
				// the sense number is a sibling of the sense element and we are normally applying styles to the
				// children of collections. Also set display:block on span
				GenerateCssForSenses(configNode, senseOptions, styleSheet, ref baseSelection, propertyTable);
			}
			else
			{
				var complexFormOpts = configNode.DictionaryNodeOptions as DictionaryNodeComplexFormOptions;
				if(complexFormOpts != null)
				{
					// Try to generate the css for the sense number before the baseSelection is updated because
					// the sense number is a sibling of the sense element and we are normally applying styles to the
					// children of collections.
					GenerateCssFromComplexFormOptions(configNode, complexFormOpts, styleSheet, baseSelection);
				}
				var pictureOptions = configNode.DictionaryNodeOptions as DictionaryNodePictureOptions;
				if(pictureOptions != null)
				{
					GenerateCssFromPictureOptions(configNode, pictureOptions, styleSheet, baseSelection, propertyTable);
				}
				var beforeAfterSelectors = GenerateSelectorsFromNode(baseSelection, configNode, out baseSelection, propertyTable.GetValue<FdoCache>("cache"), propertyTable);
				rule.Value = baseSelection;
				// if the configuration node defines a style then add all the rules generated from that style
				if(!String.IsNullOrEmpty(configNode.Style))
				{
					//Generate the rules for the default font info
					rule.Declarations.Properties.AddRange(GenerateCssStyleFromFwStyleSheet(configNode.Style, DefaultStyle, propertyTable));
				}
				var wsOptions = configNode.DictionaryNodeOptions as DictionaryNodeWritingSystemOptions;
				if(wsOptions != null)
				{
					GenerateCssFromWsOptions(configNode, wsOptions, styleSheet, baseSelection, propertyTable);
					if(wsOptions.DisplayWritingSystemAbbreviations)
					{
						GenerateCssForWritingSystemPrefix(styleSheet, baseSelection);
					}
					if(wsOptions.Options.Count(s => s.IsEnabled) > 1)
					{
						GenerateCssForFieldWithMultipleWs(styleSheet, baseSelection);
					}
				}
				styleSheet.Rules.AddRange(beforeAfterSelectors);
				styleSheet.Rules.Add(rule);
			}
			if(configNode.Children == null)
				return;
			//Recurse into each child
			foreach(var child in configNode.Children)
			{
				GenerateCssFromConfigurationNode(child, styleSheet, baseSelection, propertyTable);
			}
		}

		private static void GenerateCssForFieldWithMultipleWs(StyleSheet styleSheet, string baseSelection)
		{
			var glossRule = new StyleRule {Value = baseSelection + ":not(:last-child):after"};
			glossRule.Declarations.Properties.Add(new Property("content") {Term = new PrimitiveTerm(UnitType.String, " ")});
			styleSheet.Rules.Add(glossRule);
		}

		private static void GenerateCssForSenses(ConfigurableDictionaryNode configNode, DictionaryNodeSenseOptions senseOptions,
														StyleSheet styleSheet, ref string baseSelection, PropertyTable propertyTable)
		{
			var beforeAfterSelectors = GenerateSelectorsFromNode(baseSelection, configNode, out baseSelection, propertyTable.GetValue<FdoCache>("cache"), propertyTable);
			var senseContentSelector = string.Format("{0}> .sensecontent", baseSelection.Substring(0, baseSelection.LastIndexOf(".sense", StringComparison.Ordinal)));
			styleSheet.Rules.AddRange(beforeAfterSelectors);

			var senseNumberRule = new StyleRule();
			// Not using SelectClassName here; sense and sensenumber are siblings and the configNode is for the Senses collection.
			// Select the base plus the node's unmodified class attribute and append the sensenumber matcher.
			var senseNumberSelector = string.Format("{0} .sensenumber", senseContentSelector);

			senseNumberRule.Value = senseNumberSelector;
			if(!String.IsNullOrEmpty(senseOptions.NumberStyle))
			{
				senseNumberRule.Declarations.Properties.AddRange(GenerateCssStyleFromFwStyleSheet(senseOptions.NumberStyle, DefaultStyle, propertyTable));
			}
			styleSheet.Rules.Add(senseNumberRule);
			if(!String.IsNullOrEmpty(senseOptions.BeforeNumber))
			{
				var beforeDeclaration = new StyleDeclaration
				{
					new Property("content") { Term = new PrimitiveTerm(UnitType.String, senseOptions.BeforeNumber) }
				};
				styleSheet.Rules.Add(new StyleRule(beforeDeclaration) { Value = senseNumberSelector + ":before" });
			}
			if(!String.IsNullOrEmpty(senseOptions.AfterNumber))
			{
				var afterDeclaration = new StyleDeclaration();
				afterDeclaration.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.String, senseOptions.AfterNumber) });
				var afterRule = new StyleRule(afterDeclaration) { Value = senseNumberSelector + ":after" };
				styleSheet.Rules.Add(afterRule);
			}
			var styleDeclaration = string.IsNullOrEmpty(configNode.Style) ? new StyleDeclaration() : GenerateCssStyleFromFwStyleSheet(configNode.Style, 0, propertyTable);
			if(senseOptions.DisplayEachSenseInAParagraph)
			{
				var senseParaDeclaration = GetOnlyParagraphStyle(styleDeclaration);
				senseParaDeclaration.Add(new Property("display")
				{
					Term = new PrimitiveTerm(UnitType.Ident, "block")
				});
				var senseParaRule = new StyleRule(senseParaDeclaration)
				{
					// Apply the paragraph style information to all but the first sensecontent block
					Value = string.Format("{0} + {1}", senseContentSelector, ".sensecontent")
				};
				styleSheet.Rules.Add(senseParaRule);
				GenerateCssforBulletedList(configNode, styleSheet, senseContentSelector, propertyTable);
			}
			// Generate the style information specifically for senses
			var senseContentRule = new StyleRule(GetOnlyCharacterStyle(styleDeclaration))
			{
				Value = string.Format(baseSelection)
			};
			styleSheet.Rules.Add(senseContentRule);

			if (senseOptions.ShowSharedGrammarInfoFirst)
			{
				var blockDeclaration = new StyleDeclaration();

				blockDeclaration.Add(new Property("font-style") { Term = new PrimitiveTerm(UnitType.Attribute, "italic") });
				var blockRule1 = new StyleRule(blockDeclaration)
				{
					Value = String.Format("{0} .{1}> .sharedgrammaticalinfo", baseSelection, GetClassAttributeForConfig(configNode))
				};
				styleSheet.Rules.Add(blockRule1);

				blockDeclaration.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.String, " ") });
				var blockRule2 = new StyleRule(blockDeclaration)
				{
					Value = String.Format("{0} .{1}> .sharedgrammaticalinfo:after", baseSelection, GetClassAttributeForConfig(configNode))
				};
				styleSheet.Rules.Add(blockRule2);
			}
		}

		/// <summary>
		/// Generates Bulleted List style properties
		/// </summary>
		/// <param name="configNode">Dictionary Node</param>
		/// <param name="styleSheet">Stylesheet to add the new rule</param>
		/// <param name="bulletSelector">Style name for the bullet property</param>
		/// <param name="mediator">mediator to get the styles</param>
		private static void GenerateCssforBulletedList(ConfigurableDictionaryNode configNode, StyleSheet styleSheet, string bulletSelector, PropertyTable propertyTable)
		{
			if (configNode.Style != null)
			{
				var bulletRule = new StyleRule { Value = bulletSelector + ":before" };
				bulletRule.Declarations.Properties.AddRange(GenerateCssStyleFromFwStyleSheet(configNode.Style, DefaultStyle, propertyTable));
				var projectStyles = FontHeightAdjuster.StyleSheetFromPropertyTable(propertyTable);
				BaseStyleInfo projectStyle = projectStyles.Styles[configNode.Style];
				var exportStyleInfo = new ExportStyleInfo(projectStyle);
				if (exportStyleInfo.NumberScheme != 0)
				{
					var wsFontInfo = exportStyleInfo.BulletInfo.FontInfo;
					bulletRule.Declarations.Add(new Property("font-size") { Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(wsFontInfo.FontSize.Value)) });
					bulletRule.Declarations.Add(new Property("color") { Term = new PrimitiveTerm(UnitType.RGB, wsFontInfo.FontColor.Value.Name) });
				}
				styleSheet.Rules.Add(bulletRule);
			}
		}

		private static void GenerateCssFromComplexFormOptions(ConfigurableDictionaryNode configNode, DictionaryNodeComplexFormOptions complexFormOpts, StyleSheet styleSheet, string baseSelection)
		{
			if(complexFormOpts.DisplayEachComplexFormInAParagraph)
			{
				var blockDeclaration = new StyleDeclaration();
				blockDeclaration.Add(new Property("display") { Term = new PrimitiveTerm(UnitType.Ident, "block") });
				var blockRule = new StyleRule(blockDeclaration)
				{
					Value = baseSelection + " " + SelectClassName(configNode)
				};
				styleSheet.Rules.Add(blockRule);
			}
		}

		private static void GenerateCssFromWsOptions(ConfigurableDictionaryNode configNode, DictionaryNodeWritingSystemOptions wsOptions,
																	StyleSheet styleSheet, string baseSelection, PropertyTable propertyTable)
		{
			var cache = propertyTable.GetValue<FdoCache>("cache");
			foreach(var ws in wsOptions.Options)
			{
				var possiblyMagic = WritingSystemServices.GetMagicWsIdFromName(ws.Id);
				// if the writing system isn't a magic name just use it otherwise find the right one from the magic list
				var wsIdString = possiblyMagic == 0 ? ws.Id : WritingSystemServices.GetWritingSystemList(cache, possiblyMagic, true).First().Id;
				var wsId = cache.LanguageWritingSystemFactoryAccessor.GetWsFromStr(wsIdString);
				var wsRule = new StyleRule {Value = baseSelection + String.Format("[lang|=\"{0}\"]", wsIdString)};
				if (!String.IsNullOrEmpty(configNode.Style))
					wsRule.Declarations.Properties.AddRange(GenerateCssStyleFromFwStyleSheet(configNode.Style, wsId, propertyTable));
				styleSheet.Rules.Add(wsRule);
			}
		}

		private static void GenerateCssForWritingSystemPrefix(StyleSheet styleSheet, string baseSelection)
		{
			var wsRule1 = new StyleRule {Value = baseSelection + ".writingsystemprefix"};
			wsRule1.Declarations.Properties.Add(new Property("font-style") {Term = new PrimitiveTerm(UnitType.Attribute, "normal")});
			wsRule1.Declarations.Properties.Add(new Property("font-size") {Term = new PrimitiveTerm(UnitType.Point, 10)});
			styleSheet.Rules.Add(wsRule1);
			var wsRule2=new StyleRule {Value = wsRule1.Value + ":after"};
			wsRule2.Declarations.Properties.Add(new Property("content"){Term = new PrimitiveTerm(UnitType.String, " ")});
			styleSheet.Rules.Add(wsRule2);
		}

		private static void GenerateCssFromPictureOptions(ConfigurableDictionaryNode configNode, DictionaryNodePictureOptions pictureOptions,
																		  StyleSheet styleSheet, string baseSelection, PropertyTable propertyTable)
		{
			var cache = propertyTable.GetValue<FdoCache>("cache");
			var pictureAndCaptionRule = new StyleRule();
			pictureAndCaptionRule.Value = baseSelection + " " + SelectClassName(configNode);

			var pictureProps = pictureAndCaptionRule.Declarations.Properties;
			pictureProps.Add(new Property("float") { Term = new PrimitiveTerm(UnitType.Ident, "right") });
			pictureProps.Add(new Property("text-align") { Term = new PrimitiveTerm(UnitType.Ident, "center") });
			var margin = new Property("margin");
			margin.Term = new TermList(new PrimitiveTerm(UnitType.Point, 0),
												new PrimitiveTerm(UnitType.Point, 0),
												new PrimitiveTerm(UnitType.Point, 4),
												new PrimitiveTerm(UnitType.Point, 4));
			pictureProps.Add(margin);
			pictureProps.Add(new Property("padding") { Term = new PrimitiveTerm(UnitType.Point, 2) });
			pictureProps.Add(new Property("float")
			{
				Term = new PrimitiveTerm(UnitType.Ident, pictureOptions.PictureLocation.ToString().ToLowerInvariant())
			});
			styleSheet.Rules.Add(pictureAndCaptionRule);

			var pictureRule = new StyleRule();
			pictureRule.Value = pictureAndCaptionRule.Value + " img";
			if(pictureOptions.MinimumHeight > 0)
			{
				pictureRule.Declarations.Properties.Add(new Property("min-height")
				{
					Term = new PrimitiveTerm(UnitType.Inch, pictureOptions.MinimumHeight)
				});
			}
			if(pictureOptions.MaximumHeight > 0)
			{
				pictureRule.Declarations.Properties.Add(new Property("max-height")
				{
					Term = new PrimitiveTerm(UnitType.Inch, pictureOptions.MaximumHeight)
				});
			}
			if(pictureOptions.MinimumWidth > 0)
			{
				pictureRule.Declarations.Properties.Add(new Property("min-width")
				{
					Term = new PrimitiveTerm(UnitType.Inch, pictureOptions.MinimumWidth)
				});
			}
			if(pictureOptions.MaximumWidth > 0)
			{
				pictureRule.Declarations.Properties.Add(new Property("max-width")
				{
					Term = new PrimitiveTerm(UnitType.Inch, pictureOptions.MaximumWidth)
				});
			}
			styleSheet.Rules.Add(pictureRule);
		}

		/// <summary>
		/// This method will generate before and after rules if the configuration node requires them. It also generates the selector for the node
		/// </summary>
		/// <returns></returns>
		private static IEnumerable<StyleRule> GenerateSelectorsFromNode(string parentSelector,
																							 ConfigurableDictionaryNode configNode,
																							 out string baseSelection, FdoCache cache, PropertyTable propertyTable)
		{
			var rules = new List<StyleRule>();
			var fwStyles = FontHeightAdjuster.StyleSheetFromPropertyTable(propertyTable);
			if(parentSelector == null)
			{
				baseSelection = SelectClassName(configNode);
				GenerateFlowResetForBaseNode(baseSelection, rules);
			}
			else
			{

				if(!String.IsNullOrEmpty(configNode.Between))
				{
					// content is generated before each item which follows an item of the same name
					// eg. .complexformrefs>.complexformref + .complexformref:before { content: "," }
					var dec = new StyleDeclaration();
					dec.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.String, configNode.Between) });
					if (fwStyles != null && fwStyles.Styles.Contains(BeforeAfterBetweenStyleName))
						dec.Properties.AddRange(GenerateCssStyleFromFwStyleSheet(BeforeAfterBetweenStyleName, cache.DefaultAnalWs, propertyTable));
					var collectionSelector = "." + GetClassAttributeForConfig(configNode);
					var itemSelector = GetSelectorForCollectionItem(configNode);
					var betweenSelector = String.Format("{0} {1}>{2}+{2}:before", parentSelector, collectionSelector, itemSelector);
					var betweenRule = new StyleRule(dec) { Value = betweenSelector };
					if (configNode.DictionaryNodeOptions != null)
					{
						// Rule added for all span tag which only having WritingSystems attribute
						betweenSelector = String.Format("{0} {1}>{2}+{2}:before", parentSelector, collectionSelector, " span");
						betweenRule = new StyleRule(dec) { Value = betweenSelector };
					}
					rules.Add(betweenRule);
				}
				baseSelection = parentSelector + " " + SelectClassName(configNode, cache);
			}
			if(!String.IsNullOrEmpty(configNode.Before))
			{
				var dec = new StyleDeclaration();
				dec.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.String, configNode.Before) });
				if (fwStyles != null && fwStyles.Styles.Contains(BeforeAfterBetweenStyleName))
					dec.Properties.AddRange(GenerateCssStyleFromFwStyleSheet(BeforeAfterBetweenStyleName, cache.DefaultAnalWs, propertyTable));
				var beforeRule = new StyleRule(dec) { Value = baseSelection + ":first-child:before" };
				rules.Add(beforeRule);
			}
			if(!String.IsNullOrEmpty(configNode.After))
			{
				var dec = new StyleDeclaration();
				dec.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.String, configNode.After) });
				if (fwStyles != null && fwStyles.Styles.Contains(BeforeAfterBetweenStyleName))
					dec.Properties.AddRange(GenerateCssStyleFromFwStyleSheet(BeforeAfterBetweenStyleName, cache.DefaultAnalWs, propertyTable));
				var afterRule = new StyleRule(dec) { Value = baseSelection + ":last-child:after" };
				rules.Add(afterRule);
			}
			return rules;
		}

		private static void GenerateFlowResetForBaseNode(string baseSelection, List<StyleRule> rules)
		{
			var flowResetRule = new StyleRule();
			flowResetRule.Value = baseSelection;
			flowResetRule.Declarations.Properties.Add(new Property("clear") { Term = new PrimitiveTerm(UnitType.Ident, "both")});
			rules.Add(flowResetRule);
		}
		/// <summary>
		/// Generates a selector for a class name that matches xhtml that is generated for the configNode.
		/// e.g. '.entry' or '.sense'
		/// </summary>
		/// <param name="configNode"></param>
		/// <param name="cache">defaults to null, necessary for generating correct css for custom field nodes</param>
		/// <returns></returns>
		private static string SelectClassName(ConfigurableDictionaryNode configNode, FdoCache cache = null)
		{
			var type = ConfiguredXHTMLGenerator.GetPropertyTypeForConfigurationNode(configNode, cache);
			switch(type)
			{
				case ConfiguredXHTMLGenerator.PropertyType.CollectionType:
				{
					// for collections we generate a css selector to match each item e.g '.senses .sense'
					return "." + GetClassAttributeForConfig(configNode) + GetSelectorForCollectionItem(configNode);
				}
				case ConfiguredXHTMLGenerator.PropertyType.CmPictureType:
				{
					return " img"; // Pictures are written out as img tags
				}
				case ConfiguredXHTMLGenerator.PropertyType.PrimitiveType:
				{
					// for multi-lingual strings each language's string will have the contents generated in a span
					if(configNode.DictionaryNodeOptions is DictionaryNodeWritingSystemOptions)
					{
						return "." + GetClassAttributeForConfig(configNode) + " span";
					}
					goto default;
				}
				default:
					return "." + GetClassAttributeForConfig(configNode);
			}
		}

		private static string GetSelectorForCollectionItem(ConfigurableDictionaryNode configNode)
		{
			var collectionItem = GetClassAttributeForConfig(configNode);
			collectionItem = " ." + collectionItem.Remove(collectionItem.Length - 1);
			return collectionItem;
		}

		/// <summary>
		/// Generates a class name for the given configuration for use by Css and XHTML.
		/// Uses SubField and CSSClassNameOverride attributes where found
		/// </summary>
		/// <param name="configNode"></param>
		/// <returns></returns>
		internal static string GetClassAttributeForConfig(ConfigurableDictionaryNode configNode)
		{
			// write out the FieldDescription as the class name, and append a '_' followed by the SubField if it is defined.
			var classAttribute = configNode.FieldDescription +
										(String.IsNullOrEmpty(configNode.SubField) ? "" : ("_" + configNode.SubField));
			if(!String.IsNullOrEmpty(configNode.CSSClassNameOverride))
			{
					classAttribute = configNode.CSSClassNameOverride;
			}
			if (configNode.IsDuplicate)
			{
				classAttribute += "_" + configNode.LabelSuffix;
			}
			return classAttribute.ToLower();
		}

		internal static StyleDeclaration GetOnlyCharacterStyle(StyleDeclaration fullStyleDeclaration)
		{
			var declaration = new StyleDeclaration();
			foreach(var prop in fullStyleDeclaration.Where(prop => prop.Name.Contains("font") || prop.Name.Contains("color")))
			{
				declaration.Add(prop);
			}
			return declaration;
		}

		internal static StyleDeclaration GetOnlyParagraphStyle(StyleDeclaration fullStyleDeclaration)
		{
			var declaration = new StyleDeclaration();
			foreach(var prop in fullStyleDeclaration.Where(prop => !prop.Name.Contains("font") && !prop.Name.Contains("color")))
			{
				declaration.Add(prop);
			}
			return declaration;
		}

		/// <summary>
		/// Generates a css StyleDeclaration for the requested FieldWorks style.
		/// <remarks>internal to facilitate separate unit testing.</remarks>
		/// </summary>
		/// <param name="styleName"></param>
		/// <param name="wsId">writing system id</param>
		/// <param name="mediator"></param>
		/// <returns></returns>
		internal static StyleDeclaration GenerateCssStyleFromFwStyleSheet(string styleName, int wsId, PropertyTable propertyTable)
		{
			var declaration = new StyleDeclaration();
			var styleSheet = FontHeightAdjuster.StyleSheetFromPropertyTable(propertyTable);
			if(styleSheet == null || !styleSheet.Styles.Contains(styleName))
			{
				return declaration;
			}
			BaseStyleInfo projectStyle = styleSheet.Styles[styleName];
			var exportStyleInfo = new ExportStyleInfo(projectStyle);
			if(exportStyleInfo.HasAlignment)
			{
				declaration.Add(new Property("text-align") { Term = new PrimitiveTerm(UnitType.Ident, exportStyleInfo.Alignment.AsCssString()) });
			}
			if(exportStyleInfo.HasBorder)
			{
				if(exportStyleInfo.HasBorderColor)
				{
					var borderColor = new Property("border-color");
					borderColor.Term = new HtmlColor(exportStyleInfo.BorderColor.A,
																exportStyleInfo.BorderColor.R,
																exportStyleInfo.BorderColor.G,
																exportStyleInfo.BorderColor.B);
					declaration.Add(borderColor);
				}
				var borderLeft = new Property("border-left-width");
				borderLeft.Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.BorderLeading));
				var borderRight = new Property("border-right-width");
				borderRight.Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.BorderTrailing));
				var borderTop = new Property("border-top-width");
				borderTop.Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.BorderTop));
				var borderBottom = new Property("border-bottom-width");
				borderBottom.Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.BorderBottom));
				declaration.Add(borderLeft);
				declaration.Add(borderRight);
				declaration.Add(borderTop);
				declaration.Add(borderBottom);
			}
			if(exportStyleInfo.HasFirstLineIndent)
			{
				//Handles both first-line and hanging indent, hanging-indent will result in a negative text-indent value
				declaration.Add(new Property("text-indent") { Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.FirstLineIndent)) });
			}
			if(exportStyleInfo.HasKeepTogether)
			{
				throw new NotImplementedException("Keep Together style export not yet implemented.");
			}
			if(exportStyleInfo.HasKeepWithNext)
			{
				throw new NotImplementedException("Keep With Next style export not yet implemented.");
			}
			if(exportStyleInfo.HasLeadingIndent)
			{
				declaration.Add(new Property("padding-left") { Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.LeadingIndent)) });
			}
			if(exportStyleInfo.HasLineSpacing)
			{
				var lineHeight = new Property("line-height");
				//m_relative means single, 1.5 or double line spacing was chosen. The CSS should be a number
				if(exportStyleInfo.LineSpacing.m_relative)
				{
					lineHeight.Term = new PrimitiveTerm(UnitType.Number, exportStyleInfo.LineSpacing.m_lineHeight);
				}
				else
				{
					lineHeight.Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.LineSpacing.m_lineHeight));
				}
				declaration.Add(lineHeight);
			}
			if(exportStyleInfo.HasSpaceAfter)
			{
				declaration.Add(new Property("padding-bottom") { Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.SpaceAfter)) });
			}
			if(exportStyleInfo.HasSpaceBefore)
			{
				declaration.Add(new Property("padding-top") { Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.SpaceBefore)) });
			}
			if(exportStyleInfo.HasTrailingIndent)
			{
				declaration.Add(new Property("padding-right") { Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.TrailingIndent)) });
			}

			AddFontInfoCss(projectStyle, declaration, wsId);

			if (exportStyleInfo.NumberScheme != 0)
			{
				var numScheme = exportStyleInfo.NumberScheme.ToString();
				if (BulletSymbolsCollection.ContainsKey(exportStyleInfo.NumberScheme.ToString()))
				{
					string selectedBullet = BulletSymbolsCollection[numScheme];
					declaration.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.String, selectedBullet) });
				}
			}
			return declaration;
		}

		/// <summary>
		/// Mapping the bullet symbols with the number system
		/// </summary>
		private static void LoadBulletUnicodes()
		{
			if (BulletSymbolsCollection.Count > 0)
				return;

			BulletSymbolsCollection.Add("kvbnBulletBase", "\\00B7");
			BulletSymbolsCollection.Add("101", "\\2022");
			BulletSymbolsCollection.Add("102", "\\25CF");
			BulletSymbolsCollection.Add("103", "\\274D");
			BulletSymbolsCollection.Add("104", "\\25AA");
			BulletSymbolsCollection.Add("105", "\\25A0");
			BulletSymbolsCollection.Add("106", "\\25AB");
			BulletSymbolsCollection.Add("107", "\\25A1");
			BulletSymbolsCollection.Add("108", "\\2751");
			BulletSymbolsCollection.Add("109", "\\2752");
			BulletSymbolsCollection.Add("110", "\\2B27");
			BulletSymbolsCollection.Add("111", "\\29EB");
			BulletSymbolsCollection.Add("112", "\\25C6");
			BulletSymbolsCollection.Add("113", "\\2756");
			BulletSymbolsCollection.Add("114", "\\2318");
			BulletSymbolsCollection.Add("115", "\\261E");
			BulletSymbolsCollection.Add("116", "\\271E");
			BulletSymbolsCollection.Add("117", "\\271E");
			BulletSymbolsCollection.Add("118", "\\2730");
			BulletSymbolsCollection.Add("119", "\\27A2");
			BulletSymbolsCollection.Add("120", "\\27B2");
			BulletSymbolsCollection.Add("121", "\\2794");
			BulletSymbolsCollection.Add("122", "\\2794");
			BulletSymbolsCollection.Add("123", "\\21E8");
			BulletSymbolsCollection.Add("124", "\\2713");
		}



		/// <summary>
		/// In the FwStyles values were stored in millipoints to avoid expensive floating point calculations in c++ code.
		/// We need to convert these to points for use in css styles.
		/// </summary>
		private static float MilliPtToPt(int millipoints)
		{
			return (float)millipoints / 1000;
		}

		/// <summary>
		/// Builds the css rules for font info properties using the writing system overrides
		/// </summary>
		/// <param name="projectStyle"></param>
		/// <param name="declaration"></param>
		/// <param name="wsId">writing system id</param>
		private static void AddFontInfoCss(BaseStyleInfo projectStyle, StyleDeclaration declaration, int wsId)
		{
			var wsFontInfo = projectStyle.FontInfoForWs(wsId);
			var defaultFontInfo = projectStyle.DefaultCharacterStyleInfo;
			// set fontName to the wsFontInfo value if set, otherwise the defaultFontInfo if set, or null
			var fontName = wsFontInfo.FontName.ValueIsSet ? wsFontInfo.FontName.Value
																		 : defaultFontInfo.FontName.ValueIsSet ? defaultFontInfo.FontName.Value : null;
			if(fontName != null)
			{
				var fontFamily = new Property("font-family");
				fontFamily.Term =
					new TermList(
						new PrimitiveTerm(UnitType.String, fontName),
						new PrimitiveTerm(UnitType.Ident, "serif"));
				declaration.Add(fontFamily);
			}

			AddInfoFromWsOrDefaultValue(wsFontInfo.FontSize, defaultFontInfo.FontSize, "font-size", UnitType.Point, declaration);
			AddInfoFromWsOrDefaultValue(wsFontInfo.Bold, defaultFontInfo.Bold, "font-weight", "bold", "normal", declaration);
			AddInfoFromWsOrDefaultValue(wsFontInfo.Italic, defaultFontInfo.Italic, "font-style", "italic", "normal", declaration);
			AddInfoFromWsOrDefaultValue(wsFontInfo.FontColor, defaultFontInfo.FontColor, "color", declaration);
			AddInfoFromWsOrDefaultValue(wsFontInfo.BackColor, defaultFontInfo.BackColor, "background-color", declaration);
			AddInfoFromWsOrDefaultValue(wsFontInfo.SuperSub, defaultFontInfo.SuperSub, "vertical-align", declaration);
			AddInfoForUnderline(wsFontInfo, defaultFontInfo, declaration);
		}

		/// <summary>
		/// Generates css from boolean style values using writing system overrides where appropriate
		/// </summary>
		/// <param name="wsFontInfo"></param>
		/// <param name="defaultFontInfo"></param>
		/// <param name="propName"></param>
		/// <param name="trueValue"></param>
		/// <param name="falseValue"></param>
		/// <param name="declaration"></param>
		private static void AddInfoFromWsOrDefaultValue(IStyleProp<bool> wsFontInfo,
																		IStyleProp<bool> defaultFontInfo, string propName, string trueValue,
																		string falseValue, StyleDeclaration declaration)
		{
			bool fontValue;
			if(!GetFontValue(wsFontInfo, defaultFontInfo, out fontValue))
				return;
			var fontProp = new Property(propName);
			fontProp.Term = new PrimitiveTerm(UnitType.Ident, fontValue ? trueValue : falseValue);
			declaration.Add(fontProp);
		}

		/// <summary>
		/// Generates css from Color style values using writing system overrides where appropriate
		/// </summary>
		/// <param name="wsFontInfo"></param>
		/// <param name="defaultFontInfo"></param>
		/// <param name="propName"></param>
		/// <param name="declaration"></param>
		private static void AddInfoFromWsOrDefaultValue(IStyleProp<Color> wsFontInfo,
																		IStyleProp<Color> defaultFontInfo, string propName, StyleDeclaration declaration)
		{
			Color fontValue;
			if(!GetFontValue(wsFontInfo, defaultFontInfo, out fontValue))
				return;
			var fontProp = new Property(propName);
			fontProp.Term = new PrimitiveTerm(UnitType.RGB,
														 HtmlColor.FromRgba(fontValue.R, fontValue.G, fontValue.B,
																				  fontValue.A).ToString());
			declaration.Add(fontProp);
		}

		/// <summary>
		/// Generates css from integer style values using writing system overrides where appropriate
		/// </summary>
		/// <param name="wsFontInfo"></param>
		/// <param name="defaultFontInfo"></param>
		/// <param name="propName"></param>
		/// <param name="termType"></param>
		/// <param name="declaration"></param>
		private static void AddInfoFromWsOrDefaultValue(IStyleProp<int> wsFontInfo,
																		IStyleProp<int> defaultFontInfo, string propName, UnitType termType,
																		StyleDeclaration declaration)
		{
			int fontValue;
			if(!GetFontValue(wsFontInfo, defaultFontInfo, out fontValue))
				return;
			var fontProp = new Property(propName);
			fontProp.Term = new PrimitiveTerm(termType, MilliPtToPt(fontValue));
			declaration.Add(fontProp);
		}

		/// <summary>
		/// Generates css from SuperSub style values using writing system overrides where appropriate
		/// </summary>
		/// <param name="wsFontInfo"></param>
		/// <param name="defaultFontInfo"></param>
		/// <param name="propName"></param>
		/// <param name="declaration"></param>
		private static void AddInfoFromWsOrDefaultValue(IStyleProp<FwSuperscriptVal> wsFontInfo,
																		IStyleProp<FwSuperscriptVal> defaultFontInfo, string propName, StyleDeclaration declaration)
		{
			FwSuperscriptVal fontValue;
			if(!GetFontValue(wsFontInfo, defaultFontInfo, out fontValue))
				return;
			var fontProp = new Property(propName);
			string subSuperVal = "inherit";
			switch(fontValue)
			{
				case (FwSuperscriptVal.kssvSub):
				{
					subSuperVal = "sub";
					break;
				}
				case (FwSuperscriptVal.kssvSuper):
				{
					subSuperVal = "super";
					break;
				}
				case (FwSuperscriptVal.kssvOff):
				{
					subSuperVal = "initial";
					break;
				}
			}
			fontProp.Term = new PrimitiveTerm(UnitType.Ident, subSuperVal);
			declaration.Add(fontProp);
		}

		private static void AddInfoForUnderline(FontInfo wsFont, ICharacterStyleInfo defaultFont, StyleDeclaration declaration)
		{
			FwUnderlineType underlineType;
			if(!GetFontValue(wsFont.Underline, defaultFont.Underline, out underlineType))
				return;
			switch(underlineType)
			{
				case(FwUnderlineType.kuntDouble):
				{
					// use border to generate second underline then generate the standard underline
					var fontProp = new Property("border-bottom");
					var termList = new TermList();
					termList.AddTerm(new PrimitiveTerm(UnitType.Pixel, 1));
					termList.AddSeparator(TermList.TermSeparator.Space);
					termList.AddTerm(new PrimitiveTerm(UnitType.Ident, "solid"));
					fontProp.Term = termList;
					declaration.Add(fontProp);
					AddInfoFromWsOrDefaultValue(wsFont.UnderlineColor, defaultFont.UnderlineColor, "border-bottom-color", declaration);
					goto case FwUnderlineType.kuntSingle; //fall through to single
				}
				case(FwUnderlineType.kuntSingle):
				{
					var fontProp = new Property("text-decoration");
					fontProp.Term = new PrimitiveTerm(UnitType.Ident, "underline");
					declaration.Add(fontProp);
					AddInfoFromWsOrDefaultValue(wsFont.UnderlineColor, defaultFont.UnderlineColor, "text-decoration-color", declaration);
					break;
				}
				case(FwUnderlineType.kuntStrikethrough):
				{
					var fontProp = new Property("text-decoration");
					fontProp.Term = new PrimitiveTerm(UnitType.Ident, "line-through");
					declaration.Add(fontProp);
					AddInfoFromWsOrDefaultValue(wsFont.UnderlineColor, defaultFont.UnderlineColor, "text-decoration-color", declaration);
					break;
				}
				case (FwUnderlineType.kuntDashed):
				case (FwUnderlineType.kuntDotted):
				{
					// use border to generate a dotted or dashed underline
					var fontProp = new Property("border-bottom");
					var termList = new TermList();
					termList.AddTerm(new PrimitiveTerm(UnitType.Pixel, 1));
					termList.AddSeparator(TermList.TermSeparator.Space);
					termList.AddTerm(new PrimitiveTerm(UnitType.Ident,
																  underlineType == FwUnderlineType.kuntDashed ? "dashed" : "dotted"));
					fontProp.Term = termList;
					declaration.Add(fontProp);
					AddInfoFromWsOrDefaultValue(wsFont.UnderlineColor, defaultFont.UnderlineColor, "border-bottom-color", declaration);
					break;
				}
			}
		}

		/// <summary>
		/// This method will set fontValue to the font value from the writing system info falling back to the
		/// default info. It will return false if the value is not set in either info.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="wsFontInfo">writing system specific font info</param>
		/// <param name="defaultFontInfo">default font info</param>
		/// <param name="fontValue">the value retrieved from the given font infos</param>
		/// <returns>true if fontValue was defined in one of the info objects</returns>
		private static bool GetFontValue<T>(IStyleProp<T> wsFontInfo, IStyleProp<T> defaultFontInfo,
													out T fontValue)
		{
			fontValue = default(T);
			if(wsFontInfo.ValueIsSet)
				fontValue = wsFontInfo.Value;
			else if(defaultFontInfo.ValueIsSet)
				fontValue = defaultFontInfo.Value;
			else
				return false;
			return true;
		}

		/// <summary>
		/// Extension method to provide a css string conversion from an FwTextAlign enum value
		/// </summary>
		/// <param name="align"></param>
		/// <returns></returns>
		public static String AsCssString(this FwTextAlign align)
		{
			switch(align)
			{
				case (FwTextAlign.ktalJustify):
					return "justify";
				case (FwTextAlign.ktalCenter):
					return "center";
				case (FwTextAlign.ktalLeading):
					return "start";
				case (FwTextAlign.ktalTrailing):
					return "end";
				case (FwTextAlign.ktalLeft):
					return "left";
				case (FwTextAlign.ktalRight):
					return "right";
				default:
					return "inherit";
			}
		}

		public static string GenerateLetterHeaderCss(PropertyTable propertyTable)
		{
			var letHeadRule = new StyleRule { Value = ".letHead" };
			letHeadRule.Declarations.Properties.Add(new Property("-moz-column-count") { Term = new PrimitiveTerm(UnitType.Number, 1) });
			letHeadRule.Declarations.Properties.Add(new Property("-webkit-column-count") { Term = new PrimitiveTerm(UnitType.Number, 1) });
			letHeadRule.Declarations.Properties.Add(new Property("column-count") { Term = new PrimitiveTerm(UnitType.Number, 1) });
			letHeadRule.Declarations.Properties.Add(new Property("clear") { Term = new PrimitiveTerm(UnitType.Ident, "both") });

			var letterRule = new StyleRule { Value = ".letter" };
			letterRule.Declarations.Properties.Add(new Property("text-align") { Term = new PrimitiveTerm(UnitType.Ident, "center") });
			letterRule.Declarations.Properties.Add(new Property("width") { Term = new PrimitiveTerm(UnitType.Percentage, 100) });
			var cache = propertyTable.GetValue<FdoCache>("cache");
			letterRule.Declarations.Properties.AddRange(GenerateCssStyleFromFwStyleSheet(LetterHeadingStyleName, cache.DefaultVernWs, propertyTable));
			return letHeadRule.ToString(true) + Environment.NewLine + letterRule.ToString(true) + Environment.NewLine;
		}
	}
}
