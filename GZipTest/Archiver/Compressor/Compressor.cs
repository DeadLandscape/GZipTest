using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using GZipTest.Data;

namespace GZipTest.Zip.Compression
{
    /// <summary>
    /// Class performing operations to compress targeted file
    /// </summary>
    class Compressor : FileCompressor
    {
        /// <summary>
        /// Queue with elements ready to be compressed
        /// </summary>
        private readonly ConcurrentQueue<IndexedBlock> _queueToCompress = new ConcurrentQueue<IndexedBlock>();

        /// <summary>
        /// Creates instance of Compressor class with specified source file and target file
        /// </summary>
        /// <param name="src">File to compress</param>
        /// <param name="dst">Result of compression</param>
        public Compressor(string src, string dst)
        {
            sourceFile = src;
            targetFile = dst;
        }

        /// <summary>
        /// Execute compression process
        /// </summary>
        /// <returns>Returns 0 if succeeded</returns>
        public int Compress()
        {
            Stopwatch sw = new Stopwatch();
            Console.WriteLine($"Compressing file {sourceFile} ...");
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
                    // Fill queue with data to compress and start compressing
                    for (int i = 0; i < pool.Length && readerStream.Position < readerStream.Length; i++)
                    {
                        if (readyToCompress[i] == null)
                        {
                            readyToCompress[i] = new ManualResetEvent(false);
                            readyToWrite[i] = new ManualResetEvent(false);
                        }

                        readyToCompress[i].Reset();
                        readyToWrite[i].Reset();
                        writingFinished.Reset();
                        beginWrite.Reset();

                        if (readerStream.Length - readerStream.Position < bufferSize)
                            bufferSize = readerStream.Length - readerStream.Position;
                        else
                            bufferSize = defaultBufferSize;

                        buffer = new byte[bufferSize];
                        readerStream.Read(buffer, 0, buffer.Length);
                        _queueToCompress.Enqueue(new IndexedBlock(i, buffer));
                        if (pool[i] == null)
                        {
                            // If i pass 'i' without creating copy it throws index out of array exeception at some point
                            // Not sure why
                            int id = i;
                            pool[i] = new Thread(() => CompressBlock(readyToCompress[id])) { IsBackground = true };
                            pool[i].Start();
                        }
                        readyToCompress[i].Set();
                    }
                    WaitHandle.WaitAll(readyToWrite);
                    if (writer == null)
                    {
                        writer = new Thread(() => WriteToFile(blocksToWrite.OrderBy(d => d.ID), writerStream, beginWrite)) { IsBackground = true };
                        writer.Start();
                    }
                    beginWrite.Set();
                    writingFinished.WaitOne();
                }
            }
            CloseStreams();
            sw.Stop();
            TimeSpan elapsedTime = sw.Elapsed;
            Console.WriteLine($"File {sourceFile} successfully compressed in {elapsedTime.ToString()}");
            return 0;
        }
        /// <summary>
        /// Compress data from queue
        /// </summary>
        /// <param name="flag"></param>
        private void CompressBlock(ManualResetEvent flag)
        {
            while (true)
            {
                try
                {
                    flag.WaitOne();
                    flag.Reset();
                    _queueToCompress.TryDequeue(out IndexedBlock block);

                    using (MemoryStream memStream = new MemoryStream())
                    {
                        using (GZipStream gzip = new GZipStream(memStream, CompressionMode.Compress))
                        {
                            gzip.Write(block.Data, 0, block.Data.Length);
                        }
                        block.Data = memStream.ToArray();
                    }
                    BitConverter.GetBytes(block.Data.Length).CopyTo(block.Data, 4);
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
