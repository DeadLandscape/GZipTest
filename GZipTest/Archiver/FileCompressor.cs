using GZipTest.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GZipTest.Zip
{
    /// <summary>
    /// Abstract class containing common members for Compressor
    /// and Decompressor classes
    /// </summary>
    abstract class FileCompressor
    {
        /// <summary>
        /// Locker, needed when writing to file
        /// </summary>
        protected static readonly object locker = new object();
        protected string sourceFile;
        protected string targetFile;


        /// <summary>
        /// Pool of available threads
        /// </summary>
        protected static Thread[] pool = new Thread[Environment.ProcessorCount - 1];

        /// <summary>
        /// Thread 
        /// </summary>
        protected Thread writer;
        /// <summary>
        /// Signal, telling compiler that all reading threads are finished
        /// </summary>
        protected ManualResetEvent[] readyToCompress = new ManualResetEvent[pool.Length];
        protected ManualResetEvent[] readyToWrite = new ManualResetEvent[pool.Length];
        protected ManualResetEvent writingFinished = new ManualResetEvent(false);


        // Somehow if i put WaitHandle.WaitAll(readyToWrite) inside WriteToFile method
        // it works not as i expected it to so this is my solution to synchronize properly
        protected ManualResetEvent beginWrite = new ManualResetEvent(false);


        protected FileStream writerStream;
        protected FileStream readerStream;
        protected static int defaultBufferSize = 1024 * 1024;
        protected long bufferSize = defaultBufferSize;
        protected byte[] buffer;

        /// <summary>
        /// Collection of elements ready to write
        /// </summary>
        protected List<IndexedBlock> blocksToWrite = new List<IndexedBlock>();

        /// <summary>
        /// Writes compressed data to target file
        /// </summary>
        /// <param name="chunks">Ordered collection of elements to write</param>
        /// <param name="stream">Stream writing to file</param>
        /// <param name="flag">Synchronize signal</param>
        protected void WriteToFile(IEnumerable<IndexedBlock> chunks, Stream stream, ManualResetEvent flag)
        {
            while (true)
            {
                try
                {
                    flag.WaitOne();
                    flag.Reset();
                    foreach (var block in chunks)
                    {
                        stream.Write(block.Data, 0, block.Data.Length);
                    }
                    blocksToWrite.Clear();
                    writingFinished.Set();
                }
                catch (ObjectDisposedException)
                {
                    CloseStreams();
                    throw;
                }
                catch (IOException)
                {
                    CloseStreams();
                    throw;
                }
            }
        }
        /// <summary>
        /// Closes streams when exception thrown;
        /// </summary>
        public void CloseStreams()
        {
            writerStream?.Close();
            readerStream?.Close();
        }
    }
}
