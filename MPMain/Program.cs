using MPChild;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MPMain
{
    class Program
    {
        static void Main(string[] args)
        {          
            string workFolder;  //工作文件夹
            try
            {
                if (Debugger.IsAttached)    //Debug环境下无需手动输入文件夹
                {
                    Console.WriteLine("Debug env, workfolder set.");
                    workFolder = @"E:\Test";    //debug folder
                }
                else
                {
                    if (args.Length > 0)
                    {
                        workFolder = args[0];
                        Console.WriteLine("Work folder set to {0}.", workFolder);
                    }
                    else
                    {
                        Console.Write("Set workfolder:");
                        workFolder = Console.ReadLine();
                    }
                }


                ThreadManager iP = new ThreadManager(workFolder); //使用工作目录初始化对象

                //计时的停表
                Stopwatch SW = new Stopwatch();
                SW.Reset();
                SW.Start(); //开始计时

                //开始多线程操作
                iP.LaunchWaitingThreads();
                while (!iP.Done) { Thread.Sleep(200); }

                SW.Stop();  //停止计时

                Console.WriteLine("All done! Time elapsed: {0}", SW.Elapsed.ToString("hh\\:mm\\:ss"));
                Console.ReadKey();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadKey();
            }
        }
    }

    //回调们
    public delegate void StartCallbackDelegate(int idArg, ChildProcWorker workerArg);
    public delegate void DoneCallbackDelegate(int idArg);

    public class ThreadManager
    {
        /// <summary>
        /// 根目录。
        /// </summary>
        string rootDir;
        /// <summary>
        /// 等待线程队列。
        /// </summary>
        Queue<Thread> waitingThreads = new Queue<Thread>();
        /// <summary>
        /// 正在运行线程的字典。
        /// </summary>
        Dictionary<int, ChildProcWorker> runningThreads = new Dictionary<int, ChildProcWorker>();
        /// <summary>
        /// 最大并发数。
        /// </summary>
        int maxThreads = 2;
        /// <summary>
        /// 锁。
        /// </summary>
        object locker = new object();

        /// <summary>
        /// 操作结束。
        /// </summary>
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

        /// <summary>
        /// 使用根目录初始化对象。
        /// </summary>
        /// <param name="RootDir">根目录。</param>
        public ThreadManager(string RootDir)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Magenta;
            //获取计算机的处理器数目
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_ComputerSystem").Get())
            {
                Console.WriteLine("Number Of Physical Processors: {0} ", item["NumberOfProcessors"]);
            }
            int coreCount = 0;
            //获取计算机的物理核心数
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_Processor").Get())
            {
                coreCount += int.Parse(item["NumberOfCores"].ToString());
            }
            Console.WriteLine("Number Of Cores: {0}", coreCount);
            Console.WriteLine("Number Of Logical Processors: {0}", Environment.ProcessorCount); //逻辑处理器数（就是任务管理器里面看到的框框数）
            Console.WriteLine("Set maxThread to {0}", coreCount);

            maxThreads = coreCount; //设置最大并发数

            rootDir = RootDir;
            Common.GetAllChildDir(Path.Combine(rootDir, "Picture")).ForEach(n => waitingThreads.Enqueue(CreateThread(n)));  //将待处理路径入队列
            Console.ResetColor();
        }

        /// <summary>
        /// 使用指定路径创建线程。
        /// </summary>
        /// <param name="fileNameArg">路径。</param>
        /// <returns>创建的线程。</returns>
        Thread CreateThread(string fileNameArg)
        {
            Thread thread = new Thread(new ChildProcWorker(fileNameArg, WorkerStart, WorkerDone).StartChildProc);    //新建线程
            thread.IsBackground = true;
            return thread;
        }

        /// <summary>
        /// 通知开始线程。
        /// </summary>
        /// <param name="threadIdArg">线程id。</param>
        /// <param name="workerArg">Worker的实例。</param>
        public void WorkerStart(int threadIdArg, ChildProcWorker workerArg)
        {
            lock (locker)
            {
                // update with worker instance
                runningThreads[threadIdArg] = workerArg;
            }
        }

        /// <summary>
        /// 通知线程结束，并开始正在等待的线程。
        /// </summary>
        /// <param name="threadIdArg">线程id。</param>
        public void WorkerDone(int threadIdArg)
        {
            lock (locker)
            {
                runningThreads.Remove(threadIdArg);
            }
            Console.WriteLine(string.Format("Thread {0} done.", threadIdArg.ToString()));
            LaunchWaitingThreads();
        }

        /// <summary>
        /// 启动线程直至最大线程数。
        /// </summary>
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

    /// <summary>
    /// 用于管理子进程的对象。
    /// </summary>
    public class ChildProcWorker
    {
        /// <summary>
        /// 文件夹名称。
        /// </summary>
        string folderName;
        /// <summary>
        /// 文件夹完整路径。
        /// </summary>
        string folderPath;

        StartCallbackDelegate startCallback;
        DoneCallbackDelegate doneCallback;

        public ChildProcWorker(string fileNameArg, StartCallbackDelegate startCallbackArg, DoneCallbackDelegate doneCallbackArg)
        {
            folderName = new DirectoryInfo(fileNameArg).Name;
            folderPath = fileNameArg;

            startCallback = startCallbackArg;
            doneCallback = doneCallbackArg;
        }

        /// <summary>
        /// 使用参数启动子进程。
        /// </summary>
        public void StartChildProc()
        {
            startCallback(Thread.CurrentThread.ManagedThreadId, this);

            try
            {
                ProcessStartInfo pSI = new ProcessStartInfo()
                {
                    Arguments = folderPath,
                    FileName = @"MPChild.exe",
                    UseShellExecute = false,
                };
                using (var p = Process.Start(pSI))
                {
                    p.WaitForExit();
                    Console.ForegroundColor = ConsoleColor.White;
                    switch (p.ExitCode)
                    {
                        case 0:
                            {
                                Console.BackgroundColor = ConsoleColor.Green;
                                Console.WriteLine(string.Format("[{1} Process done] Process {0} done.", folderName, DateTime.Now.ToString("HH\\:mm\\:ss")));
                                break;
                            }
                        case -1:
                            {
                                Console.BackgroundColor = ConsoleColor.Red;
                                Console.WriteLine(string.Format("[{1} Process error] Process {0} error.", folderName, DateTime.Now.ToString("HH\\:mm\\:ss")));
                                break;
                            }
                        default:
                            {
                                Console.BackgroundColor = ConsoleColor.Yellow;
                                Console.WriteLine(string.Format("[{1} Unkown error] Process {0} error.", folderName, DateTime.Now.ToString("HH\\:mm\\:ss")));
                                break;
                            }
                    }
                    Console.ResetColor();
                }
            }
            catch(Exception err)
            {
                Console.WriteLine(err.ToString());
            }
            doneCallback(Thread.CurrentThread.ManagedThreadId);
        }
    }
}
