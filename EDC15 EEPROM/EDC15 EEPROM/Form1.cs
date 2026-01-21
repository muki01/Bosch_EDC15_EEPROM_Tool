using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EDC15_EEPROM
{
    public partial class Form1 : Form
    {
        byte[] originalData;   // Original File
        byte[] modifiedData;   // Modified Copy
        string openedFilePath;

        bool dragging = false;
        Point dragCursorPoint;
        Point dragFormPoint;

        private void roundPanel(Panel pnl, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.StartFigure();
            path.AddArc(0, 0, radius, radius, 180, 90);
            path.AddArc(pnl.Width - radius, 0, radius, radius, 270, 90);
            path.AddArc(pnl.Width - radius, pnl.Height - radius, radius, radius, 0, 90);
            path.AddArc(0, pnl.Height - radius, radius, radius, 90, 90);
            path.CloseFigure();
            pnl.Region = new Region(path);
        }

        // LOGIN CODE (TextBox1)
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (modifiedData == null || textBox1.Text.Length < 1) return;

            if (ushort.TryParse(textBox1.Text, out ushort newPin))
            {
                // Save in Little Endian format (e.g., 07832 -> Hex -> Byte Swap)
                byte b1 = (byte)(newPin & 0xFF);
                byte b2 = (byte)((newPin >> 8) & 0xFF);

                SetByte(0x012E, b1);
                SetByte(0x012F, b2);
                SetByte(0x0160, b1);
                SetByte(0x0161, b2);

                textBox1.ForeColor = Color.Cyan;
            }
        }

        // ODOMETER (TextBox2)
        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            if (modifiedData == null) return;

            // Just take the numbers (remove the KM text)
            string cleanValue = new string(textBox2.Text.Where(char.IsDigit).ToArray());

            if (uint.TryParse(cleanValue, out uint km))
            {
                uint rawVal = km * 100; // The equivalent in the file is (KM * 100)

                SetByte(0x01BF, (byte)(rawVal & 0xFF));
                SetByte(0x01C0, (byte)((rawVal >> 8) & 0xFF));
                SetByte(0x01C1, (byte)((rawVal >> 16) & 0xFF));
                SetByte(0x01C2, (byte)((rawVal >> 24) & 0xFF));

                textBox2.ForeColor = Color.Cyan;
            }
        }

        private void SetByte(int offset, byte value)
        {
            if (modifiedData != null && offset >= 0 && offset < modifiedData.Length)
            {
                modifiedData[offset] = value;
            }
        }

        private void GetImmoStatus()
        {
            if (modifiedData == null) return;

            rjToggleButton1.CheckedChanged -= rjToggleButton1_CheckedChanged;
            byte val1 = modifiedData[0x01B0];
            byte val2 = modifiedData[0x01DE];

            if (val1 == 0x73 && val2 == 0x73)
            {
                label15.Text = "IMMO ON";
                rjToggleButton1.OnBackColor = Color.FromArgb(46, 204, 113); // Green
                rjToggleButton1.OnToggleColor = Color.FromArgb(224, 224, 224); // Green
                if (rjToggleButton1.Checked != true) rjToggleButton1.Checked = true;
            }
            else if (val1 == 0x60 && val2 == 0x60)
            {
                label15.Text = "IMMO OFF";
                rjToggleButton1.OffBackColor = Color.FromArgb(231, 76, 60); // Red
                rjToggleButton1.OffToggleColor = Color.FromArgb(224, 224, 224); // Red
                if (rjToggleButton1.Checked != false) rjToggleButton1.Checked = false;
            }
            rjToggleButton1.CheckedChanged += rjToggleButton1_CheckedChanged;
        }

        private void GetKilometer()
        {
            if (modifiedData == null || modifiedData.Length <= 0x01C2)
            {
                label13.Text = "Unknown";
                textBox2.Text = "Unknown";
                return;
            }

            uint kmValue = (uint)(
                modifiedData[0x01BF] |
                (modifiedData[0x01C0] << 8) |
                (modifiedData[0x01C1] << 16) |
                (modifiedData[0x01C2] << 24)
            );

            double km = kmValue / 100.0;

            textBox2.Text = km.ToString("N0") + " KM";
            label13.Text = km.ToString("N0") + " KM";
        }

        private void GetPinCode()
        {
            if (modifiedData == null || modifiedData.Length <= 0x012F)
            {
                label10.Text = "Unknown";
                textBox1.Text = "Unknown";
                return;
            }

            byte b1 = modifiedData[0x012E];
            byte b2 = modifiedData[0x012F];
            ushort pinHex = (ushort)((b2 << 8) | b1); // Inverse (byte swap) 
            int pinDecimal = pinHex; // Decimal PIN

            // 5-digit format (adds zeros at the beginning)
            textBox1.Text = pinDecimal.ToString("D5");
            label10.Text = pinDecimal.ToString("D5");
        }

        public Form1()
        {

            InitializeComponent();
            roundPanel(panel2, 30);
            roundPanel(panel3, 30);
            roundPanel(panel4, 30);

            this.panel1.MouseDown += panel1_MouseDown;
            this.panel1.MouseMove += panel1_MouseMove;
            this.panel1.MouseUp += panel1_MouseUp;
        }

        private void btnAbout_Click(object sender, EventArgs e)
        {
            string aboutText =
    "Program Name: EDC15 24C04 EEPROM Tool\n\n" +
    "Developed by: Muki\n" +
    "Email: muksin.muksin04@gmail.com\n" +
    "GitHub: https://github.com/muki01\n\n" +
    "Description:\n" +
    "This tool is specifically designed for Bosch EDC15 ECUs \n" +
    "equipped with 24C04 EEPROM (512 bytes).\n\n" +
    "Functions:\n" +
    "* IMMO ON/OFF Patcher\n" +
    "* Odometer (KM) Calculation & Adjustment\n" +
    "* PIN Code Extraction\n\n" +
    "Warning: Only use with 512-byte original EEPROM dumps.";
            MessageBox.Show(
                aboutText,
                "About",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void btnOpenFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Binary Files (*.bin)|*.bin|All Files (*.*)|*.*";
                ofd.Title = "Select the EDC15 EEPROM file.";
                ofd.Multiselect = false;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    // 1. File Size Control (For Security Purposes)
                    FileInfo fileInfo = new FileInfo(ofd.FileName);
                    if (fileInfo.Length != 512)
                    {
                        MessageBox.Show($"Error: Selected file is {fileInfo.Length} bytes.\nEDC15 EEPROM files must be exactly 512 bytes!",
                 "Invalid File Size",
                 MessageBoxButtons.OK,
                 MessageBoxIcon.Error);
                        return;
                    }

                    try
                    {
                        openedFilePath = ofd.FileName;
                        originalData = File.ReadAllBytes(openedFilePath);

                        modifiedData = (byte[])originalData.Clone(); // Create Clone

                        label3.Text = Path.GetFileName(openedFilePath); // Update File Name
                        label4.Text = openedFilePath;                   // Update File Path

                        rjButton4.Enabled = true;       // Enable Save button
                        textBox1.Enabled = true;        // Enable Login box
                        textBox2.Enabled = true;        // Enable Odometer box
                        rjToggleButton1.Enabled = true;  // Enable IMMO button

                        UpdateImmoStatus();
                        UpdateKilometer();
                        UpdatePinCode();

                        byte val1 = originalData[0x01B0];
                        byte val2 = originalData[0x01DE];
                        if (val1 == 0x73 && val2 == 0x73) label11.Text = "ON";
                        else if (val1 == 0x60 && val2 == 0x60) label11.Text = "OFF";
                        else label11.Text = "Mixed";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("An error occurred while reading the file: " + ex.Message);
                    }
                }
            }
        }

        private void btnExitApp_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnMinimizeApp_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void btnSaveFile_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Binary Files (*.bin)|*.bin";
            sfd.Title = "Save the modified file.";

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllBytes(sfd.FileName, modifiedData);
            }
        }

        private void tglImmoSwitch_CheckedChanged(object sender, EventArgs e)
        {
            if (modifiedData == null) return;

            byte currentVal = modifiedData[0x01B0];

            if (currentVal == 0x73)
            {
                SetByte(0x01B0, 0x60);
                SetByte(0x01DE, 0x60);
            }
            else
            {
                SetByte(0x01B0, 0x73);
                SetByte(0x01DE, 0x73);
            }

            UpdateImmoStatus();
        }

        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            dragging = true;
            dragCursorPoint = Cursor.Position;
            dragFormPoint = this.Location;
        }

        private void panel1_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(dif));
            }
        }

        private void panel1_MouseUp(object sender, MouseEventArgs e) { dragging = false; }

        private void btnDonate_Click(object sender, EventArgs e)
        {
            string url = "https://www.paypal.com/donate/?hosted_button_id=SAAH5GHAH6T72";

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("The link could not be opened: " + ex.Message);
            }
        }

        private void picGithubLink_Click(object sender, EventArgs e)
        {
            string githubUrl = "https://github.com/muki01";

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = githubUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("The GitHub page could not be opened: " + ex.Message);
            }
        }
    }
}
