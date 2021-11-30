﻿// Developed by Phillip Hansen
//
// This is a command line program to change the wallpaper.  The program takes two arguments: the file (including path) to use as wallpaper, and the style (Tiled, Centered, Stretched, Fit, Fill)
// Syntax is: <file|directory> <style> <location>
//
//  <file> is the complete path to the file
//  <directory> is the complete path to a directory containing image files
//    a random image from the directory will be set as the background
//  <style> is an integer (if no style is specified it defaults to Fill):
//    0 for Tiled
//    1 for Centered
//    2 for Stretched
//    3 for Fit (Windows 7 or later)
//    4 for Fill (Windows 7 or later)
//  <location> is the complete path to a directory for storing the generated file
//    defaults to the temp folder which should be fine in most cases
//
// If the style argument is not specified it will default to Fill.
//
// Optional flags:
//   -h, -help   - Display the usage help
//   -r, -remove - Remove the current wallpaper
//   -m, -monitor <index> - Set the image on the specified monitor (0 indexed)
//      When using this option the full syntax is:
//          -m <index> <file|directory> <location>
//
// Alternatively a config file can be placed in the same directory as the WallpaperChanger executable.
// The file should be named 'WallpaperChanger.config'.  Each line in the file should have
// the full path to an image and can optionally include the monitor index or the style code to use. 
// If the style is not specified it will default to Fill.
//
// When setting the monitor index in the config file the format of the line should be: <file> -m <index>
// You can comment out a line in the config file by starting the line with a # character.
// 
// This program is intended to be used as a "helper" program that is executed from other programs

using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WallpaperChanger
{

    class Wallpaper
    {
        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDWININICHANGE = 0x02;

        // this is the system DLL for doing wallpaper stuff
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        public enum Style : int
        {
            Tiled,
            Centered,
            Stretched,
            Fit,
            Fill
        }

        public static int Set(String file, Style style, String storagePath)
        {
            try
            {
                System.Drawing.Image img = System.Drawing.Image.FromFile(file);
                // convert and save the image as a png/bmp file (png format should work better than bmp to avoid artifacts, but only available on Win8 or higher)
                if (storagePath.EndsWith("png"))
                    img.Save(storagePath, System.Drawing.Imaging.ImageFormat.Png);
                else
                    img.Save(storagePath, System.Drawing.Imaging.ImageFormat.Bmp);
            
                // update the registry
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
                if (style == Style.Tiled)
                {
                    key.SetValue(@"WallpaperStyle", 0.ToString());
                    key.SetValue(@"TileWallpaper", 1.ToString());
                }
                if (style == Style.Centered)
                {
                    key.SetValue(@"WallpaperStyle", 0.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                }
                if (style == Style.Stretched)
                {
                    key.SetValue(@"WallpaperStyle", 2.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                }
                if (style == Style.Fit)
                {
                    key.SetValue(@"WallpaperStyle", 6.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                }
                if (style == Style.Fill)
                {
                    key.SetValue(@"WallpaperStyle", 10.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                }

                // set the wallpaper using the external method
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, storagePath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            }
            catch (OutOfMemoryException ex)
            {  // thrown when the file does not have a valid image format or the decoder does not support the pixel format of the file
                Console.WriteLine("\nInvalid file format or the file format is not supported");
                return 1;
            }
            catch (Exception ex)
            { // catch everything else just in case
                Console.WriteLine("<unexpected error>\n\n" + ex.Message);
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Saves the file to the storage path location and sets it as the background for the specified monitor
        /// </summary>
        public static int SetMonitor(int monitorIndex, String file, Style style)
        {
            try
            {
                if (!IsWin8OrHigher())
                {
                    Console.WriteLine("Specifying a monitor is only supported on Windows 8 or higher\n");
                    return 1;
                }

                IDesktopWallpaper wallpaper = (IDesktopWallpaper)new DesktopWallpaper();
                wallpaper.SetWallpaper(wallpaper.GetMonitorDevicePathAt((uint)monitorIndex), file);

                DesktopWallpaperPosition pos = DesktopWallpaperPosition.Fill;
                switch (style)
                {
                    case Style.Tiled:
                        pos = DesktopWallpaperPosition.Tile;
                        break;
                    case Style.Centered:
                        pos = DesktopWallpaperPosition.Center;
                        break;
                    case Style.Stretched:
                        pos = DesktopWallpaperPosition.Stretch;
                        break;
                    case Style.Fit:
                        pos = DesktopWallpaperPosition.Fit;
                        break;
                    case Style.Fill:
                        pos = DesktopWallpaperPosition.Fill;
                        break;
                }
                wallpaper.SetPosition(pos);
            }
            catch (Exception ex)
            { // catch everything just in case
                Console.WriteLine("<unexpected error>\n\n" + ex.Message);
                return 1;
            }

            return 0;
        }

        public static int Remove()
        {
            try
            {
                // remove the wallpaper using the external method
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, "", SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            }
            // catch everything just in case
            catch (Exception ex)
            {
                Console.WriteLine("<unexpected error>\n\n" + ex.Message);
                return 1;
            }

            return 0;
        }

        static int Main(string[] args)
        {
            String help = "\nCopyright (c) 2005-" + DateTime.Now.Year.ToString() + " Phillip Hansen  http://sg20.com (version 1.8)\n"
                + "Source available at: https://github.com/philhansen/WallpaperChanger\n\nSyntax is: <file|directory> <style> <location>\n\n"
                + "  <file> is the complete path to the file\n"
                + "  <directory> is the complete path to a directory containing image files\n"
                + "    a random image from the directory will be set as the background\n"
                + "  <style> is an integer (if no style is specified it defaults to Fill):\n"
                + "    0 for Tiled\n    1 for Centered\n    2 for Stretched\n    3 for Fit (Windows 7 or later)\n    4 for Fill (Windows 7 or later)\n"
                + "  <location> is the complete path to a directory for storing the generated file\n"
                + "    defaults to the temp folder which should be fine in most cases";
            help += "\n\nIf the style argument is not specified it will default to Fill.";
            help += "\n\nOptional flags:\n"
                + "  -h, -help   - Display the usage help\n"
                + "  -r, -remove - Remove the current wallpaper\n"
                + "  -m, -monitor <index> - Set the image on the specified monitor (0 indexed)\n"
                + "     When using this option the full syntax is:\n"
                + "       -m <index> <file|directory> <location>";
            help += "\n\nAlternatively a config file can be placed in the same directory as the WallpaperChanger executable. "
                + "The file should be named 'WallpaperChanger.config'.  Each line in the file should have the full path "
                + "to an image and can optionally include the monitor index or the style code to use.  If the style is not specified it will default to Fill."
                + "\n\nWhen setting the monitor index in the config file the format of the line should be: <file> -m <index>\n"
                + "You can comment out a line in the config file by starting the line with a # character.";
            help += "\n";

            String path = "";
            bool setMonitor = false;
            int monitorIndex = 0;
            Style style = Style.Fill; // default value
            // Use png file for Win 8 or higher, otherwise use bmp file
            String fileType = "png";
            if (!IsWin8OrHigher())
                fileType = "bmp";
            // get the path to the user's temp folder
            String storagePath = Path.Combine(Path.GetTempPath(), "wallpaper." + fileType);

            // check the arguments
            if (args.Length == 0)
            {
                // a config file can be stored in the same directory as the wallpaper changer
                String configFile = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "WallpaperChanger.config");
                if (File.Exists(configFile))
                {
                    ProcessConfig(configFile, storagePath);
                    return 0;
                }
                else
                {
                    Console.WriteLine(help);
                    return 1;
                }
            }

            // special check for a help flag
            if (args[0] == "-h" || args[0] == "--help" || args[0] == "-help")
            {
                Console.WriteLine(help);
                return 1;
            }
            // remove wallpaper flag
            else if (args[0] == "-r" || args[0] == "-remove")
            {
                return Remove();
            }
            // specify monitor
            else if (args[0] == "-m" || args[0] == "-monitor")
            {
                if (!IsWin8OrHigher())
                {
                    Console.WriteLine("Specifying a monitor is only supported on Windows 8 or higher\n");
                    return 1;
                }
                if (args.Length < 3)
                {
                    Console.WriteLine(help);
                    return 1;
                }
                setMonitor = true;
                monitorIndex = int.Parse(args[1]);
                path = args[2];
            }
            // retrieve file/directory if we are not using config file
            else
            {
                path = args[0];
                if (args.Length >= 2)
                {
                    style = (Wallpaper.Style)Enum.Parse(typeof(Wallpaper.Style), args[1]);
                }
            }

            int index = (setMonitor) ? 3 : 2;
            // location directory may be specified
            if (args.Length >= index + 1)
            {
                // make sure it's a directory
                if (!Directory.Exists(args[index]))
                {
                    Console.WriteLine("\n{0} is not a valid directory.", args[index]);
                    return 1;
                }
                storagePath = Path.Combine(args[index], "wallpaper." + fileType);
            }

            if (File.Exists(path))
            {
                int status = 0;
                if (setMonitor)
                    status = Wallpaper.SetMonitor(monitorIndex, path, style);
                else
                    status = Wallpaper.Set(path, style, storagePath);
                if (status == 1)
                    return 1;
            }
            else if (Directory.Exists(path))
            {
                String file = ProcessDirectory(path);
                if (file == null)
                {
                    Console.WriteLine("\nNo valid images found in {0}.", path);
                    return 1;
                }
                int status = 0;
                if (setMonitor)
                    status = Wallpaper.SetMonitor(monitorIndex, file, style);
                else
                    status = Wallpaper.Set(file, style, storagePath);
                if (status == 1)
                    return 1;
            }
            else
            {
                Console.WriteLine("\n{0} is not a valid file or directory.", path);
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Get the list of files from the given directory and choose a random image file
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        static String ProcessDirectory(String path)
        {
            ArrayList files = new ArrayList();
            bool finished = false;
            String file = null;

            // get the list of files in the directory
            foreach (String tmpFile in Directory.GetFiles(path))
            {
                files.Add(tmpFile);
            }

            if (files.Count == 0) // ignore empty directories
            {
                return null;
            }

            // initialize a Random object with a unique seed (based on the current time)
            Random randomObject = new Random(((int)DateTime.Now.Ticks));

            // select a random file and check it against the list of file types
            while (!finished)
            {
                // pick a random index from the list
                int index = randomObject.Next(0, files.Count);

                String f = (String)files[index];
                // this is an image file
                if (f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                {
                    file = f;
                    finished = true;
                }
                // this is not an image, remove it from the list
                else
                {
                    files.RemoveAt(index);
                }

                // if the files list is now empty, end the loop
                if (files.Count == 0)
                {
                    finished = true;
                }
            }
            
            return file;
        }

        /// <summary>
        /// Apply the image from each line of the config file
        /// </summary>
        /// <param name="path">config file</param>
        /// <param name="storagePath">temp file</param>
        static void ProcessConfig(String path, String storagePath)
        {
            string file = null;
            Style style = Style.Fill;
            bool setMonitor = false;
            int monitorIndex = 0;
            ArrayList files = new ArrayList();
            
            try
            {
                using (StreamReader sr = new StreamReader(path))
                {
                    String line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Trim() != "" && line[0] != '#')
                        {
                            files.Add(line);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading from config file\n\n" + ex.ToString());
                return;
            }

            if (files.Count == 0)
            {
                return;
            }

            // Apply all the images
            for (int index = 0; index < files.Count; index++)
            {
                String f = (String)files[index];
                // parse the file
                // can contain file and optionally the style and monitor index
                // split on the space between them, and combine values within double quotes (e.g. files with a space in the path)
                MatchCollection matches = Regex.Matches(f, @"(?<match>[^\s""]+)|""(?<match>[^""]*)""");
                String tmpFile = matches[0].Groups["match"].Value;

                // make sure the image exists
                if (File.Exists(tmpFile))
                {
                    file = tmpFile;

                    // parse the other options
                    int argNum = 1;
                    while (argNum < matches.Count)
                    {
                        string value = matches[argNum].Groups["match"].Value;

                        // check if a monitor index is specified
                        if (value == "-m" || value == "-monitor")
                        {
                            argNum++;

                            value = matches[argNum].Groups["match"].Value;
                            if (argNum < matches.Count && IsWin8OrHigher() && int.TryParse(value, out monitorIndex))
                            {
                                setMonitor = true;
                            }
                            argNum++;
                        }
                        else
                        {
                            // parse the style
                            style = (Wallpaper.Style)Enum.Parse(typeof(Wallpaper.Style), matches[argNum].Groups["match"].Value);
                            argNum++;
                        }
                    }

                    if (setMonitor)
                        Wallpaper.SetMonitor(monitorIndex, file, style);
                    else
                        Wallpaper.Set(file, style, storagePath);
                }
            }
        }

        /// <summary>
        /// Determine if the Operating system is Windows 8 or higher
        /// </summary>
        /// <returns>True if Win 8 or higher, False otherwise</returns>
        public static bool IsWin8OrHigher()
        {
            OperatingSystem OS = Environment.OSVersion;
            return (OS.Platform == PlatformID.Win32NT) && ((OS.Version.Major > 6) || ((OS.Version.Major == 6) && (OS.Version.Minor >= 2)));
        }
    }
}
