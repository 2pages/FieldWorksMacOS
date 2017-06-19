﻿// Copyright (c) 2014 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using NUnit.Framework;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.FDOTests;
using SIL.FieldWorks.FDO.Infrastructure;

namespace SIL.FieldWorks.XWorks
{
#if RANDYTODO // Some of this can be salvaged, but not the part where it loads the main xml config files.
	[TestFixture]
	class DictionaryConfigurationListenerTests : XWorksAppTestBase, IDisposable
	{
		#region Context
		private IPropertyTable m_propertyTable;

		[TestFixtureSetUp]
		public new void FixtureSetup()
		{
			// We won't call Init since XWorksAppTestBase's TestFixtureSetUp calls it.
		}
		#endregion

		[Test]
		public void GetProjectConfigurationDirectory_ReportsCorrectlyForDictionaryAndReversal()
		{
			{
				string projectConfigDir;

				m_propertyTable.SetProperty("toolChoice", "lexiconEdit", true);
				projectConfigDir = Path.Combine(FdoFileHelper.GetConfigSettingsDir(Cache.ProjectId.ProjectFolder), "Dictionary");
			Assert.That(DictionaryConfigurationListener.GetProjectConfigurationDirectory(m_propertyTable), Is.EqualTo(projectConfigDir), "did not return expected directory");

				m_propertyTable.SetProperty("toolChoice", "reversalEditComplete", true);
			projectConfigDir = Path.Combine(FdoFileHelper.GetConfigSettingsDir(Cache.ProjectId.ProjectFolder), "ReversalIndex");
			Assert.That(DictionaryConfigurationListener.GetProjectConfigurationDirectory(m_propertyTable), Is.EqualTo(projectConfigDir), "did not return expected directory");

				m_propertyTable.SetProperty("toolChoice", "somethingElse", true);
			Assert.IsNull(DictionaryConfigurationListener.GetProjectConfigurationDirectory(m_propertyTable), "Other areas should cause null return");
		}
		}

		[Test]
		public void GetDictionaryConfigurationBaseType_ReportsCorrectlyForDictionaryAndReversal()
		{
			m_propertyTable.SetProperty("toolChoice", "lexiconEdit", true);
			Assert.AreEqual("Dictionary", DictionaryConfigurationListener.GetDictionaryConfigurationBaseType(m_propertyTable), "did not return expected type");
			m_propertyTable.SetProperty("toolChoice", "lexiconBrowse", true);
			Assert.AreEqual("Dictionary", DictionaryConfigurationListener.GetDictionaryConfigurationBaseType(m_propertyTable), "did not return expected type");
			m_propertyTable.SetProperty("toolChoice", "lexiconDictionary", true);
			Assert.AreEqual("Dictionary", DictionaryConfigurationListener.GetDictionaryConfigurationBaseType(m_propertyTable), "did not return expected type");

			m_propertyTable.SetProperty("toolChoice", "reversalToolEditComplete", true);
			Assert.AreEqual("Reversal Index", DictionaryConfigurationListener.GetDictionaryConfigurationBaseType(m_propertyTable), "did not return expected type");
			m_propertyTable.SetProperty("toolChoice", "reversalToolBulkEditReversalEntries", true);
			Assert.AreEqual("Reversal Index", DictionaryConfigurationListener.GetDictionaryConfigurationBaseType(m_propertyTable), "did not return expected type");

			m_propertyTable.SetProperty("toolChoice", "somethingElse", true);
			Assert.IsNull(DictionaryConfigurationListener.GetDictionaryConfigurationBaseType(m_propertyTable), "Other areas should return null");
		}

		[Test]
		public void GetDefaultConfigurationDirectory_ReportsCorrectlyForDictionaryAndReversal()
		{
			string configDir;

			m_propertyTable.SetProperty("toolChoice", "lexiconEdit", true);
			configDir = Path.Combine(FwDirectoryFinder.DefaultConfigurations, "Dictionary");
			Assert.That(DictionaryConfigurationListener.GetDefaultConfigurationDirectory(m_propertyTable), Is.EqualTo(configDir), "did not return expected directory");

			m_propertyTable.SetProperty("toolChoice", "reversalEditComplete", true);
			configDir = Path.Combine(FwDirectoryFinder.DefaultConfigurations, "ReversalIndex");
			Assert.That(DictionaryConfigurationListener.GetDefaultConfigurationDirectory(m_propertyTable), Is.EqualTo(configDir), "did not return expected directory");

			m_propertyTable.SetProperty("toolChoice", "somethingElse", true);
			Assert.IsNull(DictionaryConfigurationListener.GetDefaultConfigurationDirectory(m_propertyTable), "Other areas should cause null return");
		}

		#region Context
		protected override void Init()
		{
			BootstrapSystem(new TestProjectId(FDOBackendProviderType.kMemoryOnly, "TestProject"),
				BackendBulkLoadDomain.Lexicon, new FdoSettings());
			m_application = new MockFwXApp(new MockFwManager { Cache = Cache }, null, null);
			m_configFilePath = Path.Combine(FwDirectoryFinder.CodeDirectory, m_application.DefaultConfigurationPathname);
			m_window = new MockFwXWindow(m_application, m_configFilePath);
			((MockFwXWindow)m_window).Init(Cache); // initializes Mediator values
			m_propertyTable = m_window.PropTable;
		}

		~DictionaryConfigurationListenerTests()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			System.Diagnostics.Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			if(m_application != null)
				m_application.Dispose();
			if(m_window != null)
				m_window.Dispose();
			if(m_propertyTable != null)
				m_propertyTable.Dispose();
		}
		#endregion
	}
#endif
}
