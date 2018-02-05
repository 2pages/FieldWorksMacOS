﻿// Copyright (c) 2014-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.IO;

namespace LanguageExplorer
{
	/// <summary>
	/// An ISimpleLogger can be temporarily passed to a class which is not Dispsable in place of a TextWriter.
	/// This makes it unambiguous that the class using the logger is not responsible to dispose of it.
	/// The actual class is disposable and should normally be created in a Using clause.
	/// The logger can also track an indent.
	/// </summary>
	internal sealed class SimpleLogger : ISimpleLogger
	{
		/// <summary>
		/// Make one (on a memory stream the logger is responsible for).
		/// </summary>
		public SimpleLogger()
		{
			m_stream = new MemoryStream();
			m_writer = new StreamWriter(m_stream);
		}

		private MemoryStream m_stream;
		private int m_indent;
		private TextWriter m_writer;
		/// <summary>
		/// For logging nested structures, increments the current indent level.
		/// </summary>
		public void IncreaseIndent()
		{
			CheckDisposed();
			m_indent++;
		}

		/// <summary>
		/// For logging nested structures, decrements the current indent level.
		/// </summary>
		public void DecreaseIndent()
		{
			CheckDisposed();
			m_indent--;
		}

		/// <summary>
		/// Write a line of text to the log (preceded by the current indent).
		/// </summary>
		/// <exception cref="ObjectDisposedException">If called after being disposed</exception>
		public void WriteLine(string text)
		{
			CheckDisposed();
			for (var i = 0; i < m_indent; i++)
			{
				m_writer.Write("    ");
			}
			m_writer.WriteLine(text);
		}

		public bool HasContent
		{
			get
			{
				CheckDisposed();
				m_writer.Flush();
				return m_stream.Length > 0;
			}
		}

		/// <summary>
		/// Get the text that has been written to the stream.
		/// Immediately disposes itself (a developer wishing to change this behavior in the future is welcome to).
		/// </summary>
		public string Content
		{
			get
			{
				CheckDisposed();
				m_writer.Flush();
				using (var sr = new StreamReader(m_stream))
				using (this) // reading m_stream destroys it, so dispose the whole logger afterwards
				{
					m_stream.Seek(0, SeekOrigin.Begin);
					return sr.ReadToEnd();
				}
			}
		}

		#if DEBUG
		/// <summary/>
		~SimpleLogger()
		{
			Dispose(false);
		}
		#endif

		/// <summary/>
		public bool IsDisposed { get; private set; }

		/// <summary/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Check to see if the object has been disposed. All public Properties and Methods should call this before doing anything else.
		/// </summary>
		public void CheckDisposed()
		{
			if (IsDisposed)
			{
				throw new ObjectDisposedException($"'{GetType().Name}' in use after being disposed.");
			}
		}

		/// <summary>
		/// As a special case, this class does not HAVE to be disposed if it does not allow pictures.
		/// </summary>
		/// <param name="disposing"></param>
		private void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****************** Missing Dispose() call for " + GetType().Name + ". ******************");

			// Must not be run more than once.
			if (IsDisposed)
			{
				return;
			}
			IsDisposed = true;

			if (disposing)
			{
				m_writer.Dispose();
				m_stream.Dispose();
			}
		}
	}
}
