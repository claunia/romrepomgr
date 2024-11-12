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
// Copyright © 2017-2024 Natalia Portillo
*******************************************************************************/

using System;
using System.Diagnostics;
using System.IO;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Resources;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;
using ErrorEventArgs = RomRepoMgr.Core.EventArgs.ErrorEventArgs;

namespace RomRepoMgr.Core.Workers;

public sealed class Compression
{
    const long BUFFER_SIZE = 131072;

    public event EventHandler<ProgressBoundsEventArgs> SetProgressBounds;
    public event EventHandler<ProgressEventArgs>       SetProgress;
    public event EventHandler<MessageEventArgs>        FinishedWithText;
    public event EventHandler<ErrorEventArgs>          FailedWithText;

    public void CompressFile(string source, string destination)
    {
        var    inFs    = new FileStream(source,      FileMode.Open,      FileAccess.Read);
        var    outFs   = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
        Stream zStream = new LZipStream(outFs, CompressionMode.Compress);

        var buffer = new byte[BUFFER_SIZE];

        SetProgressBounds?.Invoke(this,
                                  new ProgressBoundsEventArgs
                                  {
                                      Minimum = 0,
                                      Maximum = inFs.Length
                                  });

        while(inFs.Position + BUFFER_SIZE <= inFs.Length)
        {
            SetProgress?.Invoke(this,
                                new ProgressEventArgs
                                {
                                    Value = inFs.Position
                                });

            inFs.EnsureRead(buffer, 0, buffer.Length);
            zStream.Write(buffer, 0, buffer.Length);
        }

        buffer = new byte[inFs.Length - inFs.Position];

        SetProgressBounds?.Invoke(this,
                                  new ProgressBoundsEventArgs
                                  {
                                      Minimum = 0,
                                      Maximum = inFs.Length
                                  });

        inFs.EnsureRead(buffer, 0, buffer.Length);
        zStream.Write(buffer, 0, buffer.Length);

        inFs.Close();
        zStream.Close();
        outFs.Dispose();
    }

    public void DecompressFile(string source, string destination)
    {
        var    inFs    = new FileStream(source,      FileMode.Open,   FileAccess.Read);
        var    outFs   = new FileStream(destination, FileMode.Create, FileAccess.Write);
        Stream zStream = new LZipStream(inFs, CompressionMode.Decompress);

        zStream.CopyTo(outFs);

        outFs.Close();
        zStream.Close();
        inFs.Close();
    }

    public bool CheckUnAr(string unArPath)
    {
        if(string.IsNullOrWhiteSpace(unArPath))
        {
            FailedWithText?.Invoke(this,
                                   new ErrorEventArgs
                                   {
                                       Message = Localization.UnArPathNotSet
                                   });

            return false;
        }

        string unarFolder   = Path.GetDirectoryName(unArPath);
        string extension    = Path.GetExtension(unArPath);
        string unarfilename = Path.GetFileNameWithoutExtension(unArPath);
        string lsarfilename = unarfilename.Replace("unar", "lsar");
        string unarPath     = Path.Combine(unarFolder, unarfilename + extension);
        string lsarPath     = Path.Combine(unarFolder, lsarfilename + extension);

        if(!File.Exists(unarPath))
        {
            FailedWithText?.Invoke(this,
                                   new ErrorEventArgs
                                   {
                                       Message = string.Format(Localization.CannotFindUnArAtPath, unarPath)
                                   });

            return false;
        }

        if(!File.Exists(lsarPath))
        {
            FailedWithText?.Invoke(this,
                                   new ErrorEventArgs
                                   {
                                       Message = Localization.CannotFindLsAr
                                   });

            return false;
        }

        string unarOut, lsarOut;

        try
        {
            var unarProcess = new Process
            {
                StartInfo =
                {
                    FileName               = unarPath,
                    CreateNoWindow         = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false
                }
            };

            unarProcess.Start();
            unarProcess.WaitForExit();
            unarOut = unarProcess.StandardOutput.ReadToEnd();
        }
        catch
        {
            FailedWithText?.Invoke(this,
                                   new ErrorEventArgs
                                   {
                                       Message = Localization.CannotRunUnAr
                                   });

            return false;
        }

        try
        {
            var lsarProcess = new Process
            {
                StartInfo =
                {
                    FileName               = lsarPath,
                    CreateNoWindow         = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false
                }
            };

            lsarProcess.Start();
            lsarProcess.WaitForExit();
            lsarOut = lsarProcess.StandardOutput.ReadToEnd();
        }
        catch
        {
            FailedWithText?.Invoke(this,
                                   new ErrorEventArgs
                                   {
                                       Message = Localization.CannotRunLsAr
                                   });

            return false;
        }

        if(!unarOut.StartsWith("unar ", StringComparison.CurrentCulture))
        {
            FailedWithText?.Invoke(this,
                                   new ErrorEventArgs
                                   {
                                       Message = Localization.NotCorrectUnAr
                                   });

            return false;
        }

        if(!lsarOut.StartsWith("lsar ", StringComparison.CurrentCulture))
        {
            FailedWithText?.Invoke(this,
                                   new ErrorEventArgs
                                   {
                                       Message = Localization.NotCorrectLsAr
                                   });

            return false;
        }

        var versionProcess = new Process
        {
            StartInfo =
            {
                FileName               = unarPath,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                Arguments              = "-v"
            }
        };

        versionProcess.Start();
        versionProcess.WaitForExit();

        FinishedWithText?.Invoke(this,
                                 new MessageEventArgs
                                 {
                                     Message = versionProcess.StandardOutput.ReadToEnd().TrimEnd('\n')
                                 });

        return true;
    }
}