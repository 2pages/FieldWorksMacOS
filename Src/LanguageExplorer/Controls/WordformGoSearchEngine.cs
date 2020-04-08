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
using SIL.LCModel.DomainImpl;

namespace LanguageExplorer.Controls
{
	/// <summary>
	/// This is the search engine for WordformGoDlg.
	/// </summary>
	internal sealed class WordformGoSearchEngine : SearchEngine
	{
		private readonly Virtuals m_virtuals;

		internal WordformGoSearchEngine(LcmCache cache)
			: base(cache, SearchType.Prefix)
		{
			m_virtuals = Cache.ServiceLocator.GetInstance<Virtuals>();
		}

		protected override IEnumerable<ITsString> GetStrings(SearchField field, ICmObject obj)
		{
			switch (field.Flid)
			{
				case WfiWordformTags.kflidForm:
					var form = ((IWfiWordform)obj).Form.StringOrNull(field.String.get_WritingSystemAt(0));
					if (form != null && form.Length > 0)
					{
						yield return form;
					}
					break;
				default:
					throw new ArgumentException(@"Unrecognized field.", nameof(field));
			}
		}

		protected override IList<ICmObject> GetSearchableObjects()
		{
			return Cache.ServiceLocator.GetInstance<IWfiWordformRepository>().AllInstances().Cast<ICmObject>().ToArray();
		}

		protected override bool IsIndexResetRequired(int hvo, int flid)
		{
			return flid == m_virtuals.LangProjectAllWordforms || flid == WfiWordformTags.kflidForm;
		}

		protected override bool IsFieldMultiString(SearchField field)
		{
			return field.Flid == WfiWordformTags.kflidForm ? true : throw new ArgumentException(@"Unrecognized field.", nameof(field));
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