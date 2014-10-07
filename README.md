ASNA Component Installer
========================

The ASNA Component Installer (CI) is a small .NET console app that installs ASNA GitHub project runtimes for your apps.

This installer doesn't install the Visual Studio projects. Its intent to install, and at least minimal, configure your project to use the package you're installing. The ASNA open source Visual Studio projects are available for download in their own repositories at ASNA's various GitHub repositories. 

The CI's main purpose is to make easy to add ASNA open source project components to your project. CI components many parts of a project or only a single part. CI's component definition lets you be as granular as necessary when you define your own components. Many components need files in a very specific directory structure and the PI ensures project files are installed appropriately.   

To install the CI, [download the project](https://github.com/ASNApalooza/ProjectInstaller) and compile it. Then put all of the files from the project's bin\release folder contents in a folder that is in your path. 

To use the CI, open a DOS command box and navigate to a project's root folder. Type 
`'projectinstaller n'`
 (where n is a package name) and press Enter. 

The current package names available are:
  * asna-helpers-services
  * asna-helpers-aspnet

CI packages have the ability to specify nested dependencies so installing one package may result in more than one package being installed.

At runtime, the CI pulls down the corresponding package files from GitHub and installs the specified files in their specified locations in your project. After installing a package the package files (which have a `.apkg` extension) used will be in your project's root folder. There will also be projectinstaller.manifest and optionally a projectinstaller.log file. These files provide additional information about the install.

Packages many take a few minutes to install--be patient. After all files have installed, depending on the package installed, your browser may be opened to a page about the package installed.           