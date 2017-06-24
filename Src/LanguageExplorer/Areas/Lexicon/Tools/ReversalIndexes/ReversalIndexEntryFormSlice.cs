// Copyright (c) 2015-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Xml.Linq;
using SIL.FieldWorks.Common.Framework.DetailControls;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.DomainServices;

namespace LanguageExplorer.Areas.Lexicon.Tools.ReversalIndexes
{
	/// <summary>
	/// Summary description for ReversalIndexEntryFormSlice.
	/// </summary>
	internal sealed class ReversalIndexEntryFormSlice : MultiStringSlice
	{
#pragma warning disable 0414
		private XElement m_configNode = null;
		private IPersistenceProvider m_persistProvider = null;
#pragma warning restore 0414

		/// <summary />
		public ReversalIndexEntryFormSlice(FdoCache cache, string editor, int flid, XElement node,
			ICmObject obj, IPersistenceProvider persistenceProvider, int ws)
			: base(obj, flid, WritingSystemServices.kwsAllReversalIndex, 0, false, true, true)
		{
			m_configNode = node;
			m_persistProvider = persistenceProvider;
		}
	}
}
