using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System;
using System.Text;
using static System.Net.WebRequestMethods;

namespace SaveFileManager
{
    public class FileReader
    {
        private static readonly string FILE_NAME_SEED_REPLACE_STRING = "*";

        /// <summary>
        /// This exeption is raised if "fileName" and "seed" are both null in ReadFiles
        /// </summary>
        public class ReadFilesArgsError : Exception
        {
            /// <summary>
            /// This exeption is raised if "fileName" and "seed" are both null or fileName doesn't contain the special character ("*") in ReadFiles.
            /// </summary>
            public ReadFilesArgsError()
                : this($"\"fileName\" and \"seed\" can\'t both be null at the same time, and \"fileName\" must contain at least one \"{FILE_NAME_SEED_REPLACE_STRING}\"") { }

            /// <summary>
            /// This exeption is raised if "fileName" and "seed" are both null or fileName doesn't contain the special character ("*") in ReadFiles.
            /// </summary>
            /// <param name="message">The message to display.</param>
            public ReadFilesArgsError(string message) : base(message) { }
        }


        /// <summary>
        /// Gets data from all save files in a folder, and returns them in a format that save managers can read.<br/>
        /// If a file is corrupted, the data will be replaced with null.<br/>
        /// If <c>fileName</c>  is null and <c>seed</c> is NOT null then the function will search for all files with the <c>fileExt</c> extension and tries to decode them with the  <c>seed</c>.
        /// </summary>
        /// <param name="maxFiles">The maximum amount of files to return / the range of seeds for the files that will be returned. -1 for no limit.</param>
        /// <param name="fileName">The name of the file without the extension, that will be decoded, with the seed coming from the number that is in the place of "*"-s.</param>
        /// <param name="fileExt">The extension of the files that will be decoded.</param>
        /// <param name="dirName">The directory that will be searched for files. By default it uses the current working directory.</param>
        /// <param name="decodeUntil">How many lines the function should decode (strarting from the beggining, with 1).</param>
        /// <param name="seed">The seed that will be used to decode files.</param>
        /// <returns>A dictionary of file datas, where the key is eighter the seed, or the name of the file, and the data is a list os lines returned by the <c>DecodeFile</c> function or null.</returns>
        /// <exception cref="ReadFilesArgsError"></exception>
        private static Dictionary<string, List<string>?> ReadFilesPrivate(int maxFiles = 5, string? fileName = "file*", string fileExt = "sav", string? dirName = null, int decodeUntil = -1, long? seed = null)
        {
            if (dirName is null)
            {
                dirName = AppContext.BaseDirectory;
            }

            // setup vars
            var regexStr = new StringBuilder("^");
            var fileCount = 0;
            var namingPatternSearch = false;
            if (fileName is not null && fileName.IndexOf(FILE_NAME_SEED_REPLACE_STRING) != -1)
            {
                namingPatternSearch = true;
                var saveNameSplit = $"{fileName}.{fileExt}".Split(FILE_NAME_SEED_REPLACE_STRING);
                for (int x = 0; x < saveNameSplit.Length; x++)
                {
                    regexStr.Append(Regex.Escape(saveNameSplit[x]));
                    if (x + 1 < saveNameSplit.Length)
                    {
                        regexStr.Append("(\\d+)");
                    }
                }
                regexStr.Append("$");
            }
            // get existing file numbers
            var filePaths = Directory.GetFiles(dirName);
            var validFiles = new List<string>();
            foreach (var filePath in filePaths)
            {
                var fName = filePath.Replace(dirName, string.Empty);
                // get files by naming pattern
                if (namingPatternSearch)
                {
                    var regexMatch = Regex.Match(fName, regexStr.ToString());
                    if (regexMatch.Success)
                    {
                        // are all regex groups the same?
                        var firstMatchGroup = regexMatch.Groups.Values.ElementAt(1).Value;
                        var sameGroupValues = true;
                        for (var x = 2; x < regexMatch.Groups.Count; x++)
                        {
                            if (regexMatch.Groups.Values.ElementAt(x).Value != firstMatchGroup)
                            {
                                sameGroupValues = false;
                                break;
                            }
                        }
                        if (sameGroupValues)
                        {
                            var fileNumber = int.Parse(firstMatchGroup);
                            if (fileNumber <= maxFiles || maxFiles < 0)
                            {
                                validFiles.Add(fileNumber.ToString());
                            }
                        }
                    }
                }
                // get files by extension only
                else if (seed is not null)
                {
                    if (Path.HasExtension(fName) && Path.GetExtension(fName) == $".{fileExt}")
                    {
                        validFiles.Add(Path.GetFileNameWithoutExtension(fName));
                        if (maxFiles >= 0)
                        {
                            fileCount++;
                            if (fileCount >= maxFiles)
                            {
                                break;
                            }
                        }
                    }
                }
                else
                {
                    throw new ReadFilesArgsError();
                }
            }
            validFiles.Sort();

            // get file datas
            var filesData = new Dictionary<string, List<string>?>();
            foreach (var file in validFiles)
            {
                List<string>? fileData = null;
                try
                {
                    try
                    {
                        if (fileName is not null)
                        {
                            fileData = FileConversion.DecodeFile(int.Parse(file), Path.Join(dirName, fileName.Replace(FILE_NAME_SEED_REPLACE_STRING, file)), fileExt, decodeUntil);
                        }
                        else if (seed is not null)
                        {
                            fileData = FileConversion.DecodeFile((long)seed, Path.Join(dirName, file), fileExt, decodeUntil);
                        }
                    }
                    catch (Exception ex) when (ex is FormatException || ex is DecoderFallbackException) { }
                    filesData.Add(file, fileData);
                }
                catch (FileNotFoundException e) { }
            }
            return filesData;
        }

        /// <summary>
        /// Gets data from all save files in a folder, and returns them in a format that save managers can read.<br/>
        /// Search for all files with the <c>fileExt</c> extension and tries to decode them with the seed coming from the number that is in the place of "*"(-s) in <c>fileName</c>.<br/>
        /// If a file is corrupted, the data will be replaced with null.
        /// </summary>
        /// <param name="maxFiles">The maximum amount of files to return. -1 for no limit.</param>
        /// <param name="fileName">The name of the file without the extension, that will be decoded, with the seed coming from the number that is in the place of "*"-s.</param>
        /// <param name="fileExt">The extension of the files that will be decoded.</param>
        /// <param name="dirName">The directory that will be searched for files. By default it uses the current working directory.</param>
        /// <param name="decodeUntil">How many lines the function should decode (strarting from the beggining, with 1).</param>
        /// <returns>A dictionary of file datas, where the key is the name of the file, and the data is a list os lines returned by the <c>DecodeFile</c> function or null.</returns>
        public static Dictionary<string, List<string>?> ReadFiles(string fileName = "file*", string fileExt = "sav", string? dirName = null, int maxFiles = 5, int decodeUntil = -1)
        {
            return ReadFilesPrivate(maxFiles, fileName, fileExt, dirName, decodeUntil, null);
        }

        /// <summary>
        /// Gets data from all save files in a folder, and returns them in a format that save managers can read.<br/>
        /// Searches for all files with the <c>fileExt</c> extension and tries to decode them with the <c>seed</c>.<br/>
        /// If a file is corrupted, the data will be replaced with null.
        /// </summary>
        /// <param name="maxFiles">The range of seeds for the files that will be returned. -1 for no limit.</param>
        /// <param name="fileExt">The extension of the files that will be decoded.</param>
        /// <param name="dirName">The directory that will be searched for files. By default it uses the current working directory.</param>
        /// <param name="decodeUntil">How many lines the function should decode (strarting from the beggining, with 1).</param>
        /// <param name="seed">The seed that will be used to decode files.</param>
        /// <returns>A dictionary of file datas, where the key is the seed, and the data is a list os lines returned by the <c>DecodeFile</c> function or null.</returns>
        public static Dictionary<string, List<string>?> ReadFiles(long seed = 1, string fileExt = "sav", string? dirName = null, int maxFiles = 5, int decodeUntil = -1)
        {
            return ReadFilesPrivate(maxFiles, null, fileExt, dirName, decodeUntil, seed);
        }

        /// <inheritdoc cref="ReadFiles(string, string, string?, int, int)"/>
        public static Dictionary<string, List<string>?> ReadFiles(string fileName = "file*", string? dirName = null, int decodeUntil = -1)
        {
            return ReadFiles(maxFiles:-1, fileName:fileName, dirName:dirName, decodeUntil:decodeUntil);
        }

        /// <inheritdoc cref="ReadFiles(long, string, string?, int, int)"/>
        public static Dictionary<string, List<string>?> ReadFiles(long seed, string? dirName = null, int decodeUntil = -1)
        {
            return ReadFiles(maxFiles:-1, seed:seed, dirName:dirName, decodeUntil:decodeUntil);
        }
    }
}
