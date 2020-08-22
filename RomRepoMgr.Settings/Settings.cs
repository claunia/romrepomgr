/******************************************************************************
// RomRepoMgr - ROM repository manager
// ----------------------------------------------------------------------------
//
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// --[ License ] --------------------------------------------------------------
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as
//     published by the Free Software Foundation, either version 3 of the
//     License, or (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
// ----------------------------------------------------------------------------
// Copyright © 2020 Natalia Portillo
*******************************************************************************/

using System;
using System.IO;
using System.Text.Json;
using Aaru.CommonTypes.Interop;
using Claunia.PropertyList;
using Microsoft.Win32;
using PlatformID = Aaru.CommonTypes.Interop.PlatformID;

namespace RomRepoMgr.Settings
{
    public sealed class SetSettings
    {
        public string DatabasePath    { get; set; }
        public string RepositoryPath  { get; set; }
        public string TemporaryFolder { get; set; }
        public string UnArchiverPath  { get; set; }
    }

    /// <summary>Manages statistics</summary>
    public static class Settings
    {
        const string XDG_CONFIG_HOME          = "XDG_CONFIG_HOME";
        const string XDG_CONFIG_HOME_RESOLVED = ".config";
        /// <summary>Current statistcs</summary>
        public static SetSettings Current;

        /// <summary>Loads settings</summary>
        public static void LoadSettings()
        {
            Current = new SetSettings();
            PlatformID ptId     = DetectOS.GetRealPlatformID();
            string     homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            FileStream   prefsFs = null;
            StreamReader prefsSr = null;

            try
            {
                switch(ptId)
                {
                    // In case of macOS or iOS settings will be saved in ~/Library/Preferences/com.claunia.romrepomgr.plist
                    case PlatformID.MacOSX:
                    case PlatformID.iOS:
                    {
                        string preferencesPath =
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library",
                                         "Preferences");

                        string preferencesFilePath = Path.Combine(preferencesPath, "com.claunia.romrepomgr.plist");

                        if(!File.Exists(preferencesFilePath))
                        {
                            SetDefaultSettings();
                            SaveSettings();
                        }

                        prefsFs = new FileStream(preferencesFilePath, FileMode.Open, FileAccess.Read);

                        var parsedPreferences = (NSDictionary)BinaryPropertyListParser.Parse(prefsFs);

                        if(parsedPreferences != null)
                        {
                            NSObject obj;

                            Current.DatabasePath = parsedPreferences.TryGetValue("DatabasePath", out obj)
                                                       ? ((NSString)obj).ToString() : null;

                            Current.RepositoryPath = parsedPreferences.TryGetValue("RepositoryPath", out obj)
                                                         ? ((NSString)obj).ToString() : null;

                            Current.TemporaryFolder = parsedPreferences.TryGetValue("TemporaryFolder", out obj)
                                                          ? ((NSString)obj).ToString() : null;

                            Current.UnArchiverPath = parsedPreferences.TryGetValue("UnArchiverPath", out obj)
                                                         ? ((NSString)obj).ToString() : null;

                            prefsFs.Close();
                        }
                        else
                        {
                            prefsFs.Close();

                            SetDefaultSettings();
                            SaveSettings();
                        }
                    }

                        break;
                #if !NETSTANDARD2_0

                    // In case of Windows settings will be saved in the registry: HKLM/SOFTWARE/Claunia.com/RomRepoMgr
                    case PlatformID.Win32NT:
                    case PlatformID.Win32S:
                    case PlatformID.Win32Windows:
                    case PlatformID.WinCE:
                    case PlatformID.WindowsPhone:
                    {
                        RegistryKey parentKey = Registry.CurrentUser.OpenSubKey("SOFTWARE")?.OpenSubKey("Claunia.com");

                        if(parentKey == null)
                        {
                            SetDefaultSettings();
                            SaveSettings();

                            return;
                        }

                        RegistryKey key = parentKey.OpenSubKey("RomRepoMgr");

                        if(key == null)
                        {
                            SetDefaultSettings();
                            SaveSettings();

                            return;
                        }

                        Current.DatabasePath    = key.GetValue("DatabasePath") as string;
                        Current.RepositoryPath  = key.GetValue("RepositoryPath") as string;
                        Current.TemporaryFolder = key.GetValue("TemporaryFolder") as string;
                        Current.UnArchiverPath  = key.GetValue("UnArchiverPath") as string;
                    }

                        break;
                #endif

                    // Otherwise, settings will be saved in ~/.config/RomRepoMgr.json
                    default:
                    {
                        string xdgConfigPath =
                            Path.Combine(homePath,
                                         Environment.GetEnvironmentVariable(XDG_CONFIG_HOME) ??
                                         XDG_CONFIG_HOME_RESOLVED);

                        string settingsPath = Path.Combine(xdgConfigPath, "RomRepoMgr.json");

                        if(!File.Exists(settingsPath))
                        {
                            SetDefaultSettings();
                            SaveSettings();

                            return;
                        }

                        prefsSr = new StreamReader(settingsPath);

                        Current = JsonSerializer.Deserialize<SetSettings>(prefsSr.ReadToEnd(), new JsonSerializerOptions
                        {
                            AllowTrailingCommas         = true,
                            PropertyNameCaseInsensitive = true,
                            ReadCommentHandling         = JsonCommentHandling.Skip,
                            WriteIndented               = true
                        });
                    }

                        break;
                }
            }
            catch
            {
                prefsFs?.Close();
                prefsSr?.Close();
                SetDefaultSettings();
                SaveSettings();
            }
        }

        public static void SaveSettings()
        {
            try
            {
                PlatformID ptId = DetectOS.GetRealPlatformID();

                switch(ptId)
                {
                    // In case of macOS or iOS settings will be saved in ~/Library/Preferences/com.claunia.romrepomgr.plist
                    case PlatformID.MacOSX:
                    case PlatformID.iOS:
                    {
                        var root = new NSDictionary
                        {
                            {
                                "DatabasePath", Current.DatabasePath
                            },
                            {
                                "RepositoryPath", Current.RepositoryPath
                            },
                            {
                                "TemporaryFolder", Current.TemporaryFolder
                            },
                            {
                                "UnArchiverPath", Current.UnArchiverPath
                            }
                        };

                        string preferencesPath =
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library",
                                         "Preferences");

                        string preferencesFilePath = Path.Combine(preferencesPath, "com.claunia.romrepomgr.plist");

                        var fs = new FileStream(preferencesFilePath, FileMode.Create);
                        BinaryPropertyListWriter.Write(fs, root);
                        fs.Close();
                    }

                        break;
                #if !NETSTANDARD2_0

                    // In case of Windows settings will be saved in the registry: HKLM/SOFTWARE/Claunia.com/RomRepoMgr
                    case PlatformID.Win32NT:
                    case PlatformID.Win32S:
                    case PlatformID.Win32Windows:
                    case PlatformID.WinCE:
                    case PlatformID.WindowsPhone:
                    {
                        RegistryKey parentKey = Registry.CurrentUser.OpenSubKey("SOFTWARE", true)?.
                                                         CreateSubKey("Claunia.com");

                        RegistryKey key = parentKey?.CreateSubKey("RomRepoMgr");

                        key?.SetValue("DatabasePath", Current.DatabasePath);
                        key?.SetValue("RepositoryPath", Current.RepositoryPath);
                        key?.SetValue("TemporaryFolder", Current.TemporaryFolder);
                        key?.SetValue("UnArchiverPath", Current.UnArchiverPath);
                    }

                        break;
                #endif

                    // Otherwise, settings will be saved in ~/.config/RomRepoMgr.json
                    default:
                    {
                        string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                        string xdgConfigPath =
                            Path.Combine(homePath,
                                         Environment.GetEnvironmentVariable(XDG_CONFIG_HOME) ??
                                         XDG_CONFIG_HOME_RESOLVED);

                        string settingsPath = Path.Combine(xdgConfigPath, "RomRepoMgr.json");

                        if(!Directory.Exists(xdgConfigPath))
                            Directory.CreateDirectory(xdgConfigPath);

                        var prefsSr = new StreamWriter(settingsPath);

                        prefsSr.Write(JsonSerializer.Serialize(Current, new JsonSerializerOptions
                        {
                            AllowTrailingCommas         = true,
                            PropertyNameCaseInsensitive = true,
                            ReadCommentHandling         = JsonCommentHandling.Skip,
                            WriteIndented               = true
                        }));

                        prefsSr.Close();
                    }

                        break;
                }
            }
            #pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
            catch
            {
                // ignored
            }
            #pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
        }

        /// <summary>Sets default settings as all statistics, share everything</summary>
        static void SetDefaultSettings()
        {
            string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string dataPath = Path.Combine(docsPath, "RomRepoMgr");

            Current = new SetSettings
            {
                DatabasePath    = Path.Combine(dataPath, "romrepo.db"),
                RepositoryPath  = Path.Combine(dataPath, "repo"),
                TemporaryFolder = Path.GetTempPath()
            };
        }
    }
}