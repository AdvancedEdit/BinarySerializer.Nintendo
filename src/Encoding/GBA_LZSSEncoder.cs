﻿using System;
using System.IO;

namespace BinarySerializer.GBA
{
    // Implemented from: https://github.com/Barubary/dsdecmp/blob/master/CSharp/DSDecmp/Formats/Nitro/LZ10.cs

    /// <summary>
    /// Compresses/decompresses data using LZSS
    /// </summary>
    public class GBA_LZSSEncoder : IStreamEncoder
    {
        public string Name => "GBA_LZSS";

        /// <summary>
        /// Decodes the data and returns it in a stream
        /// </summary>
        /// <param name="s">The encoded stream</param>
        /// <returns>The stream with the decoded data</returns>
        public Stream DecodeStream(Stream s) 
        {
            var decompressedStream = new MemoryStream();

            Reader reader = new Reader(s, isLittleEndian: true); // No using, because we don't want to close the stream
            byte magic = reader.ReadByte();

            if (magic != 0x10)
                throw new InvalidDataException("The data is not LZSS compressed!");

            var decompressedSizeValue = reader.ReadBytes(3);
            Array.Resize(ref decompressedSizeValue, 4);
            var decompressedSize = BitConverter.ToUInt32(decompressedSizeValue, 0);

            // the maximum 'DISP-1' is 0xFFF.
            const int bufferLength = 0x1000;
            byte[] buffer = new byte[bufferLength];
            int bufferOffset = 0;

            int currentOutSize = 0;
            byte flags = 0, mask = 1;
            while (currentOutSize < decompressedSize) 
            {
                // (throws when requested new flags byte is not available)
                #region Update the mask. If all flag bits have been read, get a new set.
                // the current mask is the mask used in the previous run. So if it masks the
                // last flag bit, get a new flags byte.
                if (mask == 1) {
                    flags = reader.ReadByte();

                    mask = 0x80;
                } else {
                    mask >>= 1;
                }
                #endregion

                // bit = 1 <=> compressed.
                if ((flags & mask) > 0) {
                    // (throws when < 2 bytes are available)
                    #region Get length and displacement('disp') values from next 2 bytes
                    // there are < 2 bytes available when the end is at most 1 byte away
                    //if (readBytes + 1 >= inLength)
                    //{
                    //    // make sure the stream is at the end
                    //    if (readBytes < inLength)
                    //    {
                    //        instream.ReadByte(); 
                    //        readBytes++;
                    //    }
                    //    throw new NotEnoughDataException(currentOutSize, decompressedSize);
                    //}

                    int byte1 = reader.ReadByte();
                    int byte2 = reader.ReadByte();

                    // the number of bytes to copy
                    int length = byte1 >> 4;
                    length += 3;

                    // from where the bytes should be copied (relatively)
                    int disp = ((byte1 & 0x0F) << 8) | byte2;
                    disp += 1;

                    if (disp > currentOutSize)
                        throw new InvalidDataException($"Cannot go back more than already written. DISP = {disp}, written bytes = {currentOutSize}");
                    #endregion

                    int bufIdx = bufferOffset + bufferLength - disp;
                    for (int i = 0; i < length; i++) {
                        byte next = buffer[bufIdx % bufferLength];
                        bufIdx++;
                        decompressedStream.WriteByte(next);
                        buffer[bufferOffset] = next;
                        bufferOffset = (bufferOffset + 1) % bufferLength;
                    }
                    currentOutSize += length;
                } 
                else 
                {
                    byte next = reader.ReadByte();

                    currentOutSize++;
                    decompressedStream.WriteByte(next);
                    buffer[bufferOffset] = next;
                    bufferOffset = (bufferOffset + 1) % bufferLength;
                }
            }

            // Set position back to 0
            decompressedStream.Position = 0;

            // Return the compressed data stream
            return decompressedStream;
        }

        public Stream EncodeStream(Stream s) => throw new NotImplementedException();
    }
}