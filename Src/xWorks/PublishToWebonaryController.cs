// Copyright (c) 2014-2016 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Ionic.Zip;
using SIL.FieldWorks.FDO;
using XCore;
using System.Net;
using System.Text;

namespace SIL.FieldWorks.XWorks
{
	/// <summary>
	/// Currently serves as the controller and the model for the PublishToWebonaryView
	/// </summary>
	[SuppressMessage("Gendarme.Rules.Design", "TypesWithDisposableFieldsShouldBeDisposableRule",
		Justification="Cache and Mediator are references")]
	public class PublishToWebonaryController
	{
		private readonly FdoCache m_cache;
		private readonly Mediator m_mediator;
		private readonly DictionaryExportService m_exportService;

		public PublishToWebonaryController(FdoCache cache, Mediator mediator)
		{
			m_cache = cache;
			m_mediator = mediator;
			m_exportService = new DictionaryExportService(mediator);
		}

		public int CountDictionaryEntries()
		{
			return m_exportService.CountDictionaryEntries();
		}

		public int CountReversalIndexEntries(IEnumerable<string> indexes)
		{
			return m_exportService.CountReversalIndexEntries(indexes);
		}

		/// <summary>
		/// Exports the dictionary xhtml and css for the publication and configuration that the user had selected in the dialog.
		/// </summary>
		private void ExportDictionaryContent(string tempDirectoryToCompress, PublishToWebonaryModel model, IPublishToWebonaryView webonaryView)
		{
			webonaryView.UpdateStatus(String.Format(xWorksStrings.ExportingEntriesToWebonary, model.SelectedPublication, model.SelectedConfiguration));
			var xhtmlPath = Path.Combine(tempDirectoryToCompress, "configured.xhtml");
			var configuration = model.Configurations[model.SelectedConfiguration];
			m_exportService.ExportDictionaryContent(xhtmlPath, configuration);
			webonaryView.UpdateStatus(xWorksStrings.ExportingEntriesToWebonaryCompleted);
		}

		internal static void CompressExportedFiles(string tempDirectoryToCompress, string zipFileToUpload, IPublishToWebonaryView webonaryView)
		{
			webonaryView.UpdateStatus(xWorksStrings.BeginCompressingDataForWebonary);
			using(var zipFile = new ZipFile())
			{
				RecursivelyAddFilesToZip(zipFile, tempDirectoryToCompress, "", webonaryView);
				zipFile.Save(zipFileToUpload);
			}
			webonaryView.UpdateStatus(xWorksStrings.FinishedCompressingDataForWebonary);
		}

		/// <summary>
		/// This method will recurse into a directory and add files into the zip file with their relative path
		/// to the original dirToCompress.
		/// </summary>
		private static void RecursivelyAddFilesToZip(ZipFile zipFile, string dirToCompress, string dirInZip, IPublishToWebonaryView webonaryView)
		{
			foreach(var file in Directory.EnumerateFiles(dirToCompress))
			{
				if (!IsSupportedWebonaryFile(file))
				{
					webonaryView.UpdateStatus(string.Format("Excluding {0},{1} format is unsupported by Webonary.",
						Path.GetFileName(file), Path.GetExtension(file)));
					continue;
				}
				zipFile.AddFile(file, dirInZip);
				webonaryView.UpdateStatus(Path.GetFileName(file));
			}
			foreach(var dir in Directory.EnumerateDirectories(dirToCompress))
			{
				RecursivelyAddFilesToZip(zipFile, dir, Path.Combine(dirInZip, Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar))), webonaryView);
			}
		}

		/// <summary>
		/// Exports the reversal xhtml and css for the reversals that the user had selected in the dialog
		/// </summary>
		private void ExportReversalContent(string tempDirectoryToCompress, PublishToWebonaryModel model, IPublishToWebonaryView webonaryView)
		{
			if (model.Reversals == null)
				return;
			foreach (var reversal in model.Reversals)
			{
				webonaryView.UpdateStatus(string.Format(xWorksStrings.ExportingReversalsToWebonary, reversal));
				var reversalWs = m_cache.LangProject.AnalysisWritingSystems.FirstOrDefault(ws => ws.DisplayLabel == reversal);
				// The reversalWs should always match the Display label of one of the AnalysisWritingSystems, this exception is for future programming errors
				if (reversalWs == null)
				{
					throw new ApplicationException(string.Format("Could not locate reversal writing system for {0}", reversal));
				}
				var xhtmlPath = Path.Combine(tempDirectoryToCompress, string.Format("reversal_{0}.xhtml", reversalWs.RFC5646));
				var configurationFile = Path.Combine(m_mediator.PropertyTable.UserSettingDirectory, "ReversalIndex", reversal + ".fwdictconfig"); // TODO (Hasso) 2016.01: what if the user wants to use a copy of this config?
				var configuration = new DictionaryConfigurationModel(configurationFile, m_cache);
				m_exportService.ExportReversalContent(xhtmlPath, reversal, configuration);
				webonaryView.UpdateStatus(xWorksStrings.ExportingReversalsToWebonaryCompleted);
			}
		}

		/// <summary>
		/// Return upload URI, based on siteName.
		/// </summary>
		internal virtual string DestinationURI(string siteName)
		{
			// TODO use specified site with respect to webonary domain, rather than using value
			// for current testing. eg $siteName.webonary.org/something or
			// www.webonary.org/$sitename/wp-json/something .
			var server = Environment.GetEnvironmentVariable("WEBONARYSERVER");
			server = string.IsNullOrEmpty(server) ? "192.168.33.10" : server;
			return string.Format("https://{0}.{1}/wp-json/webonary/import", siteName, server);
		}

		internal void UploadToWebonary(string zipFileToUpload, PublishToWebonaryModel model, IPublishToWebonaryView view)
		{
			if (zipFileToUpload == null)
				throw new ArgumentNullException("zipFileToUpload");
			if(model == null)
				throw new ArgumentNullException("model");
			if (view == null)
				throw new ArgumentNullException("view");

			view.UpdateStatus("Connecting to Webonary.");
			var targetURI = DestinationURI(model.SiteName);
			using (var client = new WebClient())
			{
				var credentials = string.Format("{0}:{1}", model.UserName, model.Password);
				client.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(new UTF8Encoding().GetBytes(credentials)));

				byte[] response = null;
				try
				{
					response = client.UploadFile(targetURI, zipFileToUpload);
				}
				catch (WebException e)
				{
					const string errorMessage = "Unable to connect to Webonary.  Please check your username and password and your Internet connection.";
					view.UpdateStatus(string.Format("An error occurred uploading your data: {0}{1}{2}", errorMessage, Environment.NewLine, e.Message));
					return;
				}
				var responseText = Encoding.ASCII.GetString(response);

				if (responseText.Contains("Upload successful"))
				{
					if (responseText.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0)
					{
						view.UpdateStatus("Upload successful. " +
							"Preparing your data for publication. " +
							"This may take several minutes to a few hours depending on the size of your dictionary. " +
							"You will receive an email when the process is complete. " +
							"You can examine the progress on the admin page of your Webonary site. "+
							"You may now safely close this dialog.");
						return;
					}

					view.UpdateStatus("The upload was successful; however, there were errors processing your data.");
				}

				if (responseText.Contains("Wrong username or password"))
					view.UpdateStatus("Error: Wrong username or password");
				if (responseText.Contains("User doesn't have permission to import data"))
					view.UpdateStatus("Error: User doesn't have permission to import data");

				view.UpdateStatus(string.Format("Response from server:{0}{1}{0}", Environment.NewLine, responseText));
			}
		}

		private void ExportOtherFilesContent(string tempDirectoryToCompress, PublishToWebonaryModel logTextbox, object outputLogTextbox)
		{
			//TODO:Copy the user selected other files into the temp directory
		}

		public void PublishToWebonary(PublishToWebonaryModel model, IPublishToWebonaryView view)
		{
			view.UpdateStatus("Publishing to Webonary.");

			if(string.IsNullOrEmpty(model.SiteName))
			{
				view.UpdateStatus("Error: No site name specified.");
				return;
			}

			if(string.IsNullOrEmpty(model.UserName))
			{
				view.UpdateStatus("Error: No username specified.");
				return;
			}

			if (string.IsNullOrEmpty(model.Password))
			{
				view.UpdateStatus("Error: No Password specified.");
				return;
			}

			if(string.IsNullOrEmpty(model.SelectedPublication))
			{
				view.UpdateStatus("Error: No Publication specified.");
				return;
			}

			if(string.IsNullOrEmpty(model.SelectedConfiguration))
			{
				view.UpdateStatus("Error: No Configuration specified.");
				return;
			}

			var tempDirectoryToCompress = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			var zipFileToUpload = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
			Directory.CreateDirectory(tempDirectoryToCompress);
			ExportDictionaryContent(tempDirectoryToCompress, model, view);
			ExportReversalContent(tempDirectoryToCompress, model, view);
			ExportOtherFilesContent(tempDirectoryToCompress, model, view);
			CompressExportedFiles(tempDirectoryToCompress, zipFileToUpload, view);
			UploadToWebonary(zipFileToUpload, model, view);
		}

		/// <summary>
		/// True if given a path to a file type that is acceptable to upload to Webonary. Otherwise false.
		/// </summary>
		/// <remarks>Could be changed to consider the magic number instead of file extension, if helpful.</remarks>
		internal static bool IsSupportedWebonaryFile(string path)
		{
			var supportedFileExtensions = new List<string>
			{
				".xhtml", ".css", ".html", ".htm", ".json", ".xml",
				".jpg", ".jpeg", ".gif", ".png", ".mp3"
			};
			return supportedFileExtensions.Any(path.EndsWith);
		}
	}
}
