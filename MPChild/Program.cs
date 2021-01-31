using ESRI.ArcGIS.esriSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MPChild
{
    class Program
    {
        private static LicenseInitializer m_AOLicenseInitializer = new MPChild.LicenseInitializer();

        static int Main(string[] args)
        {
            try
            {
                string file = args[0];
                //ESRI License Initializer generated code.
                m_AOLicenseInitializer.InitializeApplication(new esriLicenseProductCode[] { esriLicenseProductCode.esriLicenseProductCodeEngine, esriLicenseProductCode.esriLicenseProductCodeBasic, esriLicenseProductCode.esriLicenseProductCodeStandard, esriLicenseProductCode.esriLicenseProductCodeAdvanced },
                new esriLicenseExtensionCode[] { esriLicenseExtensionCode.esriLicenseExtensionCodeSpatialAnalyst });

                //使用启动参数新建处理对象并进行处理
                MainProcessor i = new MainProcessor(file);
                i.ProcessFile();

                //ESRI License Initializer generated code.
                //Do not make any call to ArcObjects after ShutDownApplication()
                m_AOLicenseInitializer.ShutdownApplication();
                //Console.ReadKey();
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                //Console.ReadKey();
                return -1;
            }
        }
    }

    /// <summary>
    /// 真正进行处理的对象。
    /// </summary>
    public class MainProcessor
    {
        /// <summary>
        /// 文件夹名称。
        /// </summary>
        string folderName;
        /// <summary>
        /// 工作目录。
        /// </summary>
        string rootFolder;
        /// <summary>
        /// 存放原始数据的文件夹。
        /// </summary>
        string picFolder;
        /// <summary>
        /// 存放分类表的文件夹。
        /// </summary>
        string tableFolder;
        /// <summary>
        /// 重分类结果文件夹。
        /// </summary>
        string recFolder;
        /// <summary>
        /// 聚合结果文件夹。
        /// </summary>
        string aggFolder;
        /// <summary>
        /// 结果文件夹。
        /// </summary>
        string resultFolder;

        /// <summary>
        /// 分类表的完整路径列表。
        /// </summary>
        List<string> tables = new List<string>();
        /// <summary>
        /// 待处理栅格文件的完整路径列表。
        /// </summary>
        List<string> rasters = new List<string>();

        public MainProcessor(string folderPath)
        {
            DirectoryInfo current = new DirectoryInfo(folderPath);
            this.folderName = current.Name;
            rootFolder = current.Parent.Parent.FullName;
            picFolder = Path.Combine(rootFolder, "Picture", this.folderName);
            tableFolder = Path.Combine(rootFolder, "table");
            recFolder = Path.Combine(rootFolder, "Reclass", this.folderName);
            aggFolder = Path.Combine(rootFolder, "Aggregate", this.folderName);
            resultFolder = Path.Combine(rootFolder, "Result", this.folderName);

            GetRecTables();
        }

        /// <summary>
        /// 进行处理。
        /// </summary>
        public void ProcessFile()
        {
            //重分类
            GetRaster(picFolder);
            Common.CheckExists(recFolder);
            foreach (string origDataName in rasters)
            {
                for (int i = 0; i < tables.Count; i++)
                {
                    Console.WriteLine(string.Format("[{2} Reclassify start] Reclassifying {0}, table {1}.", folderName, i + 1, DateTime.Now.ToString("hh\\:mm\\:ss")));
                    ReclassFunc rF = new ReclassFunc(origDataName, tables[i], Path.Combine(recFolder, string.Format("{0}_table{1}.tif", folderName, (i + 1).ToString("00"))));
                    try
                    {
                        rF.Exec();
                        Console.WriteLine(string.Format("[{2} Reclassify done] Reclassify {0}, table {1} done.", folderName, i + 1, DateTime.Now.ToString("hh\\:mm\\:ss")));
                        Thread.Sleep(500);
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine(string.Format("[{2} Reclassify error] Error reclassifying {0}, table {1}.", folderName, i + 1, DateTime.Now.ToString("hh\\:mm\\:ss")));
                        Console.WriteLine(err.ToString());
                    }
                }
            }

            //聚合
            GetRaster(recFolder);
            Common.CheckExists(aggFolder);
            foreach (string recDataName in rasters)
            {
                string tFilename = Path.GetFileNameWithoutExtension(recDataName);
                Console.WriteLine(string.Format("[{1} Aggregate start] Aggregating {0}.", tFilename, DateTime.Now.ToString("hh\\:mm\\:ss")));
                AggregateFunc aF = new AggregateFunc(recDataName, 60, Path.Combine(aggFolder, string.Format("{0}_Agg.tif", tFilename)));
                try
                {
                    aF.Exec();
                    Console.WriteLine(string.Format("[{1} Aggregate done] Aggregate {0} done.", tFilename, DateTime.Now.ToString("hh\\:mm\\:ss")));
                    Thread.Sleep(500);
                }
                catch (Exception err)
                {
                    Console.WriteLine(string.Format("[{1} Aggregate error] Aggregating {0} error.", tFilename, DateTime.Now.ToString("hh\\:mm\\:ss")));
                    Console.WriteLine(err.ToString());
                }
            }

            //除以3600
            GetRaster(aggFolder);
            Common.CheckExists(resultFolder);
            foreach (string aggDataName in rasters)
            {
                string tFilename = Path.GetFileNameWithoutExtension(aggDataName);
                Console.WriteLine(string.Format("[{1} Divide start] Dividing {0} by 3600.", tFilename, DateTime.Now.ToString("hh\\:mm\\:ss")));
                DivideFunc dF = new DivideFunc(aggDataName, "3600", Path.Combine(resultFolder, string.Format("{0}_Div.tif", tFilename)));
                try
                {
                    dF.Exec();
                    Console.WriteLine(string.Format("[{1} Divide done] Divide {0} by 3600 done.", tFilename, DateTime.Now.ToString("hh\\:mm\\:ss")));
                    Thread.Sleep(500);
                }
                catch (Exception err)
                {
                    Console.WriteLine(string.Format("[{1} Divide error] Dividing {0} by 3600 error.", tFilename, DateTime.Now.ToString("hh\\:mm\\:ss")));
                    Console.WriteLine(err.ToString());
                }
            }
        }

        /// <summary>
        /// 获取重分类表
        /// </summary>
        private void GetRecTables()
        {
            DirectoryInfo d = new DirectoryInfo(tableFolder);
            foreach (FileInfo f in d.GetFiles())
            {
                tables.Add(f.FullName);
            }
        }

        /// <summary>
        /// 获取指定文件夹下所有tif文件。
        /// </summary>
        /// <param name="path"></param>
        private void GetRaster(string path)
        {
            rasters = Directory.GetFiles(path, "*.tif").ToList();
        }
    }
}
//Reference: https://stackoverflow.com/questions/2807654/multi-threaded-file-processing-with-net