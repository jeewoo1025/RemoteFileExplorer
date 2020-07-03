using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace HW2_Client
{
    public partial class 상세정보 : Form
    {
        public 상세정보(FileInfo fi)
        {
            InitializeComponent();

            txtFileName.Text = fi.Name;
            labelFileType.Text += fi.Extension;
            loadPictureBox(fi.Extension);

            labelFilePath.Text += fi.FullName;
            labelFileSize.Text += (fi.Length + " 바이트");

            labelCreateDate.Text += fi.CreationTime;
            labelModified.Text += fi.LastWriteTime;
            labelAccess.Text += fi.LastAccessTime;
        }

        private void loadPictureBox(string obj)
        {
            if (Regex.IsMatch(obj, ".txt", RegexOptions.IgnoreCase))
                pictureBox1.Image = Properties.Resources.text;
            else if (Regex.IsMatch(obj, ".avi", RegexOptions.IgnoreCase) || Regex.IsMatch(obj, ".mp4", RegexOptions.IgnoreCase))
                pictureBox1.Image = Properties.Resources.avi;
            else if (Regex.IsMatch(obj, ".jpg", RegexOptions.IgnoreCase) || Regex.IsMatch(obj, ".png", RegexOptions.IgnoreCase))
                pictureBox1.Image = Properties.Resources.image;
            else if (Regex.IsMatch(obj, ".mp3", RegexOptions.IgnoreCase) || Regex.IsMatch(obj, ".wav", RegexOptions.IgnoreCase))
                pictureBox1.Image = Properties.Resources.music;
            else
                pictureBox1.Image = Properties.Resources.temp;
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
