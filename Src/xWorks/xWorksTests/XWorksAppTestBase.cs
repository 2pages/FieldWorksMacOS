// Copyright (c) 2003-2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SIL.CoreImpl;
using SIL.CoreImpl.Text;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.FieldWorks.Common.Framework;
using SIL.FieldWorks.FDO.DomainServices;
using SIL.Utils;
using SIL.FieldWorks.FDO.FDOTests;
using SIL.FieldWorks.Common.FwUtils;

namespace SIL.FieldWorks.XWorks
{
	public struct ControlAssemblyReplacement
	{
		public string m_toolName;
		public string m_controlName;
		public string m_targetAssembly;
		public string m_targetControlClass;
		public string m_newAssembly;
		public string m_newControlClass;
	}

	public class MockFwManager : IFieldWorksManager
	{

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the cache.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public FdoCache Cache { get; set; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Shutdowns the specified application. The application will be disposed of immediately.
		/// If no other applications are running, then FieldWorks will also be shutdown.
		/// </summary>
		/// <param name="app">The application to shut down.</param>
		/// ------------------------------------------------------------------------------------
		public void ShutdownApp(IFlexApp app)
		{
			throw new NotImplementedException();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Executes the specified method asynchronously. The method will typically be called
		/// when the the Application.Run() loop regains control or the next call to
		/// Application.DoEvents() at some unspecified time in the future.
		/// </summary>
		/// <param name="action">The action to execute</param>
		/// <param name="param1">The first parameter of the action.</param>
		/// ------------------------------------------------------------------------------------
		public void ExecuteAsync<T>(Action<T> action, T param1)
		{
			try
			{
				action(param1);
			}
			catch
			{
				Assert.Fail(String.Format("This action caused an exception in MockFwManager. Action={0}, Param={1}",
					action, param1));
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Opens a new main window for the specified application.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void OpenNewWindowForApp()
		{
			throw new NotImplementedException();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Lets the user chooses a language project and opens it. If the project is already
		/// open in a FieldWorks process, then the request is sent to the running FieldWorks
		/// process and a new window is opened for that project. Otherwise a new FieldWorks
		/// process is started to handle the project request.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void ChooseLangProject()
		{
			throw new NotImplementedException();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Lets the user create a new language project and opens it. If the project is already
		/// open in a FieldWorks process, then the request is sent to the running FieldWorks
		/// process and a new window is opened for that project. Otherwise a new FieldWorks
		/// process is started to handle the new project.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void CreateNewProject()
		{
			throw new NotImplementedException();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Lets the user delete any FW databases that are not currently open
		/// </summary>
		/// <param name="app">The application.</param>
		/// <param name="dialogOwner">The owner of the dialog</param>
		/// ------------------------------------------------------------------------------------
		public void DeleteProject(IFlexApp app, Form dialogOwner)
		{
			throw new NotImplementedException();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Lets the user backup any FW databases that are not currently open
		/// </summary>
		/// <param name="dialogOwner">The owner of the dialog</param>
		/// ------------------------------------------------------------------------------------
		public string BackupProject(Form dialogOwner)
		{
			throw new NotImplementedException();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Restore a project.
		/// </summary>
		/// <param name="fwApp">The FieldWorks application.</param>
		/// <param name="dialogOwner">The dialog owner.</param>
		/// ------------------------------------------------------------------------------------
		public void RestoreProject(IFlexApp fwApp, Form dialogOwner)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Reopens the given FLEx project. This may be necessary if some external process modified the project data.
		/// Currently used when FLExBridge modifies our project during a Send/Receive
		/// </summary>
		/// <param name="project">The project name to re-open</param>
		/// <param name="app"></param>
		public IFlexApp ReopenProject(string project, FwAppArgs app)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		///
		/// </summary>
		public void FileProjectLocation(IFlexApp fwApp, Form dialogOwner)
		{
			throw new NotImplementedException();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Rename the project used by this FieldWorks to the specified new name.
		/// </summary>
		/// <param name="newName">The new name</param>
		/// <returns>True if the rename was successful, false otherwise</returns>
		/// ------------------------------------------------------------------------------------
		public bool RenameProject(string newName)
		{
			throw new NotImplementedException();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Handles a link request. This is expected to handle determining the correct
		/// application to start up on the correct project and passing the link to any newly
		/// started application.
		/// </summary>
		/// <param name="link">The link.</param>
		/// ------------------------------------------------------------------------------------
		public void HandleLinkRequest(FwAppArgs link)
		{
			throw new NotImplementedException();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Archive selected project files using RAMP
		/// </summary>
		/// <param name="fwApp">The FieldWorks application</param>
		/// <param name="dialogOwner">The owner of the dialog</param>
		/// <returns>The list of the files to archive, or <c>null</c> if the user cancels the
		/// archive dialog</returns>
		/// ------------------------------------------------------------------------------------
		public List<string> ArchiveProjectWithRamp(IFlexApp fwApp, Form dialogOwner)
		{
			throw new NotImplementedException();
		}
	}

#if RANDYTODO
	public class MockFwXApp : FwXApp
	{
		public MockFwXApp(IFieldWorksManager fwManager, IHelpTopicProvider helpTopicProvider, FwAppArgs appArgs)
			: base(fwManager, helpTopicProvider, appArgs)
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates a main FLEx window.
		/// </summary>
		/// <param name="progressDlg">The progress DLG.</param>
		/// <param name="isNewCache">if set to <c>true</c> [is new cache].</param>
		/// <param name="wndCopyFrom">The WND copy from.</param>
		/// <param name="fOpeningNewProject">if set to <c>true</c> [f opening new project].</param>
		/// ------------------------------------------------------------------------------------
		public override Form NewMainAppWnd(IProgress progressDlg, bool isNewCache,
			Form wndCopyFrom, bool fOpeningNewProject)
		{
			if (progressDlg != null)
				progressDlg.Message = String.Format("Creating window for MockFwXApp {0}", Cache.ProjectId.Name);
			Form form = base.NewMainAppWnd(progressDlg, isNewCache, wndCopyFrom, fOpeningNewProject);

			if (form is IFwMainWnd)
			{
				IFwMainWnd wnd = (IFwMainWnd)form;

				m_activeMainWindow = form;
			}
			return form;
		}

		/// <summary>
		/// Provides a hook for initializing the cache in application-specific ways.
		/// </summary>
		/// <param name="progressDlg">The progress dialog.</param>
		/// <returns>True if the initialization was successful, false otherwise</returns>
		/// ------------------------------------------------------------------------------------
		public override bool InitCacheForApp(IThreadedProgress progressDlg)
		{
			return true;
		}

		public override void RemoveWindow(IFwMainWnd fwMainWindow)
		{
			//base.RemoveWindow(fwMainWindow); We never added it.
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the full path of the product executable filename
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public override string ProductExecutableFile
		{
			get { throw new NotImplementedException(); }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the name of the application.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public override string ApplicationName
		{
			get { return "FLEx"; }
		}

		protected override string SettingsKeyName
		{
			get
			{
				return FwSubKey.LexText;
			}
		}
	}

	[TestFixture]
	public abstract class XWorksAppTestBase : MemoryOnlyBackendProviderTestBase
	{
		protected IFwMainWnd m_window; // defined and disposed here but created in subclass
		protected MockFwXApp m_application;
		protected string m_configFilePath;

		private ICmPossibilityFactory m_possFact;
		private ICmPossibilityRepository m_possRepo;
		private IPartOfSpeechFactory m_posFact;
		private IPartOfSpeechRepository m_posRepo;
		private ILexEntryFactory m_entryFact;
		private ILexSenseFactory m_senseFact;
		private IMoStemAllomorphFactory m_stemFact;
		private IMoAffixAllomorphFactory m_affixFact;

		protected XWorksAppTestBase()
		{
			m_application =null;
		}

		//this needs to set the m_application and be called separately from the constructor because nunit runs the
		//default constructor on all of the fixtures before showing anything...
		//and since multiple fixtures will start Multiple FieldWorks applications,
		//this shows multiple splash screens before we have done anything, and
		//runs afoul of the code which enforces only one FieldWorks application defined in the process
		//at any one time.
		protected abstract void Init();

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Instantiate a TestXCoreApp object.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[TestFixtureSetUp]
		public virtual void FixtureInit()
		{
			FwRegistrySettings.Init();
			SetupEverythingButBase();
			Init(); // subclass version must create and set m_application

			m_configFilePath = Path.Combine(FwDirectoryFinder.CodeDirectory, @"Language Explorer", @"Configuration", @"Main.xml");

			// Setup for possibility loading [GetPossibilityOrCreateOne()]
			// and test data creation
			SetupFactoriesAndRepositories();

			/* note that someday, when we write a test to test the persistence function,
			 * set "TestRestoringFromTestSettings" the second time the application has run in order to pick up
			 * the settings from the first run. The code for this is already in xWindow.
			 */

			//m_window.Show(); Why?
			Application.DoEvents();//without this, tests may fail non-deterministically
		}

		private void SetupFactoriesAndRepositories()
		{
			Assert.True(Cache != null, "No cache yet!?");
			var servLoc = Cache.ServiceLocator;
			m_possFact = servLoc.GetInstance<ICmPossibilityFactory>();
			m_possRepo = servLoc.GetInstance<ICmPossibilityRepository>();
			m_posFact = servLoc.GetInstance<IPartOfSpeechFactory>();
			m_posRepo = servLoc.GetInstance<IPartOfSpeechRepository>();
			m_entryFact = servLoc.GetInstance<ILexEntryFactory>();
			m_senseFact = servLoc.GetInstance<ILexSenseFactory>();
			m_stemFact = servLoc.GetInstance<IMoStemAllomorphFactory>();
			m_affixFact = servLoc.GetInstance<IMoAffixAllomorphFactory>();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure the TestXCoreApp object is destroyed.
		/// Especially since the splash screen it puts up needs to be closed.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[TestFixtureTearDown]
		public void FixtureCleanUp()
		{
			TearDown();
			m_application.Dispose();
			if (m_window != null)
			{
				m_window.Close();
				m_window.Dispose();
				m_window = null;
			}
			m_application = null;
			FwRegistrySettings.Release();
		}

#if RANDYTODO
		protected ITestableUIAdapter Menu
		{
			get
			{
				try
				{
					return (ITestableUIAdapter)this.m_window.MenuAdapter;
				}
				catch (InvalidCastException)
				{
					throw new ApplicationException ("The installed Adapter does not yet ITestableUIAdapter support ");
				}
			}

		protected Command GetCommand (string commandName)
		{
			Command command = (Command)this.m_window.Mediator.CommandSet[commandName];
			if (command == null)
				throw new ApplicationException ("whoops, there is no command with the id " + commandName);
			return command;
		}

		protected void DoCommand (string commandName)
		{
			GetCommand(commandName).InvokeCommand();
			//let the screen redraw
			Application.DoEvents();
		}

		protected void SetTool(string toolValueName)
		{
			//use the Tool menu to select the requested tool
			//(and don't specify anything about the view, so we will get the default)
			Menu.ClickItem("Tools", toolValueName);
		}

		protected void DoCommandRepeatedly(string commandName, int times)
		{
			Command command = GetCommand(commandName);
			for(int i=0; i<times; i++)
			{
				command.InvokeCommand();
				//let the screen redraw
				Application.DoEvents();
			}
		}
#endif

		#region Data Setup methods

		/// <summary>
		/// Will find a morph type (if one exists) with the given (analysis ws) name.
		/// If not found, will create the morph type in the Lexicon MorphTypes list.
		/// </summary>
		/// <param name="morphTypeName"></param>
		/// <returns></returns>
		protected IMoMorphType GetMorphTypeOrCreateOne(string morphTypeName)
		{
			Assert.IsNotNull(m_possFact, "Fixture Initialization is not complete.");
			Assert.IsNotNull(m_window, "No window.");
			var poss = m_possRepo.AllInstances().Where(
				someposs => someposs.Name.AnalysisDefaultWritingSystem.Text == morphTypeName).FirstOrDefault();
			if (poss != null)
				return poss as IMoMorphType;
			var owningList = Cache.LangProject.LexDbOA.MorphTypesOA;
			Assert.IsNotNull(owningList, "No MorphTypes property on Lexicon object.");
			var ws = Cache.DefaultAnalWs;
			poss = m_possFact.Create(new Guid(), owningList);
			poss.Name.set_String(ws, morphTypeName);
			return poss as IMoMorphType;
		}

		/// <summary>
		/// Will find a variant entry type (if one exists) with the given (analysis ws) name.
		/// If not found, will create the variant entry type in the Lexicon VariantEntryTypes list.
		/// </summary>
		/// <param name="variantTypeName"></param>
		/// <returns></returns>
		protected ILexEntryType GetVariantTypeOrCreateOne(string variantTypeName)
		{
			Assert.IsNotNull(m_possFact, "Fixture Initialization is not complete.");
			Assert.IsNotNull(m_window, "No window.");
			var poss = m_possRepo.AllInstances().Where(
				someposs => someposs.Name.AnalysisDefaultWritingSystem.Text == variantTypeName).FirstOrDefault();
			if (poss != null)
				return poss as ILexEntryType;
			// shouldn't get past here; they're already defined.
			var owningList = Cache.LangProject.LexDbOA.VariantEntryTypesOA;
			Assert.IsNotNull(owningList, "No VariantEntryTypes property on Lexicon object.");
			var ws = Cache.DefaultAnalWs;
			poss = m_possFact.Create(new Guid(), owningList);
			poss.Name.set_String(ws, variantTypeName);
			return poss as ILexEntryType;
		}

		/// <summary>
		/// Will find a grammatical category (if one exists) with the given (analysis ws) name.
		/// If not found, will create a category as a subpossibility of a grammatical category.
		/// </summary>
		/// <param name="catName"></param>
		/// <param name="owningCategory"></param>
		/// <returns></returns>
		protected IPartOfSpeech GetGrammaticalCategoryOrCreateOne(string catName, IPartOfSpeech owningCategory)
		{
			return GetGrammaticalCategoryOrCreateOne(catName, null, owningCategory);
		}

		/// <summary>
		/// Will find a grammatical category (if one exists) with the given (analysis ws) name.
		/// If not found, will create the grammatical category in the owning list.
		/// </summary>
		/// <param name="catName"></param>
		/// <param name="owningList"></param>
		/// <returns></returns>
		protected IPartOfSpeech GetGrammaticalCategoryOrCreateOne(string catName, ICmPossibilityList owningList)
		{
			return GetGrammaticalCategoryOrCreateOne(catName, owningList, null);
		}

		/// <summary>
		/// Will find a grammatical category (if one exists) with the given (analysis ws) name.
		/// If not found, will create a grammatical category either as a possibility of a list,
		/// or as a subpossibility of a category.
		/// </summary>
		/// <param name="catName"></param>
		/// <param name="owningList"></param>
		/// <param name="owningCategory"></param>
		/// <returns></returns>
		protected IPartOfSpeech GetGrammaticalCategoryOrCreateOne(string catName, ICmPossibilityList owningList,
			IPartOfSpeech owningCategory)
		{
			Assert.True(m_posFact != null, "Fixture Initialization is not complete.");
			Assert.True(m_window != null, "No window.");
			var category = m_posRepo.AllInstances().Where(
				someposs => someposs.Name.AnalysisDefaultWritingSystem.Text == catName).FirstOrDefault();
			if (category != null)
				return category;
			var ws = Cache.DefaultAnalWs;
			if (owningList == null)
			{
				if (owningCategory == null)
					throw new ArgumentException(
						"Grammatical category not found and insufficient information given to create one.");
				category = m_posFact.Create(new Guid(), owningCategory);
			}
			else
				category = m_posFact.Create(new Guid(), owningList);
			category.Name.set_String(ws, catName);
			return category;
		}

		protected ILexEntry AddLexeme(IList<ICmObject> addList, string lexForm, string citationForm,
			IMoMorphType morphTypePoss, string gloss, IPartOfSpeech catPoss)
		{
			var ws = Cache.DefaultVernWs;
			var le = AddLexeme(addList, lexForm, morphTypePoss, gloss, catPoss);
			le.CitationForm.set_String(ws, citationForm);
			return le;
		}

		protected ILexEntry AddLexeme(IList<ICmObject> addList, string lexForm, IMoMorphType morphTypePoss,
			string gloss, IPartOfSpeech categoryPoss)
		{
			var msa = new SandboxGenericMSA { MainPOS = categoryPoss };
			var comp = new LexEntryComponents { MorphType = morphTypePoss, MSA = msa };
			comp.GlossAlternatives.Add(TsStringUtils.MakeString(gloss, Cache.DefaultAnalWs));
			comp.LexemeFormAlternatives.Add(TsStringUtils.MakeString(lexForm, Cache.DefaultVernWs));
			var entry = m_entryFact.Create(comp);
			addList.Add(entry);
			return entry;
		}

		protected ILexEntry AddVariantLexeme(IList<ICmObject> addList, IVariantComponentLexeme origLe,
			string lexForm, IMoMorphType morphTypePoss, string gloss, IPartOfSpeech categoryPoss,
			ILexEntryType varType)
		{
			Assert.IsNotNull(varType, "Need a variant entry type!");
			var msa = new SandboxGenericMSA { MainPOS = categoryPoss };
			var comp = new LexEntryComponents { MorphType = morphTypePoss, MSA = msa };
			comp.GlossAlternatives.Add(TsStringUtils.MakeString(gloss, Cache.DefaultAnalWs));
			comp.LexemeFormAlternatives.Add(TsStringUtils.MakeString(lexForm, Cache.DefaultVernWs));
			var entry = m_entryFact.Create(comp);
			var ler = entry.MakeVariantOf(origLe, varType);
			addList.Add(entry);
			addList.Add(ler);
			return entry;
		}

		protected ILexSense AddSenseToEntry(IList<ICmObject> addList, ILexEntry le, string gloss,
			IPartOfSpeech catPoss)
		{
			var msa = new SandboxGenericMSA();
			msa.MainPOS = catPoss;
			var sense = m_senseFact.Create(le, msa, gloss);
			addList.Add(sense);
			return sense;
		}

		protected ILexSense AddSubSenseToSense(IList<ICmObject> addList, ILexSense ls, string gloss,
			IPartOfSpeech catPoss)
		{
			var msa = new SandboxGenericMSA();
			msa.MainPOS = catPoss;
			var sense = m_senseFact.Create(new Guid(), ls);
			sense.SandboxMSA = msa;
			sense.Gloss.set_String(Cache.DefaultAnalWs, gloss);
			addList.Add(sense);
			return sense;
		}

		protected void AddStemAllomorphToEntry(IList<ICmObject> addList, ILexEntry le, string alloName,
			IPhEnvironment env)
		{
			var allomorph = m_stemFact.Create();
			le.AlternateFormsOS.Add(allomorph);
			if (env != null)
				allomorph.PhoneEnvRC.Add(env);
			allomorph.Form.set_String(Cache.DefaultVernWs, alloName);
			addList.Add(allomorph);
		}

		protected void AddAffixAllomorphToEntry(IList<ICmObject> addList, ILexEntry le, string alloName,
			IPhEnvironment env)
		{
			var allomorph = m_affixFact.Create();
			le.AlternateFormsOS.Add(allomorph);
			if (env != null)
				allomorph.PhoneEnvRC.Add(env);
			allomorph.Form.set_String(Cache.DefaultVernWs, alloName);
			addList.Add(allomorph);
		}

		#endregion
	}
#endif
}
