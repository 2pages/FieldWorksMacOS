<<<<<<< HEAD:Src/FieldWorks.TestUtilities/Attributes/HandleApplicationThreadExceptionAttribute.cs
// Copyright (c) 2017-2020 SIL International
||||||| f013144d5:Src/Common/FwUtils/FwUtilsTests/Attributes/HandleApplicationThreadExceptionAttribute.cs
﻿// Copyright (c) 2017 SIL International
=======
// Copyright (c) 2017 SIL International
>>>>>>> develop:Src/Common/FwUtils/FwUtilsTests/Attributes/HandleApplicationThreadExceptionAttribute.cs
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace FieldWorks.TestUtilities.Attributes
{
	/// <summary>
	/// Handles unhandled exceptions that occur in Windows Forms threads. This avoids the display of unhandled exception dialogs when
	/// running nunit-console. Avoiding the dialogs is preferable, because it can cause an unattended build to pause, while it waits for
	/// input from the user. In addition, if the user presses "Continue", it makes the test pass. Rethrowing the exception doesn't bring
	/// up the dialog and correctly makes the test fail.
	/// </summary>
	[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = true)]
	public class HandleApplicationThreadExceptionAttribute : TestActionAttribute
	{
<<<<<<< HEAD:Src/FieldWorks.TestUtilities/Attributes/HandleApplicationThreadExceptionAttribute.cs
		/// <inheritdoc />
		public override void BeforeTest(ITest testDetails)
||||||| f013144d5:Src/Common/FwUtils/FwUtilsTests/Attributes/HandleApplicationThreadExceptionAttribute.cs
		/// <summary>
		/// Method called before each test
		/// </summary>
		public override void BeforeTest(TestDetails testDetails)
=======
		/// <summary>
		/// Method called before each test
		/// </summary>
		public override void BeforeTest(ITest test)
>>>>>>> develop:Src/Common/FwUtils/FwUtilsTests/Attributes/HandleApplicationThreadExceptionAttribute.cs
		{
			base.BeforeTest(test);

			Application.ThreadException += OnThreadException;
		}

<<<<<<< HEAD:Src/FieldWorks.TestUtilities/Attributes/HandleApplicationThreadExceptionAttribute.cs
		/// <inheritdoc />
		public override void AfterTest(ITest testDetails)
||||||| f013144d5:Src/Common/FwUtils/FwUtilsTests/Attributes/HandleApplicationThreadExceptionAttribute.cs
		/// <summary>
		/// Method called after each test
		/// </summary>
		public override void AfterTest(TestDetails testDetails)
=======
		/// <summary>
		/// Method called after each test
		/// </summary>
		public override void AfterTest(ITest test)
>>>>>>> develop:Src/Common/FwUtils/FwUtilsTests/Attributes/HandleApplicationThreadExceptionAttribute.cs
		{
			base.AfterTest(test);

			Application.ThreadException -= OnThreadException;
		}

		private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
		{
			throw new ApplicationException(e.Exception.Message, e.Exception);
		}
	}
}
