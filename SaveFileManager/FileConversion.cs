using System.Text;
using NPrng.Generators;
using NPrng;
using System.Numerics;

namespace SaveFileManager
{
    public static class FileConversion
    {
        private static readonly string FILE_NAME_SEED_REPLACE_STRING = "*";

        /// <param name="fileLine">The line that will be in the file.</param>
        /// <inheritdoc cref="EncodeFile(IEnumerable{string}, long, string, string, int, Encoding)"/>
        public static void EncodeFile(string fileLine, long seed = 1, string filePath = "file", string fileExt = "sav", int version = 2, Encoding? encoding = null)
        {
            EncodeFile(new List<string> { fileLine }, seed, filePath, fileExt, version, encoding);
        }

        /// <summary>
        /// Creates a file that has been encoded by a seed.<br/>
        /// version numbers:<br/>
        /// - 1: normal: weak<br/>
        /// - 2: secure: stronger<br/>
        /// - 3: super-secure: strogest(only works, if opened on the same location, with the same name)<br/>
        /// - 4: stupid secure: v3 but encription "expires" on the next day
        /// </summary>
        /// <param name="fileLines">The list of lines that will be in the file.</param>
        /// <param name="seed">The seed for encoding the file.</param>
        /// <param name="filePath">The path and the name of the file without the extension, that will be created. If the path contains a *, it will be replaced with the seed.</param>
        /// <param name="fileExt">The extension of the file that will be created.</param>
        /// <param name="version">The encription version.</param>
        /// <param name="encoding">The encoding of the input lines. By default it uses the UTF8 encoding. You shouldn't need to change this.</param>
        public static void EncodeFile(IEnumerable<string> fileLines, long seed = 1, string filePath = "file", string fileExt = "sav", int version=2, Encoding? encoding = null)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }
            var r = MakeRandom(MakeSeed(seed));

            using (var f = File.Create($"{filePath.Replace(FILE_NAME_SEED_REPLACE_STRING, seed.ToString())}.{fileExt}"))
            {
                // v1
                if (version == 1)
                {
                    WriteLine(f, EncodeLine("1", r, encoding));
                    WriteLine(f, EncodeLine("-1", r, encoding));
                    var rr = MakeRandom(MakeSeed(seed));
                    foreach (var line in fileLines)
                    {
                        WriteLine(f, EncodeLine(line, rr, encoding));
                    }
                }
                else
                {
                    WriteLine(f, EncodeLine(version.ToString(), r, encoding));
                    BigInteger seedNum;
                    // v2
                    if (version == 2)
                    {
                        seedNum = BigInteger.Parse(DateTime.Now.ToString().Replace(" ", "").Replace("-", "").Replace(".", "").Replace(":", "")) / MakeSeed(seed, 17, 0.587);
                    }
                    // v3-4
                    else if (version == 3 || version == 4)
                    {
                        var path = AppContext.BaseDirectory + $"{filePath.Replace(FILE_NAME_SEED_REPLACE_STRING, seed.ToString())}.{fileExt}";
                        var pathBytes = Encoding.UTF8.GetBytes(path);
                        var pathNum = 1;
                        foreach (var by in pathBytes)
                        {
                            pathNum *= by;
                            pathNum = int.Parse(pathNum.ToString().Replace("0", ""));
                        }
                        var nowNum = decimal.Parse(DateTime.Now.ToString().Replace(" ", "").Replace("-", "").Replace(".", "").Replace(":", "")) / (decimal)MakeSeed(seed, 2, 0.587);
                        seedNum = new BigInteger(decimal.Parse((pathNum * nowNum).ToString().Replace("0", "").Replace("E+", "")) * 15439813);
                    }
                    else
                    {
                        seedNum = MakeSeed(seed);
                    }
                    WriteLine(f, EncodeLine(seedNum.ToString(), r, encoding));
                    // v4
                    if (version == 4)
                    {
                        var now = DateTime.Now;
                        seedNum *= (now.Year + now.Month + now.Day);
                    }
                    var mainRandom = MakeRandom(seedNum);
                    foreach (var line in fileLines)
                    {
                        WriteLine(f, EncodeLine(line, mainRandom, encoding));
                    }
                }
            }
        }

        private static void WriteLine(FileStream file, IEnumerable<byte> byteList)
        {
            var bytes = byteList.ToArray();
            file.Write(bytes, 0, bytes.Count());
        }

        /// <summary>
        /// Returns a list of strings, decoded fron the encoded file.<br/>
        /// </summary>
        /// <param name="seed">The seed for decoding the file.</param>
        /// <param name="filePath">The path and the name of the file without the extension, that will be decoded. If the path contains a *, it will be replaced with the seed.</param>
        /// <param name="fileExt">The extension of the file that will be decoded.</param>
        /// <param name="decodeUntil">Controlls how many lines the function should decode(strarting from the beggining, with 1). If it is set to -1, it will decode all the lines in the file.</param>
        /// <param name="encoding">The encoding of the output lines. By default it uses the UTF8 encoding. You shouldn't need to change this.</param>
        public static List<string> DecodeFile(long seed = 1, string filePath = "file", string fileExt = "sav", int decodeUntil = -1, Encoding? encoding = null)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }
            //get lines
            var fileBytes = File.ReadAllBytes($"{filePath.Replace(FILE_NAME_SEED_REPLACE_STRING, seed.ToString())}.{fileExt}");

            var byteLines = new List<IEnumerable<byte>>();
            var newL = new List<byte>();
            foreach (var by in fileBytes)
            {
                newL.Add(by);
                if (by == 10)
                {
                    byteLines.Add(newL);
                    newL = new List<byte>();
                }
            }

            // get version
            var r = MakeRandom(MakeSeed(seed));
            var version = int.Parse(DecodeLine(byteLines.ElementAt(0), r, encoding));
            var seedNum = BigInteger.Parse(DecodeLine(byteLines.ElementAt(1), r, encoding));
            // decode
            if (version != -1)
            {
                var linesDecoded = new List<string>();
                if (version == 4)
                {
                    var now = DateTime.Now;
                    seedNum *= (now.Year + now.Month + now.Day);
                }
                else if (version < 2 || version > 4)
                {
                    seedNum = MakeSeed(seed);
                }
                var mainRandom = MakeRandom(seedNum);
                for (var x = 2; x < byteLines.Count(); x++)
                {
                    if (decodeUntil > -1 && x >= decodeUntil + 2)
                    {
                        break;
                    }
                    linesDecoded.Add(DecodeLine(byteLines.ElementAt(x), mainRandom, encoding));
                }
                return linesDecoded;
            }
            throw new FormatException("The seed of the file cannot be decoded.");
        }

        /// <summary>
        /// Encodes a line into a list of bytes.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="rand">A random number generator from NPrng.</param>
        /// <param name="encoding">The encoding that the text is in.</param>
        /// <returns>The encoded bytes.</returns>
        private static IEnumerable<byte> EncodeLine(string line, AbstractPseudoRandomGenerator rand, Encoding encoding)
        {
            var encode64 = rand.GenerateInRange(2, 5);
            // encoding into bytes
            var lineEnc = encoding.GetBytes(line);
            // change encoding to utf-8
            var lineUtf8 = Encoding.Convert(encoding, Encoding.UTF8, lineEnc);
            // encode to base64 x times
            var lineBase64 = lineUtf8;
            for (int x = 0; x < encode64; x++)
            {
                lineBase64 = Encoding.UTF8.GetBytes(Convert.ToBase64String(lineBase64));
            }
            // shufling bytes
            var lineEncoded = new List<byte>();
            foreach (var byteBase64 in lineBase64)
            {
                var modByte = (byte)(byteBase64 + (int)rand.GenerateInRange(-32, 134));
                lineEncoded.Add(modByte);
            }
            // + \n
            lineEncoded.Add(10);
            return lineEncoded;
        }

        /// <summary>
        /// Decodes a list of bytes into a line.
        /// </summary>
        /// <param name="bytes">The list of bytes.</param>
        /// <param name="rand">A random number generator from NPrng.</param>
        /// <param name="encoding">The encoding that the text is in.</param>
        /// <returns>The decoded line.</returns>
        private static string DecodeLine(IEnumerable<byte> bytes, AbstractPseudoRandomGenerator rand, Encoding encoding)
        {
            var encode64 = rand.GenerateInRange(2, 5);
            // deshufling bytes
            var lineDecoded = new List<byte>();
            foreach (var lineByte in bytes)
            {
                if (lineByte != 10)
                {
                    var modByte = (byte)(lineByte - (int)rand.GenerateInRange(-32, 134));
                    lineDecoded.Add(modByte);
                }
            }
            // encode to base64 x times
            var lineUtf8 = lineDecoded.ToArray();
            for (int x = 0; x < encode64; x++)
            {
                var e1 = lineUtf8.ToArray();
                var e2 = Encoding.UTF8.GetString(e1);
                lineUtf8 = Convert.FromBase64String(e2);
            }
            // change encoding from utf-8
            var lineBytes = Encoding.Convert(Encoding.UTF8, encoding, lineUtf8);
            // decode into string
            return encoding.GetString(lineBytes);
        }

        /// <summary>
        /// Generates a seed number from another number.
        /// </summary>
        /// <param name="seed">The number to use to generate the seed.</param>
        private static BigInteger MakeSeed(long seed, int powNum = 73, double plusNum = 713853.587)
        {
            var seedA = new BigInteger(Math.Abs(seed));
            var pi = new BigInteger(Math.PI);
            return Utils.Sqrt(BigInteger.Pow(seedA * pi, powNum) * (new BigInteger(plusNum) + seedA * pi));
        }

        private static readonly BigInteger MaxModuloValue = new BigInteger(ulong.MaxValue) + 1;

        /// <summary>
        /// Generates a random number generator from another number.
        /// </summary>
        /// <param name="seed">The number to use to generate the random number generator.</param>
        private static AbstractPseudoRandomGenerator MakeRandom(BigInteger seed)
        {
            return new SplittableRandom((ulong)(BigInteger.Abs(seed) % MaxModuloValue));
        }
    }
}
