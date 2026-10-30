using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace PalworldServerEditor
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;

            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                Theme.Init(g.DpiX);

            Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
        }

        static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show("Si è verificato un errore imprevisto:\n\n" + e.Exception.Message,
                "Palworld Server Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
