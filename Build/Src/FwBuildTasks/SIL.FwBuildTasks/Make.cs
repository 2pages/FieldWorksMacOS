// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FwBuildTasks
{
	public class Make : ToolTask
	{
		public Make()
		{
		}

		/// <summary>
		/// Gets or sets the path to the makefile.
		/// </summary>
		[Required]
		public string Makefile { get; set; }

		/// <summary>
		/// Gets or sets the build configuration (Debug, Release, Profile, Bounds).
		/// </summary>
		[Required]
		public string Configuration { get; set; }

		/// <summary>
		/// Gets or sets the root directory of the build (repository) tree.
		/// </summary>
		[Required]
		public string BuildRoot { get; set; }

		/// <summary>
		/// The build architecture (x86 or x64)
		/// </summary>
		public string BuildArch { get; set; }

		/// <summary>
		/// Gets or sets the target inside the Makefile.
		/// </summary>
		public string Target { get; set; }

		/// <summary>
		/// Gets or sets additional macros passed to make
		/// </summary>
		public string Macros { get; set; }

		/// <summary>
		/// Gets the build "type" (d, r, p, b).
		/// This can be derived from the build configuration.
		/// </summary>
		private string BuildType
		{
			get
			{
				switch (Configuration)
				{
					case "Debug":
						return "d";
					case "Release":
						return "r";
					case "Profile":
						return "p";
					case "Bounds":
						return "b";
					default:
						return "d";
				}
			}
		}

		/// <summary>
		/// Gets or sets the working directory.
		/// </summary>
		/// <value>The working directory.</value>
		/// <returns>
		/// The directory in which to run the executable file, or a null reference (Nothing in Visual Basic) if the executable file should be run in the current directory.
		/// </returns>
		public string WorkingDirectory { get; set; }

		#region Task Overrides
		protected override string ToolName => Environment.OSVersion.Platform == PlatformID.Unix ? "make" : "nmake.exe";

		private void CheckToolPath()
		{
			var makePath = ToolPath == null ? string.Empty : ToolPath.Trim();
			if (!string.IsNullOrEmpty(makePath) && File.Exists(Path.Combine(makePath, ToolName)))
			{
				ToolPath = makePath;
				return;
			}
			if (Environment.OSVersion.Platform == PlatformID.Unix)
			{
				ToolPath = "/usr/bin";
				if (File.Exists(Path.Combine(ToolPath, ToolName)))
					return;
			}
			var path = Environment.GetEnvironmentVariable("PATH");
			//Console.WriteLine("DEBUG Make Task: PATH='{0}'", path);
			var splitPath = path.Split(new char[] {Path.PathSeparator});
			foreach (var dir in splitPath)
			{
				if (File.Exists(Path.Combine(dir, ToolName)))
				{
					ToolPath = dir;
					return;
				}
			}
			// Fall Back to the install directory
			var vcInstallDir = Environment.GetEnvironmentVariable("VCINSTALLDIR");
			ToolPath = Path.Combine(vcInstallDir, "bin");
		}

		protected override string GenerateFullPathToTool()
		{
			CheckToolPath();
			return Path.Combine(ToolPath, ToolName);
		}

		protected override string GenerateCommandLineCommands()
		{
			var bldr = new CommandLineBuilder();
			if (Environment.OSVersion.Platform == PlatformID.Unix)
			{
				bldr.AppendSwitchIfNotNull("BUILD_CONFIG=", Configuration);
				bldr.AppendSwitchIfNotNull("BUILD_TYPE=", BuildType);
				bldr.AppendSwitchIfNotNull("BUILD_ROOT=", BuildRoot);
				bldr.AppendSwitchIfNotNull("BUILD_ARCH=", BuildArch);
				if (!string.IsNullOrEmpty(Macros))
				{
					bldr.AppendTextUnquoted(" ");
					bldr.AppendTextUnquoted(Macros);
				}
				bldr.AppendSwitchIfNotNull("-C ", WorkingDirectory);
				bldr.AppendSwitchIfNotNull("-f ", Makefile);
				bldr.AppendSwitch(string.IsNullOrEmpty(Target) ? "all" : Target);
			}
			else
			{
				bldr.AppendSwitch("/nologo");
				bldr.AppendSwitchIfNotNull("BUILD_CONFIG=", Configuration);
				bldr.AppendSwitchIfNotNull("BUILD_TYPE=", BuildType);
				bldr.AppendSwitchIfNotNull("BUILD_ROOT=", BuildRoot);
				bldr.AppendSwitchIfNotNull("BUILD_ARCH=", BuildArch);
				if (!string.IsNullOrEmpty(Macros))
				{
					bldr.AppendTextUnquoted(" ");
					bldr.AppendTextUnquoted(Macros);
				}
				bldr.AppendSwitchIfNotNull("/f ", Makefile);
				if (!string.IsNullOrEmpty(Target))
					bldr.AppendSwitch(Target);
			}
			return bldr.ToString();
		}

		/// <summary>
		/// Returns the directory in which to run the executable file, or a null reference if the executable file should be run in the current directory.
		/// </summary>
		protected override string GetWorkingDirectory()
		{
			return string.IsNullOrEmpty(WorkingDirectory) ? base.GetWorkingDirectory() : WorkingDirectory;
		}
		#endregion
	}
}
