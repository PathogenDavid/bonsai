using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Bonsai;
using Bonsai.Design;
using System.Diagnostics;
using System.Drawing;
using System.Reactive;
using System.Text;

[assembly: TypeVisualizer(typeof(ObjectTextVisualizer), Target = typeof(object))]

namespace Bonsai.Design
{
    /// <summary>
    /// Provides a type visualizer for displaying any object type as text.
    /// </summary>
    public class ObjectTextVisualizer : BufferedVisualizer
    {
        const int AutoScaleHeight = 13;
        const float DefaultDpi = 96f;

        RichTextBox textBox;
        UserControl textPanel;
        Queue<string> buffer;
        int bufferSize;

        /// <inheritdoc/>
        protected override int TargetInterval => 1000 / 30;

        /// <inheritdoc/>
        protected override void ShowBuffer(IList<Timestamped<object>> values)
        {
            if (values.Count == 0)
                return;

            var sb = new StringBuilder();

            // Add new values to the buffer (and only the ones which might appear)
            for (int i = Math.Max(0, values.Count - bufferSize); i < values.Count; i++)
            {
                var rawText = values[i].Value?.ToString() ?? string.Empty;
                sb.Clear();
                sb.EnsureCapacity(rawText.Length + rawText.Length / 16); // Arbitrary extra space for control characters
                MakeDisplayString(rawText, sb);
                DisplayString(sb.ToString());
            }

            // Update the visual representation of the buffer
            Debug.Assert(buffer.Count <= bufferSize);
            sb.Clear();
            foreach (var line in buffer)
            {
                if (sb.Length > 0)
                    sb.Append(Environment.NewLine);
                sb.Append(line);
            }
            textBox.Text = sb.ToString();
            textPanel.Invalidate();
        }

        /// <inheritdoc/>
        public override void Show(object value)
        {
            var rawText = value?.ToString() ?? string.Empty;
            var stringBuilder = new StringBuilder(rawText.Length);
            MakeDisplayString(rawText, stringBuilder);
            DisplayString(stringBuilder.ToString());
        }

        /// <summary>Converts the specified string for display in this visualizer</summary>
        /// <param name="rawText">The raw text to be converted</param>
        /// <param name="stringBuilder">The string builder which receives the display string</param>
        protected virtual void MakeDisplayString(string rawText, StringBuilder stringBuilder)
        {
            foreach (char c in rawText)
            {
                switch (c)
                {
                    // Carriage returns are presumed to be followed by line feeds, skip them entirely
                    case '\r':
                        continue;
                    // Newlines become space so that things like multi-line JSON or matricies are still visible
                    case '\n':
                    case '\x0085': // Next line character (NEL)
                    case '\x2028': // Unicode line separator
                    case '\x2029': // Unicode paragraph separator
                        stringBuilder.Append(' ');
                        break;
                    case '\t':
                        stringBuilder.Append(c);
                        break;
                    // Replace all other control characters with the unknown replacement character
                    case < ' ': // C0 control characters
                    case '\x007F': // Delete
                    case >= '\x0080' and <= '\x009F': // C1 control characters
                        stringBuilder.Append('\xFFFD');
                        break;
                    default:
                        stringBuilder.Append(c);
                        break;
                }
            }
        }

        private void DisplayString(string value)
        {
            // Loop instead of simple if because the buffer size may have decreased
            while (buffer.Count >= bufferSize)
                buffer.Dequeue();

            buffer.Enqueue(value);
        }

        /// <inheritdoc/>
        public override void Load(IServiceProvider provider)
        {
            buffer = new Queue<string>();
            textBox = new RichTextLabel { Dock = DockStyle.Fill };
            textBox.Multiline = true;
            textBox.WordWrap = false;
            textBox.ScrollBars = RichTextBoxScrollBars.Horizontal;
            textBox.MouseDoubleClick += (sender, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    buffer.Clear();
                    textBox.Text = string.Empty;
                    textPanel.Invalidate();
                }
            };

            textPanel = new UserControl();
            textPanel.SuspendLayout();
            textPanel.Dock = DockStyle.Fill;
            textPanel.MinimumSize = textPanel.Size = new Size(320, 2 * AutoScaleHeight);
            textPanel.AutoScaleDimensions = new SizeF(6F, AutoScaleHeight);
            textPanel.AutoScaleMode = AutoScaleMode.Font;
            textPanel.Paint += textPanel_Paint;
            textPanel.Controls.Add(textBox);
            textPanel.ResumeLayout(false);

            var visualizerService = (IDialogTypeVisualizerService)provider.GetService(typeof(IDialogTypeVisualizerService));
            if (visualizerService != null)
            {
                visualizerService.AddControl(textPanel);
            }
        }

        void textPanel_Paint(object sender, PaintEventArgs e)
        {
            var lineHeight = AutoScaleHeight * e.Graphics.DpiY / DefaultDpi;
            bufferSize = (int)((textBox.ClientSize.Height - 2) / lineHeight);
            var textSize = TextRenderer.MeasureText(textBox.Text, textBox.Font);
            if (textBox.ClientSize.Width < textSize.Width)
            {
                var offset = 2 * lineHeight + SystemInformation.HorizontalScrollBarHeight - textPanel.Height;
                if (offset > 0)
                {
                    textPanel.Parent.Height += (int)offset;
                }
            }
        }

        /// <inheritdoc/>
        public override void Unload()
        {
            bufferSize = 0;
            textBox.Dispose();
            textPanel.Dispose();
            textBox = null;
            textPanel = null;
            buffer = null;
        }
    }
}
