// Copyright (c) 2015-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

namespace SIL.FieldWorks.Common.FwUtils
{
	/// <summary>
	/// Implementors can provide an IPropertyTable
	/// </summary>
	public interface IPropertyTableProvider
	{
		/// <summary>
		/// Placement in the IPropertyTableProvider interface lets FwApp call IPropertyTable.DoStuff.
		/// </summary>
		IPropertyTable PropertyTable
		{
			get;
		}
	}
}