﻿// Copyright (c) 2014-2016 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NUnit.Framework;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.FDO.FDOTests;
using SIL.Utils;
using FileUtils = SIL.Utils.FileUtils;
// ReSharper disable InconsistentNaming

namespace SIL.FieldWorks.XWorks
{
	[TestFixture]
	public class DictionaryConfigurationManagerControllerTests : MemoryOnlyBackendProviderTestBase
	{
		private DictionaryConfigurationManagerController _controller;
		private List<DictionaryConfigurationModel> _configurations;

		private readonly string _projectConfigPath = Path.GetTempPath();
		private readonly string _defaultConfigPath = Path.Combine(FwDirectoryFinder.DefaultConfigurations, "Dictionary");

		[TestFixtureSetUp]
		public override void FixtureSetup()
		{
			base.FixtureSetup();
			FileUtils.Manager.SetFileAdapter(new MockFileOS());

			FileUtils.EnsureDirectoryExists(_defaultConfigPath);
		}

		[TestFixtureTearDown]
		public override void FixtureTeardown()
		{
			FileUtils.Manager.Reset();
			base.FixtureTeardown();
		}

		[SetUp]
		public void Setup()
		{
			_configurations = new List<DictionaryConfigurationModel>
			{
				new DictionaryConfigurationModel { Label = "configuration0", Publications = new List<string>() },
				new DictionaryConfigurationModel { Label = "configuration1", Publications = new List<string>() }
			};

			var publications = new List<string>
			{
				"publicationA",
				"publicationB"
			};

			_controller = new DictionaryConfigurationManagerController(Cache, _configurations, publications, _projectConfigPath, _defaultConfigPath);
		}

		[TearDown]
		public void TearDown()
		{
		}

		[Test]
		public void GetPublication_UsesAssociations()
		{
			_configurations[0].Publications.Add("publicationA");
			// SUT
			var pubs = _controller.GetPublications(_configurations[0]);
			Assert.That(pubs, Contains.Item("publicationA"));
			Assert.That(pubs, Has.Count.EqualTo(1));

			// SUT
			Assert.Throws<ArgumentNullException>(() => _controller.GetPublications(null));
		}

		[Test]
		public void AssociatePublication_BadArgsTests()
		{
			Assert.Throws<ArgumentNullException>(() => _controller.AssociatePublication(null, null), "No configuration to associate with");
			Assert.Throws<ArgumentNullException>(() => _controller.AssociatePublication("publicationA", null), "No configuration to associate with");
			Assert.Throws<ArgumentNullException>(() => _controller.AssociatePublication(null, _configurations[0]), "Don't allow trying to add null");

			Assert.Throws<ArgumentOutOfRangeException>(() => _controller.AssociatePublication("unknown publication", _configurations[0]), "Don't associate with an invalid/unknown publication");
		}

		[Test]
		public void AssociatesPublication()
		{
			// SUT
			_controller.AssociatePublication("publicationA", _configurations[0]);
			Assert.That(_configurations[0].Publications, Contains.Item("publicationA"), "failed to associate");
			Assert.That(_configurations[0].Publications, Is.Not.Contains("publicationB"), "should not have associated with publicationB");

			// SUT
			_controller.AssociatePublication("publicationA", _configurations[1]);
			_controller.AssociatePublication("publicationB", _configurations[1]);
			Assert.That(_configurations[1].Publications, Contains.Item("publicationA"), "failed to associate");
			Assert.That(_configurations[1].Publications, Contains.Item("publicationB"), "failed to associate");
			Assert.That(_configurations[0].Publications, Is.Not.Contains("publicationB"),
				"should not have associated configuration0 with publicationB");
		}

		[Test]
		public void AssociatesPublicationOnlyOnce()
		{
			for (int i = 0; i < 3; i++)
			{
				// SUT
				_controller.AssociatePublication("publicationA", _configurations[0]);
			}
			Assert.AreEqual(1, _configurations[0].Publications.Count(pub => pub.Equals("publicationA")), "associated too many times");
		}

		[Test]
		public void DisassociatePublication_BadArgsTests()
		{
			Assert.Throws<ArgumentNullException>(() => _controller.DisassociatePublication(null, null), "No configuration to disassociate. No publication to disassociate from.");
			Assert.Throws<ArgumentNullException>(() => _controller.DisassociatePublication("publicationA", null), "No configuration");
			Assert.Throws<ArgumentNullException>(() => _controller.DisassociatePublication(null, _configurations[0]), "No publication");

			Assert.Throws<ArgumentOutOfRangeException>(() => _controller.DisassociatePublication("unknown publication", _configurations[0]), "Don't try to operate using an invalid/unknown publication");
		}

		[Test]
		public void DisassociatesPublication()
		{
			_controller.AssociatePublication("publicationA", _configurations[1]);
			_controller.AssociatePublication("publicationB", _configurations[1]);
			// SUT
			_controller.DisassociatePublication("publicationA", _configurations[1]);

			Assert.That(_configurations[1].Publications, Contains.Item("publicationB"), "Should not have disassociated unrelated publication");
			Assert.That(_configurations[1].Publications, Is.Not.Contains("publicationA"), "failed to disassociate");
		}

		[Test]
		public void Rename_RevertsOnCancel()
		{
			var selectedConfig = _configurations[0];
			var listViewItem = new ListViewItem { Tag = selectedConfig };
			var oldLabel = selectedConfig.Label;

			// SUT
			Assert.True(_controller.RenameConfiguration(listViewItem, new LabelEditEventArgs(0, null)), "'Cancel' should complete successfully");

			Assert.AreEqual(oldLabel, selectedConfig.Label, "Configuration should not have been renamed");
			Assert.AreEqual(oldLabel, listViewItem.Text, "ListViewItem Text should have been reset");
			Assert.False(_controller.IsDirty, "No changes; should not be dirty");
		}

		[Test]
		public void Rename_PreventsDuplicate()
		{
			var dupLabelArgs = new LabelEditEventArgs(0, "DuplicateLabel");
			var configA = _configurations[0];
			var configB = new DictionaryConfigurationModel { Label = "configuration2", Publications = new List<string>() };
			_controller.RenameConfiguration(new ListViewItem { Tag = configA }, dupLabelArgs);
			_configurations.Insert(0, configB);

			// SUT
			Assert.False(_controller.RenameConfiguration(new ListViewItem { Tag = configB }, dupLabelArgs), "Duplicate should return 'incomplete'");

			Assert.AreEqual(dupLabelArgs.Label, configA.Label, "The first config should have been given the specified name");
			Assert.AreNotEqual(dupLabelArgs.Label, configB.Label, "The second config should not have been given the same name");
		}

		[Test]
		public void Rename_RenamesConfigAndFile()
		{
			const string newLabel = "NewLabel";
			var selectedConfig = _configurations[0];
			selectedConfig.FilePath = null;
			// SUT
			Assert.True(_controller.RenameConfiguration(new ListViewItem { Tag = selectedConfig }, new LabelEditEventArgs(0, newLabel)),
				"Renaming a config to a unique name should complete successfully");
			Assert.AreEqual(newLabel, selectedConfig.Label, "The configuration should have been renamed");
			Assert.AreEqual(_controller.FormatFilePath(newLabel), selectedConfig.FilePath, "The FilePath should have been generated");
			Assert.True(_controller.IsDirty, "Made changes; should be dirty");
		}

		[Test]
		public void GenerateFilePath()
		{
			var configToRename = new DictionaryConfigurationModel
			{
				Label = "configuration3", FilePath = null, Publications = new List<string>()
			};
			var conflictingConfigs = new List<DictionaryConfigurationModel>
			{
				new DictionaryConfigurationModel
				{
					Label = "conflicting file 3-0", FilePath = _controller.FormatFilePath("configuration3"), Publications = new List<string>()
				},
				new DictionaryConfigurationModel
				{
					Label = "conflicting file 3-1", FilePath = _controller.FormatFilePath("configuration3_1"), Publications = new List<string>()
				},
				new DictionaryConfigurationModel
				{
					Label = "conflicting file 3-2--in another directory to prove we can't accidentally mask unchanged default configurations",
					FilePath = Path.Combine(Path.Combine(_projectConfigPath, "subdir"),
						"configuration3_2" + DictionaryConfigurationModel.FileExtension),
					Publications = new List<string>()
				}
			};
			_configurations.Add(configToRename);
			_configurations.AddRange(conflictingConfigs);

			// SUT
			_controller.GenerateFilePath(configToRename);

			var newFilePath = configToRename.FilePath;
			StringAssert.StartsWith(_projectConfigPath, newFilePath);
			StringAssert.EndsWith(DictionaryConfigurationModel.FileExtension, newFilePath);
			Assert.AreEqual(_controller.FormatFilePath("configuration3_3"), configToRename.FilePath, "The file path should be based on the label");
			foreach (var config in conflictingConfigs)
			{
				Assert.AreNotEqual(Path.GetFileName(newFilePath), Path.GetFileName(config.FilePath), "File name should be unique");
			}
		}

		[Test]
		public void FormatFilePath()
		{
			var formattedFilePath = _controller.FormatFilePath("\nFile\\Name/With\"Chars<?>"); // SUT
			StringAssert.StartsWith(_projectConfigPath, formattedFilePath);
			StringAssert.EndsWith(DictionaryConfigurationModel.FileExtension, formattedFilePath);
			StringAssert.DoesNotContain("\n", formattedFilePath);
			StringAssert.DoesNotContain("\\", Path.GetFileName(formattedFilePath));
			StringAssert.DoesNotContain("/", Path.GetFileName(formattedFilePath));
			StringAssert.DoesNotContain("\"", formattedFilePath);
			StringAssert.DoesNotContain("<", formattedFilePath);
			StringAssert.DoesNotContain("?", formattedFilePath);
			StringAssert.DoesNotContain(">", formattedFilePath);
			StringAssert.Contains("File", formattedFilePath);
			StringAssert.Contains("Name", formattedFilePath);
			StringAssert.Contains("With", formattedFilePath);
			StringAssert.Contains("Chars", formattedFilePath);
		}

		[Test]
		public void CopyConfiguration()
		{
			// insert a series of "copied" configs
			var pubs = new List<string> { "publicationA", "publicationB" };
			var extantConfigs = new List<DictionaryConfigurationModel>
			{
				new DictionaryConfigurationModel { Label = "configuration4", Publications = pubs },
				new DictionaryConfigurationModel { Label = "Copy of configuration4", Publications = new List<string>() },
				new DictionaryConfigurationModel { Label = "Copy of configuration4 (1)", Publications = new List<string>() },
				new DictionaryConfigurationModel { Label = "Copy of configuration4 (2)", Publications = new List<string>() }
			};
			_configurations.InsertRange(0, extantConfigs);

			// SUT
			var newConfig = _controller.CopyConfiguration(extantConfigs[0]);

			Assert.AreEqual("Copy of configuration4 (3)", newConfig.Label, "The new label should be based on the original");
			Assert.Contains(newConfig, _configurations, "The new config should have been added to the list");
			Assert.AreEqual(1, _configurations.Count(conf => newConfig.Label.Equals(conf.Label)), "The label should be unique");

			Assert.AreEqual(pubs.Count, newConfig.Publications.Count, "Publications were not copied");
			for (int i = 0; i < pubs.Count; i++)
			{
				Assert.AreEqual(pubs[i], newConfig.Publications[i], "Publications were not copied");
			}
			Assert.IsNull(newConfig.FilePath, "Path should be null to signify that it should be generated on rename");
			Assert.True(_controller.IsDirty, "Made changes; should be dirty");
		}

		[Test]
		public void DeleteConfigurationRemovesFromList()
		{
			var configurationToDelete = _configurations[0];
			// SUT
			_controller.DeleteConfiguration(configurationToDelete);

			Assert.That(_configurations, Is.Not.Contains(configurationToDelete), "Should have removed configuration from list of configurations");
			Assert.That(_controller.IsDirty, "made changes; should be dirty");
		}

		[Test]
		public void DeleteConfigurationRemovesFromDisk()
		{
			var configurationToDelete = _configurations[0];

			_controller.GenerateFilePath(configurationToDelete);
			var pathToConfiguration = configurationToDelete.FilePath;
			FileUtils.WriteStringtoFile(pathToConfiguration, "file contents", Encoding.UTF8);
			Assert.That(FileUtils.FileExists(pathToConfiguration), "Unit test not set up right");

			// SUT
			_controller.DeleteConfiguration(configurationToDelete);

			Assert.That(!FileUtils.FileExists(pathToConfiguration), "File should have been deleted");
			Assert.That(_controller.IsDirty, "made changes; should be dirty");
		}

		[Test]
		public void DeleteConfigurationDoesNotCrashIfNullFilePath()
		{
			var configurationToDelete = _configurations[0];
			Assert.That(configurationToDelete.FilePath, Is.Null, "Unit test not testing what it used to. Perhaps the code is smarter now.");

			// SUT
			Assert.DoesNotThrow(()=> _controller.DeleteConfiguration(configurationToDelete), "Don't crash if the FilePath isn't set for some reason.");
			Assert.That(_controller.IsDirty, "made changes; should be dirty");
		}

		[Test]
		public void DeleteConfigurationCrashesOnNullArgument()
		{
			Assert.Throws<ArgumentNullException>(() => _controller.DeleteConfiguration(null), "Failed to throw");
		}

		[Test]
		public void DeleteConfigurationResetsForShippedDefaultRatherThanDelete()
		{
			var shippedRootDefaultConfigurationPath = Path.Combine(_defaultConfigPath, "Root" + DictionaryConfigurationModel.FileExtension);
			FileUtils.WriteStringtoFile(shippedRootDefaultConfigurationPath, "shipped root default configuration file contents", Encoding.UTF8);

			var configurationToDelete = _configurations[0];
			configurationToDelete.FilePath = Path.Combine("whateverdir", "Root" + DictionaryConfigurationModel.FileExtension);
			configurationToDelete.Label = "customizedLabel";

			var pathToConfiguration = configurationToDelete.FilePath;
			FileUtils.WriteStringtoFile(pathToConfiguration, "customized file contents", Encoding.UTF8);
			Assert.That(FileUtils.FileExists(pathToConfiguration), "Unit test not set up right");

			// SUT
			_controller.DeleteConfiguration(configurationToDelete);

			Assert.That(FileUtils.FileExists(pathToConfiguration), "File should still be there, not deleted.");
			Assert.That(configurationToDelete.Label, Is.EqualTo("Root-based (complex forms as subentries)"), "Did not seem to reset configuration to shipped defaults.");
			Assert.Contains(configurationToDelete, _configurations, "Should still have the configuration in the list of configurations");
			Assert.That(_controller.IsDirty, "Resetting is a change that is saved later; should be dirty");

			// Not asserting that the configurationToDelete.FilePath file contents are reset because that will happen later when it is saved.
		}

		[Test]
		public void KnowsWhenNotAShippedDefault()
		{
			var configuration = new DictionaryConfigurationModel
			{
				Label = "configuration",
				FilePath = Path.Combine("whateverdir", "somefile" + DictionaryConfigurationModel.FileExtension)
			};

			// SUT
			var claimsToBeDerived = _controller.IsConfigurationACustomizedOriginal(configuration);

			Assert.That(claimsToBeDerived, Is.False, "Should not have reported this as a shipped default configuration.");
		}

		[Test]
		public void NotAShippedDefaultIfNullFilePath()
		{
			var configuration = new DictionaryConfigurationModel
			{
				Label = "configuration",
				FilePath = null
			};

			// SUT
			var claimsToBeDerived = _controller.IsConfigurationACustomizedOriginal(configuration);

			Assert.That(claimsToBeDerived, Is.False, "Should not have reported this as a shipped default configuration.");
		}

		[Test]
		public void KnowsWhenIsAShippedDefault()
		{
			var configuration = new DictionaryConfigurationModel
			{
				Label = "configuration",
				FilePath = Path.Combine("whateverdir", "Root" + DictionaryConfigurationModel.FileExtension)
			};

			// SUT
			var claimsToBeDerived = _controller.IsConfigurationACustomizedOriginal(configuration);

			Assert.That(claimsToBeDerived, Is.True, "Should have reported this as a shipped default configuration.");
		}

		[Test]
		public void ReversalCopyIsNotACustomizedOriginal()
		{
			var configuration = new DictionaryConfigurationModel
			{
				Label = "English Copy",
				WritingSystem = "en",
				FilePath = Path.Combine("whateverdir", "Copy of English" + DictionaryConfigurationModel.FileExtension)
			};

			// SUT
			var claimsToBeDerived = _controller.IsConfigurationACustomizedOriginal(configuration);

			Assert.That(claimsToBeDerived, Is.False, "Copy of a reversal should not claim to be a customized original");
		}

		[Test]
		public void ReversalMatchingLanguageIsACustomizedOriginal()
		{
			var configuration = new DictionaryConfigurationModel
			{
				Label = "English",
				WritingSystem = "en",
				FilePath = Path.Combine("whateverdir", "English" + DictionaryConfigurationModel.FileExtension)
			};

			// SUT
			var claimsToBeDerived = _controller.IsConfigurationACustomizedOriginal(configuration);

			Assert.That(claimsToBeDerived, Is.True, "Should have reported this as a shipped default configuration.");
		}

		[Test]
		public void RenamedReversalMatchingLanguageIsACustomizedOriginal()
		{
			var configuration = new DictionaryConfigurationModel
			{
				Label = "Manglish",
				WritingSystem = "en",
				FilePath = Path.Combine("whateverdir", "English" + DictionaryConfigurationModel.FileExtension)
			};

			// SUT
			var claimsToBeDerived = _controller.IsConfigurationACustomizedOriginal(configuration);

			Assert.That(claimsToBeDerived, Is.True, "Should have reported this as a shipped default configuration.");
		}

		[Test]
		public void ReversalNotMatchingLanguageIsACustomizedOriginal()
		{
			var configuration = new DictionaryConfigurationModel
			{
				Label = "English (copy)",
				WritingSystem = "en",
				FilePath = Path.Combine("whateverdir", "English-Copy" + DictionaryConfigurationModel.FileExtension)
			};

			// SUT
			var claimsToBeDerived = _controller.IsConfigurationACustomizedOriginal(configuration);

			Assert.That(claimsToBeDerived, Is.False, "This is a copy and not a customized original and should have reported false.");
		}

		[Test]
		public void ReversalOfLanguageWithRegionIsACustomizedOriginal()
		{
			var configuration = new DictionaryConfigurationModel
			{
				Label = "German (Algeria)",
				WritingSystem = "de-DZ",
				FilePath = Path.Combine("whateverdir", "German (Algeria)" + DictionaryConfigurationModel.FileExtension)
			};

			// SUT
			var claimsToBeDerived = _controller.IsConfigurationACustomizedOriginal(configuration);

			Assert.That(claimsToBeDerived, Is.True, "Should have reported this as a shipped default configuration.");
		}

		[Test]
		public void ReversalOfInvalidLanguageIsNotACustomizedOriginal()
		{
			var configuration = new DictionaryConfigurationModel
			{
				Label = "English",
				WritingSystem = "enz1a",
				FilePath = Path.Combine("whateverdir", "enz1a" + DictionaryConfigurationModel.FileExtension)
			};

			// SUT
			var claimsToBeDerived = _controller.IsConfigurationACustomizedOriginal(configuration);

			Assert.That(claimsToBeDerived, Is.False, "Should have reported this as a shipped default configuration.");
		}
	}
}
