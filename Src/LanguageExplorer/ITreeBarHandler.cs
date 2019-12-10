// Copyright (c) 2017-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using SIL.LCModel;

namespace LanguageExplorer
{
	internal interface ITreeBarHandler : IDisposable
	{
		bool IsItemInTree(int hvo);
		void PopulateRecordBarIfNeeded(IRecordList list);
		void PopulateRecordBar(IRecordList list);
		void UpdateSelection(ICmObject currentObject);
		void ReloadItem(ICmObject currentObject);
		void ReleaseRecordBar();
	}
}