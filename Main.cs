using System;
using System.Collections.Generic;
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
using System.Text;
using System.Threading;



namespace vmtest
{
    class Main
    {

        // Buffer for reading data
        bool isExit = false;
        WebServer ws = null;

        public Main()
        {
            Application.UseWaitCursor = false;

            Application.ApplicationExit += new EventHandler(OnApplicationExit);


            if (!File.Exists("convert.exe") || !File.Exists("compare.exe") || !File.Exists("ppp.exe"))
            {
                MessageBox.Show("make sure convert.exe, compare.exe, ppp.exe all exists!");
                return;
            }


            var httpserver = "mongoose";

            if (File.Exists(httpserver + ".exe") && Process.GetProcessesByName(httpserver).Length == 0)
            {
                runExe("-listening_port " + (port + 100).ToString() + " -start_browser no", httpserver + ".exe", null, null);
            }

            //HotKeyManager.RegisterHotKey(Keys.F12, KeyModifiers.Control);
            //HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(StartStopRec);


            //HotKeyManager.RegisterHotKey(Keys.F8, KeyModifiers.Control);
            //HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(MakeSnap);


            ws = new WebServer(handleClient, "http://*:" + port.ToString() + "/");
            ws.Run();
        }


        private static Random random = new Random();
        private static string RandomString(int Size)
        {
            string input = "abcdefghijklmnopqrstuvwxyz0123456789";
            var chars = Enumerable.Range(0, Size)
                                   .Select(x => input[random.Next(0, input.Length)]);
            return new string(chars.ToArray());
        }

        string PathAddBackslash(string path)
        {
            string separator1 = Path.DirectorySeparatorChar.ToString();
            string separator2 = Path.AltDirectorySeparatorChar.ToString();

            path = path.TrimEnd();

            if (path.EndsWith(separator1) || path.EndsWith(separator2))
                return path;

            if (path.Contains(separator2))
                return path + separator2;
            return path + separator1;
        }

        void OnApplicationExit(object sender, EventArgs e)
        {
            if (ws != null) ws.Stop();
            stopCompare(false);
        }

        string logFile = "vmtest.log";
        void log(string str)
        {
            if (DEBUG) File.AppendAllText(logFile, str + "\n");
        }

        bool DEBUG = false;
        Int32 port = 22300;
        string remotePath = null;
        string currentImage = null;
        Process compareProcess = null;
        Process replayProcess = null;
        IEnumerable<string> remoteFiles = null;
        IEnumerable<string> localFiles = null;
        IEnumerable<string> compareFiles = null;
        StringBuilder lastCompareOutput = null;
        System.Timers.Timer compareTimer = null;

        string replayExitCode = null;

        string getReplayStatus(string remotePath)
        {
            string ret = "";
            string images = "";

            if (!string.IsNullOrEmpty(remotePath))
            {
                remotePath = Path.Combine(remotePath, " ").TrimEnd();

                getFolderImageFiles(remotePath);

                if (compareFiles != null && compareFiles.Count() > 0)
                {
                    string current = compareFiles.Last();
                    string compareImage = Path.Combine("compared", current);
                    string testImage = Path.Combine(remotePath + "data", current);
                    string dataImage = Path.Combine("data", "_alpha_" + current);

                    images = String.Format(", \"images\": [\"{0}\", \"{1}\", \"{2}\"]",
                        testImage.Replace('\\', '/'),
                        dataImage.Replace('\\', '/'),
                        compareImage.Replace('\\', '/'));

                }
            }


            //ret += "running status:" + (replayProcess == null ? "0" : "1") + "<br>";

            //ret += "last result:" + (replayExitCode == null ? "NULL" : replayExitCode) + "<br>";

            ret += string.Format("{{\"running\": {0}, \"last_code\": {1} {2} }}"
                , (replayProcess == null ? "0" : "1")
                , (replayExitCode == null ? "null" : replayExitCode)
                , images);


            return ret;
        }

        string startReplay()
        {
            // copy all coords files into data
            string sourcePath = remotePath + "data\\";
            string targetPath = "data\\";



            if (!Directory.Exists(sourcePath))
            {
                return "{\"code\":1, \"message\": \"ERR_NO_TEST_DATA\"}";
            }
            if (Directory.Exists(targetPath))
            {
                if (File.Exists(Path.Combine(targetPath, "log.key")))
                {
                    return "{\"code\":2, \"message\": \"ERR_HAVE_LOG_KEY\"}";
                }
                Directory.Delete(targetPath, true);
                // return;
            }
            if (Directory.Exists("compared"))
            {
                Directory.Delete("compared", true);
                // return;
            }
            Directory.CreateDirectory(targetPath);
            Directory.CreateDirectory("compared");


            foreach (var sourceFilePath in Directory.GetFiles(sourcePath, "*.txt"))
            {
                string fileName = Path.GetFileName(sourceFilePath);
                if (fileName[0] == '_') continue;
                string destinationFilePath = Path.Combine(targetPath, fileName);

                System.IO.File.Copy(sourceFilePath, destinationFilePath, true);
            }

            startCompare();

            StringBuilder output = new StringBuilder();


            replayProcess = runExe(sourcePath + "log.key", "ppp", (s, e) =>
            {
                replayExitCode = replayProcess.ExitCode.ToString();
                string exitStr = "replay exit code: " + replayExitCode;
                replayProcess = null;
                stopCompare(false);

                //MessageBox.Show(output + exitStr);

            }, output);

            return null;

        }

        void getFolderImageFiles(string remotePath)
        {
            if (string.IsNullOrEmpty(remotePath)) return;
            try
            {
                remoteFiles = new System.IO.DirectoryInfo(remotePath + "data\\").GetFiles("*.png").Select(fi => fi.Name);
                localFiles = new System.IO.DirectoryInfo("data\\").GetFiles("*.png").Select(fi => fi.Name);
                compareFiles = new System.IO.DirectoryInfo("compared\\").GetFiles("*.png").Select(fi => fi.Name);
            }
            catch (Exception) { }
        }

        void compareFolder()
        {
            if (remotePath == null) return;

            getFolderImageFiles(remotePath);

            IEnumerable<string> list3 = localFiles.Except(compareFiles).OrderBy(v => v);

            //Console.WriteLine("The following files are in data\\ but not compare\\:");

            foreach (var v in list3)
            {
                Console.WriteLine(v);
                log("compare " + v);
                if (v[0] == '_') continue;
                compareImages(v);
                break;
            }

        }


        void compareImages(string filename)
        {
            currentImage = filename;
            Directory.CreateDirectory("compared");
            lastCompareOutput = new StringBuilder();

            var tempPath = Path.Combine("data", "_alpha_" + filename);
            // first ignore alpha area
            string args = String.Format("\"{0}\" \"{1}\" -compose copy-opacity -composite \"{2}\"",
                Path.Combine("data", filename),
                Path.Combine(remotePath + "data", filename),
                tempPath
                );

            Console.WriteLine(args);


            compareProcess = runExe(args, "convert.exe", (sender2, e2) =>
            {
                // convert ok, then compare
                compareImageStep2(filename, tempPath);
            }, lastCompareOutput);


        }

        void compareImageStep2(string filename, string tempPath)
        {
            lastCompareOutput = new StringBuilder();

            if (!File.Exists(tempPath))
            {
                stopCompare(false);
                MessageBox.Show(tempPath + " does not exists");
                return;
            }

            string args = String.Format("-metric MAE \"{0}\" \"{1}\" \"{2}\" ",
                Path.Combine(remotePath + "data", filename),
                tempPath,
                Path.Combine("compared", filename)
                );

            Console.WriteLine(args);
            log(args);

            compareProcess = runExe(args, "compare.exe", (sender2, e2) =>
            {

                string result = lastCompareOutput.ToString();

                if (!result.Contains("0 (0)"))
                {
                    Console.WriteLine(result);

                    stopCompare(false);

                    File.WriteAllText(Path.Combine("compared", filename.Substring(0, filename.Length - 4) + "_diff.txt"), lastCompareOutput.ToString());

                    //MessageBox.Show("test error: " + remotePath);

                }
                compareProcess = null;
            }, lastCompareOutput);
        }


        void stopCompare(bool force)
        {

            if (compareTimer != null)
            {
                compareTimer.Stop();
                compareTimer.Enabled = false;
                compareTimer = null;
            }
            if (replayProcess != null)
            {
                if (force)
                {
                    replayProcess.Kill();
                }
                else
                {
                    // run again to terminate
                    runExe("", "ppp", null, null);
                }

            }
        }
        void startCompare()
        {
            compareTimer = new System.Timers.Timer();
            compareTimer.Interval = 1000;
            compareTimer.Elapsed += TimerEventProcessor;
            compareTimer.Start();
        }

        void TimerEventProcessor(object sender, ElapsedEventArgs e)
        {
            if (compareProcess == null) compareFolder();
            else Console.WriteLine("already in compare!!");
        }

        bool isRecording = false;

        void StartStopRec(object sender, HotKeyEventArgs e)
        {
            Console.WriteLine(isRecording);

            isRecording = !isRecording;

            runExe("log.key \"^0x7B\"", "fff.exe", (object sender2, System.EventArgs e2) =>
            {
                Console.WriteLine("finished");
            }, null);

        }


        string shot()
        {

            Cursor curSor = Cursor.Current;

            Rectangle bounds = Screen.GetBounds(Point.Empty);
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);

                Rectangle cursorBounds = new Rectangle(Cursor.Position, curSor.Size);
                Cursors.Default.Draw(g, cursorBounds);
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


        string getExePath()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            return assembly.Location;
        }
        string getVersion()
        {
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(getExePath());
            string version = fvi.FileVersion;
            return version;
        }

        void exeCmd(string arg)
        {

            runExe(arg, "nircmd.exe", (object sender, System.EventArgs e) =>
            {
                Console.WriteLine("finished");
            }, null);

        }


        Process runExe(string arg, string exePath, Action<object, EventArgs> onExit, StringBuilder outputBuilder)
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

        string handleClient(HttpListenerContext ctx)
        {
            Console.WriteLine("Connected!");

            var req = ctx.Request;
            var res = ctx.Response;

            var ret = "";
            var contentType = "text/html";
            var query = req.QueryString;

            switch (req.Url.AbsolutePath)
            {
                case "/":

                    ret = "<form method=\"GET\"><input type=checkbox name=debug value=1><input type=text name=cmd></form>";

                    if (query["debug"] == "1")
                    {
                        ret += "<pre>" + query["cmd"] + "</pre>";
                    }
                    else if (!string.IsNullOrEmpty(query["cmd"])) exeCmd(query["cmd"]);

                    if (query["exit"] == "1")
                    {
                        isExit = true;
                    }
                    // get snapshot of client
                    if (query["snap"] == "1")
                    {
                        exeCmd("savescreenshot \"~$sys.temp$\\vmtest_snap.png\"");
                    }


                    if (query["snap"] == "2")
                    {

                        ret = "<style>body {margin:0;padding:0;}</style><img src=\"data:image/png;base64," + shot() + "\">";
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

                            ret += string.Format("<tr><td>{0}</td><td>{1}&nbsp;&nbsp;</td><td>{2:n0} KB</td><td>{3}&nbsp;&nbsp;</td><td>{4}</td></tr>", theprocess.ProcessName, cpu_time, theprocess.VirtualMemorySize64 / 1024, theprocess.Id, proc_filename);
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
                            exeCmd(string.Format("cmdwait 5000 execmd copy /y \"{0}\" \"{1}\"", source, target));
                            exeCmd(string.Format("cmdwait 10000 exec hide \"{0}\"", target));
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

                    contentType = "application/json";

                    if (!string.IsNullOrEmpty(query["test"]))
                    {
                        if (replayProcess == null)
                        {
                            remotePath = PathAddBackslash(query["test"]);
                            string startResult = startReplay();
                            if (null != startResult)
                            {
                                ret = startResult;
                            }
                            else
                            {
                                //ret += "test path: " + remotePath + "<br>";
                                //ret += getReplayStatus();

                                ret = "{\"code\":0, \"message\": \"OK\"}";
                            }
                        }
                        else
                        {
                            //ret = "test already in process: " + remotePath;

                            ret = "{\"code\":-1, \"message\": \"WARN_ALREADY_STARTED\"}";
                        }

                    }
                    else if (!string.IsNullOrEmpty(query["status"]))
                    {
                        ret = getReplayStatus(query["folder"]);
                    }
                    else if (!string.IsNullOrEmpty(query["stop"]))
                    {
                        stopCompare(true);

                        ret = "{}";
                    }


                    break;

                case "/dir":

                    contentType = "application/json";

                    string curDir = Directory.GetCurrentDirectory();
                    if (!string.IsNullOrEmpty(query["dir"]))
                    {
                        curDir = query["dir"];
                    }

                    var dirs = Directory.GetDirectories(curDir);

                    string[] excludeDirs = { }; //{ "compared", "data" };

                    ret += "[";
                    foreach (string name in dirs)
                    {
                        string folder = name.Split(Path.DirectorySeparatorChar).Last().ToString();
                        if (excludeDirs.Contains(folder)) continue;
                        ret += "\"" + folder + "\",";
                    }
                    ret = ret.TrimEnd(',');
                    ret += "]";
                    break;

            }
            // Process the data sent by the client.
            //data = data.ToUpper();

            string CORS = "Access-Control-Allow-Origin:*\r\nAccess-Control-Allow-Methods:*\r\n";

            string.Format("HTTP/1.1 200 OK\r\nContent-Type:{0}; charset=UTF-8\r\nX-App: vmtest\r\n{1}Content-Length: {2}\r\n\r\n{3}"
                , contentType
                , CORS
                , ret.Length
                , ret);

            res.AddHeader("X-App", "vmtest");
            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.AddHeader("Access-Control-Allow-Methods", "*");
            res.AddHeader("Content-Type", contentType + "; charset=UTF-8");
            Console.WriteLine(req.Url.AbsolutePath, query.ToString());
            return isExit ? null : ret;
        }

    }
}
