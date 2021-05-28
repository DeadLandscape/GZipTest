namespace GZipTest.Data
{
    /// <summary>
    /// This class describes block containing data to compress/decompress
    /// and ID of thread working with it.
    /// </summary>
    public class IndexedBlock
    {
        public int ID { get; private set; }
        public byte[] Data { get; set; }
        public byte[] CompressedData  { get; set; }
        /// <summary>
        /// Creates instance of class
        /// </summary>
        /// <param name="id">Thread ID</param>
        /// <param name="data">Data to process</param>
        /// <param name="compressedData">Compressed data (needed in decompression)</param>
        public IndexedBlock(int id, byte[] data, byte[] compressedData = null)
        {
            ID = id;
            Data = data;
            CompressedData = compressedData;
        }
    }
}
