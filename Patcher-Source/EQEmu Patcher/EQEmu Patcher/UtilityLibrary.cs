using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Net.Http;
using System.Threading;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;

namespace EQEmu_Patcher
{
    /* General Utility Methods */
    class UtilityLibrary
    {
        //Download a file to current directory
        public static async Task<string> DownloadFile(CancellationTokenSource cts, string url, string outFile)
        {
            string temporary=null;
            try
            {
                string outPath=new PatchTransaction(Path.GetDirectoryName(Application.ExecutablePath)).Resolve(outFile);
                var data=await Download(cts,url);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                temporary=outPath+"."+Guid.NewGuid().ToString("N")+".partial";
                using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {output.Write(data,0,data.Length);output.Flush(true);}
                cts.Token.ThrowIfCancellationRequested();
                if(File.Exists(outPath))File.Replace(temporary,outPath,null);else File.Move(temporary,outPath);
            }
            catch (ArgumentNullException e)
            {
                return "ArgumentNullException: " + e.Message;
            }
            catch (WebException e)
            {
                return "WebException (" + e.Status + "): " + e.Message + " (" + e.GetBaseException() + ")";
            }
            catch (HttpRequestException e)
            {
                return "HttpRequestException: " + e.Message + " (" + e.GetBaseException() + ")";
            }
            catch (Exception e)
            {
                return "Exception: " + e.Message + " (" + e.GetBaseException() + ")";
            }
            finally {if(temporary!=null&&File.Exists(temporary))try{File.Delete(temporary);}catch(IOException){}}
            return "";      
        }

        // Download will grab a remote URL's file and return the data as a byte array
        public static async Task<byte[]> Download(CancellationTokenSource cts, string url)
        {
            var uri=new Uri(url);
            if(uri.Scheme!="https")throw new IOException("Manifest and patcher metadata require HTTPS.");
            using(var handler=new HttpClientHandler {AllowAutoRedirect=false})
            using(var client=new HttpClient(handler) {Timeout=TimeSpan.FromMinutes(2)})
            using(var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,cts.Token))
            {
                response.EnsureSuccessStatusCode();
                const int limit=16*1024*1024;
                if(response.Content.Headers.ContentLength.GetValueOrDefault()>limit)throw new IOException("Metadata exceeds the size limit.");
                using(var stream=await response.Content.ReadAsStreamAsync())
                using(var w=new MemoryStream())
                {
                    byte[] buffer=new byte[81920];int count;
                    while((count=await stream.ReadAsync(buffer,0,buffer.Length,cts.Token))>0) {if(w.Length+count>limit)throw new IOException("Metadata exceeds the size limit.");w.Write(buffer,0,count);}
                    if(response.Content.Headers.ContentLength.HasValue && w.Length!=response.Content.Headers.ContentLength.Value)throw new IOException("Incomplete metadata download.");
                    return w.ToArray();
                }
            }
        }

        public static string GetMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);

                    StringBuilder sb = new StringBuilder();

                    for (int i = 0; i < hash.Length; i++)
                    {
                        sb.Append(hash[i].ToString("X2"));
                    }

                    return sb.ToString();
                }
            }
        }

        public static System.Diagnostics.Process StartEverquest()
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = System.IO.Path.GetDirectoryName(Application.ExecutablePath) + "\\eqgame.exe",
                Arguments = "patchme",
                WorkingDirectory = System.IO.Path.GetDirectoryName(Application.ExecutablePath)
            };

            return System.Diagnostics.Process.Start(startInfo);
        }

        //Pass the working directory (or later, you can pass another directory) and it returns a hash if the file is found
        public static string GetEverquestExecutableHash(string path)
        {
            var di = new System.IO.DirectoryInfo(path);
            var files = di.GetFiles("eqgame.exe");
            if (files == null || files.Length == 0)
            {
                return "";
            }
            return UtilityLibrary.GetMD5(files[0].FullName);
        }

        // Returns true only if the path is a relative and does not contain ..
        public static bool IsPathChild(string path)
        {
            try {new PatchTransaction(Path.GetDirectoryName(Application.ExecutablePath)).Resolve(path);return true;}
            catch(IOException) {return false;}
        }
    }
}
