﻿// Copyright (c) 2014 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using NUnit.Framework;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.FDO;
using XCore;

namespace SIL.FieldWorks.XWorks
{
	[TestFixture]
	class DictionaryConfigurationListenerTests : XWorksAppTestBase, IDisposable
	{
		#region Context
		private Mediator m_mediator;

		[TestFixtureSetUp]
		public new void FixtureSetup()
		{
			// We won't call Init since XWorksAppTestBase's TestFixtureSetUp calls it.
		}
		#endregion

		[Test]
		public void GetProjectConfigurationDirectory_ReportsCorrectlyForDictionaryAndReversal()
		{
			var mediator = m_mediator;
			{
				string projectConfigDir;

				mediator.PropertyTable.SetProperty("currentContentControl", "lexiconEdit");
				projectConfigDir = Path.Combine(FdoFileHelper.GetConfigSettingsDir(Cache.ProjectId.ProjectFolder), "Dictionary");
				Assert.That(DictionaryConfigurationListener.GetProjectConfigurationDirectory(mediator), Is.EqualTo(projectConfigDir), "did not return expected directory");

				mediator.PropertyTable.SetProperty("currentContentControl", "reversalToolEditComplete");
				projectConfigDir = Path.Combine(FdoFileHelper.GetConfigSettingsDir(Cache.ProjectId.ProjectFolder), "ReversalIndex");
				Assert.That(DictionaryConfigurationListener.GetProjectConfigurationDirectory(mediator), Is.EqualTo(projectConfigDir), "did not return expected directory");

				mediator.PropertyTable.SetProperty("currentContentControl", "somethingElse");
				Assert.IsNull(DictionaryConfigurationListener.GetProjectConfigurationDirectory(mediator), "Other areas should cause null return");
			}
		}

		[Test]
		public void GetDefaultConfigurationDirectory_ReportsCorrectlyForDictionaryAndReversal()
		{
			var mediator = m_mediator;
			{
				string configDir;

				mediator.PropertyTable.SetProperty("currentContentControl", "lexiconEdit");
				configDir = Path.Combine(FwDirectoryFinder.DefaultConfigurations, "Dictionary");
				Assert.That(DictionaryConfigurationListener.GetDefaultConfigurationDirectory(mediator), Is.EqualTo(configDir), "did not return expected directory");

				mediator.PropertyTable.SetProperty("currentContentControl", "reversalToolEditComplete");
				configDir = Path.Combine(FwDirectoryFinder.DefaultConfigurations, "ReversalIndex");
				Assert.That(DictionaryConfigurationListener.GetDefaultConfigurationDirectory(mediator), Is.EqualTo(configDir), "did not return expected directory");

				mediator.PropertyTable.SetProperty("currentContentControl", "somethingElse");
				Assert.IsNull(DictionaryConfigurationListener.GetDefaultConfigurationDirectory(mediator), "Other areas should cause null return");
			}
		}

		#region Context
		protected override void Init()
		{
			m_application = new MockFwXApp(new MockFwManager { Cache = Cache }, null, null);
			m_configFilePath = Path.Combine(FwDirectoryFinder.CodeDirectory, m_application.DefaultConfigurationPathname);
			m_window = new MockFwXWindow(m_application, m_configFilePath);
			((MockFwXWindow)m_window).Init(Cache); // initializes Mediator values
			m_mediator = m_window.Mediator;
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
			m_mediator.Dispose();
		}
		#endregion
	}
}
