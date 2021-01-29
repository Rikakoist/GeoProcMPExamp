using ESRI.ArcGIS.esriSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Diagnostics;

namespace MTTest
{
    class Program
    {
        private static LicenseInitializer m_AOLicenseInitializer = new MTTest.LicenseInitializer();

        static void Main(string[] args)
        {
            //ESRI License Initializer generated code.
            m_AOLicenseInitializer.InitializeApplication(new esriLicenseProductCode[] { esriLicenseProductCode.esriLicenseProductCodeEngine, esriLicenseProductCode.esriLicenseProductCodeBasic, esriLicenseProductCode.esriLicenseProductCodeStandard },
            new esriLicenseExtensionCode[] { esriLicenseExtensionCode.esriLicenseExtensionCodeSpatialAnalyst });

            string workFolder;
            if (Debugger.IsAttached)
            {
                Console.WriteLine("Debug env, workfolder set.");
                workFolder = @"E:\Test";    //debug folder
            }
            else
            {
                Console.Write("Set workfolder:");
                workFolder = Console.ReadLine();
            }


            ImgProcessor iw = new ImgProcessor(workFolder);

            Stopwatch SW = new Stopwatch();
            SW.Reset();
            SW.Start();
            iw.LaunchWaitingThreads();

            while (!iw.Done) { Thread.Sleep(200); }

            SW.Stop();
            Console.WriteLine("All done! Time elapsed: {0}", SW.Elapsed.ToString("hh\\:mm\\:ss"));
            Console.ReadKey();
            Console.ReadKey();
            //ESRI License Initializer generated code.
            //Do not make any call to ArcObjects after ShutDownApplication()
            m_AOLicenseInitializer.ShutdownApplication();
        }
    }

    public delegate void StartCallbackDelegate(int idArg, ImgWorker workerArg);
    public delegate void DoneCallbackDelegate(int idArg);

    public class ImgProcessor
    {
        string rootDir;
        Queue<Thread> waitingThreads = new Queue<Thread>();
        Dictionary<int, ImgWorker> runningThreads = new Dictionary<int, ImgWorker>();
        int maxThreads = 2;
        object locker = new object();

        public bool Done
        {
            get
            {
                lock (locker)
                {
                    return ((waitingThreads.Count == 0) && (runningThreads.Count == 0));
                }
            }
        }

        public ImgProcessor(string RootDir)
        {
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_ComputerSystem").Get())
            {
                Console.WriteLine("Number Of Physical Processors: {0} ", item["NumberOfProcessors"]);
            }
            int coreCount = 0;
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_Processor").Get())
            {
                coreCount += int.Parse(item["NumberOfCores"].ToString());
            }
            Console.WriteLine("Number Of Cores: {0}", coreCount);
            Console.WriteLine("Number Of Logical Processors: {0}", Environment.ProcessorCount);
            Console.WriteLine("Set maxThread to {0}", coreCount);
            maxThreads = coreCount;

            rootDir = RootDir;
            Common.GetAllChildDir(Path.Combine(rootDir, "Picture")).ForEach(n => waitingThreads.Enqueue(CreateThread(n)));
        }

        Thread CreateThread(string fileNameArg)
        {
            Thread thread = new Thread(new ImgWorker(fileNameArg, WorkerStart, WorkerDone).ProcessFile);
            thread.IsBackground = true;
            return thread;
        }

        // called when a worker starts
        public void WorkerStart(int threadIdArg, ImgWorker workerArg)
        {
            lock (locker)
            {
                // update with worker instance
                runningThreads[threadIdArg] = workerArg;
            }
        }

        // called when a worker finishes
        public void WorkerDone(int threadIdArg)
        {
            lock (locker)
            {
                runningThreads.Remove(threadIdArg);
            }
            Console.WriteLine(string.Format("Thread {0} done", threadIdArg.ToString()));
            LaunchWaitingThreads();
        }

        // launches workers until max is reached
        public void LaunchWaitingThreads()
        {
            lock (locker)
            {
                while ((runningThreads.Count < maxThreads) && (waitingThreads.Count > 0))
                {
                    Thread thread = waitingThreads.Dequeue();
                    runningThreads.Add(thread.ManagedThreadId, null); // place holder so count is accurate
                    thread.Start();
                }
            }
        }
    }

    public class ImgWorker
    {
        string folderName;
        string rootFolder;
        string picFolder;
        string tableFolder;
        string recFolder;

        string aggFolder;

        string resultFolder;

        List<string> tables = new List<string>();
        List<string> rasters = new List<string>();

        StartCallbackDelegate startCallback;
        DoneCallbackDelegate doneCallback;
        public ImgWorker(string fileNameArg, StartCallbackDelegate startCallbackArg, DoneCallbackDelegate doneCallbackArg)
        {
            DirectoryInfo current = new DirectoryInfo(fileNameArg);
            folderName = current.Name;
            rootFolder = current.Parent.Parent.FullName;
            picFolder = Path.Combine(rootFolder, "Picture", folderName);
            tableFolder = Path.Combine(rootFolder, "table");
            recFolder = Path.Combine(rootFolder, "Reclass", folderName);
            aggFolder = Path.Combine(rootFolder, "Aggregate", folderName);
            resultFolder = Path.Combine(rootFolder, "Result", folderName);

            startCallback = startCallbackArg;
            doneCallback = doneCallbackArg;
            GetRecTables();
        }

        public void ProcessFile()
        {
            startCallback(Thread.CurrentThread.ManagedThreadId, this);

            GetRaster(picFolder);
            Common.CheckExists(recFolder);
            foreach (string origDataName in rasters)
            {
                for (int i = 0; i < tables.Count; i++)
                {
                    Console.WriteLine(string.Format("[{3} Start] Reclassifying {0}, table {1} on thread {2}.", folderName, i + 1, Thread.CurrentThread.ManagedThreadId.ToString(), DateTime.Now.ToString("hh\\:mm\\:ss")));
                    ReclassFunc rF = new ReclassFunc(origDataName, tables[i], Path.Combine(recFolder, string.Format("{0}_table{1}.tif", folderName, (i + 1).ToString("00"))));
                    try
                    {
                        rF.Exec();
                        Console.WriteLine(string.Format("[{3} Done] Reclassify {0}, table {1} on thread {2} done.", folderName, i + 1, Thread.CurrentThread.ManagedThreadId.ToString(), DateTime.Now.ToString("hh\\:mm\\:ss")));
                        Thread.Sleep(500);
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine(string.Format("[{3} Error] Error reclassifying {0}, table {1} on thread {2}.", folderName, i + 1, Thread.CurrentThread.ManagedThreadId.ToString(), DateTime.Now.ToString("hh\\:mm\\:ss")));
                    }
                }
            }

            GetRaster(recFolder);
            Common.CheckExists(aggFolder);
            foreach (string recDataName in rasters)
            {
                string tFilename = Path.GetFileNameWithoutExtension(recDataName);
                Console.WriteLine(string.Format("[{2} Start] Aggregating {0}, on thread {1}.", tFilename, Thread.CurrentThread.ManagedThreadId.ToString(), DateTime.Now.ToString("hh\\:mm\\:ss")));
                AggregateFunc aF = new AggregateFunc(recDataName,60,Path.Combine(aggFolder,string.Format("{0}_Agg.tif", tFilename)));
                try
                {
                    aF.Exec();
                    Console.WriteLine(string.Format("[{2} Done] Aggregation {0}, on thread {1} done.", tFilename, Thread.CurrentThread.ManagedThreadId.ToString(), DateTime.Now.ToString("hh\\:mm\\:ss")));
                    Thread.Sleep(500);
                }
                catch (Exception err)
                {
                    Console.WriteLine(string.Format("[{2} Error] Aggregating {0}, on thread {1} error.", tFilename, Thread.CurrentThread.ManagedThreadId.ToString(), DateTime.Now.ToString("hh\\:mm\\:ss")));
                }
            }

            GetRaster(aggFolder);
            Common.CheckExists(resultFolder);
            foreach(string aggDataName in rasters)
            {
                string tFilename = Path.GetFileNameWithoutExtension(aggDataName);
                Console.WriteLine(string.Format("[{2} Start] Dividing {0} by 3600, on thread {1}.", tFilename, Thread.CurrentThread.ManagedThreadId.ToString(), DateTime.Now.ToString("hh\\:mm\\:ss")));
                DivideFunc dF = new DivideFunc(aggDataName, "3600", Path.Combine(resultFolder, string.Format("{0}_Div.tif", tFilename)));
                try
                {
                    dF.Exec();
                    Console.WriteLine(string.Format("[{2} Done] Dividing {0} by 3600, on thread {1} done.", tFilename, Thread.CurrentThread.ManagedThreadId.ToString(), DateTime.Now.ToString("hh\\:mm\\:ss")));
                    Thread.Sleep(500);
                }
                catch(Exception err)
                {
                    Console.WriteLine(string.Format("[{2} Error] Dividing {0} by 3600, on thread {1} error.", tFilename, Thread.CurrentThread.ManagedThreadId.ToString(), DateTime.Now.ToString("hh\\:mm\\:ss")));
                }
            }

            doneCallback(Thread.CurrentThread.ManagedThreadId);
        }

        private void GetRecTables()
        {
            DirectoryInfo d = new DirectoryInfo(tableFolder);
            foreach (FileInfo f in d.GetFiles())
            {
                tables.Add(f.FullName);
            }
        }

        private void GetRaster(string path)
        {
            rasters = Directory.GetFiles(path, "*.tif").ToList();
        }
    }
}
//From https://stackoverflow.com/questions/2807654/multi-threaded-file-processing-with-net


