// Copyright (c) 2013-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

namespace LanguageExplorer.SendReceive
{
	/// <summary>
	/// Enumeration of possible sources of a new FW project.
	/// </summary>
	internal enum ObtainedProjectType
	{
		/// <summary>Default value for no source</summary>
		None,
		/// <summary>Lift repository was the source</summary>
		Lift,
		/// <summary>FW repository was the source</summary>
		FieldWorks
	}
}