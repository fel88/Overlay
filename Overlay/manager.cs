using System;
using System.Windows.Forms;

namespace Overlay
{
    public partial class manager : Form
    {
        public manager()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            overlay.FillRects = checkBox1.Checked;
        }

        private void button2_Click(object sender, EventArgs e)
        {            
            overlay.Form.InitNnet(textBox1.Text);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() != DialogResult.OK) return;
            textBox1.Text = ofd.FileName;                
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            overlay.UseHistory = checkBox2.Checked;
        }
    }
}
