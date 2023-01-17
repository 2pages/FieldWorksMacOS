// Copyright (c) 2014-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;

namespace LanguageExplorer.Controls
{
	/// <summary>
	/// This is the search engine for RecordGoDlg.
	/// </summary>
	internal sealed class RecordGoSearchEngine : SearchEngine
	{
		internal RecordGoSearchEngine(LcmCache cache)
			: base(cache, SearchType.FullText)
		{
		}

		protected override IEnumerable<ITsString> GetStrings(SearchField field, ICmObject obj)
		{
			var rec = (IRnGenericRec)obj;
			switch (field.Flid)
			{
				case RnGenericRecTags.kflidTitle:
					var title = rec.Title;
					if (title != null && title.Length > 0)
					{
						yield return title;
					}
					break;

				default:
					throw new ArgumentException("Unrecognized field.", nameof(field));
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
			return field.Flid == RnGenericRecTags.kflidTitle ? false : throw new ArgumentException("Unrecognized field.", nameof(field));
		}

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + " ******");
			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}
			base.Dispose(disposing);
		}
	}
}