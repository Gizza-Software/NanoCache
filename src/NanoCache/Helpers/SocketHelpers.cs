#define CRC32

using NanoCache.Security;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NanoCache.Helpers
{
    internal static class SocketHelpers
    {
        internal static void CacheAndConsume(byte[] bytes, long connectionId, List<byte> buffer, Action<byte[], long> ConsumeAction)
        {
            try
            {
                // Gelen verileri buffer'a ekle ve bu halini bufferArray olarak al. Sonrasında bufferı temizle
                buffer.AddRange(bytes.ToList());
                var bufferArray = buffer.ToArray();
                buffer.Clear();

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
                    if (bufferArray[0] == 0xF1 && bufferArray[1] == 0xF2) // SYNC Bytes
                    {
                        var lengthBytes = new byte[] { bufferArray[2], bufferArray[3] };
                        var lenghtValue = BitConverter.ToInt16(lengthBytes, 0);

                        // Paket yeterki kadar büyük mü? lenghtValue = Data Type (1) + Content (X)
                        // Paketin gereğinden fazla büyük olması sorun değil.
                        // Pakcet Length CRC bytelarını kapsamıyor.
                        var packetLength = 4 + crcLength + lenghtValue;
                        if (bufferArray.Length >= packetLength)
                        {
                            var crcBody = new byte[lenghtValue];
                            Array.Copy(bufferArray, 4, crcBody, 0, lenghtValue);
                            try
                            {
#if CRC16
                                if (CRC16.CheckCRC16(crcBody, bufferArray[lenghtValue + 4], bufferArray[lenghtValue + 5]))
                                {
                                    ConsumeAction(crcBody, connectionId);
                                    return;
                                }
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
                                {
                                    ConsumeAction(crcBody, connectionId);
                                    return;
                                }
#endif
                            }
                            catch { }

                            // Arta kalanları paketleri bu methoda yeniden gönder
                            if (bufferArray.Length > packetLength)
                            {
                                var remainLength = bufferArray.Length - packetLength;
                                var remainBytes = new byte[remainLength];
                                Array.Copy(bufferArray, packetLength, remainBytes, 0, remainLength);

                                CacheAndConsume(remainBytes, connectionId, buffer, ConsumeAction);
                                return;
                            }
                        }
                    }
                }

                // Buraya gelebilmenin tek şartı gelen paketin kısmi veri içermesi.
                // Eldeki verileri Buffer'a atıp sonraki paketleri bekliyoruz.
                buffer.AddRange(bufferArray);
            }
            catch
            {
                buffer.Clear();
            }
        }
    }
}
