// Copyright (c) 2010-2022 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Utils;
using SIL.PlatformUtilities;

namespace SIL.FieldWorks.UnicodeCharEditor
{
	/// <summary />
	internal sealed class PUAInstaller
	{
		private readonly Dictionary<int, PUACharacter> m_dictCustomChars = new Dictionary<int, PUACharacter>();
		private static string m_icuDir;
		private string m_icuDataDir;

		/// <summary>
		/// Get the pathname of the Icu folder (similar to "C:\ProgramData\SIL\Icu54")
		/// </summary>
		public static string IcuDir
		{
			get
			{
				if (string.IsNullOrEmpty(m_icuDir))
				{
					m_icuDir = CustomIcu.DefaultDataDirectory;
					if (string.IsNullOrEmpty(m_icuDir))
					{
						throw new DirectoryNotFoundException("ICU directory not found. Registry value for ICU not set?");
					}

					// There is ambiguity about whether the ICU_DATA directory should point to the icudt{icuver}l folder, or its base
					// Since we can't seem to make up our mind handle both
					if (!Directory.Exists(Path.Combine(m_icuDir, "data")))
					{
						m_icuDir = Path.GetDirectoryName(m_icuDir);
					}

					if (!Directory.Exists(m_icuDir))
					{
						throw new DirectoryNotFoundException($"ICU directory does not exist at {m_icuDir}. Registry value for ICU set incorrectly?");
					}
				}
				return m_icuDir;
			}
		}

		private string IcuDataDir
		{
			get
			{
				if (string.IsNullOrEmpty(m_icuDataDir))
				{
					m_icuDataDir = Path.Combine(IcuDir, "data");
					if (!Directory.Exists(m_icuDataDir))
					{
						throw new DirectoryNotFoundException($"ICU data directory does not exist at {m_icuDataDir}.");
					}
				}
				return m_icuDataDir;
			}
		}

		#region install PUA characters

		/// <summary>
		/// Installs the PUA characters (PUADefinitions) from the given xml file
		/// We do this by:
		/// 1. Maps the XML file to a list of PUACharacter objects
		/// 2. Sorts the PUA characters
		/// 3. Opens UnicodeDataOverrides.txt for reading and writing
		/// 4. Inserts the PUA characters via their codepoints
		/// 5. Regenerate nfc.txt and nfkc.txt, the input files for gennorm2, which generates
		///    the ICU custom normalization files.
		/// 6. Run "gennorm2" to create the actual binary files used by the ICU normalization functions.
		/// </summary>
		/// <param name="filename">Our XML file containing our PUA Defintions</param>
		internal void InstallPUACharacters(string filename)
		{
			try
			{
				// 0. Intro: Prepare files

				// 0.1: File names
				var outputUnicodeDataFilename = Path.Combine(IcuDir, "UnicodeDataOverrides.txt");
				var originalUnicodeDataFilename = Path.Combine(IcuDataDir, "UnicodeDataOverrides.txt");
				var nfcOverridesFileName = Path.Combine(IcuDir, "nfcOverrides.txt"); // Intermediate we will generate
				var nfkcOverridesFileName = Path.Combine(IcuDir, "nfkcOverrides.txt"); // Intermediate we will generate

				// 0.2: Create a one-time backup that will not be overwritten if the file exists
				BackupOrig(outputUnicodeDataFilename);

				// 0.3: Create a stack of files to restore if we encounter and error
				//			This allows us to work with the original files
				// If we are successful we call this.RemoveBackupFiles() to clean up
				// If we are not we call this.RestoreFiles() to restore the original files
				//		and delete the backups
				var unicodeDataBackup = CreateBackupFile(outputUnicodeDataFilename);
				AddUndoFileFrame(outputUnicodeDataFilename, unicodeDataBackup);

				//Initialize and populate the parser if necessary
				// 1. Maps our XML file to a list of PUACharacter objects.
				var chars = ParseCustomCharsFile(filename, out var comment);

				// (Step 1 has been moved before the "intro")
				// 2. Sort the PUA characters
				chars.Sort();

				// 3. Open the file for reading and writing
				// 4. Insert the PUA via their codepoints
				InsertCharacters(chars, comment, originalUnicodeDataFilename, outputUnicodeDataFilename);

				// 5. Generate the modified normalization file inputs.
				using (var reader = new StreamReader(outputUnicodeDataFilename, Encoding.ASCII))
				using (var writeNfc = new StreamWriter(nfcOverridesFileName, false, Encoding.ASCII))
				using (var writeNfkc = new StreamWriter(nfkcOverridesFileName, false, Encoding.ASCII))
				{
					// ReSharper disable ReturnValueOfPureMethodIsNotUsed -- Justification: force autodetection of encoding.
					reader.Peek();
					// ReSharper restore ReturnValueOfPureMethodIsNotUsed
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						if (line.StartsWith("Code") || line.StartsWith("block")) // header line or special instruction
						{
							continue;
						}
						// Extract the first, fourth, and sixth fields.
						var match = new Regex("^([^;]*);[^;]*;[^;]*;([^;]*);[^;]*;([^;]*);").Match(line);
						if (!match.Success)
						{
							continue;
						}
						var codePoint = match.Groups[1].Value.Trim();
						var combiningClass = match.Groups[2].Value.Trim();
						var decomp = match.Groups[3].Value.Trim();
						if (!string.IsNullOrEmpty(combiningClass) && combiningClass != "0")
						{
							writeNfc.WriteLine(codePoint + ":" + combiningClass);
						}
						if (!string.IsNullOrEmpty(decomp))
						{
							if (decomp.StartsWith("<"))
							{
								var index = decomp.IndexOf(">", StringComparison.InvariantCulture);
								if (index < 0)
								{
									continue; // badly formed, ignore it.
								}
								decomp = decomp.Substring(index + 1).Trim();
							}
							// otherwise we should arguably write to nfc.txt
							// If exactly two code points write codePoint=decomp
							// otherwise write codePoint>decomp
							// However, we should not be modifying standard normalization.
							// For now treat them all as compatibility only.
							writeNfkc.WriteLine(codePoint + ">" + decomp);
						}
					}
				}


				// 6. Run the "gennorm2" commands to write the actual files
				RunICUTools(IcuDataDir, nfcOverridesFileName, nfkcOverridesFileName);

				RemoveBackupFiles();
			}
			catch (Exception)
			{
				try
				{
					RestoreFiles();
				}
				catch
				{
					// don't mask the original exception with an exception thrown during cleanup
				}
				throw;
			}
		}

		private List<PUACharacter> ParseCustomCharsFile(string filename, out string comment)
		{
			var ci = CultureInfo.CreateSpecificCulture("en-US");
			comment = $"[SIL-Corp] {filename} User Added {DateTime.Now.ToString("F", ci)}";
			var chars = new List<PUACharacter>();
			var xd = XDocument.Load(filename, LoadOptions.None);
			foreach (var xe in xd.Descendants("CharDef"))
			{
				var xaCode = xe.Attribute("code");
				if (string.IsNullOrEmpty(xaCode?.Value))
				{
					continue;
				}
				var xaData = xe.Attribute("data");
				if (string.IsNullOrEmpty(xaData?.Value))
				{
					continue;
				}
				if (int.TryParse(xaCode.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) && !m_dictCustomChars.ContainsKey(code))
				{
					var spec = new PUACharacter(xaCode.Value, xaData.Value);
					m_dictCustomChars.Add(code, spec);
					chars.Add(spec);
				}
			}
			return chars;
		}

		#region Undo File Stack

		///<summary />
		private struct UndoFiles
		{
			///<summary />
			internal string m_backupFile;
			///<summary />
			internal string m_originalFile;
		}

		private readonly List<UndoFiles> m_undoFileStack = new List<UndoFiles>();

		/// <summary>
		/// Create a copy of the backup and source file names for future use.
		/// </summary>
		private void AddUndoFileFrame(string srcFile, string backupFile)
		{
			LogFile.AddVerboseLine("Adding Undo File: <" + backupFile + ">");
			var frame = new UndoFiles { m_originalFile = srcFile, m_backupFile = backupFile };
			m_undoFileStack.Add(frame);
		}

		/// <summary>
		/// Remove the backup files that were created to restore original files if there
		/// were a problem.  If this method is called, there was no problem.  We're
		/// just removing the backup file - much like a temp file at this point.
		/// </summary>
		private void RemoveBackupFiles()
		{
			LogFile.AddVerboseLine("Removing Undo Files --- Start");
			for (var i = m_undoFileStack.Count; --i >= 0;)
			{
				var frame = m_undoFileStack[i];
				if (File.Exists(frame.m_backupFile))
				{
					DeleteFile(frame.m_backupFile);
				}
			}

			LogFile.AddVerboseLine("Removing Undo Files --- Finish");
		}


		/// <summary>
		/// Copies the backup files over the source files and then deletes the backup files.
		/// </summary>
		private void RestoreFiles()
		{
			for (var i = m_undoFileStack.Count; --i >= 0;)
			{
				var frame = m_undoFileStack[i];
				var fi = new FileInfo(frame.m_backupFile);
				if (fi.Exists)
				{
					// Use the safe versions of the methods so that the process can continue
					// even if there are errors.
					SafeFileCopyWithLogging(frame.m_backupFile, frame.m_originalFile, true);
					if (fi.Length <= 0)
					{
						// no data in the backup file, remove originalFile also
						SafeDeleteFile(frame.m_originalFile);
					}
					SafeDeleteFile(frame.m_backupFile);
				}
			}
		}
		#endregion

		/// <summary>
		/// This runs genprops and gennames in order to use UnicodeData.txt,
		/// DerivedBidiClass.txt, as well as other *.txt files in unidata to
		/// create <i>icuprefix/</i>uprops.icu and other binary data files.
		/// </summary>
		private void RunICUTools(string icuDataDir, string nfcOverridesFileName, string nfkcOverridesFileName)
		{
			// run commands similar to the following (with full paths in quotes)
			//
			//    gennorm2 -o nfc.nrm nfc.txt nfcHebrew.txt nfcOverrides.txt
			//    gennorm2 -o nfkc.nrm nfc.txt nfkc.txt nfcHebrew.txt nfcOverrides.txt nfkcOverrides.txt
			//
			// Note: this compiles the input files to produce the two output files, which ICU loads to customize normalization.

			// Get the icu directory information
			var nfcTxtFileName = Path.Combine(icuDataDir, "nfc.txt"); // Original from ICU, installed
			var nfcHebrewFileName = Path.Combine(icuDataDir, "nfcHebrew.txt"); // Original from ICU, installed
			var nfkcTxtFileName = Path.Combine(icuDataDir, "nfkc.txt"); // Original from ICU, installed

			// The exact name of this directory is built into ICU and can't be changed. It must be this subdirectory
			// relative to the IcuDir (which is the IcuDataDirectory passed to ICU).
			var uniBinaryDataDir = Path.Combine(IcuDir, $"icudt{CustomIcu.Version}l");
			var nfcBinaryFileName = Path.Combine(uniBinaryDataDir, "nfc_fw.nrm"); // Binary file generated by gennorm2
			var nfkcBinaryFileName = Path.Combine(uniBinaryDataDir, "nfkc_fw.nrm"); // Binary file generated by gennorm2

			// Make a one-time original backup of the files we are about to generate.
			var nfcBackupFileName = CreateBackupFile(nfcBinaryFileName);
			var nfkcBackupFileName = CreateBackupFile(nfkcBinaryFileName);
			AddUndoFileFrame(nfcBinaryFileName, nfcBackupFileName);
			AddUndoFileFrame(nfkcBinaryFileName, nfkcBackupFileName);
			// Move existing files out of the way in case they are locked and can't be overwritten.
			RemoveFile(nfcBinaryFileName);
			RemoveFile(nfkcBinaryFileName);

			// Clean up the ICU and set the icuDatadir correctly
			Icu.Wrapper.Cleanup();

			var genNorm2 = GetIcuExecutable("gennorm2");

			// run it to generate the canonical binary data.
			var args = $@" -o ""{nfcBinaryFileName}"" ""{nfcTxtFileName}"" ""{nfcHebrewFileName}"" ""{nfcOverridesFileName}""";
			LogFile.AddVerboseLine($"Executing gennorm: {genNorm2} {args}");
			RunProcess(genNorm2, args);

			// run it again to generate the non-canonical binary data.
			args = $@" -o ""{nfkcBinaryFileName}"" ""{nfcTxtFileName}"" ""{nfkcTxtFileName}"" ""{nfcHebrewFileName}"" ""{nfcOverridesFileName}"" ""{nfkcOverridesFileName}""";
			RunProcess(genNorm2, args);
		}

		private static void RunProcess(string executable, string args)
		{
			using (var gennormProcess = new Process())
			{
				gennormProcess.StartInfo = new ProcessStartInfo
				{
					FileName = executable,
					WorkingDirectory = Path.GetDirectoryName(executable), // lets it find its DLL
					Arguments = args,
					WindowStyle = ProcessWindowStyle.Hidden,
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				};

				// For some reason Hidden worked on FW6.0, but not on FW7.0+. NoWindow works!?!

				// Allows us to re-direct the std. output for logging.

				gennormProcess.Start();
				gennormProcess.WaitForExit();
				var ret = gennormProcess.ExitCode;

				// If gen props doesn't run correctly, log what it displays to the standar output
				// and throw and exception
				if (ret != 0)
				{
					var stdOutput = gennormProcess.StandardOutput.ReadToEnd();
					var stdError = gennormProcess.StandardError.ReadToEnd();
					if (LogFile.IsLogging)
					{
						LogFile.AddErrorLine("Error running gennorm2:");
						LogFile.AddErrorLine(stdOutput);
						LogFile.AddErrorLine(stdError);
					}
					if (ret == 4 /* Old IcuErrorCodes.FILE_ACCESS_ERROR value */)
					{
						throw new IcuLockedException(ErrorCodes.Gennorm, stdError);
					}
					throw new PuaException(ErrorCodes.Gennorm, stdError);
				}
			}
		}

		private static string GetIcuExecutable(string exeName)
		{
			if (Platform.IsMono)
			{
				// TODO-Linux: Review - is the approach of expecting executable location to be in PATH ok?
				return exeName;
			}
			var codeBaseUri = typeof(PUAInstaller).Assembly.CodeBase;
			var path = Path.GetDirectoryName(FileUtils.StripFilePrefix(codeBaseUri));
			var x86Path = Path.Combine(path, "lib", "x86", exeName + ".exe");
			var x64Path = Path.Combine(path, "lib", "x64", exeName + ".exe");
			return Environment.Is64BitProcess ? x64Path : x86Path;
		}

		///  <summary>
		///  Inserts the given PUADefinitions (any Unicode character) into the UnicodeData.txt file.
		///
		///  This accounts for all the cases of inserting into the "first/last" blocks.  That
		///  is, it will split the blocks into two or move the first and last tags to allow a
		///  codepoint to be inserted correctly.
		///
		///  Also, this accounts for Hexadecimal strings that are within the unicode range, not
		///  just four digit unicode files.
		///
		///  <list type="number">
		///  <listheader>Assumptions made about the format</listheader>
		///  <item>The codepoints are in order</item>
		///  <item>There first last block will always have no space between the word first and the following ">"</item>
		///  <item>No other data entries contain the word first followed by a ">"</item>
		///  <item>There will always be a "last" on the line directly after a "first".</item>
		///  </list>
		///
		///  </summary>
		///  <remarks>
		///  Pseudocode for inserting lines:
		/// 	if the unicodePoint	is a first tag
		/// 		Get	first and last uncodePoint range
		/// 		Stick into array all the xmlPoints that fit within the uncodePoint range
		/// 			Look at the next xmlPoint
		/// 			if there are any
		/// 				call WriteCodepointBlock subroutine
		/// 	else if the unicodePoint is greater than the last point but less than or equal to "the xmlPoint"
		/// 		insert the missing line or replace	the	line
		/// 		look at	the	next xmlPoint
		/// 	else
		/// 		do nothing except write	the	line
		/// </remarks>
		///  <param name="puaDefinitions">A list of PUADefinitions to insert into UnicodeDataOverrides.txt.</param>
		/// <param name="comment"></param>
		/// <param name="originalOverrides">original to merge into</param>
		///  <param name="outputOverrides">where to write output</param>
		private static void InsertCharacters(IReadOnlyList<IPuaCharacter> puaDefinitions, string comment, string originalOverrides, string outputOverrides)
		{
			// Open the file for reading and writing
			LogFile.AddVerboseLine("StreamReader on <" + originalOverrides + ">");
			using (var reader = new StreamReader(originalOverrides, Encoding.ASCII))
			{
				// ReSharper disable ReturnValueOfPureMethodIsNotUsed -- Justification: force autodetection of encoding.
				reader.Peek();
				// ReSharper restore ReturnValueOfPureMethodIsNotUsed
				using (var writer = new StreamWriter(outputOverrides, false, Encoding.ASCII))
				{
					// Insert the PUA via their codepoints

					string line;
					var lastCode = 0;
					// Start looking at the first codepoint
					var codeIndex = 0;
					var newCode = puaDefinitions.Count > 0 ? Convert.ToInt32(puaDefinitions[codeIndex].CodePoint, 16) : 0;

					//While there is a line to be read in the file
					while ((line = reader.ReadLine()) != null)
					{
						// skip entirely blank lines
						if (line.Length <= 0)
						{
							continue;
						}
						if (line.StartsWith("Code") || line.StartsWith("block") || puaDefinitions.Count == 0) // header line or special instruction, or all overrides removed
						{
							writer.WriteLine(line);
							continue;
						}
						//Grab codepoint
						var strFileCode = line.Substring(0, line.IndexOf(';')).Trim(); // current code in file
						var fileCode = Convert.ToInt32(strFileCode, 16);

						// If the new codepoint is greater than the last one processed in the file, but
						// less than or equal to the current codepoint in the file.
						if (newCode > lastCode && newCode <= fileCode)
						{
							while (newCode <= fileCode)
							{
								LogCodepoint(puaDefinitions[codeIndex].CodePoint);

								// Replace the line with the new PuaDefinition
								writer.WriteLine("{0} #{1}", puaDefinitions[codeIndex], comment);
								lastCode = newCode;

								// Look for the next PUA codepoint that we wish to insert, we are done
								// with this one If we are all done, push through the rest of the file.
								if (++codeIndex >= puaDefinitions.Count)
								{
									// Write out the original top of the section if it hasn't been replaced.
									if (fileCode != lastCode)
									{
										writer.WriteLine(line);
									}
									while ((line = reader.ReadLine()) != null)
									{
										writer.WriteLine(line);
									}
									break;
								}
								newCode = Convert.ToInt32(puaDefinitions[codeIndex].CodePoint, 16);
							}
							if (codeIndex >= puaDefinitions.Count)
							{
								break;
							}
							// Write out the original top of the section if it hasn't been replaced.
							if (fileCode != lastCode)
							{
								writer.WriteLine(line);
							}
						}
						//if it's not a first tag and the codepoints don't match
						else
						{
							writer.WriteLine(line);
						}
						lastCode = fileCode;
					}
					// Output any codepoints after the old end
					while (codeIndex < puaDefinitions.Count)
					{
						LogCodepoint(puaDefinitions[codeIndex].CodePoint);

						// Add a line with the new PuaDefinition
						writer.WriteLine("{0} #{1}", puaDefinitions[codeIndex], comment);
						codeIndex++;
					}
				}
			}
		}

		/// <summary>
		/// Prints a message to the console when storing a Unicode character.
		/// </summary>
		private static void LogCodepoint(string code)
		{
			if (LogFile.IsLogging)
			{
				LogFile.AddErrorLine("Storing definition for Unicode character: " + code);
			}
		}

		#endregion

		private const string ksOriginal = "_ORIGINAL";
		private const string ksBackupFileSuffix = "_BAK";

		///<summary />
		private static void SafeFileCopyWithLogging(string inName, string outName, bool overwrite)
		{
			try
			{
				FileCopyWithLogging(inName, outName, overwrite);
			}
			catch
			{
				LogFile.AddVerboseLine($"ERROR  : unable to copy <{inName}> to <{outName}> <{overwrite}>");
			}
		}

		///<summary />
		private static void FileCopyWithLogging(string inName, string outName, bool overwrite)
		{
			var fi = new FileInfo(inName);
			if (fi.Length > 0)
			{
				if (LogFile.IsLogging)
				{
					LogFile.AddVerboseLine($"Copying: <{inName}> to <{outName}> <{overwrite}>");
				}
				File.Copy(inName, outName, overwrite);
			}
			else
			{
				LogFile.AddVerboseLine($"Not Copying (Zero size): <{inName}> to <{outName}> <{overwrite}>");
			}
		}

		///<summary />
		///<returns>whether the file was found and successfully deleted</returns>
		private static void SafeDeleteFile(string file)
		{
			try
			{
				DeleteFile(file);
			}
			catch (Exception e)
			{
				LogFile.AddVerboseLine($"ERROR: Unable to remove file: <{file}>: {e.Message}");
			}
		}

		///<summary />
		///<returns>whether the file was found and successfully deleted</returns>
		private static void DeleteFile(string file)
		{
			if (!File.Exists(file))
			{
				if (LogFile.IsLogging)
				{
					LogFile.AddVerboseLine($"Tried to delete file that didn't exist:<{file}>");
				}

				return;
			}
			File.SetAttributes(file, FileAttributes.Normal);
			File.Delete(file);
			if (LogFile.IsLogging)
			{
				LogFile.AddVerboseLine($"Removed file:<{file}>");
			}
		}

		/// <summary>
		/// Remove the file by renaming it - it might be locked so a direct delete might not work
		/// </summary>
		private static void RemoveFile(string inputFilespec)
		{
			if (!File.Exists(inputFilespec))
			{
				return;
			}
			// First try to remove the file immediately
			try
			{
				File.Delete(inputFilespec);
			}
			catch
			{
				var dir = Path.GetDirectoryName(inputFilespec);
				var outputFilespec = Path.Combine(dir, Path.GetRandomFileName());

				try
				{
					File.Move(inputFilespec, outputFilespec);
				}
				catch
				{
					LogFile.AddErrorLine($"Error renaming file {inputFilespec} to {outputFilespec}");
					throw;
				}

				// Register outputFilespec for later deletion
				File.AppendAllText(Path.Combine(dir, "TempFilesToDelete"), outputFilespec);
			}
		}

		/// <summary>
		/// This method will create a copy of the input file with the specified suffix.
		/// </summary>
		private static string CreateXxFile(string inputFilespec, string suffix)
		{
			if (!File.Exists(inputFilespec))
			{
				LogFile.AddVerboseLine($"No Orig to back up: <{inputFilespec}>");
				return null;
			}

			var outputFilespec = CreateNewFileName(inputFilespec, suffix);
			if (!File.Exists(outputFilespec))
			{
				try
				{
					FileCopyWithLogging(inputFilespec, outputFilespec, true);
				}
				catch
				{
					LogFile.AddErrorLine($"Error creating {suffix} copy: {inputFilespec}");
					throw;
				}
			}
			return outputFilespec;
		}

		/// <summary>
		/// This method will create the "bak" backup of the original input file.
		/// </summary>
		private static string CreateBackupFile(string inputFilespec)
		{
			return CreateXxFile(inputFilespec, ksBackupFileSuffix);
		}

		/// <summary>
		/// Create the "original" (backup) copy of the file to be modified,  if it doesn't already exist.
		/// </summary>
		private static void BackupOrig(string inputFilespec)
		{
			CreateXxFile(inputFilespec, ksOriginal);
		}

		/// <summary>This method appends 'nameSplice' to a file 'inputFilespec'.</summary>
		/// <param name="inputFilespec">Input file name to modify.</param>
		/// <param name="nameSplice">The 'text' to append to the file name before the
		/// extension.</param>
		/// <returns>The new file name.</returns>
		private static string CreateNewFileName(string inputFilespec, string nameSplice)
		{
			var index = inputFilespec.LastIndexOf('.');
			string newName;

			if (index == -1)
			{
				newName = inputFilespec + nameSplice;
			}
			else
			{
				newName = inputFilespec.Substring(0, index);
				newName += nameSplice;
				newName += inputFilespec.Substring(index);
			}
			return newName;
		}
	}
}