// Copyright (c) 2019-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using SIL.LCModel;

namespace LanguageExplorer
{
	/// <summary>
	/// Add List area specific behavior.
	/// </summary>
	internal interface IListTool : ITool
	{
		ICmPossibilityList MyList { get; }
	}
}