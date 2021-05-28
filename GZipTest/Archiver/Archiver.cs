using GZipTest.Zip.Compression;
using System;
using System.IO;

namespace GZipTest.Zip
{
    /// <summary>
    /// Interface to communicate with compressor and decompressor
    /// </summary>
    class Archiver
    {

        private Compressor _compressor;
        private Decompressor _decompressor;
        private readonly string _sourceFile;
        private readonly string _targetFile;

        public Archiver(string sourceFile, string targetFile)
        {
            _sourceFile = sourceFile;
            _targetFile = targetFile;
        }

        /// <summary>
        /// Removes created file if process is interrupted with exception
        /// </summary>
        /// <param name="fileName"></param>
        private void RemoveOnError(string fileName)
        {
            if (File.Exists(fileName))
                File.Delete(fileName);
        }

        /// <summary>
        /// Method runs command entered by user
        /// </summary>
        /// <param name="command">Command to execute (compress/decompress)</param>
        /// <returns>Returns 0 if execution was succesfull, 1 otherwise</returns>
        public int Launch(string command)
        {
            FileInfo info = new FileInfo(_sourceFile);
            try
            {
                switch (command)
                {
                    case "COMPRESS":
                        _compressor = new Compressor(_sourceFile, _targetFile);
                        return _compressor.Compress();
                    case "DECOMPRESS":
                        _decompressor = new Decompressor(_sourceFile, _targetFile);
                        return _decompressor.Decompress();
                    default:
                        Console.WriteLine("Unknown command");
                        return 1;
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                RemoveOnError(_targetFile);
                Console.WriteLine(ex.Message);
                return 1;
            }
            catch (FileNotFoundException ex)
            {
                RemoveOnError(_targetFile);
                Console.WriteLine($"File {ex.FileName} not found");
                return 1;
            }
            catch (UnauthorizedAccessException)
            {
                RemoveOnError(_targetFile);
                Console.WriteLine($"Access to file {_targetFile} denied");
                return 1;
            }
            catch (ObjectDisposedException)
            {
                RemoveOnError(_targetFile);
                System.Console.WriteLine("Access to disposed object");
                return 1;
            }
            catch (IOException ex)
            {
                RemoveOnError(_targetFile);
                Console.WriteLine(ex.Message);
                return 1;
            }
            finally
            {
                _compressor?.CloseStreams();
                _decompressor?.CloseStreams();
            }
        }
    }
}