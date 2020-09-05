using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ionic.Zip;
using Ionic.Zlib;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Resources;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;
using SharpCompress.Compressors.LZMA;
using CompressionMode = SharpCompress.Compressors.CompressionMode;

namespace RomRepoMgr.Core.Workers
{
    public class FileExporter
    {
        const    long                     BUFFER_SIZE = 131072;
        readonly string                   _outPath;
        readonly long                     _romSetId;
        long                              _filePosition;
        Dictionary<string, FileByMachine> _filesByMachine;
        long                              _machinePosition;
        Machine[]                         _machines;
        string                            _zipCurrentEntryName;

        public FileExporter(long romSetId, string outPath)
        {
            _romSetId = romSetId;
            _outPath  = outPath;
        }

        public event EventHandler                          WorkFinished;
        public event EventHandler<ProgressBoundsEventArgs> SetProgressBounds;
        public event EventHandler<ProgressEventArgs>       SetProgress;
        public event EventHandler<MessageEventArgs>        SetMessage;
        public event EventHandler<ProgressBoundsEventArgs> SetProgress2Bounds;
        public event EventHandler<ProgressEventArgs>       SetProgress2;
        public event EventHandler<MessageEventArgs>        SetMessage2;
        public event EventHandler<ProgressBoundsEventArgs> SetProgress3Bounds;
        public event EventHandler<ProgressEventArgs>       SetProgress3;
        public event EventHandler<MessageEventArgs>        SetMessage3;

        public void Export()
        {
            SetMessage?.Invoke(this, new MessageEventArgs
            {
                Message = Localization.RetrievingRomSetFromDatabase
            });

            using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

            RomSet romSet = ctx.RomSets.Find(_romSetId);

            if(romSet == null)
            {
                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.CouldNotFindRomSetInDatabase
                });

                WorkFinished?.Invoke(this, System.EventArgs.Empty);

                return;
            }

            SetMessage?.Invoke(this, new MessageEventArgs
            {
                Message = Localization.ExportingRoms
            });

            _machines = ctx.Machines.Where(m => m.RomSet.Id == _romSetId).ToArray();

            SetProgressBounds?.Invoke(this, new ProgressBoundsEventArgs
            {
                Minimum = 0,
                Maximum = _machines.Length
            });

            _machinePosition = 0;
            CompressNextMachine();
        }

        void CompressNextMachine()
        {
            SetProgress?.Invoke(this, new ProgressEventArgs
            {
                Value = _machinePosition
            });

            if(_machinePosition >= _machines.Length)
            {
                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.Finished
                });

                WorkFinished?.Invoke(this, System.EventArgs.Empty);

                return;
            }

            Machine machine = _machines[_machinePosition];

            SetMessage2?.Invoke(this, new MessageEventArgs
            {
                Message = machine.Name
            });

            using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

            string machineName = machine.Name;

            Dictionary<string, MediaByMachine> mediasByMachine = ctx.MediasByMachines.
                                                                     Where(f => f.Machine.Id == machine.Id &&
                                                                               f.Media.IsInRepo).
                                                                     ToDictionary(f => f.Name);

            if(mediasByMachine.Count > 0)
            {
                SetProgress2Bounds?.Invoke(this, new ProgressBoundsEventArgs
                {
                    Minimum = 0,
                    Maximum = mediasByMachine.Count
                });

                if(machineName.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                    machineName = machineName.Substring(0, machineName.Length - 4);

                string machinePath = Path.Combine(_outPath, machineName);

                if(!Directory.Exists(machinePath))
                    Directory.CreateDirectory(machinePath);

                long mediaPosition = 0;

                foreach(KeyValuePair<string, MediaByMachine> mediaByMachine in mediasByMachine)
                {
                    string outputPath = Path.Combine(machinePath, mediaByMachine.Key);

                    if(!outputPath.EndsWith(".aif", StringComparison.InvariantCultureIgnoreCase))
                        outputPath += ".aif";

                    SetProgress2?.Invoke(this, new ProgressEventArgs
                    {
                        Value = mediaPosition
                    });

                    string repoPath   = null;
                    string md5Path    = null;
                    string sha1Path   = null;
                    string sha256Path = null;

                    DbMedia media = mediaByMachine.Value.Media;

                    if(media.Sha256 != null)
                    {
                        byte[] sha256Bytes = new byte[32];
                        string sha256      = media.Sha256;

                        for(int i = 0; i < 32; i++)
                        {
                            if(sha256[i * 2] >= 0x30 &&
                               sha256[i * 2] <= 0x39)
                                sha256Bytes[i] = (byte)((sha256[i * 2] - 0x30) * 0x10);
                            else if(sha256[i * 2] >= 0x41 &&
                                    sha256[i * 2] <= 0x46)
                                sha256Bytes[i] = (byte)((sha256[i * 2] - 0x37) * 0x10);
                            else if(sha256[i * 2] >= 0x61 &&
                                    sha256[i * 2] <= 0x66)
                                sha256Bytes[i] = (byte)((sha256[i * 2] - 0x57) * 0x10);

                            if(sha256[(i * 2) + 1] >= 0x30 &&
                               sha256[(i * 2) + 1] <= 0x39)
                                sha256Bytes[i] += (byte)(sha256[(i * 2) + 1] - 0x30);
                            else if(sha256[(i * 2) + 1] >= 0x41 &&
                                    sha256[(i * 2) + 1] <= 0x46)
                                sha256Bytes[i] += (byte)(sha256[(i * 2) + 1] - 0x37);
                            else if(sha256[(i * 2) + 1] >= 0x61 &&
                                    sha256[(i * 2) + 1] <= 0x66)
                                sha256Bytes[i] += (byte)(sha256[(i * 2) + 1] - 0x57);
                        }

                        string sha256B32 = Base32.ToBase32String(sha256Bytes);

                        sha256Path = Path.Combine(Settings.Settings.Current.RepositoryPath, "aaru", "sha256",
                                                  sha256B32[0].ToString(), sha256B32[1].ToString(),
                                                  sha256B32[2].ToString(), sha256B32[3].ToString(),
                                                  sha256B32[4].ToString(), sha256B32 + ".aif");
                    }

                    if(media.Sha1 != null)
                    {
                        byte[] sha1Bytes = new byte[20];
                        string sha1      = media.Sha1;

                        for(int i = 0; i < 20; i++)
                        {
                            if(sha1[i * 2] >= 0x30 &&
                               sha1[i * 2] <= 0x39)
                                sha1Bytes[i] = (byte)((sha1[i * 2] - 0x30) * 0x10);
                            else if(sha1[i * 2] >= 0x41 &&
                                    sha1[i * 2] <= 0x46)
                                sha1Bytes[i] = (byte)((sha1[i * 2] - 0x37) * 0x10);
                            else if(sha1[i * 2] >= 0x61 &&
                                    sha1[i * 2] <= 0x66)
                                sha1Bytes[i] = (byte)((sha1[i * 2] - 0x57) * 0x10);

                            if(sha1[(i * 2) + 1] >= 0x30 &&
                               sha1[(i * 2) + 1] <= 0x39)
                                sha1Bytes[i] += (byte)(sha1[(i * 2) + 1] - 0x30);
                            else if(sha1[(i * 2) + 1] >= 0x41 &&
                                    sha1[(i * 2) + 1] <= 0x46)
                                sha1Bytes[i] += (byte)(sha1[(i * 2) + 1] - 0x37);
                            else if(sha1[(i * 2) + 1] >= 0x61 &&
                                    sha1[(i * 2) + 1] <= 0x66)
                                sha1Bytes[i] += (byte)(sha1[(i * 2) + 1] - 0x57);
                        }

                        string sha1B32 = Base32.ToBase32String(sha1Bytes);

                        sha1Path = Path.Combine(Settings.Settings.Current.RepositoryPath, "aaru", "sha1",
                                                sha1B32[0].ToString(), sha1B32[1].ToString(), sha1B32[2].ToString(),
                                                sha1B32[3].ToString(), sha1B32[4].ToString(), sha1B32 + ".aif");
                    }

                    if(media.Md5 != null)
                    {
                        byte[] md5Bytes = new byte[16];
                        string md5      = media.Md5;

                        for(int i = 0; i < 16; i++)
                        {
                            if(md5[i * 2] >= 0x30 &&
                               md5[i * 2] <= 0x39)
                                md5Bytes[i] = (byte)((md5[i * 2] - 0x30) * 0x10);
                            else if(md5[i * 2] >= 0x41 &&
                                    md5[i * 2] <= 0x46)
                                md5Bytes[i] = (byte)((md5[i * 2] - 0x37) * 0x10);
                            else if(md5[i * 2] >= 0x61 &&
                                    md5[i * 2] <= 0x66)
                                md5Bytes[i] = (byte)((md5[i * 2] - 0x57) * 0x10);

                            if(md5[(i * 2) + 1] >= 0x30 &&
                               md5[(i * 2) + 1] <= 0x39)
                                md5Bytes[i] += (byte)(md5[(i * 2) + 1] - 0x30);
                            else if(md5[(i * 2) + 1] >= 0x41 &&
                                    md5[(i * 2) + 1] <= 0x46)
                                md5Bytes[i] += (byte)(md5[(i * 2) + 1] - 0x37);
                            else if(md5[(i * 2) + 1] >= 0x61 &&
                                    md5[(i * 2) + 1] <= 0x66)
                                md5Bytes[i] += (byte)(md5[(i * 2) + 1] - 0x57);
                        }

                        string md5B32 = Base32.ToBase32String(md5Bytes);

                        md5Path = Path.Combine(Settings.Settings.Current.RepositoryPath, "aaru", "md5",
                                               md5B32[0].ToString(), md5B32[1].ToString(), md5B32[2].ToString(),
                                               md5B32[3].ToString(), md5B32[4].ToString(), md5B32 + ".aif");
                    }

                    if(File.Exists(sha256Path))
                        repoPath = sha256Path;
                    else if(File.Exists(sha1Path))
                        repoPath = sha1Path;
                    else if(File.Exists(md5Path))
                        repoPath = md5Path;

                    if(repoPath == null)
                        throw new ArgumentException(string.Format(Localization.CannotFindHashInRepository,
                                                                  media.Sha256 ?? media.Sha1 ?? media.Md5));

                    var inFs  = new FileStream(repoPath, FileMode.Open, FileAccess.Read);
                    var outFs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);

                    SetMessage3?.Invoke(this, new MessageEventArgs
                    {
                        Message = string.Format(Localization.Copying, Path.GetFileName(outputPath))
                    });

                    SetProgress3Bounds?.Invoke(this, new ProgressBoundsEventArgs
                    {
                        Minimum = 0,
                        Maximum = inFs.Length
                    });

                    byte[] buffer = new byte[BUFFER_SIZE];

                    while(inFs.Position + BUFFER_SIZE <= inFs.Length)
                    {
                        SetProgress3?.Invoke(this, new ProgressEventArgs
                        {
                            Value = inFs.Position
                        });

                        inFs.Read(buffer, 0, buffer.Length);
                        outFs.Write(buffer, 0, buffer.Length);
                    }

                    buffer = new byte[inFs.Length - inFs.Position];

                    SetProgress3?.Invoke(this, new ProgressEventArgs
                    {
                        Value = inFs.Position
                    });

                    inFs.Read(buffer, 0, buffer.Length);
                    outFs.Write(buffer, 0, buffer.Length);

                    inFs.Close();
                    outFs.Close();

                    mediaPosition++;
                }
            }

            Dictionary<string, DiskByMachine> disksByMachine = ctx.DisksByMachines.
                                                                   Where(f => f.Machine.Id == machine.Id &&
                                                                              f.Disk.IsInRepo).
                                                                   ToDictionary(f => f.Name);

            if(disksByMachine.Count > 0)
            {
                SetProgress2Bounds?.Invoke(this, new ProgressBoundsEventArgs
                {
                    Minimum = 0,
                    Maximum = disksByMachine.Count
                });

                if(machineName.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                    machineName = machineName.Substring(0, machineName.Length - 4);

                string machinePath = Path.Combine(_outPath, machineName);

                if(!Directory.Exists(machinePath))
                    Directory.CreateDirectory(machinePath);

                long diskPosition = 0;

                foreach(KeyValuePair<string, DiskByMachine> diskByMachine in disksByMachine)
                {
                    string outputPath = Path.Combine(machinePath, diskByMachine.Key);

                    if(!outputPath.EndsWith(".chd", StringComparison.InvariantCultureIgnoreCase))
                        outputPath += ".chd";

                    SetProgress2?.Invoke(this, new ProgressEventArgs
                    {
                        Value = diskPosition
                    });

                    string repoPath = null;
                    string md5Path  = null;
                    string sha1Path = null;

                    DbDisk disk = diskByMachine.Value.Disk;

                    if(disk.Sha1 != null)
                    {
                        byte[] sha1Bytes = new byte[20];
                        string sha1      = disk.Sha1;

                        for(int i = 0; i < 20; i++)
                        {
                            if(sha1[i * 2] >= 0x30 &&
                               sha1[i * 2] <= 0x39)
                                sha1Bytes[i] = (byte)((sha1[i * 2] - 0x30) * 0x10);
                            else if(sha1[i * 2] >= 0x41 &&
                                    sha1[i * 2] <= 0x46)
                                sha1Bytes[i] = (byte)((sha1[i * 2] - 0x37) * 0x10);
                            else if(sha1[i * 2] >= 0x61 &&
                                    sha1[i * 2] <= 0x66)
                                sha1Bytes[i] = (byte)((sha1[i * 2] - 0x57) * 0x10);

                            if(sha1[(i * 2) + 1] >= 0x30 &&
                               sha1[(i * 2) + 1] <= 0x39)
                                sha1Bytes[i] += (byte)(sha1[(i * 2) + 1] - 0x30);
                            else if(sha1[(i * 2) + 1] >= 0x41 &&
                                    sha1[(i * 2) + 1] <= 0x46)
                                sha1Bytes[i] += (byte)(sha1[(i * 2) + 1] - 0x37);
                            else if(sha1[(i * 2) + 1] >= 0x61 &&
                                    sha1[(i * 2) + 1] <= 0x66)
                                sha1Bytes[i] += (byte)(sha1[(i * 2) + 1] - 0x57);
                        }

                        string sha1B32 = Base32.ToBase32String(sha1Bytes);

                        sha1Path = Path.Combine(Settings.Settings.Current.RepositoryPath, "chd", "sha1",
                                                sha1B32[0].ToString(), sha1B32[1].ToString(), sha1B32[2].ToString(),
                                                sha1B32[3].ToString(), sha1B32[4].ToString(), sha1B32 + ".chd");
                    }

                    if(disk.Md5 != null)
                    {
                        byte[] md5Bytes = new byte[16];
                        string md5      = disk.Md5;

                        for(int i = 0; i < 16; i++)
                        {
                            if(md5[i * 2] >= 0x30 &&
                               md5[i * 2] <= 0x39)
                                md5Bytes[i] = (byte)((md5[i * 2] - 0x30) * 0x10);
                            else if(md5[i * 2] >= 0x41 &&
                                    md5[i * 2] <= 0x46)
                                md5Bytes[i] = (byte)((md5[i * 2] - 0x37) * 0x10);
                            else if(md5[i * 2] >= 0x61 &&
                                    md5[i * 2] <= 0x66)
                                md5Bytes[i] = (byte)((md5[i * 2] - 0x57) * 0x10);

                            if(md5[(i * 2) + 1] >= 0x30 &&
                               md5[(i * 2) + 1] <= 0x39)
                                md5Bytes[i] += (byte)(md5[(i * 2) + 1] - 0x30);
                            else if(md5[(i * 2) + 1] >= 0x41 &&
                                    md5[(i * 2) + 1] <= 0x46)
                                md5Bytes[i] += (byte)(md5[(i * 2) + 1] - 0x37);
                            else if(md5[(i * 2) + 1] >= 0x61 &&
                                    md5[(i * 2) + 1] <= 0x66)
                                md5Bytes[i] += (byte)(md5[(i * 2) + 1] - 0x57);
                        }

                        string md5B32 = Base32.ToBase32String(md5Bytes);

                        md5Path = Path.Combine(Settings.Settings.Current.RepositoryPath, "chd", "md5",
                                               md5B32[0].ToString(), md5B32[1].ToString(), md5B32[2].ToString(),
                                               md5B32[3].ToString(), md5B32[4].ToString(), md5B32 + ".chd");
                    }

                    if(File.Exists(sha1Path))
                        repoPath = sha1Path;
                    else if(File.Exists(md5Path))
                        repoPath = md5Path;

                    if(repoPath == null)
                        throw new ArgumentException(string.Format(Localization.CannotFindHashInRepository,
                                                                  disk.Sha1 ?? disk.Md5));

                    var inFs  = new FileStream(repoPath, FileMode.Open, FileAccess.Read);
                    var outFs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);

                    SetMessage3?.Invoke(this, new MessageEventArgs
                    {
                        Message = string.Format(Localization.Copying, Path.GetFileName(outputPath))
                    });

                    SetProgress3Bounds?.Invoke(this, new ProgressBoundsEventArgs
                    {
                        Minimum = 0,
                        Maximum = inFs.Length
                    });

                    byte[] buffer = new byte[BUFFER_SIZE];

                    while(inFs.Position + BUFFER_SIZE <= inFs.Length)
                    {
                        SetProgress3?.Invoke(this, new ProgressEventArgs
                        {
                            Value = inFs.Position
                        });

                        inFs.Read(buffer, 0, buffer.Length);
                        outFs.Write(buffer, 0, buffer.Length);
                    }

                    buffer = new byte[inFs.Length - inFs.Position];

                    SetProgress3?.Invoke(this, new ProgressEventArgs
                    {
                        Value = inFs.Position
                    });

                    inFs.Read(buffer, 0, buffer.Length);
                    outFs.Write(buffer, 0, buffer.Length);

                    inFs.Close();
                    outFs.Close();

                    diskPosition++;
                }
            }

            _filesByMachine = ctx.FilesByMachines.Where(f => f.Machine.Id == machine.Id && f.File.IsInRepo).
                                  ToDictionary(f => f.Name);

            if(_filesByMachine.Count == 0)
            {
                _machinePosition++;
                Task.Run(CompressNextMachine);

                return;
            }

            SetProgress2Bounds?.Invoke(this, new ProgressBoundsEventArgs
            {
                Minimum = 0,
                Maximum = _filesByMachine.Count
            });

            if(!machineName.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                machineName += ".zip";

            var zf = new ZipFile(Path.Combine(_outPath, machineName), Encoding.UTF8)
            {
                CompressionLevel                   = CompressionLevel.BestCompression,
                CompressionMethod                  = CompressionMethod.Deflate,
                EmitTimesInUnixFormatWhenSaving    = true,
                EmitTimesInWindowsFormatWhenSaving = true,
                UseZip64WhenSaving                 = Zip64Option.AsNecessary,
                SortEntriesBeforeSaving            = true
            };

            zf.SaveProgress += Zf_SaveProgress;

            foreach(KeyValuePair<string, FileByMachine> fileByMachine in _filesByMachine)
            {
                // Is a directory
                if((fileByMachine.Key.EndsWith("/", StringComparison.InvariantCultureIgnoreCase) ||
                    fileByMachine.Key.EndsWith("\\", StringComparison.InvariantCultureIgnoreCase)) &&
                   fileByMachine.Value.File.Size == 0)
                {
                    ZipEntry zd = zf.AddDirectoryByName(fileByMachine.Key.Replace('/', '\\'));
                    zd.Attributes   = FileAttributes.Normal;
                    zd.CreationTime = DateTime.UtcNow;
                    zd.AccessedTime = DateTime.UtcNow;
                    zd.LastModified = DateTime.UtcNow;
                    zd.ModifiedTime = DateTime.UtcNow;

                    continue;
                }

                ZipEntry zi = zf.AddEntry(fileByMachine.Key, Zf_HandleOpen, Zf_HandleClose);
                zi.Attributes   = FileAttributes.Normal;
                zi.CreationTime = DateTime.UtcNow;
                zi.AccessedTime = DateTime.UtcNow;
                zi.LastModified = DateTime.UtcNow;
                zi.ModifiedTime = DateTime.UtcNow;
            }

            zf.Save();
        }

        Stream Zf_HandleOpen(string entryName)
        {
            if(!_filesByMachine.TryGetValue(entryName, out FileByMachine fileByMachine))
                if(!_filesByMachine.TryGetValue(entryName.Replace('/', '\\'), out fileByMachine))
                    throw new ArgumentException(Localization.CannotFindZipEntryInDictionary);

            DbFile file = fileByMachine.File;

            // Special case for empty file, as it seems to crash when SharpCompress tries to unLZMA it.
            if(file.Size == 0)
                return new MemoryStream();

            byte[] sha384Bytes = new byte[48];
            string sha384      = file.Sha384;

            for(int i = 0; i < 48; i++)
            {
                if(sha384[i * 2] >= 0x30 &&
                   sha384[i * 2] <= 0x39)
                    sha384Bytes[i] = (byte)((sha384[i * 2] - 0x30) * 0x10);
                else if(sha384[i * 2] >= 0x41 &&
                        sha384[i * 2] <= 0x46)
                    sha384Bytes[i] = (byte)((sha384[i * 2] - 0x37) * 0x10);
                else if(sha384[i * 2] >= 0x61 &&
                        sha384[i * 2] <= 0x66)
                    sha384Bytes[i] = (byte)((sha384[i * 2] - 0x57) * 0x10);

                if(sha384[(i * 2) + 1] >= 0x30 &&
                   sha384[(i * 2) + 1] <= 0x39)
                    sha384Bytes[i] += (byte)(sha384[(i * 2) + 1] - 0x30);
                else if(sha384[(i * 2) + 1] >= 0x41 &&
                        sha384[(i * 2) + 1] <= 0x46)
                    sha384Bytes[i] += (byte)(sha384[(i * 2) + 1] - 0x37);
                else if(sha384[(i * 2) + 1] >= 0x61 &&
                        sha384[(i * 2) + 1] <= 0x66)
                    sha384Bytes[i] += (byte)(sha384[(i * 2) + 1] - 0x57);
            }

            string sha384B32 = Base32.ToBase32String(sha384Bytes);

            string repoPath = Path.Combine(Settings.Settings.Current.RepositoryPath, "files", sha384B32[0].ToString(),
                                           sha384B32[1].ToString(), sha384B32[2].ToString(), sha384B32[3].ToString(),
                                           sha384B32[4].ToString(), sha384B32 + ".lz");

            if(!File.Exists(repoPath))
                throw new ArgumentException(string.Format(Localization.CannotFindHashInRepository, file.Sha256));

            var inFs = new FileStream(repoPath, FileMode.Open, FileAccess.Read);

            return new StreamWithLength(new LZipStream(inFs, CompressionMode.Decompress), (long)file.Size);
        }

        void Zf_HandleClose(string entryName, Stream stream) => stream.Close();

        void Zf_SaveProgress(object sender, SaveProgressEventArgs e)
        {
            if(e.CurrentEntry          != null &&
               e.CurrentEntry.FileName != _zipCurrentEntryName)
            {
                _zipCurrentEntryName = e.CurrentEntry.FileName;
                _filePosition++;

                SetProgress2?.Invoke(this, new ProgressEventArgs
                {
                    Value = _filePosition
                });

                if(!_filesByMachine.TryGetValue(e.CurrentEntry.FileName, out FileByMachine fileByMachine))
                    if(!_filesByMachine.TryGetValue(e.CurrentEntry.FileName.Replace('/', '\\'), out fileByMachine))
                        throw new ArgumentException(Localization.CannotFindZipEntryInDictionary);

                DbFile currentFile = fileByMachine.File;

                SetMessage3?.Invoke(this, new MessageEventArgs
                {
                    Message = string.Format(Localization.Compressing, e.CurrentEntry.FileName)
                });

                SetProgress3Bounds?.Invoke(this, new ProgressBoundsEventArgs
                {
                    Minimum = 0,
                    Maximum = currentFile.Size
                });
            }

            SetProgress3?.Invoke(this, new ProgressEventArgs
            {
                Value = e.BytesTransferred
            });

            switch(e.EventType)
            {
                case ZipProgressEventType.Error_Saving:
                #if DEBUG
                    throw new Exception();
                #endif

                    break;
                case ZipProgressEventType.Saving_Completed:
                    _machinePosition++;
                    CompressNextMachine();

                    break;
            }
        }
    }
}