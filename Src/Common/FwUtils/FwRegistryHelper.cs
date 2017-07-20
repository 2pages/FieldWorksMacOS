// Copyright (c) 2010-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// File: FwRegistryHelper.cs
// Responsibility: FW Team
//
// <remarks>
// </remarks>

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Win32;
using SIL.Utils;

namespace SIL.FieldWorks.Common.FwUtils
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Helper class for accessing FieldWorks-specific registry settings
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public static class FwRegistryHelper
	{
		private static IFwRegistryHelper RegistryHelperImpl = new FwRegistryHelperImpl();

		/// <summary/>
		public static class Manager
		{
			/// <summary>
			/// Resets the registry helper. NOTE: should only be used from unit tests!
			/// </summary>
			public static void Reset()
			{
				RegistryHelperImpl = new FwRegistryHelperImpl();
			}

			/// <summary>
			/// Sets the registry helper. NOTE: Should only be used from unit tests!
			/// </summary>
			public static void SetRegistryHelper(IFwRegistryHelper helper)
			{
				RegistryHelperImpl = helper;
			}
		}

		/// <summary>Default implementation of registry helper</summary>
		private class FwRegistryHelperImpl: IFwRegistryHelper
		{
			public FwRegistryHelperImpl()
			{
				// FWNX-1235 Mono's implementation of the "Windows Registry" on Unix uses XML files in separate folders for
				// each user and each software publisher.  We need to read Paratext's entries, so we copy theirs into ours.
				// We overwrite any existing Paratext keys in case they have changed.
				if (MiscUtils.IsUnix)
				{
#if DEBUG
					// On a developer Linux machine these are kept under output/registry. Since the program is running at output/{debug|release},
					// one level up should find the registry folder.
					var fwRegLoc = Path.Combine(
						Path.GetDirectoryName(FileUtils.StripFilePrefix(System.Reflection.Assembly.GetExecutingAssembly().CodeBase)) ?? ".", "../registry");
#else
					var fwRegLoc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ".config/fieldworks/registry");
#endif

					var ptRegKeys = new[]
					{
						"LocalMachine/software/scrchecks", // Paratext 7 and earlier
						"LocalMachine/software/paratext" // Paratext 8 (latest as of 2017.07)
					};

					foreach (var ptRegKey in ptRegKeys)
					{
						var ptRegKeyLoc = Path.Combine(
							Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ".config/paratext/registry", ptRegKey);

						if (Directory.Exists(ptRegKeyLoc))
							DirectoryUtils.CopyDirectory(ptRegKeyLoc, Path.Combine(fwRegLoc, ptRegKey), true, true);
					}
				}
			}

			/// ------------------------------------------------------------------------------------
			/// <summary>
			/// Gets the read-only local machine Registry key for FieldWorks.
			/// NOTE: This key is not opened for write access because it will fail on
			/// non-administrator logins.
			/// </summary>
			/// ------------------------------------------------------------------------------------
			[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
				Justification = "We're returning an object")]
			public RegistryKey FieldWorksRegistryKeyLocalMachine
			{
				get
				{
					return RegistryHelper.SettingsKeyLocalMachine(FieldWorksRegistryKeyName);
				}
			}

			/// <summary>
			/// Get LocalMachine hive. (Overridable for unit tests.)
			/// </summary>
			public RegistryKey LocalMachineHive
			{
				get { return Registry.LocalMachine; }
			}

			/// ------------------------------------------------------------------------------------
			/// <summary>
			/// Gets the read-only local machine Registry key for FieldWorksBridge.
			/// NOTE: This key is not opened for write access because it will fail on
			/// non-administrator logins.
			/// </summary>
			/// ------------------------------------------------------------------------------------
			[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
				Justification = "We're returning an object")]
			public RegistryKey FieldWorksBridgeRegistryKeyLocalMachine
			{
				get
				{
					return Registry.LocalMachine.OpenSubKey("Software\\SIL\\FLEx Bridge\\8");
				}
			}

			/// ------------------------------------------------------------------------------------
			/// <summary>
			/// Gets the local machine Registry key for FieldWorks.
			/// NOTE: This will throw with non-administrative logons! Be ready for that.
			/// </summary>
			/// ------------------------------------------------------------------------------------
			[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
				Justification = "We're returning an object")]
			public RegistryKey FieldWorksRegistryKeyLocalMachineForWriting
			{
				get
				{
					return RegistryHelper.SettingsKeyLocalMachineForWriting(FieldWorksRegistryKeyName);
				}
			}

			/// ------------------------------------------------------------------------------------
			/// <summary>
			/// Gets the default (current user) Registry key for FieldWorks.
			/// </summary>
			/// ------------------------------------------------------------------------------------
			[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
				Justification = "We're returning an object")]
			public RegistryKey FieldWorksRegistryKey
			{
				get { return RegistryHelper.SettingsKey(FieldWorksRegistryKeyName); }
			}

			/// ------------------------------------------------------------------------------------
			/// <summary>
			/// Gets the default (current user) Registry key for FieldWorks without the version number.
			/// </summary>
			/// ------------------------------------------------------------------------------------
			[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
				Justification = "We're returning an object")]
			public RegistryKey FieldWorksVersionlessRegistryKey
			{
				get { return RegistryHelper.SettingsKey(); }
			}

			/// <summary>
			/// The value we look up in the FieldWorksRegistryKey to get(or set) the persisted user locale.
			/// </summary>
			public string UserLocaleValueName
			{
				get
				{
					return "UserWs";
				}
			}

			/// ------------------------------------------------------------------------------------
			/// <summary>
			/// Determines the installation or absence of version 7 of the Paratext program by checking for the
			/// existence of the registry key that that application uses to store its program files
			/// directory in the local machine settings.
			/// This is 'HKLM\Software\ScrChecks\1.0\Program_Files_Directory_Ptw7'
			/// NOTE: This key is not opened for write access because it will fail on
			/// non-administrator logins.
			/// </summary>
			/// ------------------------------------------------------------------------------------
			public bool Paratext7Installed()
			{
				using (var ParatextKey = Registry.LocalMachine.OpenSubKey("Software\\ScrChecks\\1.0"))
				{
#if __MonoCS__
					// Unfortunately on Linux Paratext 7.5 does not produce all the same registry keys as it does on Windows
					// we can't actually tell the version of Paratext from these keys, so assume 7 if Settings_Directory is found
					return ParatextKey != null && RegistryHelper.KeyExists(ParatextKey, "Settings_Directory");
#else
					return ParatextKey != null && RegistryHelper.KeyExists(ParatextKey, "Program_Files_Directory_Ptw7");
#endif
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the read-only local machine Registry key for FieldWorks.
		/// NOTE: This key is not opened for write access because it will fail on
		/// non-administrator logins.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static RegistryKey FieldWorksRegistryKeyLocalMachine
		{
			get { return RegistryHelperImpl.FieldWorksRegistryKeyLocalMachine; }
		}


		/// <summary>
		/// Get LocalMachine hive. (Overridable for unit tests.)
		/// </summary>
		public static RegistryKey LocalMachineHive
		{
			get { return RegistryHelperImpl.LocalMachineHive; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the read-only local machine Registry key for FieldWorksBridge.
		/// NOTE: This key is not opened for write access because it will fail on
		/// non-administrator logins.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static RegistryKey FieldWorksBridgeRegistryKeyLocalMachine
		{
			get { return RegistryHelperImpl.FieldWorksBridgeRegistryKeyLocalMachine; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the local machine Registry key for FieldWorks.
		/// NOTE: This will throw with non-administrative logons! Be ready for that.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static RegistryKey FieldWorksRegistryKeyLocalMachineForWriting
		{
			get { return RegistryHelperImpl.FieldWorksRegistryKeyLocalMachineForWriting; }
		}

		/// <summary>
		/// Extension method to write a registry key to somewhere in HKLM hopfully with
		/// eleverating privileges. This method can cause the UAC dialog to be shown to the user
		/// (on Vista or later).
		/// Can throw SecurityException on permissions problems.
		/// </summary>
		public static void SetValueAsAdmin(this RegistryKey key, string name, string value)
		{
			Debug.Assert(key.Name.Substring(0, key.Name.IndexOf("\\")) == "HKEY_LOCAL_MACHINE",
				"SetValueAsAdmin should only be used for writing hklm values.");

			if (MiscUtils.IsUnix)
			{
				key.SetValue(name, value);
				return;
			}

			int startOfKey = key.Name.IndexOf("\\") + "\\".Length;
			string location = key.Name.Substring(startOfKey, key.Name.Length - startOfKey);
			location = location.Trim('\\');

			// .NET cmd processing treats \" as a single ", not part of a delimiter.
			// This can mess up closing " delimiters when the string ends with backslash.
			// To get around this, you need to add an extra \ to the end.  "D:\"  -> D:"	 "D:\\" -> D:\
			// Cmd line with 4 args: "Software\SIL\"8" "Projects\\Dir\" "I:\" "e:\\"
			// Interpreted as 3 args: 1)"Software\\SIL\\FieldWorks\"8"  2)"Projects\\\\Dir\" I:\""  3)"e:\\"
			// We'll hack the final value here to put in an extra \ for final \. "c:\\" will come through as c:\.
			string path = value;
			if (value.EndsWith("\\"))
				path = value + "\\";

			using (var process = new Process())
			{
				// Have to show window to get UAC message to allow admin action.
				//process.StartInfo.CreateNoWindow = true;
				process.StartInfo.FileName = "WriteKey.exe";
				process.StartInfo.Arguments = String.Format("LM \"{0}\" \"{1}\" \"{2}\"", location, name, path);
				// NOTE: According to information I found, these last 2 values have to be set as they are
				// (Verb='runas' and UseShellExecute=true) in order to get the UAC dialog to show.
				// On Xp (Verb='runas' and UseShellExecute=true) causes crash.
				if (MiscUtils.IsWinVistaOrNewer)
				{
					process.StartInfo.Verb = "runas";
					process.StartInfo.UseShellExecute = true;
				}
				else
				{
					process.StartInfo.UseShellExecute = false;
				}
				// Make sure the shell window is not shown (FWR-3361)
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

				// Can throw a SecurityException.
				process.Start();
				process.WaitForExit();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the default (current user) Registry key for FieldWorks.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static RegistryKey FieldWorksRegistryKey
		{
			get { return RegistryHelperImpl.FieldWorksRegistryKey; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the default (current user) Registry key for FieldWorks without the version number.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static RegistryKey FieldWorksVersionlessRegistryKey
		{
			get { return RegistryHelperImpl.FieldWorksVersionlessRegistryKey; }
		}

		/// <summary>
		/// Gets the current SuiteVersion as a string
		/// </summary>
		public static string FieldWorksRegistryKeyName
		{
			get { return FwUtils.SuiteVersion.ToString(CultureInfo.InvariantCulture); }
		}

		/// <summary>
		/// It's probably a good idea to keep around the name of the old versions' keys
		/// for upgrading purposes. See UpgradeUserSettingsIfNeeded().
		/// </summary>
		internal const string OldFieldWorksRegistryKeyNameVersion7 = "7.0";

		/// <summary>
		/// The value we look up in the FieldWorksRegistryKey to get(or set) the persisted user locale.
		/// </summary>
		public static string UserLocaleValueName
		{
			get { return RegistryHelperImpl.UserLocaleValueName; }
		}


		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines the installation or absence of version 7 of the Paratext program by checking for the
		/// existence of the registry key that that application uses to store its program files
		/// directory in the local machine settings.
		/// This is 'HKLM\Software\ScrChecks\1.0\Program_Files_Directory_Ptw7'
		/// NOTE: This key is not opened for write access because it will fail on
		/// non-administrator logins.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool Paratext7Installed()
		{
			return RegistryHelperImpl.Paratext7Installed();
		}

		/// <summary>
		/// E.g. the first time the user runs FW8, we need to copy a bunch of registry keys
		/// from HKCU/Software/SIL/FieldWorks/7.0 -> FieldWorks/8.
		/// </summary>
		public static void UpgradeUserSettingsIfNeeded()
		{
			try
			{
				using (var fieldWorksVersionlessRegistryKey = FieldWorksVersionlessRegistryKey)
				{
					var v7exists = RegistryHelper.KeyExists(fieldWorksVersionlessRegistryKey,
						OldFieldWorksRegistryKeyNameVersion7);
					if (!v7exists)
						return; // We'll assume this already got done!

					// If v8 key exists, we will go ahead and do the copy, but not overwrite any existing values.
					using (var version7Key = fieldWorksVersionlessRegistryKey.CreateSubKey(OldFieldWorksRegistryKeyNameVersion7))
					using (var version8Key = fieldWorksVersionlessRegistryKey.CreateSubKey(FieldWorksRegistryKeyName))
					{
						// Copy over almost everything from 7.0 to 8
						// Don't copy the "launches" key or keys starting with "NumberOf"
						CopySubKeyTree(version7Key, version8Key);
					}

					// After copying everything delete the old key
					fieldWorksVersionlessRegistryKey.DeleteSubKeyTree(OldFieldWorksRegistryKeyNameVersion7);
				}
			}
			catch (SecurityException se)
			{
				// What to do here? Punt!
			}
		}

		/// <summary>
		/// Migrate the ProjectShared value stored in HKLM in version 7 into the HKCU (.Default since this will be run as system)
		/// </summary>
		/// <returns></returns>
		[SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule",
			Justification = "LocalMachineHive is a reference")]
		public static void MigrateVersion7ValueIfNeeded()
		{
			// Guard for some broken Windows machines having trouble accessing HKLM (LT-15158).
			var hklm = LocalMachineHive;
			if (hklm != null)
			{
				using (var oldProjectSharedSettingLocation = hklm.OpenSubKey(@"SOFTWARE\SIL\FieldWorks\7.0"))
				{
					object oldProjectSharedValue, newProjectSharedValue;
					if (oldProjectSharedSettingLocation != null
						&& !RegistryHelper.RegEntryExists(FieldWorksRegistryKey, string.Empty, @"ProjectShared", out newProjectSharedValue)
						&& RegistryHelper.RegEntryExists(oldProjectSharedSettingLocation, string.Empty, @"ProjectShared", out oldProjectSharedValue))
					{
						FieldWorksRegistryKey.SetValue(@"ProjectShared", oldProjectSharedValue);
					}
				}
			}
		}

		private static void CopySubKeyTree(RegistryKey srcSubKey, RegistryKey destSubKey)
		{
			// Copies all keys and values from src to dest subKey recursively
			// except 'launches' value (whereever found) and values with names starting with "NumberOf"
			CopyAllValuesToNewSubKey(srcSubKey, destSubKey);
			foreach (var subKeyName in srcSubKey.GetSubKeyNames())
			{
				using(var newDestKey = destSubKey.CreateSubKey(subKeyName))
				{
					CopySubKeyTree(srcSubKey.CreateSubKey(subKeyName), newDestKey);
				}
			}
		}

		private static void CopyAllValuesToNewSubKey(RegistryKey srcSubKey, RegistryKey destSubKey)
		{
			const string NumberPrefix = "NumberOf";
			const string LaunchesString = "launches";
			foreach (var valueName in srcSubKey.GetValueNames().Where(
				valueName => !valueName.StartsWith(NumberPrefix) && valueName != LaunchesString))
			{
				CopyValueToNewKey(valueName, srcSubKey, destSubKey);
			}
		}

		private static void CopyValueToNewKey(string valueName, RegistryKey oldSubKey, RegistryKey newSubKey)
		{
			// Just don't overwrite the value if it exists already!
			object dummyValue;
			if (RegistryHelper.RegEntryExists(newSubKey, string.Empty, valueName, out dummyValue))
				return;
			var valueObject = oldSubKey.GetValue(valueName);
			newSubKey.SetValue(valueName, valueObject);
		}
	}

}
