﻿// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace FwBuildTasks
{
	/// <summary>
	/// This class implements a complex process required to generate various resource DLLs for
	/// the localization of fieldworks. The main input of the process is a set of files kept
	/// under Localizations following the pattern messages.[locale].po, which contain
	/// translations of the many English strings in FieldWorks for the specified [locale]
	/// (e.g., fr, es).
	///
	/// We first apply some sanity checks, making sure String.Format markers like {0}, {1} etc
	/// have not been obviously mangled in translation.
	///
	/// The first stage of the process applies these substitutions to certain strings in
	/// DistFiles/Language Explorer/Configuration/strings-en.txt to produce
	/// a strings-[locale].txt for each locale (in the same place).
	///
	/// The second stage generates a resources.dll for each (non-test) project under Src, in
	/// Output/[config]/[locale]/[project].resources.dll. For example, in a release build
	/// (config), for the FDO project, for the French locale, it will generate
	/// Output/release/fr/FDO.resources.dll.
	///
	/// The second stage involves multiple steps to get to the resources DLL (done in parallel
	/// for each locale).
	/// - First we generate a lookup table (for an XSLT) which contains a transformation of the
	///   .PO file, Output/[locale].xml, e.g., Output/fr.xml.
	/// - For each non-test project,
	///     - for each .resx file in the project folder or a direct subfolder
	///         - apply an xslt, Build/LocalizeResx.xsl, to [path]/File.resx, producing
	///           Output[path]/[namespace].File.Strings.[locale].resx. (resx files are
	///           internally xml)
	///           (LocalizeResx.xsl includes the Output/fr.xml file created in the first step
	///           and uses it to translate appropriate items in the resx.)
	///           (Namespace is the fully qualified namespace of the project, which is made
	///           part of the filename.)
	///         - run an external program, resgen, which converts the resx to a .resources file
	///           in the same location, with otherwise the same name.
	///     - run an external program, al (assembly linker) which assembles all the .resources
	///       files into the final localized resources.dll
	/// </summary>
	public class LocalizeFieldWorks : Task
	{
		public LocalizeFieldWorks()
		{
			Config = "Release"; // a suitable default.
			Build = "All";
		}
		/// <summary>
		/// The directory in which it all happens, corresponding to the main ww directory in source control.
		/// </summary>
		[Required]
		public string RootDirectory { get; set; }

		/// <summary>
		/// What to build: Valid values are: SourceOnly, BinaryOnly, All
		/// </summary>
		public string Build { get; set; }

		/// <summary>
		/// The configuration we want to build (typically Release or Debug).
		/// </summary>
		public string Config { get; set; }

		static internal readonly string PoFileRelative = "Localizations"; // relative to root directory.

		internal string PoFileDirectory
		{
			get { return Path.Combine(RootDirectory, PoFileRelative); }
		}

		static internal readonly string PoFileLeadIn = "messages.";
		static internal readonly string PoFileExtension = ".po";

		internal static readonly string DistFilesFolderName = "DistFiles";
		internal static readonly string LExFolderName = "Language Explorer";
		internal static readonly string ConfigFolderName = "Configuration";
		internal static readonly string OutputFolderName = "Output";
		internal static readonly string SrcFolderName = "Src";
		internal static readonly string BldFolderName = "Build";

		internal static readonly string AssemblyInfoName = "CommonAssemblyInfo.cs";

		internal string AssemblyInfoPath
		{
			get { return Path.Combine(SrcFolder, AssemblyInfoName); }
		}

		internal string ConfigurationFolder
		{
			get
			{
				return Path.Combine(Path.Combine(Path.Combine(RootDirectory, DistFilesFolderName), LExFolderName),
					ConfigFolderName);
			}
		}

		internal string OutputFolder
		{
			get
			{
				return Path.Combine(RootDirectory, OutputFolderName);
			}
		}

		internal string SrcFolder
		{
			get
			{
				return Path.Combine(RootDirectory, SrcFolderName);
			}
		}

		internal string RealBldFolder
		{
			get { return Path.Combine(RealFwRoot, BldFolderName); }
		}

		internal bool BuildSource
		{
			get { return Build != "BinaryOnly"; }
		}

		internal bool BuildBinaries
		{
			get { return Build != "SourceOnly"; }
		}

		/// <summary>
		/// It happens to be the ConfigurationFolder, but let's keep things flexible. This one is the one where
		/// strings.en.xml (and the output strings.X.xml) live.
		/// </summary>
		internal string StringsXmlFolder
		{
			get { return ConfigurationFolder; }
		}

		internal static readonly string EnglishLocale = "en";

		internal static readonly string StringsXmlPattern = "strings-{0}.xml";
		private Localizer[] m_localizers;

		internal string StringsEnPath
		{
			get { return StringsXmlPath(EnglishLocale); }
		}

		/// <summary>
		/// The path where wer expect to store a file like strings-es.xml for a given locale.
		/// </summary>
		/// <param name="locale"></param>
		/// <returns></returns>
		internal string StringsXmlPath(string locale)
		{
			return Path.Combine(StringsXmlFolder, string.Format(StringsXmlPattern, locale));
		}


		/// <summary>
		/// The path where we expect to store a temporary form of the .PO file for a particular locale,
		/// such as Output/es.xml.
		/// </summary>
		internal string XmlPoFilePath(string locale)
		{
			return Path.Combine(OutputFolder, Path.ChangeExtension(locale, ".xml"));
		}

		/// <summary>
		/// The main entry point invoked by a line in the build script.
		/// </summary>
		/// <returns></returns>
		public override bool Execute()
		{
#if DEBUG
			string test = RealFwRoot;
			Debug.WriteLine("RealFwRoot => '{0}'", test);	// keeps compiler from complaining.
#endif
			Log.LogMessage(MessageImportance.Low, "PoFileDirectory is set to {0}.", PoFileDirectory);

			// Get all the .po files paths:
			string[] poFiles = Directory.GetFiles(PoFileDirectory, PoFileLeadIn + "*" + PoFileExtension);

			Log.LogMessage(MessageImportance.Low, "{0} .po files found.", poFiles.Length);

			// Prepare to get responses from processing each .po file
			m_localizers = new Localizer[poFiles.Length];

			Log.LogMessage(MessageImportance.Normal, "Generating localization assemblies...");
			// Main loop for each language:
			Parallel.ForEach(poFiles, currentFile =>
			{
				// Process current .po file:
				var localizer = new Localizer(currentFile, this);
				localizer.ProcessFile();

				// Slot current localizer into array at index matching current language.
				// This allows us to output any errors in a coherent manner.
				int index = Array.FindIndex(poFiles, poFile => (poFile == currentFile));
				if (index != -1)
					m_localizers[index] = localizer;
			}
			);

			bool buildFailed = false;
			// Output all processing results to console:
			for (int i = 0; i < poFiles.Length; i++)
			{
				if (m_localizers[i] == null)
				{
					LogError("ERROR: localization of " + poFiles[i] + " was not done!");
					buildFailed = true;
				}
				else
				{
					foreach (string message in m_localizers[i].Errors)
					{
						LogError(message);
						buildFailed = true;  // an error was reported, e.g., from Assembly Linker, that we didn't manage to make cause a return false.
					}
				}
			}

			// Decide if we succeeded or not:
			if (buildFailed)
				LogError("STOPPING BUILD - at least one localization build failed.");
			else
				Log.LogMessage(MessageImportance.Normal, "Finished generating localization assemblies.");

			return !buildFailed;
		}

		// overridden in tests to trap errors.
		internal virtual void LogError(string message)
		{
			Log.LogError(message);
		}

		/// <summary>
		/// In normal operation, this is the same as RootDirectory. In test, we find the real one, to allow us to
		/// find fixed files like LocalizeResx.xml
		/// </summary>
		internal virtual string RealFwRoot
		{
			get { return RootDirectory; }
		}

		// for testing only: get the project folders of the first Localizer
		internal List<string> GetProjectFolders()
		{
			List<string> result;
			m_localizers[0].GetProjectFolders(out result);
			return result;
		}

		internal virtual void RunResGen(string outputResourcePath, string localizedResxPath, string originalResxFolder)
		{
			using (var resgenProc = new Process())
			{
				resgenProc.StartInfo.UseShellExecute = false;
				resgenProc.StartInfo.RedirectStandardOutput = true;
				if (Environment.OSVersion.Platform == PlatformID.Unix)
				{
					resgenProc.StartInfo.FileName = "resgen";
					resgenProc.StartInfo.Arguments = Quote(localizedResxPath) + " " + Quote(outputResourcePath);
				}
				else
				{
					resgenProc.StartInfo.FileName = "resgen.exe";
					// It needs to be able to reference the appropriate System.Drawing.dll and System.Windows.Forms.dll to make the conversion.
					var clrFolder = RuntimeEnvironment.GetRuntimeDirectory();
					string drawingPath = Path.Combine(clrFolder, "System.Drawing.dll");
					string formsPath = Path.Combine(clrFolder, "System.Windows.Forms.dll");
					resgenProc.StartInfo.Arguments = Quote(localizedResxPath) + " " + Quote(outputResourcePath) + " /r:" + Quote(drawingPath) + " /r:" + Quote(formsPath);
				}
				// Setting the working directory to the folder containing the ORIGINAL resx file allows us to find included files
				// like FDO/Resources/Question.ico that the resx file refers to using relative paths.
				resgenProc.StartInfo.WorkingDirectory = originalResxFolder;
				resgenProc.Start();

				// This loop is needed to work around what seems to be a race condition in Mono
				do
				{
					resgenProc.StandardOutput.ReadToEnd();
					resgenProc.WaitForExit();
				} while (!resgenProc.HasExited);

				if (resgenProc.ExitCode != 0)
				{
					throw new ApplicationException("Resgen returned error " + resgenProc.ExitCode + " for " + localizedResxPath + ".");
				}
			}
		}

		string Quote(string input)
		{
			return "\"" + input + "\"";
		}

		/// <summary>
		/// Run the AssemblyLinker to create a resource DLL with the specified path and other details containing the specified embedded resources.
		/// </summary>
		/// <param name="outputDllPath"></param>
		/// <param name="culture"></param>
		/// <param name="fileversion"></param>
		/// <param name="productVersion"></param>
		/// <param name="version"></param>
		/// <param name="resources"></param>
		internal virtual void RunAssemblyLinker(string outputDllPath, string culture, string fileversion, string productVersion, string version, List<EmbedInfo> resources )
		{
			// Run assembly linker with the specified arguments
			Directory.CreateDirectory(Path.GetDirectoryName(outputDllPath)); // make sure the directory in which we want to make it exists.
			using (var alProc = new Process())
			{
				alProc.StartInfo.UseShellExecute = false;
				alProc.StartInfo.RedirectStandardOutput = true;
				if (Environment.OSVersion.Platform == PlatformID.Unix)
					alProc.StartInfo.FileName = "al";
				else
					alProc.StartInfo.FileName = "al.exe";
				alProc.StartInfo.Arguments = BuildLinkerArgs(outputDllPath, culture, fileversion, productVersion, version, resources);
				alProc.Start();

				alProc.StandardOutput.ReadToEnd();
				alProc.WaitForExit();
				if (alProc.ExitCode != 0)
				{
					throw new ApplicationException("Assembly linker returned error " + alProc.ExitCode + " for " + outputDllPath + ".");
				}
			}
		}

		internal string BuildLinkerArgs(string outputDllPath, string culture, string fileversion, string productVersion,
			string version, List<EmbedInfo> resources)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(" /out:");
			builder.Append(Quote(outputDllPath));
			foreach (var info in resources)
			{
				builder.Append(" /embed:");
				builder.Append(info.Resource);
				builder.Append(",");
				builder.Append(info.Name);
			}
			if (!String.IsNullOrEmpty(culture))
			{
				builder.Append(" /culture:");
				builder.Append(culture);
			}
			builder.Append(" /fileversion:");
			builder.Append(fileversion);
			builder.Append(" /productversion:");
			builder.Append(Quote(productVersion));
				// may be something like "8.4.2 beta 2" (see LT-14436). Test does not really cover this.
			builder.Append(" /version:");
			builder.Append(version);
			// Note: the old build process also set \target, but we want the default lib so don't need to be explicit.
			// the old version also had support for controlling verbosity; we can add that if needed.
			// May also want to set /config? The old version did not so I haven't.
			return builder.ToString();
		}
	}

	internal class EmbedInfo : Tuple<string, string>
	{
		public string Resource { get { return Item1; } }
		public string Name {get { return Item2; }}

		public EmbedInfo(string resource, string name)
			: base(resource, name)
		{
		}
	}

	/// <summary>
	/// This class exists to perform the work on one PO file.
	/// The main reason for having it is so that we can have member variables like CurrentFile,
	/// and so that data for one parallel task is quite separate from others.
	/// </summary>
	public class Localizer
	{
		private class ResourceInfo
		{
			public string ProjectFolder;
			public List<string> ResXFiles;
			public string RootNameSpace;
			public string AssemblyName;
		}

		public Localizer(string currentFile, LocalizeFieldWorks parent)
		{
			m_parentTask = parent;
			RootDirectory = m_parentTask.RootDirectory;
			CurrentFile = currentFile;
			var currentFileName = Path.GetFileName(currentFile);
			Locale = currentFileName.Substring(LocalizeFieldWorks.PoFileLeadIn.Length,
				currentFileName.Length - LocalizeFieldWorks.PoFileLeadIn.Length - LocalizeFieldWorks.PoFileExtension.Length);
			try
			{
				var x = new CultureInfo(Locale);
				LocaleIsSupported = true;
			}
			catch
			{
				Console.WriteLine("Warning: Culture name {0} is not supported.", Locale);
				LocaleIsSupported = false;
			}
		}

		private readonly LocalizeFieldWorks m_parentTask;

		public List<string> Errors = new List<string>();

		public string RootDirectory { get; set; }

		public string CurrentFile { get; set; }

		private string Locale { get; set; }
		private bool LocaleIsSupported { get; set; }

		private string Version { get; set; }
		private string FileVersion { get; set; }
		private string InformationVersion { get; set; }

		internal virtual void LogError(string message)
		{
			lock (Errors)
				Errors.Add(message);
		}

		public void ProcessFile()
		{
			try
			{
				if (m_parentTask.BuildSource)
				{
				if (!CheckForPoFileProblems())
					return;

				CreateStringsXml();

				CreateXmlMappingFromPo();
				}

				List<string> projectFolders;
				if (!GetProjectFolders(out projectFolders))
					return;

				var reader = new StreamReader(m_parentTask.AssemblyInfoPath, Encoding.UTF8);
				while (!reader.EndOfStream)
				{
					string line = reader.ReadLine();
					if (line == null)
						continue;
					if (line.StartsWith("[assembly: AssemblyFileVersion"))
						FileVersion = ExtractVersion(line);
					else if (line.StartsWith("[assembly: AssemblyInformationalVersionAttribute"))
						InformationVersion = ExtractVersion(line);
					else if (line.StartsWith("[assembly: AssemblyVersion"))
						Version = ExtractVersion(line);
				}
				reader.Close();
				if (string.IsNullOrEmpty(FileVersion))
					FileVersion = "0.0.0.0";
				if (string.IsNullOrEmpty(InformationVersion))
					InformationVersion = FileVersion;
				if (string.IsNullOrEmpty(Version))
					Version = FileVersion;

				Parallel.ForEach(projectFolders, ProcessProject);
			}
			catch (Exception ex)
			{
				LogError(String.Format("Caught exception processing {0}: {1}", Locale, ex.Message));
			}
		}

		private string ExtractVersion(string line)
		{
			int start = line.IndexOf("\"", StringComparison.Ordinal);
			int end = line.LastIndexOf("\"", StringComparison.Ordinal);
			return line.Substring(start + 1, end - start - 1);
		}

		private static List<string> GetResXFiles(string projectFolder)
		{
			var resxFiles = Directory.GetFiles(projectFolder, "*.resx").ToList();
			// include child folders, one level down, which do not have their own .csproj.
			foreach (var childFolder in Directory.GetDirectories(projectFolder))
			{
				if (Directory.GetFiles(childFolder, "*.csproj").Any())
					continue;
				resxFiles.AddRange(Directory.GetFiles(childFolder, "*.resx"));
			}
			return resxFiles;
		}

		private static ResourceInfo GetResourceInfo(string projectFolder)
		{
			var projectFile = Directory.GetFiles(projectFolder, "*.csproj").First(); // only called if there is exactly one.
			XDocument doc = XDocument.Load(projectFile);
			XNamespace ns = @"http://schemas.microsoft.com/developer/msbuild/2003";

			var resxFiles = GetResXFiles(projectFolder);
			if (resxFiles.Count == 0)
				return null;

			var resourceInfo = new ResourceInfo {
				ProjectFolder = projectFolder,
				ResXFiles = resxFiles,
				RootNameSpace = doc.Descendants(ns + "RootNamespace").First().Value,
				AssemblyName = doc.Descendants(ns + "AssemblyName").First().Value
			};

			return resourceInfo;
		}

		private void LocalizeResx(ResourceInfo resourceInfo, string resxPath)
		{
			var localizedResxPath = GetLocalizedResxPath(resourceInfo, resxPath);
			Directory.CreateDirectory(Path.GetDirectoryName(localizedResxPath));
			var stylesheet = Path.Combine(m_parentTask.RealBldFolder, "LocalizeResx.xsl");
			var parameters = new List<BuildUtils.XsltParam>();
			parameters.Add(new BuildUtils.XsltParam { Name = "lang", Value = Locale });
			// The output directory that the transform wants is not the one where it will write
			// the file, but the base Output directory, where it expects to find that we have
			// written the XML version of the PO file, [locale].xml.
			parameters.Add(new BuildUtils.XsltParam { Name = "outputdir", Value = m_parentTask.OutputFolder });
			//parameters.Add(new XsltParam() { Name = "verbose", Value = "true" });
			BuildUtils.ApplyXslt(stylesheet, resxPath, localizedResxPath, parameters);
		}

		private void CreateSources(ResourceInfo resourceInfo)
		{
			foreach (string resxFile in resourceInfo.ResXFiles)
			{
				m_parentTask.Log.LogMessage(MessageImportance.Low, "Creating source for {0}", resxFile);
				LocalizeResx(resourceInfo, resxFile);
				m_parentTask.Log.LogMessage(MessageImportance.Low, "Done creating source for {0}", resxFile);
			}
		}

		private void CreateResourceAssemblies(ResourceInfo resourceInfo)
		{
			if (resourceInfo.ResXFiles.Count == 0)
				return; // nothing to localize; in particular we should NOT call al with no inputs.

			var embedResources = new List<EmbedInfo>();
			foreach (string resxFile in resourceInfo.ResXFiles)
			{
				m_parentTask.Log.LogMessage(MessageImportance.Low, "Creating assembly for {0}", resxFile);
				string localizedResxPath = GetLocalizedResxPath(resourceInfo, resxFile);
				string localizedResourcePath = Path.ChangeExtension(localizedResxPath, ".resources");
				try
				{
					m_parentTask.RunResGen(localizedResourcePath, localizedResxPath,
						Path.GetDirectoryName(resxFile));
					embedResources.Add(new EmbedInfo(localizedResourcePath,
						Path.GetFileName(localizedResourcePath)));
				}
				catch (Exception ex)
				{
					LogError(String.Format("Error occurred while processing {0} for {1}: {2}",
						Path.GetFileName(resourceInfo.ProjectFolder), Locale, ex.Message));
				}
				m_parentTask.Log.LogMessage(MessageImportance.Low, "Done creating assembly for {0}", resxFile);
			}
			var resourceFileName = resourceInfo.AssemblyName + ".resources.dll";
			var mainDllFolder = Path.Combine(m_parentTask.OutputFolder, m_parentTask.Config);
			var localDllFolder = Path.Combine(mainDllFolder, Locale);
			string resourceDll = Path.Combine(localDllFolder, resourceFileName);
			string culture = LocaleIsSupported ? Locale : String.Empty;
			try
			{
				m_parentTask.Log.LogMessage(MessageImportance.Low, "Running AL for {0}", resourceDll);
				m_parentTask.RunAssemblyLinker(resourceDll, culture, FileVersion,
					InformationVersion, Version, embedResources);
				m_parentTask.Log.LogMessage(MessageImportance.Low, "Done running AL for {0}", resourceDll);
			}
			catch (Exception ex)
			{
				LogError(String.Format("Error occurred while processing {0} for {1}: {2}",
					Path.GetFileName(resourceInfo.ProjectFolder), Locale, ex.Message));
			}
		}

		private void ProcessProject(string projectFolder)
		{
			var resourceInfo = GetResourceInfo(projectFolder);
			if (resourceInfo == null || resourceInfo.ResXFiles.Count == 0)
				return; // nothing to localize; in particular we should NOT call al with no inputs.

			if (m_parentTask.BuildSource)
				CreateSources(resourceInfo);

			if (m_parentTask.BuildBinaries)
				CreateResourceAssemblies(resourceInfo);
		}

		private string GetLocalizedResxPath(ResourceInfo resourceInfo, string resxPath)
		{
			var resxFileName = Path.GetFileNameWithoutExtension(resxPath);
			string partialDir = Path.GetDirectoryName(resxPath.Substring(m_parentTask.SrcFolder.Length));
			string projectPartialDir = resourceInfo.ProjectFolder.Substring(m_parentTask.SrcFolder.Length);
			string outputFolder = Path.Combine(m_parentTask.OutputFolder, Locale) + partialDir;
			// This is the relative path from the project folder to the resx file folder.
			// It needs to go into the file name if not empty, but with a dot instead of folder separator.
			string subFolder = "";
			if (partialDir.Length > projectPartialDir.Length)
				subFolder = Path.GetFileName(partialDir) + ".";
			string fileName = resourceInfo.RootNameSpace + "." + subFolder + resxFileName + "." +
				Locale + ".resx";
			return Path.Combine(outputFolder, fileName);
		}

		internal bool GetProjectFolders(out List<string> projectFolders)
		{
			var root = m_parentTask.SrcFolder;
			projectFolders = new List<string>();
			return CollectInterestingProjects(root, projectFolders);
		}

		/// <summary>
		/// Collect interesting projects...returning false if we find a bad one (with two projects).
		/// </summary>
		/// <param name="root"></param>
		/// <param name="projectFolderCollector"></param>
		/// <returns><c>true</c> if the <paramref name="root"/> directory and it's subdirectories
		/// contain exactly 0 or 1 .csproj files per directory, <c>false</c> if there are two or
		/// more projects.</returns>
		private bool CollectInterestingProjects(string root, List<string> projectFolderCollector)
		{
			if (root.EndsWith("Tests"))
				return true;
			if (Path.GetFileName(root) == "SidebarLibrary")
				return true;
			if (Path.GetFileName(root) == "obj" || Path.GetFileName(root) == "bin")
				return true;
			foreach (var subfolder in Directory.EnumerateDirectories(root))
			{
				if (!CollectInterestingProjects(subfolder, projectFolderCollector))
					return false;
			}
			//for Mono 2.10.4, Directory.EnumerateFiles(...) seems to see only writeable files???
			//var projectFiles = Directory.EnumerateFiles(root, "*.csproj");
			var projectFiles = Directory.GetFiles(root, "*.csproj");
			if (projectFiles.Count() > 1)
			{
				LogError("Error: folder " + root + " has multiple .csproj files.");
				return false;
			}
			if (projectFiles.Count() == 1)
				projectFolderCollector.Add(root);
			return true;
		}

		private void CreateStringsXml()
		{
			var input = m_parentTask.StringsEnPath;
			var output = m_parentTask.StringsXmlPath(Locale);
			StoreLocalizedStrings(input, output);
		}

		private void StoreLocalizedStrings(string sEngFile, string sNewFile)
		{
			if (File.Exists(sNewFile))
				File.Delete(sNewFile);
			POString posHeader;
			Dictionary<string, POString> dictTrans = LoadPOFile(CurrentFile, out posHeader);
			if (dictTrans.Count == 0)
			{
				// Todo: test/convert
				Console.WriteLine("No translations found in PO file!");
				throw new Exception("VOID PO FILE");
			}
			XmlDocument xdoc = new XmlDocument();
			xdoc.Load(sEngFile);
			TranslateStringsElements(xdoc.DocumentElement, dictTrans);
			StoreTranslatedAttributes(xdoc.DocumentElement, dictTrans);
			StoreTranslatedLiterals(xdoc.DocumentElement, dictTrans);
			StoreTranslatedContextHelp(xdoc.DocumentElement, dictTrans);
			xdoc.Save(sNewFile);
		}

		/// <summary>
		/// This nicely recursive method replaces the English txt attribute values with the
		/// corresponding translated values if they exist.
		/// </summary>
		/// <param name="xel"></param>
		/// <param name="dictTrans"></param>
		// Copied from bin/src/LocaleStrings/Program.cs. Should remove this functionality there eventually.
		private static void TranslateStringsElements(XmlElement xel,
			Dictionary<string, POString> dictTrans)
		{
			if (xel.Name == "string")
			{
				POString pos;
				string sEnglish = xel.GetAttribute("txt");
				if (dictTrans.TryGetValue(sEnglish, out pos))
				{
					string sTranslation = pos.MsgStrAsString();
					xel.SetAttribute("txt", sTranslation);
					xel.SetAttribute("English", sEnglish);
				}
			}
			foreach (XmlNode xn in xel.ChildNodes)
			{
				if (xn is XmlElement)
					TranslateStringsElements(xn as XmlElement, dictTrans);
			}
		}

		// Copied from bin/src/LocaleStrings/Program.cs. Should remove this functionality there eventually.
		private static void StoreTranslatedAttributes(XmlElement xelRoot,
			Dictionary<string, POString> dictTrans)
		{
			XmlElement xelGroup = xelRoot.OwnerDocument.CreateElement("group");
			xelGroup.SetAttribute("id", "LocalizedAttributes");
			Dictionary<string, POString>.Enumerator en = dictTrans.GetEnumerator();
			while (en.MoveNext())
			{
				POString pos = en.Current.Value;
				string sValue = pos.MsgStrAsString();
				if (String.IsNullOrEmpty(sValue))
					continue;
				List<string> rgs = pos.AutoComments;
				if (rgs == null)
					continue;
				for (int i = 0; i < rgs.Count; ++i)
				{
					if (rgs[i] != null &&
						// handle bug in creating original POT file due to case sensitive search.
						(rgs[i].StartsWith("/") || rgs[i].StartsWith("file:///")) &&
						IsFromXmlAttribute(rgs[i]))
					{
						XmlElement xelString = xelRoot.OwnerDocument.CreateElement("string");
						xelString.SetAttribute("id", pos.MsgIdAsString());
						xelString.SetAttribute("txt", sValue);
						xelGroup.AppendChild(xelString);
						break;
					}
				}
			}
			xelRoot.AppendChild(xelGroup);
		}

		// Copied from bin/src/LocaleStrings/Program.cs. Should remove this functionality there eventually.
		private static void StoreTranslatedLiterals(XmlElement xelRoot,
		Dictionary<string, POString> dictTrans)
		{
			XmlElement xelGroup = xelRoot.OwnerDocument.CreateElement("group");
			xelGroup.SetAttribute("id", "LocalizedLiterals");
			Dictionary<string, POString>.Enumerator en = dictTrans.GetEnumerator();
			while (en.MoveNext())
			{
				POString pos = en.Current.Value;
				string sValue = pos.MsgStrAsString();
				if (String.IsNullOrEmpty(sValue))
					continue;
				List<string> rgs = pos.AutoComments;
				if (rgs == null)
					continue;
				for (int i = 0; i < rgs.Count; ++i)
				{
					if (rgs[i] != null && rgs[i].StartsWith("/") && rgs[i].EndsWith("/lit"))
					{
						XmlElement xelString = xelRoot.OwnerDocument.CreateElement("string");
						xelString.SetAttribute("id", pos.MsgIdAsString());
						xelString.SetAttribute("txt", sValue);
						xelGroup.AppendChild(xelString);
						break;
					}
				}
			}
			xelRoot.AppendChild(xelGroup);
		}


		// Copied from bin/src/LocaleStrings/Program.cs. Should remove this functionality there eventually.
		private static void StoreTranslatedContextHelp(XmlElement xelRoot,
			Dictionary<string, POString> dictTrans)
		{
			XmlElement xelGroup = xelRoot.OwnerDocument.CreateElement("group");
			xelGroup.SetAttribute("id", "LocalizedContextHelp");
			Dictionary<string, POString>.Enumerator en = dictTrans.GetEnumerator();
			while (en.MoveNext())
			{
				POString pos = en.Current.Value;
				string sValue = pos.MsgStrAsString();
				if (String.IsNullOrEmpty(sValue))
					continue;
				List<string> rgs = pos.AutoComments;
				if (rgs == null)
					continue;
				for (int i = 0; i < rgs.Count; ++i)
				{
					string sId = FindContextHelpId(rgs[i]);
					if (!String.IsNullOrEmpty(sId))
					{
						XmlElement xelString = xelRoot.OwnerDocument.CreateElement("string");
						xelString.SetAttribute("id", sId);
						xelString.SetAttribute("txt", sValue);
						xelGroup.AppendChild(xelString);
						break;
					}
				}
			}
			xelRoot.AppendChild(xelGroup);
		}

		// Copied from bin/src/LocaleStrings/Program.cs. Should remove this functionality there eventually.
		private static string FindContextHelpId(string sComment)
		{
			const string ksContextMarker = "/ContextHelp.xml::/strings/item[@id=\"";
			if (sComment != null &&
				sComment.StartsWith("/"))
			{
				int idx = sComment.IndexOf(ksContextMarker);
				if (idx > 0)
				{
					string sId = sComment.Substring(idx + ksContextMarker.Length);
					int idxEnd = sId.IndexOf('"');
					if (idxEnd > 0)
						return sId.Remove(idxEnd);
				}
			}
			return null;
		}

		private static bool IsFromXmlAttribute(string sComment)
		{
			int idx = sComment.LastIndexOf("/");
			if (idx < 0 || sComment.Length == idx + 1)
				return false;
			if (sComment[idx + 1] != '@')
				return false;
			else
				return sComment.Length > idx + 2;
		}

		private static Dictionary<string, POString> LoadPOFile(string sMsgFile, out POString posHeader)
		{
			using (StreamReader srIn = new StreamReader(sMsgFile, Encoding.UTF8))
			{
				Dictionary<string, POString> dictTrans = new Dictionary<string, POString>();
				posHeader = POString.ReadFromFile(srIn);
				POString pos = POString.ReadFromFile(srIn);
				while (pos != null)
				{
					if (!pos.HasEmptyMsgStr && (pos.Flags == null || !pos.Flags.Contains("fuzzy")))
						dictTrans.Add(pos.MsgIdAsString(), pos);
					pos = POString.ReadFromFile(srIn);
				}
				srIn.Close();
				return dictTrans;
			}
		}

		void CreateXmlMappingFromPo()
		{
			var output = m_parentTask.XmlPoFilePath(Locale);
			Directory.CreateDirectory(Path.GetDirectoryName(output));
			var converter = new Po2XmlConverter { PoFilePath = CurrentFile, XmlFilePath = output };
			converter.Run();
		}

		enum PoState
		{
			Start,		// beginning of file
			MsgId,		// msgid seen most recently
			MsgStr		// msgstr seen most recently
		};
		private bool CheckForPoFileProblems()
		{
			bool retval = true;
			var keys = new HashSet<string>();
			var state = PoState.Start;
			var currentId = String.Empty;
			var currentValue = string.Empty;
			foreach (var line in File.ReadLines(CurrentFile))
			{
				if (String.IsNullOrEmpty(line.Trim()) || line.StartsWith("#"))
					continue;
				// Check for translator using a look-alike character in place of digit 0 or 1 in string.Format control string.
				if (CheckForError(line, new Regex("{[oOlLiI]}"),
					"{0} contains a suspicious string ({1}) that is probably a mis-typed string substitution marker using a letter in place of digit 0 or 1"))
					retval = false;
				if (CheckForError(line, new Regex("[{}][0-9]{"),
					"{0} contains a suspicious string ({1}) that is probably a mis-typed string substitution marker with braces messed up"))
					retval = false;
				if (CheckForError(line, new Regex("}[0-9][{}]"),
					"{0} contains a suspicious string ({1}) that is probably a mis-typed string substitution marker with braces messed up"))
					retval = false;
				if (CheckForError(line, new Regex("^(msgid|msgstr)[^{]*[0-9]}"),
					"{0} contains a suspicious string in ({1}) that is probably a mis-typed string substitution marker with a missing opening brace"))
					retval = false;
				if (CheckForError(line, new Regex("{[0-9][^}]*$"),
					"{0} contains a suspicious string ({1}) that is probably a mis-typed string substitution marker with a missing closing brace"))
					retval = false;
				if (CheckForError(line, new Regex("^(msgid|msgstr)[ \\t]+[^\"]"),
					"{0} contains a suspicious line starting with ({1}) that is probably a key or value with missing required open quote"))
					retval = false;
				if (CheckForError(line, new Regex("^(msgid|msgstr)[ \\t]+\"[^\"]*$"),
					"{0} contains a suspicious line ({1}) that is probably a key or value with missing required closing quote"))
					retval = false;
				if (line.StartsWith("msgid"))
				{
					if (state == PoState.MsgStr)
					{
						// We've collected the full Id and Value, so check them.
						if (!CheckMsgidAndMsgstr(keys, currentId, currentValue))
							retval = false;
					}
					if (state == PoState.MsgId)
					{
						LogError(String.Format("{0} contains a key with no corresponding value: ({1})", CurrentFile, currentId));
						retval = false;
					}
					state = PoState.MsgId;
					currentId = ExtractMsgValue(line);
					currentValue = string.Empty;
				}
				else if (line.StartsWith("msgstr"))
				{
					currentValue = ExtractMsgValue(line);
					if (state != PoState.MsgId)
					{
						LogError(String.Format("{0} contains a value with no corresponding key: ({1})", CurrentFile, currentValue));
						retval = false;
					}
					state = PoState.MsgStr;
				}
				else if (state == PoState.MsgId)
				{
					var id = ExtractMsgValue(line);
					if (!String.IsNullOrEmpty(id))
						currentId = currentId + id;
				}
				else if (state == PoState.MsgStr)
				{
					var val = ExtractMsgValue(line);
					if (!String.IsNullOrEmpty(val))
						currentValue = currentValue + val;
				}
			}
			// We need to check the final msgid/msgstr pair.
			if (!CheckMsgidAndMsgstr(keys, currentId, currentValue))
				retval = false;
			return retval;
		}

		bool CheckForError(string contents, Regex pattern, string message)
		{
			var matches = pattern.Matches(contents);
			if (matches.Count == 0)
				return false; // all is well.
			LogError(string.Format(message, CurrentFile, matches[0].Value));
			return true;
		}

		string ExtractMsgValue(string line)
		{
			var idxMin = line.IndexOf('"');
			var idxLim = line.LastIndexOf('"');
			if (idxMin < 0 || idxLim <= idxMin)
				return string.Empty;
			++idxMin;	//step past the quote
			return line.Substring(idxMin, idxLim - idxMin);
		}

		bool CheckMsgidAndMsgstr(HashSet<string> keys, string msgid, string msgstr)
		{
			// allow empty data without complaint
			if (String.IsNullOrEmpty(msgid) && String.IsNullOrEmpty(msgstr))
				return true;
			if (keys.Contains(msgid))
			{
				LogError(string.Format("{0} contains a duplicate key: {1}", CurrentFile, msgid));
				return false;
			}
			keys.Add(msgid);
			var argRegEx = new Regex("{[0-9]}");
			int maxArg = -1;
			foreach (Match idmatch in argRegEx.Matches(msgid))
				maxArg = Math.Max(maxArg, Convert.ToInt32(idmatch.Value[1]));
			foreach (Match strmatch in argRegEx.Matches(msgstr))
			{
				if (Convert.ToInt32(strmatch.Value[1]) > maxArg)
				{
					LogError(String.Format(
						"{0} contains a key/value pair where the value ({1}) has more arguments than the key ({2})",
						CurrentFile, msgstr, msgid));
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// In normal operation, this is the same as RootDirectory. In test, we find the real one, to allow us to
		/// find fixed files like LocalizeResx.xml
		/// </summary>
		internal virtual string RealFwRoot
		{
			get { return m_parentTask.RealFwRoot; }
		}
	}
}
