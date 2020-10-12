// Copyright (c) 2002-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.FwCoreDlgs;
using SIL.LCModel.Utils;

namespace LanguageExplorer
{
	/// <summary>
	/// Provides a means to store in and retrieve from the registry misc. fieldworks settings.
	/// </summary>
	internal sealed class FwRegistrySettings : IDisposable
	{
		#region Member Variables
		private readonly RegistryBoolSetting m_firstTimeAppHasBeenRun;
		private readonly RegistryBoolSetting m_showSideBar;
		private readonly RegistryBoolSetting m_showStatusBar;
		private readonly RegistryBoolSetting m_openLastEditedProject;
		private readonly RegistryIntSetting m_loadingProcessId;
		private readonly RegistryIntSetting m_numberOfLaunches;
		private readonly RegistryIntSetting m_numberOfSeriousCrashes;
		private readonly RegistryIntSetting m_numberOfAnnoyingCrashes;
		private readonly RegistryIntSetting m_totalAppRuntime;
		private readonly RegistryStringSetting m_appStartupTime;
		private readonly RegistryStringSetting m_latestProject;
		private readonly RegistryStringSetting m_latestServer;

		// These affect all FieldWorks apps.
		private static RegistryBoolSetting s_disableSplashScreen;
		// Data Notebook has the MeasurementUnits setting in the Data Notebook registry
		//  folder rather than in the folder for general FieldWorks settings.
		//  The MeasurementUnits setting should be set for all FieldWorks applications,
		//  not just in the individual applications.
		private static RegistryIntSetting s_measurementUnitSetting;
		#endregion

		#region Constructors
		static FwRegistrySettings()
		{
			Init();
		}

		/// <summary>
		/// Initialize static registry settings.
		/// NOTE: This should be called only by unit tests.
		/// </summary>
		internal static void Init()
		{
			if (s_measurementUnitSetting != null)
			{
				return;
			}
			// Data Notebook has the MeasurementUnits setting in the Data Notebook registry
			//  folder rather than in the folder for general FieldWorks settings.
			//  The MeasurementUnits setting should be set for all FieldWorks applications,
			//  not just in the individual applications.
			s_measurementUnitSetting = new RegistryIntSetting((int)MsrSysType.Cm, "MeasurementSystem");

			// This affects all FieldWorks apps.
			s_disableSplashScreen = new RegistryBoolSetting(false, "DisableSplashScreen");
		}

		/// <summary />
		internal FwRegistrySettings(IApp app)
		{
			if (app == null)
			{
				throw new ArgumentNullException(nameof(app));
			}
			m_firstTimeAppHasBeenRun = new RegistryBoolSetting(app.SettingsKey, "FirstTime", true);
			m_showSideBar = new RegistryBoolSetting(app.SettingsKey, "ShowSideBar", true);
			m_showStatusBar = new RegistryBoolSetting(app.SettingsKey, "ShowStatusBar", true);
			m_openLastEditedProject = new RegistryBoolSetting(app.SettingsKey, "OpenLastEditedProject", false);
			m_loadingProcessId = new RegistryIntSetting(app.SettingsKey, "LoadingProcessId", 0);
			m_numberOfLaunches = new RegistryIntSetting(app.SettingsKey, "launches", 0);
			m_numberOfSeriousCrashes = new RegistryIntSetting(app.SettingsKey, "NumberOfSeriousCrashes", 0);
			m_numberOfAnnoyingCrashes = new RegistryIntSetting(app.SettingsKey, "NumberOfAnnoyingCrashes", 0);
			m_totalAppRuntime = new RegistryIntSetting(app.SettingsKey, "TotalAppRuntime", 0);
			m_appStartupTime = new RegistryStringSetting(app.SettingsKey, "LatestAppStartupTime", string.Empty);
			m_latestProject = new RegistryStringSetting(app.SettingsKey, "LatestProject", string.Empty);
			m_latestServer = new RegistryStringSetting(app.SettingsKey, "LatestServer", string.Empty);
		}
		#endregion

		#region Disposable stuff
		/// <summary />
		~FwRegistrySettings()
		{
			Dispose(false);
		}

		/// <summary/>
		private bool IsDisposed { get; set; }

		/// <summary/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary/>
		private void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");

			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}

			if (disposing)
			{
				// dispose managed objects
				m_firstTimeAppHasBeenRun.Dispose();
				m_showSideBar.Dispose();
				m_showStatusBar.Dispose();
				m_openLastEditedProject.Dispose();
				m_loadingProcessId.Dispose();
				m_numberOfLaunches.Dispose();
				m_numberOfSeriousCrashes.Dispose();
				m_numberOfAnnoyingCrashes.Dispose();
				m_totalAppRuntime.Dispose();
				m_appStartupTime.Dispose();
				m_latestProject.Dispose();
				m_latestServer.Dispose();
			}
			IsDisposed = true;
		}

		/// <summary>
		/// Release static registry settings.
		/// NOTE: This should be called only by unit tests.
		/// </summary>
		internal static void Release()
		{
			s_measurementUnitSetting?.Dispose();
			s_disableSplashScreen?.Dispose();
			s_measurementUnitSetting = null;
			s_disableSplashScreen = null;
		}
		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the ID of a FieldWorks process currently loading a FieldWorks project.
		/// This value is set at the start of loading a FieldWorks project and is cleared
		/// when the loading of that project finishes.
		/// </summary>
		internal int LoadingProcessId
		{
			get => m_loadingProcessId.Value;
			set => m_loadingProcessId.Value = value;
		}

		/// <summary>
		/// Gets a value indicating whether or not this is the first time this app has
		/// ever been run.
		/// </summary>
		internal bool FirstTimeAppHasBeenRun
		{
			get => m_firstTimeAppHasBeenRun.Value;
			set => m_firstTimeAppHasBeenRun.Value = value;
		}

		/// <summary>
		/// Gets the number of times this app has been run.
		/// </summary>
		internal int NumberOfLaunches
		{
			get => m_numberOfLaunches.Value;
			set => m_numberOfLaunches.Value = value;
		}

		/// <summary>
		/// Get/set the number of serious (Green Dialog Box) crashes that have happened.
		/// </summary>
		internal int NumberOfSeriousCrashes
		{
			get => m_numberOfSeriousCrashes.Value;
			set => m_numberOfSeriousCrashes.Value = value;
		}

		/// <summary>
		/// Get/set the number of annoying (Yellow Dialog Box) crashes that have happened.
		/// </summary>
		internal int NumberOfAnnoyingCrashes
		{
			get => m_numberOfAnnoyingCrashes.Value;
			set => m_numberOfAnnoyingCrashes.Value = value;
		}

		/// <summary>
		/// Get/set the total number of seconds that the application has run on this computer.
		/// </summary>
		internal int TotalAppRuntime
		{
			get => m_totalAppRuntime.Value;
			set => m_totalAppRuntime.Value = value;
		}

		/// <summary>
		/// Get/set the startup time (in ticks) for the current run of the application.
		/// </summary>
		internal string LatestAppStartupTime
		{
			get => m_appStartupTime.Value;
			set => m_appStartupTime.Value = value;
		}

		/// <summary>
		/// Get/set the name (can be a filename or project name) of the last project saved in
		/// the application. (Until something gets saved, it is the very first project the
		/// user opens.)
		/// </summary>
		internal string LatestProject
		{
			get => m_latestProject.Value;
			set => m_latestProject.Value = value;
		}

		/// <summary>
		/// Gets or sets the value in the registry for the sidebar's visibility.
		/// </summary>
		internal bool ShowSideBarSetting
		{
			get => m_showSideBar.Value;
			set => m_showSideBar.Value = value;
		}

		/// <summary>
		/// Gets or sets the value in the registry for the status bar's visibility.
		/// </summary>
		internal bool ShowStatusBarSetting
		{
			get => m_showStatusBar.Value;
			set => m_showStatusBar.Value = value;
		}

		/// <summary>
		/// Gets or sets the value in the registry for whether to show the Welcome dialog, or
		/// just automatically open the latest successfully opened project.
		/// </summary>
		internal bool AutoOpenLastEditedProject
		{
			get => m_openLastEditedProject.Value;
			set => m_openLastEditedProject.Value = value;
		}
		#endregion

		#region Static Properties

		/// <summary>
		/// Gets the value in the registry for the splash screen enable flag.
		/// </summary>
		internal static bool DisableSplashScreenSetting => s_disableSplashScreen.Value;

		/// <summary>
		/// Get or set the value for the "Measurement Units" setting from the registry.
		/// </summary>
		internal static int MeasurementUnitSetting
		{
			get => s_measurementUnitSetting.Value;
			set => s_measurementUnitSetting.Value = value;
		}
		#endregion

		/// <summary>
		/// Adds the options as properties of the error reporter so that they show up in a
		/// call stack.
		/// </summary>
		internal void AddErrorReportingInfo()
		{
			ErrorReporter.AddProperty("FirstTimeAppHasBeenRun", FirstTimeAppHasBeenRun.ToString());
			var cLaunches = NumberOfLaunches + 1;   // this is stored before it's incremented.
			ErrorReporter.AddProperty("NumberOfLaunches", cLaunches.ToString());
			var cmin = TotalAppRuntime / 60;
			ErrorReporter.AddProperty("TotalRuntime", $"{cmin / 60}:{cmin % 60}");
			ErrorReporter.AddProperty("NumberOfSeriousCrashes", NumberOfSeriousCrashes.ToString());
			ErrorReporter.AddProperty("NumberOfAnnoyingCrashes", NumberOfAnnoyingCrashes.ToString());
			ErrorReporter.AddProperty("RuntimeBeforeCrash", "0:00");
			ErrorReporter.AddProperty("LoadingProcessId", LoadingProcessId.ToString());
			ErrorReporter.AddProperty("ShowSideBarSetting", ShowSideBarSetting.ToString());
			ErrorReporter.AddProperty("ShowStatusBarSetting", ShowStatusBarSetting.ToString());
			ErrorReporter.AddProperty("AutoOpenLastEditedProjectSetting", AutoOpenLastEditedProject.ToString());
			ErrorReporter.AddProperty("DisableSplashScreenSetting", DisableSplashScreenSetting.ToString());
			ErrorReporter.AddProperty("MeasurementUnitSetting", ((MsrSysType)MeasurementUnitSetting).ToString());
			ErrorReporter.AddProperty("BackupDirectorySetting", FwDirectoryFinder.DefaultBackupDirectory);
			ErrorReporter.AddProperty("ProjectsDirectorySetting", FwDirectoryFinder.ProjectsDirectory);
		}
	}
}