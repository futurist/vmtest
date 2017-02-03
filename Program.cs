using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace vmtest
{

    class Program
    {
        // below line allow access to Clipboard
        [STAThread]
        static void Main(string[] args)
        {
            // Make only one instance
            // http://stackoverflow.com/questions/6486195/ensuring-only-one-application-instance
            bool result;
            var mutex = new System.Threading.Mutex(true, "vmtestapp", out result);
            if (!result)
            {
                return;
            }
            GC.KeepAlive(mutex);

            new Main();

            Application.Run(new ApplicationContext());

        }


    }
}
