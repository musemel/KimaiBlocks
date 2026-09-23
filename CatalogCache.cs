using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using MarkZither.KimaiDotNet.Models;

public sealed class CatalogSnapshot {
 public string Url {get;set;}
 public int User {get;set;}
 public DateTime SavedUtc {get;set;}
 public List<CachedProject> Projects {get;set;}=new List<CachedProject>();
 public List<CachedActivity> Activities {get;set;}=new List<CachedActivity>();
}
public sealed class CachedProject {
 public int Id {get;set;} public string Name {get;set;} public bool? Visible {get;set;} public bool? Billable {get;set;} public bool? GlobalActivities {get;set;}
}
public sealed class CachedActivity {
 public int Id {get;set;} public int? Project {get;set;} public string Name {get;set;} public bool? Visible {get;set;} public bool? Billable {get;set;}
}
public partial class Blocks {
 async Task LoadCatalog(KimaiService api,bool force) {
  string path=AccountFile(api.BaseUrl,api.Me.Id.Value)+".catalog";
  CatalogSnapshot cached=null;
  if(!force&&File.Exists(path))try {cached=JsonSerializer.Deserialize<CatalogSnapshot>(await File.ReadAllTextAsync(path));}catch{}
  if(cached!=null&&cached.Url==api.BaseUrl&&cached.User==api.Me.Id&&cached.SavedUtc<=DateTime.UtcNow&&DateTime.UtcNow-cached.SavedUtc<TimeSpan.FromMinutes(Math.Clamp(settings.CatalogMinutes,1,1440))) {
   api.RestoreCatalog(cached);return;
  }
  await api.ReloadCatalogAsync();
  var snapshot=new CatalogSnapshot {Url=api.BaseUrl,User=api.Me.Id.Value,SavedUtc=DateTime.UtcNow,
   Projects=api.Projects.Select(p=>new CachedProject {Id=p.Id.Value,Name=p.Name,Visible=p.Visible,Billable=p.Billable,GlobalActivities=p.GlobalActivities}).ToList(),
   Activities=api.Activities.Select(a=>new CachedActivity {Id=a.Id.Value,Project=a.Project,Name=a.Name,Visible=a.Visible,Billable=a.Billable}).ToList()};
  Directory.CreateDirectory(DataDirectory);await File.WriteAllTextAsync(path+".tmp",JsonSerializer.Serialize(snapshot));File.Move(path+".tmp",path,true);
 }
}
