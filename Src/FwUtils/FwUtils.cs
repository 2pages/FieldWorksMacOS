// Copyright (c) 2009-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Linq;
using Icu.Collation;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Utils;
using SIL.PlatformUtilities;
using SIL.Reporting;
using SIL.Settings;

namespace SIL.FieldWorks.Common.FwUtils
{
	/// <summary>
	/// Collection of miscellaneous utility methods needed for FieldWorks
	/// </summary>
	public static class FwUtils
	{
		private static string _FwMajorVersion;

		/// <summary>
		/// The current major version of FieldWorks. This is also known in FwKernelInterfaces/IcuWrappers.cs, InitIcuDataDir.
		/// </summary>
		public static string SuiteVersion
		{
			get
			{
				if (string.IsNullOrEmpty(_FwMajorVersion))
				{
					var gitVersionInformationType = Assembly.GetExecutingAssembly().GetType("GitVersionInformation");
					var versionField = gitVersionInformationType.GetField("Major");
					_FwMajorVersion = versionField.GetValue(null) as string;
				}

				return _FwMajorVersion;
			}
		}

		/// <summary>
		/// Generates a name suitable for use as a pipe name from the specified project handle.
		/// </summary>
		public static string GeneratePipeHandle(string handle)
		{
			const string ksSuiteIdPrefix = FwUtilsConstants.ksSuiteName + ":";
			return (handle.StartsWith(ksSuiteIdPrefix) ? string.Empty : ksSuiteIdPrefix) + handle.Replace('/', ':').Replace('\\', ':');
		}

		// On Linux, the default string output does not choose a font based on the characters in
		// the string, but on the current user interface locale.  At times, we want to display,
		// for example, Korean when the user interface locale is English.  By default, this
		// nicely displays boxes instead of characters.  Thus, we need some way to obtain the
		// correct font in such situations.  See FWNX-1069 for the obvious place this is needed.

		// These constants and enums are borrowed from fontconfig.h
		const string FC_FAMILY = "family";          /* String */
		const string FC_LANG = "lang";              /* String - RFC 3066 langs */

		private enum FcMatchKind
		{
			FcMatchPattern
		};

		private enum FcResult
		{
			FcResultMatch
		}
		[DllImport("libfontconfig.so.1")]
		static extern IntPtr FcPatternCreate();
		[DllImport("libfontconfig.so.1")]
		static extern int FcPatternAddString(IntPtr pattern, string fcObject, string stringValue);
		[DllImport("libfontconfig.so.1")]
		static extern void FcDefaultSubstitute(IntPtr pattern);
		[DllImport("libfontconfig.so.1")]
		static extern void FcPatternDestroy(IntPtr pattern);
		[DllImport("libfontconfig.so.1")]
		static extern int FcConfigSubstitute(IntPtr config, IntPtr pattern, FcMatchKind kind);
		[DllImport("libfontconfig.so.1")]
		static extern IntPtr FcFontMatch(IntPtr config, IntPtr pattern, out FcResult result);
		// Note that the output string from this method must NOT be freed, so we have to play games
		// with deferred marshalling.
		[DllImport("libfontconfig.so.1")]
		static extern FcResult FcPatternGetString(IntPtr pattern, string fcObject, int index, out IntPtr stringValue);

		/// <summary>
		/// Store the mapping from language to font to save future computation.
		/// </summary>
		static Dictionary<string, string> m_mapLangToFont = new Dictionary<string, string>();

		/// <summary>
		/// Get the name of the default font for the given language tag.
		/// </summary>
		/// <param name="lang">ISO 3066 tag for the language (e.g., "en", "es", "zh-CN", etc.)</param>
		/// <returns>Name of the font, or <c>null</c> if not found.</returns>
		public static string GetFontNameForLanguage(string lang)
		{
			if (Platform.IsWindows)
			{
				throw new PlatformNotSupportedException();
			}
			if (m_mapLangToFont.TryGetValue(lang, out var fontName))
			{
				return fontName;
			}
			var pattern = FcPatternCreate();
			var isOk = FcPatternAddString(pattern, FC_LANG, lang);
			if (isOk == 0)
			{
				FcPatternDestroy(pattern);
				return null;
			}
			isOk = FcConfigSubstitute(IntPtr.Zero, pattern, FcMatchKind.FcMatchPattern);
			if (isOk == 0)
			{
				FcPatternDestroy(pattern);
				return null;
			}
			FcDefaultSubstitute(pattern);
			var fullPattern = FcFontMatch(IntPtr.Zero, pattern, out var result);
			if (result != FcResult.FcResultMatch)
			{
				FcPatternDestroy(pattern);
				FcPatternDestroy(fullPattern);
				return null;
			}
			FcPatternDestroy(pattern);
			var fcRes = FcPatternGetString(fullPattern, FC_FAMILY, 0, out var res);
			if (fcRes == FcResult.FcResultMatch)
			{
				fontName = Marshal.PtrToStringAuto(res);
				m_mapLangToFont.Add(lang, fontName);
			}
			FcPatternDestroy(fullPattern);
			return fontName;
		}

		public static void CheckResumeProcessing(int currentCount, string classname, string suspendMethodName = "SuspendIdleProcessing", string resumeMethodName = "ResumeIdleProcessing")
		{
			if (currentCount <= 0)
			{
				// Thou shalt not call resume more times than suspend is called.
				throw new InvalidOperationException($"'{resumeMethodName}' has been called more times than the matching '{suspendMethodName}' method on class '{classname}'");
			}
		}

		/// <summary>
		/// Return true if the application is in the process of shutting down after a crash.
		/// Some settings should not be saved in this case.
		/// </summary>
		public static bool InCrashedState { get; set; }

		/// <summary>
		/// Whenever possible use this in place of new PalasoWritingSystemManager.
		/// It sets the TemplateFolder, which unfortunately the constructor cannot do because
		/// the direction of our dependencies does not allow it to reference FwUtils and access FwDirectoryFinder.
		/// </summary>
		/// <returns></returns>
		public static WritingSystemManager CreateWritingSystemManager()
		{
			return new WritingSystemManager { TemplateFolder = FwDirectoryFinder.TemplateDirectory };
		}

		/// <summary>
		/// Initialize the ICU Data Dir. If necessary, adds the architecture-appropriate ICU DLL's to the PATH.
		/// </summary>
		public static void InitializeIcu()
		{
			// Set ICU_DATA environment variable
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ICU_DATA")))
			{
				// We read the registry value and set an environment variable ICU_DATA here so that
				// FwKernelInterfaces.dll is independent of WinForms.
				// ENHANCE: store data directory somewhere else other than registry (user.config
				// file?) and use that.
				var icuDirValueName = $"Icu{CustomIcu.Version}DataDir";
				using (var userKey = RegistryHelper.CompanyKey)
				using (var machineKey = RegistryHelper.CompanyKeyLocalMachine)
				{
					string dir = null;
					if (userKey?.GetValue(icuDirValueName) != null)
					{
						dir = userKey.GetValue(icuDirValueName, null) as string;
					}
					else if (machineKey?.GetValue(icuDirValueName) != null)
					{

						dir = machineKey.GetValue(icuDirValueName, null) as string;
					}
					if (!string.IsNullOrEmpty(dir))
					{
						Environment.SetEnvironmentVariable("ICU_DATA", dir);
					}
				}
			}
			// ICU_DATA should point to the directory that contains nfc_fw.nrm and nfkc_fw.nrm
			// (i.e. icudt54l).
			CustomIcu.InitIcuDataDir();
		}

		/// <summary>
		/// Creates a collator using Icu Locale for the writing system if possible
		/// </summary>
		/// <returns>A collator for the writing system, or null</returns>
		public static Collator GetCollatorForWs(string sWs)
		{
			Collator col = null;
			try
			{
				var icuLocale = new Icu.Locale(sWs).Name;
				col = Collator.Create(icuLocale);
			}
			catch (Exception)
			{
				// If we can't create a collator for this writing system cache a null, we won't be able to next time either
			}

			return col;
		}

		/// <summary>
		/// Determines whether the window handle is a child window of the specified parent form.
		/// </summary>
		public static bool IsChildWindowOfForm(Form parent, IntPtr hWnd)
		{
			if (parent == null)
			{
				return false;
			}
			// Try to get the managed window for the handle. We will get one only if hWnd is
			// a child window of this application, so we can return true.
			if (Control.FromHandle(hWnd) != null)
			{
				return true;
			}
			// Otherwise hWnd might be the handle of an unmanaged window. We look at all
			// parents and grandparents... to see if we eventually belong to the application
			// window.
			var hWndParent = hWnd;
			while (hWndParent != IntPtr.Zero)
			{
				hWnd = hWndParent;
				hWndParent = Win32.GetParent(hWnd);
			}
			return (hWnd == parent.Handle);
		}

		/// <summary>
		/// Finds the first control of the given name under the parentControl.
		/// </summary>
		public static Control FindControl(Control parentControl, string nameOfChildToFocus)
		{
			if (string.IsNullOrEmpty(nameOfChildToFocus))
			{
				return null;
			}
			if (parentControl.Name == nameOfChildToFocus)
			{
				return parentControl;
			}
			var controls = parentControl.Controls.Find(nameOfChildToFocus, true);
			return controls.Length > 0 ? controls[0] : null;
		}

		/// <summary>
		/// Replace underline character with ampersand.
		/// </summary>
		public static string ReplaceUnderlineWithAmpersand(string guiString)
		{
			return guiString.Replace("_", "&");
		}

		/// <summary>
		/// Remove underline character.
		/// </summary>
		public static string RemoveUnderline(string guiString)
		{
			return guiString.Replace("_", string.Empty);
		}

		/// <summary>
		/// Unit tests can set this to true to suppress error beeps
		/// </summary>
		public static bool SuppressErrorBeep { get; set; }

		public static void ErrorBeep()
		{
			if (!SuppressErrorBeep)
			{
				SystemSounds.Beep.Play();
			}
		}

		#region Advapi32.dll
		// Requires Windows NT 3.1 or later
		// From http://www.codeproject.com/useritems/processownersid.asp

		private const int TOKEN_QUERY = 0X00000008;

		private enum TOKEN_INFORMATION_CLASS
		{
			TokenUser = 1
		}

		/// <summary>The TOKEN_USER structure identifies the user associated with an access token.</summary>
		[StructLayout(LayoutKind.Sequential)]
		private struct TOKEN_USER
		{
			/// <summary>Specifies a SID_AND_ATTRIBUTES structure representing the user
			/// associated with the access token. There are currently no attributes defined for
			/// user security identifiers (SIDs).</summary>
			public readonly SID_AND_ATTRIBUTES User;
		}

		/// <summary>The SID_AND_ATTRIBUTES structure represents a security identifier (SID) and
		/// its attributes. SIDs are used to uniquely identify users or groups.</summary>
		/// <remarks>
		/// Only used by TOKEN_USER private struct.
		/// </remarks>
		[StructLayout(LayoutKind.Sequential)]
		private struct SID_AND_ATTRIBUTES
		{
			/// <summary>Pointer to a SID structure. </summary>
			public readonly IntPtr Sid;
		}

		/// <summary>
		/// The OpenProcessToken function opens the access token associated with a process.
		/// </summary>
		/// <param name="processHandle">[in] Handle to the process whose access token is opened.
		/// The process must have the PROCESS_QUERY_INFORMATION access permission. </param>
		/// <param name="desiredAccess">[in] Specifies an access mask that specifies the
		/// requested types of access to the access token. These requested access types are
		/// compared with the discretionary access control list (DACL) of the token to determine
		/// which accesses are granted or denied. </param>
		/// <param name="tokenHandle">[out] Pointer to a handle that identifies the newly opened
		/// access token when the function returns. </param>
		/// <returns>If the function succeeds, the return value is <c>true</c>.
		/// If the function fails, the return value is <c>false</c>. To get extended error
		/// information, call GetLastError.</returns>
		[DllImport("advapi32")]
		private static extern bool OpenProcessToken(IntPtr processHandle, int desiredAccess, out IntPtr tokenHandle);

		/// <summary>
		/// The GetTokenInformation function retrieves a specified type of information about an
		/// access token. The calling process must have appropriate access rights to obtain the
		/// information.
		/// </summary>
		/// <param name="hToken">[in] Handle to an access token from which information is
		/// retrieved. If TokenInformationClass specifies TokenSource, the handle must have
		/// TOKEN_QUERY_SOURCE access. For all other TokenInformationClass values, the handle
		/// must have TOKEN_QUERY access.</param>
		/// <param name="tokenInfoClass">[in] Specifies a value from the TOKEN_INFORMATION_CLASS
		/// enumerated type to identify the type of information the function retrieves.</param>
		/// <param name="tokenInformation">[out] Pointer to a buffer the function fills with the
		/// requested information. The structure put into this buffer depends upon the type of
		/// information specified by the TokenInformationClass parameter (see MSDN).</param>
		/// <param name="tokeInfoLength">[in] Specifies the size, in bytes, of the buffer
		/// pointed to by the TokenInformation parameter. If TokenInformation is <c>null</c>,
		/// this parameter must be zero.</param>
		/// <param name="returnLength"><para>[out] Pointer to a variable that receives the number of
		/// bytes needed for the buffer pointed to by the TokenInformation parameter. If this
		/// value is larger than the value specified in the TokenInformationLength parameter,
		/// the function fails and stores no data in the buffer.</para>
		/// <para>If the value of the TokenInformationClass parameter is TokenDefaultDacl and
		/// the token has no default DACL, the function sets the variable pointed to by
		/// ReturnLength to sizeof(TOKEN_DEFAULT_DACL) and sets the DefaultDacl member of the
		/// TOKEN_DEFAULT_DACL structure to NULL.</para></param>
		/// <returns>If the function succeeds, the return value is <c>true</c>.
		/// If the function fails, the return value is <c>false</c>. To get extended error
		/// information, call GetLastError.</returns>
		[DllImport("advapi32", CharSet = CharSet.Auto)]
		private static extern bool GetTokenInformation(IntPtr hToken, TOKEN_INFORMATION_CLASS tokenInfoClass, IntPtr tokenInformation, int tokeInfoLength, out int returnLength);

		/// <summary>
		/// The CloseHandle function closes an open object handle.
		/// </summary>
		/// <param name="handle">[in] Handle to an open object. This parameter can be a pseudo
		/// handle or INVALID_HANDLE_VALUE. </param>
		/// <returns><c>true</c> if the function succeeds, otherwise <c>false</c>.</returns>
		[DllImport("kernel32")]
		private static extern bool CloseHandle(IntPtr handle);

		/// <summary>
		/// The ConvertSidToStringSid function converts a security identifier (SID) to a string
		/// format suitable for display, storage, or transmission.
		/// </summary>
		/// <param name="sid">[in] Pointer to the SID structure to convert.</param>
		/// <param name="stringSid">[out] The SID string.</param>
		/// <returns><c>true</c> if the function succeeds, otherwise <c>false</c>.</returns>
		[DllImport("advapi32", CharSet = CharSet.Auto)]
		private static extern bool ConvertSidToStringSid(IntPtr sid, [MarshalAs(UnmanagedType.LPTStr)] out string stringSid);

		#endregion

		#region Helper methods

		/// <summary>
		/// Gets the user SID for the given process token.
		/// </summary>
		/// <param name="hToken">The process token.</param>
		/// <returns>The SID of the user that owns the process, or <c>IntPtr.Zero</c> if we
		/// can't get the user information (e.g. insufficient permissions to retrieve this
		/// information)</returns>
		private static IntPtr GetSidForProcessToken(IntPtr hToken)
		{
			const int bufferLen = 256;
			var buffer = Marshal.AllocHGlobal(bufferLen);

			try
			{
				return GetTokenInformation(hToken, TOKEN_INFORMATION_CLASS.TokenUser, buffer, bufferLen, out _) ? ((TOKEN_USER)Marshal.PtrToStructure(buffer, typeof(TOKEN_USER))).User.Sid : IntPtr.Zero;
			}
			catch (Exception ex)
			{
				// Just ignore exceptions and return false
				Debug.WriteLine("ProcessTokenToSid failed: " + ex.Message);
				return IntPtr.Zero;
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		#endregion

		/// <summary>
		/// Gets the user for the given process.
		/// </summary>
		public static string GetUserForProcess(Process process)
		{
			// Verify permission to read process Handle
			IntPtr procHandle;
			try
			{
				procHandle = process.Handle;
			}
			catch (InvalidOperationException)
			{
				return string.Empty;
			}
			try
			{
				if (Platform.IsDotNet)
				{
					string sidString = null;
					if (OpenProcessToken(procHandle, TOKEN_QUERY, out var procToken))
					{
						var sid = GetSidForProcessToken(procToken);
						if (sid != IntPtr.Zero)
						{
							ConvertSidToStringSid(sid, out sidString);
						}
						CloseHandle(procToken);
					}
					return sidString;
				}

				return process.StartInfo.UserName;
			}
			catch (Exception ex)
			{
				Debug.WriteLine("GetUserForProcess failed: " + ex.Message);
				return string.Empty;
			}
		}

		/// <summary>
		/// Determines whether the exception <paramref name="e"/> is caused by an unsupported
		/// culture, i.e. a culture that neither Windows nor .NET support and there is no
		/// custom culture (see LT-8248).
		/// </summary>
		public static bool IsUnsupportedCultureException(Exception e)
		{
			return e is CultureNotFoundException;
		}

		/// <summary>
		/// If necessary, append a number to make the filename unique, maintaining original extension.
		/// </summary>
		/// <param name="directoryPath">Path to directory where unique filename is needed</param>
		/// <param name="desiredFilename">Seed name with which to begin</param>
		/// <returns>A unique path/filename within <paramref name="directoryPath"/>.
		/// It may have a number appended to <paramref name="desiredFilename"/>, or it may be <paramref name="desiredFilename"/>.
		/// </returns>
		/// <remarks>Patterned off of SIL.IO.PathHelper.GetUniqueFolderPath(), which unfortunately only applies to folders.
		/// This maybe fits better in Folders.cs, but again, it's not folders, but files.</remarks>
		public static string GetUniqueFilename(string directoryPath, string desiredFilename)
		{
			var i = 0;
			Debug.Assert(Directory.Exists(directoryPath), "Param 'directoryPath' doesn't lead to a valid directory.");
			var extension = Path.GetExtension(desiredFilename);
			var nameNoExtension = Path.GetFileNameWithoutExtension(desiredFilename);
			var filename = Path.Combine(directoryPath, desiredFilename);
			while (File.Exists(filename))
			{
				filename = Path.Combine(directoryPath, $"{nameNoExtension}{++i}{extension}");
			}
			return filename;
		}

		/// <summary>
		/// Get the stylesheet from an IPropertyRetriever.
		/// </summary>
		public static LcmStyleSheet StyleSheetFromPropertyTable(IPropertyRetriever propertyTable)
		{
			return propertyTable.GetValue<LcmStyleSheet>(FwUtilsConstants.FlexStyleSheet);
		}

		/// <summary>
		/// If the given folder path is in the "My Documents" folder, trim the "My Documents" portion off the path.
		/// </summary>
		/// <param name="sDir">The name of the path to try to shorten.</param>
		/// <returns>The (potentially) trimmed path name</returns>
		public static string ShortenMyDocsPath(string sDir)
		{
			var sMyDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			if (sDir.ToLowerInvariant().StartsWith(sMyDocs.ToLowerInvariant()))
			{
				var idx = sMyDocs.LastIndexOf(Path.DirectorySeparatorChar);
				return sDir.Substring(idx + 1);
			}
			return sDir;
		}

		/// <summary>
		/// Gets the boot drive, based on the location of the system directory. (Note: This
		/// includes the colon and backslash in the return value.)
		/// </summary>
		/// <remarks>This returns an empty string on Linux (unless environment variable
		/// 'windir' happens to be set).</remarks>
		public static string BootDrive => Path.GetPathRoot(Environment.GetEnvironmentVariable("windir"));

		/// <summary>
		/// Migrates the old CoreImpl config section to the new settings if necessary.
		/// </summary>
		public static bool MigrateIfNecessary(Stream stream, IFwApplicationSettings applicationSettings)
		{
			var configDoc = XDocument.Load(stream);
			stream.Position = 0;
			var userSettingsGroupElem = configDoc.Root?.Element("configSections")?.Elements("sectionGroup").FirstOrDefault(e => (string)e.Attribute("name") == "userSettings");
			var coreImplSectionElem = userSettingsGroupElem?.Elements("section").FirstOrDefault(e => (string)e.Attribute("name") == "SIL.CoreImpl.Properties.Settings");
			if (coreImplSectionElem != null)
			{
				var coreImplElem = configDoc.Root.Element("userSettings")?.Element("SIL.CoreImpl.Properties.Settings");
				if (coreImplElem != null)
				{
					foreach (var settingElem in coreImplElem.Elements("setting"))
					{
						var valueElem = settingElem.Element("value");
						if (valueElem == null)
						{
							continue;
						}
						switch ((string)settingElem.Attribute("name"))
						{
							case "UpdateGlobalWSStore":
								applicationSettings.UpdateGlobalWSStore = (bool)valueElem;
								break;
							case "WebonaryUser":
								applicationSettings.WebonaryUser = (string)valueElem;
								break;
							case "WebonaryPass":
								applicationSettings.WebonaryPass = (string)valueElem;
								break;
							case "Reporting":
								using(var reader = valueElem.CreateReader())
								{
									reader.MoveToContent();
									var xml = reader.ReadInnerXml();
									applicationSettings.Reporting = Xml.XmlSerializationHelper.DeserializeFromString<ReportingSettings>(xml);
								}
								break;
							case "LocalKeyboards":
								applicationSettings.LocalKeyboards = (string)valueElem;
								break;
							case "Update":
								using(var reader = valueElem.CreateReader())
								{
									reader.MoveToContent();
									var xml = reader.ReadInnerXml();
									applicationSettings.Update = Xml.XmlSerializationHelper.DeserializeFromString<UpdateSettings>(xml);
								}
								break;
						}
					}
					coreImplElem.Remove();
				}
				coreImplSectionElem.Remove();

				stream.SetLength(0);
				configDoc.Save(stream);
				stream.Position = 0;
				return true;
			}
			return false;
		}

		/// <summary>
		/// Return an ITsTextProps typically used for WS labels in views.
		/// </summary>
		public static ITsTextProps LanguageCodeTextProps(int defaultWs)
		{
			const int red = 47;
			const int green = 96;
			const int blue = 255;
			var tpb = TsStringUtils.MakePropsBldr();
			tpb.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, defaultWs);
			tpb.SetIntPropValues((int)FwTextPropType.ktptForeColor, (int)FwTextPropVar.ktpvDefault, red + (blue * 256 + green) * 256);
			tpb.SetIntPropValues((int)FwTextPropType.ktptFontSize, (int)FwTextPropVar.ktpvMilliPoint, 8000);
			tpb.SetIntPropValues((int)FwTextPropType.ktptEditable, (int)FwTextPropVar.ktpvEnum, (int)TptEditable.ktptNotEditable);
			tpb.SetStrPropValue((int)FwTextPropType.ktptNamedStyle, StyleServices.UiElementStylename);
			return tpb.GetTextProps();
		}

		public static bool TryToDeleteFile(object param)
		{
			try
			{
				// I'm not sure why, but if we try to delete it right away, we typically get a failure,
				// with an exception indicating that something is using the file, despite the code above that
				// tries to make our picture cache let go of it.
				// However, waiting until idle seems to solve the problem.
				File.Delete((string)param);
			}
			catch (IOException)
			{
				// If we can't actually delete the file for some reason, don't bother the user complaining.
			}
			return true; // task is complete, don't try again.
		}

		/// <summary>
		/// Fetches the GUID value of the given property, having checked it is a valid object.
		/// If it is not a valid object, the property is removed.
		/// </summary>
		public static Guid GetObjectGuidIfValid(IPropertyTable propertyTable, string key)
		{
			var sGuid = propertyTable.GetValue<string>(key);
			if (string.IsNullOrEmpty(sGuid))
			{
				return Guid.Empty;
			}
			if (!Guid.TryParse(sGuid, out var guid))
			{
				return Guid.Empty;
			}
			var lcmCache = propertyTable.GetValue<LcmCache>(FwUtilsConstants.cache);
			if (lcmCache.ServiceLocator.ObjectRepository.IsValidObjectId(guid))
			{
				return guid;
			}
			propertyTable.RemoveProperty(key);
			return Guid.Empty;
		}
	}
}
