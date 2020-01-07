// Copyright (c) 2015-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

namespace SIL.FieldWorks.MigrateSqlDbs.MigrateProjects.Properties
{
	/// <summary>
	/// This class allows you to handle specific events on the settings class:
	/// The SettingChanging event is raised before a setting's value is changed.
	/// The PropertyChanged event is raised after a setting's value is changed.
	/// The SettingsLoaded event is raised after the setting values are loaded.
	/// The SettingsSaving event is raised before the setting values are saved.
	/// </summary>
	public sealed partial class Settings
	{
		/// <summary />
		public Settings()
		{
		}

		private void SettingChangingEventHandler(object sender, System.Configuration.SettingChangingEventArgs e)
		{
			// Add code to handle the SettingChangingEvent event here.
		}

		private void SettingsSavingEventHandler(object sender, System.ComponentModel.CancelEventArgs e)
		{
			// Add code to handle the SettingsSaving event here.
		}
	}
}