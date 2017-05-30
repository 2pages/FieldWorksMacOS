﻿// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;
using SIL.FieldWorks.SharpViews.Selections;

namespace SIL.FieldWorks.SharpViews.Hookups
{
	public abstract class BaseParagraphOperationsFdo<T> : BaseParagraphOperations<T>
	{
		/// <summary>
		/// Insert the indicated text at the start of the new object. The IP indicates where it came from,
		/// which may be significant for subclasses where the display of T is complex. This is used for
		/// moving the tail end of a split string, when inserting a line break into a paragraph. The destination
		/// may be assumed newly created and empty.
		/// </summary>
		public override void InsertAtStartOfNewObject(InsertionPoint source, ITsString move, T destination)
		{
			if (source.Hookup.Tag != 0 && destination is ICmObject)
			{
				// A good default is to assume it is the same property as the source.
				// Subclass needs to override if this will not work.
				var cmObj = (ICmObject)destination;
				cmObj.Cache.DomainDataByFlid.SetString(cmObj.Hvo, source.Hookup.Tag, move);
			}
			else
				base.InsertAtStartOfNewObject(source, move, destination);
		}
	}
}
