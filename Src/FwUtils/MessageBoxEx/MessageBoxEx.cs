// Copyright (c) 2005-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
// From:http://www.codeproject.com/cs/miscctrl/MessageBoxEx.asp

using System;
using System.Drawing;
using System.Windows.Forms;
using SIL.Code;

namespace SIL.FieldWorks.Common.FwUtils.MessageBoxEx
{
	/// <summary>
	/// An extended MessageBox with lot of customizing capabilities.
	/// </summary>
	public sealed class MessageBoxEx : IDisposable
	{
		private MessageBoxExForm _msgBox = new MessageBoxExForm();

		#region Properties

		/// <summary>
		/// Get/Set the name of the message box.
		/// </summary>
		internal string Name { get; set; } = null;

		/// <summary>
		/// Sets the caption of the message box
		/// </summary>
		public string Caption
		{
			set => _msgBox.Caption = value;
		}

		/// <summary>
		/// Sets the text of the message box
		/// </summary>
		public string Text
		{
			set => _msgBox.Message = value;
		}

		/// <summary>
		/// Sets the icon to show in the message box
		/// </summary>
		public Icon CustomIcon
		{
			set => _msgBox.CustomIcon = value;
		}

		/// <summary>
		/// Sets the icon to show in the message box
		/// </summary>
		public MessageBoxExIcon Icon
		{
			set => _msgBox.StandardIcon = (MessageBoxIcon)Enum.Parse(typeof(MessageBoxIcon), value.ToString());
		}

		/// <summary>
		/// Sets the font for the text of the message box
		/// </summary>
		public Font Font
		{
			set => _msgBox.Font = value;
		}

		/// <summary>
		/// Sets or Gets the ability of the  user to save his/her response
		/// </summary>
		public bool AllowSaveResponse
		{
			get => _msgBox.AllowSaveResponse;
			set => _msgBox.AllowSaveResponse = value;
		}

		/// <summary>
		/// Sets the text to show to the user when saving his/her response
		/// </summary>
		public string SaveResponseText
		{
			set => _msgBox.SaveResponseText = value;
		}

		/// <summary>
		/// Sets or Gets whether the saved response if available should be used
		/// </summary>
		public bool UseSavedResponse { get; set; } = true;

		/// <summary>
		/// Sets or Gets the time in milliseconds for which the message box is displayed.
		/// </summary>
		public int Timeout
		{
			get => _msgBox.Timeout;
			set => _msgBox.Timeout = value;
		}

		#endregion

		#region Methods
		/// <summary>
		/// Shows the message box
		/// </summary>
		public string Show()
		{
			return Show(null);
		}

		/// <summary>
		/// Shows the message box with the specified owner
		/// </summary>
		public string Show(IWin32Window owner)
		{
			if (UseSavedResponse && Name != null)
			{
				var savedResponse = MessageBoxExManager.GetSavedResponse(this);
				if (savedResponse != null)
				{
					return savedResponse;
				}
			}
			if (owner == null)
			{
				_msgBox.Name = Name;//needed for nunitforms support
				_msgBox.ShowDialog();
			}
			else
			{
				_msgBox.ShowDialog(owner);
			}
			if (Name != null)
			{
				if (_msgBox.AllowSaveResponse && _msgBox.SaveResponse)
				{
					MessageBoxExManager.SetSavedResponse(this, _msgBox.Result);
				}
				else
				{
					MessageBoxExManager.ResetSavedResponse(this.Name);
				}
			}
			else
			{
				Dispose();
			}

			return _msgBox.Result;
		}

		/// <summary>
		/// Add a custom button to the message box
		/// </summary>
		/// <param name="button">The button to add</param>
		public void AddButton(MessageBoxExButton button)
		{
			if (button == null)
			{
				throw new ArgumentNullException(nameof(button), "A null button cannot be added");
			}

			_msgBox.Buttons.Add(button);

			if (button.IsCancelButton)
			{
				_msgBox.CustomCancelButton = button;
			}
		}

		/// <summary>
		/// Add a custom button to the message box
		/// </summary>
		/// <param name="text">The text of the button</param>
		/// <param name="val">The return value in case this button is clicked</param>
		public void AddButton(string text, string val)
		{
			Guard.AgainstNullOrEmptyString(text, nameof(text));
			Guard.AgainstNullOrEmptyString(val, nameof(val));

			var button = new MessageBoxExButton
			{
				Text = text,
				Value = val
			};

			AddButton(button);
		}

		/// <summary>
		/// Add a standard button to the message box
		/// </summary>
		/// <param name="button">The standard button to add</param>
		public void AddButton(MessageBoxExButtons button)
		{
			var buttonText = MessageBoxExManager.GetLocalizedString(button.ToString()) ?? button.ToString();
			var buttonVal = button.ToString();
			var btn = new MessageBoxExButton
			{
				Text = buttonText,
				Value = buttonVal
			};

			if (button == MessageBoxExButtons.Cancel)
			{
				btn.IsCancelButton = true;
			}

			AddButton(btn);
		}

		#endregion

		/// <summary>
		/// Ctor is internal because this can only be created by MBManager
		/// </summary>
		internal MessageBoxEx()
		{
		}

		~MessageBoxEx()
		{
			Dispose(false);
		}

		/// <inheritdoc />
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool IsDisposed { get; set; }

		private void Dispose(bool disposing)
		{
			System.Diagnostics.Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ******");
			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}

			if (disposing)
			{
				_msgBox?.Dispose();
			}

			_msgBox = null;

			IsDisposed = true;
		}
	}
}