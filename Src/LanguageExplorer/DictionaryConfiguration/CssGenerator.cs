// Copyright (c) 2014-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ExCSS;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;
using Property = ExCSS.Property;

namespace LanguageExplorer.DictionaryConfiguration
{
	internal static class CssGenerator
	{
		/// <summary>
		/// id that triggers using the default selection on a character style instead of a writing system specific one
		/// </summary>
		internal const int DefaultStyle = -1;
		internal const string BeforeAfterBetweenStyleName = "Dictionary-Context";
		internal const string LetterHeadingStyleName = "Dictionary-LetterHeading";
		internal const string DictionaryNormal = "Dictionary-Normal";
		internal const string DictionaryMinor = "Dictionary-Minor";
		internal const string WritingSystemPrefix = "writingsystemprefix";
		internal const string WritingSystemStyleName = "Writing System Abbreviation";
		private static readonly Dictionary<string, string> BulletSymbolsCollection = new Dictionary<string, string>();
		private static readonly Dictionary<string, string> NumberingStylesCollection = new Dictionary<string, string>();
		private static readonly Dictionary<string, string> UnderlineStyleMap = new Dictionary<string, string>
		{
			{"kuntSingle", "single"},
			{"kuntDouble", "double"},
			{"kuntDotted", "dotted"},
			{"kuntDashed", "dashed"},
			{"kuntStrikethrough", "strikethrough"},
			{"kuntSquiggle", "squiggle"}
		};

		/// <summary>
		/// Generate all the css rules necessary to represent every enabled portion of the given configuration
		/// </summary>
		public static string GenerateCssFromConfiguration(DictionaryConfigurationModel model, IReadonlyPropertyTable readonlyPropertyTable)
		{
			Guard.AgainstNull(model, nameof(model));

			var cache = readonlyPropertyTable.GetValue<LcmCache>(FwUtilsConstants.cache);
			var styleSheet = new StyleSheet();
			var propStyleSheet = FwUtils.StyleSheetFromPropertyTable(readonlyPropertyTable);
			LoadBulletUnicodes();
			LoadNumberingStyles();
			GenerateLetterHeaderCss(styleSheet, propStyleSheet);
			GenerateCssForDefaultStyles(cache, propStyleSheet, styleSheet, model);
			MakeLinksLookLikePlainText(styleSheet);
			GenerateBidirectionalCssShim(styleSheet);
			GenerateCssForAudioWs(styleSheet, cache);
			foreach (var configNode in model.Parts.Where(x => x.IsEnabled).Concat(model.SharedItems.Where(x => x.Parent != null)))
			{
				GenerateCssFromConfigurationNode(configNode, styleSheet, null, cache, propStyleSheet);
			}
			// Pretty-print the stylesheet
			return CustomIcu.GetIcuNormalizer(FwNormalizationMode.knmNFC).Normalize(styleSheet.ToString(true, 1));
		}

		private static void GenerateCssForDefaultStyles(LcmCache cache, LcmStyleSheet fwStyleSheet, StyleSheet styleSheet, DictionaryConfigurationModel model)
		{
<<<<<<< HEAD:Src/LanguageExplorer/DictionaryConfiguration/CssGenerator.cs
			if (fwStyleSheet == null)
||||||| f013144d5:Src/xWorks/CssGenerator.cs
			if (propStyleSheet == null)
				return;

			if (propStyleSheet.Styles.Contains("Normal"))
				GenerateCssForWsSpanWithNormalStyle(styleSheet, propertyTable);

			if (propStyleSheet.Styles.Contains(DictionaryNormal))
				GenerateDictionaryNormalParagraphCss(styleSheet, propertyTable);

			if (propStyleSheet.Styles.Contains(LetterHeadingStyleName))
=======
			if (propStyleSheet == null)
				return;

			if (propStyleSheet.Styles.Contains("Normal"))
				GenerateCssForWsSpanWithNormalStyle(styleSheet, propertyTable);

			var entryBaseStyle = ConfiguredLcmGenerator.GetEntryStyle(model);
			if (propStyleSheet.Styles.Contains(entryBaseStyle))
				GenerateDictionaryNormalParagraphCss(styleSheet, propertyTable, entryBaseStyle);

			if (propStyleSheet.Styles.Contains(LetterHeadingStyleName))
>>>>>>> develop:Src/xWorks/CssGenerator.cs
			{
				return;
			}
			if (fwStyleSheet.Styles.Contains("Normal"))
			{
				GenerateCssForWsSpanWithNormalStyle(styleSheet, cache, fwStyleSheet);
			}
			if (fwStyleSheet.Styles.Contains(DictionaryNormal))
			{
				GenerateDictionaryNormalParagraphCss(styleSheet, cache, fwStyleSheet);
			}
			if (fwStyleSheet.Styles.Contains(LetterHeadingStyleName))
			{
				GenerateCssForWritingSystems(".letter", LetterHeadingStyleName, styleSheet, cache, fwStyleSheet);
			}
			GenerateDictionaryMinorParagraphCss(styleSheet, cache, fwStyleSheet, model);
		}

		private static void MakeLinksLookLikePlainText(StyleSheet styleSheet)
		{
			var rule = new StyleRule { Value = "a" };
			rule.Declarations.Properties.AddRange(new[]
			{
				new Property("text-decoration") { Term = new PrimitiveTerm(UnitType.Attribute, "inherit") },
				new Property("color") { Term = new PrimitiveTerm(UnitType.Attribute, "inherit") }
			});
			styleSheet.Rules.Add(rule);
		}

		/// <summary>
		/// Generate a CSS Shim needed to make bidirectional text render properly in browsers older than Firefox 47
		/// See https://www.w3.org/International/articles/inline-bidi-markup/#cssshim
		/// </summary>
		private static void GenerateBidirectionalCssShim(StyleSheet styleSheet)
		{
			var rule = new StyleRule { Value = "*[dir='ltr'], *[dir='rtl']" };
			rule.Declarations.Properties.AddRange(new[]
			{
				new Property("unicode-bidi") { Term = new PrimitiveTerm(UnitType.Attribute, "isolate") },
				new Property("unicode-bidi") { Term = new PrimitiveTerm(UnitType.Attribute, "-ms-isolate") },
				new Property("unicode-bidi") { Term = new PrimitiveTerm(UnitType.Attribute, "-moz-isolate") }
			});
			styleSheet.Rules.Add(rule);
		}

		private static void GenerateCssForWsSpanWithNormalStyle(StyleSheet styleSheet, LcmCache cache, LcmStyleSheet fwStyleSheet)
		{
			// Generate the rules for the programmatic default style info (
			var defaultStyleProps = GetOnlyCharacterStyle(GenerateCssStyleFromLcmStyleSheet("Normal", DefaultStyle, fwStyleSheet, cache.ServiceLocator.WritingSystemManager.get_EngineOrNull(DefaultStyle)));
			if (defaultStyleProps.All(p => p.Name != "font-size"))
			{
				defaultStyleProps.Add(new Property("font-size") { Term = new PrimitiveTerm(UnitType.Point, FontInfo.kDefaultFontSize) });
			}
			var defaultRule = new StyleRule { Value = "body" };
			defaultRule.Declarations.Properties.AddRange(defaultStyleProps);
			styleSheet.Rules.Add(defaultRule);
			// Then generate the rules for all the writing system overrides
			GenerateCssForWritingSystems("span", "Normal", styleSheet, cache, fwStyleSheet);
		}

<<<<<<< HEAD:Src/LanguageExplorer/DictionaryConfiguration/CssGenerator.cs
		private static void GenerateDictionaryNormalParagraphCss(StyleSheet styleSheet, LcmCache cache, LcmStyleSheet fwStyleSheet)
||||||| f013144d5:Src/xWorks/CssGenerator.cs
		private static void GenerateDictionaryNormalParagraphCss(StyleSheet styleSheet, ReadOnlyPropertyTable propertyTable)
=======
		private static void GenerateDictionaryNormalParagraphCss(StyleSheet styleSheet, ReadOnlyPropertyTable propertyTable, string entryBaseStyle)
>>>>>>> develop:Src/xWorks/CssGenerator.cs
		{
			var dictNormalRule = new StyleRule { Value = "div.entry" };
<<<<<<< HEAD:Src/LanguageExplorer/DictionaryConfiguration/CssGenerator.cs
			var dictNormalStyle = GenerateCssStyleFromLcmStyleSheet(DictionaryNormal, 0, fwStyleSheet, cache.ServiceLocator.WritingSystemManager.get_EngineOrNull(0));
||||||| f013144d5:Src/xWorks/CssGenerator.cs
			var dictNormalStyle = GenerateCssStyleFromLcmStyleSheet(DictionaryNormal, 0, propertyTable);
=======
			var dictNormalStyle = GenerateCssStyleFromLcmStyleSheet(entryBaseStyle, 0, propertyTable);
>>>>>>> develop:Src/xWorks/CssGenerator.cs
			dictNormalRule.Declarations.Properties.AddRange(GetOnlyParagraphStyle(dictNormalStyle));
			styleSheet.Rules.Add(dictNormalRule);
			// Then generate the rules for all the writing system overrides
<<<<<<< HEAD:Src/LanguageExplorer/DictionaryConfiguration/CssGenerator.cs
			GenerateCssForWritingSystems("div.entry span", DictionaryNormal, styleSheet, cache, fwStyleSheet);
||||||| f013144d5:Src/xWorks/CssGenerator.cs
			GenerateCssForWritingSystems("div.entry span", DictionaryNormal, styleSheet, propertyTable);
=======
			GenerateCssForWritingSystems("div.entry span", entryBaseStyle, styleSheet, propertyTable);
>>>>>>> develop:Src/xWorks/CssGenerator.cs
		}

		private static void GenerateDictionaryMinorParagraphCss(StyleSheet styleSheet, LcmCache cache, LcmStyleSheet fwStyleSheet, DictionaryConfigurationModel model)
		{
			// Use the style set in all the parts following main entry, if no style is specified assume Dictionary-Minor
			for (var i = 1; i < model.Parts.Count; ++i)
			{
				var minorEntryNode = model.Parts[i];
				if (!minorEntryNode.IsEnabled)
				{
					continue;
				}
				var styleName = minorEntryNode.Style;
				if (string.IsNullOrEmpty(styleName))
				{
					styleName = DictionaryMinor;
				}
				var dictionaryMinorStyle = GenerateCssStyleFromLcmStyleSheet(styleName, 0, fwStyleSheet, cache.ServiceLocator.WritingSystemManager.get_EngineOrNull(0));
				var minorRule = new StyleRule { Value = $"div.{GetClassAttributeForConfig(minorEntryNode)}" };
				minorRule.Declarations.Properties.AddRange(GetOnlyParagraphStyle(dictionaryMinorStyle));
				styleSheet.Rules.Add(minorRule);
				// Then generate the rules for all the writing system overrides
				GenerateCssForWritingSystems($"div.{GetClassAttributeForConfig(minorEntryNode)} span", styleName, styleSheet, cache, fwStyleSheet);
			}
		}

		private static void GenerateCssForWritingSystems(string selector, string styleName, StyleSheet styleSheet, LcmCache cache, LcmStyleSheet fwStyleSheet)
		{
			// Generate the rules for all the writing system overrides
			foreach (var aws in cache.ServiceLocator.WritingSystems.AllWritingSystems)
			{
				// We want only the character type settings from the styleName style since we're applying them
				// to a span.
				var wsRule = new StyleRule { Value = selector + $"[lang|=\"{aws.LanguageTag}\"]" };
				var styleDecls = GenerateCssStyleFromLcmStyleSheet(styleName, aws.Handle, fwStyleSheet, cache.ServiceLocator.WritingSystemManager.get_EngineOrNull(aws.Handle));
				wsRule.Declarations.Properties.AddRange(GetOnlyCharacterStyle(styleDecls));
				styleSheet.Rules.Add(wsRule);
			}
		}

		private static void GenerateCssForAudioWs(StyleSheet styleSheet, LcmCache cache)
		{
			foreach (var aws in cache.ServiceLocator.WritingSystems.AllWritingSystems)
			{
				if (!aws.LanguageTag.Contains("audio"))
				{
					continue;
				}
				var wsaudioRule = new StyleRule {Value = $"a.{aws.LanguageTag}:after" };
				wsaudioRule.Declarations.Properties.Add(new Property("content")
				{
					Term = new PrimitiveTerm(UnitType.String, ConfiguredLcmGenerator.LoudSpeaker)
				});
				styleSheet.Rules.Add(wsaudioRule);
				wsaudioRule = new StyleRule {Value = $"a.{aws.LanguageTag}" };
				wsaudioRule.Declarations.Properties.Add(new Property("text-decoration")
				{
					Term = new PrimitiveTerm(UnitType.Attribute, "none")
				});
				styleSheet.Rules.Add(wsaudioRule);
			}
		}

		/// <summary>
		/// Generates css rules for a configuration node and adds them to the given stylesheet (recursive).
		/// </summary>
		private static void GenerateCssFromConfigurationNode(ConfigurableDictionaryNode configNode, StyleSheet styleSheet, string baseSelection, LcmCache cache, LcmStyleSheet fwStyleSheet)
		{
			var rule = new StyleRule();
			switch (configNode.DictionaryNodeOptions)
			{
				case DictionaryNodeSenseOptions senseOptions:
					// Try to generate the css for the sense number before the baseSelection is updated because
					// the sense number is a sibling of the sense element and we are normally applying styles to the
					// children of collections. Also set display:block on span
					GenerateCssForSenses(configNode, senseOptions, styleSheet, ref baseSelection, cache, fwStyleSheet);
					break;
				case IParaOption listAndParaOpts:
				{
					GenerateCssFromListAndParaOptions(configNode, listAndParaOpts, styleSheet, ref baseSelection, cache, fwStyleSheet);
					if (configNode.DictionaryNodeOptions is DictionaryNodeWritingSystemOptions wsOptions && wsOptions.DisplayWritingSystemAbbreviations)
					{
						if (DictionaryConfigurationModel.NoteInParaStyles.Contains(configNode.FieldDescription))
						{
							baseSelection = baseSelection + "> span";
						}
						GenerateCssForWritingSystemPrefix(configNode, styleSheet, baseSelection, cache, fwStyleSheet);
					}
					break;
				}
				default:
				{
					if (configNode.DictionaryNodeOptions is DictionaryNodePictureOptions dictionaryNodePictureOptions)
					{
						GenerateCssFromPictureOptions(configNode, dictionaryNodePictureOptions, styleSheet, baseSelection);
					}
					var selectors = GenerateSelectorsFromNode(baseSelection, configNode, out baseSelection, cache, fwStyleSheet);
					if (configNode.DictionaryNodeOptions is DictionaryNodeWritingSystemOptions wsOptions)
					{
						GenerateCssFromWsOptions(configNode, wsOptions, styleSheet, baseSelection, cache, fwStyleSheet);
						if (wsOptions.DisplayWritingSystemAbbreviations)
						{
							GenerateCssForWritingSystemPrefix(configNode, styleSheet, baseSelection, cache, fwStyleSheet);
						}
					}
					rule.Value = baseSelection;
					// if the configuration node defines a style then add all the rules generated from that style
					if (!string.IsNullOrEmpty(configNode.Style))
					{
						//Generate the rules for the default font info
						rule.Declarations.Properties.AddRange(GenerateCssStyleFromLcmStyleSheet(configNode.Style, DefaultStyle, configNode, fwStyleSheet, cache.ServiceLocator.WritingSystemManager.get_EngineOrNull(DefaultStyle)));
						GenerateCssForWritingSystems(baseSelection + " span", configNode.Style, styleSheet, cache, fwStyleSheet);
					}
					styleSheet.Rules.AddRange(CheckRangeOfRulesForEmpties(selectors));
					if (!IsEmptyRule(rule))
					{
						styleSheet.Rules.Add(rule);
					}
					break;
				}
			}
			if (configNode.Children == null)
			{
				return;
			}
			//Recurse into each child
			foreach (var child in configNode.Children.Where(x => x.IsEnabled))
			{
				GenerateCssFromConfigurationNode(child, styleSheet, baseSelection, cache, fwStyleSheet);
			}
		}

		private static bool IsEmptyRule(StyleRule rule)
		{
			return rule.Declarations.All(decl => string.IsNullOrWhiteSpace(decl.ToString()));
		}

		private static IEnumerable<StyleRule> CheckRangeOfRulesForEmpties(IEnumerable<StyleRule> rules)
		{
			return rules.Where(rule => !IsEmptyRule(rule));
		}

		private static bool IsBeforeOrAfter(StyleRule rule)
		{
			var sel = rule.Selector.ToString();
			return sel.EndsWith(":before") || sel.EndsWith(":after");
		}

		private static IEnumerable<StyleRule> RemoveBeforeAfterSelectorRules(IEnumerable<StyleRule> rules)
		{
			return rules.Where(rule => !IsBeforeOrAfter(rule));
		}

		private static void GenerateCssForSenses(ConfigurableDictionaryNode configNode, DictionaryNodeSenseOptions senseOptions, StyleSheet styleSheet, ref string baseSelection, LcmCache cache, LcmStyleSheet fwStyleSheet)
		{
			var selectors = GenerateSelectorsFromNode(baseSelection, configNode, out baseSelection, cache, fwStyleSheet);
			// Insert '> .sensecontent' between '.*senses' and '.*sense' (where * could be 'referring', 'sub', or similar)
			var senseContentSelector = $"{baseSelection.Substring(0, baseSelection.LastIndexOf('.'))}> .sensecontent";
			var senseItemName = baseSelection.Substring(baseSelection.LastIndexOf('.'));
			if (senseOptions.DisplayEachSenseInAParagraph)
			{
				selectors = RemoveBeforeAfterSelectorRules(selectors);
			}
			styleSheet.Rules.AddRange(CheckRangeOfRulesForEmpties(selectors));
			var senseNumberRule = new StyleRule();
			// Not using SelectClassName here; sense and sensenumber are siblings and the configNode is for the Senses collection.
			// Select the base plus the node's unmodified class attribute and append the sensenumber matcher.
			var senseNumberSelector = $"{senseContentSelector} .sensenumber";
			senseNumberRule.Value = senseNumberSelector;
			if (!string.IsNullOrEmpty(senseOptions.NumberStyle))
			{
				senseNumberRule.Declarations.Properties.AddRange(GenerateCssStyleFromLcmStyleSheet(senseOptions.NumberStyle, DefaultStyle, fwStyleSheet, cache.ServiceLocator.WritingSystemManager.get_EngineOrNull(DefaultStyle)));
			}
			if (!IsEmptyRule(senseNumberRule))
			{
				styleSheet.Rules.Add(senseNumberRule);
			}
			if (!string.IsNullOrEmpty(senseOptions.BeforeNumber))
			{
				var beforeDeclaration = new StyleDeclaration
				{
					new Property("content") { Term = new PrimitiveTerm(UnitType.String, senseOptions.BeforeNumber) }
				};
				styleSheet.Rules.Add(new StyleRule(beforeDeclaration) { Value = senseNumberSelector + ":before" });
			}
			if (!string.IsNullOrEmpty(senseOptions.AfterNumber))
			{
				var afterDeclaration = new StyleDeclaration
				{
					new Property("content") {Term = new PrimitiveTerm(UnitType.String, senseOptions.AfterNumber)}
				};
				var afterRule = new StyleRule(afterDeclaration) { Value = senseNumberSelector + ":after" };
				styleSheet.Rules.Add(afterRule);
			}
			// set the base selection to the sense level under the sense content
			baseSelection = $"{senseContentSelector} > {senseItemName}";
			const int wsId = 0;
			var styleDeclaration = string.IsNullOrEmpty(configNode.Style) ? new StyleDeclaration() : GenerateCssStyleFromLcmStyleSheet(configNode.Style, wsId, configNode, fwStyleSheet, cache.ServiceLocator.WritingSystemManager.get_EngineOrNull(wsId));
			if (senseOptions.DisplayEachSenseInAParagraph)
			{
				var sensCharDeclaration = GetOnlyCharacterStyle(styleDeclaration);
				var senseCharRule = new StyleRule(sensCharDeclaration)
				{
					// Apply the style with paragraph info removed to the first sense
					Value = baseSelection
				};
				if (!IsEmptyRule(senseCharRule))
				{
					styleSheet.Rules.Add(senseCharRule);
				}
				var senseParaDeclaration = GetOnlyParagraphStyle(styleDeclaration);
				senseParaDeclaration.Add(new Property("display")
				{
					Term = new PrimitiveTerm(UnitType.Ident, "block")
				});
				var senseParaRule = new StyleRule(senseParaDeclaration)
				{
					// Apply the paragraph style information to all but the first sensecontent block, if requested
					Value = senseOptions.DisplayFirstSenseInline ? $"{senseContentSelector} + {".sensecontent"}" : senseContentSelector
				};
				styleSheet.Rules.Add(senseParaRule);
				GenerateCssforBulletedList(configNode, styleSheet, senseParaRule.Value, styleDeclaration, cache, fwStyleSheet);
			}
			else
			{
				// Generate the style information specifically for senses
				var senseContentRule = new StyleRule(GetOnlyCharacterStyle(styleDeclaration))
				{
					Value = baseSelection
				};
				if (!IsEmptyRule(senseContentRule))
				{
					styleSheet.Rules.Add(senseContentRule);
				}
			}

			if (senseOptions.ShowSharedGrammarInfoFirst)
			{
				var collectionSelector = senseContentSelector.Substring(0, senseContentSelector.LastIndexOf(" .", StringComparison.Ordinal));
				foreach (var gramInfoNode in configNode.Children.Where(node => node.FieldDescription == "MorphoSyntaxAnalysisRA" && node.IsEnabled))
				{
					GenerateCssFromConfigurationNode(gramInfoNode, styleSheet, collectionSelector + " .sharedgrammaticalinfo", cache, fwStyleSheet);
				}
			}
		}

		/// <summary>
		/// Generates Bulleted List style properties
		/// </summary>
		/// <param name="configNode">Dictionary Node</param>
		/// <param name="styleSheet">Stylesheet to add the new rule</param>
		/// <param name="bulletSelector">Style name for the bullet property</param>
		/// <param name="styleDeclaration">Style properties collection</param>
		/// <param name="cache"></param>
		/// <param name="fwStyleSheet"></param>
		private static void GenerateCssforBulletedList(ConfigurableDictionaryNode configNode, StyleSheet styleSheet, string bulletSelector, StyleDeclaration styleDeclaration, LcmCache cache, LcmStyleSheet fwStyleSheet)
		{
			if (configNode.Style != null)
			{
				if (styleDeclaration.Properties.Count == 0)
				{
					styleDeclaration = GenerateCssStyleFromLcmStyleSheet(configNode.Style, DefaultStyle, fwStyleSheet, cache.ServiceLocator.WritingSystemManager.get_EngineOrNull(DefaultStyle));
				}
				GenerateCssForCounterReset(styleSheet, bulletSelector, styleDeclaration, false);
				var senseSufixRule = configNode.DictionaryNodeOptions is DictionaryNodeSenseOptions senseOptions && senseOptions.DisplayFirstSenseInline ? ":not(:first-child):before" : ":before";
				var bulletRule = new StyleRule { Value = bulletSelector + senseSufixRule };
				bulletRule.Declarations.Properties.AddRange(GetOnlyBulletContent(styleDeclaration));
				var projectStyle = fwStyleSheet.Styles[configNode.Style];
				var exportStyleInfo = new ExportStyleInfo(projectStyle);
				if (exportStyleInfo.NumberScheme != 0)
				{
					var wsFontInfo = exportStyleInfo.BulletInfo.FontInfo;
					bulletRule.Declarations.Add(new Property("font-size") { Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(wsFontInfo.FontSize.Value)) });
					bulletRule.Declarations.Add(new Property("color") { Term = new PrimitiveTerm(UnitType.RGB, wsFontInfo.FontColor.Value.Name) });
					bulletRule.Declarations.Add(new Property("font-family") { Term = new PrimitiveTerm(UnitType.Ident, wsFontInfo.FontName.Value) });
					bulletRule.Declarations.Add(new Property("font-weight") { Term = new PrimitiveTerm(UnitType.Ident, wsFontInfo.Bold.Value ? "bold" : "normal") });
					bulletRule.Declarations.Add(new Property("font-style") { Term = new PrimitiveTerm(UnitType.Ident, wsFontInfo.Italic.Value ? "italic" : "normal") });
					bulletRule.Declarations.Add(new Property("background-color") { Term = new PrimitiveTerm(UnitType.RGB, wsFontInfo.BackColor.Value.Name) });
					if (wsFontInfo.Underline.Value.ToString().ToLower() != "kuntnone")
					{
						bulletRule.Declarations.Add(new Property("text-decoration") { Term = new PrimitiveTerm(UnitType.Ident, "underline") });
						bulletRule.Declarations.Add(new Property("text-decoration-style") { Term = new PrimitiveTerm(UnitType.Ident, UnderlineStyleMap[wsFontInfo.Underline.Value.ToString()]) });
						bulletRule.Declarations.Add(new Property("text-decoration-color") { Term = new PrimitiveTerm(UnitType.RGB, wsFontInfo.UnderlineColor.Value.Name) });
					}
				}
				if (!IsEmptyRule(bulletRule))
				{
					styleSheet.Rules.Add(bulletRule);
				}
			}
		}

		private static void GenerateCssFromListAndParaOptions(ConfigurableDictionaryNode configNode, IParaOption listAndParaOpts, StyleSheet styleSheet, ref string baseSelection,
			LcmCache cache, LcmStyleSheet fwStyleSheet)
		{
			var selectors = GenerateSelectorsFromNode(baseSelection, configNode, out baseSelection, cache, fwStyleSheet);
			List<StyleDeclaration> blockDeclarations;
			if (string.IsNullOrEmpty(configNode.Style))
			{
				blockDeclarations = new List<StyleDeclaration> { new StyleDeclaration() };
			}
			else
			{
				const int wsId = 0;
				// We currently do not generate font information for these blocks
				blockDeclarations = GenerateCssStyleFromLcmStyleSheet(configNode.Style, wsId, configNode, fwStyleSheet, cache.ServiceLocator.WritingSystemManager.get_EngineOrNull(wsId), true);
				GenerateCssForWritingSystems(baseSelection + " span", configNode.Style, styleSheet, cache, fwStyleSheet);
			}
			var styleRules = selectors as StyleRule[] ?? selectors.ToArray();
			if (listAndParaOpts.DisplayEachInAParagraph)
			{
				foreach (var declaration in blockDeclarations)
				{
					declaration.Add(new Property("display") { Term = new PrimitiveTerm(UnitType.Ident, "block") });
					var blockRule = new StyleRule(declaration)
					{
						Value = baseSelection
					};
					styleSheet.Rules.Add(blockRule);
					GenerateCssForCounterReset(styleSheet, baseSelection, declaration, true);
					var bulletRule = AdjustRuleIfParagraphNumberScheme(blockRule, configNode, fwStyleSheet);
					// REVIEW (Hasso) 2016.10: could these two lines be moved outside the loop?
					// REVIEW (Hasso) 2016.10: both of these following lines add all rules but BeforeAfter (so if the condition in the first line
					// REVIEW (cont) is true, both excluded rule categories will nonetheless be added)
					styleSheet.Rules.AddRange(DictionaryConfigurationModel.NoteInParaStyles.Contains(configNode.FieldDescription)
						? RemoveBeforeAndAfterForNoteInParaRules(styleRules)
						: RemoveBeforeAfterSelectorRules(styleRules));
					styleSheet.Rules.AddRange(RemoveBeforeAfterSelectorRules(styleRules));
					styleSheet.Rules.Add(bulletRule);
				}
			}
			else
			{
				foreach (var declaration in blockDeclarations)
				{
					// Generate the style information specifically for ComplexFormsOptions
					var complexContentRule = new StyleRule(GetOnlyCharacterStyle(declaration))
					{
						Value = baseSelection
					};
					if (!IsEmptyRule(complexContentRule))
					{
						styleSheet.Rules.Add(complexContentRule);
					}
				}
				styleSheet.Rules.AddRange(styleRules);
			}
		}


		private static IEnumerable<StyleRule> RemoveBeforeAndAfterForNoteInParaRules(IEnumerable<StyleRule> rules)
		{
			return rules.Where(rule => rule.Value.Contains("~"));
		}

		/// <summary>
		/// Generates Counter reset style properties
		/// </summary>
		/// <param name="styleSheet">Stylesheet to add the new rule</param>
		/// <param name="baseSelection">Style name for the bullet property</param>
		/// <param name="declaration">Style properties collection</param>
		/// <param name="isSplitBySpace">Split baseSelection by space/greater than</param>
		private static void GenerateCssForCounterReset(StyleSheet styleSheet, string baseSelection, StyleDeclaration declaration, bool isSplitBySpace)
		{
			var resetSection = GetOnlyCounterResetContent(declaration);
			if (string.IsNullOrEmpty(resetSection))
			{
				return;
			}
			var bulletParentSelector = baseSelection.Substring(0, baseSelection.LastIndexOf('>') - 1);
			if (isSplitBySpace)
			{
				bulletParentSelector = baseSelection.Substring(0, baseSelection.LastIndexOf(' '));
			}
			var resetRule = new StyleRule { Value = bulletParentSelector };
			resetRule.Declarations.Add(new Property("counter-reset")
			{
				Term = new PrimitiveTerm(UnitType.Attribute, resetSection)
			});
			styleSheet.Rules.Add(resetRule);
		}

		/// <summary>
		/// Return a :before rule if the given rule derives from a paragraph style with a number scheme (such as bulleted).
		/// Remove the content part of the given rule if it is present and also remove the properties that don't apply to a :before rule.
		/// </summary>
		/// <remarks>
		/// See https://jira.sil.org/browse/LT-11625 for justification.
		/// </remarks>
		private static StyleRule AdjustRuleIfParagraphNumberScheme(StyleRule rule, ConfigurableDictionaryNode configNode, LcmStyleSheet fwStyleSheet)
		{
			if (string.IsNullOrEmpty(configNode.Style))
			{
				return rule;
			}
			var projectStyle = fwStyleSheet.Styles[configNode.Style];
			var exportStyleInfo = new ExportStyleInfo(projectStyle);
			if (exportStyleInfo.NumberScheme != 0)
			{
				// Create a rule to add the bullet content before based off the given rule
				var bulletRule = new StyleRule { Value = rule.Value + ":before" };
				bulletRule.Declarations.Properties.AddRange(GetOnlyBulletContent(rule.Declarations));
				var wsFontInfo = exportStyleInfo.BulletInfo.FontInfo;
				bulletRule.Declarations.Add(new Property("font-size") { Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(wsFontInfo.FontSize.Value)) });
				bulletRule.Declarations.Add(new Property("color") { Term = new PrimitiveTerm(UnitType.RGB, wsFontInfo.FontColor.Value.Name) });
				// remove the bullet content if present in the base rule
				var contentInRule = rule.Declarations.FirstOrDefault(p => p.Name == "content");
				if (contentInRule != null)
				{
					rule.Declarations.Remove(contentInRule);
				}
				// remove the bullet counter-increment if present in the base rule
				var counterIncrement = rule.Declarations.FirstOrDefault(p => p.Name == "counter-increment");
				if (counterIncrement != null)
				{
					rule.Declarations.Remove(counterIncrement);
				}
				return bulletRule;
			}
			return rule;
		}

		private static void GenerateCssFromWsOptions(ConfigurableDictionaryNode configNode, DictionaryNodeWritingSystemOptions wsOptions, StyleSheet styleSheet, string baseSelection, LcmCache cache, LcmStyleSheet fwStyleSheet)
		{
			foreach (var ws in wsOptions.Options.Where(opt => opt.IsEnabled))
			{
				var possiblyMagic = WritingSystemServices.GetMagicWsIdFromName(ws.Id);
				// if the writing system isn't a magic name just use it otherwise find the right one from the magic list
				var wsIdString = possiblyMagic == 0 ? ws.Id : WritingSystemServices.GetWritingSystemList(cache, possiblyMagic, true).First().Id;
				var wsId = cache.LanguageWritingSystemFactoryAccessor.GetWsFromStr(wsIdString);
				var wsRule = new StyleRule { Value = $"{baseSelection}[lang|=\"{wsIdString}\"]" };
				if (!string.IsNullOrEmpty(configNode.Style))
				{
					wsRule.Declarations.Properties.AddRange(GenerateCssStyleFromLcmStyleSheet(configNode.Style, wsId, fwStyleSheet, cache.ServiceLocator.WritingSystemManager.get_EngineOrNull(wsId)));
				}
				if (!IsEmptyRule(wsRule))
					styleSheet.Rules.Add(wsRule);
			}
		}

		private static void GenerateCssForWritingSystemPrefix(ConfigurableDictionaryNode configNode, StyleSheet styleSheet, string baseSelection, LcmCache cache, LcmStyleSheet fwStyleSheet)
		{
			var wsRule1 = new StyleRule { Value = $"{baseSelection}.{WritingSystemPrefix}" };
			const int wsId = 0;
			wsRule1.Declarations.Properties.AddRange(GetOnlyCharacterStyle(GenerateCssStyleFromLcmStyleSheet(WritingSystemStyleName, wsId, configNode, fwStyleSheet, cache.ServiceLocator.WritingSystemManager.get_EngineOrNull(wsId))));
			styleSheet.Rules.Add(wsRule1);
			var wsRule2 = new StyleRule { Value = $"{baseSelection}.{WritingSystemPrefix}:after" };
			wsRule2.Declarations.Properties.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.String, " ") });
			styleSheet.Rules.Add(wsRule2);
		}

		private static void GenerateCssFromPictureOptions(ConfigurableDictionaryNode configNode, DictionaryNodePictureOptions pictureOptions, StyleSheet styleSheet, string baseSelection)
		{
			var pictureAndCaptionRule = new StyleRule
			{
				Value = $"{baseSelection} {SelectClassName(configNode)}"
			};
			var pictureProps = pictureAndCaptionRule.Declarations.Properties;
			pictureProps.Add(new Property("float") { Term = new PrimitiveTerm(UnitType.Ident, "right") });
			pictureProps.Add(new Property("text-align") { Term = new PrimitiveTerm(UnitType.Ident, "center") });
			var margin = new Property("margin");
			var marginValues = BuildTermList(TermList.TermSeparator.Space, new PrimitiveTerm(UnitType.Point, 0),
				new PrimitiveTerm(UnitType.Point, 0), new PrimitiveTerm(UnitType.Point, 4), new PrimitiveTerm(UnitType.Point, 4));
			margin.Term = marginValues;
			pictureProps.Add(margin);
			pictureProps.Add(new Property("padding") { Term = new PrimitiveTerm(UnitType.Point, 2) });
			pictureProps.Add(new Property("float")
			{
				Term = new PrimitiveTerm(UnitType.Ident, pictureOptions.PictureLocation.ToString().ToLowerInvariant())
			});
			styleSheet.Rules.Add(pictureAndCaptionRule);
			var pictureRule = new StyleRule { Value = pictureAndCaptionRule.Value + " img" };
			if (pictureOptions.MinimumHeight > 0)
			{
				pictureRule.Declarations.Properties.Add(new Property("min-height")
				{
					Term = new PrimitiveTerm(UnitType.Inch, pictureOptions.MinimumHeight)
				});
			}
			if (pictureOptions.MaximumHeight > 0)
			{
				pictureRule.Declarations.Properties.Add(new Property("max-height")
				{
					Term = new PrimitiveTerm(UnitType.Inch, pictureOptions.MaximumHeight)
				});
			}
			if (pictureOptions.MinimumWidth > 0)
			{
				pictureRule.Declarations.Properties.Add(new Property("min-width")
				{
					Term = new PrimitiveTerm(UnitType.Inch, pictureOptions.MinimumWidth)
				});
			}
			if (pictureOptions.MaximumWidth > 0)
			{
				pictureRule.Declarations.Properties.Add(new Property("max-width")
				{
					Term = new PrimitiveTerm(UnitType.Inch, pictureOptions.MaximumWidth)
				});
			}
			if (!IsEmptyRule(pictureRule))
			{
				styleSheet.Rules.Add(pictureRule);
			}
		}

		/// <summary>
		/// This method will generate before and after rules if the configuration node requires them. It also generates the selector for the node
		/// </summary>
		// REVIEW (Hasso) 2016.10: parentSelector and baseSelector could be combined into a single `ref` parameter
		private static IEnumerable<StyleRule> GenerateSelectorsFromNode(string parentSelector, ConfigurableDictionaryNode configNode, out string baseSelection, LcmCache cache, LcmStyleSheet fwStyleSheet)
		{
			// TODO: REFACTOR this method to handle certain nodes more specifically. The options type should be used to branch into node specific code.
			var analWsId = cache.DefaultAnalWs;
			var wsEngine = cache.ServiceLocator.WritingSystemManager.get_EngineOrNull(analWsId);
			var metaDataCacheAccessor = cache.GetManagedMetaDataCache();
			parentSelector = GetParentForFactoredReference(parentSelector, configNode);
			var rules = new List<StyleRule>();
			// simpleSelector is used for nodes that use before and after.  Collection type nodes produce wrong
			// results if we use baseSelection in handling before and after content.  See LT-17048.
			string simpleSelector;
			const string pictCaptionContent = ".captionContent ";
			if (parentSelector == null)
			{
				baseSelection = SelectClassName(configNode);
				simpleSelector = SelectBareClassName(configNode, metaDataCacheAccessor);
				GenerateFlowResetForBaseNode(baseSelection, rules);
			}
			else
			{
				if (!string.IsNullOrEmpty(configNode.Between))
				{
					// content is generated before each item which follows an item of the same name
					// eg. .complexformrefs>.complexformref + .complexformref:before { content: "," }
					var dec = new StyleDeclaration();
					dec.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.String, SpecialCharacterHandling.MakeSafeCss(configNode.Between)) });
					if (fwStyleSheet != null && fwStyleSheet.Styles.Contains(BeforeAfterBetweenStyleName))
					{
						dec.Properties.AddRange(GenerateCssStyleFromLcmStyleSheet(BeforeAfterBetweenStyleName, analWsId, fwStyleSheet, wsEngine));
					}
					var collectionSelector = "." + GetClassAttributeForConfig(configNode);
					if (configNode.Parent.DictionaryNodeOptions is DictionaryNodePictureOptions)
					{
						collectionSelector = pictCaptionContent + "." + GetClassAttributeForConfig(configNode);
					}
					var itemSelector = $" .{GetClassAttributeForCollectionItem(configNode)}";
					var betweenSelector = $"{parentSelector}> {collectionSelector}>{itemSelector}+{itemSelector}:before";
					// use default (class-named) between selector for factored references, because "span+span" erroneously matches Type spans
					if (configNode.DictionaryNodeOptions != null && !ConfiguredLcmGenerator.IsFactoredReference(configNode, out _))
					{
						var wsOptions = configNode.DictionaryNodeOptions as DictionaryNodeWritingSystemOptions;
						var senseOptions = configNode.DictionaryNodeOptions as DictionaryNodeSenseOptions;
						// If wsOptions are enabled generate a between rule which will not put content between the abbreviation and the ws data
						if (wsOptions != null)
						{
							if (wsOptions.DisplayWritingSystemAbbreviations)
							{
								betweenSelector = $"{parentSelector}> {collectionSelector}> span.{WritingSystemPrefix} ~ span.{WritingSystemPrefix}:before";
							}
							else
							{
								var enabledWsOptions = wsOptions.Options.Where(x => x.IsEnabled).ToArray();
								//Fix LT-17238: Between rule added as before rule to ws span which iterates from last ws to second ws span
								//First Ws is skipped as between rules no longer needed before first WS span
								for (var i = enabledWsOptions.Count() - 1; i > 0; i--)
								{
									betweenSelector = (i == enabledWsOptions.Length - 1 ? string.Empty : betweenSelector + ",") +
													  $"{parentSelector}> {collectionSelector}> span+span[lang|='{enabledWsOptions[i].Id}']:before";
								}
							}
						}
						else if (senseOptions != null && senseOptions.ShowSharedGrammarInfoFirst)
						{
							betweenSelector = $"{parentSelector}> {collectionSelector}> span.sensecontent+ span:before";
						}
						else if (configNode.FieldDescription == "PicturesOfSenses")
						{
							betweenSelector = $"{parentSelector}> {collectionSelector}> div+ div:before";
						}
						else
						{
							betweenSelector = $"{parentSelector}> {collectionSelector}> span+ span:before";
						}
					}
					else if (IsFactoredReferenceType(configNode))
					{
						// Between factored Type goes between a reference (last in the list for its Type)
						// and its immediately-following Type "list" (label on the following list of references)
						betweenSelector = $"{parentSelector}> .{GetClassAttributeForCollectionItem(configNode.Parent)}+{collectionSelector}:before";
					}
					var betweenRule = new StyleRule(dec) { Value = betweenSelector };
					rules.Add(betweenRule);
				}
				// Headword, Gloss, and Caption are contained in a captionContent area.
				if (configNode.Parent.DictionaryNodeOptions is DictionaryNodePictureOptions)
				{
					baseSelection = parentSelector + "> " + pictCaptionContent + SelectClassName(configNode, cache);
					simpleSelector = parentSelector + "> " + pictCaptionContent + SelectBareClassName(configNode, metaDataCacheAccessor);
				}
				else
				{
					baseSelection = parentSelector + "> " + SelectClassName(configNode, cache);
					simpleSelector = parentSelector + "> " + SelectBareClassName(configNode, metaDataCacheAccessor);
				}
			}
			if (!string.IsNullOrEmpty(configNode.Before))
			{
				var dec = new StyleDeclaration();
				dec.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.String, SpecialCharacterHandling.MakeSafeCss(configNode.Before)) });
				if (fwStyleSheet != null && fwStyleSheet.Styles.Contains(BeforeAfterBetweenStyleName))
				{
					dec.Properties.AddRange(GenerateCssStyleFromLcmStyleSheet(BeforeAfterBetweenStyleName, analWsId, fwStyleSheet, wsEngine));
				}
				var selectorBase = simpleSelector;
				if (configNode.FieldDescription == "PicturesOfSenses")
				{
					selectorBase += "> div:first-child";
				}
				var beforeRule = new StyleRule(dec) { Value = GetBaseSelectionWithSelectors(selectorBase, ":before") };
				rules.Add(beforeRule);
			}
			if (!string.IsNullOrEmpty(configNode.After))
			{
				var dec = new StyleDeclaration();
				dec.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.String, SpecialCharacterHandling.MakeSafeCss(configNode.After)) });
				if (fwStyleSheet != null && fwStyleSheet.Styles.Contains(BeforeAfterBetweenStyleName))
				{
					dec.Properties.AddRange(GenerateCssStyleFromLcmStyleSheet(BeforeAfterBetweenStyleName, analWsId, fwStyleSheet, wsEngine));
				}
				var selectorBase = simpleSelector;
				if (configNode.FieldDescription == "PicturesOfSenses")
				{
					selectorBase += "> div:last-child";
				}
				var afterRule = new StyleRule(dec) { Value = GetBaseSelectionWithSelectors(selectorBase, ":after") };
				rules.Add(afterRule);
			}
			return rules;
		}

		/// <summary>
		/// If configNode is the Type node for a factored collection of references, strip the collection singular selector from the parent selector
		/// </summary>
		private static string GetParentForFactoredReference(string parentSelector, ConfigurableDictionaryNode configNode)
		{
			if (!IsFactoredReferenceType(configNode))
			{
				return parentSelector;
			}
			var parentPlural = GetClassAttributeForConfig(configNode.Parent);
			var parentSingular = GetClassAttributeForCollectionItem(configNode.Parent);
			return parentSelector.Replace($".{parentPlural} .{parentSingular}", '.' + parentPlural);
		}

		private static bool IsFactoredReferenceType(ConfigurableDictionaryNode configNode)
		{
			var parent = configNode.Parent;
			return parent != null && ConfiguredLcmGenerator.IsFactoredReference(parent, out var typeNode) && ReferenceEquals(typeNode, configNode);
		}

		/// <summary>
		/// Method to create matching selector based on baseSelection
		/// If baseSelection ends with span, first-child/last-child will add before the before/after selector
		/// </summary>
		/// <param name="baseSelection">baseselector value</param>
		/// <param name="selector">Before/After selector</param>
		/// <returns></returns>
		private static string GetBaseSelectionWithSelectors(string baseSelection, string selector)
		{
			var baseSelectionValue = baseSelection;
			if (baseSelection.LastIndexOf("span", StringComparison.Ordinal) != baseSelection.Length - 4)
			{
				return baseSelectionValue + selector;
			}
			var firstOrLastChild = selector == ":before" ? ":first-child" : ":last-child";
			baseSelectionValue = baseSelectionValue + firstOrLastChild + selector;
			return baseSelectionValue;
		}

		private static void GenerateFlowResetForBaseNode(string baseSelection, List<StyleRule> rules)
		{
			var flowResetRule = new StyleRule();
			flowResetRule.Value = baseSelection;
			flowResetRule.Declarations.Properties.Add(new Property("clear") { Term = new PrimitiveTerm(UnitType.Ident, "both") });
			flowResetRule.Declarations.Properties.Add(new Property("white-space") { Term = new PrimitiveTerm(UnitType.Ident, "pre-wrap") });
			rules.Add(flowResetRule);
		}

		/// <summary>
		/// Generates a selector for a class name that matches xhtml that is generated for the configNode.
		/// e.g. '.entry' or '.sense'
		/// </summary>
		/// <param name="configNode"></param>
		/// <param name="cache">defaults to null, necessary for generating correct css for custom field nodes</param>
		private static string SelectClassName(ConfigurableDictionaryNode configNode, LcmCache cache = null)
		{
			var type = ConfiguredLcmGenerator.GetPropertyTypeForConfigurationNode(configNode, cache?.GetManagedMetaDataCache());
			return SelectClassName(configNode, type);
		}

		private static string SelectClassName(ConfigurableDictionaryNode configNode, PropertyType type)
		{
			switch (type)
			{
				case PropertyType.CollectionType:
				{
					// for collections we generate a css selector to match each item e.g '.senses .sense'
					return $".{GetClassAttributeForConfig(configNode)} .{GetClassAttributeForCollectionItem(configNode)}";
				}
				case PropertyType.CmPictureType:
				{
					return " img"; // Pictures are written out as img tags
				}
				case PropertyType.PrimitiveType:
				case PropertyType.MoFormType:
				{
						// for multi-lingual strings each language's string will have the contents generated in a span
						// for multi-lingual strings each language's string will have the contents generated in a span
						if (configNode.DictionaryNodeOptions is DictionaryNodeWritingSystemOptions)
						{
							var spanStyle = string.Empty;
							if (!DictionaryConfigurationModel.NoteInParaStyles.Contains(configNode.FieldDescription))
							{
								spanStyle = "> span";
							}
							return "." + GetClassAttributeForConfig(configNode) + spanStyle;
						}
						goto default;
				}
				default:
					return "." + GetClassAttributeForConfig(configNode);
			}
		}

		/// <summary>
		/// Generate the singular of the collection name: drop the final character ('s') or
		/// handle "entries" => "entry" or "analyses" to "analysis" or "glosses" to "gloss"
		/// </summary>
		internal static string GetClassAttributeForCollectionItem(ConfigurableDictionaryNode configNode)
		{
			var classNameBase = GetClassAttributeBase(configNode).ToLower();
			string singularBase;
			if (classNameBase.EndsWith("ies"))
			{
				singularBase = classNameBase.Remove(classNameBase.Length - 3) + "y";
			}
			else if (classNameBase.EndsWith("analyses"))
			{
				singularBase = classNameBase.Remove(classNameBase.Length - 2) + "is";
			}
			else if (classNameBase.EndsWith("sses"))
			{
				singularBase = classNameBase.Remove(classNameBase.Length - 2);
			}
			else
			{
				singularBase = classNameBase.Remove(classNameBase.Length - 1);
			}
			return CustomIcu.GetIcuNormalizer(FwNormalizationMode.knmNFC).Normalize(singularBase + GetClassAttributeDupSuffix(configNode).ToLower());
		}

		/// <summary>
		/// For collection type nodes, generates a selector on the collection as a whole.  For all other nodes,
		/// calls SelectClassName to generate the selector.
		/// </summary>
		/// <remarks>
		/// Perhaps SelectClassName should have been changed, but that's a rather far reaching change.  Using the
		/// output of this method for :before and :after rules in the css is sufficient to fix the bug reported in
		/// LT-17048.  A better name might be nice, but this one is fairly descriptive.
		/// </remarks>
		private static string SelectBareClassName(ConfigurableDictionaryNode configNode, IFwMetaDataCacheManaged metaDataCacheAccessor = null)
		{
			var type = ConfiguredLcmGenerator.GetPropertyTypeForConfigurationNode(configNode, metaDataCacheAccessor);
			if (type == PropertyType.CollectionType)
			{
				return "." + GetClassAttributeForConfig(configNode);
			}
			return SelectClassName(configNode, type);
		}

		/// <summary>
		/// Generates a class name for the given configuration for use by Css and XHTML.
		/// Uses SubField and CSSClassNameOverride attributes where found
		/// </summary>
		internal static string GetClassAttributeForConfig(ConfigurableDictionaryNode configNode)
		{
			var classAtt = CustomIcu.GetIcuNormalizer(FwNormalizationMode.knmNFC).Normalize((GetClassAttributeBase(configNode) + GetClassAttributeDupSuffix(configNode)).ToLower());
			// Custom field names might begin with a digit which would cause invalid css, so we prepend 'cf' to those class names.
			return char.IsDigit(Convert.ToChar(classAtt.Substring(0, 1))) ? "cf" + classAtt : classAtt;
		}

		private static string GetClassAttributeBase(ConfigurableDictionaryNode configNode)
		{
			// use the FieldDescription as the class name, and append a '_' followed by the SubField if it is defined.
			// Note that custom fields can have spaces in their names, which CSS can't handle.  Convert spaces to hyphens,
			// which CSS allows but FieldWorks doesn't use (except maybe in custom fields).
			if (string.IsNullOrEmpty(configNode.CSSClassNameOverride))
			{
				var classAttribute = string.Empty; // REVIEW (Hasso) 2016.10: use StringBuilder
				if (configNode.DictionaryNodeOptions is DictionaryNodeGroupingOptions)
				{
					classAttribute += "grouping_";
				}
				classAttribute += configNode.FieldDescription.Replace(' ', '-') + (string.IsNullOrEmpty(configNode.SubField) ? "" : "_" + configNode.SubField);
				return classAttribute;
			}
			return configNode.CSSClassNameOverride;
		}

		private static string GetClassAttributeDupSuffix(ConfigurableDictionaryNode configNode)
		{
			return configNode.IsDuplicate
				? "_" + (configNode.LabelSuffix = Regex.Replace(configNode.LabelSuffix, "[^a-zA-Z0-9+]", "-"))
				: string.Empty;
		}

		internal static StyleDeclaration GetOnlyCharacterStyle(StyleDeclaration fullStyleDeclaration)
		{
			var declaration = new StyleDeclaration();
			foreach (var prop in fullStyleDeclaration.Where(prop => prop.Name.Contains("font") || prop.Name.Contains("color")))
			{
				declaration.Add(prop);
			}
			return declaration;
		}

		internal static StyleDeclaration GetOnlyParagraphStyle(StyleDeclaration fullStyleDeclaration)
		{
			var declaration = new StyleDeclaration();
			foreach (var prop in fullStyleDeclaration.Where(prop => !prop.Name.Contains("font") && !prop.Name.Contains("color") && !prop.Name.Contains("content") && !prop.Name.Contains("counter-increment")))
			{
				declaration.Add(prop);
			}
			return declaration;
		}

		internal static StyleDeclaration GetOnlyBulletContent(StyleDeclaration fullStyleDeclaration)
		{
			var declaration = new StyleDeclaration();
			foreach (var prop in fullStyleDeclaration.Where(prop => prop.Name.Contains("content") || prop.Name.Contains("counter-increment")))
			{
				declaration.Add(prop);
			}
			return declaration;
		}

		internal static string GetOnlyCounterResetContent(StyleDeclaration fullStyleDeclaration)
		{
			var counterProp = fullStyleDeclaration.FirstOrDefault(prop => prop.Name.Contains("counter-increment"));
			return counterProp?.Term.ToString() ?? string.Empty;
		}

		/// <summary>
		/// Generates a css StyleDeclaration for the requested FieldWorks style.
		/// <remarks>internal to facilitate separate unit testing.</remarks>
		/// </summary>
		internal static StyleDeclaration GenerateCssStyleFromLcmStyleSheet(string styleName, int wsId, LcmStyleSheet styleSheet, ILgWritingSystem lgWritingSystem)
		{
			return GenerateCssStyleFromLcmStyleSheet(styleName, wsId, null, styleSheet, lgWritingSystem);
		}

		/// <summary>
		/// Generates a css StyleDeclaration for the requested FieldWorks style.
		/// <remarks>internal to facilitate separate unit testing.</remarks>
		/// </summary>
		/// <param name="styleName"></param>
		/// <param name="wsId">writing system id</param>
		/// <param name="node">The configuration node to use for generating paragraph margin in context</param>
		/// <param name="styleSheet"></param>
		/// <param name="lgWritingSystem"></param>
		/// <returns></returns>
		internal static StyleDeclaration GenerateCssStyleFromLcmStyleSheet(string styleName, int wsId, ConfigurableDictionaryNode node, LcmStyleSheet styleSheet, ILgWritingSystem lgWritingSystem)
		{
			return GenerateCssStyleFromLcmStyleSheet(styleName, wsId, node, styleSheet, lgWritingSystem, false)[0];
		}

		internal static List<StyleDeclaration> GenerateCssStyleFromLcmStyleSheet(string styleName, int wsId, ConfigurableDictionaryNode node, LcmStyleSheet styleSheet, ILgWritingSystem lgWritingSystem, bool calculateFirstSenseStyle)
		{
			var declaration = new StyleDeclaration();
			if (styleSheet == null || !styleSheet.Styles.Contains(styleName))
			{
				return new List<StyleDeclaration> { declaration };
			}
			var projectStyle = styleSheet.Styles[styleName];
			var exportStyleInfo = new ExportStyleInfo(projectStyle);
			var hangingIndent = 0.0f;
			// Tuple ancestorIndents used for ancestor components leadingIndent and hangingIndent.
			var ancestorIndents = new AncestorIndents(0.0f, 0.0f);
			if (exportStyleInfo.IsParagraphStyle && node != null)
			{
				ancestorIndents = CalculateParagraphIndentsFromAncestors(node, styleSheet, ancestorIndents);
			}
			if (exportStyleInfo.HasAlignment)
			{
				declaration.Add(new Property("text-align") { Term = new PrimitiveTerm(UnitType.Ident, exportStyleInfo.Alignment.AsCssString()) });
			}
			if (exportStyleInfo.HasBorder)
			{
				if (exportStyleInfo.HasBorderColor)
				{
					var borderColor = new Property("border-color")
					{
						Term = new HtmlColor(exportStyleInfo.BorderColor.A, exportStyleInfo.BorderColor.R, exportStyleInfo.BorderColor.G, exportStyleInfo.BorderColor.B)
					};
					declaration.Add(borderColor);
				}
				var borderLeft = new Property("border-left-width")
				{
					Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.BorderLeading))
				};
				var borderRight = new Property("border-right-width")
				{
					Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.BorderTrailing))
				};
				var borderTop = new Property("border-top-width")
				{
					Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.BorderTop))
				};
				var borderBottom = new Property("border-bottom-width")
				{
					Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.BorderBottom))
				};
				declaration.Add(borderLeft);
				declaration.Add(borderRight);
				declaration.Add(borderTop);
				declaration.Add(borderBottom);
			}
			if (exportStyleInfo.HasFirstLineIndent)
			{
				// Handles both first-line and hanging indent, hanging-indent will result in a negative text-indent value
				var firstLineIndentValue = MilliPtToPt(exportStyleInfo.FirstLineIndent);

				if (firstLineIndentValue < 0.0f)
				{
					hangingIndent = firstLineIndentValue;
				}
				declaration.Add(new Property("text-indent") { Term = new PrimitiveTerm(UnitType.Point, firstLineIndentValue) });
			}
			if (exportStyleInfo.HasKeepTogether)
			{
				declaration.Add(new Property("page-break-inside") { Term = new PrimitiveTerm(UnitType.Ident, "avoid") });
			}
			if (exportStyleInfo.HasKeepWithNext)
			{
				declaration.Add(new Property("page-break-inside") { Term = new PrimitiveTerm(UnitType.Ident, "initial") });
			}
			if (exportStyleInfo.HasLeadingIndent || hangingIndent < 0.0f || ancestorIndents.TextIndent < 0.0f)
			{
				var leadingIndent = CalculateMarginLeft(exportStyleInfo, ancestorIndents, hangingIndent);
				var marginDirection = "margin-left";
				if (exportStyleInfo.DirectionIsRightToLeft == TriStateBool.triTrue)
				{
					marginDirection = "margin-right";
				}
				declaration.Add(new Property(marginDirection) { Term = new PrimitiveTerm(UnitType.Point, leadingIndent) });
			}
			if (exportStyleInfo.HasLineSpacing)
			{
				var lineHeight = new Property("line-height");
<<<<<<< HEAD:Src/LanguageExplorer/DictionaryConfiguration/CssGenerator.cs
				if (!exportStyleInfo.LineSpacing.m_relative && exportStyleInfo.LineSpacing.m_lineHeight >= 0)
				{
					lineHeight = new Property("flex-line-height");
				}
||||||| f013144d5:Src/xWorks/CssGenerator.cs
				if (!exportStyleInfo.LineSpacing.m_relative && exportStyleInfo.LineSpacing.m_lineHeight >= 0)
					lineHeight = new Property("flex-line-height");
=======
>>>>>>> develop:Src/xWorks/CssGenerator.cs
				//m_relative means single, 1.5 or double line spacing was chosen. The CSS should be a number
				if (exportStyleInfo.LineSpacing.m_relative)
				{
					// The relative value is stored internally multiplied by 10000.  (FieldWorks code generally hates floating point.)
					// CSS expects to see the actual floating point value.  See https://jira.sil.org/browse/LT-16735.
					lineHeight.Term = new PrimitiveTerm(UnitType.Number, Math.Abs(exportStyleInfo.LineSpacing.m_lineHeight) / 10000.0F);
				}
				else
				{
					// Note: In Flex a user can set 'at least' or 'exactly' for line heights. These are differentiated using negative and positive
					// values in LineSpacing.m_lineHeight.
					// There is no reasonable way to handle the 'exactly' option in css so both will behave the same for html views and exports
					lineHeight.Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(Math.Abs(exportStyleInfo.LineSpacing.m_lineHeight)));
					if (exportStyleInfo.LineSpacing.m_lineHeight >= 0)
					{
						// The flex-line-height property is used here for continued Pathway export support
						var flexLineHeight = new Property("flex-line-height");
						flexLineHeight.Term = lineHeight.Term;
						declaration.Add(flexLineHeight);
					}
				}
				declaration.Add(lineHeight);
			}
			if (exportStyleInfo.HasSpaceAfter)
			{
				declaration.Add(new Property("padding-bottom") { Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.SpaceAfter)) });
			}
			if (exportStyleInfo.HasSpaceBefore)
			{
				declaration.Add(new Property("padding-top") { Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.SpaceBefore)) });
			}
			if (exportStyleInfo.HasTrailingIndent)
			{
				var paddingDirection = "padding-right";
				if (exportStyleInfo.DirectionIsRightToLeft == TriStateBool.triTrue)
				{
					paddingDirection = "padding-left";
				}
				declaration.Add(new Property(paddingDirection) { Term = new PrimitiveTerm(UnitType.Point, MilliPtToPt(exportStyleInfo.TrailingIndent)) });
			}
			AddFontInfoCss(projectStyle, projectStyle.FontInfoForWs(wsId), declaration, lgWritingSystem);
			if (exportStyleInfo.NumberScheme != 0)
			{
				var numScheme = exportStyleInfo.NumberScheme.ToString();
				if (!string.IsNullOrEmpty(exportStyleInfo.BulletInfo.m_bulletCustom))
				{
					var customBullet = exportStyleInfo.BulletInfo.m_bulletCustom;
					declaration.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.String, customBullet) });
				}
				else if (BulletSymbolsCollection.ContainsKey(exportStyleInfo.NumberScheme.ToString()))
				{
					var selectedBullet = BulletSymbolsCollection[numScheme];
					declaration.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.String, selectedBullet) });
				}
				else if (NumberingStylesCollection.ContainsKey(exportStyleInfo.NumberScheme.ToString()))
				{
					if (node != null)
					{
<<<<<<< HEAD:Src/LanguageExplorer/DictionaryConfiguration/CssGenerator.cs
						var selectedNumStyle = NumberingStylesCollection[numScheme];
						declaration.Add(new Property("counter-increment") { Term = new PrimitiveTerm(UnitType.Attribute, " " + node.Label.ToLower()) });
						declaration.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.Attribute, $" counter({node.Label.ToLower()}, {selectedNumStyle}) {@"' '"}") });
||||||| f013144d5:Src/xWorks/CssGenerator.cs
						string selectedNumStyle = NumberingStylesCollection[numScheme];
						declaration.Add(new Property("counter-increment") { Term = new PrimitiveTerm(UnitType.Attribute, " " + node.Label.ToLower()) });
						declaration.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.Attribute, string.Format(" counter({0}, {1}) {2}", node.Label.ToLower(), selectedNumStyle, @"' '")) });
=======
						string selectedNumStyle = NumberingStylesCollection[numScheme];

						if (string.IsNullOrEmpty(node.CSSClassNameOverride))
							node.CSSClassNameOverride = GetClassAttributeForConfig(node);

						declaration.Add(new Property("counter-increment") { Term = new PrimitiveTerm(UnitType.Attribute, " " + node.CSSClassNameOverride) });
						declaration.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.Attribute, string.Format(" counter({0}, {1}) {2}", node.CSSClassNameOverride, selectedNumStyle, @"' '")) });
>>>>>>> develop:Src/xWorks/CssGenerator.cs
					}
				}
			}
			var styleList = new List<StyleDeclaration> { declaration };
			if (calculateFirstSenseStyle && ancestorIndents.Ancestor != null)
			{
				if (ancestorIndents.Ancestor.DictionaryNodeOptions is DictionaryNodeSenseOptions senseOptions && senseOptions.DisplayEachSenseInAParagraph)
				{
					ancestorIndents = CalculateParagraphIndentsFromAncestors(ancestorIndents.Ancestor, styleSheet, new AncestorIndents(0f, 0f));
					var marginLeft = CalculateMarginLeft(exportStyleInfo, ancestorIndents, hangingIndent);
					var firstSenseStyle = new StyleDeclaration();
					var marginDirection = "margin-left";
					if (exportStyleInfo.DirectionIsRightToLeft == TriStateBool.triTrue)
					{
						marginDirection = "margin-right";
					}
					firstSenseStyle.Properties.AddRange(declaration.Where(p => p.Name != marginDirection));
					firstSenseStyle.Properties.Add(new Property(marginDirection) { Term = new PrimitiveTerm(UnitType.Point, marginLeft) });
					styleList.Insert(0, firstSenseStyle);
				}
			}
			if (exportStyleInfo.DirectionIsRightToLeft != TriStateBool.triNotSet)
			{
				// REVIEW (Hasso) 2016.07: I think the only time this matters is when the user has paragraphs (senses, subentries, etc)
				// REVIEW (cont) whose directions oppose Dictionary-Normal. In this case, O Pesky Users, we will need to know which direction the
				// REVIEW (cont) paragraph is going when we generate the innermost strings. Implementing this will be pricy for paragraphy
				// REVIEW (cont) dictionaries, but beneficial for only our small bidirectional contingency. Alas, O Pesky Users.
				// REVIEW (cont) But we may need a CSS fix for bidirectionality until we can get GeckoFx 47+. O Fair Quill, Delicate Parchment.
				declaration.Add(new Property("direction") { Term = new PrimitiveTerm(UnitType.Ident, exportStyleInfo.DirectionIsRightToLeft == TriStateBool.triTrue ? "rtl" : "ltr") });
			}
			return styleList;
		}

		private static float CalculateMarginLeft(ExportStyleInfo exportStyleInfo, AncestorIndents ancestorIndents, float hangingIndent)
		{
			var leadingIndent = 0.0f;
			if (exportStyleInfo.HasLeadingIndent)
			{
				leadingIndent = MilliPtToPt(exportStyleInfo.LeadingIndent);
			}
			var ancestorMargin = ancestorIndents.Margin - ancestorIndents.TextIndent;
			leadingIndent -= ancestorMargin + hangingIndent;
			return (float)Math.Round(leadingIndent, 3);
		}

		private static AncestorIndents CalculateParagraphIndentsFromAncestors(ConfigurableDictionaryNode currentNode, LcmStyleSheet styleSheet, AncestorIndents ancestorIndents)
		{
			var parentNode = currentNode;
			do
			{
				parentNode = parentNode.Parent;
				if (parentNode == null)
				{
					return ancestorIndents;
				}
			} while (!IsParagraphStyle(parentNode, styleSheet));
			var projectStyle = styleSheet.Styles[parentNode.Style];
			var exportStyleInfo = new ExportStyleInfo(projectStyle);
			return new AncestorIndents(parentNode, GetLeadingIndent(exportStyleInfo), GetHangingIndentIfAny(exportStyleInfo));
		}

		private static float GetHangingIndentIfAny(ExportStyleInfo exportStyleInfo)
		{
			// Handles both first-line and hanging indent: hanging indent represented as a negative first-line indent value
			return exportStyleInfo.HasFirstLineIndent && exportStyleInfo.FirstLineIndent < 0 ? MilliPtToPt(exportStyleInfo.FirstLineIndent) : 0.0f;
		}

		private static float GetLeadingIndent(ExportStyleInfo exportStyleInfo)
		{
			return exportStyleInfo.HasLeadingIndent ? MilliPtToPt(exportStyleInfo.LeadingIndent) : 0.0f;
		}

		private static bool IsParagraphStyle(ConfigurableDictionaryNode node, LcmStyleSheet styleSheet)
		{
			if (node.StyleType == StyleTypes.Character)
			{
				return false;
			}
			var style = node.Style;
			return !string.IsNullOrEmpty(style) && styleSheet.Styles.Contains(style) && styleSheet.Styles[style].IsParagraphStyle;
		}

		/// <summary>
		/// Mapping the bullet symbols with the number system
		/// </summary>
		private static void LoadBulletUnicodes()
		{
			if (BulletSymbolsCollection.Count > 0)
			{
				return;
			}
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
		/// Mapping the numbering styles with the content's number format
		/// </summary>
		private static void LoadNumberingStyles()
		{
			if (NumberingStylesCollection.Count > 0)
			{
				return;
			}
			NumberingStylesCollection.Add("kvbnNumberBase", "decimal");
			NumberingStylesCollection.Add("kvbnArabic", "decimal");
			NumberingStylesCollection.Add("kvbnRomanLower", "lower-roman");
			NumberingStylesCollection.Add("kvbnRomanUpper", "upper-roman");
			NumberingStylesCollection.Add("kvbnLetterLower", "lower-alpha");
			NumberingStylesCollection.Add("kvbnLetterUpper", "upper-alpha");
			NumberingStylesCollection.Add("kvbnArabic01", "decimal-leading-zero");
		}

		/// <summary>
		/// In the FwStyles values were stored in millipoints to avoid expensive floating point calculations in c++ code.
		/// We need to convert these to points for use in css styles.
		/// </summary>
		private static float MilliPtToPt(int millipoints)
		{
			return (float)Math.Round((float)millipoints / 1000, 3);
		}

		/// <summary>
		/// Builds the css rules for font info properties using the writing system overrides
		/// </summary>
		private static void AddFontInfoCss(BaseStyleInfo projectStyle, FontInfo wsFontInfo, StyleDeclaration declaration, ILgWritingSystem lgWritingSystem)
		{
			var defaultFontInfo = projectStyle.DefaultCharacterStyleInfo;
			// set fontName to the wsFontInfo publicly accessible InheritableStyleProp value if set, otherwise the
			// defaultFontInfo if set, or null.
			var fontName = wsFontInfo.m_fontName.ValueIsSet ? wsFontInfo.m_fontName.Value : defaultFontInfo.FontName.ValueIsSet ? defaultFontInfo.FontName.Value : null;
			// fontName still null means not set in Normal Style, then get default fonts from WritingSystems configuration.
			// Comparison, projectStyle.Name == "Normal", required to limit the font-family definition to the
			// empty span (ie span[lang|="en"]{}. If not included, font-family will be added to many more spans.
			if (fontName == null && projectStyle.Name == "Normal")
			{
				if (lgWritingSystem != null)
				{
					fontName = lgWritingSystem.DefaultFontName;
				}
			}
			if (fontName != null)
			{
				var fontFamily = new Property("font-family")
				{
					Term = new TermList(new PrimitiveTerm(UnitType.String, fontName), new PrimitiveTerm(UnitType.Ident, "serif"))
				};
				declaration.Add(fontFamily);
			}
			// For the following additions, wsFontInfo is publicly accessible InheritableStyleProp value if set (ie. m_fontSize, m_bold, etc.),
			// checks for explicit overrides. Otherwise the defaultFontInfo if set (ie. FontSize, Bold, etc), or null.
			AddInfoFromWsOrDefaultValue(wsFontInfo.m_fontSize, defaultFontInfo.FontSize, "font-size", UnitType.Point, declaration);
			AddInfoFromWsOrDefaultValue(wsFontInfo.m_bold, defaultFontInfo.Bold, "font-weight", "bold", "normal", declaration);
			AddInfoFromWsOrDefaultValue(wsFontInfo.m_italic, defaultFontInfo.Italic, "font-style", "italic", "normal", declaration);
			AddInfoFromWsOrDefaultValue(wsFontInfo.m_fontColor, defaultFontInfo.FontColor, "color", declaration);
			AddInfoFromWsOrDefaultValue(wsFontInfo.m_backColor, defaultFontInfo.BackColor, "background-color", declaration);
			AddInfoFromWsOrDefaultValue(wsFontInfo.m_superSub, defaultFontInfo.SuperSub, declaration);
			AddFontFeaturesFromWsOrDefaultValue(wsFontInfo.m_features, defaultFontInfo.Features, declaration);
			AddInfoForUnderline(wsFontInfo, defaultFontInfo, declaration);
		}

		/// <summary>
		/// Generates css from boolean style values using writing system overrides where appropriate
		/// </summary>
		private static void AddInfoFromWsOrDefaultValue(InheritableStyleProp<bool> wsFontInfo, IStyleProp<bool> defaultFontInfo, string propName, string trueValue, string falseValue, StyleDeclaration declaration)
		{
			if (!GetFontValue(wsFontInfo, defaultFontInfo, out var fontValue))
			{
				return;
			}
			var fontProp = new Property(propName)
			{
				Term = new PrimitiveTerm(UnitType.Ident, fontValue ? trueValue : falseValue)
			};
			declaration.Add(fontProp);
		}

		/// <summary>
		/// Generates css from Color style values using writing system overrides where appropriate
		/// </summary>
		private static void AddInfoFromWsOrDefaultValue(InheritableStyleProp<Color> wsFontInfo, IStyleProp<Color> defaultFontInfo, string propName, StyleDeclaration declaration)
		{
			if (!GetFontValue(wsFontInfo, defaultFontInfo, out var fontValue))
			{
				return;
			}
			var fontProp = new Property(propName)
			{
				Term = new PrimitiveTerm(UnitType.RGB, HtmlColor.FromRgba(fontValue.R, fontValue.G, fontValue.B, fontValue.A).ToString())
			};
			declaration.Add(fontProp);
		}

		/// <summary>
		/// Generates css from integer style values using writing system overrides where appropriate
		/// </summary>
		private static void AddInfoFromWsOrDefaultValue(InheritableStyleProp<int> wsFontInfo, IStyleProp<int> defaultFontInfo, string propName, UnitType termType, StyleDeclaration declaration)
		{
			if (!GetFontValue(wsFontInfo, defaultFontInfo, out var fontValue))
			{
				return;
			}
			var fontProp = new Property(propName)
			{
				Term = new PrimitiveTerm(termType, MilliPtToPt(fontValue))
			};
			declaration.Add(fontProp);
		}

		/// <summary>
		/// Generates css from string style values using writing system overrides where appropriate
		/// </summary>
		private static void AddFontFeaturesFromWsOrDefaultValue(InheritableStyleProp<string> wsFontInfo, IStyleProp<string> defaultFontInfo, StyleDeclaration declaration)
		{
			if (!GetFontValue(wsFontInfo, defaultFontInfo, out var fontValue))
			{
				return;
			}
			var fontProp = new Property("font-feature-settings")
			{
				Term = ConvertToCssFeatures(fontValue)
			};
			declaration.Add(fontProp);
		}

		/// <summary>
		/// Converts values similar to 'Eng=2,smcp=1' into '"Eng" 2,"smcp" 1
		/// see web documentation for "font-feature-settings" css attribute
		/// </summary>
		/// <remarks>ExCss doesn't support this type of attribute well so we build it by hand</remarks>
		private static Term ConvertToCssFeatures(string fontValue)
		{
			return new PrimitiveTerm(UnitType.Unknown, string.Join(",", fontValue.Split(',').Select(f => $"\"{f.Replace("=", "\" ")}")));
		}

		/// <summary>
		/// Generates css from SuperSub style values using writing system overrides where appropriate
		/// </summary>
		private static void AddInfoFromWsOrDefaultValue(InheritableStyleProp<FwSuperscriptVal> wsFontInfo, IStyleProp<FwSuperscriptVal> defaultFontInfo, StyleDeclaration declaration)
		{
			if (!GetFontValue(wsFontInfo, defaultFontInfo, out var fontValue))
			{
				return;
			}
			var sizeProp = new Property("font-size")
			{
				//58% is what OpenOffice does
				Term = new PrimitiveTerm(UnitType.Ident, "58%")
			};
			declaration.Add(sizeProp);
			if (fontValue != FwSuperscriptVal.kssvOff)
			{
				var position = new Property("position")
				{
					Term = new PrimitiveTerm(UnitType.Ident, "relative")
				};
				var top = new Property("top")
				{
					Term = fontValue == FwSuperscriptVal.kssvSub ? new PrimitiveTerm(UnitType.Pixel, "0.3em") : new PrimitiveTerm(UnitType.Pixel, "-0.6em")
				};
				declaration.Add(position);
				declaration.Add(top);
			}
		}

		private static void AddInfoForUnderline(FontInfo wsFont, ICharacterStyleInfo defaultFont, StyleDeclaration declaration)
		{
			if (!GetFontValue(wsFont.m_underline, defaultFont.Underline, out var underlineType))
			{
				return;
			}
			switch (underlineType)
			{
				case (FwUnderlineType.kuntDouble):
					{
						// use border to generate second underline then generate the standard underline
						var fontProp = new Property("border-bottom");
						var termList = new TermList();
						termList.AddTerm(new PrimitiveTerm(UnitType.Pixel, 1));
						termList.AddSeparator(TermList.TermSeparator.Space);
						termList.AddTerm(new PrimitiveTerm(UnitType.Ident, "solid"));
						fontProp.Term = termList;
						declaration.Add(fontProp);
						// The wsFontInfo is publicly accessible InheritableStyleProp value if set, checks for explicit overrides.
						// Otherwise the defaultFontInfo if set, or null.
						AddInfoFromWsOrDefaultValue(wsFont.m_underlineColor, defaultFont.UnderlineColor, "border-bottom-color", declaration);
						goto case FwUnderlineType.kuntSingle; //fall through to single
					}
				case (FwUnderlineType.kuntSingle):
					{
						var fontProp = new Property("text-decoration")
						{
							Term = new PrimitiveTerm(UnitType.Ident, "underline")
						};
						declaration.Add(fontProp);
						// The wsFontInfo is publicly accessible InheritableStyleProp value if set, checks for explicit overrides.
						// Otherwise the defaultFontInfo if set, or null.
						AddInfoFromWsOrDefaultValue(wsFont.m_underlineColor, defaultFont.UnderlineColor, "text-decoration-color", declaration);
						break;
					}
				case (FwUnderlineType.kuntStrikethrough):
					{
						var fontProp = new Property("text-decoration")
						{
							Term = new PrimitiveTerm(UnitType.Ident, "line-through")
						};
						declaration.Add(fontProp);
						// The wsFontInfo is publicly accessible InheritableStyleProp value if set, checks for explicit overrides.
						// Otherwise the defaultFontInfo if set, or null.
						AddInfoFromWsOrDefaultValue(wsFont.m_underlineColor, defaultFont.UnderlineColor, "text-decoration-color", declaration);
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
						termList.AddTerm(new PrimitiveTerm(UnitType.Ident, underlineType == FwUnderlineType.kuntDashed ? "dashed" : "dotted"));
						fontProp.Term = termList;
						declaration.Add(fontProp);
						// The wsFontInfo is publicly accessible InheritableStyleProp value if set, checks for explicit overrides.
						// Otherwise the defaultFontInfo if set, or null.
						AddInfoFromWsOrDefaultValue(wsFont.m_underlineColor, defaultFont.UnderlineColor, "border-bottom-color", declaration);
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
		private static bool GetFontValue<T>(InheritableStyleProp<T> wsFontInfo, IStyleProp<T> defaultFontInfo, out T fontValue)
		{
			fontValue = default;
			if (wsFontInfo.ValueIsSet)
			{
				fontValue = wsFontInfo.Value;
			}
			else if (defaultFontInfo.ValueIsSet)
			{
				fontValue = defaultFontInfo.Value;
			}
			else
			{
				return false;
			}
			return true;
		}

		/// <summary>
		/// Extension method to provide a css string conversion from an FwTextAlign enum value
		/// </summary>
		public static string AsCssString(this FwTextAlign align)
		{
			switch (align)
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

		public static void GenerateLetterHeaderCss(StyleSheet styleSheet, LcmStyleSheet lcmStyleSheet)
		{
			var letHeadRule = new StyleRule { Value = ".letHead" };
			letHeadRule.Declarations.Properties.Add(new Property("-moz-column-count") { Term = new PrimitiveTerm(UnitType.Number, 1) });
			letHeadRule.Declarations.Properties.Add(new Property("-webkit-column-count") { Term = new PrimitiveTerm(UnitType.Number, 1) });
			letHeadRule.Declarations.Properties.Add(new Property("column-count") { Term = new PrimitiveTerm(UnitType.Number, 1) });
			letHeadRule.Declarations.Properties.Add(new Property("clear") { Term = new PrimitiveTerm(UnitType.Ident, "both") });
			letHeadRule.Declarations.Properties.Add(new Property("width") { Term = new PrimitiveTerm(UnitType.Percentage, 100) });
			letHeadRule.Declarations.Properties.AddRange(GetOnlyParagraphStyle(GenerateCssStyleFromLcmStyleSheet(LetterHeadingStyleName, 0, lcmStyleSheet, null)));
			styleSheet.Rules.Add(letHeadRule);
		}

		public static string GenerateCssForPageButtons()
		{
			var screenPages = new StyleRule { Value = ".pages" };
			screenPages.Declarations.Properties.Add(new Property("display") { Term = new PrimitiveTerm(UnitType.Ident, "table") });
			screenPages.Declarations.Properties.Add(new Property("width") { Term = new PrimitiveTerm(UnitType.Percentage, 100) });
			var screen = new MediaRule { Condition = "screen", RuleSets = { screenPages } };
			var printPages = new StyleRule { Value = ".pages" };
			printPages.Declarations.Properties.Add(new Property("display") { Term = new PrimitiveTerm(UnitType.Ident, "none") });
			var print = new MediaRule { Condition = "print", RuleSets = { printPages } };
			var pageButtonHover = new StyleRule { Value = ".pagebutton:hover" };
			pageButtonHover.Declarations.Properties.Add(new Property("background") { Term = new PrimitiveTerm(UnitType.Grad, "linear-gradient(to bottom, #dfdfdf 5%, #ededed 100%)") });
			pageButtonHover.Declarations.Properties.Add(new Property("background-color") { Term = new PrimitiveTerm(UnitType.RGB, "#cdcdcd") });
			var pageButtonActive = new StyleRule { Value = ".pagebutton:active" };
			pageButtonActive.Declarations.Properties.Add(new Property("position") { Term = new PrimitiveTerm(UnitType.Ident, "relative") });
			pageButtonActive.Declarations.Properties.Add(new Property("top") { Term = new PrimitiveTerm(UnitType.Pixel, 1) });
			var pageButton = new StyleRule { Value = ".pagebutton" };
			pageButton.Declarations.Properties.Add(new Property("display") { Term = new PrimitiveTerm(UnitType.Ident, "table-cell") });
			pageButton.Declarations.Properties.Add(new Property("cursor") { Term = new PrimitiveTerm(UnitType.Ident, "pointer") });
			pageButton.Declarations.Properties.Add(new Property("color") { Term = new PrimitiveTerm(UnitType.RGB, "#777777") });
			pageButton.Declarations.Properties.Add(new Property("text-decoration") { Term = new PrimitiveTerm(UnitType.Ident, "none") });
			pageButton.Declarations.Properties.Add(new Property("text-align") { Term = new PrimitiveTerm(UnitType.Ident, "center") });
			pageButton.Declarations.Properties.Add(new Property("font-weight") { Term = new PrimitiveTerm(UnitType.Ident, "bold") });
			var shadowTerms = BuildTermList(TermList.TermSeparator.Space, new PrimitiveTerm(UnitType.Ident, "inset"), new PrimitiveTerm(UnitType.Pixel, 0),
				new PrimitiveTerm(UnitType.Pixel, 1), new PrimitiveTerm(UnitType.Pixel, 0), new PrimitiveTerm(UnitType.Pixel, 0), new PrimitiveTerm(UnitType.RGB, "#ffffff"));
			pageButton.Declarations.Properties.Add(new Property("box-shadow") { Term = shadowTerms });
			var textShadowTerms = BuildTermList(TermList.TermSeparator.Space, new PrimitiveTerm(UnitType.Pixel, 0), new PrimitiveTerm(UnitType.Pixel, 1),
				new PrimitiveTerm(UnitType.Pixel, 0), new PrimitiveTerm(UnitType.RGB, "#ffffff"));
			pageButton.Declarations.Properties.Add(new Property("text-shadow") { Term = textShadowTerms });
			var borderTerms = BuildTermList(TermList.TermSeparator.Space, new PrimitiveTerm(UnitType.Pixel, 1),
				new PrimitiveTerm(UnitType.Ident, "solid"), new PrimitiveTerm(UnitType.RGB, "#dcdcdc"));
			pageButton.Declarations.Properties.Add(new Property("border") { Term = borderTerms });
			pageButton.Declarations.Properties.Add(new Property("border-radius") { Term = new PrimitiveTerm(UnitType.Pixel, 6) });
			pageButton.Declarations.Properties.Add(new Property("background-color") { Term = new PrimitiveTerm(UnitType.RGB, "#ededed") });
			var currentButtonRule = new StyleRule { Value = "#currentPageButton" };
			currentButtonRule.Declarations.Properties.Add(new Property("background") { Term = new PrimitiveTerm(UnitType.Grad, "linear-gradient(to bottom, #dfdfdf 5%, #ededed 100%)") });
			currentButtonRule.Declarations.Properties.Add(new Property("background-color") { Term = new PrimitiveTerm(UnitType.RGB, "#cdcdcd") });
			return string.Format("{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}", Environment.NewLine, screen.ToString(true), print.ToString(true),
				pageButton.ToString(true), pageButtonHover.ToString(true), pageButtonActive.ToString(true), currentButtonRule.ToString(true));
		}

		/// <summary>
		/// Generates css that will apply to the current entry in our preview and highlight it for the user
		/// </summary>
		internal static string GenerateCssForSelectedEntry(bool isRtl)
		{
			// Draw a blue gradient behind the entry to highlight it
			var selectedEntryBefore = new StyleRule { Value = "." + DictionaryConfigurationServices.CurrentSelectedEntryClass + ":before" };
			var directionOfRule = !isRtl ? "right" : "left";
			selectedEntryBefore.Declarations.Properties.Add(new Property("background")
			{
				Term = new PrimitiveTerm(UnitType.Ident, "linear-gradient(to " + directionOfRule + ", rgb(100,200,245), rgb(200,238,252) 2em, rgb(200,238,252), transparent, transparent)")
			});
			selectedEntryBefore.Declarations.Properties.Add(new Property("background-position")
			{
				Term = new TermList(new PrimitiveTerm(UnitType.Pixel, 0), new PrimitiveTerm(UnitType.Pixel, 3))
			});
			selectedEntryBefore.Declarations.Properties.Add(new Property("content") { Term = new PrimitiveTerm(UnitType.Ident, "''") });
			selectedEntryBefore.Declarations.Properties.Add(new Property("position") { Term = new PrimitiveTerm(UnitType.Ident, "absolute") });
			selectedEntryBefore.Declarations.Properties.Add(new Property("z-index") { Term = new PrimitiveTerm(UnitType.Number, -10) });
			selectedEntryBefore.Declarations.Properties.Add(new Property("width") { Term = new PrimitiveTerm(UnitType.Percentage, 75) });
			var selectedEntry = new StyleRule { Value = "." + DictionaryConfigurationServices.CurrentSelectedEntryClass };
			selectedEntry.Declarations.Properties.Add(new Property("background")
			{
				Term = new PrimitiveTerm(UnitType.Ident, "linear-gradient(to bottom " + directionOfRule + ", transparent, rgb(200,238,252) 1em, rgb(200,238,252), rgb(200,238,252), transparent)")
			});
			var screenRule = new MediaRule { Condition = "screen", RuleSets = { selectedEntryBefore, selectedEntry } };
			return screenRule.ToString(true) + Environment.NewLine;
		}

		/// <summary>
		/// This method will build a css term list with all the provided terms separated by the provided separator
		/// </summary>
		private static TermList BuildTermList(TermList.TermSeparator separator, params Term[] terms)
		{
			var termList = new TermList();
			for (var i = 0; i < terms.Length; ++i)
			{
				if (i > 0)
				{
					termList.AddSeparator(separator);
				}
				termList.AddTerm(terms[i]);
			}
			return termList;
		}

		/// <summary>
		/// Method to copy the custom Css file from Project folder to the Temp folder for FieldWorks preview and export
		/// </summary>
		/// <param name="projectPath">path where the custom css file should be found</param>
		/// <param name="exportPath">destination folder for the copy</param>
		/// <param name="custCssFileName"></param>
		/// <returns>full path to the custom css file or empty string if no copy happened</returns>
		internal static string CopyCustomCssToTempFolder(string projectPath, string exportPath, string custCssFileName)
		{
			if (exportPath == null || projectPath == null)
			{
				return string.Empty;
			}
			var custCssProjectPath = Path.Combine(projectPath, custCssFileName);
			if (!File.Exists(custCssProjectPath))
			{
				return string.Empty;
			}
			var custCssTempPath = Path.Combine(exportPath, custCssFileName);
			File.Copy(custCssProjectPath, custCssTempPath, true);
			return custCssTempPath;
		}

		public static string CopyCustomCssAndGetPath(string destinationFolder, string configDir)
		{
			var custCssPath = string.Empty;
			var projType = string.IsNullOrEmpty(configDir) ? null : new DirectoryInfo(configDir).Name;
			if (!string.IsNullOrEmpty(projType))
			{
				var cssName = projType == "Dictionary" ? "ProjectDictionaryOverrides.css" : "ProjectReversalOverrides.css";
				custCssPath = CopyCustomCssToTempFolder(configDir, destinationFolder, cssName);
			}
			return custCssPath;
		}

		private sealed class AncestorIndents
		{
			internal AncestorIndents(float margin, float textIndent) : this(null, margin, textIndent)
			{
			}

			internal AncestorIndents(ConfigurableDictionaryNode ancestor, float margin, float textIndent)
			{
				Ancestor = ancestor;
				Margin = margin;
				TextIndent = textIndent;
			}

			internal float Margin { get; }
			internal float TextIndent { get; }
			internal ConfigurableDictionaryNode Ancestor { get; }
		}
	}
}
