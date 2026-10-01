using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public static class SettingsOverride {
 public const string FileName="settings.override.json";
 public static ConnectionSettings Apply(ConnectionSettings current,string json) {
  var patch=JsonNode.Parse(json) as JsonObject??throw new ArgumentException("上書き設定はJSONオブジェクトで指定してください。");
  var allowed=new[]{"Url","SaveSeconds","CatalogMinutes","UpdateFolder","ActivityGroupingPattern","Calendar","ShowWeekends","ShowStatistics","ZoomPercent","LeftPanelWidth","RightPanelWidth"};
  var merged=JsonSerializer.SerializeToNode(current).AsObject();
  foreach(var field in patch){if(!allowed.Contains(field.Key))throw new ArgumentException("上書き対象外の設定: "+field.Key);if(field.Value==null)throw new ArgumentException("上書き設定にnullは指定できません: "+field.Key);
   if(field.Key=="Calendar") {if(field.Value is not JsonObject calendar)throw new ArgumentException("Calendarはオブジェクトで指定してください。");var target=merged["Calendar"]?.AsObject()??new JsonObject();foreach(var item in calendar){if(!new[]{"DaysOff","Holidays","Breaks","OffHours","Shade","BlockInput"}.Contains(item.Key)||item.Value==null)throw new ArgumentException("不正なCalendar設定: "+item.Key);target[item.Key]=item.Value.DeepClone();}merged["Calendar"]=target.DeepClone();}
   else merged[field.Key]=field.Value.DeepClone();
  }
  var updated=merged.Deserialize<ConnectionSettings>();
  if(updated.SaveSeconds<10||updated.SaveSeconds>3600||updated.CatalogMinutes<1||updated.CatalogMinutes>1440||updated.ZoomPercent<50||updated.ZoomPercent>400||updated.LeftPanelWidth<200||updated.LeftPanelWidth>600||updated.RightPanelWidth<200||updated.RightPanelWidth>600)throw new ArgumentException("上書き設定の数値が範囲外です。");
  _ = new ActivityGrouping(updated.ActivityGroupingPattern);updated.Calendar.Intervals();updated.Calendar.HolidayDates();
  if(updated.Calendar.DaysOff?.Any(d=>d<0||d>6)==true)throw new ArgumentException("休日の曜日は0〜6で指定してください。");
  if(patch.ContainsKey("Url")){if(!string.IsNullOrWhiteSpace(updated.Url)&&(!Uri.TryCreate(updated.Url,UriKind.Absolute,out var uri)||(uri.Scheme!="http"&&uri.Scheme!="https")))throw new ArgumentException("上書きURLが不正です。");if(updated.Url!=current.Url){updated.UserId=0;var account=updated.Accounts.FirstOrDefault(a=>a.Id==updated.ActiveAccountId);if(account!=null){account.Url=updated.Url;account.UserId=0;}}}
  return updated;
 }
 public static void Tests(){var original=new ConnectionSettings();var changed=Apply(original,"{\"SaveSeconds\":120,\"Calendar\":{\"BlockInput\":true}}");if(changed.SaveSeconds!=120||!changed.Calendar.BlockInput||changed.Calendar.Breaks!=original.Calendar.Breaks||original.SaveSeconds!=60)throw new Exception("Override merge failed");foreach(var json in new[]{"{\"SaveSeconds\":0}","{\"ProtectedToken\":\"x\"}","{\"Calendar\":null}"}){bool failed=false;try{Apply(original,json);}catch(ArgumentException){failed=true;}if(!failed)throw new Exception("Invalid override accepted");}}
}
public partial class Blocks {
 void ApplyStartupOverrides() {
  string pending=Path.Combine(DataDirectory,"settings.override.pending.json");
  if(File.Exists(pending)) {
   var queued=JsonNode.Parse(File.ReadAllText(pending)).AsObject();
   if(Version.Parse(AppVersion)>=Version.Parse(queued["Version"].GetValue<string>())) {
    var previous=settings;settings=SettingsOverride.Apply(settings,queued["Settings"].ToJsonString());
    try{StoreSettings();}catch{settings=previous;throw;}File.Delete(pending);
   }
  }
  foreach(var path in new[]{Path.Combine(DataDirectory,SettingsOverride.FileName),Path.Combine(AppContext.BaseDirectory,SettingsOverride.FileName)}.Distinct(StringComparer.OrdinalIgnoreCase)) {
   if(!File.Exists(path))continue;var previous=settings;
   var updated=SettingsOverride.Apply(settings,File.ReadAllText(path));settings=updated;
   try{StoreSettings();}catch{settings=previous;throw;}
   File.Delete(path);
  }
 }
 void StageUpdateOverride(string folder,Version version) {
  string path=Path.Combine(folder,SettingsOverride.FileName);
  if(settings.StagedOverrideVersion==version.ToString()||!File.Exists(path))return;
  var patch=JsonNode.Parse(File.ReadAllText(path)) as JsonObject??throw new ArgumentException("更新用の上書き設定はJSONオブジェクトで指定してください。");
  var queued=new JsonObject {["Version"]=version.ToString(),["Settings"]=patch};
  Directory.CreateDirectory(DataDirectory);var target=Path.Combine(DataDirectory,"settings.override.pending.json");
  File.WriteAllText(target+".tmp",queued.ToJsonString());File.Move(target+".tmp",target,true);
  settings.StagedOverrideVersion=version.ToString();StoreSettings();
 }
}

