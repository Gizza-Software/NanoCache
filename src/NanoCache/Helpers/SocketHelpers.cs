#define CRC32

using NanoCache.Security;
using System;
using System.Collections.Generic;

namespace NanoCache.Helpers
{
    internal static class SocketHelpers
    {
        public static void CacheAndConsume(byte[] bytes, long connectionId, List<byte> bufferList, Action<byte[], long> ConsumeAction)
        {
            try
            {
                // Gelen verileri buffer'a ekle ve bu halini bufferArray olarak al. Sonrasında bufferı temizle
                bufferList.AddRange(bytes);
                var bufferArray = bufferList.ToArray();
                // buffer.Clear();

                /*
                 * Minimum paket uzunluğu 8 byte
                 * SYNC     : 2 Bytes
                 * Length   : 2 Bytes
                 * Data Type: 1 Byte
                 * Content  : 1 Byte (Minimum)
                 * CRC16    : 2 Bytes
                 * CRC32    : 4 Bytes
                 */
#if CRC16
                var crcLength = 2;
#elif CRC32
                var crcLength = 4;
#endif
                if (bufferArray.Length >= crcLength + 5 + 1)
                {
                    var indexOf = IndexOf<byte>(bufferArray, 0xF1, 0xF2);
                    if (indexOf == -1) bufferList.Clear();

                    // if (bufferArray[0] == 0xF1 && bufferArray[1] == 0xF2) // SYNC Bytes
                    else if (indexOf == 0) // SYNC Bytes
                    {
                        var lenghtValue = BitConverter.ToUInt16(bufferArray, 2);

                        // Paket yeterki kadar büyük mü? lenghtValue = Data Type (1) + Content (X)
                        // Paketin gereğinden fazla büyük olması sorun değil.
                        // Pakcet Length CRC bytelarını kapsamıyor.
                        var packetLength = 4 + crcLength + lenghtValue;
                        if (bufferArray.Length >= packetLength)
                        {
                            // CRC-Body'i ayarlayalım
                            var crcBody = new byte[lenghtValue];
                            Array.Copy(bufferArray, 4, crcBody, 0, lenghtValue);

                            // Check CRC & Consume
                            try
                            {
#if CRC16
                                if (CRC16.CheckCRC16(crcBody, bufferArray[lenghtValue + 4], bufferArray[lenghtValue + 5]))
                                    ConsumeAction(crcBody, connectionId);
#endif
#if CRC32
                                var crc32Bytes = new byte[] {
                                    bufferArray[lenghtValue + 4],
                                    bufferArray[lenghtValue + 5],
                                    bufferArray[lenghtValue + 6],
                                    bufferArray[lenghtValue + 7]
                                };
                                var crc32Value = BitConverter.ToUInt32(crc32Bytes, 0);
                                if (CRC32.CheckCRC32(crcBody, crc32Value))
                                    ConsumeAction(crcBody, connectionId);
#endif
                            }
                            catch { }

                            // Consume edilen veriyi buffer'dan at
                            var currentLenght = bufferList.Count;
                            bufferList.RemoveRange(0, packetLength);

                            // Arta kalanları veri için bu methodu yeniden çalıştır
                            if (currentLenght > packetLength)
                                CacheAndConsume(Array.Empty<byte>(), connectionId, bufferList, ConsumeAction);
                        }
                    }

                    else
                    {
                        bufferList.RemoveRange(0, indexOf);
                        CacheAndConsume(Array.Empty<byte>(), connectionId, bufferList, ConsumeAction);
                    }
                }
            }
            catch { }
        }

        static int IndexOf<T>(T[] source, T search01, T search02)
        {
            var index = -1;
            for (var i = 0; i < source.Length - 1; i++)
            {
                if (source[i].Equals(search01) && source[i + 1].Equals(search02))
                {
                    index = i;
                    break;
                }
            }
            return index;
        }

        public static int IndexOf<T>(List<T> source, T search01, T search02)
        {
            var index = -1;
            for (var i = 0; i < source.Count - 1; i++)
            {
                if (source[i].Equals(search01) && source[i + 1].Equals(search02))
                {
                    index = i;
                    break;
                }
            }
            return index;
        }
    }
}
