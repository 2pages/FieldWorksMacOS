// Copyright (c) 2003-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using NUnit.Framework;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;

namespace SIL.FieldWorks.Common.RootSites
{
	/// <summary>
	/// Tests the UndoTaskHelperTests class
	/// </summary>
	[TestFixture]
	internal class UndoTaskHelperTests : RootsiteDummyViewTestsBase
	{
		public override void TestSetup()
		{
			base.TestSetup();

			DummyUndoTaskHelper.m_fRollbackAction = true;
			DummyUndoTaskHelper.m_fRollbackCalled = false;

			m_actionHandler.EndUndoTask();
		}

		/// <summary>
		/// Tests that UndoTaskHelper begins and ends a undo task
		/// </summary>
		[Test]
		public void BeginAndEndUndoTask()
		{
			ShowForm();
			// we need a selection
			m_basicView.RootBox.MakeSimpleSel(true, true, false, true);

			// this should begin an outer undo task, so we will have only one undoable task!
			using (var helper = new UndoTaskHelper(m_actionHandler, m_basicView, "kstidUndoStyleChanges"))
			{
				var book = Cache.ServiceLocator.GetInstance<IScrBookFactory>().Create(6);
				Cache.LanguageProject.TranslatedScriptureOA.ScriptureBooksOS.Add(book);

				book = Cache.ServiceLocator.GetInstance<IScrBookFactory>().Create(7);
				Cache.LanguageProject.TranslatedScriptureOA.ScriptureBooksOS.Add(book);
				helper.RollBack = false;
			}
			var nUndoTasks = 0;
			while (m_actionHandler.CanUndo())
			{
				Assert.AreEqual(UndoResult.kuresSuccess, m_actionHandler.Undo());
				nUndoTasks++;
			}
			Assert.AreEqual(1, nUndoTasks);
		}

		/// <summary>
		/// Tests that Dispose gets called after we get an unhandled exception and that the
		/// action is rolled back.
		/// </summary>
		[Test]
		public void EndUndoCalledAfterUnhandledException()
		{
			ShowForm();
			// we need a selection
			m_basicView.RootBox.MakeSimpleSel(true, true, false, true);

			try
			{
				using (new DummyUndoTaskHelper(m_actionHandler, m_basicView, "kstidUndoStyleChanges"))
				{
					throw new Exception(); // this throws us out of the using statement
				}
			}
			catch
			{
				// just catch the exception so that we can test if undo task was ended
			}

			Assert.IsTrue(DummyUndoTaskHelper.m_fRollbackAction);
			Assert.IsTrue(DummyUndoTaskHelper.m_fRollbackCalled);
		}

		/// <summary>
		/// Test that Dispose gets not called after handled exception
		/// </summary>
		[Test]
		public void EndUndoNotCalledAfterHandledException()
		{
			ShowForm();
			// we need a selection
			m_basicView.RootBox.MakeSimpleSel(true, true, false, true);

			try
			{
				using (new DummyUndoTaskHelper(m_basicView, "kstidUndoStyleChanges"))
				{
					throw new Exception();
				}
			}
			catch
			{
				// just catch the exception so that we can test if undo task was ended
			}

			Assert.IsTrue(DummyUndoTaskHelper.m_fRollbackAction);
			Assert.IsTrue(DummyUndoTaskHelper.m_fRollbackCalled);
		}

		/// <summary>
		/// Tests that a save point gets set and rolled back after exception
		/// </summary>
		[Test]
		public void AutomaticRollbackAfterException()
		{
			ShowForm();
			// we need a selection
			m_basicView.RootBox.MakeSimpleSel(true, true, false, true);

			try
			{
				using (new DummyUndoTaskHelper(m_basicView, "kstidUndoStyleChanges"))
				{
					throw new Exception();
				}
			}
			catch
			{
				// just catch the exception so that we can test if undo task was ended
			}

			Assert.IsTrue(DummyUndoTaskHelper.m_fRollbackAction);
			Assert.IsTrue(DummyUndoTaskHelper.m_fRollbackCalled);

			// This re-runs the test to make sure that the undo task was ended properly
			DummyUndoTaskHelper.m_fRollbackAction = true;
			DummyUndoTaskHelper.m_fRollbackCalled = false;
			try
			{
				using (new DummyUndoTaskHelper(m_basicView, "kstidUndoStyleChanges"))
				{
					throw new Exception();
				}
			}
			catch
			{
				// just catch the exception so that we can test if undo task was ended
			}
			Assert.IsTrue(DummyUndoTaskHelper.m_fRollbackAction);
			Assert.IsTrue(DummyUndoTaskHelper.m_fRollbackCalled);
		}

		/// <summary>
		/// Tests that a save point gets set and not rolled back when no exception happens
		/// </summary>
		[Test]
		public void NoRollbackAfterNoException()
		{
			ShowForm();
			// we need a selection
			m_basicView.RootBox.MakeSimpleSel(true, true, false, true);

			using (var helper = new DummyUndoTaskHelper(m_actionHandler, m_basicView, "kstidUndoStyleChanges"))
			{
				// we have to explicitly indicate that the action not be rolled back at the end
				// of the statements
				helper.RollBack = false;
			}

			Assert.IsFalse(DummyUndoTaskHelper.m_fRollbackAction);
			Assert.IsFalse(DummyUndoTaskHelper.m_fRollbackCalled);
		}

		/// <summary />
		private sealed class DummyUndoTaskHelper : UndoTaskHelper
		{
			internal static bool m_fRollbackAction = true;
			internal static bool m_fRollbackCalled;

			/// <summary>
			/// Start the undo task
			/// </summary>
			internal DummyUndoTaskHelper(IActionHandler actionHandler, RootSite rootSite, string stid)
				: base(actionHandler, rootSite, stid)
			{
			}

			/// <summary>
			/// Start the undo task
			/// </summary>
			internal DummyUndoTaskHelper(RootSite rootSite, string stid) : base(rootSite, stid)
			{
			}

			/// <summary>
			/// End the undo task and call commit on root site
			/// </summary>
			protected override void EndUndoTask()
			{
				base.EndUndoTask();
				m_fRollbackAction = false;
			}

			/// <summary>
			/// Rollback to the save point
			/// </summary>
			protected override void RollBackChanges()
			{
				base.RollBackChanges();
				m_fRollbackCalled = true;
			}
		}
	}
}