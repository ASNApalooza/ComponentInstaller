using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using System.Diagnostics;
using System.Configuration;
using log4net;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            bool isWindows = false; 

            if (args.Length == 0) throw new System.ArgumentException("Must provide config id or file with first argument.");

            if (Regex.Match(args[0],"[-][-]list-packages").Success) {
                string packageListUrl = ConfigurationManager.AppSettings["packageListUrl"];
                Process.Start(packageListUrl);
                System.Environment.Exit(-1);                                           
            }

            string configFile;
            string rootTargetLocation = Directory.GetCurrentDirectory();

            configFile = args[0];
            if (! Regex.Match(configFile,"/.apkg$",RegexOptions.IgnoreCase).Success) {
                configFile = configFile + ".apkg";
            }

            //// This is for debugging purposes
            //if (args.Length == 2 || args.Length == 3) {
            //    rootTargetLocation = args[1];
            //}

            foreach (string arg in args) {
                if (arg == "/windows" ) {
                    isWindows = true;
                }
            }

            rpPackager rp = new rpPackager();

            rp.ReadConfig(configFile, rootTargetLocation, isWindows);
        }
    }

    class rpPackager
    {
        bool isWindows = false;
        StringBuilder manifestFile = new StringBuilder();
        ILog log;

        public string postInstallUrl = null;
        public Stack<string> packageFiles = new Stack<string>();

        private void GetConfigFiles(string configFile, string rootTargetLocation) {
            string gitHubLocation = ConfigurationManager.AppSettings["GitHubLocationRoot"];
            string targetConfigFile = rootTargetLocation + ensureDosSlash(configFile);

            DownloadFileWithHttp(gitHubLocation + "/" + configFile, targetConfigFile);
            packageFiles.Push(targetConfigFile);

            GetDependencies(targetConfigFile, rootTargetLocation);
        }

        private void GetDependencies(string targetConfigFile, string rootTargetLocation) {
            string gitHubLocation = ConfigurationManager.AppSettings["GitHubLocationRoot"];
            string currentTargetConfigFile;
            XmlDocument xml = new XmlDocument();

            xml.Load(targetConfigFile);

            XmlNodeList files = xml.SelectNodes("//package//dependencies//dependency");
            foreach (XmlNode n in files)
            {
                string gitHubUri = gitHubLocation + "/" + n.Attributes["project"].Value;
                currentTargetConfigFile = rootTargetLocation + ensureDosSlash(n.Attributes["project"].Value);
                DownloadFileWithHttp(gitHubUri, currentTargetConfigFile);
                packageFiles.Push(currentTargetConfigFile);
                GetDependencies(currentTargetConfigFile, rootTargetLocation);
            }
        }

        public string ensureDosSlash(string value) {
            if (!value.StartsWith(@"\")) {
                return @"\" + value;
            }
            else {
                return value;
            }
        }

        public void ReadConfig(string configFile, string rootTargetLocation, bool isWindows) {
            this.isWindows = isWindows;
            log4net.Config.BasicConfigurator.Configure();
            log = log4net.LogManager.GetLogger(typeof(rpPackager));
            log4net.GlobalContext.Properties["LogFileName"] = rootTargetLocation + @"\projectinstaller.log";
            // The line above works with the line below from the App.Config to dynamically 
            // set the log file path.
            //      <file type="log4net.Util.PatternString" value="%property{LogFileName}" />

            List<PackageInfo> Packages = null;

            manifestFile.AppendFormat("ProjectInstaller manifest. Date: {0} Time{1}\r\n", DateTime.Now.ToString("dd-MMM-yyyy"), DateTime.Now.ToString("hh:mm.ss"));            
            manifestFile.AppendLine(@"Installed with /windows option which appends \debug to \bin target folders for Windows programs.");

            GetConfigFiles(configFile, rootTargetLocation); 

            while (packageFiles.Count > 0 ) {
                string packageFile = packageFiles.Pop(); 
                Packages = getPackages(rootTargetLocation,packageFile);
                DeployPackages(Packages);
            }
            
            if (!String.IsNullOrEmpty(postInstallUrl)) {
                Process.Start(postInstallUrl);                
            }

            log.Info("All packages successfully installed.");

            File.WriteAllText(rootTargetLocation + @"\projectinstaller.manifest", manifestFile.ToString());
        }

        private void DownloadFileWithHttp(string uri, string toFile) {
            log.Info("Downloading: " + uri);
            try {
                WebClient Client = new WebClient();
                Client.DownloadFile(uri, toFile);
            }
            catch (WebException wex) {
                log.Fatal("**ERROR**");
                log.Fatal(wex.Message);
                log.Fatal(String.Format("Package file {0} wasn't found at Uri given.",uri));
                System.Environment.Exit(1);
            }
            catch (Exception ex) {
                log.Fatal("**ERROR**");
                log.Fatal(ex.Message);
                log.Fatal(String.Format("An error occurred with package file: {0}.", uri));
                System.Environment.Exit(1);
            }
        }

        private void DeployPackages(List<PackageInfo> Packages)
        {
            foreach (PackageInfo pi in Packages)
            {
                CreateTargetFolders(pi.TargetFolder, pi.TargetRootLocation);

                string fromFile = pi.Location + pi.SourceFolder + pi.FileName;
                string toFile = pi.TargetRootLocation + pi.TargetFolder + pi.FileName;

                if (Regex.Match(fromFile, "^http", RegexOptions.IgnoreCase).Success)
                {
                    fromFile = fromFile.Replace(@"\", @"/");
                    toFile = toFile.Replace(@"/", @"\");
                    //WebClient Client = new WebClient();                    
                    //Client.DownloadFile(fromFile, toFile);
                    DownloadFileWithHttp(fromFile, toFile);
                }
                else
                {
                    fromFile = fromFile.Replace(@"/", @"\");
                    toFile = toFile.Replace(@"/", @"\");
                    File.Copy(fromFile, toFile);
                }

                manifestFile.AppendLine( "  From file:" + fromFile);
                manifestFile.AppendLine("     To file:" + toFile);
            }
        }

        private void CreateTargetFolders(string targetFolder, string TargetRootLocation)
        {
            string fullTargetFolder = TargetRootLocation;

            string[] folders = Regex.Split(targetFolder, @"\\");
            foreach (string f in folders)
            {
                if (!String.IsNullOrEmpty(f))
                {
                    fullTargetFolder = fullTargetFolder + @"\" + f;
                    if (!Directory.Exists(fullTargetFolder))
                    {
                        Directory.CreateDirectory(fullTargetFolder);
                    }
                }
            }
        }

        private List<PackageInfo> getPackages(string TargetRootLocation, string configFile) {
            List<PackageInfo> Packages = new List<PackageInfo>();
            XmlDocument xml = new XmlDocument();

            string projectName, location;

            xml.Load(configFile);

            XmlNode node = xml.SelectSingleNode("//package");
            projectName = node.Attributes["name"].Value;
            location = node.Attributes["location"].Value;
            if (node.Attributes["postInstallUrl"] != null) {
                this.postInstallUrl = node.Attributes["postInstallUrl"].Value;
            }

            XmlNodeList files = xml.SelectNodes("//package//files//file");
            foreach (XmlNode n in files)
            {
                PackageInfo p = new PackageInfo(TargetRootLocation);
                p.ProjectName = projectName;
                p.Location = location;
                p.FileName = n.Attributes["fileName"].Value;
                p.SourceFolder = n.Attributes["sourceFolder"].Value;
                p.TargetFolder = n.Attributes["targetFolder"].Value;

                if (this.isWindows)
                {
                    p.TargetFolder = p.TargetFolder + @"\debug";
                }

                Packages.Add(p);
            }

            return Packages;
        }

        private string getAliasAttribute(XmlDocument xml, string alias, string attributeName)
        {
            string mask = @"/root/configuration/files/file[@alias='{0}']";

            XmlNode node = xml.SelectSingleNode(String.Format(mask, alias));
            return node.Attributes[attributeName].Value;
        }
    }

    public class PackageInfo {        
        public string ProjectName {get;set;}
        public string Location {get; set;}
        public string FileName {get; set;}
        public string SourceFolder {get; set;}
        public string TargetFolder {get; set;}
        public string TargetRootLocation {get;set;}

        public PackageInfo(string TargetRootLocation) {
            this.TargetRootLocation = TargetRootLocation;
        } 
    }
}
