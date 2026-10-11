using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace EQEmu_Patcher
{
    internal sealed class PatchFile
    {
        internal string Name, Md5;
        internal long Bytes;
        internal PatchFile(string name, string md5, long bytes) { Name=name; Md5=md5; Bytes=bytes; }
    }
    // Download the whole release before replacing any installed file. Roll back caught
    // commit errors; leave a marker and backups if the process stops during installation.
    internal sealed class PatchTransaction
    {
        readonly string root;
        readonly bool allowLoopback;
        internal PatchTransaction(string clientRoot, bool testLoopback=false) {
            root=Path.GetFullPath(clientRoot).TrimEnd(Path.DirectorySeparatorChar);
            allowLoopback=testLoopback;
        }
        internal string Marker { get { return Resolve("LaunchSupport/patch-incomplete.txt"); } }
        internal string Resolve(string relative) {
            if(String.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.IndexOfAny(new[]{':','\0','"','<','>','|','?','*'})>=0)
                throw new IOException("Unsafe patch path: "+relative);
            var parts=relative.Replace('\\','/').Split('/');
            foreach(var part in parts) {
                if(part.Length==0 || part=="." || part==".." || part.Any(Char.IsControl) || part.EndsWith(".") || part.EndsWith(" ") ||
                    System.Text.RegularExpressions.Regex.IsMatch(part,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    throw new IOException("Unsafe patch path: "+relative);
            }
            string full=Path.GetFullPath(Path.Combine(root,Path.Combine(parts)));
            if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new IOException("Patch path leaves the client folder.");
            string cursor=root;
            foreach(var part in parts) {
                if((File.Exists(cursor)||Directory.Exists(cursor)) && (File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0)
                    throw new IOException("Patch path uses a link: "+relative);
                cursor=Path.Combine(cursor,part);
            }
            if((File.Exists(full)||Directory.Exists(full)) && (File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0) throw new IOException("Patch target is a link: "+relative);
            return full;
        }
        internal static string Md5(string path) {
            using(var hash=MD5.Create()) using(var stream=File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        void Url(Uri url) {
            if(url.Scheme!="https" && !(allowLoopback && url.Scheme=="http" && url.IsLoopback)) throw new IOException("Patch downloads require HTTPS.");
        }
        void ValidateTarget(string name) {
            Resolve(name);
            var lower=name.Replace('\\','/').ToLowerInvariant();
            var executable=System.Reflection.Assembly.GetEntryAssembly();
            if(executable!=null && lower==Path.GetFileName(executable.Location).ToLowerInvariant()) throw new IOException("Update the patcher separately from client data.");
            if(new[]{"eqclient.ini","zeal.ini","eqemupatcher.yml","launchsupport/patch-incomplete.txt","gequip5.s3d","global6_chr.s3d","bmpwad8.s3d","eqfeart7.pak","spellsnew.edd","spellsnew.eff"}.Contains(lower) || lower.StartsWith("classicfix/") || lower=="classicfix" ||
                lower.StartsWith("launchsupport/backups/") || lower.StartsWith("launchsupport/logs/") || lower.StartsWith("launchsupport/patch-transactions/"))
                throw new IOException("Manifest attempts to replace player settings or patcher state: "+name);
        }
        internal async Task ApplyAsync(IEnumerable<PatchFile> files, IEnumerable<string> deletions, Uri prefix,
            CancellationToken cancellation, Action<int> progress, Action<string> log) {
            Url(prefix);
            if(File.Exists(Marker)) throw new IOException("An earlier update stopped during installation. Restore its transaction backup before patching or playing.");
            if(File.Exists(Resolve("ClassicFix/incomplete.json"))) throw new IOException("A classic fix stopped during installation. Use Classic fixes > Restore before patching.");
            var entries=files.ToList(); var deletes=(deletions??Enumerable.Empty<string>()).ToList();
            if(entries.Count==0 || entries.Count>25000 || deletes.Count>5000) throw new IOException("Invalid manifest file count.");
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in entries) {
                ValidateTarget(entry.Name);
                if(!names.Add(entry.Name.Replace('\\','/')) || entry.Bytes<0 || entry.Bytes>512L*1024*1024 ||
                    !System.Text.RegularExpressions.Regex.IsMatch(entry.Md5??"",@"\A[0-9a-fA-F]{32}\z")) throw new IOException("Invalid manifest entry: "+entry.Name);
            }
            foreach(var name in deletes) { ValidateTarget(name); if(!names.Add(name.Replace('\\','/'))) throw new IOException("Duplicate download/delete: "+name); }
            long total=Math.Max(1,entries.Sum(x=>x.Bytes)); if(total>50L*1024*1024*1024) throw new IOException("Manifest exceeds the release size limit.");
            string id=Guid.NewGuid().ToString("N");
            string transactionRelative="LaunchSupport/patch-transactions/"+id;
            string transaction=Resolve(transactionRelative); Directory.CreateDirectory(transaction);
            var changed=new List<PatchFile>(); long checkedBytes=0;
            using(var client=new HttpClient {Timeout=TimeSpan.FromMinutes(10)}) {
                foreach(var entry in entries) {
                    cancellation.ThrowIfCancellationRequested();
                    string target=Resolve(entry.Name);
                    if(File.Exists(target) && new FileInfo(target).Length==entry.Bytes && String.Equals(Md5(target),entry.Md5,StringComparison.OrdinalIgnoreCase)) {
                        checkedBytes+=entry.Bytes; progress((int)(checkedBytes*9000/total)); continue;
                    }
                    string escaped=String.Join("/",entry.Name.Replace('\\','/').Split('/').Select(Uri.EscapeDataString));
                    Uri url=new Uri(prefix.AbsoluteUri.TrimEnd('/')+"/"+escaped); Url(url);
                    string staging=Resolve(transactionRelative+"/download/"+entry.Name.Replace('\\','/'));
                    Directory.CreateDirectory(Path.GetDirectoryName(staging));
                    using(var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,cancellation).ConfigureAwait(false)) {
                        response.EnsureSuccessStatusCode(); Url(response.RequestMessage.RequestUri);
                        if(response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value!=entry.Bytes) throw new IOException("Download size mismatch: "+entry.Name);
                        using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false)) using(var output=new FileStream(staging,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                            byte[] buffer=new byte[81920];long received=0;int count;
                            while((count=await input.ReadAsync(buffer,0,buffer.Length,cancellation).ConfigureAwait(false))>0) {
                                received+=count;if(received>entry.Bytes) throw new IOException("Download exceeds expected size: "+entry.Name);
                                await output.WriteAsync(buffer,0,count,cancellation).ConfigureAwait(false);
                                progress((int)((checkedBytes+received)*9000/total));
                            }
                            if(received!=entry.Bytes) throw new IOException("Incomplete download: "+entry.Name);
                            output.Flush(true);
                        }
                    }
                    if(!String.Equals(Md5(staging),entry.Md5,StringComparison.OrdinalIgnoreCase)) throw new IOException("Downloaded MD5 mismatch: "+entry.Name+". Installed files were preserved.");
                    changed.Add(entry);checkedBytes+=entry.Bytes;log("Verified "+entry.Name);
                }
            }
            cancellation.ThrowIfCancellationRequested();
            string journal=Resolve(transactionRelative+"/journal.tsv");
            // Journal records every planned target before any move; the backup tree preserves relative paths.
            File.WriteAllLines(journal,changed.Select(x=>"replace\t"+x.Name).Concat(deletes.Select(x=>"delete\t"+x)));
            Directory.CreateDirectory(Path.GetDirectoryName(Marker));
            File.WriteAllText(Marker,transactionRelative+Environment.NewLine);
            var moved=new List<Tuple<string,string,bool>>();
            try {
                foreach(var name in changed.Select(x=>x.Name).Concat(deletes)) {
                    cancellation.ThrowIfCancellationRequested();
                    string target=Resolve(name),backup=Resolve(transactionRelative+"/backup/"+name.Replace('\\','/'));
                    bool replace=changed.Any(x=>String.Equals(x.Name,name,StringComparison.OrdinalIgnoreCase));
                    bool existed=File.Exists(target);
                    if(existed) {Directory.CreateDirectory(Path.GetDirectoryName(backup));File.Move(target,backup);}
                    moved.Add(Tuple.Create(target,backup,existed));
                    if(replace) {
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        File.Move(Resolve(transactionRelative+"/download/"+name.Replace('\\','/')),target);
                    }
                }
                File.WriteAllText(Resolve(transactionRelative+"/status.txt"),"COMPLETED");
                File.Delete(Marker);progress(10000);log("Update verified and installed. Previous files are in "+transactionRelative+"/backup.");
            } catch {
                bool restored=true;
                foreach(var operation in Enumerable.Reverse(moved)) {
                    try { if(File.Exists(operation.Item1)) File.Delete(operation.Item1); if(operation.Item3) File.Move(operation.Item2,operation.Item1); }
                    catch { restored=false; }
                }
                if(restored) {File.WriteAllText(Resolve(transactionRelative+"/status.txt"),"ROLLED_BACK");File.Delete(Marker);}
                throw;
            }
        }
    }
}
