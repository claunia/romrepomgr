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

            RomSet romSet = Context.Singleton.RomSets.Find(_romSetId);

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

            _machines = Context.Singleton.Machines.Where(m => m.RomSet.Id == _romSetId).ToArray();

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

            _filesByMachine = Context.Singleton.FilesByMachines.
                                      Where(f => f.Machine.Id == machine.Id && f.File.IsInRepo).
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

            string machineName = machine.Name;

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