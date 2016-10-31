// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Windows.Forms;

using SIL.FieldWorks.FDO;
using SIL.FieldWorks.Common.RootSites;
using SIL.Utils;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.FieldWorks.Common.Framework.DetailControls;
using SIL.FieldWorks.Common.FwKernelInterfaces;

namespace SIL.FieldWorks.XWorks.LexEd
{
	/// <summary>
	/// Summary description for LexReferenceTreeRootView.
	/// </summary>
	public class LexReferenceTreeRootView : AtomicReferenceView
	{
		public LexReferenceTreeRootView() : base()
		{
		}

		public override void SetReferenceVc()
		{
			CheckDisposed();

			m_atomicReferenceVc = new LexReferenceTreeRootVc(m_fdoCache,
					m_rootObj.Hvo, m_rootFlid, m_displayNameProperty);
		}
	}

	/// <summary>
	///  View constructor for creating the view details.
	/// </summary>
	public class LexReferenceTreeRootVc : AtomicReferenceVc
	{
		protected int m_hvoOwner;

		public LexReferenceTreeRootVc(FdoCache cache, int hvo, int flid, string displayNameProperty)
			: base (cache, flid, displayNameProperty)
		{
			m_hvoOwner = hvo;
		}

		protected override int HvoOfObjectToDisplay(IVwEnv vwenv, int hvo)
		{
			ISilDataAccess sda = vwenv.DataAccess;
			int chvo = sda.get_VecSize(hvo, m_flid);
			if (chvo < 1)
				return 0;
			else
				return sda.get_VecItem(hvo, m_flid, 0);
		}

		protected override void DisplayObjectProperty(IVwEnv vwenv, int hvo)
		{
			vwenv.NoteDependency(new int[] {m_hvoOwner}, new int[] {m_flid}, 1);
			vwenv.AddObj(hvo, this, AtomicReferenceView.kFragObjName);
		}
	}
}
