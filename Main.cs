using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;
using Clarified.Win32;
using System.Threading.Tasks;

namespace Clarified
{
	public partial class Main : Form
	{
		/// <summary>
		/// Default constructor
		/// </summary>
		public Main()
		{
			InitializeProgram();
		}

		private void InitializeProgram()
		{
			// run the automatic winforms code
			InitializeComponent();

			// establish our custom border colors
			PenBorderActive = new Pen(Color.FromArgb(255, 227, 227, 227), 1);
			PenBorderInactive = new Pen(Color.Transparent);
			PenBorderCurrent = PenBorderActive;

			// create our pens for drawing the viewport grid
			PenCrosshair = new Pen(Color.Black, 1);
			PenGrid = new Pen(Color.FromArgb(50, Color.White), 1);

			// establish the viewport defaults
			ViewportWidth = uxViewport.Width;
			ViewportHeight = uxViewport.Height;
			HalfWidth = (ViewportWidth / 2);
			HalfHeight = (ViewportHeight / 2);
			ZoomLevel = 10;
			ZoomWidth = ViewportWidth / ZoomLevel;
			ZoomHeight = ViewportHeight / ZoomLevel;
			ZoomMidPoint = ZoomLevel / 2;
		}

		#region Hook Events
		/// <summary>
		/// A hook event for whenever the mouse is moved
		/// </summary>
		private void HookManager_MouseMove(object sender, MouseEventArgs e)
		{
			var screen = Screen.FromPoint(new Point { X = e.X, Y = e.Y });
			if (CurrentScreen == null || !CurrentScreen.Equals(screen) || StaleScreenShot)
				InitializeCurrentScreen(screen);

			StaleScreenShot = false;
			CurrentX = e.X;
			CurrentY = e.Y;

			if (e.X >= CurrentScreen.Bounds.X && e.Y >= CurrentScreen.Bounds.Y && e.X < CurrentScreen.Bounds.Right && e.Y < CurrentScreen.Bounds.Bottom)
			{
				// get the color relative to the CurrentXY coordinate
				var color = GetScreenshotColorAt(e.X - CurrentScreen.Bounds.X, e.Y - CurrentScreen.Bounds.Y);

				// update the selected color
				UpdateColor(color);
			}

			uxViewport.Invalidate();
		}

		/// <summary>
		/// A hook event for whenever the mouse is clicked
		/// </summary>
		private void HookManager_MouseClick(object sender, MouseEventArgs e)
		{
			// prevent the click from going through
			((MouseEventExtArgs)e).Handled = true;

			// stop listening for events
			EndColorSelection();
		}
		#endregion

		#region Events for Main
		/// <summary>
		/// An event that is raised when a key is pressed
		/// </summary>
		private void Main_KeyPress(object sender, KeyPressEventArgs e)
		{
			if (char.ToUpper(e.KeyChar) == (char)Keys.Z)
				BeginColorSelection();

			if (e.KeyChar == (char)Keys.Escape)
				Close();

			// If this line of code isn't here the user can pick multiple colors without invoking the get a color button. 
			// This is actually a cool feature IMO but it violates the workflow of the initial project.  Remove this line
			// for a less interrupted UX when picking colors with precision. It's cool because it doesn't lock up the screen
			// Like the mouse events do. 
			if (uxStart.Enabled)
				return;

			var screen = Screen.FromPoint(new Point { X = Cursor.Position.X, Y = Cursor.Position.Y });
			if (CurrentScreen == null || !CurrentScreen.Equals(screen))
			{
				InitializeCurrentScreen(screen);
				CurrentX = Cursor.Position.X;
				CurrentY = Cursor.Position.Y;
			}

			switch (char.ToUpper(e.KeyChar))
			{
				case (char)Keys.W:
					if (CurrentY - 1 >= CurrentScreen.Bounds.Y)
						CurrentY -= 1;
					break;
				case (char)Keys.S:
					if (CurrentY + 1 < CurrentScreen.Bounds.Bottom)
						CurrentY += 1;
					break;
				case (char)Keys.A:
					if (CurrentX - 1 >= CurrentScreen.Bounds.X)
						CurrentX -= 1;
					break;
				case (char)Keys.D:
					if (CurrentX + 1 < CurrentScreen.Bounds.Right)
						CurrentX += 1;
					break;
			}
			// get the color relative to the CurrentXY coordinate
			var color = GetScreenshotColorAt(CurrentX - CurrentScreen.Bounds.X, CurrentY - CurrentScreen.Bounds.Y);

			// update the selected color
			UpdateColor(color);

			// start capturing the mouse events
			uxViewport.Invalidate();
			if (e.KeyChar == (char)Keys.Space)
				EndColorSelection();

			StaleScreenShot = true;
		}


		private void InitializeCurrentScreen(Screen screen)
		{
			CurrentScreen = screen;
			ScreenOffsetX = screen.Bounds.X;
			ScreenOffsetY = screen.Bounds.Y;
			CurrentScreenshot = TakeScreenshot(screen);
		}
		/// <summary>
		/// An event that is raised when the window needs painting
		/// </summary>
		private void Main_Paint(object sender, PaintEventArgs e)
		{
			// draw a custom border around the window
			e.Graphics.DrawLine(PenBorderCurrent, 0, 0, 0, Height - 1);
			e.Graphics.DrawLine(PenBorderCurrent, 0, 0, Width - 1, 0);
			e.Graphics.DrawLine(PenBorderCurrent, Width - 1, 0, Width - 1, Height - 1);
			e.Graphics.DrawLine(PenBorderCurrent, 0, Height - 1, Width - 1, Height - 1);
		}

		/// <summary>
		/// An event that is raised when the window has focus
		/// </summary>
		private void Main_Activated(object sender, EventArgs e)
		{
			// set the active border
			PenBorderCurrent = PenBorderActive;
			Invalidate();
		}

		/// <summary>
		/// An event that is raised when the window loses focus
		/// </summary>
		private void Main_Deactivate(object sender, EventArgs e)
		{
			// set the inactive border
			PenBorderCurrent = PenBorderInactive;
			Invalidate();
		}

		/// <summary>
		/// An override to move the window from any part of the form
		/// </summary>
		protected override void WndProc(ref Message m)
		{
			base.WndProc(ref m);
			switch (m.Msg)
			{
				case WM_NCHITTEST:
					if ((int)m.Result == HTCLIENT)
						m.Result = (IntPtr)HTCAPTION;

					break;
			}
		}
		#endregion

		#region Events for uxClose
		/// <summary>
		/// An event that is raised when the mouse is over the close label
		/// </summary>
		private void uxClose_MouseEnter(object sender, EventArgs e)
		{
			// change the background color to #f0f0f0
			uxClose.BackColor = Color.FromArgb(255, 240, 240, 240);
		}

		/// <summary>
		/// An event that is raised when the mouse is no longer over the label
		/// </summary>
		private void uxClose_MouseLeave(object sender, EventArgs e)
		{
			// reset the background color
			uxClose.BackColor = Color.Transparent;
		}

		/// <summary>
		/// An event that is raised when the close label needs to be painted
		/// </summary>
		private void uxClose_Paint(object sender, PaintEventArgs e)
		{
			// paint the cross icon onto the label
			e.Graphics.DrawImage(Properties.Resources.Cross, new Rectangle(11, 11, 10, 10));
		}

		/// <summary>
		/// An event that is raised when the close label is clicked
		/// </summary>
		private void uxClose_Click(object sender, EventArgs e)
		{
			Close();
		}
		#endregion

		#region Events for uxViewport
		/// <summary>
		/// An event that is raised when the viewport is repainted
		/// </summary>
		private void uxViewport_Paint(object sender, PaintEventArgs e)
		{
			if (CurrentScreenshot != null)
			{
				// get the screenshot offsets based on the cursor position
				var x = CurrentX - ScreenOffsetX - (ZoomWidth / 2);
				var y = CurrentY - ScreenOffsetY - (ZoomHeight / 2);

				// define the rectangles for the scale image
				var viewport = new Rectangle(0, 0, ViewportWidth, ViewportHeight);
				var screenshot = new Rectangle(x, y, ZoomWidth, ZoomHeight);
				var square = new Rectangle(HalfWidth - ZoomMidPoint, HalfHeight - ZoomMidPoint, ZoomLevel, ZoomLevel);

				// draw the screenshot at an offset based on the current cursor position
				e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
				e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
				e.Graphics.DrawImage(CurrentScreenshot, viewport, screenshot, GraphicsUnit.Pixel);

				// draw the pixel viewer at the center of the crosshair
				e.Graphics.DrawRectangle(PenCrosshair, square);

				// draw the horizontal piece of the crosshair
				e.Graphics.DrawLine(PenCrosshair, HalfWidth, 0, HalfWidth, HalfHeight - ZoomMidPoint);
				e.Graphics.DrawLine(PenCrosshair, HalfWidth, HalfHeight + ZoomMidPoint, HalfWidth, ViewportHeight);

				// draw the vertical piece of the crosshair
				e.Graphics.DrawLine(PenCrosshair, 0, HalfHeight, HalfWidth - ZoomMidPoint, HalfHeight);
				e.Graphics.DrawLine(PenCrosshair, HalfWidth + ZoomMidPoint, HalfHeight, ViewportWidth, HalfHeight);

				// draw vertical grid from the center out
				for (var i = 0; i < HalfWidth; i += ZoomLevel)
				{
					e.Graphics.DrawLine(PenGrid, HalfWidth + i + ZoomMidPoint, 0, HalfWidth + i + ZoomMidPoint, ViewportHeight);
					e.Graphics.DrawLine(PenGrid, HalfWidth - i - ZoomMidPoint, 0, HalfWidth - i - ZoomMidPoint, ViewportHeight);
				}

				// draw horizontal grid from the center out
				for (var i = 0; i < HalfHeight; i += ZoomLevel)
				{
					// draw a vertical line
					e.Graphics.DrawLine(PenGrid, 0, HalfHeight + i + ZoomMidPoint, ViewportWidth, HalfHeight + i + ZoomMidPoint);
					e.Graphics.DrawLine(PenGrid, 0, HalfHeight - i - ZoomMidPoint, ViewportWidth, HalfHeight - i - ZoomMidPoint);
				}
			}

			// draw a custom border around the window
			e.Graphics.DrawLine(PenBorderActive, 1, 1, 1, uxViewport.Height);
			e.Graphics.DrawLine(PenBorderActive, 1, 1, uxViewport.Width, 1);
			e.Graphics.DrawLine(PenBorderActive, uxViewport.Width, 0, uxViewport.Width, uxViewport.Height);
			e.Graphics.DrawLine(PenBorderActive, 0, uxViewport.Height, uxViewport.Width, uxViewport.Height);
		}
		#endregion

		#region Events for uxStart
		/// <summary>
		/// An event that is raised when the start label is clicked
		/// </summary>
		private void uxStart_MouseUp(object sender, MouseEventArgs e)
		{
			if (uxStart.Enabled)
			{
				// reset the screen
				CurrentScreen = null;
				CurrentScreenshot = null;

				// start capturing the mouse events
				BeginColorSelection();
			}
		}
		#endregion

		#region Events for colorBlock
		/// <summary>
		/// An event that is raised when the user clicks on one of the colors in the palette
		/// </summary>
		private void colorBlock_Click(object sender, EventArgs e)
		{
			var panel = sender as Panel;

			// update the selected color to this color palette selection
			if (panel != null)
				UpdateColor(panel.BackColor);
		}

		/// <summary>
		/// An event that is raised when the user starts hovering over a color panel
		/// </summary>
		private void colorBlock_MouseEnter(object sender, EventArgs e)
		{
			var panel = sender as Panel;
			if (panel == null)
				return;

			var outerBorder = panel.ClientRectangle;
			var outerBorderSize = 1;

			var innerBorder = new Rectangle(outerBorder.Location, outerBorder.Size);
			var innerBorderSize = 1;

			innerBorder.Inflate(-outerBorderSize, -outerBorderSize);

			using (var graphics = panel.CreateGraphics())
			{
				ControlPaint.DrawBorder(graphics, innerBorder,
					Color.FromArgb(255, 0, 125, 197), innerBorderSize, ButtonBorderStyle.Solid,
					Color.FromArgb(255, 0, 125, 197), innerBorderSize, ButtonBorderStyle.Solid,
					Color.FromArgb(255, 0, 125, 197), innerBorderSize, ButtonBorderStyle.Solid,
					Color.FromArgb(255, 0, 125, 197), innerBorderSize, ButtonBorderStyle.Solid);
			}
		}

		/// <summary>
		/// An event that is raised when the user stops hovering over a color panel
		/// </summary>
		private void colorBlock_MouseLeave(object sender, EventArgs e)
		{
			var panel = sender as Panel;
			// force the panel to redraw so it loses the border
			if (panel != null)
				panel.Invalidate();
		}
		#endregion

		#region Events for uxColor
		/// <summary>
		/// An event that is raised when the color box is painted
		/// </summary>
		private void uxColor_Paint(object sender, PaintEventArgs e)
		{
			var outerBorder = uxColor.ClientRectangle;
			const int outerBorderSize = 1;

			var innerBorder = new Rectangle(outerBorder.Location, outerBorder.Size);
			const int innerBorderSize = 3;

			innerBorder.Inflate(-outerBorderSize, -outerBorderSize);

			ControlPaint.DrawBorder(e.Graphics, outerBorder,
				uxColor.BackColor, outerBorderSize, ButtonBorderStyle.Solid,
				uxColor.BackColor, outerBorderSize, ButtonBorderStyle.Solid,
				uxColor.BackColor, outerBorderSize, ButtonBorderStyle.Solid,
				uxColor.BackColor, outerBorderSize, ButtonBorderStyle.Solid);

			ControlPaint.DrawBorder(e.Graphics, innerBorder,
				BackColor, innerBorderSize, ButtonBorderStyle.Solid,
				BackColor, innerBorderSize, ButtonBorderStyle.Solid,
				BackColor, innerBorderSize, ButtonBorderStyle.Solid,
				BackColor, innerBorderSize, ButtonBorderStyle.Solid);
		}
		#endregion

		#region Events for uxCopy links
		/// <summary>
		/// An event that is raised when the copy hex link is clicked
		/// </summary>
		private void uxCopyHEX_Click(object sender, EventArgs e)
		{
			Clipboard.SetText(uxRgbHex.Text);
			uxCopyHEX.Text = "copied!";

			ResetCopyLabelAsync((Label)sender, DEFAULT_LABEL_RESET_TIME);
		}

		/// <summary>
		/// An event that is raised when the copy rgb link is clicked
		/// </summary>
		private void uxCopyRGB_Click(object sender, EventArgs e)
		{
			Clipboard.SetText(uxRgb.Text);
			uxCopyRGB.Text = "copied!";

			ResetCopyLabelAsync((Label)sender, DEFAULT_LABEL_RESET_TIME);
		}

		/// <summary>
		/// An event that is raised when the copy hsl link is clicked
		/// </summary>
		private void uxCopyHSL_Click(object sender, EventArgs e)
		{
			Clipboard.SetText(uxHsl.Text);
			uxCopyHSL.Text = "copied!";

			ResetCopyLabelAsync((Label)sender, DEFAULT_LABEL_RESET_TIME);
		}
		#endregion

		#region Helper Functions
		/// <summary>
		/// Starts capturing the mouse events
		/// </summary>
		private void BeginColorSelection()
		{
			// prevent multiple color selections
			uxStart.Enabled = false;

			// subscribe to the mouse hooks
			HookManager.MouseMove += HookManager_MouseMove;
			HookManager.MouseClick += HookManager_MouseClick;
		}

		/// <summary>
		/// Stops capturing the mouse events
		/// </summary>
		private void EndColorSelection()
		{
			// unsubscribe
			HookManager.MouseMove -= HookManager_MouseMove;
			HookManager.MouseClick -= HookManager_MouseClick;

			// add the selected color to the palette
			AddColorToPalette(uxColor.BackColor);

			// allow the user to start another color selection
			uxStart.Enabled = true;
		}

		/// <summary>
		/// Adds a new color to the color palette
		/// </summary>
		private void AddColorToPalette(Color color)
		{
			var paletteSize = 16;
			var paddingSize = 5;
			var numColors = uxColorPalette.Controls.Count;
			var offsetX = 0;
			var offsetY = 0;

			// create a new color block
			var colorBlock = new Panel { Height = paletteSize, Width = paletteSize, BackColor = color, Cursor = Cursors.Hand };

			// wire up the click event to change the selected color
			colorBlock.Click += colorBlock_Click;

			// wire up the mouse enter/leave events to give the panel a hover-border
			colorBlock.MouseEnter += colorBlock_MouseEnter;
			colorBlock.MouseLeave += colorBlock_MouseLeave;

			// figure out where to put it
			if (numColors > 0)
			{
				var lastControl = uxColorPalette.Controls[numColors - 1];

				offsetX = lastControl.Right + paddingSize;
				offsetY = lastControl.Top;

				if (numColors % 7 == 0)
				{
					// go to the new row
					offsetX = 0;
					offsetY = lastControl.Bottom + paddingSize;
				}
			}

			// adjust the coordinates before we add it
			colorBlock.Left = offsetX;
			colorBlock.Top = offsetY;

			// add it to the list
			uxColorPalette.Controls.Add(colorBlock);
		}

		/// <summary>
		/// Grab a screenshot of the specific monitor
		/// </summary>
		private FastAccessBitmap TakeScreenshot(Screen currentScreen)
		{
			var left = currentScreen.Bounds.X;
			var top = currentScreen.Bounds.Y;
			var width = currentScreen.Bounds.Width;
			var height = currentScreen.Bounds.Height;
			var size = currentScreen.Bounds.Size;
			var screenshot = new Bitmap(width, height, PixelFormat.Format24bppRgb);

			using (var graphics = Graphics.FromImage(screenshot))
			{
				var source = new Point(left, top);
				var destination = new Point(0, 0);

				graphics.CopyFromScreen(source, destination, size);
			}

			return new FastAccessBitmap(screenshot, false);
		}

		/// <summary>
		/// Gets the color from the screenshot at the specified coordinates
		/// </summary>
		private Color GetScreenshotColorAt(int x, int y)
		{
			// get the color at the current cursor position from the screenshot
			return CurrentScreenshot.GetPixelSafe(x, y);
		}

		/// <summary>
		/// Updates the color block with the selected color
		/// </summary>
		private void UpdateColor(Color color)
		{
			// update the color block
			uxColor.BackColor = color;

			// update the RGB hex value
			uxRgbHex.Text = string.Format("#{0:x2}{1:x2}{2:x2}", color.R, color.G, color.B);
			uxRgb.Text = string.Format("rgb({0:N0}, {1:N0}, {2:N0})", color.R, color.G, color.B);
			uxHsl.Text = string.Format("hsl({0:N0}, {1:N0}%, {2:N0}%)", color.GetHue(), color.GetSaturation() * 100, color.GetBrightness() * 100);
		}

		/// <summary>
		/// An async function to delay the resetting of the copy link text
		/// </summary>
		private async void ResetCopyLabelAsync(Label label, int resetTimeInMilliSeconds)
		{
			if (label == null)
				throw new ArgumentNullException("label");

			// wait 2 seconds before setting it back
			await Task.Delay(resetTimeInMilliSeconds);
			label.Text = "copy";
		}

		private void helpLabel_MouseUp(object sender, MouseEventArgs e)
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("To start click on the get a color button. You can move the mouse around the screen and click on a color of your choosing.");
			sb.AppendLine();
			sb.AppendLine("Precision controls: ");
			sb.AppendLine("W - move up");
			sb.AppendLine("S - move down");
			sb.AppendLine("A - move left");
			sb.AppendLine("D - move right");
			sb.AppendLine("Space bar - adds current color to color palette");
			sb.AppendLine("Z - hotkey for the  \"Get a color\" button");
			MessageBox.Show(sb.ToString(), "Help menu");
		}
		#endregion

		#region Properties
		private const int WM_NCHITTEST = 0x84;
		private const int HTCLIENT = 0x1;
		private const int HTCAPTION = 0x2;
		private const int DEFAULT_LABEL_RESET_TIME = 2000;

		private Pen PenBorderActive { get; set; }
		private Pen PenBorderInactive { get; set; }
		private Pen PenBorderCurrent { get; set; }
		private Pen PenCrosshair { get; set; }
		private Pen PenGrid { get; set; }

		private Screen CurrentScreen { get; set; }
		private FastAccessBitmap CurrentScreenshot { get; set; }
		private int ScreenOffsetX { get; set; }
		private int ScreenOffsetY { get; set; }
		private int CurrentX { get; set; }
		private int CurrentY { get; set; }
		private int HalfWidth { get; set; }
		private int HalfHeight { get; set; }
		private int ZoomLevel { get; set; }
		private int ZoomMidPoint { get; set; }
		private int ViewportWidth { get; set; }
		private int ViewportHeight { get; set; }
		private int ZoomWidth { get; set; }
		private int ZoomHeight { get; set; }

		private bool StaleScreenShot = true;
		#endregion
	}
}