using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Media;
using System.IO;
using System.Diagnostics;
using System.Web;
using System.Drawing;
using System.Windows.Forms;
using System.Timers;
using System.Linq;

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

            Console.WriteLine(getExePath());


            //HotKeyManager.RegisterHotKey(Keys.F12, KeyModifiers.Control);
            //HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(StartStopRec);


            //HotKeyManager.RegisterHotKey(Keys.F8, KeyModifiers.Control);
            //HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(MakeSnap);

            Application.ApplicationExit += new EventHandler(OnApplicationExit);


            remotePath = "test\\";

            startReplay();

            createTCP();



        }

        static void OnApplicationExit(object sender, EventArgs e)
        {
            stopCompare();
        }

        static string logFile = "vmtest.log";
        static void log(string str)
        {
            File.AppendAllText(logFile, str + "\n");
        }

        static string remotePath = null;
        static Process compareProcess = null;
        static Process replayProcess = null;
        static IEnumerable<string> remoteFiles = null;
        static IEnumerable<string> localFiles = null;
        static IEnumerable<string> compareFiles = null;
        static StringBuilder lastCompareOutput = null;
        static System.Timers.Timer compareTimer = null;

        static void startReplay()
        {
            if (Directory.Exists("data"))
            {
                Directory.Delete("data", true);
                // return;
            }
            if (Directory.Exists("compared"))
            {
                Directory.Delete("compared", true);
                // return;
            }
            Directory.CreateDirectory("data");
            Directory.CreateDirectory("compared");

            // copy all coords files into data
            string sourcePath = remotePath + "data\\";
            string targetPath = "data\\";

            foreach (var sourceFilePath in Directory.GetFiles(sourcePath, "*.txt"))
            {
                string fileName = Path.GetFileName(sourceFilePath);
                string destinationFilePath = Path.Combine(targetPath, fileName);

                System.IO.File.Copy(sourceFilePath, destinationFilePath, true);
            }

            startCompare();

            replayProcess = runExe("log.key", "ppp", (s, e) =>
            {
                string exitStr = "replay exit code: " + replayProcess.ExitCode.ToString();
                replayProcess = null;
                stopCompare();

                MessageBox.Show(exitStr);

            }, null);

        }

        static void compareFolder()
        {
            if (remotePath == null) return;

            remoteFiles = new System.IO.DirectoryInfo(remotePath + "data\\").GetFiles("*.png").Select(fi => fi.Name);
            localFiles = new System.IO.DirectoryInfo("data\\").GetFiles("*.png").Select(fi => fi.Name);
            compareFiles = new System.IO.DirectoryInfo("compared\\").GetFiles("*.png").Select(fi => fi.Name);

            IEnumerable<string> list3 = localFiles.Except(compareFiles).OrderBy(v => v);

            //Console.WriteLine("The following files are in data\\ but not compare\\:");

            foreach (var v in list3)
            {
                Console.WriteLine(v);
                log("compare " + v);
                compareImages(v);
                break;
            }

        }


        static void compareImages(string filename)
        {
            Directory.CreateDirectory("compared");
            lastCompareOutput = new StringBuilder();
            string args = "-metric MAE \"" + remotePath + "data\\" + filename + "\" \"data\\" + filename + "\" \"compared\\" + filename + "\"";

            Console.WriteLine(args);
            log(args);

            compareProcess = runExe(args, "compare.exe", (sender2, e2) =>
            {
                log(lastCompareOutput.ToString());
                if (!lastCompareOutput.ToString().Contains("0 (0)"))
                {
                    Console.WriteLine(lastCompareOutput.ToString());
                    log(lastCompareOutput.ToString());

                    stopCompare();
                }
                compareProcess = null;
            }, lastCompareOutput);
        }


        static void stopCompare()
        {

            if (compareTimer != null)
            {
                compareTimer.Stop();
                compareTimer.Enabled = false;
                compareTimer = null;
            }
            if (replayProcess != null)
            {
                replayProcess.Kill();
            }
        }
        static void startCompare()
        {
            compareTimer = new System.Timers.Timer();
            compareTimer.Interval = 1000;
            compareTimer.Elapsed += TimerEventProcessor;
            compareTimer.Start();
        }

        static void TimerEventProcessor(object sender, ElapsedEventArgs e)
        {
            if (compareProcess == null) compareFolder();
            else Console.WriteLine("already in compare!!");
        }

        static bool isRecording = false;

        static void StartStopRec(object sender, HotKeyEventArgs e)
        {
            Console.WriteLine(isRecording);

            isRecording = !isRecording;

            runExe("log.key \"^0x7B\"", "fff.exe", (object sender2, System.EventArgs e2) =>
            {
                Console.WriteLine("finished");
            }, null);

        }


        static string shot()
        {
            Rectangle bounds = Screen.GetBounds(Point.Empty);
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
            }


            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            byte[] byteImage = ms.ToArray();

            // consume: "data:image/png;base64," + SigBase64
            var SigBase64 = Convert.ToBase64String(byteImage); //Get Base64

            // textBox1.Text = SigBase64;

            // Write the bytes (as a string) to the textbox
            // Console.Write(SigBase64.Length);

            return SigBase64;

        }

        static void createTCP()
        {
            TcpListener server = null;
            try
            {
                // Set the TcpListener port.
                Int32 port = 22300;
                IPAddress localAddr = IPAddress.Parse("127.0.0.1");

                // TcpListener server = new TcpListener(port);
                // server = new TcpListener(localAddr, port);
                server = new TcpListener(IPAddress.Any, port);

                // Start listening for client requests.
                server.Start();

                // Buffer for reading data
                Byte[] bytes = new Byte[2560];
                String data = null;
                bool isExit = false;

                // Enter the listening loop.
                while (!isExit)
                {
                    Console.Write("Waiting for a connection... ");

                    // Perform a blocking call to accept requests.
                    // You could also user server.AcceptSocket() here.
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("Connected!");

                    data = "";

                    // Get a stream object for reading and writing
                    NetworkStream stream = client.GetStream();

                    int i;

                    // Loop to receive all the data sent by the client.
                    if ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        // Translate data bytes to a ASCII string.
                        data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                        var url = data.Split(' ')[1];
                        //url = HttpUtility.UrlDecode(url).Substring(1);
                        var urlObj = new Uri("http://localhost" + url);
                        var query = HttpUtility.ParseQueryString(urlObj.Query);
                        Console.WriteLine("Received: {0}---{1}", url, urlObj.AbsolutePath);

                        var ret = "";

                        switch (urlObj.AbsolutePath)
                        {
                            case "/":

                                ret = "<form method=\"GET\"><input type=checkbox name=debug value=1><input type=text name=cmd></form>";

                                if (query["debug"] == "1")
                                {

                                    ret += "<pre>" + query["cmd"] + "</pre>";
                                }
                                else if (!string.IsNullOrEmpty(query["cmd"])) exeCmd(query["cmd"]);

                                if (query["exit"] == "1") isExit = true;

                                // get snapshot of client
                                if (query["snap"] == "1")
                                {
                                    exeCmd("savescreenshot \"~$sys.temp$\\vmtest_snap.png\"");
                                }


                                if (query["snap"] == "2")
                                {

                                    ret = "<style>*{margin:0;padding:0;}</style><img src=\"data:image/png;base64," + shot() + "\">";
                                }


                                // get process of client
                                if (query["proc"] == "1")
                                {
                                    Process[] processlist = Process.GetProcesses();
                                    ret += "<style>td,th{font-size:12px;padding:0;text-align:left;}</style><b>Process:</b><table cellborder=0><tr><th>NAME</th><th>CPU</th><th>MEM</th><th>PID</th><th>FILE</th></tr>";
                                    foreach (Process theprocess in processlist)
                                    {
                                        var proc_filename = "";
                                        var cpu_time = "";
                                        try
                                        {
                                            proc_filename = theprocess.MainModule.FileName;
                                            cpu_time = new DateTime(theprocess.TotalProcessorTime.Ticks).ToString("HH:mm:ss");
                                        }
                                        catch (Exception)
                                        {

                                        }

                                        ret += String.Format("<tr><td>{0}</td><td>{1}&nbsp;&nbsp;</td><td>{2:n0} KB</td><td>{3}&nbsp;&nbsp;</td><td>{4}</td></tr>", theprocess.ProcessName, cpu_time, theprocess.VirtualMemorySize64 / 1024, theprocess.Id, proc_filename);
                                    }
                                    ret += "</table>";
                                }

                                if (query["update"] == "1")
                                {
                                    var target = getExePath();
                                    var source = query["source"];

                                    source = !string.IsNullOrEmpty(source) && Path.GetExtension(source).ToLower() == ".exe" && File.Exists(source) ? source : "M:\\vmtest.exe";

                                    if (File.Exists(source))
                                    {
                                        Console.WriteLine("update from {0} to {1}", source, target);
                                        exeCmd(String.Format("cmdwait 5000 execmd copy /y \"{0}\" \"{1}\"", source, target));
                                        exeCmd(String.Format("cmdwait 10000 exec hide \"{0}\"", target));
                                        isExit = true;
                                    }
                                }

                                if (!string.IsNullOrEmpty(query["version"]))
                                {
                                    ret += "<p>" + getVersion() + "</p>" + File.GetCreationTime(getExePath());
                                }

                                break;

                            case "/rec":



                                break;
                            case "/play":


                                break;


                        }
                        // Process the data sent by the client.
                        //data = data.ToUpper();
                        data = "HTTP/1.1 200 OK\r\nContent-Type:text/html; charset=UTF-8\r\nX-App: vmtest\r\nContent-Length: " + ret.Length + "\r\n\r\n" + ret;

                        byte[] msg = System.Text.Encoding.ASCII.GetBytes(data);

                        // Send back a response.
                        stream.Write(msg, 0, msg.Length);
                        //Console.WriteLine("Sent: {0}", data);


                    }

                    // Shutdown and end connection
                    client.Close();


                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
            }


            //Console.WriteLine("\nHit enter to continue...");
            //Console.Read();
        }


        static string getExePath()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            return assembly.Location;
        }
        static string getVersion()
        {
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(getExePath());
            string version = fvi.FileVersion;
            return version;
        }

        static void exeCmd(string arg)
        {

            runExe(arg, "nircmd.exe", (object sender, System.EventArgs e) =>
            {
                Console.WriteLine("finished");
            }, null);

        }


        static Process runExe(string arg, string exePath, Action<object, EventArgs> onExit, StringBuilder outputBuilder)
        {
            string path = Directory.GetCurrentDirectory();
            DirectoryInfo d = new DirectoryInfo(path);

            Process myProcess = new Process();

            ProcessStartInfo startInfo = new ProcessStartInfo();

            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = true;
            startInfo.FileName = exePath;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            // startInfo.WorkingDirectory = d.FullName;
            startInfo.Arguments = arg;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.CreateNoWindow = true;

            if (outputBuilder != null)
            {
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardOutput = true;
                myProcess.OutputDataReceived += (sender, eventArgs) => outputBuilder.AppendLine(eventArgs.Data);
                myProcess.ErrorDataReceived += (sender, eventArgs) => outputBuilder.AppendLine(eventArgs.Data);
            }


            if (onExit != null)
            {
                myProcess.EnableRaisingEvents = true;
                myProcess.Exited += new EventHandler(onExit);
            }

            myProcess.StartInfo = startInfo;
            myProcess.Start();

            if (outputBuilder != null)
            {
                myProcess.BeginOutputReadLine();
                myProcess.BeginErrorReadLine();
            }

            return myProcess;
        }

    }
}
