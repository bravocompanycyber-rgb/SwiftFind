using System;
using System.Drawing;
using System.Windows.Forms;
using SwiftFind.Core;

namespace SwiftFind.Controls
{
    /// <summary>Flat button with hover/pressed feedback and an optional "active" (toggle) state.</summary>
    public class FlatButton : Button
    {
        public enum Style { Primary, Orange, Ghost }

        Color normal, hover, pressed;
        bool isHover, isDown, active;

        public FlatButton(string text, Style style = Style.Ghost)
        {
            Text = text;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            AutoSize = true;                       // grows when text size is increased, never shrinks below the set width
            AutoSizeMode = AutoSizeMode.GrowOnly;
            UseMnemonic = false;   // so "Drives & scope" shows the ampersand instead of underlining a letter
            Cursor = Cursors.Hand;
            Font = Theme.Ui(9.5f, FontStyle.Bold);
            ForeColor = Color.White;
            Height = 36;
            Padding = new Padding(10, 0, 10, 0);
            SetStyle(style);
        }

        public void SetStyle(Style style)
        {
            switch (style)
            {
                case Style.Primary: normal = Theme.Accent; hover = Theme.AccentHover; pressed = Theme.AccentPressed; ForeColor = Color.White; break;
                case Style.Orange: normal = Theme.Orange; hover = Theme.OrangeHover; pressed = Color.FromArgb(214, 112, 20); ForeColor = Color.White; break;
                default: normal = Theme.Surface; hover = Theme.SurfaceHi; pressed = Theme.Border; ForeColor = Theme.Text; break;
            }
            Refresh2();
        }

        /// <summary>Highlights the button as "on" (used for the Favorites toggle).</summary>
        public bool Active
        {
            get { return active; }
            set { active = value; Refresh2(); }
        }

        void Refresh2()
        {
            Color c = isDown ? pressed : (isHover ? hover : normal);
            if (active && !isHover && !isDown) c = Theme.SurfaceHi;
            BackColor = c;
            FlatAppearance.MouseOverBackColor = hover;
            FlatAppearance.MouseDownBackColor = pressed;
            if (active) ForeColor = Theme.ColorFav;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { isHover = true; Refresh2(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHover = false; isDown = false; Refresh2(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { isDown = true; Refresh2(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { isDown = false; Refresh2(); base.OnMouseUp(e); }
    }
}
