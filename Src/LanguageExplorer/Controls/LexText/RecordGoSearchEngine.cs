﻿// Copyright (c) 2015-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Linq;
using LanguageExplorer.Controls.XMLViews;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel;

namespace LanguageExplorer.Controls.LexText
{
	/// <summary>
	/// This is the search engine for RecordGoDlg.
	/// </summary>
	internal class RecordGoSearchEngine : SearchEngine
	{
		public RecordGoSearchEngine(LcmCache cache)
			: base(cache, SearchType.FullText)
		{
		}

		protected override IEnumerable<ITsString> GetStrings(SearchField field, ICmObject obj)
		{
			var rec = (IRnGenericRec) obj;
			switch (field.Flid)
			{
				case RnGenericRecTags.kflidTitle:
					var title = rec.Title;
					if (title != null && title.Length > 0)
						yield return title;
					break;

				default:
					throw new ArgumentException("Unrecognized field.", "field");
			}
		}

		protected override IList<ICmObject> GetSearchableObjects()
		{
			return Cache.ServiceLocator.GetInstance<IRnGenericRecRepository>().AllInstances().Cast<ICmObject>().ToArray();
		}

		protected override bool IsIndexResetRequired(int hvo, int flid)
		{
			switch (flid)
			{
				case RnResearchNbkTags.kflidRecords:
				case RnGenericRecTags.kflidSubRecords:
				case RnGenericRecTags.kflidTitle:
					return true;
			}

			return false;
		}

		protected override bool IsFieldMultiString(SearchField field)
		{
			switch (field.Flid)
			{
				case RnGenericRecTags.kflidTitle:
					return false;
			}

			throw new ArgumentException("Unrecognized field.", "field");
		}
	}
}
