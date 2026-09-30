using System;
using System.Diagnostics;
using System.IO;

public static class UpdateVersion {
 public static Version Read(string folder,Func<string,Version> read=null) {
  read??=ReadFile;Version latest=null;
  foreach(string name in new[]{"KimaiBlocks.dll","KimaiBlocks.exe"}) {
   try {var version=read(Path.Combine(folder,name));if(version!=null&&version>new Version(0,0,0,0)&&(latest==null||version>latest))latest=version;}
   catch(IOException){}catch(UnauthorizedAccessException){}catch(System.ComponentModel.Win32Exception){}
  }
  return latest??throw new IOException("KimaiBlocks.dll または KimaiBlocks.exe のバージョンを読み込めません。");
 }
 static Version ReadFile(string path) {
  var info=FileVersionInfo.GetVersionInfo(path);
  if(string.IsNullOrWhiteSpace(info.FileVersion))return null;
  return new Version(info.FileMajorPart,info.FileMinorPart,info.FileBuildPart,info.FilePrivatePart);
 }
 public static void Tests() {
  var older=new Version(2026,9,27,1);var newer=new Version(2026,10,1,130);
  if(Read("test",p=>p.EndsWith(".exe")?newer:throw new FileNotFoundException())!=newer)throw new Exception("EXE-only update check");
  if(Read("test",p=>p.EndsWith(".dll")?newer:throw new FileNotFoundException())!=newer)throw new Exception("DLL-only update check");
  if(Read("test",p=>p.EndsWith(".dll")?older:newer)!=newer||Read("test",p=>p.EndsWith(".exe")?older:newer)!=newer)throw new Exception("Mixed update versions");
  if(Read("test",p=>p.EndsWith(".dll")?null:newer)!=newer)throw new Exception("Missing version fallback");
  try {Read("test",p=>throw new FileNotFoundException());throw new Exception("Missing update accepted");}catch(IOException){}
 }
}

