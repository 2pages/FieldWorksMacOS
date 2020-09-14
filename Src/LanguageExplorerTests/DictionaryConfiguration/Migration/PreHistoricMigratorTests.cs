// Copyright (c) 2014-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LanguageExplorer;
using LanguageExplorer.Controls.XMLViews;
using LanguageExplorer.DictionaryConfiguration;
using LanguageExplorer.DictionaryConfiguration.Migration;
using LanguageExplorer.TestUtilities;
using NUnit.Framework;
using SIL.FieldWorks.Common.FwUtils;
using SIL.IO;
using SIL.LCModel;
using SIL.LCModel.Core.Cellar;
using SIL.LCModel.DomainServices;
using SIL.Linq;
using SIL.TestUtilities;

namespace LanguageExplorerTests.DictionaryConfiguration.Migration
{
	public class PreHistoricMigratorTests : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		private FlexComponentParameters _flexComponentParameters;
		private LcmStyleSheet _lcmStyleSheet;
		private PreHistoricMigrator _migrator;
		private IDisposable _cf1, _cf2, _cf3, _cf4;

		// Set up Custom Fields at the Fixture level, since disposing one in one test disposes them all in all tests
		private const string CustomFieldChangedLabel = "Custom Label";
		private const string CustomFieldOriginalName = "Custom Name";
		private const string CustomFieldUnchangedNameAndLabel = "Custom";
		private const string CustomFieldGenDate = "Custom GenDate";
		private const string CustomFieldLocation = "Custom Person";

		// Minor Entry Nodes
		private const string MinorEntryOldLabel = "Minor Entry";
		private const string MainEntryComplexLabel = "Main Entry (Complex Forms)";
		private const string MinorEntryComplexLabel = "Minor Entry (Complex Forms)";
		private const string MinorEntryVariantLabel = "Minor Entry (Variants)";
		private const string MinorEntryOldXpath = "//ConfigurationItem[@name='" + MinorEntryOldLabel + "']";
		private const string MinorEntryComplexXpath = "//ConfigurationItem[@name='" + MinorEntryComplexLabel + "']";
		private const string MinorEntryVariantXpath = "//ConfigurationItem[@name='" + MinorEntryVariantLabel + "']";

		#region Overrides of LcmTestBase

		public override void TestSetup()
		{
			base.TestSetup();

			_cf1 = new CustomFieldForTest(Cache, CustomFieldChangedLabel, CustomFieldOriginalName, Cache.MetaDataCacheAccessor.GetClassId("LexEntry"), CellarPropertyType.ReferenceCollection, Guid.Empty);
			_cf2 = new CustomFieldForTest(Cache, CustomFieldUnchangedNameAndLabel, Cache.MetaDataCacheAccessor.GetClassId("LexEntry"), -1, CellarPropertyType.ReferenceCollection, Guid.Empty);
			_cf3 = new CustomFieldForTest(Cache, CustomFieldGenDate, Cache.MetaDataCacheAccessor.GetClassId("LexEntry"), 0, CellarPropertyType.GenDate, Guid.Empty);
			_cf4 = new CustomFieldForTest(Cache, CustomFieldLocation, Cache.MetaDataCacheAccessor.GetClassId("LexEntry"), -1, CellarPropertyType.ReferenceAtomic, Cache.LanguageProject.LocationsOA.Guid);

			_flexComponentParameters = TestSetupServices.SetupEverything(Cache);
			_lcmStyleSheet = FwUtils.StyleSheetFromPropertyTable(_flexComponentParameters.PropertyTable);
			_migrator = new PreHistoricMigrator(string.Empty, Cache, null, _flexComponentParameters.PropertyTable);
		}

		public override void TestTearDown()
		{
			try
			{
				_cf1.Dispose();
				_cf2.Dispose();
				_cf3.Dispose();
				_cf4.Dispose();
				if (_migrator != null)
				{
					_migrator.SetTestLogger = null;
				}
				TestSetupServices.DisposeTrash(_flexComponentParameters);

				_cf1 = null;
				_cf2 = null;
				_cf3 = null;
				_cf4 = null;
				_migrator = null;
				_flexComponentParameters = null;
			}
			catch (Exception err)
			{
				throw new Exception($"Error in running {GetType().Name} TestTearDown method.", err);
			}
			finally
			{
				base.TestTearDown();
			}
		}

		#endregion

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_BeforeAfterAndBetweenWork()
		{
			ConfigurableDictionaryNode configNode = null;
			var oldNode = new LayoutTreeNode { After = "]", Between = ",", Before = "[" };
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(oldNode));
			Assert.AreEqual(configNode.After, oldNode.After, "After not migrated");
			Assert.AreEqual(configNode.Between, oldNode.Between, "Between not migrated");
			Assert.AreEqual(configNode.Before, oldNode.Before, "Before not migrated");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_SubsensesBeforeAfterAndBetweenWork()
		{
			var oldExampleSentenceNode = new ConfigurableDictionaryNode
			{
				Label = "Example Sentences",
				FieldDescription = "ExampleSentences",
				Before = "@",
				Between = ",",
				After = "@"
			};
			var oldExampleNode = new ConfigurableDictionaryNode
			{
				Label = "Examples",
				FieldDescription = "ExamplesOS",
				Children = new List<ConfigurableDictionaryNode> { oldExampleSentenceNode }
			};
			var oldSensesNode = new ConfigurableDictionaryNode
			{
				Label = "Senses",
				FieldDescription = "SensesOS",
				Children = new List<ConfigurableDictionaryNode> { oldExampleNode }
			};
			var oldSubsensesNode = new ConfigurableDictionaryNode
			{
				Label = "Subsenses",
				FieldDescription = "SensesOS",
				Parent = oldSensesNode
			};
			var exampleSentenceNode = new ConfigurableDictionaryNode
			{
				Label = "Example Sentences",
				FieldDescription = "ExampleSentences"
			};
			var exampleNode = new ConfigurableDictionaryNode
			{
				Label = "Examples",
				FieldDescription = "ExamplesOS",
				Children = new List<ConfigurableDictionaryNode> { exampleSentenceNode }
			};
			var newSubsensesNode = new ConfigurableDictionaryNode
			{
				Label = "Subsenses",
				FieldDescription = "SensesOS",
				Children = new List<ConfigurableDictionaryNode> { exampleNode }
			};
			var sensesNode = new ConfigurableDictionaryNode
			{
				Label = "Senses",
				FieldDescription = "SensesOS",
				Children = new List<ConfigurableDictionaryNode> { newSubsensesNode }
			};
			var mainEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Main Entry",
				FieldDescription = "LexEntry",
				Children = new List<ConfigurableDictionaryNode> { sensesNode }
			};
			var model = new DictionaryConfigurationModel
			{
				Version = PreHistoricMigrator.VersionPre83,
				Parts = new List<ConfigurableDictionaryNode> { mainEntryNode }
			};

			_migrator.CopyDefaultsIntoConfigNode(model, oldSubsensesNode, newSubsensesNode);
			Assert.AreEqual(oldSubsensesNode.Children[0].Children[0].Between, ",", "Between not migrated");
			Assert.AreEqual(oldSubsensesNode.Children[0].Children[0].Before, "@", "Before not migrated");
			Assert.AreEqual(oldSubsensesNode.Children[0].Children[0].After, "@", "After not migrated");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_SubsensesGetsConvertedSenseChildren()
		{
			var oldExampleSentenceNode = new ConfigurableDictionaryNode
			{
				Label = "Example Sentences"
			};
			var oldAfterSubSensesNode = new ConfigurableDictionaryNode
			{
				Label = "After Subsenses"
			};
			var oldExampleNode = new ConfigurableDictionaryNode
			{
				Label = "Examples",
				Children = new List<ConfigurableDictionaryNode> { oldExampleSentenceNode }
			};
			var oldSubsensesNode = new ConfigurableDictionaryNode
			{
				Label = "Subsenses"
			};
			var oldSensesNode = new ConfigurableDictionaryNode
			{
				Label = "Senses",
				Children = new List<ConfigurableDictionaryNode> { oldExampleNode, oldSubsensesNode, oldAfterSubSensesNode }
			};
			oldAfterSubSensesNode.Parent = oldSensesNode;
			oldExampleNode.Parent = oldSensesNode;
			oldSubsensesNode.Parent = oldSensesNode;
			var oldMainEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Main Entry",
				FieldDescription = "LexEntry",
				Children = new List<ConfigurableDictionaryNode> { oldSensesNode }
			};
			oldSensesNode.Parent = oldMainEntryNode;

			var exampleSentenceNode = new ConfigurableDictionaryNode
			{
				Label = "Example Sentences",
				FieldDescription = "ExampleSentences"
			};

			var exampleNode = new ConfigurableDictionaryNode
			{
				Label = "Examples",
				FieldDescription = "ExamplesOS",
				Children = new List<ConfigurableDictionaryNode> { exampleSentenceNode }
			};
			var newAfterSubSensesNode = new ConfigurableDictionaryNode
			{
				Label = "After Subsenses",
				FieldDescription = "PostSubsenses"
			};
			var newSubsensesNode = new ConfigurableDictionaryNode
			{
				Label = "Subsenses",
				FieldDescription = "SensesOS",
				Children = new List<ConfigurableDictionaryNode> { exampleNode }
			};
			newSubsensesNode.Children.Add(newAfterSubSensesNode.DeepCloneUnderParent(newSubsensesNode));
			var sensesNode = new ConfigurableDictionaryNode
			{
				Label = "Senses",
				FieldDescription = "SensesOS",
				Children = new List<ConfigurableDictionaryNode> { exampleNode, newSubsensesNode, newAfterSubSensesNode }
			};
			newSubsensesNode.Parent = sensesNode;
			var mainEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Main Entry",
				FieldDescription = "LexEntry",
				Children = new List<ConfigurableDictionaryNode> { sensesNode }
			};
			var model = new DictionaryConfigurationModel { Version = PreHistoricMigrator.VersionPre83, Parts = new List<ConfigurableDictionaryNode> { oldMainEntryNode } };
			CssGeneratorTests.PopulateFieldsForTesting(mainEntryNode);

			using (_migrator.SetTestLogger = new SimpleLogger())
			{
				_migrator.CopyDefaultsIntoConfigNode(model, oldSubsensesNode, newSubsensesNode);
			}
			Assert.AreEqual(oldSubsensesNode.Children[0].Children[0].FieldDescription, "ExampleSentences", "Defaults not copied in for fields before Subsenses");
			Assert.AreEqual(oldSubsensesNode.Children[2].FieldDescription, "PostSubsenses", "Defaults not copied into fields following Subsenses");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_StyleWorks()
		{
			ConfigurableDictionaryNode configNode = null;
			var oldNode = new LayoutTreeNode { StyleName = "Dictionary-Headword" };
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(oldNode));
			Assert.AreEqual(configNode.Style, oldNode.StyleName, "Style not migrated");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_MainEntryAndMinorEntryWork()
		{
			var oldMainNode = new LayoutTreeNode { Label = "Main Entry", ClassName = "LexEntry" };
			var oldMinorNode = new LayoutTreeNode { Label = MinorEntryOldLabel, ClassName = "LexEntry" };
			ConfigurableDictionaryNode configNode = null;
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(oldMainNode));
			Assert.AreEqual(configNode.Label, oldMainNode.Label, "Label Main Entry root node was not migrated");
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(oldMinorNode));
			Assert.AreEqual(configNode.Label, oldMinorNode.Label, "Label for Minor Entry root node was not migrated");
		}

		[Test]
		public void CopyDefaultsIntoConfigNode_MinorEntryWithoutBeforeAfterWorks()
		{
			using (var convertedModelFile = new TempFile())
			{
				var convertedMinorEntryNodesType = BuildConvertedMinorEntryNodes();
				convertedMinorEntryNodesType.FilePath = convertedModelFile.Path;
				var defaultMinorEntryNodesType = BuildCurrentDefaultMinorEntryNodes();

				_migrator.CopyNewDefaultsIntoConvertedModel(convertedMinorEntryNodesType, defaultMinorEntryNodesType);
				convertedMinorEntryNodesType.Save();
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(MinorEntryComplexXpath, 1);
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(MinorEntryVariantXpath, 1);
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(MinorEntryComplexXpath + "/@before");
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(MinorEntryComplexXpath + "/@after");
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(MinorEntryVariantXpath + "/@before");
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(MinorEntryVariantXpath + "/@after");
			}
		}

		/// <summary>
		/// In Lexeme-Based dictionaries, Complex Forms are displayed as Main Entries. Ensure that the converted configuration for
		/// Main Entry is also used for the new Main Entry (Complex Forms) node.
		/// </summary>
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_TreatsComplexAsMainForStem()
		{
			const string beforeMainHeadword = "Main Headword: ";
			var convertedModel = BuildConvertedMinorEntryNodes();
			var convertedMainNode = new ConfigurableDictionaryNode
			{
				Label = "Main Entry",
				Children = new List<ConfigurableDictionaryNode>
				{
					new ConfigurableDictionaryNode
					{
						Label = "Headword",
						Before = beforeMainHeadword
					}
				}
			};
			convertedModel.Parts[0] = convertedMainNode;
			CssGeneratorTests.PopulateFieldsForTesting(convertedModel);

			var currentDefaultModel = BuildCurrentDefaultMinorEntryNodes();
			currentDefaultModel.FilePath = "./" + DictionaryConfigurationServices.LexemeFileName + LanguageExplorerConstants.DictionaryConfigurationFileExtension;
			currentDefaultModel.Parts[1].Label = MainEntryComplexLabel;
			var currentDefaultMainNode = new ConfigurableDictionaryNode
			{
				Label = "Main Entry",
				FieldDescription = "LexEntry",
				Children = new List<ConfigurableDictionaryNode>
				{
					new ConfigurableDictionaryNode
					{
						Label = "Headword",
						FieldDescription = "MLHeadWord"
					}
				}
			};
			currentDefaultModel.Parts[0] = currentDefaultMainNode;
			CssGeneratorTests.PopulateFieldsForTesting(currentDefaultModel);

			_migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, currentDefaultModel);
			Assert.IsFalse(convertedModel.IsRootBased, "Lexeme-based should not be Root-based!");
			Assert.AreEqual(3, convertedModel.Parts.Count, "Number of top-level nodes");
			convertedMainNode = convertedModel.Parts[0];
			Assert.AreEqual("Main Entry", convertedMainNode.Label);
			Assert.AreEqual("LexEntry", convertedMainNode.FieldDescription, "Main Field");
			Assert.AreEqual(beforeMainHeadword, convertedMainNode.Children[0].Before, "Before Main Headword");
			convertedMainNode = convertedModel.Parts[1];
			Assert.AreEqual(MainEntryComplexLabel, convertedMainNode.Label);
			Assert.AreEqual("LexEntry", convertedMainNode.FieldDescription, "Main (Complex) Field");
			Assert.AreEqual(currentDefaultModel.Parts[1].Style, convertedMainNode.Style);
			Assert.AreEqual(beforeMainHeadword, convertedMainNode.Children[0].Before, "Before Main (Complex) Headword");
			var convertedVariantNode = convertedModel.Parts[2];
			Assert.AreEqual(MinorEntryVariantLabel, convertedVariantNode.Label);
			Assert.AreEqual("LexEntry", convertedVariantNode.FieldDescription, "Minor (Variant) Field");
		}

		[Test]
		public void CopyNewDefaultsIntoConvertedModel_SplitsMinorEntryNodes([Values(true, false)] bool isOriginal, [Values(true, false)] bool isComplex, [Values(true, false)] bool isVariant)
		{
			using (var convertedModelFile = new TempFile())
			{
				var convertedMinorEntryModel = BuildConvertedMinorEntryNodes();
				convertedMinorEntryModel.FilePath = convertedModelFile.Path;
				var meNode = convertedMinorEntryModel.Parts[1];
				meNode.IsDuplicate = !isOriginal;
				meNode.DictionaryNodeOptions = new DictionaryNodeListOptions
				{
					ListId = ListIds.Minor,
					Options = new List<DictionaryNodeOption>
					{
						new DictionaryNodeOption
						{
							Id = XmlViewsUtils.GetGuidForUnspecifiedComplexFormType().ToString(), IsEnabled = isComplex
						},
						new DictionaryNodeOption
						{
							Id = XmlViewsUtils.GetGuidForUnspecifiedVariantType().ToString(), IsEnabled = isVariant
						}
					}
				};

				// SUT
				_migrator.CopyNewDefaultsIntoConvertedModel(convertedMinorEntryModel, BuildCurrentDefaultMinorEntryNodes());
				convertedMinorEntryModel.Save();

				var hasComplexNode = isOriginal || isComplex || !isVariant;
				var hasVariantNode = isOriginal || isVariant || !isComplex;
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(MinorEntryComplexXpath, hasComplexNode ? 1 : 0);
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(MinorEntryVariantXpath, hasVariantNode ? 1 : 0);
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(MinorEntryOldXpath, message: "All old Minor Entry nodes should have been split");
			}
		}

		[Test]
		public void CopyNewDefaultsIntoConvertedModel_UpdatesVersionNumberToAlpha1()
		{
			var convertedModel = new DictionaryConfigurationModel { Parts = new List<ConfigurableDictionaryNode> { new ConfigurableDictionaryNode() } };
			var currentDefaultModel = new DictionaryConfigurationModel
			{
				Version = DictionaryConfigurationServices.VersionCurrent,
				Parts = new List<ConfigurableDictionaryNode> { new ConfigurableDictionaryNode() }
			};
			// SUT
			_migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, currentDefaultModel);
			Assert.AreEqual(PreHistoricMigrator.VersionAlpha1, convertedModel.Version);
		}

		[Test]
		public void CopyDefaultsIntoMinorEntryNode_UpdatesLabelAndListId()
		{
			var convertedModel = BuildConvertedMinorEntryNodes();
			var convertedMinorEntryNode = convertedModel.Parts[1];
			_migrator.CopyDefaultsIntoMinorEntryNode(convertedModel, convertedMinorEntryNode, BuildCurrentDefaultMinorEntryNodes().Parts[1], ListIds.Complex);
			Assert.AreEqual(MinorEntryComplexLabel, convertedMinorEntryNode.Label);
			Assert.AreEqual(ListIds.Complex, ((DictionaryNodeListOptions)convertedMinorEntryNode.DictionaryNodeOptions).ListId);
		}

		[Test]
		public void CopyDefaultsIntoMinorEntryNode_PreservesOnlyRelevantTypes()
		{
			var convertedModel = BuildConvertedMinorEntryNodes();
			var convertedMinorEntryNode = convertedModel.Parts[1];
			convertedMinorEntryNode.DictionaryNodeOptions = ConfiguredXHTMLGeneratorTests.GetFullyEnabledListOptions(Cache, ListIds.Minor);
			_migrator.CopyDefaultsIntoMinorEntryNode(convertedModel, convertedMinorEntryNode, BuildCurrentDefaultMinorEntryNodes().Parts[1], ListIds.Complex);
			var options = ((DictionaryNodeListOptions)convertedMinorEntryNode.DictionaryNodeOptions).Options;
			var complexTypeGuids = _migrator.AvailableComplexFormTypes;
			Assert.AreEqual(complexTypeGuids.Count(), options.Count, "All Complex Form Types should be present");
			foreach (var option in options)
			{
				Assert.That(option.IsEnabled);
				Assert.That(complexTypeGuids, Contains.Item(option.Id), "Only Complex Form Types should be present");
			}
		}

		[Test]
		public void CopyDefaultsIntoMinorEntryNode_PreservesSelections()
		{
			var convertedModel = BuildConvertedMinorEntryNodes();
			var convertedMinorEntryNode = convertedModel.Parts[1];
			convertedMinorEntryNode.DictionaryNodeOptions = ConfiguredXHTMLGeneratorTests.GetFullyEnabledListOptions(Cache, ListIds.Minor);
			var options = ((DictionaryNodeListOptions)convertedMinorEntryNode.DictionaryNodeOptions).Options;
			// Disable some options
			for (var i = 0; i < options.Count; i += 2)
			{
				options[i].IsEnabled = false;
			}
			// Deep clone Complex Types; we'll be testing those
			var expectedOptions = options.Where(option => _migrator.AvailableComplexFormTypes.Contains(option.Id)).Select(option => new DictionaryNodeOption { Id = option.Id, IsEnabled = option.IsEnabled }).ToList();

			// SUT
			_migrator.CopyDefaultsIntoMinorEntryNode(convertedModel, convertedMinorEntryNode, BuildCurrentDefaultMinorEntryNodes().Parts[1], ListIds.Complex);
			var resultOptions = ((DictionaryNodeListOptions)convertedMinorEntryNode.DictionaryNodeOptions).Options;

			Assert.AreEqual(expectedOptions.Count, resultOptions.Count);
			var j = 0;
			foreach (var option in expectedOptions)
			{
				Assert.AreEqual(option.Id, resultOptions[j].Id);
				Assert.AreEqual(option.IsEnabled, resultOptions[j++].IsEnabled);
			}
		}

		private static ConfigurableDictionaryNode EmptyNode => new ConfigurableDictionaryNode { Label = string.Empty };

		private static DictionaryConfigurationModel BuildConvertedMinorEntryNodes(bool isRootBased = false)
		{
			var minorEntryNode = new ConfigurableDictionaryNode
			{
				Label = MinorEntryOldLabel,
				FieldDescription = "LexEntry",
				IsEnabled = true,
				After = "(",
				Before = ")",
				DictionaryNodeOptions = new DictionaryNodeListOptions()
			};
			return new DictionaryConfigurationModel(isRootBased)
			{
				Parts = new List<ConfigurableDictionaryNode> { EmptyNode, minorEntryNode },
				Version = PreHistoricMigrator.VersionPre83
			};
		}

		private static DictionaryConfigurationModel BuildCurrentDefaultMinorEntryNodes(bool isRootBased = false)
		{
			var complexEntryNode = new ConfigurableDictionaryNode
			{
				Label = MinorEntryComplexLabel,
				FieldDescription = "LexEntry",
				Style = "Dictionary-Minor",
				IsEnabled = true,
				Children = new List<ConfigurableDictionaryNode>
				{
					new ConfigurableDictionaryNode
					{
						Label = "Headword",
						FieldDescription = "MLHeadWord"
					}
				}
			};
			var variantEntryNode = new ConfigurableDictionaryNode
			{
				Label = MinorEntryVariantLabel,
				FieldDescription = "LexEntry",
				IsEnabled = true,
			};
			return new DictionaryConfigurationModel(isRootBased)
			{
				Parts = new List<ConfigurableDictionaryNode> { EmptyNode, complexEntryNode, variantEntryNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};
		}

		[Test]
		public void HasComplexFormTypesSelected_And_HasVariantTypesSelected([Values(true, false)] bool isUnspecifiedComplexSelected,
			[Values(true, false)] bool isSpecifiedComplexSelected, [Values(true, false)] bool isUnspecifiedVariantSelected, [Values(true, false)] bool isSpecifiedVariantSelected)
		{
			var options = new List<DictionaryNodeOption>
			{
				new DictionaryNodeOption
				{
					Id = XmlViewsUtils.GetGuidForUnspecifiedComplexFormType().ToString(), IsEnabled = isUnspecifiedComplexSelected
				},
				new DictionaryNodeOption
				{
					Id = Cache.LangProject.LexDbOA.ComplexEntryTypesOA.PossibilitiesOS.Last().Guid.ToString(), IsEnabled = isSpecifiedComplexSelected
				},
				new DictionaryNodeOption
				{
					Id = XmlViewsUtils.GetGuidForUnspecifiedVariantType().ToString(), IsEnabled = isUnspecifiedVariantSelected
				},
				new DictionaryNodeOption
				{
					Id = Cache.LangProject.LexDbOA.VariantEntryTypesOA.PossibilitiesOS.Last().Guid.ToString(), IsEnabled = isSpecifiedVariantSelected
				}
			};

			Assert.AreEqual(isUnspecifiedComplexSelected || isSpecifiedComplexSelected, _migrator.HasComplexFormTypesSelected(options), "Complex");
			Assert.AreEqual(isUnspecifiedVariantSelected || isSpecifiedVariantSelected, _migrator.HasVariantTypesSelected(options), "Variant");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_IsEnabledWorks()
		{
			var tickedNode = new LayoutTreeNode { Checked = true };
			var untickedNode = new LayoutTreeNode { Checked = false };
			ConfigurableDictionaryNode configNode = null;
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(tickedNode));
			Assert.AreEqual(configNode.IsEnabled, tickedNode.Checked, "Checked node in old tree did not set IsEnabled correctly after migration");
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(untickedNode));
			Assert.AreEqual(configNode.IsEnabled, untickedNode.Checked, "Unchecked node in old tree did not set IsEnabled correctly after migration");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_WritingSystemOptionsAnalysisTypeWorks()
		{
			var nodeWithWs = new LayoutTreeNode { WsType = "analysis", WsLabel = "analysis" };
			ConfigurableDictionaryNode configNode = null;
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(nodeWithWs));
			Assert.NotNull(configNode.DictionaryNodeOptions, "No DictionaryNodeOptions were created for a treenode with a writing system");
			Assert.IsTrue(configNode.DictionaryNodeOptions is DictionaryNodeWritingSystemOptions, "Writing system options node not created");
			var wsOpts = (DictionaryNodeWritingSystemOptions)configNode.DictionaryNodeOptions;
			Assert.AreEqual(wsOpts.WsType, WritingSystemType.Analysis);
			Assert.IsNotNull(wsOpts.Options, "analysis choice did not result in any options being created.");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_WritingSystemOptionsVernacularTypeWorks()
		{
			var nodeWithWs = new LayoutTreeNode { WsType = "vernacular", WsLabel = "vernacular" };
			ConfigurableDictionaryNode configNode = null;
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(nodeWithWs));
			Assert.NotNull(configNode.DictionaryNodeOptions, "No DictionaryNodeOptions were created for a treenode with a writing system");
			Assert.IsTrue(configNode.DictionaryNodeOptions is DictionaryNodeWritingSystemOptions, "Writing system options node not created");
			var wsOpts = (DictionaryNodeWritingSystemOptions)configNode.DictionaryNodeOptions;
			Assert.AreEqual(wsOpts.WsType, WritingSystemType.Vernacular);
			Assert.IsNotNull(wsOpts.Options, "vernacular choice did not result in any options being created.");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_WritingSystemOptionsVernacularAnalysisTypeWorks()
		{
			var nodeWithWs = new LayoutTreeNode { WsType = "vernacular analysis", WsLabel = "vernacular" };
			ConfigurableDictionaryNode configNode = null;
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(nodeWithWs));
			Assert.NotNull(configNode.DictionaryNodeOptions, "No DictionaryNodeOptions were created for a treenode with a writing system");
			Assert.IsTrue(configNode.DictionaryNodeOptions is DictionaryNodeWritingSystemOptions, "Writing system options node not created");
			var wsOpts = (DictionaryNodeWritingSystemOptions)configNode.DictionaryNodeOptions;
			Assert.AreEqual(wsOpts.WsType, WritingSystemType.Both);
			Assert.IsNotNull(wsOpts.Options, "vernacular analysis choice did not result in any options being created.");
			Assert.IsNotNull(wsOpts.Options.Find(option => option.IsEnabled && option.Id == "vernacular"), "vernacular choice was not migrated.");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_WritingSystemOptionsPronunciationTypeWorks()
		{
			var nodeWithWs = new LayoutTreeNode { WsType = "pronunciation", WsLabel = "pronunciation" };
			ConfigurableDictionaryNode configNode = null;
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(nodeWithWs));
			Assert.NotNull(configNode.DictionaryNodeOptions, "No DictionaryNodeOptions were created for a treenode with a writing system");
			Assert.IsTrue(configNode.DictionaryNodeOptions is DictionaryNodeWritingSystemOptions, "Writing system options node not created");
			var wsOpts = (DictionaryNodeWritingSystemOptions)configNode.DictionaryNodeOptions;
			Assert.AreEqual(wsOpts.WsType, WritingSystemType.Pronunciation);
			Assert.IsNotNull(wsOpts.Options, "pronunciation choice did not result in any options being created.");
			Assert.IsNotNull(wsOpts.Options.Find(option => option.IsEnabled && option.Id == "pronunciation"), "pronunciation choice was not migrated.");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_WritingSystemOptionsAnalysisVernacularTypeWorks()
		{
			var nodeWithWs = new LayoutTreeNode { WsType = "analysis vernacular", WsLabel = "analysis" };
			ConfigurableDictionaryNode configNode = null;
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(nodeWithWs));
			Assert.NotNull(configNode.DictionaryNodeOptions, "No DictionaryNodeOptions were created for a treenode with a writing system");
			Assert.IsTrue(configNode.DictionaryNodeOptions is DictionaryNodeWritingSystemOptions, "Writing system options node not created");
			var wsOpts = (DictionaryNodeWritingSystemOptions)configNode.DictionaryNodeOptions;
			Assert.AreEqual(wsOpts.WsType, WritingSystemType.Both);
			Assert.IsNotNull(wsOpts.Options, "analysis vernacular choice did not result in any options being created.");
			Assert.IsNotNull(wsOpts.Options.Find(option => option.IsEnabled && option.Id == "analysis"), "analysis choice was not migrated.");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_WritingSystemOptionsVernacularSingleLanguageWorks()
		{
			var nodeWithWs = new LayoutTreeNode { WsType = "vernacular", WsLabel = "fr" };
			ConfigurableDictionaryNode configNode = null;
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(nodeWithWs));
			Assert.NotNull(configNode.DictionaryNodeOptions, "No DictionaryNodeOptions were created for a treenode with a writing system");
			Assert.IsTrue(configNode.DictionaryNodeOptions is DictionaryNodeWritingSystemOptions, "Writing system options node not created");
			var wsOpts = (DictionaryNodeWritingSystemOptions)configNode.DictionaryNodeOptions;
			Assert.AreEqual(wsOpts.WsType, WritingSystemType.Vernacular);
			Assert.IsNotNull(wsOpts.Options, "French choice did not result in any options being created.");
			Assert.IsNotNull(wsOpts.Options.Find(option => option.IsEnabled && option.Id == "fr"), "French choice was not migrated.");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_WritingSystemOptionsTwoLanguagesWork()
		{
			var nodeWithWs = new LayoutTreeNode { WsType = "vernacular", WsLabel = "fr, hi" };
			ConfigurableDictionaryNode configNode = null;
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(nodeWithWs));
			Assert.NotNull(configNode.DictionaryNodeOptions, "No DictionaryNodeOptions were created for a treenode with a writing system");
			Assert.IsTrue(configNode.DictionaryNodeOptions is DictionaryNodeWritingSystemOptions, "Writing system options node not created");
			var wsOpts = (DictionaryNodeWritingSystemOptions)configNode.DictionaryNodeOptions;
			Assert.AreEqual(wsOpts.WsType, WritingSystemType.Vernacular);
			Assert.IsNotNull(wsOpts.Options, "two languages did not result in ws options being created");
			Assert.IsNotNull(wsOpts.Options.Find(option => option.IsEnabled && option.Id == "fr"), "French choice was not migrated.");
			Assert.IsNotNull(wsOpts.Options.Find(option => option.IsEnabled && option.Id == "hi"), "hi choice was not migrated.");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_WritingSystemOptionsWsAbbreviationWorks()
		{
			var nodeWithWs = new LayoutTreeNode { WsType = "vernacular", WsLabel = "fr", ShowWsLabels = true };
			ConfigurableDictionaryNode configNode = null;

			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(nodeWithWs));
			Assert.NotNull(configNode.DictionaryNodeOptions, "No DictionaryNodeOptions were created for a treenode with a writing system");
			Assert.IsTrue(configNode.DictionaryNodeOptions is DictionaryNodeWritingSystemOptions, "Writing system options node not created");
			var wsOpts = (DictionaryNodeWritingSystemOptions)configNode.DictionaryNodeOptions;
			Assert.IsTrue(wsOpts.DisplayWritingSystemAbbreviations, "ShowWsLabels true value did not convert into DisplayWritingSystemAbbreviation");
			nodeWithWs.ShowWsLabels = false;

			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(nodeWithWs));
			wsOpts = (DictionaryNodeWritingSystemOptions)configNode.DictionaryNodeOptions;
			Assert.IsFalse(wsOpts.DisplayWritingSystemAbbreviations, "ShowWsLabels false value did not convert into DisplayWritingSystemAbbreviation");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_ListOptionsEnabledLexRelationWorks()
		{
			const string enabledGuid = "+a0000000-1000-b000-2000-c00000000000";
			var nodeWithSequence = new LayoutTreeNode { LexRelType = "entry", RelTypeList = LexReferenceInfo.CreateListFromStorageString(enabledGuid) };
			ConfigurableDictionaryNode configNode = null;

			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(nodeWithSequence));
			Assert.NotNull(configNode.DictionaryNodeOptions, "No DictionaryNodeOptions were created for a treenode with a LexReferenceInfo");
			Assert.IsTrue(configNode.DictionaryNodeOptions is DictionaryNodeListOptions, "List system options node not created");
			var lexRelationOptions = (DictionaryNodeListOptions)configNode.DictionaryNodeOptions;
			Assert.AreEqual(lexRelationOptions.Options.Count, 1);
			Assert.AreEqual(lexRelationOptions.Options[0].Id, enabledGuid.Substring(1));
			Assert.IsTrue(lexRelationOptions.Options[0].IsEnabled);
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_ListOptionsDisabledLexRelationWorks()
		{
			const string disabledGuid = "-a0000000-1000-b000-2000-c00000000000";
			var nodeWithSequence = new LayoutTreeNode { LexRelType = "entry", RelTypeList = LexReferenceInfo.CreateListFromStorageString(disabledGuid) };
			ConfigurableDictionaryNode configNode = null;

			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(nodeWithSequence));
			Assert.NotNull(configNode.DictionaryNodeOptions, "No DictionaryNodeOptions were created for a treenode with a LexReferenceInfo");
			Assert.IsTrue(configNode.DictionaryNodeOptions is DictionaryNodeListOptions, "List system options node not created");
			var lexRelationOptions = (DictionaryNodeListOptions)configNode.DictionaryNodeOptions;
			Assert.AreEqual(lexRelationOptions.Options.Count, 1);
			Assert.AreEqual(lexRelationOptions.Options[0].Id, disabledGuid.Substring(1));
			Assert.IsFalse(lexRelationOptions.Options[0].IsEnabled);
		}

		///<summary>Test that a list with two guids migrates both items and keeps their order</summary>
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_ListOptionsMultipleItemsWorks()
		{
			const string enabledGuid = "b0000000-2000-b000-2000-c00000000000";
			const string disabledGuid = "a0000000-1000-b000-2000-c00000000000";
			var guidList = $"+{enabledGuid},-{disabledGuid}";
			var nodeWithSequence = new LayoutTreeNode { LexRelType = "entry", RelTypeList = LexReferenceInfo.CreateListFromStorageString(guidList) };
			ConfigurableDictionaryNode configNode = null;

			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(nodeWithSequence));
			Assert.NotNull(configNode.DictionaryNodeOptions, "No DictionaryNodeOptions were created for a treenode with a LexReferenceInfo");
			Assert.IsTrue(configNode.DictionaryNodeOptions is DictionaryNodeListOptions, "List system options node not created");
			var lexRelationOptions = (DictionaryNodeListOptions)configNode.DictionaryNodeOptions;
			Assert.AreEqual(lexRelationOptions.Options.Count, 2);
			Assert.AreEqual(lexRelationOptions.Options[0].Id, enabledGuid);
			Assert.IsTrue(lexRelationOptions.Options[0].IsEnabled);
			Assert.AreEqual(lexRelationOptions.Options[1].Id, disabledGuid);
			Assert.IsFalse(lexRelationOptions.Options[1].IsEnabled);
		}

		///<summary>Subentries node should have "Display .. in a Paragraph" checked (LT-15834).</summary>
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_DisplaySubentriesInParagraph()
		{
			const string disabledGuid = "-a0000000-1000-b000-2000-c00000000000";
			var node = new MockLayoutTreeNode
			{
				m_partName = "LexEntry-Jt-RootSubentriesConfig",
				EntryType = "complex",
				EntryTypeList = ItemTypeInfo.CreateListFromStorageString(disabledGuid)
			};

			var configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(node);
			Assert.NotNull(configNode.DictionaryNodeOptions, "No DictionaryNodeOptions were created");

			Assert.IsTrue(configNode.DictionaryNodeOptions is DictionaryNodeListAndParaOptions, "wrong type");
			var options = (DictionaryNodeListAndParaOptions)configNode.DictionaryNodeOptions;
			Assert.IsTrue(options.DisplayEachInAParagraph, "Did not set");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_ListOptionsEnabledLexEntryTypeWorks()
		{
			const string enabledGuid = "+a0000000-1000-b000-2000-c00000000000";
			var nodeWithSequence = new LayoutTreeNode { EntryType = "sense", EntryTypeList = ItemTypeInfo.CreateListFromStorageString(enabledGuid) };
			ConfigurableDictionaryNode configNode = null;

			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(nodeWithSequence));
			Assert.NotNull(configNode.DictionaryNodeOptions, "No DictionaryNodeOptions were created for the treenode");
			Assert.IsTrue(configNode.DictionaryNodeOptions is DictionaryNodeListOptions, "List system options node not created");
			var lexRelationOptions = (DictionaryNodeListOptions)configNode.DictionaryNodeOptions;
			Assert.AreEqual(lexRelationOptions.Options.Count, 1);
			Assert.AreEqual(lexRelationOptions.Options[0].Id, enabledGuid.Substring(1));
			Assert.IsTrue(lexRelationOptions.Options[0].IsEnabled);
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_ListOptionsDisabledLexEntryTypeWorks()
		{
			const string disabledGuid = "-a0000000-1000-b000-2000-c00000000000";
			var nodeWithSequence = new LayoutTreeNode { EntryType = "variant", EntryTypeList = ItemTypeInfo.CreateListFromStorageString(disabledGuid) };
			ConfigurableDictionaryNode configNode = null;

			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(nodeWithSequence));
			Assert.NotNull(configNode.DictionaryNodeOptions, "No DictionaryNodeOptions were created for the treenode");
			Assert.IsTrue(configNode.DictionaryNodeOptions is DictionaryNodeListOptions, "List system options node not created");
			var lexRelationOptions = (DictionaryNodeListOptions)configNode.DictionaryNodeOptions;
			Assert.AreEqual(lexRelationOptions.Options.Count, 1);
			Assert.AreEqual(lexRelationOptions.Options[0].Id, disabledGuid.Substring(1));
			Assert.IsFalse(lexRelationOptions.Options[0].IsEnabled);
		}

		/// <summary>
		/// A XmlDocConfigureDlg.LayoutTreeNode.Label includes the suffix. A ConfigurableDictionaryNode.Label does not.
		/// </summary>
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_DupStringInfoIsConverted()
		{
			var duplicateNode = new LayoutTreeNode { DupString = "1", IsDuplicate = true, Label = "A b c (1)" };
			ConfigurableDictionaryNode configNode = null;
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(duplicateNode));
			Assert.IsTrue(configNode.IsDuplicate, "Duplicate node not marked as duplicate.");
			Assert.AreEqual(duplicateNode.DupString, configNode.LabelSuffix, "number appended to old duplicates not migrated to label suffix");
			Assert.That(configNode.Label, Is.EqualTo("A b c"), "should not have a suffix on ConfigurableDictionaryNode.Label");

			var originalNode = new LayoutTreeNode { IsDuplicate = false };
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(originalNode));
			Assert.IsFalse(configNode.IsDuplicate, "node should not have been marked as a duplicate");
			Assert.IsTrue(string.IsNullOrEmpty(configNode.LabelSuffix), "suffix should be empty.");
		}

		/// <summary>
		/// A XmlDocConfigureDlg.LayoutTreeNode that is a duplicate of a duplicate will have a DupString of the form "1-2" but
		/// a Label of the form "Foo (2)", where "1" was the DupString of the first duplicate.
		/// </summary>
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_DupStringInfoIsConvertedForDuplicateOfDuplicate()
		{
			var duplicateNode = new LayoutTreeNode { DupString = "1-2", IsDuplicate = true, Label = "A b c (2)" };
			ConfigurableDictionaryNode configNode = null;
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(duplicateNode));
			Assert.IsTrue(configNode.IsDuplicate, "Duplicate node not marked as duplicate.");
			Assert.AreEqual("2", configNode.LabelSuffix, "incorrect suffix migrated");
			Assert.That(configNode.Label, Is.EqualTo("A b c"), "should not have a suffix on ConfigurableDictionaryNode.Label");
		}

		/// <summary>
		/// In some cases (Minor Entry, Subsubentry), a descendent of a duplicate will be flagged as a duplicate and given a DupString. Fortunately,
		/// these differ in that the DupString is not included in the Label. In this case, misleading Duplicate info should be discarded.
		/// If a node has both false and true duplicate information, the true information comes last, in the form "1.0-1"; this is handled correctly
		/// and is tested by <see cref="ConvertLayoutTreeNodeToConfigNode_DupStringInfoIsConvertedForDuplicateOfDuplicate"/>.
		/// </summary>
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_DupStringInfoIsDiscardedForFalseDuplicate()
		{
			var duplicateNode = new LayoutTreeNode { DupString = "1.0", IsDuplicate = true, Label = "A b c D e f" };
			ConfigurableDictionaryNode configNode;
			// SUT
			using (_migrator.SetTestLogger = new SimpleLogger())
			{
				configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(duplicateNode);
			}
			Assert.IsFalse(configNode.IsDuplicate, "Node incorrectly marked as a duplicate.");
			Assert.IsNullOrEmpty(configNode.LabelSuffix, "suffix incorrectly migrated");
			Assert.AreEqual("A b c D e f", configNode.Label, "should not have a suffix on ConfigurableDictionaryNode.Label");
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_ChildrenAreAdded()
		{
			var parentNode = new LayoutTreeNode { Label = "Parent" };
			var childNode = new LayoutTreeNode { Label = "Child" };
			parentNode.Nodes.Add(childNode);
			ConfigurableDictionaryNode configNode = null;

			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(parentNode));
			Assert.AreEqual(configNode.Label, parentNode.Label);
			Assert.IsNotNull(configNode.Children);
			Assert.AreEqual(configNode.Children.Count, 1);
			Assert.AreEqual(configNode.Children[0].Label, childNode.Label);
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_SenseNumberStyleIsAddedAndUsed()
		{
			var senseNumberNode = new LayoutTreeNode
			{
				Label = "Parent",
				NumFont = "arial",
				NumStyle = "bold -italic",
				ShowSenseConfig = true
			};
			ConfigurableDictionaryNode configNode = null;
			const string styleName = "Dictionary-SenseNumber";
			var senseStyle = _lcmStyleSheet.FindStyle(styleName);
			Assert.IsNull(senseStyle, "Sense number should not exist before conversion for a valid test.");

			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(senseNumberNode));
			Assert.AreEqual(((DictionaryNodeSenseOptions)configNode.DictionaryNodeOptions).NumberStyle, styleName);
			senseStyle = _lcmStyleSheet.FindStyle(styleName);
			Assert.IsNotNull(senseStyle, "Sense number should have been created by the migrator.");
			var usefulStyle = _lcmStyleSheet.Styles[styleName];
			Assert.IsTrue(usefulStyle.DefaultCharacterStyleInfo.Bold.Value, "bold was not turned on in the created style.");
			Assert.IsFalse(usefulStyle.DefaultCharacterStyleInfo.Italic.Value, "italic was not turned off in the created style.");
			Assert.AreEqual(usefulStyle.DefaultCharacterStyleInfo.FontName.Value, "arial", "arial font not used");
			DeleteStyleSheet(styleName);
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_SenseConfigsWithDifferingStylesMakeTwoStyles()
		{
			var senseNumberNode = new LayoutTreeNode
			{
				Label = "Parent",
				NumFont = "arial",
				NumStyle = "bold -italic",
				ShowSenseConfig = true
			};
			var senseNumberNode2 = new LayoutTreeNode
			{
				Label = "Parent",
				NumFont = "arial",
				NumStyle = "bold",
				ShowSenseConfig = true
			};
			ConfigurableDictionaryNode configNode = null;
			const string styleName = "Dictionary-SenseNumber";
			const string styleName2 = "Dictionary-SenseNumber-2";
			var senseStyle = _lcmStyleSheet.FindStyle(styleName);
			var senseStyle2 = _lcmStyleSheet.FindStyle(styleName2);
			Assert.IsNull(senseStyle, "Sense number style should not exist before conversion for a valid test.");
			Assert.IsNull(senseStyle2, "Second sense number style should not exist before conversion for a valid test.");

			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(senseNumberNode));
			Assert.AreEqual(((DictionaryNodeSenseOptions)configNode.DictionaryNodeOptions).NumberStyle, styleName);
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(senseNumberNode2));
			Assert.AreEqual(((DictionaryNodeSenseOptions)configNode.DictionaryNodeOptions).NumberStyle, styleName2);
			senseStyle = _lcmStyleSheet.FindStyle(styleName);
			senseStyle2 = _lcmStyleSheet.FindStyle(styleName2);
			Assert.IsNotNull(senseStyle, "Sense number should have been created by the migrator.");
			Assert.IsNotNull(senseStyle2, "Sense number should have been created by the migrator.");
			var usefulStyle = _lcmStyleSheet.Styles[styleName];
			Assert.IsTrue(usefulStyle.DefaultCharacterStyleInfo.Bold.Value, "bold was not turned on in the created style.");
			Assert.IsFalse(usefulStyle.DefaultCharacterStyleInfo.Italic.Value, "italic was not turned off in the created style.");
			Assert.AreEqual(usefulStyle.DefaultCharacterStyleInfo.FontName.Value, "arial", "arial font not used");
			usefulStyle = _lcmStyleSheet.Styles[styleName2];
			Assert.IsTrue(usefulStyle.DefaultCharacterStyleInfo.Bold.Value, "bold was not turned on in the created style.");
			Assert.IsFalse(usefulStyle.DefaultCharacterStyleInfo.Italic.ValueIsSet, "italic should not have been set in the created style.");
			Assert.AreEqual(usefulStyle.DefaultCharacterStyleInfo.FontName.Value, "arial", "arial font not used");
			DeleteStyleSheet(styleName);
			DeleteStyleSheet(styleName2);
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_AllDifferentNumStylesResultInNewStyleSheets()
		{
			var senseNumberOptions = new[] { "-bold -italic", "bold italic", "bold", "italic", "-bold italic", "bold -italic" };
			var senseNumberNode = new LayoutTreeNode
			{
				Label = "Parent",
				NumFont = "arial",
				ShowSenseConfig = true
			};
			ConfigurableDictionaryNode configNode = null;
			const string styleName = "Dictionary-SenseNumber";
			var lastStyleName = $"Dictionary-SenseNumber-{1 + senseNumberOptions.Length}";
			var senseStyle = _lcmStyleSheet.FindStyle(styleName);
			var senseStyle2 = _lcmStyleSheet.FindStyle(lastStyleName);
			Assert.IsNull(senseStyle, "Sense number style should not exist before conversion for a valid test.");
			Assert.IsNull(senseStyle2, "Second sense number style should not exist before conversion for a valid test.");

			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(senseNumberNode));
			Assert.AreEqual(((DictionaryNodeSenseOptions)configNode.DictionaryNodeOptions).NumberStyle, styleName);
			foreach (var option in senseNumberOptions)
			{
				senseNumberNode.NumStyle = option;
				Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(senseNumberNode));
			}
			Assert.AreEqual(((DictionaryNodeSenseOptions)configNode.DictionaryNodeOptions).NumberStyle, lastStyleName);
			DeleteStyleSheet(styleName);
			for (var i = 2; i < 2 + senseNumberOptions.Length; i++) // Delete all the created dictionary styles
			{
				DeleteStyleSheet($"Dictionary-SenseNumber-{i}");
			}
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_AllDifferentNumStylesMatchThemselves()
		{
			var senseNumberOptions = new[] { "-bold -italic", "bold italic", "bold", "italic", "-bold italic", "bold -italic", "" };
			var senseNumberNode = new LayoutTreeNode
			{
				Label = "Parent",
				NumFont = "arial",
				ShowSenseConfig = true
			};
			const string styleName = "Dictionary-SenseNumber";
			var senseStyle = _lcmStyleSheet.FindStyle(styleName);
			const string styleName2 = "Dictionary-SenseNumber-2";
			var senseStyle2 = _lcmStyleSheet.FindStyle(styleName2);
			Assert.IsNull(senseStyle, "Sense number style should not exist before conversion for a valid test.");
			Assert.IsNull(senseStyle2, "A second sense number style should not exist before conversion for a valid test.");

			foreach (var option in senseNumberOptions)
			{
				senseNumberNode.NumStyle = option;
				Assert.DoesNotThrow(() => _migrator.ConvertLayoutTreeNodeToConfigNode(senseNumberNode));
				Assert.DoesNotThrow(() => _migrator.ConvertLayoutTreeNodeToConfigNode(senseNumberNode));
				senseStyle2 = _lcmStyleSheet.FindStyle(styleName2);
				DeleteStyleSheet(styleName);
				Assert.IsNull(senseStyle2, "A duplicate sense number style should not have been created converting the same node twice.");
			}
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_SenseConfigsWithDifferentFontsMakeTwoStyles()
		{
			var senseNumberNode = new LayoutTreeNode
			{
				Label = "Parent",
				NumFont = "arial",
				ShowSenseConfig = true
			};
			var senseNumberNode2 = new LayoutTreeNode
			{
				Label = "Parent",
				NumFont = "notarial",
				ShowSenseConfig = true
			};
			ConfigurableDictionaryNode configNode = null;
			const string styleName = "Dictionary-SenseNumber";
			const string styleName2 = "Dictionary-SenseNumber-2";
			var senseStyle = _lcmStyleSheet.FindStyle(styleName);
			var senseStyle2 = _lcmStyleSheet.FindStyle(styleName2);
			Assert.IsNull(senseStyle, "Sense number style should not exist before conversion for a valid test.");
			Assert.IsNull(senseStyle2, "Second sense number style should not exist before conversion for a valid test.");

			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(senseNumberNode));
			Assert.AreEqual(((DictionaryNodeSenseOptions)configNode.DictionaryNodeOptions).NumberStyle, styleName);
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(senseNumberNode2));
			Assert.AreEqual(((DictionaryNodeSenseOptions)configNode.DictionaryNodeOptions).NumberStyle, styleName2);
			senseStyle = _lcmStyleSheet.FindStyle(styleName);
			senseStyle2 = _lcmStyleSheet.FindStyle(styleName2);
			Assert.IsNotNull(senseStyle, "Sense number should have been created by the migrator.");
			Assert.IsNotNull(senseStyle2, "Sense number should have been created by the migrator.");
			var usefulStyle = _lcmStyleSheet.Styles[styleName];
			Assert.AreEqual(usefulStyle.DefaultCharacterStyleInfo.FontName.Value, "arial", "arial font not used");
			usefulStyle = _lcmStyleSheet.Styles[styleName2];
			Assert.AreEqual(usefulStyle.DefaultCharacterStyleInfo.FontName.Value, "notarial", "notarial font not used in second style");
			DeleteStyleSheet(styleName);
			DeleteStyleSheet(styleName2);
		}

		///<summary />
		[Test]
		public void ConvertLayoutTreeNodeToConfigNode_SenseOptionsAreMigrated()
		{
			var senseNumberNode = new LayoutTreeNode
			{
				Label = "Parent",
				Number = "(%O)",
				NumberSingleSense = true,
				ShowSenseConfig = true
			};
			ConfigurableDictionaryNode configNode = null;
			Assert.DoesNotThrow(() => configNode = _migrator.ConvertLayoutTreeNodeToConfigNode(senseNumberNode));
			var senseOptions = configNode.DictionaryNodeOptions as DictionaryNodeSenseOptions;
			Assert.NotNull(senseOptions);
			Assert.IsTrue(senseOptions.NumberEvenASingleSense);
			Assert.AreEqual("(", senseOptions.BeforeNumber);
			Assert.AreEqual(")", senseOptions.AfterNumber);
			Assert.AreEqual("%O", senseOptions.NumberingStyle);
			DeleteStyleSheet("Dictionary-SenseNumber");
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_FieldDescriptionIsMigrated()
		{
			var convertedParentNode = new ConfigurableDictionaryNode { Label = "Parent" };
			var convertedChildNode = new ConfigurableDictionaryNode { Label = "Child" };
			convertedParentNode.Children = new List<ConfigurableDictionaryNode> { convertedChildNode };
			var convertedModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedParentNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			const string parentField = "ParentDescription";
			var baseParentNode = new ConfigurableDictionaryNode { Label = "Parent", FieldDescription = parentField };
			const string childField = "ChildDescription";
			var baseChildNode = new ConfigurableDictionaryNode { Label = "Child", FieldDescription = childField };
			baseParentNode.Children = new List<ConfigurableDictionaryNode> { baseChildNode };
			var baseModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { baseParentNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			Assert.DoesNotThrow(() => _migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, baseModel));
			Assert.AreEqual(convertedModel.Parts[0].FieldDescription, parentField, "Field description for parent node not migrated");
			Assert.AreEqual(convertedModel.Parts[0].Children[0].FieldDescription, childField, "Field description for child not migrated");
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_CSSClassOverrideIsMigrated()
		{
			var convertedParentNode = new ConfigurableDictionaryNode { Label = "Parent" };
			var convertedChildNode = new ConfigurableDictionaryNode { Label = "Child" };
			convertedParentNode.Children = new List<ConfigurableDictionaryNode> { convertedChildNode };
			var convertedModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedParentNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			const string parentOverride = "dad";
			var baseParentNode = new ConfigurableDictionaryNode { Label = "Parent", CSSClassNameOverride = parentOverride };
			const string childOverride = "johnboy";
			var baseChildNode = new ConfigurableDictionaryNode { Label = "Child", CSSClassNameOverride = childOverride };
			baseParentNode.Children = new List<ConfigurableDictionaryNode> { baseChildNode };
			var baseModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { baseParentNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			Assert.DoesNotThrow(() => _migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, baseModel));
			Assert.AreEqual(convertedModel.Parts[0].CSSClassNameOverride, parentOverride, "CssClassNameOverride for parent node not migrated");
			Assert.AreEqual(convertedModel.Parts[0].Children[0].CSSClassNameOverride, childOverride, "CssClassNameOverride for child not migrated");
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_NewStyleDefaultsAreAddedWhenStyleIsNotSet()
		{
			var convertedParentNode = new ConfigurableDictionaryNode { Label = "Parent" };
			var convertedChildNode1 = new ConfigurableDictionaryNode { Label = "Little Thing 1" };
			var convertedChildNode2 = new ConfigurableDictionaryNode { Label = "Little Thing 2" };
			convertedParentNode.Children = new List<ConfigurableDictionaryNode> { convertedChildNode1, convertedChildNode2 };
			var convertedModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedParentNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			const StyleTypes parentOverride = StyleTypes.Paragraph;
			const StyleTypes child1Override = StyleTypes.Character;
			const StyleTypes defaultStyleType = StyleTypes.Default;
			const string baseStyle = "80's";
			var baseParentNode = new ConfigurableDictionaryNode { Label = "Parent", StyleType = parentOverride };
			var baseChildNode1 = new ConfigurableDictionaryNode { Label = "Little Thing 1", StyleType = child1Override, Style = baseStyle };
			var baseChildNode2 = new ConfigurableDictionaryNode { Label = "Little Thing 2" }; // Child2 will have the default StyleType
			baseParentNode.Children = new List<ConfigurableDictionaryNode> { baseChildNode1, baseChildNode2 };
			var baseModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { baseParentNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			Assert.DoesNotThrow(() => _migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, baseModel));
			Assert.AreEqual(parentOverride, convertedModel.Parts[0].StyleType, "StyleType for parent node not filled in from base");
			Assert.AreEqual(child1Override, convertedModel.Parts[0].Children[0].StyleType, "StyleType for child 1 not filled in from base");
			Assert.AreEqual(baseStyle, convertedModel.Parts[0].Children[0].Style, "Style for child 1 not filled in from base");
			Assert.AreEqual(defaultStyleType, convertedModel.Parts[0].Children[1].StyleType, "StyleType for child 2 not set to Default");
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_StyleInfoIsMigratedWhenStyleIsSet()
		{
			const StyleTypes parentStyleType = StyleTypes.Paragraph;
			const StyleTypes childStyleType = StyleTypes.Character;
			const string parentStyle = "bold";
			var convertedParentNode = new ConfigurableDictionaryNode
			{
				Label = "Parent",
				StyleType = parentStyleType,
				Style = parentStyle
			};
			const string childStyle = "italic";
			var convertedChildNode1 = new ConfigurableDictionaryNode
			{
				Label = "Little Thing 1",
				StyleType = childStyleType,
				Style = childStyle
			};
			convertedParentNode.Children = new List<ConfigurableDictionaryNode> { convertedChildNode1 };
			var convertedModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedParentNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			var baseParentNode = new ConfigurableDictionaryNode { Label = "Parent", StyleType = StyleTypes.Character, Style = "unused" };
			var baseChildNode1 = new ConfigurableDictionaryNode { Label = "Little Thing 1", StyleType = StyleTypes.Paragraph, Style = "unused2" };
			baseParentNode.Children = new List<ConfigurableDictionaryNode> { baseChildNode1 };
			var baseModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { baseParentNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			Assert.DoesNotThrow(() => _migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, baseModel));
			Assert.AreEqual(parentStyleType, convertedModel.Parts[0].StyleType, "The parent StyleType was not migrated correctly or was incorrectly overwritten");
			Assert.AreEqual(parentStyle, convertedModel.Parts[0].Style, "parent Style not migrated");
			Assert.AreEqual(childStyleType, convertedModel.Parts[0].Children[0].StyleType, "child StyleType not migrated");
			Assert.AreEqual(childStyle, convertedModel.Parts[0].Children[0].Style, "child Style not migrated");
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_WsOptionIsMigrated()
		{
			var convertedParentNode = new ConfigurableDictionaryNode { Label = "Parent" };
			var convertedChildNode = new ConfigurableDictionaryNode { Label = "Child" };
			convertedParentNode.Children = new List<ConfigurableDictionaryNode> { convertedChildNode };
			var convertedModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedParentNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			var baseParentNode = new ConfigurableDictionaryNode { Label = "Parent", DictionaryNodeOptions = new DictionaryNodeWritingSystemOptions() };
			((DictionaryNodeWritingSystemOptions)baseParentNode.DictionaryNodeOptions).WsType = WritingSystemType.Vernacular;
			((DictionaryNodeWritingSystemOptions)baseParentNode.DictionaryNodeOptions).DisplayWritingSystemAbbreviations = false;
			((DictionaryNodeWritingSystemOptions)baseParentNode.DictionaryNodeOptions).Options = new List<DictionaryNodeOption>
			{
				new DictionaryNodeOption { Id = "vernacular", IsEnabled = true }
			};
			var baseChildNode = new ConfigurableDictionaryNode { Label = "Child", DictionaryNodeOptions = new DictionaryNodeWritingSystemOptions() };
			((DictionaryNodeWritingSystemOptions)baseChildNode.DictionaryNodeOptions).WsType = WritingSystemType.Analysis;
			((DictionaryNodeWritingSystemOptions)baseChildNode.DictionaryNodeOptions).DisplayWritingSystemAbbreviations = false;
			((DictionaryNodeWritingSystemOptions)baseChildNode.DictionaryNodeOptions).Options = new List<DictionaryNodeOption>
			{
				new DictionaryNodeOption { Id = "analysis", IsEnabled = true }
			};
			baseParentNode.Children = new List<ConfigurableDictionaryNode> { baseChildNode };
			var baseModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { baseParentNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			Assert.DoesNotThrow(() => _migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, baseModel));
			Assert.AreEqual(convertedModel.Parts[0].DictionaryNodeOptions, baseModel.Parts[0].DictionaryNodeOptions, "DictionaryNodeOptions for parent node not migrated");
			Assert.AreEqual(convertedModel.Parts[0].Children[0].DictionaryNodeOptions, baseModel.Parts[0].Children[0].DictionaryNodeOptions, "DictionaryNodeOptions for child not migrated");
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_CopyOfNodeGetsValueFromBase()
		{
			var convertedParentNode = new ConfigurableDictionaryNode { Label = "Parent" };
			// make convertedChildNode look like a copy of a Child node which is not represented in the test.
			var convertedChildNode = new ConfigurableDictionaryNode { Label = "Child", LabelSuffix = "1" };
			convertedParentNode.Children = new List<ConfigurableDictionaryNode> { convertedChildNode };
			var convertedModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedParentNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			const string parentField = "ParentDescription";
			var baseParentNode = new ConfigurableDictionaryNode { Label = "Parent", FieldDescription = parentField };
			const string childField = "ChildDescription";
			var baseChildNode = new ConfigurableDictionaryNode { Label = "Child", FieldDescription = childField };
			baseParentNode.Children = new List<ConfigurableDictionaryNode> { baseChildNode };
			var baseModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { baseParentNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			Assert.DoesNotThrow(() => _migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, baseModel));
			Assert.AreEqual(convertedModel.Parts[0].Children[0].FieldDescription, childField, "Field description for copy of child not migrated");
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_TwoCopiesBothGetValueFromBase()
		{
			var convertedParentNode = new ConfigurableDictionaryNode { Label = "Parent" };
			var convertedChildNode = new ConfigurableDictionaryNode { Label = "Child" };
			var convertedChildNodeCopy1 = new ConfigurableDictionaryNode { Label = "Child", LabelSuffix = "1" };
			var convertedChildNodeCopy2 = new ConfigurableDictionaryNode { Label = "Child", LabelSuffix = "2" };
			convertedParentNode.Children = new List<ConfigurableDictionaryNode> { convertedChildNode, convertedChildNodeCopy1, convertedChildNodeCopy2 };
			var convertedModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedParentNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			const string parentField = "ParentDescription";
			var baseParentNode = new ConfigurableDictionaryNode { Label = "Parent", FieldDescription = parentField };
			const string childField = "ChildDescription";
			var baseChildNode = new ConfigurableDictionaryNode { Label = "Child", FieldDescription = childField };
			baseParentNode.Children = new List<ConfigurableDictionaryNode> { baseChildNode };
			var baseModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { baseParentNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			Assert.DoesNotThrow(() => _migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, baseModel));
			Assert.AreEqual(convertedModel.Parts[0].Children.Count, 3, "The copied children did not get migrated");
			Assert.AreEqual(convertedModel.Parts[0].Children[0].FieldDescription, childField, "Field description for copy of child not migrated");
			Assert.AreEqual(convertedModel.Parts[0].Children[1].FieldDescription, childField, "Field description for copy of child not migrated");
			Assert.AreEqual(convertedModel.Parts[0].Children[2].FieldDescription, childField, "Field description for copy of child not migrated");
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_NewNodeFromBaseIsMerged()
		{
			var convertedParentNode = new ConfigurableDictionaryNode { Label = "Parent" };
			var convertedChildNode = new ConfigurableDictionaryNode { Label = "Child" };
			convertedParentNode.Children = new List<ConfigurableDictionaryNode> { convertedChildNode };
			var convertedModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedParentNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			var baseParentNode = new ConfigurableDictionaryNode { Label = "Parent" };
			var baseChildNode = new ConfigurableDictionaryNode { Label = "Child" };
			var baseChildNodeTwo = new ConfigurableDictionaryNode { Label = "Child2" };
			baseParentNode.Children = new List<ConfigurableDictionaryNode> { baseChildNode, baseChildNodeTwo };
			var baseModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { baseParentNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			using (_migrator.SetTestLogger = new SimpleLogger())
			{
				Assert.DoesNotThrow(() => _migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, baseModel));
			}
			Assert.AreEqual(convertedModel.Parts[0].Children.Count, 2, "New node from base was not merged");
			Assert.AreEqual(convertedModel.Parts[0].Children[0].Label, "Child", "new node inserted out of order");
			Assert.AreEqual(convertedModel.Parts[0].Children[1].Label, "Child2", "New node from base was not merged properly");
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_OrderFromOldModelIsRetained()
		{
			var convertedParentNode = new ConfigurableDictionaryNode { Label = "Parent" };
			var convertedChildNode = new ConfigurableDictionaryNode { Label = "Child" };
			var convertedChildNodeTwo = new ConfigurableDictionaryNode { Label = "Child2" };
			convertedParentNode.Children = new List<ConfigurableDictionaryNode> { convertedChildNodeTwo, convertedChildNode };
			var convertedModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedParentNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			var baseParentNode = new ConfigurableDictionaryNode { Label = "Parent" };
			var baseChildNode = new ConfigurableDictionaryNode { Label = "Child" };
			var baseChildNodeTwo = new ConfigurableDictionaryNode { Label = "Child2" };
			baseParentNode.Children = new List<ConfigurableDictionaryNode> { baseChildNode, baseChildNodeTwo };
			var baseModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { baseParentNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			using (_migrator.SetTestLogger = new SimpleLogger())
			{
				Assert.DoesNotThrow(() => _migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, baseModel));
			}
			Assert.AreEqual(convertedModel.Parts[0].Children.Count, 2, "Nodes incorrectly merged");
			Assert.AreEqual(convertedModel.Parts[0].Children[0].Label, convertedChildNodeTwo.Label, "order of old model was not retained");
			Assert.AreEqual(convertedModel.Parts[0].Children[1].Label, convertedChildNode.Label, "Nodes incorrectly merged");
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_UnmatchedNodeFromOldModelIsCustom()
		{
			var convertedParentNode = new ConfigurableDictionaryNode { Label = "Parent" };
			var customNode = new ConfigurableDictionaryNode { Label = CustomFieldUnchangedNameAndLabel };
			var oldChild = new ConfigurableDictionaryNode { Label = "Child" };
			convertedParentNode.Children = new List<ConfigurableDictionaryNode> { customNode, oldChild };
			var convertedModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedParentNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			var baseParentNode = new ConfigurableDictionaryNode { Label = "Parent", FieldDescription = "LexEntry" };
			var baseChildNode = new ConfigurableDictionaryNode { Label = "Child" };
			baseParentNode.Children = new List<ConfigurableDictionaryNode> { baseChildNode };
			var baseModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { baseParentNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			using (_migrator.SetTestLogger = new SimpleLogger())
			{
				_migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, baseModel);
			}
			Assert.AreEqual(convertedModel.Parts[0].Children.Count, 2, "Nodes incorrectly merged");
			Assert.AreEqual(convertedModel.Parts[0].Children[0].Label, customNode.Label, "order of old model was not retained");
			Assert.IsFalse(oldChild.IsCustomField, "Child node which is matched should not be a custom field");
			Assert.IsTrue(customNode.IsCustomField, "The unmatched 'Custom' node should have been marked as a custom field");
			Assert.AreEqual(customNode.Label, customNode.FieldDescription, "Custom nodes' Labels and Fields should match");
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_NestedCustomFieldsAreAllMarked()
		{
			var convertedParentNode = new ConfigurableDictionaryNode { Label = "Parent" };
			var customNode = new ConfigurableDictionaryNode { Label = CustomFieldUnchangedNameAndLabel };
			var oldChild = new ConfigurableDictionaryNode { Label = "Child" };
			convertedParentNode.Children = new List<ConfigurableDictionaryNode> { customNode, oldChild };
			var customChild = new ConfigurableDictionaryNode { Label = "Custom Child" };
			customNode.Children = new List<ConfigurableDictionaryNode> { customChild };
			var convertedModel = new DictionaryConfigurationModel
			{
				Version = PreHistoricMigrator.VersionPre83,
				Parts = new List<ConfigurableDictionaryNode> { convertedParentNode }
			};
			var baseParentNode = new ConfigurableDictionaryNode { Label = "Parent", FieldDescription = "LexEntry" };
			var baseChildNode = new ConfigurableDictionaryNode { Label = "Child" };
			baseParentNode.Children = new List<ConfigurableDictionaryNode> { baseChildNode };
			var baseModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { baseParentNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			using (_migrator.SetTestLogger = new SimpleLogger())
			{
				_migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, baseModel);
			}
			Assert.AreEqual(convertedModel.Parts[0].Children.Count, 2, "Nodes incorrectly merged");
			Assert.AreEqual(convertedModel.Parts[0].Children[0].Label, customNode.Label, "order of old model was not retained");
			Assert.IsFalse(oldChild.IsCustomField, "Child node which is matched should not be a custom field");
			Assert.IsTrue(customNode.IsCustomField, "The unmatched 'Custom' node should have been marked as a custom field");
			Assert.IsFalse(customChild.IsCustomField, "Children of Custom nodes are not necessarily Custom.");
			Assert.AreEqual(customNode.Label, customNode.FieldDescription, "Custom nodes' Labels and Fields should match");
			Assert.AreEqual(customChild.Label, customChild.FieldDescription, "Custom nodes' Labels and Fields should match");
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_RelabeledCustomFieldsNamesAreMigrated()
		{
			var customNode = new ConfigurableDictionaryNode { Label = CustomFieldChangedLabel };
			var oldChild = new ConfigurableDictionaryNode { Label = "Child" };
			var convertedParentNode = new ConfigurableDictionaryNode
			{
				Label = "Parent",
				Children = new List<ConfigurableDictionaryNode> { customNode, oldChild }
			};
			var convertedModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedParentNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			var baseChildNode = new ConfigurableDictionaryNode { Label = "Child" };
			var baseParentNode = new ConfigurableDictionaryNode
			{
				Label = "Parent",
				FieldDescription = "LexEntry",
				Children = new List<ConfigurableDictionaryNode> { baseChildNode }
			};
			var baseModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { baseParentNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			using (_migrator.SetTestLogger = new SimpleLogger())
			{
				_migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, baseModel);
			}
			Assert.AreEqual(convertedModel.Parts[0].Children[0].Label, customNode.Label, "label was not retained");
			Assert.IsTrue(customNode.IsCustomField, "The unmatched 'Custom' node should have been marked as a custom field");
			Assert.AreEqual(CustomFieldOriginalName, customNode.FieldDescription, "Custom node's Field should have been loaded from the Cache");
		}

		///<summary>
		/// If a standard node in the Dictionary Configuration has a custom child,
		/// but that node doesn't have any Custom Fields in the lexical database, do *not* throw.
		/// </summary>
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_CustomFieldInStrangePlaceDoesNotThrow()
		{
			var convertedParentNode = new ConfigurableDictionaryNode { Label = "Parent" };
			var customNode = new ConfigurableDictionaryNode { Label = "Truly Custom" };
			var oldChild = new ConfigurableDictionaryNode { Label = "Child" };
			convertedParentNode.Children = new List<ConfigurableDictionaryNode> { customNode, oldChild };
			var convertedModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedParentNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			var baseParentNode = new ConfigurableDictionaryNode { Label = "Parent", FieldDescription = "LexReference" };
			var baseChildNode = new ConfigurableDictionaryNode { Label = "Child" };
			baseParentNode.Children = new List<ConfigurableDictionaryNode> { baseChildNode };
			var baseModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { baseParentNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			using (var logger = _migrator.SetTestLogger = new SimpleLogger())
			{
				Assert.DoesNotThrow(() => _migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, baseModel));
				Assert.IsTrue(logger.Content.StartsWith("Could not match 'Truly Custom' in defaults. It may have been valid in a previous version, but is no longer. It will be removed next time the model is loaded."));
			}
			Assert.AreEqual(convertedModel.Parts[0].Children.Count, 2, "Nodes incorrectly merged");
			Assert.AreEqual(convertedModel.Parts[0].Children[0].Label, customNode.Label, "order of old model was not retained");
			Assert.IsFalse(oldChild.IsCustomField, "Child node which is matched should not be a custom field");
			Assert.IsTrue(customNode.IsCustomField, "The unmatched 'Custom' node should have been marked as a custom field");
			Assert.AreEqual(customNode.Label, customNode.FieldDescription, "Custom nodes' Labels and Fields should match");
		}

		[Test]
		public void CopyNewDefaultsIntoConvertedModel_ProperChildrenAdded()
		{
			var convertedParentNode = new ConfigurableDictionaryNode { Label = "Minor Entries", FieldDescription = "LexEntry" };
			var customPersonNode = new ConfigurableDictionaryNode { Label = CustomFieldLocation, Parent = convertedParentNode };
			var customGenDateNode = new ConfigurableDictionaryNode { Label = CustomFieldGenDate, Parent = convertedParentNode };
			convertedParentNode.Children = new List<ConfigurableDictionaryNode> { customPersonNode, customGenDateNode };
			var convertedModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedParentNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			// Test handling expanding "Minor Entries" to "Minor Entries (Complex Forms)" and "Minor Entries (Variants)"
			var baseParentNode = new ConfigurableDictionaryNode { Label = "Minor Entries (Complex Forms)", FieldDescription = "LexEntry" };
			var baseChildNode = new ConfigurableDictionaryNode { Label = "Child", Parent = baseParentNode };
			baseParentNode.Children = new List<ConfigurableDictionaryNode> { baseChildNode };
			var baseModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { baseParentNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			// Ensure we don't throw because the parent node's label has been expanded.
			using (_migrator.SetTestLogger = new SimpleLogger())
			{
				Assert.DoesNotThrow(() => _migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, baseModel));
			}
			Assert.AreEqual(3, convertedModel.Parts[0].Children.Count, "Nodes incorrectly merged");
			Assert.IsTrue(customPersonNode.IsCustomField, "Custom atomic list reference field should be flagged as custom");
			Assert.IsNotNull(customPersonNode.Children, "Custom atomic list reference field should have children (added)");
			Assert.AreEqual(2, customPersonNode.Children.Count, "Custom atomic list reference field should have two children added");
			for (int i = 0; i < customPersonNode.Children.Count; ++i)
			{
				var child = customPersonNode.Children[i];
				Assert.IsFalse(child.IsCustomField, "Children of customPersonNode should not be flagged as custom (" + i + ")");
				Assert.IsNotNull(child.DictionaryNodeOptions, "Children of customPersonNode should have a DictionaryNodeOptions object");
				Assert.IsTrue(child.DictionaryNodeOptions is DictionaryNodeWritingSystemOptions, "Children of customPersonNode DictionaryNodeOptions should be a DictionaryNodeWritingSystemOptions object");
			}
			Assert.AreEqual("Name", customPersonNode.Children[0].Label, "The first child of customPersonNode should be Name");
			Assert.AreEqual("Abbreviation", customPersonNode.Children[1].Label, "The second child of customPersonNode should be Abbreviation");
			Assert.IsNotNull(customPersonNode.DictionaryNodeOptions, "Custom atomic list reference field should have a DictionaryNodeOptions object");
			Assert.IsTrue(customPersonNode.DictionaryNodeOptions is DictionaryNodeListOptions, "Custom atomic list reference field DictionaryNodeOptions should be a DictionaryNodeListOptions object");
			Assert.IsTrue(customGenDateNode.IsCustomField, "Custom GenDate field should be flagged as custom");
			Assert.IsNull(customGenDateNode.Children, "Custom GenDate field should not have any children (added)");
			Assert.IsNull(customGenDateNode.DictionaryNodeOptions, "Custom GenDate field should not have a DictionaryNodeOptions object");

		}

		#region Minor Entry Componenents Referenced Entries Tests
		private const string HwBefore = "H.before";
		private const string GlsBefore = "G.before";
		private const string HwAfter = "H.after";
		private const string GlsAfter = "G.after";
		private const string HwBetween = "H.between";
		private const string GlsBetween = "G.between";
		private const string GlsStyle = "G.Style";

		private static DictionaryConfigurationModel BuildConvertedReferenceEntryNodes(bool enableHeadword, bool enableSummaryDef, bool enableSenseHeadWord, bool enableGloss)
		{
			var headWord = new ConfigurableDictionaryNode { Label = "Referenced Headword", IsEnabled = enableHeadword, Before = HwBefore };
			var summaryDef = new ConfigurableDictionaryNode { Label = "Summary Definition", IsEnabled = enableSummaryDef, Before = GlsBefore };
			var senseHeadWord = new ConfigurableDictionaryNode { Label = "Referenced Sense Headword", IsEnabled = enableSenseHeadWord, Between = HwBetween, After = HwAfter };
			var gloss = new ConfigurableDictionaryNode { Label = "Gloss", IsEnabled = enableGloss, Between = GlsBetween, After = GlsAfter, Style = GlsStyle };
			var referencedEntriesNode = new ConfigurableDictionaryNode
			{
				Label = "Referenced Entries",
				IsEnabled = true,
				Children = new List<ConfigurableDictionaryNode> { headWord, summaryDef, senseHeadWord, gloss }
			};
			var componentsNode = new ConfigurableDictionaryNode
			{
				Label = "Components",
				IsEnabled = true,
				Children = new List<ConfigurableDictionaryNode> { referencedEntriesNode }
			};
			var minorEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Minor Entry",
				IsEnabled = true,
				Children = new List<ConfigurableDictionaryNode> { componentsNode }
			};
			var model = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { minorEntryNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			DictionaryConfigurationModel.SpecifyParentsAndReferences(model.Parts);
			return model;
		}

		private static DictionaryConfigurationModel BuildCurrentDefaultReferenceEntryNodes(bool enableHeadWord, bool enableGloss)
		{
			var headWord = new ConfigurableDictionaryNode { Label = "Referenced Headword", IsEnabled = enableHeadWord, FieldDescription = "HeadWord" };
			var gloss = new ConfigurableDictionaryNode { Label = "Gloss (or Summary Definition)", IsEnabled = enableGloss, FieldDescription = "DefinitionOrGloss" };
			var referencedEntriesNode = new ConfigurableDictionaryNode
			{
				FieldDescription = "ConfigReferencedEntries",
				Label = "Referenced Entries",
				IsEnabled = true,
				Children = new List<ConfigurableDictionaryNode> { headWord, gloss }
			};
			var componentsNode = new ConfigurableDictionaryNode
			{
				FieldDescription = "ComplexFormEntryRefs",
				Label = "Components",
				IsEnabled = true,
				Children = new List<ConfigurableDictionaryNode> { referencedEntriesNode }
			};
			var minorEntryNode = new ConfigurableDictionaryNode
			{
				FieldDescription = "LexEntry",
				Label = "Minor Entry",
				IsEnabled = true,
				Children = new List<ConfigurableDictionaryNode> { componentsNode }
			};
			var model = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { minorEntryNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};
			DictionaryConfigurationModel.SpecifyParentsAndReferences(model.Parts);
			return model;
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_MinorEntryComponentsReferencedEntriesChanged()
		{
			const string refEntriesPath = "//ConfigurationItem[@name='Minor Entry']/ConfigurationItem[@name='Components']/ConfigurationItem[@name='Referenced Entries']/";
			using (var convertedModelFile = new TempFile())
			{
				var convertedMinorEntry = BuildConvertedReferenceEntryNodes(true, true, true, true);
				convertedMinorEntry.FilePath = convertedModelFile.Path;
				var defaultMinorEntry = BuildCurrentDefaultReferenceEntryNodes(true, true);

				_migrator.CopyNewDefaultsIntoConvertedModel(convertedMinorEntry, defaultMinorEntry);
				convertedMinorEntry.Save();
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(refEntriesPath + "ConfigurationItem[@name='Referenced Sense Headword']");
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(refEntriesPath + "ConfigurationItem[@name='Gloss']");
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(refEntriesPath + "ConfigurationItem[@name='Summary Definition']");
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Referenced Headword']", 1);
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Gloss (or Summary Definition)']", 1);
			}
		}

		/// <summary>
		/// In most cases, the 'Minor Entry->Components->Referenced Entries' node has a child 'Referenced Sense Headword' that needs to be merged
		/// with 'Referenced Headword'; however, in one case, that child's name is 'Referenced Sense'. This tests that special case.
		/// </summary>
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_MinorEntryComponentsReferencedEntriesChangedInSpecialCase()
		{
			const string refEntriesPath = "//ConfigurationItem[@name='Minor Entry']/ConfigurationItem[@name='Components']/ConfigurationItem[@name='Referenced Entries']/";
			using (var convertedModelFile = new TempFile())
			{
				var convertedMinorEntry = BuildConvertedReferenceEntryNodes(true, true, true, true);
				convertedMinorEntry.Parts[0].Children[0].Children[0].Children.First(child => child.Label == "Referenced Sense Headword").Label
																											= "Referenced Sense";
				convertedMinorEntry.FilePath = convertedModelFile.Path;
				var defaultMinorEntry = BuildCurrentDefaultReferenceEntryNodes(true, true);

				_migrator.CopyNewDefaultsIntoConvertedModel(convertedMinorEntry, defaultMinorEntry);
				convertedMinorEntry.Save();
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(refEntriesPath + "ConfigurationItem[@name='Referenced Sense']");
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(refEntriesPath + "ConfigurationItem[@name='Referenced Sense Headword']");
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Referenced Headword']", 1);
			}
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_MinorEntryComponentsBeforeAfterBetweenMigrated()
		{
			using (var convertedModelFile = new TempFile())
			{
				var convertedMinorEntry = BuildConvertedReferenceEntryNodes(true, true, true, true);
				convertedMinorEntry.FilePath = convertedModelFile.Path;
				var defaultMinorEntry = BuildCurrentDefaultReferenceEntryNodes(true, true);

				_migrator.CopyNewDefaultsIntoConvertedModel(convertedMinorEntry, defaultMinorEntry);
				string cssResults = null;
				Assert.DoesNotThrow(() => cssResults = CssGenerator.GenerateCssFromConfiguration(convertedMinorEntry, new ReadOnlyPropertyTable(_flexComponentParameters.PropertyTable)));
				Assert.That(cssResults, Is.StringContaining(HwBefore));
				Assert.That(cssResults, Is.StringContaining(HwBetween));
				Assert.That(cssResults, Is.StringContaining(HwAfter));
				Assert.That(cssResults, Is.StringContaining(GlsBefore));
				Assert.That(cssResults, Is.StringContaining(GlsBetween));
				Assert.That(cssResults, Is.StringContaining(GlsAfter));
			}
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_MinorEntryComponentsHeadwordChecksMigrated()
		{
			const string refEntriesPath = "//ConfigurationItem[@name='Minor Entry']/ConfigurationItem[@name='Components']/ConfigurationItem[@name='Referenced Entries']/";
			using (var convertedModelFile = new TempFile())
			{
				var convertedMinorEntry = BuildConvertedReferenceEntryNodes(true, false, false, false);
				convertedMinorEntry.FilePath = convertedModelFile.Path;
				var defaultMinorEntry = BuildCurrentDefaultReferenceEntryNodes(false, false);
				_migrator.CopyNewDefaultsIntoConvertedModel(convertedMinorEntry, defaultMinorEntry);
				convertedMinorEntry.Save();
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Referenced Headword' and @isEnabled='true']", 1);

				convertedMinorEntry = BuildConvertedReferenceEntryNodes(false, false, false, false);
				convertedMinorEntry.FilePath = convertedModelFile.Path;
				defaultMinorEntry = BuildCurrentDefaultReferenceEntryNodes(true, false);
				_migrator.CopyNewDefaultsIntoConvertedModel(convertedMinorEntry, defaultMinorEntry);
				convertedMinorEntry.Save();
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Referenced Headword' and @isEnabled='false']", 1);

				convertedMinorEntry = BuildConvertedReferenceEntryNodes(false, false, true, false);
				convertedMinorEntry.FilePath = convertedModelFile.Path;
				defaultMinorEntry = BuildCurrentDefaultReferenceEntryNodes(false, false);
				_migrator.CopyNewDefaultsIntoConvertedModel(convertedMinorEntry, defaultMinorEntry);
				convertedMinorEntry.Save();
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Referenced Headword' and @isEnabled='true']", 1);
			}
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_MinorEntryComponentsGlossChecksMigrated()
		{
			const string refEntriesPath = "//ConfigurationItem[@name='Minor Entry']/ConfigurationItem[@name='Components']/ConfigurationItem[@name='Referenced Entries']/";
			using (var convertedModelFile = new TempFile())
			{
				var convertedMinorEntry = BuildConvertedReferenceEntryNodes(false, true, false, false);
				convertedMinorEntry.FilePath = convertedModelFile.Path;
				var defaultMinorEntry = BuildCurrentDefaultReferenceEntryNodes(false, false);
				_migrator.CopyNewDefaultsIntoConvertedModel(convertedMinorEntry, defaultMinorEntry);
				convertedMinorEntry.Save();
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Gloss (or Summary Definition)' and @isEnabled='true']", 1);

				convertedMinorEntry = BuildConvertedReferenceEntryNodes(false, false, false, false);
				convertedMinorEntry.FilePath = convertedModelFile.Path;
				defaultMinorEntry = BuildCurrentDefaultReferenceEntryNodes(false, true);
				_migrator.CopyNewDefaultsIntoConvertedModel(convertedMinorEntry, defaultMinorEntry);
				convertedMinorEntry.Save();
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Gloss (or Summary Definition)' and @isEnabled='false']", 1);

				convertedMinorEntry = BuildConvertedReferenceEntryNodes(false, false, false, true);
				convertedMinorEntry.FilePath = convertedModelFile.Path;
				defaultMinorEntry = BuildCurrentDefaultReferenceEntryNodes(false, false);
				_migrator.CopyNewDefaultsIntoConvertedModel(convertedMinorEntry, defaultMinorEntry);
				convertedMinorEntry.Save();
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Gloss (or Summary Definition)' and @isEnabled='true']", 1);
			}
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_DuplicatedConvertedNodesDoesNotBreakOriginal()
		{
			const string refEntriesPath = "//ConfigurationItem[@name='Minor Entry']/ConfigurationItem[@name='Components']/ConfigurationItem[@name='Referenced Entries']/";
			using (var convertedModelFile = new TempFile())
			{
				var convertedMinorEntry = BuildConvertedReferenceEntryNodes(true, true, true, true);
				var componentsDup = convertedMinorEntry.Parts[0].Children[0].Children[0].DuplicateAmongSiblings();
				componentsDup.Children.First(c => c.Label == "Gloss").Before = null;
				componentsDup.Children.First(c => c.Label == "Gloss").After = null;
				componentsDup.Children.First(c => c.Label == "Gloss").Between = null;
				componentsDup.Children.First(c => c.Label == "Gloss").Style = null;
				componentsDup.Children.First(c => c.Label == "Summary Definition").Before = null;
				componentsDup.Children.First(c => c.Label == "Summary Definition").After = null;
				componentsDup.Children.First(c => c.Label == "Summary Definition").Between = null;
				componentsDup.Children.First(c => c.Label == "Summary Definition").Style = null;

				convertedMinorEntry.FilePath = convertedModelFile.Path;
				var defaultMinorEntry = BuildCurrentDefaultReferenceEntryNodes(false, false);
				_migrator.CopyNewDefaultsIntoConvertedModel(convertedMinorEntry, defaultMinorEntry);
				convertedMinorEntry.Save();
				// There should be one node with Before on Gloss (or Summary Definition) and one with no such content
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Gloss (or Summary Definition)']", 2);
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Gloss (or Summary Definition)' and @before]", 1);
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Gloss (or Summary Definition)' and @after]", 1);
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Gloss (or Summary Definition)' and @between]", 1);
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Gloss (or Summary Definition)' and @style]", 1);
			}
		}

		#endregion

		private static DictionaryConfigurationModel BuildConvertedComplexEntryTypeNodes()
		{
			var reverseAbbr = new ConfigurableDictionaryNode
			{
				Label = "Abbreviation"
			};
			var complexFormTypeNode = new ConfigurableDictionaryNode
			{
				Label = "Complex Form Type",
				Children = new List<ConfigurableDictionaryNode> { reverseAbbr }
			};
			var subentriesNode = new ConfigurableDictionaryNode
			{
				Label = "Subentries",
				Children = new List<ConfigurableDictionaryNode> { complexFormTypeNode }
			};
			var mainEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Main Entry",
				CSSClassNameOverride = "entry",
				Children = new List<ConfigurableDictionaryNode> { subentriesNode }
			};
			CssGeneratorTests.PopulateFieldsForTesting(mainEntryNode);

			return new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { mainEntryNode },
				Version = PreHistoricMigrator.VersionPre83
			};
		}

		private static DictionaryConfigurationModel BuildCurrentDefaultComplexEntryTypeNodes()
		{
			var abbreviation = new ConfigurableDictionaryNode
			{
				Label = "Reverse Abbreviation",
				FieldDescription = "ReverseAbbr"
			};
			var complexFormTypeNode = new ConfigurableDictionaryNode
			{
				Label = "Complex Form Type",
				FieldDescription = ConfiguredLcmGenerator.LookupComplexEntryType,
				Children = new List<ConfigurableDictionaryNode> { abbreviation }
			};
			var subentriesNode = new ConfigurableDictionaryNode
			{
				Label = "Subentries",
				FieldDescription = "Subentries",
				Children = new List<ConfigurableDictionaryNode> { complexFormTypeNode }
			};
			var mainEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Main Entry",
				FieldDescription = "LexEntry",
				CSSClassNameOverride = "entry",
				Children = new List<ConfigurableDictionaryNode> { subentriesNode }
			};
			var model = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { mainEntryNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};
			CssGeneratorTests.PopulateFieldsForTesting(model);
			return model;
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_SubentryComplexTypeAbbreviationChanged()
		{
			const string complexEntryTypePath = "//ConfigurationItem[@name='Main Entry']/ConfigurationItem[@name='Subentries']/ConfigurationItem[@name='Complex Form Type']/";
			using (var convertedModelFile = new TempFile())
			{
				var convertedComplexEntryType = BuildConvertedComplexEntryTypeNodes();
				convertedComplexEntryType.FilePath = convertedModelFile.Path;
				var defaultComplexEntryType = BuildCurrentDefaultComplexEntryTypeNodes();

				_migrator.CopyNewDefaultsIntoConvertedModel(convertedComplexEntryType, defaultComplexEntryType);
				convertedComplexEntryType.Save();
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(complexEntryTypePath + "ConfigurationItem[@name='Abbreviation']");
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(complexEntryTypePath + "ConfigurationItem[@name='Reverse Abbreviation']", 1);
			}
		}

		private static DictionaryConfigurationModel BuildConvertedGrammaticalInfoNodes()
		{
			var features = new ConfigurableDictionaryNode
			{
				FieldDescription = "Features"
			};
			var grammaticalInfo = new ConfigurableDictionaryNode
			{
				Label = "Grammatical Info.",
				FieldDescription = "MorphoSyntaxAnalysisRA",
				Children = new List<ConfigurableDictionaryNode> { features }
			};
			var referencedSenses = new ConfigurableDictionaryNode
			{
				Label = "Referenced Senses",
				FieldDescription = "ReferringSenses",
				Children = new List<ConfigurableDictionaryNode> { grammaticalInfo }
			};
			var reversalEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Reversal Entry",
				FieldDescription = "LexEntry",
				Children = new List<ConfigurableDictionaryNode> { referencedSenses }
			};
			CssGeneratorTests.PopulateFieldsForTesting(reversalEntryNode);

			return new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { reversalEntryNode },
				Version = PreHistoricMigrator.VersionPre83
			};
		}

		private static DictionaryConfigurationModel BuildCurrentDefaultGrammaticalInfoNodes()
		{
			var inflectionFeatures = new ConfigurableDictionaryNode
			{
				Label = "Inflection Features",
				FieldDescription = "FeaturesTSS"
			};
			var grammaticalInfo = new ConfigurableDictionaryNode
			{
				Label = "Grammatical Info.",
				FieldDescription = "MorphoSyntaxAnalysisRA",
				Children = new List<ConfigurableDictionaryNode> { inflectionFeatures }
			};
			var referencedSenses = new ConfigurableDictionaryNode
			{
				Label = "Referenced Senses",
				FieldDescription = "ReferringSenses",
				Children = new List<ConfigurableDictionaryNode> { grammaticalInfo }
			};
			var reversalEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Reversal Entry",
				FieldDescription = "LexEntry",
				Children = new List<ConfigurableDictionaryNode> { referencedSenses }
			};
			var model = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { reversalEntryNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};
			CssGeneratorTests.PopulateFieldsForTesting(model);
			return model;
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_ReversalIndexInflectionFeaturesMigration()
		{
			const string grammaticalInfoTypePath = "//ConfigurationItem[@name='Reversal Entry']/ConfigurationItem[@name='Referenced Senses']/ConfigurationItem[@name='Grammatical Info.']/";
			using (var convertedModelFile = new TempFile())
			{
				var convertedGrammaticalInfoType = BuildConvertedGrammaticalInfoNodes();
				convertedGrammaticalInfoType.FilePath = convertedModelFile.Path;
				var defaultGrammaticalInfoType = BuildCurrentDefaultGrammaticalInfoNodes();

				using (_migrator.SetTestLogger = new SimpleLogger())
				{
					_migrator.CopyNewDefaultsIntoConvertedModel(convertedGrammaticalInfoType, defaultGrammaticalInfoType);
				}
				convertedGrammaticalInfoType.Save();
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(grammaticalInfoTypePath + "ConfigurationItem[@name='Features']");
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(grammaticalInfoTypePath + "ConfigurationItem[@name='Inflection Features']", 1);
			}
		}

		private static DictionaryConfigurationModel BuildConvertedReversalIndexChildNodes()
		{
			var reversalForm = new ConfigurableDictionaryNode
			{
				Label = "Reversal Form",
				FieldDescription = "ReversalForm"
			};
			var reversalCategory = new ConfigurableDictionaryNode
			{
				Label = "Reversal Category",
				FieldDescription = "PartOfSpeechRA"
			};
			var referencedSenses = new ConfigurableDictionaryNode
			{
				Label = "Referenced Senses",
				FieldDescription = "ReferringSenses"
			};
			var reversalEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Reversal Entry",
				FieldDescription = "LexEntry",
				Children = new List<ConfigurableDictionaryNode> { reversalForm, reversalCategory, referencedSenses }
			};
			CssGeneratorTests.PopulateFieldsForTesting(reversalEntryNode);
			return new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { reversalEntryNode },
				Version = PreHistoricMigrator.VersionPre83
			};
		}

		private static DictionaryConfigurationModel BuildCurrentDefaultReversalIndexChildNodes()
		{
			var reversalForm = new ConfigurableDictionaryNode
			{
				Label = "Reversal Form",
				FieldDescription = "ReversalForm"
			};
			var reversalCategory = new ConfigurableDictionaryNode
			{
				Label = "Reversal Category",
				FieldDescription = "PartOfSpeechRA"
			};
			var referencedSenses = new ConfigurableDictionaryNode
			{
				Label = "Referenced Senses",
				FieldDescription = "ReferringSenses"
			};
			var reversalEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Reversal Entry",
				FieldDescription = "LexEntry",
				Children = new List<ConfigurableDictionaryNode> { reversalForm, reversalCategory, referencedSenses }
			};
			var model = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { reversalEntryNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};
			CssGeneratorTests.PopulateFieldsForTesting(model);
			return model;
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_ReversalIndexChildNodesMigrated()
		{
			const string reversalIndexChildNodesPath = "//ConfigurationItem[@name='Reversal Entry']/";
			using (var convertedModelFile = new TempFile())
			{
				var convertedreversalIndexChildNodesType = BuildConvertedReversalIndexChildNodes();
				convertedreversalIndexChildNodesType.FilePath = convertedModelFile.Path;
				var defaultreversalIndexChildNodesType = BuildCurrentDefaultReversalIndexChildNodes();

				_migrator.CopyNewDefaultsIntoConvertedModel(convertedreversalIndexChildNodesType, defaultreversalIndexChildNodesType);
				convertedreversalIndexChildNodesType.Save();
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(reversalIndexChildNodesPath + "ConfigurationItem[@name='Form']");
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(reversalIndexChildNodesPath + "ConfigurationItem[@name='Reversal Form']", 1);
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(reversalIndexChildNodesPath + "ConfigurationItem[@name='Category']");
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(reversalIndexChildNodesPath + "ConfigurationItem[@name='Reversal Category']", 1);
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(reversalIndexChildNodesPath + "ConfigurationItem[@name='Referenced Senses']", 1);
			}
		}

		[Test]
		[TestCase("publishReversal", "All Reversal Indexes (original)", "AllReversalIndexes")]
		[TestCase("publishReversal#All RU93", "Copy of All Reversal Indexes", "Copy of All Reversal Indexes-AllReversalIndexes-#All RU93")]
		[TestCase("publishReversal-en", "English (original)", "en")]
		[TestCase("publishReversal-en#Engli704", "Copy of English", "Copy of English-en-#Engli704")]
		public void CopyDefaultsIntoConvertedModel_PicksSensibleNameForReversalIndexes(string oldLayout, string oldLabel, string newFileName)
		{
			var node = new ConfigurableDictionaryNode { Label = "Reversal Entry" };
			var model = new DictionaryConfigurationModel
			{
				Version = PreHistoricMigrator.VersionPre83,
				Label = oldLabel,
				Parts = new List<ConfigurableDictionaryNode> { node }
			};
			_migrator.m_configDirSuffixBeingMigrated = DictionaryConfigurationServices.ReversalIndexConfigurationDirectoryName;
			_migrator.CopyNewDefaultsIntoConvertedModel(oldLayout, model);
			Assert.AreEqual(newFileName, Path.GetFileNameWithoutExtension(model.FilePath));
		}

		private static DictionaryConfigurationModel BuildConvertedComponentReferencesNodes()
		{
			var headwordNode = new ConfigurableDictionaryNode { Label = "Referenced Headword" };
			var refSenseHeadwordNode = new ConfigurableDictionaryNode { Label = "Referenced Sense Headword" };
			var summaryDefNode = new ConfigurableDictionaryNode { Label = "Summary Definition" };
			var glossNode = new ConfigurableDictionaryNode { Label = "Gloss" };
			var componentsNode = new ConfigurableDictionaryNode
			{
				Label = "Components",
				Children = new List<ConfigurableDictionaryNode> { headwordNode, glossNode, summaryDefNode, refSenseHeadwordNode }
			};
			var componentReferencesNode = new ConfigurableDictionaryNode
			{
				Label = "Component References",
				Children = new List<ConfigurableDictionaryNode> { componentsNode }
			};
			var mainEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Main Entry",
				CSSClassNameOverride = "entry",
				Children = new List<ConfigurableDictionaryNode> { componentReferencesNode }
			};
			CssGeneratorTests.PopulateFieldsForTesting(mainEntryNode);

			return new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { mainEntryNode },
				Version = PreHistoricMigrator.VersionPre83
			};
		}

		private static DictionaryConfigurationModel BuildCurrentDefaultComponentReferencesNodes()
		{
			var refHeadwordNode = new ConfigurableDictionaryNode
			{
				Label = "Referenced Headword",
				FieldDescription = "HeadWord"
			};
			var glossNode = new ConfigurableDictionaryNode
			{
				Label = "Gloss (or Summary Definition)",
				FieldDescription = "DefinitionOrGloss"
			};
			var referencedEntriesNode = new ConfigurableDictionaryNode
			{
				Label = "Referenced Entries",
				FieldDescription = "ConfigReferencedEntries",
				CSSClassNameOverride = "referencedentries",
				Children = new List<ConfigurableDictionaryNode> { refHeadwordNode, glossNode }
			};
			var componentReferencesNode = new ConfigurableDictionaryNode
			{
				Label = "Component References",
				FieldDescription = "ComplexFormEntryRefs",
				Children = new List<ConfigurableDictionaryNode> { referencedEntriesNode }
			};
			var mainEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Main Entry",
				FieldDescription = "LexEntry",
				CSSClassNameOverride = "entry",
				Children = new List<ConfigurableDictionaryNode> { componentReferencesNode }
			};
			var model = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { mainEntryNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};
			CssGeneratorTests.PopulateFieldsForTesting(model);
			return model;
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_MainEntryComponentReferences_ComponentsRenamedToReferencedEntries()
		{
			const string refEntriesPath = "//ConfigurationItem[@name='Main Entry']/ConfigurationItem[@name='Component References']/";
			using (var convertedModelFile = new TempFile())
			{
				var convertedMainEntry = BuildConvertedComponentReferencesNodes();
				convertedMainEntry.FilePath = convertedModelFile.Path;
				var defaultMainEntry = BuildCurrentDefaultComponentReferencesNodes();

				_migrator.CopyNewDefaultsIntoConvertedModel(convertedMainEntry, defaultMainEntry);
				convertedMainEntry.Save();
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(refEntriesPath + "ConfigurationItem[@name='Components']");
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(refEntriesPath + "ConfigurationItem[@name='Referenced Entries']/ConfigurationItem[@name='Gloss']");
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(refEntriesPath + "ConfigurationItem[@name='Referenced Entries']/ConfigurationItem[@name='Summary Definition']");
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(refEntriesPath + "ConfigurationItem[@name='Referenced Entries']/ConfigurationItem[@name='Reference Sense HeadWord']");
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Referenced Entries']/ConfigurationItem[@name='Gloss (or Summary Definition)']", 1);
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(refEntriesPath + "ConfigurationItem[@name='Referenced Entries']/ConfigurationItem[@name='Referenced Headword']", 1);
			}
		}

		private static DictionaryConfigurationModel BuildConvertedHomographNumberNodes()
		{
			var subentryHomographNumberNode = new ConfigurableDictionaryNode { Label = "Homograph Number" };
			var subentriesNode = new ConfigurableDictionaryNode
			{
				Label = "Subentries",
				Children = new List<ConfigurableDictionaryNode> { subentryHomographNumberNode }
			};
			var headwordNode = new ConfigurableDictionaryNode { Label = "Headword" };
			var homographNumberNode = new ConfigurableDictionaryNode { Label = "Homograph Number" };
			var mainEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Main Entry",
				CSSClassNameOverride = "entry",
				Children = new List<ConfigurableDictionaryNode> { headwordNode, homographNumberNode, subentriesNode }
			};
			var minorHomographNumberNode = new ConfigurableDictionaryNode { Label = "Homograph Number" };
			var minorEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Minor Entry",
				CSSClassNameOverride = "minorentries",
				DictionaryNodeOptions = new DictionaryNodeListOptions(),
				Children = new List<ConfigurableDictionaryNode> { minorHomographNumberNode }
			};
			var model = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { mainEntryNode, minorEntryNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			CssGeneratorTests.PopulateFieldsForTesting(model);
			return model;
		}

		private static DictionaryConfigurationModel BuildCurrentDefaultHomographNumberNodes()
		{
			var subentryHomographNumberNode = new ConfigurableDictionaryNode
			{
				Label = "Secondary Homograph Number",
				FieldDescription = "HomographNumber"
			};
			var subentriesNode = new ConfigurableDictionaryNode
			{
				Label = "Subentries",
				FieldDescription = "Subentries",
				Children = new List<ConfigurableDictionaryNode> { subentryHomographNumberNode }
			};
			var headwordNode = new ConfigurableDictionaryNode
			{
				Label = "Headword",
				FieldDescription = "MLHeadWord"
			};
			var homographNumberNode = new ConfigurableDictionaryNode
			{
				Label = "Secondary Homograph Number",
				FieldDescription = "HomographNumber"
			};
			var mainEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Main Entry",
				FieldDescription = "LexEntry",
				CSSClassNameOverride = "entry",
				Children = new List<ConfigurableDictionaryNode> { headwordNode, homographNumberNode, subentriesNode }
			};
			var minorCfHomographNumberNode = new ConfigurableDictionaryNode
			{
				Label = "Secondary Homograph Number",
				FieldDescription = "HomographNumber"
			};
			var minorComplexNode = new ConfigurableDictionaryNode
			{
				Label = "Minor Entry (Complex Forms)",
				FieldDescription = "LexEntry",
				CSSClassNameOverride = "minorentrycomplex",
				Children = new List<ConfigurableDictionaryNode> { minorCfHomographNumberNode }
			};
			var minorVarHomographNumberNode = new ConfigurableDictionaryNode
			{
				Label = "Secondary Homograph Number",
				FieldDescription = "HomographNumber"
			};
			var minorVariantNode = new ConfigurableDictionaryNode
			{
				Label = "Minor Entry (Variants)",
				FieldDescription = "LexEntry",
				CSSClassNameOverride = "minorentryvariant",
				Children = new List<ConfigurableDictionaryNode> { minorVarHomographNumberNode }
			};
			var model = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { mainEntryNode, minorComplexNode, minorVariantNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};
			CssGeneratorTests.PopulateFieldsForTesting(model);

			return model;
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_Homograph_RenamedTo_SecondaryHomographNumber()
		{
			const string mainEntriesPath = "//ConfigurationItem[@name='Main Entry']/";
			const string subentriesPath = "//ConfigurationItem[@name='Main Entry']/ConfigurationItem[@name='Subentries']/";
			const string minorCfEntriesPath = "//ConfigurationItem[@name='Minor Entry (Complex Forms)']/";
			const string minorVarEntriesPath = "//ConfigurationItem[@name='Minor Entry (Variants)']/";
			const string oldHomographPath = "ConfigurationItem[@name='Homograph Number']";
			const string newHomographPath = "ConfigurationItem[@name='Secondary Homograph Number']";
			using (var convertedModelFile = new TempFile())
			{
				var convertedConfig = BuildConvertedHomographNumberNodes();
				convertedConfig.FilePath = convertedModelFile.Path;
				var defaultConfig = BuildCurrentDefaultHomographNumberNodes();

				_migrator.CopyNewDefaultsIntoConvertedModel(convertedConfig, defaultConfig);
				convertedConfig.Save();
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(mainEntriesPath + oldHomographPath);
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(mainEntriesPath + newHomographPath, 1);
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(subentriesPath + oldHomographPath);
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(subentriesPath + newHomographPath, 1);
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(minorCfEntriesPath + oldHomographPath);
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(minorCfEntriesPath + newHomographPath, 1);
				AssertThatXmlIn.File(convertedModelFile.Path).HasNoMatchForXpath(minorVarEntriesPath + oldHomographPath);
				AssertThatXmlIn.File(convertedModelFile.Path).HasSpecifiedNumberOfMatchesForXpath(minorVarEntriesPath + newHomographPath, 1);
			}
		}

		///<summary />
		[Test]
		public void CopyNewDefaultsIntoConvertedModel_CopyNewDefaultsThrowsWhenLabelsAreMismatched()
		{
			var convertedNode = new ConfigurableDictionaryNode
			{
				Label = "Miss",
				FieldDescription = "LexEntry"
			};
			var defaultNode = new ConfigurableDictionaryNode
			{
				Label = "Match",
				FieldDescription = "LexEntry"
			};
			CssGeneratorTests.PopulateFieldsForTesting(convertedNode);
			CssGeneratorTests.PopulateFieldsForTesting(defaultNode);

			var convertedConfig = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedNode },
				Version = PreHistoricMigrator.VersionPre83
			};
			var defaultConfig = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { defaultNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};

			Assert.Throws<ArgumentException>(() => _migrator.CopyNewDefaultsIntoConvertedModel(convertedConfig, defaultConfig));
		}

		///<summary />
		[Test]
		public void ConfigsNeedMigratingFromPre83_ReturnsFalseIfNewReversalConfigsExist()
		{
			var newRevIdxConfigLoc = Path.Combine(LcmFileHelper.GetConfigSettingsDir(Path.GetDirectoryName(Cache.ProjectId.Path)), DictionaryConfigurationServices.ReversalIndexConfigurationDirectoryName);
			try
			{
				Directory.CreateDirectory(newRevIdxConfigLoc);
				File.AppendAllText(Path.Combine(newRevIdxConfigLoc, "SomeConfig" + LanguageExplorerConstants.DictionaryConfigurationFileExtension), "Foo");
				Assert.That(!_migrator.ConfigsNeedMigratingFromPre83(), "If current configs exist no migration should be needed."); // SUT
			}
			finally
			{
				RobustIO.DeleteDirectoryAndContents(newRevIdxConfigLoc);
			}
		}

		///<summary />
		[Test]
		public void ConfigsNeedMigratingFromPre83_ReturnsFalseIfNewDictionaryConfigsExist()
		{
			var newDictConfigLoc = Path.Combine(LcmFileHelper.GetConfigSettingsDir(Path.GetDirectoryName(Cache.ProjectId.Path)), DictionaryConfigurationServices.DictionaryConfigurationDirectoryName);
			try
			{
				Directory.CreateDirectory(newDictConfigLoc);
				File.AppendAllText(Path.Combine(newDictConfigLoc, "SomeConfig" + LanguageExplorerConstants.DictionaryConfigurationFileExtension), "Foo");
				Assert.That(!_migrator.ConfigsNeedMigratingFromPre83(), "If current configs exist no migration should be needed."); // SUT
			}
			finally
			{
				RobustIO.DeleteDirectoryAndContents(newDictConfigLoc);
			}
		}

		///<summary />
		[Test]
		public void ConfigsNeedMigratingFromPre83_ReturnsFalseIfNoNewConfigsAndNoOldConfigs()
		{
			var newDictConfigLoc = Path.Combine(LcmFileHelper.GetConfigSettingsDir(Path.GetDirectoryName(Cache.ProjectId.Path)), DictionaryConfigurationServices.DictionaryConfigurationDirectoryName);
			try
			{
				Directory.CreateDirectory(newDictConfigLoc);
				Directory.EnumerateFiles(newDictConfigLoc).ForEach(File.Delete);
				Assert.That(!_migrator.ConfigsNeedMigratingFromPre83(), "With no new or old configs no migration should be needed."); // SUT
			}
			finally
			{
				RobustIO.DeleteDirectoryAndContents(newDictConfigLoc);
			}
		}

		///<summary />
		[Test]
		public void ConfigsNeedMigratingFromPre83_ReturnsTrueIfNoNewConfigsAndOneOldConfig()
		{
			var configSettingsDir = LcmFileHelper.GetConfigSettingsDir(Path.GetDirectoryName(Cache.ProjectId.Path));
			var newDictConfigLoc = Path.Combine(configSettingsDir, "Dictionary");
			try
			{
				Directory.CreateDirectory(newDictConfigLoc);
				Directory.EnumerateFiles(newDictConfigLoc).ForEach(File.Delete);
				var tempFwLayoutPath = Path.Combine(configSettingsDir, "SomeConfig.fwlayout");
				using (TempFile.WithFilename(tempFwLayoutPath))
				{
					File.AppendAllText(tempFwLayoutPath, "LayoutFoo");
					Assert.That(_migrator.ConfigsNeedMigratingFromPre83(), "There is an old config, a migration is needed."); // SUT
				}
			}
			finally
			{
				RobustIO.DeleteDirectoryAndContents(configSettingsDir);
			}
		}

		/// <summary>
		/// Check that an old configuration node migrates properly even if the label has been changed
		/// from "Type" to "Variant Type".  (See https://jira.sil.org/browse/LT-16896.)
		/// </summary>
		[Test]
		public void ConfigsMigrateModifiedLabelOkay()
		{
			var oldTypeNode = new LayoutTreeNode
			{
				After = string.Empty,
				Before = " ",
				Between = ", ",
				ClassName = "LexEntryRef",
				ContentVisible = true,
				Label = "Type"
			};
			var oldVariantFormNode = new LayoutTreeNode
			{
				After = string.Empty,
				Before = " ",
				Between = ", ",
				ClassName = "LexEntry",
				ContentVisible = true,
				Label = "Variant Form"
			};
			var oldCommentNode = new LayoutTreeNode
			{
				After = string.Empty,
				Before = " ",
				Between = " ",
				ClassName = "LexEntryRef",
				ContentVisible = false,
				Label = "Comment",
				WsLabel = "analysis",
				WsType = "analysis"
			};
			var oldVariantsNode = new LayoutTreeNode
			{
				After = ")",
				Before = " (",
				Between = "; ",
				ClassName = "LexEntry",
				ContentVisible = true,
				Label = "Variants (of Entry)",
			};
			oldVariantsNode.Nodes.Add(oldTypeNode);
			oldVariantsNode.Nodes.Add(oldVariantFormNode);
			oldVariantsNode.Nodes.Add(oldCommentNode);
			var oldRefSensesNode = new LayoutTreeNode
			{
				After = string.Empty,
				Before = " ",
				Between = "; ",
				ClassName = "ReversalIndexEntry",
				ContentVisible = true,
				Label = "Referenced Senses",
			};
			oldRefSensesNode.Nodes.Add(oldVariantsNode);
			var oldReversalEntryNode = new LayoutTreeNode
			{
				ClassName = "ReversalIndexEntry",
				ContentVisible = false,
				Label = "Reversal Entry",
			};
			oldReversalEntryNode.Nodes.Add(oldRefSensesNode);

			var convertedTopNode = _migrator.ConvertLayoutTreeNodeToConfigNode(oldReversalEntryNode);
			Assert.AreEqual("Reversal Entry", convertedTopNode.Label, "Initial conversion should copy the Label attribute verbatim.");
			Assert.AreEqual(1, convertedTopNode.Children.Count, "Children nodes should be converted");
			Assert.AreEqual(1, convertedTopNode.Children[0].Children.Count, "Grandchildren nodes should be converted");
			Assert.AreEqual(3, convertedTopNode.Children[0].Children[0].Children.Count, "Greatgrandchildren should be converted");
			var convertedTypeNode = convertedTopNode.Children[0].Children[0].Children[0];
			Assert.AreEqual("Type", convertedTypeNode.Label, "Nodes are converted in order");
			Assert.IsNull(convertedTypeNode.FieldDescription, "Initial conversion should not set FieldDescription for the Type node");
			var convertedCommentNode = convertedTopNode.Children[0].Children[0].Children[2];
			Assert.AreEqual("Comment", convertedCommentNode.Label, "Third child converted in order okay");
			Assert.IsNull(convertedCommentNode.FieldDescription, "Initial conversion should not set FieldDescription for the Comment node");

			var convertedModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { convertedTopNode },
				Label = "Test",
				Version = PreHistoricMigrator.VersionPre83,
				AllPublications = true
			};
			DictionaryConfigurationModel.SpecifyParentsAndReferences(convertedModel.Parts);

			var newTypeNode = new ConfigurableDictionaryNode
			{
				After = " ",
				Between = ", ",
				Label = "Variant Type",
				FieldDescription = "OwningEntry",
				IsEnabled = true
			};
			var newFormNode = new ConfigurableDictionaryNode
			{
				Between = ", ",
				Label = "Variant Form",
				FieldDescription = "VariantEntryTypesRS",
				SubField = "MLHeadWord",
				DictionaryNodeOptions = new DictionaryNodeWritingSystemOptions(),
				IsEnabled = true
			};
			((DictionaryNodeWritingSystemOptions)newFormNode.DictionaryNodeOptions).WsType = WritingSystemType.Vernacular;
			((DictionaryNodeWritingSystemOptions)newFormNode.DictionaryNodeOptions).DisplayWritingSystemAbbreviations = false;
			((DictionaryNodeWritingSystemOptions)newFormNode.DictionaryNodeOptions).Options = new List<DictionaryNodeOption>
			{
				new DictionaryNodeOption { Id = "vernacular", IsEnabled = true }
			};
			var newCommentNode = new ConfigurableDictionaryNode
			{
				After = " ",
				Between = " ",
				Label = "Comment",
				FieldDescription = "Summary",
				DictionaryNodeOptions = new DictionaryNodeWritingSystemOptions(),
				IsEnabled = false
			};
			((DictionaryNodeWritingSystemOptions)newCommentNode.DictionaryNodeOptions).WsType = WritingSystemType.Analysis;
			((DictionaryNodeWritingSystemOptions)newCommentNode.DictionaryNodeOptions).DisplayWritingSystemAbbreviations = false;
			((DictionaryNodeWritingSystemOptions)newCommentNode.DictionaryNodeOptions).Options = new List<DictionaryNodeOption>
			{
				new DictionaryNodeOption { Id = "analysis", IsEnabled = true }
			};
			var newVariantsNode = new ConfigurableDictionaryNode
			{
				After = ") ",
				Before = "(",
				Between = "; ",
				Label = "Variants (of Entry)",
				FieldDescription = "Owner",
				SubField = "VariantFormEntryBackRefs",
				DictionaryNodeOptions = new DictionaryNodeListOptions(),
				Children = new List<ConfigurableDictionaryNode> { newTypeNode, newFormNode, newCommentNode },
				IsEnabled = true
			};
			((DictionaryNodeListOptions)newVariantsNode.DictionaryNodeOptions).ListId = ListIds.Variant;
			((DictionaryNodeListOptions)newVariantsNode.DictionaryNodeOptions).Options = new List<DictionaryNodeOption> {
				new DictionaryNodeOption { Id = "b0000000-c40e-433e-80b5-31da08771344", IsEnabled = true },
				new DictionaryNodeOption { Id = "024b62c9-93b3-41a0-ab19-587a0030219a", IsEnabled = true },
				new DictionaryNodeOption { Id = "4343b1ef-b54f-4fa4-9998-271319a6d74c", IsEnabled = true },
				new DictionaryNodeOption { Id = "01d4fbc1-3b0c-4f52-9163-7ab0d4f4711c", IsEnabled = true },
				new DictionaryNodeOption { Id = "837ebe72-8c1d-4864-95d9-fa313c499d78", IsEnabled = true },
				new DictionaryNodeOption { Id = "a32f1d1c-4832-46a2-9732-c2276d6547e8", IsEnabled = true },
				new DictionaryNodeOption { Id = "0c4663b3-4d9a-47af-b9a1-c8565d8112ed", IsEnabled = true }
			};
			var newRefSensesNode = new ConfigurableDictionaryNode
			{
				After = " ",
				Between = "; ",
				Label = "Referenced Senses",
				FieldDescription = "ReferringSenses",
				Children = new List<ConfigurableDictionaryNode> { newVariantsNode },
				IsEnabled = true
			};
			var newReversalEntryNode = new ConfigurableDictionaryNode
			{
				Label = "Reversal Entry",
				FieldDescription = "ReversalIndexEntry",
				Children = new List<ConfigurableDictionaryNode> { newRefSensesNode },
				IsEnabled = true,
				StyleType = StyleTypes.Paragraph,
				Style = "Reversal-Normal",
				CSSClassNameOverride = "reversalindexentry"
			};
			var currentDefaultModel = new DictionaryConfigurationModel
			{
				Parts = new List<ConfigurableDictionaryNode> { newReversalEntryNode },
				Version = PreHistoricMigrator.VersionAlpha1
			};
			DictionaryConfigurationModel.SpecifyParentsAndReferences(currentDefaultModel.Parts);

			_migrator.CopyNewDefaultsIntoConvertedModel(convertedModel, currentDefaultModel);
			Assert.AreEqual("ReversalIndexEntry", convertedTopNode.FieldDescription, "Converted top node should have FieldDescription=ReversalIndexEntry");
			Assert.AreEqual("reversalindexentry", convertedTopNode.CSSClassNameOverride, "Converted top node should have CSSClassNameOverride=reversalindexentry");
			Assert.AreEqual(StyleTypes.Paragraph, convertedTopNode.StyleType, "Converted top node should have StyleType=Paragraph");
			Assert.AreEqual("Reversal-Normal", convertedTopNode.Style, "Converted top node should have Style=Reversal-Normal");
			// Prior to fixing https://jira.sil.org/browse/LT-16896, convertedTypeNode.FieldDescription was set to "Type".
			Assert.AreEqual("OwningEntry", convertedTypeNode.FieldDescription, "Converted type node should have FieldDescription=OwningEntry");
			Assert.AreEqual("Summary", convertedCommentNode.FieldDescription, "Converted comment node should have FieldDescription=Summary");
		}

		[Test]
		public void TestMigrateCustomFieldNode()
		{
			var xdoc0 = XDocument.Parse("<part ref=\"ScientificName\" label=\"Scientific Name\" before=\" \" after=\"\" visibility=\"ifdata\" css=\"scientific-name\"/>");
			var oldTypeNode0 = new LayoutTreeNode(xdoc0.Root, _migrator, "LexSense");
			var newTypeNode0 = _migrator.ConvertLayoutTreeNodeToConfigNode(oldTypeNode0);
			Assert.IsFalse(newTypeNode0.IsCustomField, "A normal field should not be marked as custom after conversion");
			Assert.IsTrue(newTypeNode0.IsEnabled, "A normal field should be enabled properly.");
			Assert.AreEqual("Scientific Name", newTypeNode0.Label, "A normal field copies its label properly during conversion");
			var xdoc1 = XDocument.Parse("<part ref=\"$child\" label=\"Single Sense\" before=\" Custom Field:( \" after=\" )\" visibility=\"ifdata\" originalLabel=\"Single Sense\"><string field=\"Single Sense\" class=\"LexSense\"/></part>");
			var oldTypeNode1 = new LayoutTreeNode(xdoc1.Root, _migrator, "LexSense");
			var newTypeNode1 = _migrator.ConvertLayoutTreeNodeToConfigNode(oldTypeNode1);
			Assert.IsTrue(newTypeNode1.IsCustomField, "A custom field should be marked as such after conversion");
			Assert.IsTrue(newTypeNode1.IsEnabled, "A custom field should be enabled properly.");
			Assert.AreEqual("Single Sense", newTypeNode1.Label, "A custom field copies its label properly during conversion");
		}

		private void DeleteStyleSheet(string styleName)
		{
			var style = _lcmStyleSheet.FindStyle(styleName);
			if (style != null)
			{
				_lcmStyleSheet.Delete(style.Hvo);
			}
		}

		private sealed class MockLayoutTreeNode : LayoutTreeNode
		{
			public string m_partName;

			public override string PartName => m_partName;
		}
	}
}
