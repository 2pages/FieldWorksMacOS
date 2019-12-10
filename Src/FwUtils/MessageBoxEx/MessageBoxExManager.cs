// Copyright (c) 2005-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
// From:http://www.codeproject.com/cs/miscctrl/MessageBoxEx.asp

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace SIL.FieldWorks.Common.FwUtils.MessageBoxEx
{
	/// <summary>
	/// Manages a collection of MessageBoxes. Basically manages the
	/// saved response handling for messageBoxes.
	/// </summary>
	public static class MessageBoxExManager
	{
		#region Fields
		private static readonly Dictionary<string, MessageBoxEx> s_messageBoxes = new Dictionary<string, MessageBoxEx>();
		private static readonly Dictionary<string, string> s_standardButtonsText = new Dictionary<string, string>();
		#endregion

		#region Static ctor
		static MessageBoxExManager()
		{
			s_standardButtonsText[MessageBoxExButtons.OK.ToString()] = FwUtilsStrings.Ok;
			s_standardButtonsText[MessageBoxExButtons.Cancel.ToString()] = FwUtilsStrings.Cancel;
			s_standardButtonsText[MessageBoxExButtons.Yes.ToString()] = FwUtilsStrings.Yes;
			s_standardButtonsText[MessageBoxExButtons.No.ToString()] = FwUtilsStrings.No;
			s_standardButtonsText[MessageBoxExButtons.Abort.ToString()] = FwUtilsStrings.Abort;
			s_standardButtonsText[MessageBoxExButtons.Retry.ToString()] = FwUtilsStrings.Retry;
			s_standardButtonsText[MessageBoxExButtons.Ignore.ToString()] = FwUtilsStrings.Ignore;
		}
		#endregion

		#region Methods

		/// <summary/>
		public static void DefineMessageBox(string triggerName, string caption, string text, bool displayDontShowAgainButton, string iconName)
		{
			// Don't dispose msgBox here. MessageBoxExManager holds a reference to it and will dispose it eventually.
			MessageBoxEx msgBox;
			try
			{
				msgBox = CreateMessageBox(triggerName);
			}
			catch (ArgumentException)
			{
				return;
				//this message box library throws an exception if you have already defined this triggerName.
				//for us, this might just mean that you opened another window the same kind or something like that.
			}

			msgBox.Caption = caption;
			msgBox.Text = text;
			msgBox.SaveResponseText = FwUtilsStrings.DonTShowThisAgain;
			msgBox.AllowSaveResponse = displayDontShowAgainButton;
			msgBox.UseSavedResponse = displayDontShowAgainButton;
			switch (iconName)
			{
				case "info":
					msgBox.Icon = MessageBoxExIcon.Information;
					break;
				case "exclamation":
					msgBox.Icon = MessageBoxExIcon.Exclamation;
					break;
			}
		}

		/// <summary>
		/// Creates and returns a reference to a new message box with the specified name. The
		/// caller does not have to dispose the message box.
		/// </summary>
		/// <param name="name">The name of the message box</param>
		/// <returns>A new message box</returns>
		/// <remarks>If <c>null</c> is specified as the message name then the message box is not
		/// managed by the Manager and will be disposed automatically after a call to Show().
		/// Otherwise the message box is managed by MessageBoxExManager who will eventually
		/// dispose it.</remarks>
		public static MessageBoxEx CreateMessageBox(string name)
		{
			if (name == null)
			{
				throw new ArgumentNullException(nameof(name), "parameter cannot be null");
			}
			if (s_messageBoxes.ContainsKey(name))
			{
				var err = $"A MessageBox with the name {name} already exists.";
				throw new ArgumentException(err, nameof(name));
			}
			var msgBox = new MessageBoxEx
			{
				Name = name
			};
			s_messageBoxes[name] = msgBox;

			return msgBox;
		}

		/// <summary>
		/// load with properties stored
		///  in the settings file, if that file is found.
		/// </summary>
		public static void ReadSettingsFile()
		{
			var path = FwDirectoryFinder.CommonAppDataFolder("FieldWorks");
			if (!File.Exists(path))
			{
				return;
			}
			StreamReader reader = null;
			try
			{
				var szr = new XmlSerializer(typeof(StringPair[]));
				reader = new StreamReader(path);
				var list = (StringPair[])szr.Deserialize(reader);
				ReadStringPairArrayForDeserializing(list);
			}
			catch (FileNotFoundException)
			{
				//don't do anything
			}
			catch (Exception)
			{
				var activeForm = Form.ActiveForm;
				if (activeForm == null)
				{
					MessageBoxUtils.Show(FwUtilsStrings.CannotRestoreSavedResponses);
				}
				else
				{
					// Make sure as far as possible it comes up in front of any active window, including the splash screen.
					activeForm.Invoke((Func<DialogResult>)(() => MessageBoxUtils.Show(activeForm, FwUtilsStrings.CannotRestoreSavedResponses, string.Empty)));
				}
			}
			finally
			{
				reader?.Dispose();
			}
		}

		private static void ReadStringPairArrayForDeserializing(StringPair[] list)
		{
			foreach (var pair in list)
			{
				//I know it is strange, but the serialization code will give us a
				//	null property if there were no other properties.
				if (pair != null)
				{
					AddSavedResponsesSafely(SavedResponses, pair);
				}
			}
		}

		private static void AddSavedResponsesSafely(Dictionary<string, string> responseDict, StringPair pair)
		{
			// The original code here threw an exception if the pair key was already in the dictionary.
			// We don't want to overwrite what's in memory with what's on disk, so we'll skip them in that case.
			string dummyValue;
			if (responseDict.TryGetValue(pair.key, out dummyValue))
			{
				return;
			}
			responseDict.Add(pair.key, pair.value);
		}

		/// <summary />
		public static Dictionary<string, string> SavedResponses { get; } = new Dictionary<string, string>();

		/// <summary />
		public static string Trigger(string triggerName)
		{
			// Don't dispose the msgbox here - we store it and re-use it. Will be disposed in our Dispose() method.
			var msgBox = GetMessageBox(triggerName);
			if (msgBox == null)
			{
				throw new ApplicationException($"Could not find the message box with trigger name = {triggerName}");
			}
			return msgBox.Show();
		}

		/// <summary>
		/// Gets the message box with the specified name
		/// </summary>
		/// <param name="name">The name of the message box to retrieve</param>
		/// <returns>The message box with the specified name or null if a message box
		/// with that name does not exist</returns>
		public static MessageBoxEx GetMessageBox(string name)
		{
			MessageBoxEx result;
			s_messageBoxes.TryGetValue(name, out result);
			return result;
		}

		/// <summary>
		/// Deletes the message box with the specified name
		/// </summary>
		/// <param name="name">The name of the message box to delete</param>
		public static void DeleteMessageBox(string name)
		{
			if (name == null)
			{
				return;
			}
			MessageBoxEx msgBox;
			if (s_messageBoxes.TryGetValue(name, out msgBox))
			{
				s_messageBoxes.Remove(name);
				msgBox.Dispose();
			}
		}

		/// <summary>
		/// Disposes all stored message boxes
		/// </summary>
		public static void DisposeAllMessageBoxes()
		{
			foreach (var messageBox in s_messageBoxes.Values)
			{
				messageBox.Dispose();
			}
			s_messageBoxes.Clear();
		}

		/// <summary>
		/// Reset the saved response for the message box with the specified name.
		/// </summary>
		/// <param name="messageBoxName">The name of the message box whose response is to be reset.</param>
		public static void ResetSavedResponse(string messageBoxName)
		{
			if (messageBoxName == null)
			{
				return;
			}
			SavedResponses.Remove(messageBoxName);
		}

		/// <summary>
		/// Resets the saved responses for all message boxes that are managed by the manager.
		/// </summary>
		public static void ResetAllSavedResponses()
		{
			SavedResponses.Clear();
		}
		#endregion

		#region Internal Methods
		/// <summary>
		/// Set the saved response for the specified message box
		/// </summary>
		/// <param name="msgBox">The message box whose response is to be set</param>
		/// <param name="response">The response to save for the message box</param>
		internal static void SetSavedResponse(MessageBoxEx msgBox, string response)
		{
			if (msgBox.Name == null)
			{
				return;
			}
			SavedResponses[msgBox.Name] = response;
		}

		/// <summary>
		/// Gets the saved response for the specified message box
		/// </summary>
		/// <param name="msgBox">The message box whose saved response is to be retrieved</param>
		/// <returns>The saved response if exists, null otherwise</returns>
		internal static string GetSavedResponse(MessageBoxEx msgBox)
		{
			var msgBoxName = msgBox.Name;
			if (msgBoxName == null)
			{
				return null;
			}
			string result = null;
			if (SavedResponses.ContainsKey(msgBoxName))
			{
				result = SavedResponses[msgBoxName];
			}
			return result;
		}

		/// <summary>
		/// Returns the localized string for standard button texts like,
		/// "Ok", "Cancel" etc.
		/// </summary>
		internal static string GetLocalizedString(string key)
		{
			string result = null;
			if (s_standardButtonsText.ContainsKey(key))
			{
				result = s_standardButtonsText[key];
			}
			return result;
		}
		#endregion

		/// <summary />
		[Serializable]
		private sealed class StringPair
		{
			/// <summary>
			/// required for XML serialization
			/// </summary>
			public StringPair()
			{
			}

			/// <summary />
			public string key;
			/// <summary />
			public string value;
		}
	}
}