using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using GZipTest.Data;

namespace GZipTest.Zip.Compression
{
    /// <summary>
    /// Class performing operations to decompress targeted file
    /// </summary>
    class Decompressor : FileCompressor
    {
        /// <summary>
        /// Queue with elements ready to be decompressed
        /// </summary>
        private readonly ConcurrentQueue<IndexedBlock> _queueToDecompress = new ConcurrentQueue<IndexedBlock>();

        /// <summary>
        /// Creates instance of class with specified source file and target file
        /// </summary>
        /// <param name="src">File to decompress</param>
        /// <param name="dst">Result of decompression</param>
        public Decompressor(string src, string dst)
        {
            sourceFile = src;
            targetFile = dst;
        }
        /// <summary>
        /// Executes decompression process
        /// </summary>
        /// <returns>Return 0 if succeeded</returns>
        public int Decompress()
        {
            Console.WriteLine($"Decompressing file {sourceFile} ...");
            Stopwatch sw = new Stopwatch();
            sw.Start();
            writerStream = new FileStream(targetFile, FileMode.Create);
            readerStream = File.OpenRead(sourceFile);
            using (readerStream)
            {
                //If file is too small for all threads resize arrays of reset events

                int fileSize = (int)Math.Ceiling((double)readerStream.Length / bufferSize);
                if (fileSize < readyToCompress.Length)
                {
                    readyToCompress = new ManualResetEvent[fileSize];
                    readyToWrite = new ManualResetEvent[fileSize];
                }
                while (readerStream.Position < readerStream.Length)
                {
                    // Fill queue with data to decompress and start decompressing

                    for (int i = 0; i < pool.Length && readerStream.Position < readerStream.Length; i++)
                    {
                        if (readyToCompress[i] == null)
                        {
                            readyToCompress[i] = new ManualResetEvent(false);
                            readyToWrite[i] = new ManualResetEvent(false);
                        }
                        else
                        {
                            readyToCompress[i].Reset();
                            readyToWrite[i].Reset();
                        }

                        writingFinished.Reset();
                        beginWrite.Reset();
                        //Get length of block in int
                        byte[] lengthBuffer = new byte[8]; // Service bytes
                        readerStream.Read(lengthBuffer, 0, lengthBuffer.Length);
                        int blockLength = BitConverter.ToInt32(lengthBuffer, 4); // last 4 bytes containing length of block
                        byte[] compressedData = new byte[blockLength]; // 
                        lengthBuffer.CopyTo(compressedData, 0);

                        //Skip 8 service bytes
                        readerStream.Read(compressedData, 8, blockLength - 8);
                        bufferSize = BitConverter.ToInt32(compressedData, blockLength - 4);
                        buffer = new byte[bufferSize];
                        _queueToDecompress.Enqueue(new IndexedBlock(i, buffer, compressedData));
                        if (pool[i] == null)
                        {
                            int id = i;
                            pool[i] = new Thread(() => DecompressBlock(readyToCompress[id])) { IsBackground = true };
                            pool[i].Start();
                        }
                        readyToCompress[i].Set();
                    }
                    WaitHandle.WaitAll(readyToWrite); // Wait for decompression to finish
                    if (writer == null)
                    {
                        writer = new Thread(() => WriteToFile(blocksToWrite.OrderBy(data => data.ID), writerStream, beginWrite)) { IsBackground = true };
                        writer.Start();
                    }
                    beginWrite.Set();
                    writingFinished.WaitOne();
                }
            }
            CloseStreams();
            sw.Stop();
            TimeSpan elapsedTime = sw.Elapsed;
            Console.WriteLine($"File {sourceFile} successfully decompressed in {elapsedTime.ToString()}");
            return 0;
        }

        /// <summary>
        /// Decompresses given IndexedBlock data
        /// </summary>
        /// <param name="flag">Synchronization object</param>
        private void DecompressBlock(ManualResetEvent flag)
        {
            while (true)
            {
                try
                {
                    flag.WaitOne();
                    flag.Reset();
                    _queueToDecompress.TryDequeue(out IndexedBlock block);
                    using (MemoryStream memStream = new MemoryStream(block.CompressedData))
                    {
                        using (GZipStream gzip = new GZipStream(memStream, CompressionMode.Decompress))
                        {
                            gzip.Read(block.Data, 0, block.Data.Length);
                        }
                    }
                    lock (locker)
                    {
                        blocksToWrite.Add(block);
                    }
                    readyToWrite[block.ID].Set();
                }
                catch (ObjectDisposedException)
                {
                    CloseStreams();
                    throw;
                }
            }
        }
    }
}
