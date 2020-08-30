/*
 * Copyright © 2020 Oleg Ignat
 * This code is available under Creative Commons Attribution license
 * https://creativecommons.org/licenses/by/2.0/
 */

using System;
using System.Text;
using RomRepoMgr.Core.Resources;

namespace RomRepoMgr.Core
{
    /// <summary>Class used for conversion between byte array and Base32 notation</summary>
    public sealed class Base32
    {
        /// <summary>Size of the regular byte in bits</summary>
        const int _inByteSize = 8;

        /// <summary>Size of converted byte in bits</summary>
        const int _outByteSize = 5;

        /// <summary>Alphabet</summary>
        const string _base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        /// <summary>Convert byte array to Base32 format</summary>
        /// <param name="bytes">An array of bytes to convert to Base32 format</param>
        /// <returns>Returns a string representing byte array</returns>
        public static string ToBase32String(byte[] bytes)
        {
            // Check if byte array is null
            if(bytes == null)
                return null;

            // Check if empty

            if(bytes.Length == 0)
                return string.Empty;

            // Prepare container for the final value
            var builder = new StringBuilder((bytes.Length * _inByteSize) / _outByteSize);

            // Position in the input buffer
            int bytesPosition = 0;

            // Offset inside a single byte that <bytesPosition> points to (from left to right)
            // 0 - highest bit, 7 - lowest bit
            int bytesSubPosition = 0;

            // Byte to look up in the dictionary
            byte outputBase32Byte = 0;

            // The number of bits filled in the current output byte
            int outputBase32BytePosition = 0;

            // Iterate through input buffer until we reach past the end of it
            while(bytesPosition < bytes.Length)
            {
                // Calculate the number of bits we can extract out of current input byte to fill missing bits in the output byte
                int bitsAvailableInByte =
                    Math.Min(_inByteSize - bytesSubPosition, _outByteSize - outputBase32BytePosition);

                // Make space in the output byte
                outputBase32Byte <<= bitsAvailableInByte;

                // Extract the part of the input byte and move it to the output byte
                outputBase32Byte |=
                    (byte)(bytes[bytesPosition] >> (_inByteSize - (bytesSubPosition + bitsAvailableInByte)));

                // Update current sub-byte position
                bytesSubPosition += bitsAvailableInByte;

                // Check overflow
                if(bytesSubPosition >= _inByteSize)
                {
                    // Move to the next byte
                    bytesPosition++;
                    bytesSubPosition = 0;
                }

                // Update current base32 byte completion
                outputBase32BytePosition += bitsAvailableInByte;

                // Check overflow or end of input array
                if(outputBase32BytePosition < _outByteSize)
                    continue;

                // Drop the overflow bits
                outputBase32Byte &= 0x1F; // 0x1F = 00011111 in binary

                // Add current Base32 byte and convert it to character
                builder.Append(_base32Alphabet[outputBase32Byte]);

                // Move to the next byte
                outputBase32BytePosition = 0;
            }

            // Check if we have a remainder
            if(outputBase32BytePosition <= 0)
                return builder.ToString();

            // Move to the right bits
            outputBase32Byte <<= _outByteSize - outputBase32BytePosition;

            // Drop the overflow bits
            outputBase32Byte &= 0x1F; // 0x1F = 00011111 in binary

            // Add current Base32 byte and convert it to character
            builder.Append(_base32Alphabet[outputBase32Byte]);

            return builder.ToString();
        }

        /// <summary>Convert base32 string to array of bytes</summary>
        /// <param name="base32String">Base32 string to convert</param>
        /// <returns>Returns a byte array converted from the string</returns>
        public static byte[] FromBase32String(string base32String)
        {
            // Check if string is null
            if(base32String == null)
                return null;

            // Check if empty
            if(base32String == string.Empty)
                return new byte[0];

            // Convert to upper-case
            string base32StringUpperCase = base32String.ToUpperInvariant();

            // Prepare output byte array
            byte[] outputBytes = new byte[(base32StringUpperCase.Length * _outByteSize) / _inByteSize];

            // Check the size
            if(outputBytes.Length == 0)
                throw new ArgumentException(Localization.Base32_Not_enought_data);

            // Position in the string
            int base32Position = 0;

            // Offset inside the character in the string
            int base32SubPosition = 0;

            // Position within outputBytes array
            int outputBytePosition = 0;

            // The number of bits filled in the current output byte
            int outputByteSubPosition = 0;

            // Normally we would iterate on the input array but in this case we actually iterate on the output array
            // We do it because output array doesn't have overflow bits, while input does and it will cause output array overflow if we don''t stop in time
            while(outputBytePosition < outputBytes.Length)
            {
                // Look up current character in the dictionary to convert it to byte
                int currentBase32Byte = _base32Alphabet.IndexOf(base32StringUpperCase[base32Position]);

                // Check if found
                if(currentBase32Byte < 0)
                    throw new ArgumentException(string.Format(Localization.Base32_Invalid_format,
                                                              base32String[base32Position]));

                // Calculate the number of bits we can extract out of current input character to fill missing bits in the output byte
                int bitsAvailableInByte =
                    Math.Min(_outByteSize - base32SubPosition, _inByteSize - outputByteSubPosition);

                // Make space in the output byte
                outputBytes[outputBytePosition] <<= bitsAvailableInByte;

                // Extract the part of the input character and move it to the output byte
                outputBytes[outputBytePosition] |=
                    (byte)(currentBase32Byte >> (_outByteSize - (base32SubPosition + bitsAvailableInByte)));

                // Update current sub-byte position
                outputByteSubPosition += bitsAvailableInByte;

                // Check overflow
                if(outputByteSubPosition >= _inByteSize)
                {
                    // Move to the next byte
                    outputBytePosition++;
                    outputByteSubPosition = 0;
                }

                // Update current base32 byte completion
                base32SubPosition += bitsAvailableInByte;

                // Check overflow or end of input array
                if(base32SubPosition < _outByteSize)
                    continue;

                // Move to the next character
                base32Position++;
                base32SubPosition = 0;
            }

            return outputBytes;
        }
    }
}