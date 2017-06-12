// Copyright (c) 2003-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.IO;
using System.Threading;		// for Monitor (dlh)
using System.Text;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.FwUtils;

namespace SIL.FieldWorks.Common.FwUtils.Impls
{
	/// <summary>
	/// Table of properties, some of which are persisted, and some that are not.
	/// </summary>
	[Serializable]
	internal sealed class PropertyTable : IPropertyTable, IDisposable
	{
		private IPublisher Publisher { get; set; }

		private Dictionary<string, Property> m_properties;
		/// <summary>
		/// Control how much output we send to the application's listeners (e.g. visual studio output window)
		/// </summary>
		private TraceSwitch m_traceSwitch = new TraceSwitch("PropertyTable", "");
		private string m_localSettingsId = null;
		private string m_userSettingDirectory = "";

		/// -----------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="PropertyTable"/> class.
		/// </summary>
		/// -----------------------------------------------------------------------------------
		internal PropertyTable(IPublisher publisher)
		{
			m_properties = new Dictionary<string, Property>(100);
			Publisher = publisher;
		}

#if RANDYTODO
		// TODO: Put these in the right places.
<!-- global persist -->
<defaultProperties>
	<!-- increment this to make toolbar layouts revert to default -->
	<property name="CurrentToolbarVersion" value="1" />
</defaultProperties>
<!-- local persist -->
<defaultProperties>
	<property name="Show_DictionaryPubPreview" bool="true" persist="true" settingsGroup="local" />
	<property name="PartsOfSpeech.posEdit.DataTree-Splitter" intValue="200" settingsGroup="local" />
	<property name="PartsOfSpeech.posAdvancedEdit.DataTree-Splitter" intValue="200" settingsGroup="local" />
</defaultProperties>
<!-- local do not persist -->
<defaultProperties>
	<property name="Show_reversalIndexEntryList" bool="true" persist="false" settingsGroup="local" />
</defaultProperties>
#endif

		#region IDisposable & Co. implementation
		// Region last reviewed: never

		/// <summary>
		/// Check to see if the object has been disposed.
		/// All public Properties and Methods should call this
		/// before doing anything else.
		/// </summary>
		public void CheckDisposed()
		{
			if (IsDisposed)
				throw new ObjectDisposedException(String.Format("'{0}' in use after being disposed.", GetType().Name));
		}

		/// <summary>
		/// True, if the object has been disposed.
		/// </summary>
		private bool m_isDisposed = false;

		/// <summary>
		/// See if the object has been disposed.
		/// </summary>
		public bool IsDisposed
		{
			get { return m_isDisposed; }
		}

		/// <summary>
		/// Finalizer, in case client doesn't dispose it.
		/// Force Dispose(false) if not already called (i.e. m_isDisposed is true)
		/// </summary>
		/// <remarks>
		/// In case some clients forget to dispose it directly.
		/// </remarks>
		~PropertyTable()
		{
			Dispose(false);
			// The base class finalizer is called automatically.
		}

		/// <summary>
		///
		/// </summary>
		/// <remarks>Must not be virtual.</remarks>
		public void Dispose()
		{
			Dispose(true);
			// This object will be cleaned up by the Dispose method.
			// Therefore, you should call GC.SupressFinalize to
			// take this object off the finalization queue
			// and prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Executes in two distinct scenarios.
		///
		/// 1. If disposing is true, the method has been called directly
		/// or indirectly by a user's code via the Dispose method.
		/// Both managed and unmanaged resources can be disposed.
		///
		/// 2. If disposing is false, the method has been called by the
		/// runtime from inside the finalizer and you should not reference (access)
		/// other managed objects, as they already have been garbage collected.
		/// Only unmanaged resources can be disposed.
		/// </summary>
		/// <param name="disposing"></param>
		/// <remarks>
		/// If any exceptions are thrown, that is fine.
		/// If the method is being done in a finalizer, it will be ignored.
		/// If it is thrown by client code calling Dispose,
		/// it needs to be handled by fixing the bug.
		///
		/// If subclasses override this method, they should call the base implementation.
		/// </remarks>
		private void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			// Must not be run more than once.
			if (m_isDisposed)
				return;

			if (disposing)
			{
				// Dispose managed resources here.
				foreach (var property in m_properties.Values)
				{
					if (property.name == "Subscriber")
					{
						// Leave this for now, as stuff that is being disposed,
						// may want to unsubscribe.
						continue;
					}
					if (property.doDispose)
					{
						((IDisposable)property.value).Dispose();
					}
					property.name = null;
					property.value = null;
				}
				m_properties.Clear();
			}

			// Dispose unmanaged resources here, whether disposing is true or false.
			m_localSettingsId = null;
			m_userSettingDirectory = null;
			m_properties = null;
			m_traceSwitch = null;
			Publisher = null;

			m_isDisposed = true;
		}

		#endregion IDisposable & Co. implementation

		#region Removing

		/// <summary>
		/// Remove a property from the table.
		/// </summary>
		/// <param name="name">Name of the property to remove.</param>
		/// <param name="settingsGroup">The group to remove the property from.</param>
		public void RemoveProperty(string name, SettingsGroup settingsGroup)
		{
			CheckDisposed();

			var key = GetPropertyKeyFromSettingsGroup(name, settingsGroup);
			Property goner;
			if (m_properties.TryGetValue(key, out goner))
			{
				m_properties.Remove(key);
				goner.value = null;
			}
		}

		/// <summary>
		/// Remove a property from the table.
		/// </summary>
		/// <param name="name">Name of the property to remove.</param>
		public void RemoveProperty(string name)
		{
			RemoveProperty(name, SettingsGroup.BestSettings);
		}

		#endregion Removing

		#region getting and setting

		/// <summary>
		/// Get the property key/name, based on 'settingsGroup'.
		/// It may be the original property name or one adjusted for local settings.
		/// Caller then uses the returned value as the property dictionary key.
		///
		/// For SettingsGroup.BestSettings:
		/// Prefer local over global, if both exist.
		///	Prefer global if neither exists.
		/// </summary>
		/// <returns>The original property name or one adjusted for local settings</returns>
		private string GetPropertyKeyFromSettingsGroup(string name, SettingsGroup settingsGroup)
		{
			switch (settingsGroup)
		{
				default:
					throw new NotImplementedException(string.Format("{0} is not yet supported. Developers need to add support for it.", settingsGroup));
				case SettingsGroup.BestSettings:
			{
				var key = FormatPropertyNameForLocalSettings(name);
					return GetProperty(key) != null ?
						key // local exists. We don't care if global exists, or not, since we prefer local over global.
						: name; // Whether a global property exists, or not, go with the global internal property name.
				}
				case SettingsGroup.LocalSettings:
					return FormatPropertyNameForLocalSettings(name);
				case SettingsGroup.GlobalSettings:
					return name;
			}
		}

		/// <summary>
		/// Test whether a property exists, tries local first and then global.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public bool PropertyExists(string name)
		{
			CheckDisposed();

			return PropertyExists(name, SettingsGroup.BestSettings);
		}

		/// <summary>
		/// Test whether a property exist in the specified group.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="settingsGroup"></param>
		/// <returns></returns>
		public bool PropertyExists(string name, SettingsGroup settingsGroup)
		{
			CheckDisposed();

			return GetProperty(GetPropertyKeyFromSettingsGroup(name, settingsGroup)) != null;
		}

		/// <summary>
		/// Test whether a property exists in the specified group. Gives any value found.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="propertyValue">null, if it didn't find the property.</param>
		/// <returns></returns>
		public bool TryGetValue<T>(string name, out T propertyValue)
		{
			CheckDisposed();

			return TryGetValue(name, SettingsGroup.BestSettings, out propertyValue);
		}

		/// <summary>
		/// Test whether a property exists in the specified group. Gives any value found.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="settingsGroup"></param>
		/// <param name="propertyValue">null, if it didn't find the property.</param>
		/// <returns></returns>
		public bool TryGetValue<T>(string name, SettingsGroup settingsGroup, out T propertyValue)
		{
			CheckDisposed();

			propertyValue = default(T);
			var prop = GetProperty(GetPropertyKeyFromSettingsGroup(name, settingsGroup));
			if (prop == null)
			{
				return false;
			}
			var basicValue = prop.value;
			if (basicValue == null)
			{
				return false;
			}
			if (basicValue is T)
			{
				propertyValue = (T)basicValue;
				return true;
			}
			throw new ArgumentException("Mismatched data type.");
		}

		private Property GetProperty(string key)
		{
			if (!Monitor.TryEnter(m_properties))
			{
				FwUtils.ErrorBeep();
				TraceVerboseLine(">>>>>>>*****  colision: <A>  ********<<<<<<<<<<<");
				Monitor.Enter(m_properties);
			}

			Property result;
			m_properties.TryGetValue(key, out result);

			Monitor.Exit(m_properties);

			return result;
		}

		/// <summary>
		/// get the value of the best property (i.e. tries local first, then global).
		/// </summary>
		/// <param name="name"></param>
		/// <returns>returns null if the property is not found</returns>
		public T GetValue<T>(string name)
		{
			CheckDisposed();

			return GetValue<T>(name, SettingsGroup.BestSettings);
		}

		/// <summary>
		/// Get the value of the property of the specified settingsGroup.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="settingsGroup"></param>
		/// <returns></returns>
		public T GetValue<T>(string name, SettingsGroup settingsGroup)
		{
			CheckDisposed();

			return GetValueInternal<T>(GetPropertyKeyFromSettingsGroup(name, settingsGroup));
		}

		/// <summary>
		/// Get the property of type "T"
		/// </summary>
		/// <typeparam name="T">Type of property to return</typeparam>
		/// <param name="name">Name of property to return</param>
		/// <param name="defaultValue">Default value of property, if it isn't in the table.</param>
		/// <returns>The stroed property of type "T", or the defualt value, if not stored.</returns>
		public T GetValue<T>(string name, T defaultValue)
		{
			CheckDisposed();

			return GetValue(name, SettingsGroup.BestSettings, defaultValue);
		}

		/// <summary>
		/// Get the value of the property in the specified settingsGroup.
		/// Sets the defaultValue if the property doesn't exist.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="settingsGroup"></param>
		/// <param name="defaultValue"></param>
		/// <returns></returns>
		public T GetValue<T>(string name, SettingsGroup settingsGroup, T defaultValue)
		{
			CheckDisposed();

			return GetValueInternal(GetPropertyKeyFromSettingsGroup(name, settingsGroup), defaultValue);
		}

		/// <summary>
		/// get the value of a property
		/// </summary>
		/// <param name="key">Encoded name for local or global lookup</param>
		/// <returns>Returns the property value, or null if property does not exist.</returns>
		/// <exception cref="ArgumentException">Thrown if the property value is not type "T".</exception>
		private T GetValueInternal<T>(string key)
		{
			if (!Monitor.TryEnter(m_properties))
			{
				FwUtils.ErrorBeep();
				TraceVerboseLine(">>>>>>>*****  colision: <A>  ********<<<<<<<<<<<");
				Monitor.Enter(m_properties);
			}
			var result = default(T);
			Property prop;
			if (m_properties.TryGetValue(key, out prop))
			{
				var basicValue = prop.value;
				if (basicValue == null)
				{
					return result;
				}
				if (basicValue is T)
				{
					result = (T)basicValue;
				}
				else
				{
					throw new ArgumentException("Mismatched data type.");
				}
			}
			Monitor.Exit(m_properties);

			return result;
		}

		/// <summary>
		/// Get the value of the property of the specified settingsGroup.
		/// </summary>
		/// <param name="key">Encoded name for local or global lookup</param>
		/// <param name="defaultValue"></param>
		/// <returns></returns>
		private T GetValueInternal<T>(string key, T defaultValue)
		{
			T result;
			var prop = GetProperty(key);
			if (prop == null)
			{
				result = defaultValue;
				SetPropertyInternal(key, defaultValue, false, false);
			}
			else
			{
				if (prop.value == null)
				{
					// Gutless wonder (prop exists, but has no value).
					prop.value = defaultValue;
					result = defaultValue;
				}
				else
				{
					if (prop.value is T)
					{
						result = (T)prop.value;
					}
					else
					{
						throw new ArgumentException("Mismatched data type.");
					}
				}
			}
			return result;
		}

		/// <summary>
		/// Set the default value of a property, but *only* if property is not in the table.
		/// Do nothing, if the property is alreeady in the table.
		/// </summary>
		/// <param name="name">Name of the property to set</param>
		/// <param name="defaultValue">Default value of the new property</param>
		/// <param name="settingsGroup">Group the property is expected to be in.</param>
		/// <param name="persistProperty">
		/// "true" if the property is to be persisted, otherwise "false".</param>
		/// <param name="doBroadcastIfChanged">
		/// "true" if the property should be broadcast, and then, only if it has changed.
		/// "false" to not broadcast it at all.
		/// </param>
		public void SetDefault(string name, object defaultValue, SettingsGroup settingsGroup, bool persistProperty, bool doBroadcastIfChanged)
		{
			CheckDisposed();

			SetDefaultInternal(GetPropertyKeyFromSettingsGroup(name, settingsGroup), defaultValue, persistProperty, doBroadcastIfChanged);
		}

		/// <summary>
		/// set a default; does nothing if this value is already in the PropertyTable.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="defaultValue"></param>
		/// <param name="persistProperty"></param>
		/// <param name="doBroadcastIfChanged">
		/// "true" if the property should be broadcast, and then, only if it has changed.
		/// "false" to not broadcast it at all.
		/// </param>
		private void SetDefaultInternal(string key, object defaultValue, bool persistProperty, bool doBroadcastIfChanged)
		{
			if(!Monitor.TryEnter(m_properties))
			{
				TraceVerboseLine(">>>>>>>*****  colision: <c>  ********<<<<<<<<<<<");
				Monitor.Enter(m_properties);
			}
			if (!m_properties.ContainsKey(key))
			{
				SetPropertyInternal(key, defaultValue, persistProperty, doBroadcastIfChanged);
			}

			Monitor.Exit(m_properties);
		}

		/// <summary>
		/// Set the property value for the specified settingsGroup, and allow user to broadcast the change, or not.
		/// Caller must also declare if the property is to be persisted, or not.
		/// </summary>
		/// <param name="name">Property name</param>
		/// <param name="newValue">New value of the property. (It may never have been set before.)</param>
		/// <param name="settingsGroup">The group to store the property in.</param>
		/// <param name="persistProperty">
		/// "true" if the property is to be persisted, otherwise "false".</param>
		/// <param name="doBroadcastIfChanged">
		/// "true" if the property should be broadcast, and then, only if it has changed.
		/// "false" to not broadcast it at all.
		/// </param>
		public void SetProperty(string name, object newValue, SettingsGroup settingsGroup, bool persistProperty, bool doBroadcastIfChanged)
		{
			CheckDisposed();

			SetPropertyInternal(GetPropertyKeyFromSettingsGroup(name, settingsGroup), newValue, persistProperty, doBroadcastIfChanged);
		}

		/// <summary>
		/// set the value of the best property (try finding local first, then global)
		/// and broadcast the change if so instructed
		/// </summary>
		/// <param name="name"></param>
		/// <param name="newValue"></param>
		/// <param name="persistProperty"></param>
		/// <param name="doBroadcastIfChanged">
		/// "true" if the property should be broadcast, and then, only if it has changed.
		/// "false" to not broadcast it at all.
		/// </param>
		public void SetProperty(string name, object newValue, bool persistProperty, bool doBroadcastIfChanged)
		{
			CheckDisposed();
			SetProperty(name, newValue, SettingsGroup.BestSettings, persistProperty, doBroadcastIfChanged);
		}

		/// <summary>
		/// set the value and broadcast the change if so instructed
		/// </summary>
		/// <param name="key"></param>
		/// <param name="newValue"></param>
		/// <param name="persistProperty"></param>
		/// <param name="doBroadcastIfChanged">
		/// "true" if the property should be broadcast, and then, only if it has changed.
		/// "false" to not broadcast it at all.
		/// </param>
		private void SetPropertyInternal(string key, object newValue, bool persistProperty, bool doBroadcastIfChanged)
		{
			CheckDisposed();

			var didChange = true;
			if (!Monitor.TryEnter(m_properties))
			{
				TraceVerboseLine(">>>>>>>*****  collision: <d>  ********<<<<<<<<<<<");
				Monitor.Enter(m_properties);
			}
			if (m_properties.ContainsKey(key))
			{
				var property = m_properties[key];
				// May update the persistance, as in when a default was created which persists, but now we want to not persist it.
				property.doPersist = persistProperty;
				object oldValue = property.value;
				bool bothNull = (oldValue == null && newValue == null);
				bool oldExists = (oldValue != null);
				didChange = !( bothNull
								|| (oldExists
									&&
									(	(oldValue == newValue) // Identity is the same
										|| oldValue.Equals(newValue)) // Close enough for government work.
									)
								);
				if (didChange)
				{
					if (property.value != null && property.doDispose)
						(property.value as IDisposable).Dispose(); // Get rid of the old value.
					property.value = newValue;
				}
			}
			else
			{
				m_properties[key] = new Property(key, newValue)
				{
					doPersist = persistProperty
				};
			}

			if (didChange && doBroadcastIfChanged && Publisher != null)
			{
				var localSettingsPrefix = GetPathPrefixForSettingsId(LocalSettingsId);
				var propertyName = key.StartsWith(localSettingsPrefix) ? key.Remove(0, localSettingsPrefix.Length) : key;
				Publisher.Publish(propertyName, newValue);
			}

			Monitor.Exit(m_properties);

#if SHOWTRACE
			if (newValue != null)
			{
				TraceVerboseLine("Property '"+key+"' --> '"+newValue.ToString()+"'");
			}
#endif
		}

		/// <summary>
		/// Declare if the property is to be disposed by the table.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="doDispose"></param>
		public void SetPropertyDispose(string name, bool doDispose)
		{
			CheckDisposed();
			SetPropertyDispose(name, doDispose, SettingsGroup.BestSettings);
		}

		/// <summary>
		/// Declare if the property is to be disposed by the table.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="doDispose"></param>
		/// <param name="settingsGroup"></param>
		public void SetPropertyDispose(string name, bool doDispose, SettingsGroup settingsGroup)
		{
			CheckDisposed();

			SetPropertyDisposeInternal(GetPropertyKeyFromSettingsGroup(name, settingsGroup), doDispose);
		}

		private void SetPropertyDisposeInternal(string key, bool doDispose)
		{
			if(!Monitor.TryEnter(m_properties))
			{
				TraceVerboseLine(">>>>>>>*****  colision: <e>  ********<<<<<<<<<<<");
				Monitor.Enter(m_properties);
			}
			try
			{
				Property property = m_properties[key];
				// Don't need an assert,
				// since the Dictionary will throw an exception,
				// if the key is missing.
				//Debug.Assert(property != null);
				if (!(property.value is IDisposable))
					throw new ArgumentException(String.Format("The property named: {0} is not valid for disposing.", key));
				property.doDispose = doDispose;
			}
			finally
			{
				Monitor.Exit(m_properties);
			}
		}
		#endregion

		#region persistence stuff

		/// <summary>
		/// Save general application settings
		/// </summary>
		public void SaveGlobalSettings()
		{
			CheckDisposed();
			// first save global settings, ignoring database specific ones.
			// The empty string '""' in the first parameter means the global settings.
			// The array in the second parameter means to 'exclude me'.
			// In this case, local settings won't be saved.
			Save("", new[] { LocalSettingsId });
		}

		/// <summary>
		/// Save database specific settings.
		/// </summary>
		public void SaveLocalSettings()
		{
			CheckDisposed();
			// now save database specific settings.
			Save(LocalSettingsId, new string[0]);
		}

		/// <summary>
		/// save the project and its contents to a file
		/// </summary>
		/// <param name="settingsId">save settings starting with this, and use as part of file name</param>
		/// <param name="omitSettingIds">skip settings starting with any of these.</param>
		private void Save(string settingsId, string[] omitSettingIds)
		{
			CheckDisposed();
			try
			{
				XmlSerializer szr = new XmlSerializer(typeof (Property[]));
				string path = SettingsPath(settingsId);
				Directory.CreateDirectory(Path.GetDirectoryName(path)); // Just in case it does not exist.
				using (var writer = new StreamWriter(path))
				{
					szr.Serialize(writer, MakePropertyArrayForSerializing(settingsId, omitSettingIds));
				}
			}
			catch (SecurityException)
			{
				// Probably another instance of FieldWorks is saving settings at the same time.
				// We can afford to ignore this, since it doesn't really matter which of them
				// manages to write its settings.
			}
			catch (UnauthorizedAccessException)
			{
				// Likewise...not sure which of these is actually thrown when another instance is writing.
			}
			catch (Exception err)
			{
				throw new ApplicationException("There was a problem saving your settings.", err);
			}
		}

		private string GetPathPrefixForSettingsId(string settingsId)
		{
			if (String.IsNullOrEmpty(settingsId))
				return string.Empty;

			return FormatPropertyNameForLocalSettings(string.Empty, settingsId);
		}

		/// <summary>
		/// Get a file path for the project settings file.
		/// </summary>
		/// <param name="settingsId"></param>
		/// <returns></returns>
		private string SettingsPath(string settingsId)
		{
			CheckDisposed();
			string pathPrefix = GetPathPrefixForSettingsId(settingsId);
			return Path.Combine(UserSettingDirectory, pathPrefix + "Settings.xml");
		}

		/// <summary>
		/// Arg 0 is database "LocalSettingsId"
		/// Arg 1 is PropertyName
		/// </summary>
		private string LocalSettingsPropertyFormat
		{
			// NOTE: The reason we are using 'db${0}' for local settings identifier is for FLEx historical
			// reasons. FLEx was using this prefix to store its local settings.
			get { return "db${0}${1}"; }
		}

		private string FormatPropertyNameForLocalSettings(string name, string settingsId)
		{
			return String.Format(LocalSettingsPropertyFormat, settingsId, name);
		}

		private string FormatPropertyNameForLocalSettings(string name)
		{
			return FormatPropertyNameForLocalSettings(name, LocalSettingsId);
		}

		/// <summary>
		/// Establishes a current group id for saving to property tables/files with SettingsGroup.LocalSettings.
		/// By default, this is the same as GlobalSettingsId.
		/// </summary>
		public string LocalSettingsId
		{
			get
			{
				CheckDisposed();
				if (m_localSettingsId == null)
					return GlobalSettingsId;
				return m_localSettingsId;
			}
			set
			{
				CheckDisposed();
				m_localSettingsId = value;
			}
		}

		/// <summary>
		/// Establishes a current group id for saving to property tables/files with SettingsGroup.GlobalSettings.
		/// </summary>
		public string GlobalSettingsId
		{
			get
			{
				CheckDisposed();
				return "";
			}
		}

		/// <summary>
		/// Gets/sets folder where user settings are saved
		/// </summary>
		public string UserSettingDirectory
		{
			get
			{
				CheckDisposed();
				Debug.Assert(!String.IsNullOrEmpty(m_userSettingDirectory));
				return m_userSettingDirectory;
			}
			set
			{
				CheckDisposed();

				if (string.IsNullOrEmpty(value))
					throw new ArgumentNullException("value", @"Cannot set 'UserSettingDirectory' to null or empty string.");

				m_userSettingDirectory = value;
			}
		}

		/// <summary>
		/// load with properties stored
		///  in the settings file, if that file is found.
		/// </summary>
		/// <param name="settingsId">e.g. "itinerary"</param>
		/// <returns></returns>
		public void RestoreFromFile(string settingsId)
		{
			CheckDisposed();
			string path = SettingsPath(settingsId);

			if (!System.IO.File.Exists(path))
				return;

			try
			{
				XmlSerializer szr = new XmlSerializer(typeof(Property[]));
				using (var reader = new StreamReader(path))
				{
					Property[] list = (Property[])szr.Deserialize(reader);
					ReadPropertyArrayForDeserializing(list);
				}
			}
			catch(FileNotFoundException)
			{
				//don't do anything
			}
			catch(Exception )
			{
				var activeForm = Form.ActiveForm;
				if (activeForm == null)
					MessageBox.Show(FwUtilsStrings.ProblemRestoringSettings);
				else
				{
					// Make sure as far as possible it comes up in front of any active window, including the splash screen.
					activeForm.Invoke((Func<DialogResult>)(() => MessageBox.Show(activeForm, FwUtilsStrings.ProblemRestoringSettings)));
				}
			}
		}

		private void ReadPropertyArrayForDeserializing(Property[] list)
		{
			//TODO: make a property which contains the date and time that the configuration file we are using.
			//then, when reading this back in, ignore the properties if they were saved under an old configuration file.

			foreach(Property property in list)
			{
				//I know it is strange, but the serialization code will give us a
				//	null property if there were no other properties.
				if (property != null)
				{
					if(!Monitor.TryEnter(m_properties))
					{
						TraceVerboseLine(">>>>>>>*****  colision: <g>  ********<<<<<<<<<<<");
						Monitor.Enter(m_properties);
					}

					// REVIEW JohnH(RandyR): I added the Remove call,
					// because one of the properties was already there, and 'Add' throws an exception,
					// if it is there.
					//ANSWER (JH): But how could a duplicate get in there?
					// This is only called once, and no code should ever putting duplicates when saving.
					// RESPONSE (RR): Beats me how it happened, but I 'found it' via the exception
					// that was thrown by it already being there.
					m_properties.Remove(property.name); // In case it is there.
					m_properties.Add(property.name, property);
					Monitor.Exit(m_properties);
				}
			}
		}

		private Property[] MakePropertyArrayForSerializing(string settingsId, string[] omitSettingIds)
		{
			if (!Monitor.TryEnter(m_properties))
			{
				TraceVerboseLine(">>>>>>>*****  colision: <i>  ********<<<<<<<<<<<");
				Monitor.Enter(m_properties);
			}
			List<Property> list = new List<Property>(m_properties.Count);
			foreach (KeyValuePair<string, Property> kvp in m_properties)
			{
				Property property = kvp.Value;
				if (!property.doPersist)
					continue;
				if (property.value == null)
					continue;
				if (!property.name.StartsWith(GetPathPrefixForSettingsId(settingsId)))
					continue;

				bool fIncludeThis = true;
				foreach (string omitSettingsId in omitSettingIds)
				{
					if (property.name.StartsWith(GetPathPrefixForSettingsId(omitSettingsId)))
					{
						fIncludeThis = false;
						break;
					}
				}
				if (fIncludeThis)
					list.Add(property);
			}
			Monitor.Exit(m_properties);

			return list.ToArray();
		}
		#endregion

		#region TraceSwitch methods
//		private void TraceVerbose(string s)
//		{
//			if(m_traceSwitch.TraceVerbose)
//				Trace.Write(s);
//		}
		private void TraceVerboseLine(string s)
		{
			if(m_traceSwitch.TraceVerbose)
				Trace.WriteLine("PTID="+System.Threading.Thread.CurrentThread.GetHashCode()+": "+s);
		}
//		private void TraceInfoLine(string s)
//		{
//			if(m_traceSwitch.TraceInfo || m_traceSwitch.TraceVerbose)
//				Trace.WriteLine("PTID="+System.Threading.Thread.CurrentThread.GetHashCode()+": "+s);
//		}

		#endregion
	}
}
